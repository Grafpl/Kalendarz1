/* WSZYSTKO_LIBRANET_109.sql — wygenerowano 2026-05-12 19:06:31 */

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

/* ===== ANALIZA PAULINY (ZAKUP ŻYWCA) ===== */

/* ============================================================================
   analiza_paulina_ZAKUP.sql — Analiza zakupu żywca (dziedzina Pauliny)
   ----------------------------------------------------------------------------
   Uruchom na: 192.168.0.109 / LibraNet (user pronova/pronova)

   KONTEKST:
   Paulina obsługuje dział zakupów (negocjacje z hodowcami żywca). Sergiusz
   planuje przesunąć Maję częściowo na zakup żywca — Maja musi:
     1. Utrzymać obecne ~25M obrotu sprzedażowego rocznie (29 klientów)
     2. Przejąć część operacji Pauliny: harmonogramy dostaw, ceny, hodowcy
     3. Rozwinąć portfel sprzedażowy

   Skrypt pokazuje WSZYSTKO co Paulina robi przez tabele LibraNet:
     • HarmonogramDostaw — plan zakupu żywca, kontrakty
     • Pozyskiwanie_Hodowcy + Aktywnosci — CRM hodowców (1874 leadów)
     • listapartii + PartiaDostawca — partie ubojowe od hodowców
     • FarmerCalc — rozliczenia finansowe z hodowcami
     • ReklamacjePartie — reklamacje na partie hodowców

   ============================================================================ */

USE [LibraNet];
GO

SET NOCOUNT ON;
SET ANSI_WARNINGS ON;

DECLARE @DataOd DATE = DATEADD(MONTH, -12, GETDATE());   -- 12 mies. wstecz
DECLARE @DataDo DATE = CAST(GETDATE() AS DATE);

/* ============================================================================
   ===  A. HARMONOGRAM DOSTAW ŻYWCA  ==========================================
   ============================================================================ */
SELECT N'A.1 — Skala harmonogramu dostaw żywca (12 mies.)' AS [Raport];

SELECT COUNT(*)                                                                                  AS LiczbaWierszy,
       SUM(CAST(CASE WHEN Bufor = 'Potwierdzony' THEN 1 ELSE 0 END AS INT))                       AS Potwierdzonych,
       SUM(CAST(CASE WHEN Bufor <> 'Potwierdzony' OR Bufor IS NULL THEN 1 ELSE 0 END AS INT))     AS Niepotwierdzonych,
       COUNT(DISTINCT DostawcaID)                                                                 AS UnikalnychDostawcow,
       CAST(SUM(CAST(SztukiDek AS DECIMAL(18,0))) AS DECIMAL(18,0))                               AS SztukDeklarowanych,
       CAST(SUM(CAST(SztukiDek AS DECIMAL(18,2)) * CAST(WagaDek AS DECIMAL(10,2))) AS DECIMAL(18,1)) AS KgDeklarowanych,
       CAST(SUM(CAST(PotwSztuki AS DECIMAL(18,0))) AS DECIMAL(18,0))                              AS SztukPotwierdzonych,
       CAST(SUM(CAST(PotwWaga AS DECIMAL(18,2))) AS DECIMAL(18,1))                                AS KgPotwierdzonych,
       MIN(DataOdbioru)                                                                           AS PierwszaDostawa,
       MAX(DataOdbioru)                                                                           AS OstatniaDostawa
FROM dbo.HarmonogramDostaw
WHERE DataOdbioru BETWEEN @DataOd AND @DataDo;

SELECT N'A.2 — Klasyfikacja TypCeny (kontrakt vs wolny rynek)' AS [Raport];

SELECT ISNULL(TypCeny, N'(brak)')                              AS TypCeny,
       CASE WHEN LOWER(ISNULL(TypCeny,N'')) IN (N'wolnyrynek',N'wolnorynkowa')
            THEN N'Wolny rynek' ELSE N'Kontrakt' END           AS Kategoria,
       COUNT(*)                                                AS LiczbaWierszy,
       COUNT(DISTINCT DostawcaID)                              AS UnikalnychDostawcow,
       CAST(SUM(SztukiDek * WagaDek) AS DECIMAL(18,1))         AS KgDeklarowanych,
       CAST(SUM(PotwWaga) AS DECIMAL(18,1))                    AS KgPotwierdzonych,
       CAST(AVG(CAST(Cena AS DECIMAL(10,4))) AS DECIMAL(10,2)) AS SredniaCena,
       CAST(MIN(Cena) AS DECIMAL(10,2))                        AS MinCena,
       CAST(MAX(Cena) AS DECIMAL(10,2))                        AS MaxCena
FROM dbo.HarmonogramDostaw
WHERE DataOdbioru BETWEEN @DataOd AND @DataDo
  AND Bufor = 'Potwierdzony'
GROUP BY ISNULL(TypCeny, N'(brak)'),
         CASE WHEN LOWER(ISNULL(TypCeny,N'')) IN (N'wolnyrynek',N'wolnorynkowa')
              THEN N'Wolny rynek' ELSE N'Kontrakt' END
ORDER BY KgPotwierdzonych DESC;

SELECT N'A.3 — Trend miesięczny: kontrakt vs wolny rynek (kg + cena)' AS [Raport];

SELECT CONVERT(CHAR(7), DataOdbioru, 120)                              AS RokMiesiac,
       CAST(SUM(CASE WHEN LOWER(ISNULL(TypCeny,N'')) IN (N'wolnyrynek',N'wolnorynkowa')
                     THEN PotwWaga ELSE 0 END) AS DECIMAL(18,1))        AS Wolny_Kg,
       CAST(SUM(CASE WHEN LOWER(ISNULL(TypCeny,N'')) NOT IN (N'wolnyrynek',N'wolnorynkowa')
                     THEN PotwWaga ELSE 0 END) AS DECIMAL(18,1))        AS Kontrakt_Kg,
       CAST(SUM(PotwWaga) AS DECIMAL(18,1))                             AS Razem_Kg,
       CAST(SUM(CASE WHEN LOWER(ISNULL(TypCeny,N'')) IN (N'wolnyrynek',N'wolnorynkowa')
                     THEN PotwWaga ELSE 0 END) * 100.0
            / NULLIF(SUM(PotwWaga), 0) AS DECIMAL(6,2))                 AS Wolny_Proc,
       -- średnia ważona cena per okres
       CAST(SUM(Cena * PotwWaga) / NULLIF(SUM(PotwWaga), 0) AS DECIMAL(10,2)) AS SredniaCenaWazona,
       CAST(SUM(Cena * SztukiDek) / NULLIF(SUM(SztukiDek), 0) AS DECIMAL(10,2)) AS SredniaCenaWgSztuk
FROM dbo.HarmonogramDostaw
WHERE DataOdbioru BETWEEN @DataOd AND @DataDo
  AND Bufor = 'Potwierdzony'
GROUP BY CONVERT(CHAR(7), DataOdbioru, 120)
ORDER BY RokMiesiac;

SELECT N'A.4 — TOP 30 dostawców żywca (12 mies., potwierdzone)' AS [Raport];

SELECT TOP 30
       DostawcaID,
       MAX(Dostawca)                                          AS Dostawca,
       COUNT(*)                                               AS LiczbaDostaw,
       SUM(Auta)                                              AS LacznieAut,
       CAST(SUM(SztukiDek) AS DECIMAL(18,0))                  AS SztukDeklar,
       CAST(SUM(PotwSztuki) AS DECIMAL(18,0))                 AS SztukPotw,
       CAST(SUM(SztukiDek * WagaDek) AS DECIMAL(18,1))        AS KgDeklar,
       CAST(SUM(PotwWaga) AS DECIMAL(18,1))                   AS KgPotw,
       CAST(SUM(Cena * PotwWaga) / NULLIF(SUM(PotwWaga), 0) AS DECIMAL(10,2)) AS SredniaCena,
       MIN(DataOdbioru) AS PierwszaDostawa,
       MAX(DataOdbioru) AS OstatniaDostawa,
       SUM(CASE WHEN LOWER(ISNULL(TypCeny,N'')) IN (N'wolnyrynek',N'wolnorynkowa') THEN 1 ELSE 0 END) AS DostawWolnyRynek,
       SUM(CASE WHEN LOWER(ISNULL(TypCeny,N'')) NOT IN (N'wolnyrynek',N'wolnorynkowa') THEN 1 ELSE 0 END) AS DostawKontrakt
