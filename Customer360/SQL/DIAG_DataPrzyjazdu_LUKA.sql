/* ════════════════════════════════════════════════════════════════════════════
   DIAGNOSTYKA: dlaczego Customer360 widzi dane dopiero od 12/2025
   Hipoteza: starsze ZamowieniaMieso mają DataPrzyjazdu = NULL → filtr odrzuca.
   Środowisko: LibraNet (192.168.0.109) + HANDEL (192.168.0.112, linked server).

   STRUKTURA SKRYPTU (18 selektów):
     A) #1-#6   — DATY i NULL-e w ZamowieniaMieso (sedno hipotezy)
     B) #7-#10  — SCHEMA, INDEKSY, INNE KOLUMNY DAT (alternatywne fallbacki)
     C) #11-#14 — KONTEKST: Status, IdUser, ZamowieniaMiesoTowar, KlientId
     D) #15-#16 — CROSS-CHECK z HANDEL (czy faktury istnieją dla tych okresów)
     E) #17-#18 — DEEP-DIVE: TOP klient i kontrola anomalii

   Każdy SELECT NIEZALEŻNY — można uruchamiać partiami.
   ════════════════════════════════════════════════════════════════════════════ */

USE LibraNet;
SET NOCOUNT ON;


/* ████████████████████████████████████████████████████████████████████████████
   GRUPA A — DATY i NULL-e w ZamowieniaMieso
   ████████████████████████████████████████████████████████████████████████████ */

/* ────────────────────────────────────────────────────────────────────────────
   #1  GLOBALNY ROZKŁAD: NULL vs OK, min/max wszystkich kolumn dat.
   ──────────────────────────────────────────────────────────────────────────── */
SELECT
    COUNT(*)                                                                  AS WierszyOgolem,
    SUM(CASE WHEN DataPrzyjazdu  IS NULL THEN 1 ELSE 0 END)                   AS Brak_DataPrzyjazdu,
    SUM(CASE WHEN DataZamowienia IS NULL THEN 1 ELSE 0 END)                   AS Brak_DataZamowienia,
    SUM(CASE WHEN DataUboju      IS NULL THEN 1 ELSE 0 END)                   AS Brak_DataUboju,
    SUM(CASE WHEN DataWydania    IS NULL THEN 1 ELSE 0 END)                   AS Brak_DataWydania,
    CAST(100.0 * SUM(CASE WHEN DataPrzyjazdu IS NULL THEN 1 ELSE 0 END)
         / NULLIF(COUNT(*),0) AS DECIMAL(5,2))                                AS Proc_Brak_Przyjazdu,
    MIN(DataZamowienia)  AS Min_Zam, MAX(DataZamowienia)  AS Max_Zam,
    MIN(DataPrzyjazdu)   AS Min_Prz, MAX(DataPrzyjazdu)   AS Max_Prz,
    MIN(DataUboju)       AS Min_Ubo, MAX(DataUboju)       AS Max_Ubo,
    MIN(DataWydania)     AS Min_Wyd, MAX(DataWydania)     AS Max_Wyd
FROM dbo.ZamowieniaMieso;


/* ────────────────────────────────────────────────────────────────────────────
   #2  ROZKŁAD MIESIĘCZNY (ostatnie 24 mies wg DataZamowienia).
       Pokaże ZA JAKI MIESIĄC NULL-e dominują → moment "włączenia" DataPrzyjazdu.
   ──────────────────────────────────────────────────────────────────────────── */
SELECT
    YEAR(DataZamowienia) AS Rok, MONTH(DataZamowienia) AS Mies,
    COUNT(*)                                                                   AS Zamowien,
    SUM(CASE WHEN DataPrzyjazdu IS NULL     THEN 1 ELSE 0 END)                 AS BrakPrzyjazdu,
    SUM(CASE WHEN DataPrzyjazdu IS NOT NULL THEN 1 ELSE 0 END)                 AS MaPrzyjazd,
    CAST(100.0 * SUM(CASE WHEN DataPrzyjazdu IS NULL THEN 1 ELSE 0 END)
         / NULLIF(COUNT(*),0) AS DECIMAL(5,2))                                 AS Proc_NULL,
    MIN(DataZamowienia)                                                        AS Najwcz,
    MAX(DataZamowienia)                                                        AS Najpoz
