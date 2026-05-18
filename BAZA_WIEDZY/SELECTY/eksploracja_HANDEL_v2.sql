/* ============================================================================
   eksploracja_HANDEL_v2.sql — Pełna inwentaryzacja kolumn bazy Handel
   ----------------------------------------------------------------------------
   Uruchom na: 192.168.0.112 / Handel (user 'sa')
   Cel: poznać DOKŁADNIE jakie kolumny istnieją w każdej kluczowej tabeli,
        ile danych jest, jakie typy, jakie indeksy, triggery, FK.
   ============================================================================ */

USE [Handel];
GO

SET NOCOUNT ON;

/* ----------------------------------------------------------------------------
   1. WSZYSTKIE TABELE HM.* + LICZBA WIERSZY
---------------------------------------------------------------------------- */
SELECT N'1 — Tabele HM.* z liczbą wierszy (przybliżoną)' AS [Raport];

WITH RowCounts AS (
    SELECT t.object_id, s.name AS Schemat, t.name AS Tabela, SUM(p.rows) AS LiczbaWierszy
    FROM sys.tables t
    INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
    INNER JOIN sys.partitions p ON p.object_id = t.object_id AND p.index_id IN (0,1)
    WHERE s.name = 'HM'
    GROUP BY t.object_id, s.name, t.name
),
ColCounts AS (
    SELECT object_id, COUNT(*) AS LiczbaKolumn FROM sys.columns GROUP BY object_id
)
SELECT rc.Schemat, rc.Tabela, rc.LiczbaWierszy, ISNULL(cc.LiczbaKolumn, 0) AS LiczbaKolumn
FROM RowCounts rc LEFT JOIN ColCounts cc ON cc.object_id = rc.object_id
ORDER BY rc.LiczbaWierszy DESC;

SELECT N'2 — Tabele SSCommon.* z liczbą wierszy' AS [Raport];

WITH RowCounts AS (
    SELECT t.object_id, s.name AS Schemat, t.name AS Tabela, SUM(p.rows) AS LiczbaWierszy
    FROM sys.tables t
    INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
    INNER JOIN sys.partitions p ON p.object_id = t.object_id AND p.index_id IN (0,1)
    WHERE s.name = 'SSCommon'
    GROUP BY t.object_id, s.name, t.name
),
ColCounts AS (
    SELECT object_id, COUNT(*) AS LiczbaKolumn FROM sys.columns GROUP BY object_id
)
SELECT rc.Schemat, rc.Tabela, rc.LiczbaWierszy, ISNULL(cc.LiczbaKolumn, 0) AS LiczbaKolumn
FROM RowCounts rc LEFT JOIN ColCounts cc ON cc.object_id = rc.object_id
ORDER BY rc.LiczbaWierszy DESC;

SELECT N'3 — Tabele dbo.* z liczbą wierszy (wszystkie inne)' AS [Raport];

WITH RowCounts AS (
    SELECT t.object_id, s.name AS Schemat, t.name AS Tabela, SUM(p.rows) AS LiczbaWierszy
    FROM sys.tables t
    INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
    INNER JOIN sys.partitions p ON p.object_id = t.object_id AND p.index_id IN (0,1)
    WHERE s.name NOT IN ('HM','SSCommon','MF','sys')
    GROUP BY t.object_id, s.name, t.name
),
ColCounts AS (
    SELECT object_id, COUNT(*) AS LiczbaKolumn FROM sys.columns GROUP BY object_id
)
SELECT rc.Schemat, rc.Tabela, rc.LiczbaWierszy, ISNULL(cc.LiczbaKolumn, 0) AS LiczbaKolumn
FROM RowCounts rc LEFT JOIN ColCounts cc ON cc.object_id = rc.object_id
ORDER BY rc.LiczbaWierszy DESC;

/* ----------------------------------------------------------------------------
   2. PEŁNE KOLUMNY GŁÓWNYCH TABEL
---------------------------------------------------------------------------- */
SELECT N'4 — HM.DK (faktury) — wszystkie kolumny' AS [Raport];