FROM dbo.HarmonogramDostaw
WHERE DataOdbioru BETWEEN @DataOd AND @DataDo
  AND Bufor = 'Potwierdzony'
GROUP BY DostawcaID
ORDER BY KgPotw DESC;

SELECT N'A.5 — Kontrakty na przyszłość (potwierdzone, jeszcze nie dostarczone)' AS [Raport];

SELECT TOP 50
       DataOdbioru, DostawcaID, Dostawca, TypCeny, TypUmowy,
       CAST(SztukiDek AS DECIMAL(18,0)) AS SztukiDek,
       CAST(WagaDek AS DECIMAL(10,2)) AS WagaDekKg,
       CAST(SztukiDek * WagaDek AS DECIMAL(18,1)) AS KgPlanowanych,
       Auta, KmK, KmH, SztSzuflada,
       Cena, UWAGI,
       KtoStwo, DataUtw
FROM dbo.HarmonogramDostaw
WHERE Bufor = 'Potwierdzony'
  AND DataOdbioru >= CAST(GETDATE() AS DATE)
ORDER BY DataOdbioru;

/* ============================================================================
   ===  B. POZYSKIWANIE HODOWCÓW (CRM PAULINY)  ===============================
   ============================================================================ */
SELECT N'B.1 — Lejek hodowców: ile w każdym statusie' AS [Raport];

-- defensywnie: sprawdzam jakie kolumny ma Pozyskiwanie_Hodowcy
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Pozyskiwanie_Hodowcy' AND COLUMN_NAME='Status')
BEGIN
    SELECT Status,
           COUNT(*) AS Liczba
    FROM dbo.Pozyskiwanie_Hodowcy
    GROUP BY Status
    ORDER BY Liczba DESC;
END
ELSE
    SELECT N'⚠ Pozyskiwanie_Hodowcy nie ma kolumny Status — sprawdź eksploracja_LIBRANET sekcja 8' AS Info;

SELECT N'B.2 — Aktywności CRM ostatnio (12 mies.) — Paulina vs inni' AS [Raport];

IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='Pozyskiwanie_Aktywnosci')
BEGIN
    DECLARE @hasAutor BIT = (CASE WHEN EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                                                WHERE TABLE_NAME='Pozyskiwanie_Aktywnosci' AND COLUMN_NAME='Autor')
                                  THEN 1 ELSE 0 END);
    IF @hasAutor = 1
    BEGIN
        DECLARE @sqlB2 NVARCHAR(MAX) = N'
            SELECT Autor, COUNT(*) AS LiczbaAkcji,
                   MIN(DataAkcji) AS Pierwsza, MAX(DataAkcji) AS Ostatnia
            FROM dbo.Pozyskiwanie_Aktywnosci
            WHERE DataAkcji >= @DataOd
            GROUP BY Autor ORDER BY LiczbaAkcji DESC;';
        BEGIN TRY
            EXEC sp_executesql @sqlB2, N'@DataOd DATE', @DataOd = @DataOd;
        END TRY
        BEGIN CATCH SELECT N'⚠ Błąd B.2: ' + ERROR_MESSAGE() AS Info; END CATCH;
    END
    ELSE
    BEGIN
        SELECT N'⚠ Pozyskiwanie_Aktywnosci nie ma kolumny Autor — pokazuję strukturę:' AS Info;
        SELECT COLUMN_NAME, DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_NAME = 'Pozyskiwanie_Aktywnosci' ORDER BY ORDINAL_POSITION;
    END
END;

/* ============================================================================
   ===  C. PARTIE UBOJOWE — DIAGNOSTYKA (faktyczne kolumny listapartii)
   ============================================================================ */
SELECT N'C.0 — DIAGNOSTYKA: kolumny tabeli listapartii (uzupełnimy raporty po wyniku)' AS [Raport];

SELECT COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH AS Dlugosc, IS_NULLABLE, ORDINAL_POSITION
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'listapartii'
ORDER BY ORDINAL_POSITION;

SELECT N'C.1 — Sample TOP 5 z listapartii (zobacz jakie są nazwy kolumn)' AS [Raport];
SELECT TOP 5 * FROM dbo.listapartii ORDER BY 1 DESC;

SELECT N'C.2 — Tabela PartiaDostawca — sample 5 wierszy' AS [Raport];
SELECT TOP 5 * FROM dbo.PartiaDostawca ORDER BY 1 DESC;

SELECT N'C.3 — Reklamacje na partie od dostawców (ReklamacjePartie)' AS [Raport];

IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='ReklamacjePartie')
BEGIN
    SELECT rp.CustomerID, rp.CustomerName,
           COUNT(*) AS LiczbaReklamacji,
           COUNT(DISTINCT rp.NumerPartii) AS PartiiZReklamacja,
           MIN(rp.DataDodania) AS Pierwsza,
           MAX(rp.DataDodania) AS Ostatnia
    FROM dbo.ReklamacjePartie rp
    WHERE rp.DataDodania >= @DataOd
    GROUP BY rp.CustomerID, rp.CustomerName
    ORDER BY LiczbaReklamacji DESC;
END
ELSE
    SELECT N'⚠ ReklamacjePartie nie istnieje' AS Info;

/* ============================================================================
   ===  D. ROZLICZENIA FINANSOWE Z HODOWCAMI (FarmerCalc)  ====================
   ============================================================================ */
SELECT N'D.1 — Struktura kolumn FarmerCalc' AS [Raport];
SELECT COLUMN_NAME, DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'FarmerCalc' ORDER BY ORDINAL_POSITION;

SELECT N'D.2 — Sample 10 ostatnich wpisów FarmerCalc' AS [Raport];
SELECT TOP 10 * FROM dbo.FarmerCalc ORDER BY 1 DESC;

/* ============================================================================
   ===  E. CYKL DOSTAW (dni tygodnia, sezonowość)  =============================
   ============================================================================ */
SELECT N'E.1 — Dni tygodnia: kiedy hodowcy dostarczają (1=Nd, 2=Pn, ..., 7=Sb)' AS [Raport];

SELECT DATEPART(WEEKDAY, DataOdbioru) AS DzienTyg,
       CASE DATEPART(WEEKDAY, DataOdbioru)
            WHEN 1 THEN N'Niedziela' WHEN 2 THEN N'Poniedziałek' WHEN 3 THEN N'Wtorek'
            WHEN 4 THEN N'Środa'     WHEN 5 THEN N'Czwartek'     WHEN 6 THEN N'Piątek'
            WHEN 7 THEN N'Sobota' END AS Dzien,
       COUNT(*) AS LiczbaDostaw,
       CAST(SUM(PotwWaga) AS DECIMAL(18,1)) AS KgRazem,
       SUM(Auta) AS LacznieAut
FROM dbo.HarmonogramDostaw
WHERE DataOdbioru BETWEEN @DataOd AND @DataDo
  AND Bufor = 'Potwierdzony'
GROUP BY DATEPART(WEEKDAY, DataOdbioru)
ORDER BY DzienTyg;

SELECT N'E.2 — Miesiące roku: sezonowość zakupu żywca' AS [Raport];

SELECT MONTH(DataOdbioru) AS Miesiac,
       COUNT(*) AS LiczbaDostaw,
       CAST(SUM(PotwWaga) AS DECIMAL(18,1)) AS KgRazem,
       CAST(SUM(Cena * PotwWaga) / NULLIF(SUM(PotwWaga), 0) AS DECIMAL(10,2)) AS SredCenaWaz