FROM dbo.ZamowieniaMieso
WHERE DataZamowienia >= DATEADD(MONTH, -24, GETDATE())
GROUP BY YEAR(DataZamowienia), MONTH(DataZamowienia)
ORDER BY Rok DESC, Mies DESC;


/* ────────────────────────────────────────────────────────────────────────────
   #3  CO TRACI OBECNY FILTR vs PROPONOWANY (COALESCE).
       Sumarycznie + per klient TOP 30 (kto zyska najwięcej).
   ──────────────────────────────────────────────────────────────────────────── */
;WITH Window12 AS (
    SELECT z.Id, z.KlientId,
           CASE WHEN z.DataPrzyjazdu >= DATEADD(MONTH, -12, GETDATE())                              THEN 1 ELSE 0 END AS PrzedFix,
           CASE WHEN COALESCE(z.DataPrzyjazdu, z.DataZamowienia) >= DATEADD(MONTH, -12, GETDATE())  THEN 1 ELSE 0 END AS PoFix
    FROM dbo.ZamowieniaMieso z
    WHERE ISNULL(z.Status,'') NOT IN ('Anulowane','Anulowano')
)
SELECT 'GLOBALNIE' AS Zakres,
       SUM(PrzedFix)                                              AS PrzedFix,
       SUM(PoFix)                                                 AS PoFix,
       SUM(PoFix) - SUM(PrzedFix)                                 AS ZyskZamowien,
       CAST(100.0 * (SUM(PoFix) - SUM(PrzedFix))
            / NULLIF(SUM(PoFix),0) AS DECIMAL(5,2))               AS Proc_Zysku
FROM Window12;

;WITH Window12 AS (
    SELECT z.Id, z.KlientId,
           CASE WHEN z.DataPrzyjazdu >= DATEADD(MONTH, -12, GETDATE())                              THEN 1 ELSE 0 END AS PrzedFix,
           CASE WHEN COALESCE(z.DataPrzyjazdu, z.DataZamowienia) >= DATEADD(MONTH, -12, GETDATE())  THEN 1 ELSE 0 END AS PoFix
    FROM dbo.ZamowieniaMieso z
    WHERE ISNULL(z.Status,'') NOT IN ('Anulowane','Anulowano')
)
SELECT TOP 30 KlientId, SUM(PrzedFix) AS PrzedFix, SUM(PoFix) AS PoFix,
              SUM(PoFix) - SUM(PrzedFix) AS Zysk
FROM Window12
WHERE KlientId IS NOT NULL
GROUP BY KlientId
HAVING SUM(PoFix) - SUM(PrzedFix) > 0
ORDER BY Zysk DESC;


/* ────────────────────────────────────────────────────────────────────────────
   #4  PUNKT PRZEŁOMOWY — od kiedy DataPrzyjazdu jest zawsze NOT NULL.
   ──────────────────────────────────────────────────────────────────────────── */
;WITH Miesieczne AS (
    SELECT DATEFROMPARTS(YEAR(DataZamowienia), MONTH(DataZamowienia), 1) AS Miesiac,
           COUNT(*)                                                      AS Razem,
           SUM(CASE WHEN DataPrzyjazdu IS NULL THEN 1 ELSE 0 END)        AS Null_W_Mies
    FROM dbo.ZamowieniaMieso
    WHERE DataZamowienia IS NOT NULL
    GROUP BY DATEFROMPARTS(YEAR(DataZamowienia), MONTH(DataZamowienia), 1)
)
SELECT Miesiac, Razem, Null_W_Mies,
       CAST(100.0 * Null_W_Mies / NULLIF(Razem,0) AS DECIMAL(5,2))      AS Proc_NULL_Mies,
       SUM(Null_W_Mies) OVER (ORDER BY Miesiac DESC
                              ROWS BETWEEN CURRENT ROW AND UNBOUNDED FOLLOWING) AS Cum_Null_Pozniej