SELECT COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH AS Dlugosc, IS_NULLABLE, ORDINAL_POSITION
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = 'HM' AND TABLE_NAME = 'DK'
ORDER BY ORDINAL_POSITION;

SELECT N'5 — HM.DP (linie faktur) — wszystkie kolumny' AS [Raport];

SELECT COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH AS Dlugosc, IS_NULLABLE, ORDINAL_POSITION
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = 'HM' AND TABLE_NAME = 'DP'
ORDER BY ORDINAL_POSITION;

SELECT N'6 — HM.MG (dok. magazynowe) — wszystkie kolumny' AS [Raport];

SELECT COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH AS Dlugosc, IS_NULLABLE, ORDINAL_POSITION
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = 'HM' AND TABLE_NAME = 'MG'
ORDER BY ORDINAL_POSITION;

SELECT N'7 — HM.MZ (linie magazynowe) — wszystkie kolumny' AS [Raport];

SELECT COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH AS Dlugosc, IS_NULLABLE, ORDINAL_POSITION
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = 'HM' AND TABLE_NAME = 'MZ'
ORDER BY ORDINAL_POSITION;

SELECT N'8 — HM.TW (towary) — wszystkie kolumny' AS [Raport];

SELECT COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH AS Dlugosc, IS_NULLABLE, ORDINAL_POSITION
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = 'HM' AND TABLE_NAME = 'TW'
ORDER BY ORDINAL_POSITION;

SELECT N'9 — HM.PN (płatności) — wszystkie kolumny' AS [Raport];

SELECT COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH AS Dlugosc, IS_NULLABLE, ORDINAL_POSITION
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = 'HM' AND TABLE_NAME = 'PN'
ORDER BY ORDINAL_POSITION;

SELECT N'10 — STContractors (kontrahenci) — wszystkie kolumny' AS [Raport];

SELECT COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH AS Dlugosc, IS_NULLABLE, ORDINAL_POSITION
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = 'SSCommon' AND TABLE_NAME = 'STContractors'
ORDER BY ORDINAL_POSITION;

SELECT N'11 — ContractorClassification (klasyfikacja) — wszystkie kolumny' AS [Raport];

SELECT COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH AS Dlugosc, IS_NULLABLE, ORDINAL_POSITION
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = 'SSCommon' AND TABLE_NAME = 'ContractorClassification'
ORDER BY ORDINAL_POSITION;

SELECT N'12 — Inne często używane tabele HM (DK pochodne)' AS [Raport];

SELECT TABLE_SCHEMA, TABLE_NAME, COLUMN_NAME, DATA_TYPE, ORDINAL_POSITION
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = 'HM'
  AND TABLE_NAME IN ('DT','NK','RA','KR','KO','KW','DR','RO','MA','MAG','MS','UserRightsToWarehouses')
ORDER BY TABLE_NAME, ORDINAL_POSITION;

/* ----------------------------------------------------------------------------
   3. PRÓBKA DANYCH (TOP 5)
---------------------------------------------------------------------------- */
SELECT N'13 — Sample HM.DK (5 ostatnich faktur)' AS [Raport];
SELECT TOP 5 * FROM [HM].[DK] WITH (NOLOCK)
WHERE anulowany = 0 ORDER BY data DESC;

SELECT N'14 — Sample HM.DP (5 najnowszych pozycji)' AS [Raport];
SELECT TOP 5 * FROM [HM].[DP] WITH (NOLOCK) ORDER BY id DESC;

SELECT N'15 — Sample HM.MG (5 ostatnich dok. magazynowych)' AS [Raport];
SELECT TOP 5 * FROM [HM].[MG] WITH (NOLOCK)
WHERE anulowany = 0 ORDER BY data DESC;

SELECT N'16 — Sample HM.TW (5 najnowszych towarów)' AS [Raport];
SELECT TOP 5 * FROM [HM].[TW] WITH (NOLOCK) ORDER BY id DESC;