FROM dbo.HarmonogramDostaw
WHERE Bufor = 'Potwierdzony'
GROUP BY MONTH(DataOdbioru)
ORDER BY Miesiac;

/* ============================================================================
   ===  F. ROZRZUT CEN ŻYWCA (zmienność rynku)  ===============================
   ============================================================================ */
SELECT N'F.1 — Histogram cen żywca (kontrakt 4.40-5.23, wolny rynek ~4.00)' AS [Raport];

SELECT
    CASE
        WHEN Cena < 3.50 THEN N'01 <3.50'
        WHEN Cena < 4.00 THEN N'02 3.50-4.00'
        WHEN Cena < 4.20 THEN N'03 4.00-4.20'
        WHEN Cena < 4.40 THEN N'04 4.20-4.40'
        WHEN Cena < 4.60 THEN N'05 4.40-4.60'
        WHEN Cena < 4.80 THEN N'06 4.60-4.80'
        WHEN Cena < 5.00 THEN N'07 4.80-5.00'
        WHEN Cena < 5.20 THEN N'08 5.00-5.20'
        WHEN Cena < 5.50 THEN N'09 5.20-5.50'
        ELSE                  N'10 5.50+'
    END AS Bucket_Cena,
    COUNT(*) AS LiczbaDostaw,
    CAST(SUM(PotwWaga) AS DECIMAL(18,1)) AS KgRazem,
    COUNT(DISTINCT DostawcaID) AS DostawcowUnikalnych
FROM dbo.HarmonogramDostaw
WHERE Bufor = 'Potwierdzony'
  AND DataOdbioru BETWEEN @DataOd AND @DataDo
  AND Cena IS NOT NULL AND Cena > 0
GROUP BY CASE
        WHEN Cena < 3.50 THEN N'01 <3.50'
        WHEN Cena < 4.00 THEN N'02 3.50-4.00'
        WHEN Cena < 4.20 THEN N'03 4.00-4.20'
        WHEN Cena < 4.40 THEN N'04 4.20-4.40'
        WHEN Cena < 4.60 THEN N'05 4.40-4.60'
        WHEN Cena < 4.80 THEN N'06 4.60-4.80'
        WHEN Cena < 5.00 THEN N'07 4.80-5.00'
        WHEN Cena < 5.20 THEN N'08 5.00-5.20'
        WHEN Cena < 5.50 THEN N'09 5.20-5.50'
        ELSE                  N'10 5.50+' END
ORDER BY Bucket_Cena;

/* ============================================================================
   ===  G. ANALIZA PORÓWNAWCZA: ile pracy ma Paulina  =========================
   ============================================================================ */
SELECT N'G.1 — Skala działania Pauliny: ile dostaw / hodowców rocznie' AS [Raport];

SELECT
    CAST(SUM(PotwWaga) / 1000 AS DECIMAL(18,1))                          AS Ton_Zywca_Rocznie,
    COUNT(*)                                                              AS Dostaw_Rocznie,
    COUNT(DISTINCT DostawcaID)                                            AS Unikalnych_Hodowcow,
    CAST(SUM(Cena * PotwWaga) AS DECIMAL(18,0))                          AS Wartosc_Zakupu_PLN,
    CAST(SUM(Cena * PotwWaga) / 1000000.0 AS DECIMAL(8,1))                AS Wartosc_Zakupu_M_PLN,
    CAST(SUM(CASE WHEN LOWER(ISNULL(TypCeny,N'')) IN (N'wolnyrynek',N'wolnorynkowa')
                  THEN PotwWaga ELSE 0 END) * 100.0
         / NULLIF(SUM(PotwWaga), 0) AS DECIMAL(6,2))                     AS Wolny_Rynek_Proc,
    COUNT(*) / 52                                                         AS Sredn_Dostaw_Tygodniowo,
    COUNT(*) / 12                                                         AS Sredn_Dostaw_Miesiecznie
FROM dbo.HarmonogramDostaw
WHERE Bufor = 'Potwierdzony'
  AND DataOdbioru BETWEEN @DataOd AND @DataDo;

SELECT N'G.2 — Aktywność per autor (kto wprowadza harmonogramy = czyja to praca)' AS [Raport];

SELECT ISNULL(KtoStwo, N'(brak)') AS KtoWprowadzil,
       COUNT(*) AS LiczbaWpisow,
       SUM(CAST(CASE WHEN Bufor = 'Potwierdzony' THEN 1 ELSE 0 END AS INT)) AS Potwierdzonych,
       MIN(DataUtw) AS Pierwszy,
       MAX(DataUtw) AS Ostatni
FROM dbo.HarmonogramDostaw
WHERE DataUtw >= @DataOd
GROUP BY ISNULL(KtoStwo, N'(brak)')
ORDER BY LiczbaWpisow DESC;

SELECT N'G.3 — Zakończono analizę zakupu żywca (Paulina)' AS Info;

/* ===== ANALIZA MAI (LIBRANET) ===== */

/* ============================================================================
   analiza_maja_LIBRANET.sql  (v4 — rozbudowane, ~30 raportów)
   ----------------------------------------------------------------------------
   Uruchom na: 192.168.0.109 / LibraNet (user pronova/pronova)

   Sekcje:
     G — Zamówienia (8 raportów: wolumen, czas, fakturowanie, pakowanie, modyfikacje, anulacje)
     I — Zamówienie → Wydanie różnice (3 raporty: precyzja obietnic Mai)
     H — Reklamacje (6 raportów: typy, przyczyny, decyzje jakości, czas)
     J — CRM / Notatki / Telefony (5 raportów: aktywność operacyjna)
     K — Scorecard zbiorczy

   ============================================================================
   ŹRÓDŁA PRAWDY (potwierdzone diagnostyką 2026-05-12)
   ============================================================================
   • ZamowieniaMieso.IdUser → MapowanieHandlowcow.UserId → HandlowiecNazwa
   • ZamowieniaMieso.KlientId = STContractors.id (klient/odbiorca)
   • ZamowieniaMieso.DataPrzyjazdu = data odbioru klienta
   • Reklamacje.Handlowiec = nazwa wprost
   • ZamowienieWydanieRoznice = "obiecaliśmy X kg, wydaliśmy Y kg, różnica Z kg"
   • HistoriaZmianZamowien = audit log zmian zamówień (TypZmiany, Uzytkownik, DataZmiany)
   • Notatkiużycia.DataAkcji + Akcja (Wpisana/Wstawiona)
   • WlascicieleOdbiorcow = formalne przypisanie klienta do operatora
   ============================================================================ */

USE [LibraNet];
GO

SET NOCOUNT ON;
SET ANSI_WARNINGS ON;

DECLARE @DataOd       DATE          = '2025-10-01';
DECLARE @DataDo       DATE          = CAST(GETDATE() AS DATE);
DECLARE @HandlowiecMaja NVARCHAR(255) = N'Maja';

-- ⚠ Wklej tu listę ID kontrahentów Mai z analiza_maja_HANDEL.sql raport 0.2b
DECLARE @KlienciMaiCSV NVARCHAR(MAX) = NULL;
-- przykład: N'237,540,1288,4772,4779,4809,4820,4837,4845,4932,5049,5183,5207,5225,5228,5339,5410,5422,5431,5459,5467,5523,5541,5583,5595,5596,5597,5665,6739';

-- ============================================================================
-- TEMP TABLES
-- ============================================================================
IF OBJECT_ID('tempdb..#KlienciMai') IS NOT NULL DROP TABLE #KlienciMai;
CREATE TABLE #KlienciMai (KontrahentId INT PRIMARY KEY);

IF @KlienciMaiCSV IS NOT NULL AND LEN(@KlienciMaiCSV) > 0
BEGIN
    DECLARE @xml XML = CAST(N'<r>' + REPLACE(@KlienciMaiCSV, N',', N'</r><r>') + N'</r>' AS XML);
    INSERT INTO #KlienciMai (KontrahentId)
    SELECT DISTINCT CAST(LTRIM(RTRIM(t.value('.', 'NVARCHAR(50)'))) AS INT)
    FROM @xml.nodes('/r') AS X(t)
    WHERE LTRIM(RTRIM(t.value('.', 'NVARCHAR(50)'))) <> N'';