FROM Miesieczne
ORDER BY Miesiac DESC;


/* ────────────────────────────────────────────────────────────────────────────
   #5  RÓŻNICA DataPrzyjazdu vs DataZamowienia — czy COALESCE jest OK semantycznie.
       SQL 2008 R2 → percentyle ręcznie.
   ──────────────────────────────────────────────────────────────────────────── */
SELECT
    COUNT(*)                                                                                        AS Rekordy_OK,
    AVG(CAST(DATEDIFF(DAY, DataZamowienia, DataPrzyjazdu) AS FLOAT))                                AS Srednia_Dni,
    MIN(DATEDIFF(DAY, DataZamowienia, DataPrzyjazdu))                                               AS Min_Dni,
    MAX(DATEDIFF(DAY, DataZamowienia, DataPrzyjazdu))                                               AS Max_Dni,
    SUM(CASE WHEN DATEDIFF(DAY, DataZamowienia, DataPrzyjazdu) BETWEEN  0 AND  1 THEN 1 ELSE 0 END) AS Dni_0_1,
    SUM(CASE WHEN DATEDIFF(DAY, DataZamowienia, DataPrzyjazdu) BETWEEN  2 AND  7 THEN 1 ELSE 0 END) AS Dni_2_7,
    SUM(CASE WHEN DATEDIFF(DAY, DataZamowienia, DataPrzyjazdu) BETWEEN  8 AND 14 THEN 1 ELSE 0 END) AS Dni_8_14,
    SUM(CASE WHEN DATEDIFF(DAY, DataZamowienia, DataPrzyjazdu) BETWEEN 15 AND 30 THEN 1 ELSE 0 END) AS Dni_15_30,
    SUM(CASE WHEN DATEDIFF(DAY, DataZamowienia, DataPrzyjazdu) > 30            THEN 1 ELSE 0 END)   AS Dni_30plus,
    SUM(CASE WHEN DATEDIFF(DAY, DataZamowienia, DataPrzyjazdu) < 0             THEN 1 ELSE 0 END)   AS Dni_Ujemne_BLAD
FROM dbo.ZamowieniaMieso
WHERE DataPrzyjazdu  IS NOT NULL
  AND DataZamowienia IS NOT NULL;


/* ────────────────────────────────────────────────────────────────────────────
   #6  SAMPLE — 20 najstarszych NULL-i które fix odzyska. Realne zlecenia?
   ──────────────────────────────────────────────────────────────────────────── */
SELECT TOP 20
    z.Id, z.KlientId, z.DataZamowienia, z.DataPrzyjazdu, z.DataUboju, z.DataWydania,
    z.Status, z.IdUser,
    (SELECT COUNT(*) FROM dbo.ZamowieniaMiesoTowar zt WHERE zt.ZamowienieId = z.Id) AS Pozycji,
    (SELECT ISNULL(SUM(zt.Ilosc),0) FROM dbo.ZamowieniaMiesoTowar zt WHERE zt.ZamowienieId = z.Id) AS SumaKg
FROM dbo.ZamowieniaMieso z
WHERE z.DataPrzyjazdu IS NULL
  AND z.DataZamowienia >= DATEADD(MONTH, -12, GETDATE())
  AND ISNULL(z.Status,'') NOT IN ('Anulowane','Anulowano')
ORDER BY z.DataZamowienia DESC;


/* ████████████████████████████████████████████████████████████████████████████
   GRUPA B — SCHEMA, INDEKSY, ALTERNATYWNE KOLUMNY
   ████████████████████████████████████████████████████████████████████████████ */

/* ────────────────────────────────────────────────────────────────────────────
   #7  FULL SCHEMA ZamowieniaMieso — kolumny, typy, nullable, defaulty.
       Pokaże MI jakie kolumny jeszcze mogę użyć jako fallback dla NULL DataPrzyjazdu.
   ──────────────────────────────────────────────────────────────────────────── */