SELECT N'17 — Sample STContractors (5 największych)' AS [Raport];
SELECT TOP 5 * FROM [SSCommon].[STContractors] WITH (NOLOCK)
ORDER BY LimitAmount DESC;

SELECT N'18 — Sample ContractorClassification (5 z handlowcem Maja)' AS [Raport];
SELECT TOP 5 * FROM [SSCommon].[ContractorClassification] WITH (NOLOCK)
WHERE CDim_Handlowiec_Val = N'Maja';

/* ----------------------------------------------------------------------------
   4. INDEKSY GŁÓWNYCH TABEL
---------------------------------------------------------------------------- */
SELECT N'19 — Indeksy HM.DK / DP / MG / MZ / TW / PN' AS [Raport];

SELECT s.name AS Schemat, t.name AS Tabela, i.name AS Indeks,
       i.type_desc AS Typ, i.is_unique AS Unikalny,
       (SELECT STRING_AGG(c.name, N', ') WITHIN GROUP (ORDER BY ic.key_ordinal)
        FROM sys.index_columns ic
        INNER JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
        WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id AND ic.is_included_column = 0) AS Kolumny,
       (SELECT STRING_AGG(c.name, N', ') WITHIN GROUP (ORDER BY c.name)
        FROM sys.index_columns ic
        INNER JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
        WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id AND ic.is_included_column = 1) AS Included
FROM sys.indexes i
INNER JOIN sys.tables t ON t.object_id = i.object_id
INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
WHERE s.name = 'HM' AND t.name IN ('DK','DP','MG','MZ','TW','PN')
  AND i.type > 0
ORDER BY t.name, i.index_id;

/* ----------------------------------------------------------------------------
   5. TRIGGERY (Sage ma INSTEAD OF triggery na ContractorClassification!)
---------------------------------------------------------------------------- */
SELECT N'20 — Wszystkie triggery (na czym, co robią)' AS [Raport];

SELECT s.name AS Schemat, t.name AS Tabela, tr.name AS Trigger_Name,
       CASE WHEN tr.is_disabled = 1 THEN N'WYŁĄCZONY' ELSE N'AKTYWNY' END AS Stan,
       OBJECT_DEFINITION(tr.object_id) AS Definicja
FROM sys.triggers tr
INNER JOIN sys.tables t ON t.object_id = tr.parent_id
INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
ORDER BY s.name, t.name, tr.name;

/* ----------------------------------------------------------------------------
   6. WIDOKI
---------------------------------------------------------------------------- */
SELECT N'21 — Wszystkie widoki + ich definicja' AS [Raport];

SELECT s.name AS Schemat, v.name AS Widok,
       OBJECT_DEFINITION(v.object_id) AS Definicja
FROM sys.views v
INNER JOIN sys.schemas s ON s.schema_id = v.schema_id
ORDER BY s.name, v.name;

/* ----------------------------------------------------------------------------
   7. KATALOGI TOWARÓW (TW.katalog) — ile w każdym
---------------------------------------------------------------------------- */
SELECT N'22 — Katalogi towarów (TW.katalog) + liczba towarów + przykład' AS [Raport];

SELECT katalog,
       COUNT(*) AS LiczbaTowarow,
       MIN(nazwa) AS PrzykladNazwa1,
       MAX(nazwa) AS PrzykladNazwa2
FROM [HM].[TW]
WHERE aktywny = 1
GROUP BY katalog
ORDER BY LiczbaTowarow DESC;

/* ----------------------------------------------------------------------------
   8. SERIE DOKUMENTÓW HM.DK i HM.MG — co naprawdę jest w bazie
---------------------------------------------------------------------------- */
SELECT N'23 — Serie HM.DK (faktury sprzedaży/korekty) + liczba + zakres dat' AS [Raport];

SELECT seria,
       COUNT(*) AS Liczba,
       MIN(data) AS NajstarszaData,
       MAX(data) AS NajnowszaData