END

IF OBJECT_ID('tempdb..#UserIdMaja') IS NOT NULL DROP TABLE #UserIdMaja;
SELECT UserID INTO #UserIdMaja FROM dbo.UserHandlowcy WHERE HandlowiecName = @HandlowiecMaja;

-- ============================================================================
-- 0. DIAGNOSTYKA + bazowa tabela zamówień
-- ============================================================================
SELECT N'0.0 — UserID Mai (UserHandlowcy + MapowanieHandlowcow)' AS [Raport];
SELECT * FROM dbo.UserHandlowcy WHERE HandlowiecName = @HandlowiecMaja;
SELECT * FROM dbo.MapowanieHandlowcow WHERE HandlowiecNazwa = @HandlowiecMaja;

-- Budowa #ZamBaza
IF OBJECT_ID('tempdb..#ZamBaza') IS NOT NULL DROP TABLE #ZamBaza;
SELECT
    z.Id                                                                       AS ZamowienieId,
    COALESCE(uh.HandlowiecName, mh.HandlowiecNazwa, N'(nieznany)')             AS Handlowiec,
    z.IdUser                                                                   AS IdUser,
    z.KlientId                                                                 AS KlientId,
    z.DataZamowienia,
    z.DataPrzyjazdu                                                            AS DataOdbioru,
    z.Status,
    CASE WHEN z.AnulowanePrzez IS NOT NULL OR z.DataAnulowania IS NOT NULL
         THEN CAST(1 AS BIT) ELSE CAST(0 AS BIT) END                           AS Anulowane,
    z.AnulowanePrzez, z.PrzyczynaAnulowania, z.DataAnulowania,
    z.TransportStatus, z.Strefa, z.TrybE2,
    z.CzyZrealizowane, z.CzyWydane, z.CzyZafakturowane, z.CzyWszystkoWydane,
    z.NumerFaktury, z.NumerWZ, z.DataWydania, z.DataWystawieniaWZ,
    z.ProcentRealizacji, z.CzyCzesciowoZrealizowane,
    z.LiczbaPojemnikow, z.LiczbaPalet,
    SUM(zt.Ilosc)                                                              AS SumaKg,
    SUM(zt.Ilosc * ISNULL(TRY_CAST(NULLIF(zt.Cena, N'') AS DECIMAL(18,2)), 0)) AS SumaWartosc,
    SUM(ISNULL(zt.IloscZrealizowana, 0))                                       AS SumaKgZrealizowana,
    SUM(CASE WHEN zt.E2 = 1 THEN ISNULL(zt.Ilosc,0) ELSE 0 END)                AS KgE2,
    SUM(CASE WHEN zt.Folia = 1 THEN ISNULL(zt.Ilosc,0) ELSE 0 END)             AS KgFolia,
    SUM(CASE WHEN zt.Hallal = 1 THEN ISNULL(zt.Ilosc,0) ELSE 0 END)            AS KgHallal,
    SUM(CASE WHEN zt.Strefa = 1 THEN ISNULL(zt.Ilosc,0) ELSE 0 END)            AS KgStrefa,
    COUNT(zt.Id)                                                               AS LiczbaPozycji
INTO #ZamBaza
FROM dbo.ZamowieniaMieso z
LEFT JOIN dbo.ZamowieniaMiesoTowar zt   ON zt.ZamowienieId = z.Id
LEFT JOIN dbo.UserHandlowcy uh          ON uh.UserID = CAST(z.IdUser AS NVARCHAR(50))
LEFT JOIN dbo.MapowanieHandlowcow mh    ON mh.UserId = CAST(z.IdUser AS NVARCHAR(50))
                                       AND mh.CzyAktywny = 1
WHERE z.DataZamowienia >= '2025-07-01'
  AND z.DataZamowienia <  DATEADD(DAY, 1, @DataDo)
GROUP BY z.Id, uh.HandlowiecName, mh.HandlowiecNazwa, z.IdUser, z.KlientId,
         z.DataZamowienia, z.DataPrzyjazdu, z.Status, z.AnulowanePrzez,
         z.PrzyczynaAnulowania, z.DataAnulowania, z.TransportStatus, z.Strefa, z.TrybE2,
         z.CzyZrealizowane, z.CzyWydane, z.CzyZafakturowane, z.CzyWszystkoWydane,
         z.NumerFaktury, z.NumerWZ, z.DataWydania, z.DataWystawieniaWZ,
         z.ProcentRealizacji, z.CzyCzesciowoZrealizowane,
         z.LiczbaPojemnikow, z.LiczbaPalet;

CREATE INDEX IX_ZB_H ON #ZamBaza(Handlowiec, DataOdbioru) INCLUDE (KlientId, SumaKg, SumaWartosc, Anulowane);

SELECT N'0.1 — Sanity check: rozkład #ZamBaza per Handlowiec' AS [Raport];
SELECT Handlowiec, COUNT(*) AS LiczbaZam,
       MIN(DataZamowienia) AS Pierwsza, MAX(DataZamowienia) AS Ostatnia,
       CAST(SUM(SumaWartosc) AS DECIMAL(18,2)) AS SumaWartosc
FROM #ZamBaza GROUP BY Handlowiec ORDER BY LiczbaZam DESC;

/* ============================================================================
   ===  G. ZAMÓWIENIA  =========================================================
   ============================================================================ */
SELECT N'G.1 — Zamówienia Mai per miesiąc' AS [Raport];

SELECT CONVERT(CHAR(7), DataOdbioru, 120)             AS RokMiesiac,
       COUNT(*)                                       AS LiczbaZamowien,
       SUM(CAST(CASE WHEN Anulowane = 1 THEN 1 ELSE 0 END AS INT)) AS LiczbaAnulowanych,
       CAST(SUM(SumaKg) AS DECIMAL(18,1))             AS SumaKg,
       CAST(SUM(SumaWartosc) AS DECIMAL(18,2))        AS SumaWartoscZam,
       CAST(SUM(SumaKgZrealizowana) AS DECIMAL(18,1)) AS SumaKgZrealiz,
       CAST(SUM(SumaWartosc) / NULLIF(SUM(SumaKg), 0) AS DECIMAL(10,2)) AS SredniaCenaZlKg,
       CAST(SUM(CAST(CASE WHEN Anulowane = 1 THEN 1 ELSE 0 END AS INT)) * 100.0 / NULLIF(COUNT(*),0) AS DECIMAL(6,2)) AS Anulow_Proc,
       CAST(SUM(SumaKgZrealizowana) * 100.0 / NULLIF(SUM(SumaKg), 0) AS DECIMAL(6,2)) AS Realizacji_Proc
FROM #ZamBaza
WHERE Handlowiec = @HandlowiecMaja
  AND DataOdbioru BETWEEN @DataOd AND @DataDo
GROUP BY CONVERT(CHAR(7), DataOdbioru, 120)
ORDER BY RokMiesiac;

SELECT N'G.2 — Zamówienia per handlowiec (benchmark)' AS [Raport];

SELECT Handlowiec,
       COUNT(*)                                                      AS LiczbaZam,
       SUM(CAST(CASE WHEN Anulowane = 1 THEN 1 ELSE 0 END AS INT))                AS Anulowanych,
       CAST(SUM(CAST(CASE WHEN Anulowane = 1 THEN 1 ELSE 0 END AS INT)) * 100.0 / NULLIF(COUNT(*),0) AS DECIMAL(6,2)) AS Anulow_Proc,
       SUM(CASE WHEN ISNULL(CzyZafakturowane, 0) = 1 THEN 1 ELSE 0 END) AS Zafakturowanych,
       CAST(SUM(CASE WHEN ISNULL(CzyZafakturowane, 0) = 1 THEN 1 ELSE 0 END) * 100.0 / NULLIF(COUNT(*),0) AS DECIMAL(6,2)) AS Zafakt_Proc,
       SUM(CASE WHEN ISNULL(CzyWydane, 0) = 1 THEN 1 ELSE 0 END)     AS Wydanych,
       CAST(SUM(SumaKg) AS DECIMAL(18,1))                            AS SumaKg,
       CAST(SUM(SumaWartosc) AS DECIMAL(18,2))                       AS SumaWartosc,
       COUNT(DISTINCT KlientId)                                      AS LiczbaKlientow,
       CAST(SUM(SumaKgZrealizowana) * 100.0 / NULLIF(SUM(SumaKg),0) AS DECIMAL(6,2)) AS Realizacji_Proc