SELECT
    c.column_id                                                AS Poz,
    c.name                                                     AS Kolumna,
    t.name + CASE
        WHEN t.name IN ('varchar','nvarchar','char','nchar')
            THEN '(' + CASE WHEN c.max_length = -1 THEN 'max'
                            WHEN t.name LIKE 'n%' THEN CAST(c.max_length/2 AS varchar)
                            ELSE CAST(c.max_length AS varchar) END + ')'
        WHEN t.name IN ('decimal','numeric') THEN '(' + CAST(c.precision AS varchar) + ',' + CAST(c.scale AS varchar) + ')'
        ELSE '' END                                            AS Typ,
    CASE WHEN c.is_nullable = 1 THEN 'YES' ELSE 'NO' END       AS Nullable,
    OBJECT_DEFINITION(c.default_object_id)                     AS Default_Value,
    ep.value                                                   AS Opis
FROM sys.columns c
JOIN sys.types t ON c.user_type_id = t.user_type_id
LEFT JOIN sys.extended_properties ep
       ON ep.major_id = c.object_id AND ep.minor_id = c.column_id AND ep.name='MS_Description'
WHERE c.object_id = OBJECT_ID('dbo.ZamowieniaMieso')
ORDER BY c.column_id;


/* ────────────────────────────────────────────────────────────────────────────
   #8  INDEKSY ZamowieniaMieso — czy COALESCE(DataPrzyjazdu, DataZamowienia)
       w WHERE nie zabije performance. Index na DataPrzyjazdu? KlientId?
   ──────────────────────────────────────────────────────────────────────────── */
SELECT
    i.name                                          AS Indeks,
    i.type_desc                                     AS Typ,
    i.is_unique                                     AS Unikalny,
    STUFF((SELECT ', ' + COL_NAME(ic.object_id, ic.column_id)
           FROM sys.index_columns ic
           WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id AND ic.is_included_column = 0
           ORDER BY ic.key_ordinal
           FOR XML PATH('')), 1, 2, '')             AS Kolumny_Klucza,
    STUFF((SELECT ', ' + COL_NAME(ic.object_id, ic.column_id)
           FROM sys.index_columns ic
           WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id AND ic.is_included_column = 1
           ORDER BY ic.index_column_id
           FOR XML PATH('')), 1, 2, '')             AS Kolumny_INCLUDE
FROM sys.indexes i
WHERE i.object_id = OBJECT_ID('dbo.ZamowieniaMieso')
  AND i.index_id > 0
ORDER BY i.is_primary_key DESC, i.name;


/* ────────────────────────────────────────────────────────────────────────────
   #9  WSZYSTKIE KOLUMNY DATETIME w ZamowieniaMieso — może jest jeszcze lepszy
       fallback niż DataZamowienia (np. DataModyfikacji, DataUtworzenia)?
   ──────────────────────────────────────────────────────────────────────────── */
SELECT
    c.name                                                          AS Kolumna,
    t.name                                                          AS Typ,
    SUM(CASE WHEN cnt.x IS NULL THEN 0 ELSE cnt.x END)              AS Marker
FROM sys.columns c
JOIN sys.types   t ON c.user_type_id = t.user_type_id
OUTER APPLY (SELECT 1 AS x WHERE c.object_id IS NOT NULL) cnt
WHERE c.object_id = OBJECT_ID('dbo.ZamowieniaMieso')
  AND t.name IN ('date','datetime','datetime2','smalldatetime','datetimeoffset','time')
GROUP BY c.name, t.name
ORDER BY c.name;

/* Per-kolumna ile NULL — dynamicznie nie da się bez kursora,
   więc nazwy kolumn pobierz z #9 i sprawdź każdą:                              */
SELECT
    SUM(CASE WHEN DataZamowienia       IS NULL THEN 1 ELSE 0 END)   AS NULL_DataZamowienia,
    SUM(CASE WHEN DataPrzyjazdu        IS NULL THEN 1 ELSE 0 END)   AS NULL_DataPrzyjazdu,
    SUM(CASE WHEN DataUboju            IS NULL THEN 1 ELSE 0 END)   AS NULL_DataUboju,
    SUM(CASE WHEN DataWydania          IS NULL THEN 1 ELSE 0 END)   AS NULL_DataWydania
    /* dodaj inne kolumny dat jeśli #9 je znajdzie */