FROM [HM].[DK]
WHERE anulowany = 0 AND data >= DATEADD(YEAR, -3, GETDATE())
GROUP BY seria
ORDER BY Liczba DESC;

SELECT N'24 — Serie HM.MG (dok. magazynowe) + liczba + zakres dat' AS [Raport];

SELECT seria,
       COUNT(*) AS Liczba,
       MIN(data) AS NajstarszaData,
       MAX(data) AS NajnowszaData
FROM [HM].[MG]
WHERE anulowany = 0 AND data >= DATEADD(YEAR, -3, GETDATE())
GROUP BY seria
ORDER BY Liczba DESC;

/* ----------------------------------------------------------------------------
   9. WSZYSCY HANDLOWCY W BAZIE (kiedykolwiek, z liczbą faktur)
---------------------------------------------------------------------------- */
SELECT N'25 — Wszyscy handlowcy w CDim_Handlowiec_Val + ich aktywność' AS [Raport];

SELECT WYM.CDim_Handlowiec_Val AS Handlowiec,
       COUNT(DISTINCT WYM.ElementId) AS LiczbaKontrahentow,
       COUNT(DK.id) AS LiczbaFaktur,
       MIN(DK.data) AS PierwszaFakturaKiedy,
       MAX(DK.data) AS OstatniaFakturaKiedy,
       DATEDIFF(DAY, MAX(DK.data), GETDATE()) AS DniOdOstatniej,
       CAST(SUM(DP.wartNetto) AS DECIMAL(18,2)) AS LacznieObrot
FROM [SSCommon].[ContractorClassification] WYM WITH (NOLOCK)
LEFT JOIN [HM].[DK] DK WITH (NOLOCK) ON DK.khid = WYM.ElementId
                                    AND DK.anulowany = 0
                                    AND DK.data >= DATEADD(YEAR, -3, GETDATE())
LEFT JOIN [HM].[DP] DP WITH (NOLOCK) ON DP.super = DK.id
WHERE WYM.CDim_Handlowiec_Val IS NOT NULL
  AND WYM.CDim_Handlowiec_Val <> N''
GROUP BY WYM.CDim_Handlowiec_Val
ORDER BY LacznieObrot DESC;

/* ----------------------------------------------------------------------------
   10. AKTYWNI KONTRAHENCI POD KAŻDYM HANDLOWCEM (TOP 5 per handlowiec)
---------------------------------------------------------------------------- */
SELECT N'26 — TOP 5 klientów per handlowiec (12 miesięcy)' AS [Raport];

WITH F AS (
    SELECT WYM.CDim_Handlowiec_Val AS Handlowiec,
           C.shortcut AS Klient,
           SUM(DP.wartNetto) AS Netto,
           COUNT(DISTINCT DK.id) AS Faktur
    FROM [HM].[DK] DK WITH (NOLOCK)
    INNER JOIN [HM].[DP] DP WITH (NOLOCK) ON DP.super = DK.id
    INNER JOIN [SSCommon].[STContractors] C WITH (NOLOCK) ON C.id = DK.khid
    LEFT JOIN [SSCommon].[ContractorClassification] WYM WITH (NOLOCK) ON WYM.ElementId = DK.khid
    WHERE DK.anulowany = 0
      AND DK.data >= DATEADD(YEAR, -1, GETDATE())
    GROUP BY WYM.CDim_Handlowiec_Val, C.shortcut
),
R AS (SELECT *, ROW_NUMBER() OVER (PARTITION BY Handlowiec ORDER BY Netto DESC) AS Poz FROM F)
SELECT Handlowiec, Poz, Klient, CAST(Netto AS DECIMAL(18,2)) AS Netto, Faktur
FROM R WHERE Poz <= 5
ORDER BY Handlowiec, Poz;

SELECT N'27 — Zakończono eksplorację HANDEL — uruchom również eksploracja_LIBRANET_v2.sql' AS Info;