FROM #ZamBaza
WHERE DataOdbioru BETWEEN @DataOd AND @DataDo
  AND Handlowiec NOT IN (N'(nieznany)')
GROUP BY Handlowiec
ORDER BY SumaWartosc DESC;

SELECT N'G.3 — Średni czas zamówienie → odbiór (planowanie z wyprzedzeniem)' AS [Raport];

SELECT Handlowiec, COUNT(*) AS LiczbaZam,
       CAST(AVG(CAST(DATEDIFF(DAY, DataZamowienia, DataOdbioru) AS DECIMAL(10,2))) AS DECIMAL(8,2)) AS SredniDniDoOdbioru,
       MIN(DATEDIFF(DAY, DataZamowienia, DataOdbioru)) AS Min_Dni,
       MAX(DATEDIFF(DAY, DataZamowienia, DataOdbioru)) AS Max_Dni,
       SUM(CASE WHEN DATEDIFF(DAY, DataZamowienia, DataOdbioru) = 0 THEN 1 ELSE 0 END) AS NaTenSamDzien,
       SUM(CASE WHEN DATEDIFF(DAY, DataZamowienia, DataOdbioru) = 1 THEN 1 ELSE 0 END) AS NaJutro,
       SUM(CASE WHEN DATEDIFF(DAY, DataZamowienia, DataOdbioru) >= 7 THEN 1 ELSE 0 END) AS NaTydzienPlus
FROM #ZamBaza
WHERE DataOdbioru BETWEEN @DataOd AND @DataDo
  AND DataZamowienia IS NOT NULL AND DataOdbioru IS NOT NULL
  AND Handlowiec NOT IN (N'(nieznany)')
GROUP BY Handlowiec
ORDER BY SredniDniDoOdbioru;

SELECT N'G.4 — Top klienci Mai w LibraNet (per zamówienia)' AS [Raport];