FROM dbo.ZamowieniaMieso;


/* ────────────────────────────────────────────────────────────────────────────
   #10 CHRONOLOGIA Id vs Daty — czy ID jest sekwencyjne wzgledem czasu?
       Jeśli tak, można z grubsza wnioskować datę z ID gdy daty są NULL.
   ──────────────────────────────────────────────────────────────────────────── */
SELECT TOP 50
    z.Id, z.DataZamowienia, z.DataPrzyjazdu,
    z.KlientId, z.Status
FROM dbo.ZamowieniaMieso z
WHERE z.DataPrzyjazdu IS NULL
ORDER BY z.Id ASC;  -- najstarsze ID z brakiem przyjazdu

SELECT
    MIN(z.Id) AS Min_Id_NULL,    MAX(z.Id) AS Max_Id_NULL,
    MIN(z.DataZamowienia) AS Min_Data_NULL, MAX(z.DataZamowienia) AS Max_Data_NULL
FROM dbo.ZamowieniaMieso z
WHERE z.DataPrzyjazdu IS NULL;

SELECT
    MIN(z.Id) AS Min_Id_OK,      MAX(z.Id) AS Max_Id_OK,
    MIN(z.DataPrzyjazdu) AS Min_Data_OK, MAX(z.DataPrzyjazdu) AS Max_Data_OK
FROM dbo.ZamowieniaMieso z
WHERE z.DataPrzyjazdu IS NOT NULL;


/* ████████████████████████████████████████████████████████████████████████████
   GRUPA C — STATUS, USER, POZYCJE TOWARÓW, KlientId
   ████████████████████████████████████████████████████████████████████████████ */

/* ────────────────────────────────────────────────────────────────────────────
   #11 ROZKŁAD wg Status × NULL DataPrzyjazdu.
       Może NULL dotyczy tylko jednego statusu (Robocze? Anulowane?)?
   ──────────────────────────────────────────────────────────────────────────── */
SELECT
    ISNULL(z.Status, '(NULL)')                                                  AS Status,
    COUNT(*)                                                                    AS Razem,
    SUM(CASE WHEN z.DataPrzyjazdu IS NULL THEN 1 ELSE 0 END)                    AS Brak_Prz,
    SUM(CASE WHEN z.DataPrzyjazdu IS NOT NULL THEN 1 ELSE 0 END)                AS Ma_Prz,
    CAST(100.0 * SUM(CASE WHEN z.DataPrzyjazdu IS NULL THEN 1 ELSE 0 END)
         / NULLIF(COUNT(*),0) AS DECIMAL(5,2))                                  AS Proc_NULL
FROM dbo.ZamowieniaMieso z
GROUP BY z.Status
ORDER BY Razem DESC;


/* ────────────────────────────────────────────────────────────────────────────
   #12 ROZKŁAD wg IdUser × NULL DataPrzyjazdu.
       Może to konkretny handlowiec/operator nie wypełnia DataPrzyjazdu?
       JOIN nie potrzebny — jeśli rozkład jest jednorodny to nie problem usera.
   ──────────────────────────────────────────────────────────────────────────── */
SELECT TOP 30
    z.IdUser,
    COUNT(*)                                                                    AS Razem,
    SUM(CASE WHEN z.DataPrzyjazdu IS NULL THEN 1 ELSE 0 END)                    AS Brak_Prz,
    CAST(100.0 * SUM(CASE WHEN z.DataPrzyjazdu IS NULL THEN 1 ELSE 0 END)
         / NULLIF(COUNT(*),0) AS DECIMAL(5,2))                                  AS Proc_NULL,
    MIN(z.DataZamowienia) AS Pierwsze,
    MAX(z.DataZamowienia) AS Ostatnie
FROM dbo.ZamowieniaMieso z
WHERE z.DataZamowienia >= DATEADD(MONTH, -24, GETDATE())
GROUP BY z.IdUser
ORDER BY Razem DESC;


/* ────────────────────────────────────────────────────────────────────────────
   #13 CZY NULL ZAMÓWIENIA MAJĄ TOWARY? Pomarchive’owane czy realne?
   ──────────────────────────────────────────────────────────────────────────── */
SELECT
    CASE WHEN ma_tow.cnt IS NULL OR ma_tow.cnt = 0 THEN 'BEZ_TOWAROW' ELSE 'Z_TOWARAMI' END AS Rodzaj,
    COUNT(*)                                                                    AS Liczba,
    CAST(100.0 * COUNT(*) / SUM(COUNT(*)) OVER () AS DECIMAL(5,2))              AS Procent
FROM dbo.ZamowieniaMieso z
OUTER APPLY (SELECT COUNT(*) AS cnt FROM dbo.ZamowieniaMiesoTowar zt WHERE zt.ZamowienieId = z.Id) ma_tow
WHERE z.DataPrzyjazdu IS NULL
GROUP BY CASE WHEN ma_tow.cnt IS NULL OR ma_tow.cnt = 0 THEN 'BEZ_TOWAROW' ELSE 'Z_TOWARAMI' END;


/* ────────────────────────────────────────────────────────────────────────────
   #14 KlientId distribution — ile UNIKALNYCH klientów ma NULL DataPrzyjazdu,
       ile z nich pojawia się też w "OK" zbiorze. Sprawdza czy NULL dotyczy
       konkretnych klientów (np. starych, nieaktywnych).
   ──────────────────────────────────────────────────────────────────────────── */
SELECT
    'Klienci z NULL DataPrzyjazdu'                                              AS Grupa,
    COUNT(DISTINCT KlientId)                                                    AS Unikalni_Klienci
FROM dbo.ZamowieniaMieso WHERE DataPrzyjazdu IS NULL AND KlientId IS NOT NULL
UNION ALL
SELECT 'Klienci z OK DataPrzyjazdu', COUNT(DISTINCT KlientId)
FROM dbo.ZamowieniaMieso WHERE DataPrzyjazdu IS NOT NULL AND KlientId IS NOT NULL
UNION ALL
SELECT 'Klienci TYLKO z NULL (brak żadnego OK)', COUNT(DISTINCT a.KlientId)
FROM dbo.ZamowieniaMieso a
WHERE a.DataPrzyjazdu IS NULL AND a.KlientId IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM dbo.ZamowieniaMieso b
                  WHERE b.KlientId = a.KlientId AND b.DataPrzyjazdu IS NOT NULL);


/* ████████████████████████████████████████████████████████████████████████████
   GRUPA D — CROSS-CHECK z HANDEL (faktury Sage Symfonia)
   ████████████████████████████████████████████████████████████████████████████ */

/* ────────────────────────────────────────────────────────────────────────────
   #15 CHRONOLOGIA FAKTUR (HM.DK) z 192.168.0.112 — sięgają jak daleko?
       Pokaże czy faktury są tam też od grudnia 2025 (wtedy problem jest
       z DK.khid mappingiem, nie z DataPrzyjazdu).
       UWAGA: wymaga że HANDEL jest linked-server. Jeśli nie — uruchom osobno.
   ──────────────────────────────────────────────────────────────────────────── */
SELECT
    YEAR(DK.data)  AS Rok,
    MONTH(DK.data) AS Mies,
    COUNT(*)       AS Faktur,
    SUM(CASE WHEN DK.typ_dk IN (N'FVS', N'FS', N'FVK', N'KFS') THEN 1 ELSE 0 END) AS Sprzedaz,
    SUM(CASE WHEN DK.typ_dk IN (N'WZ', N'WZ-F', N'DWZ')        THEN 1 ELSE 0 END) AS WZ_etki,
    SUM(CASE WHEN DK.typ_dk IN (N'PA', N'PAS')                 THEN 1 ELSE 0 END) AS Paragony,
    MIN(DK.data)   AS Pierwsza,
    MAX(DK.data)   AS Ostatnia