SELECT TOP 30
       KlientId,
       COUNT(*)                                       AS LiczbaZam,
       CAST(SUM(SumaKg) AS DECIMAL(18,1))             AS SumaKg,
       CAST(SUM(SumaWartosc) AS DECIMAL(18,2))        AS SumaWartoscZam,
       MIN(DataZamowienia)                            AS Pierwsza,
       MAX(DataZamowienia)                            AS Ostatnia,
       DATEDIFF(DAY, MAX(DataZamowienia), @DataDo)    AS DniOdOstatniej,
       SUM(CAST(CASE WHEN Anulowane = 1 THEN 1 ELSE 0 END AS INT)) AS Anulowanych,
       CAST(SUM(SumaKgZrealizowana) * 100.0 / NULLIF(SUM(SumaKg),0) AS DECIMAL(6,2)) AS Realiz_Proc,
       CASE WHEN EXISTS (SELECT 1 FROM #KlienciMai k WHERE k.KontrahentId = KlientId)
            THEN N'POTWIERDZONY KLIENT MAI (z HANDEL)' ELSE N'(brak weryfikacji HANDEL)' END AS Match_HANDEL
FROM #ZamBaza
WHERE Handlowiec = @HandlowiecMaja
  AND DataOdbioru BETWEEN @DataOd AND @DataDo
GROUP BY KlientId
ORDER BY SumaWartoscZam DESC;

SELECT N'G.5 — Mix pakowania Mai vs benchmark (E2 / Folia / Hallal / Strefa)' AS [Raport];

SELECT Handlowiec,
       CAST(SUM(SumaKg) AS DECIMAL(18,1))             AS SumaKg,
       CAST(SUM(KgE2) AS DECIMAL(18,1))               AS KgE2,
       CAST(SUM(KgFolia) AS DECIMAL(18,1))            AS KgFolia,
       CAST(SUM(KgHallal) AS DECIMAL(18,1))           AS KgHallal,
       CAST(SUM(KgStrefa) AS DECIMAL(18,1))           AS KgStrefa,
       CAST(SUM(KgE2) * 100.0 / NULLIF(SUM(SumaKg),0) AS DECIMAL(6,2)) AS E2_Proc,
       CAST(SUM(KgFolia) * 100.0 / NULLIF(SUM(SumaKg),0) AS DECIMAL(6,2)) AS Folia_Proc,
       CAST(SUM(KgHallal) * 100.0 / NULLIF(SUM(SumaKg),0) AS DECIMAL(6,2)) AS Hallal_Proc,
       CAST(SUM(KgStrefa) * 100.0 / NULLIF(SUM(SumaKg),0) AS DECIMAL(6,2)) AS Strefa_Proc
FROM #ZamBaza
WHERE DataOdbioru BETWEEN @DataOd AND @DataDo
  AND Handlowiec NOT IN (N'(nieznany)')
GROUP BY Handlowiec
ORDER BY SumaKg DESC;

SELECT N'G.6 — Modyfikacje zamówień Mai (HistoriaZmianZamowien) — jak często poprawia' AS [Raport];

;WITH ZamMaja AS (
    SELECT ZamowienieId FROM #ZamBaza WHERE Handlowiec = @HandlowiecMaja
      AND DataOdbioru BETWEEN @DataOd AND @DataDo
),
ZmianyAgg AS (
    SELECT h.TypZmiany,
           COUNT(*) AS LiczbaZmian,
           COUNT(DISTINCT h.ZamowienieId) AS LiczbaZamow,
           MIN(h.DataZmiany) AS Najwczesniej, MAX(h.DataZmiany) AS Najpozniej
    FROM dbo.HistoriaZmianZamowien h
    INNER JOIN ZamMaja zm ON zm.ZamowienieId = h.ZamowienieId
    WHERE h.DataZmiany BETWEEN @DataOd AND DATEADD(DAY,1,@DataDo)
    GROUP BY h.TypZmiany
)
SELECT TypZmiany, LiczbaZmian, LiczbaZamow, Najwczesniej, Najpozniej,
       CAST(LiczbaZmian * 1.0 / NULLIF(LiczbaZamow,0) AS DECIMAL(6,2)) AS ZmianPerZamowienie
FROM ZmianyAgg ORDER BY LiczbaZmian DESC;

SELECT N'G.7 — Modyfikacje per handlowiec (benchmark — ile poprawiają swoje zamówienia)' AS [Raport];

SELECT zb.Handlowiec,
       COUNT(DISTINCT zb.ZamowienieId)                AS ZamowienOgolem,
       COUNT(h.Id)                                     AS LacznieZmian,
       COUNT(DISTINCT h.ZamowienieId)                 AS ZamowienZeZmianami,
       CAST(COUNT(DISTINCT h.ZamowienieId) * 100.0
            / NULLIF(COUNT(DISTINCT zb.ZamowienieId),0) AS DECIMAL(6,2)) AS Proc_ZamZeZmianami,
       CAST(COUNT(h.Id) * 1.0 / NULLIF(COUNT(DISTINCT zb.ZamowienieId),0) AS DECIMAL(8,2)) AS Sredn_ZmianNaZam
FROM #ZamBaza zb
LEFT JOIN dbo.HistoriaZmianZamowien h ON h.ZamowienieId = zb.ZamowienieId
                                      AND h.DataZmiany BETWEEN @DataOd AND DATEADD(DAY,1,@DataDo)
WHERE zb.DataOdbioru BETWEEN @DataOd AND @DataDo
  AND zb.Handlowiec NOT IN (N'(nieznany)')
GROUP BY zb.Handlowiec
ORDER BY Sredn_ZmianNaZam DESC;

SELECT N'G.8 — Powody anulowania zamówień Mai' AS [Raport];

SELECT ISNULL(PrzyczynaAnulowania, N'(brak powodu)') AS Powod,
       COUNT(*) AS Liczba,
       CAST(SUM(SumaKg) AS DECIMAL(18,1))      AS SumaKgUtraconych,
       CAST(SUM(SumaWartosc) AS DECIMAL(18,2)) AS SumaWartUtraconych,
       MIN(DataAnulowania) AS Najwczesniej, MAX(DataAnulowania) AS Najpozniej
FROM #ZamBaza
WHERE Handlowiec = @HandlowiecMaja
  AND Anulowane = 1
  AND DataOdbioru BETWEEN @DataOd AND @DataDo
GROUP BY ISNULL(PrzyczynaAnulowania, N'(brak powodu)')
ORDER BY Liczba DESC;

/* ============================================================================
   ===  I. ZAMÓWIENIE → WYDANIE RÓŻNICE  =======================================
   ============================================================================ */
SELECT N'I.1 — Łączne różnice zamówienie vs wydanie per handlowiec' AS [Raport];

SELECT zb.Handlowiec,
       COUNT(DISTINCT zwr.ZamowienieId)                          AS ZamowienZRoznicami,
       COUNT(zwr.Id)                                              AS LiczbaPozycjiZRoznica,
       CAST(SUM(zwr.IloscZamowiona) AS DECIMAL(18,1))              AS KgZamowionych,
       CAST(SUM(zwr.IloscWydana) AS DECIMAL(18,1))                 AS KgWydanych,
       CAST(SUM(zwr.Roznica) AS DECIMAL(18,1))                     AS Roznica_Kg,
       CAST(SUM(zwr.Roznica) * 100.0 / NULLIF(SUM(zwr.IloscZamowiona),0) AS DECIMAL(6,2)) AS Roznica_Proc,
       SUM(CAST(CASE WHEN zwr.Roznica < 0 THEN 1 ELSE 0 END AS INT))            AS Pozycji_Brak,    -- mniej wydano niż obiecano
       SUM(CAST(CASE WHEN zwr.Roznica > 0 THEN 1 ELSE 0 END AS INT))            AS Pozycji_Wiecej   -- więcej wydano (bonus)
FROM dbo.ZamowienieWydanieRoznice zwr
INNER JOIN #ZamBaza zb ON zb.ZamowienieId = zwr.ZamowienieId
WHERE zb.DataOdbioru BETWEEN @DataOd AND @DataDo
  AND zb.Handlowiec NOT IN (N'(nieznany)')
GROUP BY zb.Handlowiec
ORDER BY Roznica_Proc;

SELECT N'I.2 — TOP 20 pozycji Mai z największą różnicą (ucinanie/nadwyżki)' AS [Raport];

SELECT TOP 20
       zwr.ZamowienieId, zwr.KodTowaru,
       zb.KlientId,
       zb.DataOdbioru,
       CAST(zwr.IloscZamowiona AS DECIMAL(18,1)) AS Zamowiono,
       CAST(zwr.IloscWydana AS DECIMAL(18,1))    AS Wydano,
       CAST(zwr.Roznica AS DECIMAL(18,1))        AS Roznica,
       CAST(zwr.Roznica * 100.0 / NULLIF(zwr.IloscZamowiona,0) AS DECIMAL(6,2)) AS Roznica_Proc,
       zwr.DataWpisu
FROM dbo.ZamowienieWydanieRoznice zwr
INNER JOIN #ZamBaza zb ON zb.ZamowienieId = zwr.ZamowienieId
WHERE zb.Handlowiec = @HandlowiecMaja
  AND zb.DataOdbioru BETWEEN @DataOd AND @DataDo
ORDER BY ABS(zwr.Roznica) DESC;

SELECT N'I.3 — Powody braku towaru w pozycjach Mai (ZamowieniaMiesoTowar.PowodBraku)' AS [Raport];

SELECT ISNULL(zt.PowodBraku, N'(brak powodu)') AS Powod,
       COUNT(*) AS LiczbaPozycji,
       CAST(SUM(zt.Ilosc - ISNULL(zt.IloscZrealizowana, 0)) AS DECIMAL(18,1)) AS KgUcietych
FROM dbo.ZamowieniaMieso z
INNER JOIN dbo.ZamowieniaMiesoTowar zt ON zt.ZamowienieId = z.Id
INNER JOIN dbo.UserHandlowcy uh ON uh.UserID = CAST(z.IdUser AS NVARCHAR(50))
WHERE z.DataPrzyjazdu BETWEEN @DataOd AND @DataDo
  AND uh.HandlowiecName = @HandlowiecMaja
  AND zt.PowodBraku IS NOT NULL AND zt.PowodBraku <> N''
GROUP BY ISNULL(zt.PowodBraku, N'(brak powodu)')
ORDER BY KgUcietych DESC;

/* ============================================================================
   ===  H. REKLAMACJE  =========================================================
   ============================================================================ */
SELECT N'H.1 — Reklamacje Mai (lista pełna)' AS [Raport];

SELECT r.Id, r.DataZgloszenia, r.NumerDokumentu, r.IdKontrahenta, r.NazwaKontrahenta,
       r.TypReklamacji, r.Status, r.StatusV2, r.Priorytet,
       r.UserID AS ZglaszajacyUserID, r.Handlowiec,
       CAST(r.SumaKg AS DECIMAL(18,2))         AS SumaKg,
       CAST(r.SumaWartosc AS DECIMAL(18,2))    AS SumaWartosc,
       CAST(r.KosztReklamacji AS DECIMAL(18,2)) AS KosztReklamacji,
       r.DecyzjaJakosci, r.KategoriaPrzyczyny, r.PodkategoriaPrzyczyny,
       r.DataZamkniecia,
       DATEDIFF(DAY, r.DataZgloszenia, ISNULL(r.DataZamkniecia, GETDATE())) AS DniRozpatrywania
FROM dbo.Reklamacje r
WHERE r.DataZgloszenia BETWEEN @DataOd AND @DataDo
  AND r.Handlowiec = @HandlowiecMaja
ORDER BY r.DataZgloszenia DESC;

SELECT N'H.2 — Reklamacje per Handlowiec (benchmark)' AS [Raport];

SELECT ISNULL(r.Handlowiec, N'(brak)')                 AS Handlowiec,
       COUNT(*)                                        AS LiczbaReklamacji,
       SUM(CAST(CASE WHEN r.TypReklamacji = N'Jakosc produktu' THEN 1 ELSE 0 END AS INT))         AS Liczba_Jakosciowych,
       SUM(CAST(CASE WHEN r.TypReklamacji = N'Ilosc / Brak towaru' THEN 1 ELSE 0 END AS INT))     AS Liczba_IloscBrak,
       SUM(CAST(CASE WHEN r.TypReklamacji = N'Faktura korygujaca' THEN 1 ELSE 0 END AS INT))      AS Liczba_AutoFKS,
       SUM(CASE WHEN r.TypReklamacji NOT IN (N'Jakosc produktu', N'Ilosc / Brak towaru', N'Faktura korygujaca')
                 OR r.TypReklamacji IS NULL THEN 1 ELSE 0 END)                       AS Liczba_Inne,
       CAST(SUM(ISNULL(r.SumaWartosc,0)) AS DECIMAL(18,2)) AS WartoscRekl,
       CAST(SUM(ISNULL(r.KosztReklamacji,0)) AS DECIMAL(18,2)) AS KosztRekl,
       CAST(AVG(CAST(DATEDIFF(DAY, r.DataZgloszenia, ISNULL(r.DataZamkniecia, GETDATE())) AS DECIMAL(10,2)))
            AS DECIMAL(8,2)) AS SredniDniRozpatrywania
FROM dbo.Reklamacje r
WHERE r.DataZgloszenia BETWEEN @DataOd AND @DataDo
GROUP BY ISNULL(r.Handlowiec, N'(brak)')
ORDER BY LiczbaReklamacji DESC;

SELECT N'H.3 — Reklamacje Mai per typ (jakość vs ilość vs auto-import)' AS [Raport];

SELECT ISNULL(r.TypReklamacji, N'(brak typu)') AS TypReklamacji,
       COUNT(*) AS Liczba,
       CAST(SUM(ISNULL(r.SumaWartosc,0)) AS DECIMAL(18,2)) AS WartoscRazem,
       CAST(SUM(ISNULL(r.KosztReklamacji,0)) AS DECIMAL(18,2)) AS KosztRazem,
       CAST(SUM(ISNULL(r.SumaKg,0)) AS DECIMAL(18,1)) AS KgRazem
FROM dbo.Reklamacje r
WHERE r.DataZgloszenia BETWEEN @DataOd AND @DataDo
  AND r.Handlowiec = @HandlowiecMaja
GROUP BY ISNULL(r.TypReklamacji, N'(brak typu)')
ORDER BY Liczba DESC;

SELECT N'H.4 — Kategorie przyczyn reklamacji Mai (gdzie konkretnie szwankuje)' AS [Raport];

SELECT ISNULL(r.KategoriaPrzyczyny, N'(brak)')      AS KategoriaPrzyczyny,
       ISNULL(r.PodkategoriaPrzyczyny, N'(brak)')   AS PodkategoriaPrzyczyny,
       COUNT(*)                                     AS Liczba,
       CAST(SUM(ISNULL(r.SumaKg,0)) AS DECIMAL(18,1)) AS KgRazem,
       CAST(SUM(ISNULL(r.SumaWartosc,0)) AS DECIMAL(18,2)) AS WartoscRazem
FROM dbo.Reklamacje r
WHERE r.DataZgloszenia BETWEEN @DataOd AND @DataDo
  AND r.Handlowiec = @HandlowiecMaja
  AND (r.KategoriaPrzyczyny IS NOT NULL OR r.PodkategoriaPrzyczyny IS NOT NULL
       OR r.TypReklamacji IN (N'Jakosc produktu', N'Ilosc / Brak towaru', N'Niezgodnosc z zamowieniem', N'Inne'))
GROUP BY r.KategoriaPrzyczyny, r.PodkategoriaPrzyczyny
ORDER BY Liczba DESC;

SELECT N'H.5 — Decyzje jakości — co dział jakości stwierdził dla reklamacji Mai' AS [Raport];

SELECT ISNULL(r.DecyzjaJakosci, N'(brak decyzji jakości)') AS DecyzjaJakosci,
       r.Status,
       COUNT(*) AS Liczba,
       CAST(SUM(ISNULL(r.SumaWartosc,0)) AS DECIMAL(18,2)) AS WartoscRazem,
       CAST(SUM(ISNULL(r.KosztReklamacji,0)) AS DECIMAL(18,2)) AS KosztRazem
FROM dbo.Reklamacje r
WHERE r.DataZgloszenia BETWEEN @DataOd AND @DataDo
  AND r.Handlowiec = @HandlowiecMaja
  AND r.TypReklamacji <> N'Faktura korygujaca'  -- pomijamy auto-importy
GROUP BY r.DecyzjaJakosci, r.Status
ORDER BY Liczba DESC;

SELECT N'H.6 — Średni czas zamknięcia reklamacji per handlowiec' AS [Raport];

SELECT ISNULL(r.Handlowiec, N'(brak)') AS Handlowiec,
       COUNT(*) AS Reklamacji,
       SUM(CAST(CASE WHEN r.DataZamkniecia IS NOT NULL THEN 1 ELSE 0 END AS INT)) AS Zamknietych,
       SUM(CAST(CASE WHEN r.DataZamkniecia IS NULL THEN 1 ELSE 0 END AS INT))     AS Otwartych,
       CAST(AVG(CASE WHEN r.DataZamkniecia IS NOT NULL
                     THEN CAST(DATEDIFF(DAY, r.DataZgloszenia, r.DataZamkniecia) AS DECIMAL(10,2)) END) AS DECIMAL(8,2)) AS SredniDniDoZamkniecia,
       CAST(MAX(CASE WHEN r.DataZamkniecia IS NULL
                     THEN DATEDIFF(DAY, r.DataZgloszenia, GETDATE()) END) AS INT) AS NajdluzejOtwarte_Dni
FROM dbo.Reklamacje r
WHERE r.DataZgloszenia BETWEEN @DataOd AND @DataDo
  AND r.TypReklamacji <> N'Faktura korygujaca'  -- realne reklamacje
GROUP BY ISNULL(r.Handlowiec, N'(brak)')
ORDER BY Reklamacji DESC;

/* ============================================================================
   ===  J. CRM / NOTATKI / TELEFONY  ===========================================
   ============================================================================ */
SELECT N'J.1 — Aktywność notatek per handlowiec (NotatkiUzycia)' AS [Raport];

SELECT COALESCE(uh.HandlowiecName, mh.HandlowiecNazwa, N'(' + nu.UserId + N')') AS Handlowiec,
       COUNT(*)                                                  AS LiczbaUzyc,
       SUM(CAST(CASE WHEN nu.Akcja = N'Wpisana' THEN 1 ELSE 0 END AS INT))    AS WpisanaRecznie,
       SUM(CAST(CASE WHEN nu.Akcja = N'Wstawiona' THEN 1 ELSE 0 END AS INT))  AS WstawionaZSzablonu,
       COUNT(DISTINCT nu.KlientId)                               AS RoznychKlientow,
       MIN(nu.DataAkcji)                                         AS Pierwsze,
       MAX(nu.DataAkcji)                                         AS Ostatnie
FROM dbo.NotatkiUzycia nu
LEFT JOIN dbo.UserHandlowcy uh        ON uh.UserID = nu.UserId
LEFT JOIN dbo.MapowanieHandlowcow mh  ON mh.UserId = nu.UserId AND mh.CzyAktywny = 1
WHERE nu.DataAkcji BETWEEN @DataOd AND @DataDo
GROUP BY COALESCE(uh.HandlowiecName, mh.HandlowiecNazwa, N'(' + nu.UserId + N')')
ORDER BY LiczbaUzyc DESC;

SELECT N'J.2 — Szablony notatek stworzone/używane przez Maję (NotatkiSzablony)' AS [Raport];

SELECT TOP 30 ns.Id, ns.Tekst, ns.Kategoria, ns.Zakres, ns.KlientId,
       ns.LiczbaUzyc, ns.OstatnieUzycie, ns.UtworzonoTsmp,
       ns.UtworzonoPrzez, ns.Pinowane, ns.Aktywne
FROM dbo.NotatkiSzablony ns
WHERE ns.UtworzonoPrzez IN (SELECT UserID FROM #UserIdMaja)
   OR ns.UserId IN (SELECT UserID FROM #UserIdMaja)
ORDER BY ISNULL(ns.LiczbaUzyc,0) DESC, ns.UtworzonoTsmp DESC;

SELECT N'J.3 — Konfiguracja CallReminder Mai (czy używa systemu przypomnień)' AS [Raport];

SELECT c.ID, c.UserID, c.IsEnabled,
       c.DailyCallTarget, c.WeeklyCallTarget, c.MaxAttemptsPerContact,
       c.ReminderTime1, c.ReminderTime2, c.ReminderTime3,
       c.MinCallDurationSec, c.AlertBelowPercent,
       c.VacationStart, c.VacationEnd,
       c.CreatedAt, c.ModifiedAt
FROM dbo.CallReminderConfig c
WHERE c.UserID IN (SELECT UserID FROM #UserIdMaja);

SELECT N'J.4 — CallReminderLog: telefony/aktywności Mai (jeśli tabela ma kolumnę UserID)' AS [Raport];

IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
           WHERE TABLE_NAME='CallReminderLog' AND COLUMN_NAME='UserID')
BEGIN
    DECLARE @sqlCRL NVARCHAR(MAX) = N'
        SELECT TOP 100 *
        FROM dbo.CallReminderLog
        WHERE UserID IN (SELECT UserID FROM #UserIdMaja)
        ORDER BY 1 DESC;';   -- pierwsza kolumna ID
    BEGIN TRY EXEC sp_executesql @sqlCRL;
    END TRY
    BEGIN CATCH SELECT N'⚠ Błąd J.4: ' + ERROR_MESSAGE() AS Info; END CATCH;
END
ELSE
    SELECT N'⚠ CallReminderLog bez kolumny UserID' AS Info;

SELECT N'J.5 — Formalne właścicielstwo klientów Mai (WlascicieleOdbiorcow)' AS [Raport];

SELECT wo.OperatorID,
       COUNT(DISTINCT wo.IDOdbiorcy)             AS LiczbaPrzypisanychOdbiorcow,
       SUM(CAST(CASE WHEN wo.Priorytet = 1 THEN 1 ELSE 0 END AS INT)) AS Priorytetowych,
       MIN(wo.DataPrzypisania)                    AS NajstarszePrzyp,
       MAX(wo.DataPrzypisania)                    AS NajnowszePrzyp
FROM dbo.WlascicieleOdbiorcow wo
WHERE wo.OperatorID IN (SELECT UserID FROM #UserIdMaja)
GROUP BY wo.OperatorID;

/* ============================================================================
   ===  K. SCORECARD ZBIORCZY  =================================================
   ============================================================================ */
SELECT N'K2 — SCORECARD ZAMÓWIENIOWY (główny wynik LibraNet do Claude web)' AS [Raport];

WITH Z AS (
    SELECT Handlowiec, KlientId, ZamowienieId, SumaKg, SumaWartosc, SumaKgZrealizowana,
           Anulowane, CzyZafakturowane, KgE2, KgFolia, KgHallal
    FROM #ZamBaza
    WHERE DataOdbioru BETWEEN @DataOd AND @DataDo
      AND Handlowiec NOT IN (N'(nieznany)')
),
ReklH AS (
    SELECT ISNULL(r.Handlowiec, N'(brak)') AS Handlowiec,
           SUM(CAST(CASE WHEN r.TypReklamacji <> N'Faktura korygujaca' OR r.TypReklamacji IS NULL THEN 1 ELSE 0 END AS INT)) AS LiczbaReklJakosciowych,
           CAST(SUM(ISNULL(r.KosztReklamacji,0)) AS DECIMAL(18,2)) AS KosztRekl,
           CAST(AVG(CAST(DATEDIFF(DAY, r.DataZgloszenia, ISNULL(r.DataZamkniecia, GETDATE())) AS DECIMAL(10,2)))
                AS DECIMAL(8,2)) AS SredniDniZamykania
    FROM dbo.Reklamacje r
    WHERE r.DataZgloszenia BETWEEN @DataOd AND @DataDo
    GROUP BY ISNULL(r.Handlowiec, N'(brak)')
),
Roznice AS (
    SELECT zb.Handlowiec,
           CAST(SUM(zwr.Roznica) AS DECIMAL(18,1)) AS Roznica_Kg,
           CAST(SUM(zwr.Roznica) * 100.0 / NULLIF(SUM(zwr.IloscZamowiona),0) AS DECIMAL(6,2)) AS Roznica_Proc
    FROM dbo.ZamowienieWydanieRoznice zwr
    INNER JOIN #ZamBaza zb ON zb.ZamowienieId = zwr.ZamowienieId
    WHERE zb.DataOdbioru BETWEEN @DataOd AND @DataDo
    GROUP BY zb.Handlowiec
),
Modyfikacje AS (
    SELECT zb.Handlowiec,
           CAST(COUNT(h.Id) * 1.0 / NULLIF(COUNT(DISTINCT zb.ZamowienieId),0) AS DECIMAL(8,2)) AS ZmianNaZam
    FROM #ZamBaza zb
    LEFT JOIN dbo.HistoriaZmianZamowien h ON h.ZamowienieId = zb.ZamowienieId
                                          AND h.DataZmiany BETWEEN @DataOd AND DATEADD(DAY,1,@DataDo)
    WHERE zb.DataOdbioru BETWEEN @DataOd AND @DataDo
      AND zb.Handlowiec NOT IN (N'(nieznany)')
    GROUP BY zb.Handlowiec
)
SELECT z.Handlowiec,
       COUNT(*)                                                                                  AS LiczbaZam,
       COUNT(DISTINCT z.KlientId)                                                                AS LiczbaKlientow,
       CAST(SUM(z.SumaKg) AS DECIMAL(18,1))                                                       AS SumaKg,
       CAST(SUM(z.SumaWartosc) AS DECIMAL(18,2))                                                  AS SumaWartoscZam,
       CAST(SUM(CAST(CASE WHEN z.Anulowane = 1 THEN 1 ELSE 0 END AS INT)) * 100.0 / NULLIF(COUNT(*),0) AS DECIMAL(6,2)) AS Anulow_Proc,
       CAST(SUM(CASE WHEN ISNULL(z.CzyZafakturowane,0)=1 THEN 1 ELSE 0 END) * 100.0 / NULLIF(COUNT(*),0) AS DECIMAL(6,2)) AS Zafakt_Proc,
       CAST(SUM(z.SumaKgZrealizowana) * 100.0 / NULLIF(SUM(z.SumaKg),0) AS DECIMAL(6,2))           AS Realiz_Proc,
       CAST(SUM(z.KgE2)     * 100.0 / NULLIF(SUM(z.SumaKg),0) AS DECIMAL(6,2))                     AS E2_Proc,
       CAST(SUM(z.KgFolia)  * 100.0 / NULLIF(SUM(z.SumaKg),0) AS DECIMAL(6,2))                     AS Folia_Proc,
       CAST(SUM(z.KgHallal) * 100.0 / NULLIF(SUM(z.SumaKg),0) AS DECIMAL(6,2))                     AS Hallal_Proc,
       ISNULL(rkl.LiczbaReklJakosciowych, 0)                                                      AS Rekl_Jakosciowych,
       CAST(ISNULL(rkl.LiczbaReklJakosciowych,0) * 100.0 / NULLIF(COUNT(*),0) AS DECIMAL(6,2))     AS Rekl_per_100_Zam,
       ISNULL(rkl.KosztRekl, 0)                                                                   AS KosztReklamacji,
       rkl.SredniDniZamykania                                                                     AS Rekl_SredniDniZamyk,
       rz.Roznica_Kg                                                                              AS WydanoMinusZam_Kg,
       rz.Roznica_Proc                                                                            AS WydanoVsZam_Proc,
       md.ZmianNaZam                                                                              AS Mods_Per_Zam
FROM Z z
LEFT JOIN ReklH       rkl ON rkl.Handlowiec = z.Handlowiec
LEFT JOIN Roznice     rz  ON rz.Handlowiec  = z.Handlowiec
LEFT JOIN Modyfikacje md  ON md.Handlowiec  = z.Handlowiec
GROUP BY z.Handlowiec, rkl.LiczbaReklJakosciowych, rkl.KosztRekl, rkl.SredniDniZamykania,
         rz.Roznica_Kg, rz.Roznica_Proc, md.ZmianNaZam
ORDER BY CASE WHEN z.Handlowiec = @HandlowiecMaja THEN 0 ELSE 1 END, SumaWartoscZam DESC;

/* ----------------------------------------------------------------------------
   CLEANUP
   ---------------------------------------------------------------------------- */
IF OBJECT_ID('tempdb..#ZamBaza')   IS NOT NULL DROP TABLE #ZamBaza;
IF OBJECT_ID('tempdb..#KlienciMai') IS NOT NULL DROP TABLE #KlienciMai;
IF OBJECT_ID('tempdb..#UserIdMaja') IS NOT NULL DROP TABLE #UserIdMaja;