FROM [HANDEL].[HM].[DK] DK
WHERE DK.anulowany = 0
  AND DK.data >= DATEADD(MONTH, -24, GETDATE())
  AND DK.typ_dk IN (N'FVS', N'FS', N'FVK', N'KFS', N'WZ', N'WZ-F', N'DWZ', N'PA', N'PAS', N'FRR', N'WDT')
GROUP BY YEAR(DK.data), MONTH(DK.data)
ORDER BY Rok DESC, Mies DESC;


/* ────────────────────────────────────────────────────────────────────────────
   #16 CROSS-MATCH: dla NULL DataPrzyjazdu zamówień — czy istnieją faktury HANDEL
       w okolicach DataZamowienia? Pokaże czy mapping KlientId ↔ khid działa
       dla starszych okresów.
   ──────────────────────────────────────────────────────────────────────────── */
SELECT TOP 20
    z.Id                                                       AS ZamId,
    z.KlientId,
    z.DataZamowienia,
    z.Status,
    (SELECT COUNT(*) FROM [HANDEL].[HM].[DK] dk
      WHERE dk.khid = z.KlientId AND dk.anulowany = 0
        AND dk.data BETWEEN DATEADD(DAY,-7, z.DataZamowienia) AND DATEADD(DAY,30, z.DataZamowienia)
        AND dk.typ_dk IN (N'FVS', N'FS', N'FVK', N'KFS', N'WZ', N'WZ-F', N'DWZ', N'PA', N'PAS')
    )                                                          AS Faktur_W_Okolicy
FROM dbo.ZamowieniaMieso z
WHERE z.DataPrzyjazdu IS NULL
  AND z.DataZamowienia >= DATEADD(MONTH, -18, GETDATE())
  AND ISNULL(z.Status,'') NOT IN ('Anulowane','Anulowano')
ORDER BY z.DataZamowienia DESC;


/* ████████████████████████████████████████████████████████████████████████████
   GRUPA E — DEEP-DIVE: TOP klient + anomalie
   ████████████████████████████████████████████████████████████████████████████ */

/* ────────────────────────────────────────────────────────────────────────────
   #17 TOP-1 KLIENT (najwięcej zamówień 24 mies) — pełen rozkład miesięczny.
       Pokaże dokładnie tab "Historia" z perspektywy najaktywniejszego klienta.
   ──────────────────────────────────────────────────────────────────────────── */
;WITH Top1 AS (
    SELECT TOP 1 KlientId, COUNT(*) AS Razem
    FROM dbo.ZamowieniaMieso
    WHERE KlientId IS NOT NULL
      AND DataZamowienia >= DATEADD(MONTH, -24, GETDATE())
      AND ISNULL(Status,'') NOT IN ('Anulowane','Anulowano')
    GROUP BY KlientId
    ORDER BY Razem DESC
)
SELECT
    YEAR(z.DataZamowienia) AS Rok,
    MONTH(z.DataZamowienia) AS Mies,
    COUNT(*) AS Zamowien,
    SUM(CASE WHEN z.DataPrzyjazdu IS NULL THEN 1 ELSE 0 END)            AS NULL_Prz,
    SUM(CASE WHEN z.DataPrzyjazdu IS NOT NULL THEN 1 ELSE 0 END)        AS OK_Prz,
    (SELECT TOP 1 KlientId FROM Top1)                                   AS KlientId
FROM dbo.ZamowieniaMieso z
JOIN Top1 t ON t.KlientId = z.KlientId
WHERE z.DataZamowienia >= DATEADD(MONTH, -24, GETDATE())
GROUP BY YEAR(z.DataZamowienia), MONTH(z.DataZamowienia)
ORDER BY Rok DESC, Mies DESC;


/* ────────────────────────────────────────────────────────────────────────────
   #18 ANOMALIE: zamówienia w przyszłości, daty rozjazdowe, podejrzane statusy.
       Pokaże stan ogólny czystości danych.
   ──────────────────────────────────────────────────────────────────────────── */
SELECT
    SUM(CASE WHEN DataZamowienia > GETDATE()                                          THEN 1 ELSE 0 END) AS Zam_W_Przyszlosci,
    SUM(CASE WHEN DataPrzyjazdu  > DATEADD(MONTH,3, GETDATE())                        THEN 1 ELSE 0 END) AS Prz_Daleko_W_Przyszlosc,
    SUM(CASE WHEN DataPrzyjazdu  < DataZamowienia                                     THEN 1 ELSE 0 END) AS Przyjazd_Przed_Zamowieniem,
    SUM(CASE WHEN DataUboju      < DataPrzyjazdu                                      THEN 1 ELSE 0 END) AS Uboj_Przed_Przyjazdem,
    SUM(CASE WHEN DataWydania    < DataUboju                                          THEN 1 ELSE 0 END) AS Wydanie_Przed_Ubojem,
    SUM(CASE WHEN DataPrzyjazdu IS NOT NULL AND DataZamowienia IS NULL                THEN 1 ELSE 0 END) AS Ma_Prz_Brak_Zam,
    SUM(CASE WHEN ISNULL(Status,'') = ''                                              THEN 1 ELSE 0 END) AS Pusty_Status,
    SUM(CASE WHEN KlientId IS NULL                                                    THEN 1 ELSE 0 END) AS Brak_Klienta,
    SUM(CASE WHEN KlientId IS NOT NULL AND KlientId <= 0                              THEN 1 ELSE 0 END) AS Klient_Nieprawidlowy,
    COUNT(*)                                                                                              AS Razem
FROM dbo.ZamowieniaMieso;


/* ════════════════════════════════════════════════════════════════════════════
   INTERPRETACJA WYNIKÓW (co mi przekazać, żebym wiedział co naprawić)

   #1  → Proc_Brak_Przyjazdu  → jeśli > 10% — problem realny.
   #2  → Wybierz pierwszy miesiąc z BrakPrzyjazdu=0 → moment "włączenia".
   #3  → ZyskZamowien GLOBALNIE + TOP-30 klientów którzy zyskają.
   #4  → Window function — kiedy Cum_Null_Pozniej spada do 0.
   #5  → Srednia_Dni < 7 → COALESCE bezpieczny. Dni_Ujemne_BLAD = brudne dane.
   #6  → 20 prawdziwych przykładów — sanity check.
   #7  → SCHEMA — pełna lista kolumn → mogę odkryć dodatkowe pola.
   #8  → INDEKSY → przewidzę performance COALESCE w WHERE.
   #9  → INNE KOLUMNY DAT → może lepszy fallback niż DataZamowienia.
   #10 → CHRONOLOGIA Id → czy ID rośnie z czasem (alternatywny fallback).
   #11 → STATUS distribution NULL → czy problem dotyczy tylko 1 statusu.
   #12 → USER distribution NULL → czy 1 operator nie wypełnia.
   #13 → CZY NULL = sieroty (bez towarów) czy realne zamówienia.
   #14 → KlientId: ile UNIKALNYCH klientów dotyczy NULL.
   #15 → HM.DK chronologia → faktury istnieją w starym okresie?
   #16 → CROSS-MATCH NULL ↔ faktury HANDEL → potwierdza realność starszych zam.
   #17 → TOP-1 klient → "tak będzie wyglądała ich historia po fixie".
   #18 → ANOMALIE → ogólna jakość danych.

   ────────────────────────────────────────────────────────────────────────────
   OPCJONALNY BACKFILL (uruchom DOPIERO po analizie #5 i #6):

   -- DRY RUN
   SELECT COUNT(*) FROM dbo.ZamowieniaMieso
   WHERE DataPrzyjazdu IS NULL AND DataZamowienia IS NOT NULL;

   -- WŁAŚCIWY backfill w transakcji
   -- BEGIN TRAN;
   -- UPDATE dbo.ZamowieniaMieso
   --    SET DataPrzyjazdu = DataZamowienia
   --  WHERE DataPrzyjazdu IS NULL AND DataZamowienia IS NOT NULL;
   -- -- COMMIT; albo ROLLBACK;
   ════════════════════════════════════════════════════════════════════════════ */
