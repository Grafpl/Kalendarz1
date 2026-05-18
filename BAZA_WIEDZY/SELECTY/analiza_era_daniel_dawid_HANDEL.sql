USE [HANDEL]
GO
SET NOCOUNT ON;

-- ============================================================================
-- ERA DANIELA i DAWIDA vs ERA MAI (po HM.DK.wystawil — nie po klasyfikacji)
-- ============================================================================
-- Cel:
--   SSCommon.ContractorClassification.CDim_Handlowiec_Val to CURRENT STATE.
--   Klienci, których dzisiaj przypisano do Mai, pokazują WSZYSTKIE swoje
--   historyczne faktury jako "Maja". To zaciemnia obraz: nie widać kto
--   FAKTYCZNIE wystawiał te faktury w dacie wystawienia.
--
--   HM.DK.wystawil (int) — ID użytkownika SSCommon.STUsers, który zapisał
--   dokument. W Sage zwykle to księgowa, NIE handlowiec — ale to pierwszy
--   ślad, plus dodatkowo szukamy w logu zmian klasyfikacji (STTraceLog).
-- ============================================================================

-- ===========================================================================
-- KROK 1 — Dyskoteryzacja schematu + znalezienie Daniel/Dawid/Maja
-- ===========================================================================
-- 1.0 Najpierw: jakie kolumny ma STPersonUsers (tabela linkujaca)?
SELECT '1.0 Kolumny SSCommon.STPersonUsers (diagnostyka)' AS Raport;
SELECT c.name AS Kolumna, t.name AS Typ, c.max_length AS Dlugosc
FROM sys.columns c
JOIN sys.types   t ON t.user_type_id = c.user_type_id
WHERE c.object_id = OBJECT_ID('SSCommon.STPersonUsers')
ORDER BY c.column_id;

-- 1.1 Jakie kolumny ma STUsers (moze ma imie/nazwisko bezposrednio)?
SELECT '1.1 Kolumny SSCommon.STUsers' AS Raport;
SELECT c.name AS Kolumna, t.name AS Typ, c.max_length AS Dlugosc
FROM sys.columns c
JOIN sys.types   t ON t.user_type_id = c.user_type_id
WHERE c.object_id = OBJECT_ID('SSCommon.STUsers')
ORDER BY c.column_id;

-- 1.2 Jakie kolumny ma STPersons?
SELECT '1.2 Kolumny SSCommon.STPersons' AS Raport;
SELECT c.name AS Kolumna, t.name AS Typ, c.max_length AS Dlugosc
FROM sys.columns c
JOIN sys.types   t ON t.user_type_id = c.user_type_id
WHERE c.object_id = OBJECT_ID('SSCommon.STPersons')
ORDER BY c.column_id;

-- 1.3 Probuje znalezc Daniel/Dawid/Maja tylko po STUsers (bez joinu osob)
SELECT '1.3 Kandydaci w STUsers (po LoginName / Description)' AS Raport;
SELECT
    Id,
    LoginName,
    Description,
    Disabled,
    Hidden,
    ActiveFrom,
    ActiveTo
FROM SSCommon.STUsers
WHERE LoginName LIKE N'%aniel%'
   OR LoginName LIKE N'%awid%'
   OR LoginName LIKE N'%maja%'
   OR LoginName LIKE N'%czapnik%'
   OR LoginName LIKE N'%sosi%'
   OR LoginName LIKE N'%luzarek%'
   OR LoginName LIKE N'%leonard%'
   OR LoginName LIKE N'%koncka%'
   OR Description LIKE N'%aniel%'
   OR Description LIKE N'%awid%'
   OR Description LIKE N'%aja%';

-- 1.4 STPersons po imieniu (niezaleznie od linka do users)
SELECT '1.4 Kandydaci w STPersons po imieniu' AS Raport;
SELECT
    Id,
    Guid,
    Firstname,
    SecondName,
    Surname,
    StringIdent
FROM SSCommon.STPersons
WHERE Firstname LIKE N'Daniel%'
   OR Firstname LIKE N'Dawid%'
   OR Firstname LIKE N'Maja%'
   OR Surname   LIKE N'%Czapnik%'
   OR Surname   LIKE N'%Sosi%'
   OR Surname   LIKE N'%luzarek%'
   OR Surname   LIKE N'%Leonard%';

-- 1.5 TOP 50 STUsers (zeby zobaczyc co tam jest)
SELECT '1.5 TOP 50 wpisow STUsers (Id, LoginName, Description)' AS Raport;
SELECT TOP 50 Id, LoginName, Description, Disabled FROM SSCommon.STUsers ORDER BY Id;

-- 1.6 Tabela tymczasowa: przypisanie etykiet na podstawie LoginName tylko
IF OBJECT_ID('tempdb..#Uzytk') IS NOT NULL DROP TABLE #Uzytk;
CREATE TABLE #Uzytk (
    UzytkId      int           NOT NULL,
    Login        nvarchar(100) NULL,
    Opis         nvarchar(500) NULL,
    Etykieta     nvarchar(20)  NULL
);

INSERT INTO #Uzytk(UzytkId, Login, Opis, Etykieta)
SELECT
    U.Id,
    U.LoginName,
    U.Description,
    CASE
      WHEN U.LoginName LIKE N'%aniel%'  OR U.Description LIKE N'%aniel%' OR U.LoginName LIKE N'%czapnik%' THEN N'Daniel'
      WHEN U.LoginName LIKE N'%awid%'   OR U.Description LIKE N'%awid%'  OR U.LoginName LIKE N'%sosi%' OR U.LoginName LIKE N'%luzarek%' THEN N'Dawid'
      WHEN U.LoginName LIKE N'%maja%'   OR U.Description LIKE N'%aja%'   OR U.LoginName LIKE N'%leonard%' THEN N'Maja'
      ELSE NULL
    END
FROM SSCommon.STUsers U
WHERE U.LoginName LIKE N'%aniel%'
   OR U.LoginName LIKE N'%awid%'
   OR U.LoginName LIKE N'%maja%'
   OR U.LoginName LIKE N'%czapnik%'
   OR U.LoginName LIKE N'%sosi%'
   OR U.LoginName LIKE N'%luzarek%'
   OR U.LoginName LIKE N'%leonard%'
   OR U.Description LIKE N'%aniel%'
   OR U.Description LIKE N'%awid%'
   OR U.Description LIKE N'%aja%';

SELECT '1.7 Znalezieni Daniel/Dawid/Maja (po LoginName + Description)' AS Raport;
SELECT Etykieta, UzytkId, Login, Opis FROM #Uzytk WHERE Etykieta IS NOT NULL ORDER BY Etykieta, UzytkId;

-- ===========================================================================
-- KROK 2 — Sanity check: TOP 50 wystawcow faktur sprzedazy + ich aktywnosc
-- ===========================================================================
SELECT '2.1 TOP 50 wystawcow HM.DK (typ=0 sprzedaz, niezanulowane, zaksiegowane)' AS Raport;
SELECT TOP 50
    DK.wystawil                                                                 AS UzytkId,
    U.LoginName                                                                 AS Login,
    U.Description                                                               AS Opis,
    COUNT(*)                                                                    AS LiczbaFaktur,
    CAST(SUM(DK.netto) AS decimal(18,2))                                        AS SumaNetto,
    MIN(DK.data)                                                                AS Najwczesniej,
    MAX(DK.data)                                                                AS Najpozniej,
    DATEDIFF(DAY, MIN(DK.data), MAX(DK.data))                                   AS Rozpietosc_Dni
FROM HM.DK DK
LEFT JOIN SSCommon.STUsers       U  ON U.Id          = DK.wystawil
WHERE DK.typ = 0
  AND DK.anulowany = 0
  AND DK.bufor = 0
  AND DK.wystawil IS NOT NULL
GROUP BY DK.wystawil, U.LoginName, U.Description
ORDER BY LiczbaFaktur DESC;

-- ===========================================================================
-- KROK 3 — Szukanie zmian CDim_Handlowiec_Val w STTraceLog (historia)
-- ===========================================================================
-- Najpierw sprawdz strukture STTraceLog (na slepo):
SELECT '3.1 Kolumny STTraceLog' AS Raport;
SELECT
    c.name      AS Kolumna,
    t.name      AS Typ,
    c.max_length AS Dlugosc
FROM sys.columns c
JOIN sys.types   t ON t.user_type_id = c.user_type_id
WHERE c.object_id = OBJECT_ID('SSCommon.STTraceLog')
ORDER BY c.column_id;

-- Probka 5 dowolnych wpisow STTraceLog — najpierw zobaczmy co tam jest, potem mozna filtrowac:
SELECT '3.2 Probka 5 wpisow STTraceLog (jakie kolumny i wartosci faktycznie sa)' AS Raport;
SELECT TOP 5 * FROM SSCommon.STTraceLog;

-- ===========================================================================
-- KROK 4 — Klasyfikacja faktur Mai (po CDim_Handlowiec_Val) wedlug wystawcy
-- ===========================================================================
-- Klienci aktualnie pod Maja = id_kontrahenta:
IF OBJECT_ID('tempdb..#KlienciMai') IS NOT NULL DROP TABLE #KlienciMai;
CREATE TABLE #KlienciMai (KhId int PRIMARY KEY);
INSERT INTO #KlienciMai(KhId)
SELECT DISTINCT cc.ElementId
FROM SSCommon.ContractorClassification cc
WHERE cc.CDim_Handlowiec_Val = N'Maja';

SELECT '4.1 Klienci aktualnie pod Maja' AS Raport, COUNT(*) AS LiczbaKlientow FROM #KlienciMai;

SELECT '4.2 Faktury klientow Mai per wystawca (kto NAPRAWDE wystawial)' AS Raport;
SELECT
    DK.wystawil                                            AS UzytkId,
    U.LoginName                                            AS Login,
    U.Description                                          AS Opis,
    COUNT(*)                                               AS LiczbaFaktur,
    CAST(SUM(DK.netto) AS decimal(18,2))                   AS SumaNetto,
    MIN(DK.data)                                           AS Najwczesniej,
    MAX(DK.data)                                           AS Najpozniej
FROM HM.DK DK
INNER JOIN #KlienciMai KM        ON KM.KhId      = DK.khid
LEFT JOIN SSCommon.STUsers U     ON U.Id         = DK.wystawil
WHERE DK.typ = 0
  AND DK.anulowany = 0
  AND DK.bufor = 0
GROUP BY DK.wystawil, U.LoginName, U.Description
ORDER BY LiczbaFaktur DESC;

-- ===========================================================================
-- KROK 5 — Wystawcy faktur klientow Mai w czasie (rozklad miesieczny)
-- ===========================================================================
SELECT '5.1 Wystawcy faktur klientow Mai per miesiac' AS Raport;
WITH FaktBaza AS (
    SELECT
        CONVERT(char(7), DK.data, 120)                       AS RokMiesiac,
        DK.wystawil                                          AS UzytkId,
        ISNULL(U.LoginName, CONVERT(varchar(20), DK.wystawil)) AS Wystawca,
        DK.netto                                             AS Netto
    FROM HM.DK DK
    INNER JOIN #KlienciMai KM        ON KM.KhId      = DK.khid
    LEFT JOIN SSCommon.STUsers U     ON U.Id         = DK.wystawil
    WHERE DK.typ = 0
      AND DK.anulowany = 0
      AND DK.bufor = 0
      AND DK.data >= '2024-04-01'
)
SELECT
    RokMiesiac,
    Wystawca,
    UzytkId,
    COUNT(*)                              AS LiczbaFaktur,
    CAST(SUM(Netto) AS decimal(18,2))     AS Netto
FROM FaktBaza
GROUP BY RokMiesiac, Wystawca, UzytkId
HAVING SUM(Netto) > 50000
ORDER BY RokMiesiac, Netto DESC;

-- ===========================================================================
-- KROK 6 — Per klient Mai: kto wystawial faktury w erze Daniela vs erze Mai
-- ===========================================================================
SELECT '6.1 Per klient: porownanie wystawcy era Daniela vs era Mai' AS Raport;
WITH FaktKlient AS (
    SELECT
        DK.khid,
        ISNULL(KH.Name, CONVERT(varchar(20), DK.khid))         AS KlientNazwa,
        CASE WHEN DK.data < '2025-10-01' THEN 'EraPrzedMaja' ELSE 'EraMai' END AS Era,
        DK.wystawil                                            AS UzytkId,
        ISNULL(U.LoginName, CONVERT(varchar(20), DK.wystawil)) AS Wystawca,
        DK.netto                                               AS Netto
    FROM HM.DK DK
    INNER JOIN #KlienciMai KM        ON KM.KhId   = DK.khid
    LEFT JOIN SSCommon.STContractors KH ON KH.Id = DK.khid
    LEFT JOIN SSCommon.STUsers U     ON U.Id      = DK.wystawil
    WHERE DK.typ = 0
      AND DK.anulowany = 0
      AND DK.bufor = 0
      AND DK.data >= '2024-04-01'
),
PerKlientWystawca AS (
    SELECT
        khid, KlientNazwa, Era, Wystawca,
        SUM(Netto)  AS Netto,
        COUNT(*)    AS Faktur
    FROM FaktKlient
    GROUP BY khid, KlientNazwa, Era, Wystawca
),
GlowniWystawcy AS (
    SELECT
        khid, Era,
        Wystawca       AS GlownyWystawca,
        Faktur         AS GlowneFaktur,
        Netto          AS GlowneNetto,
        ROW_NUMBER() OVER (PARTITION BY khid, Era ORDER BY Netto DESC) AS rn
    FROM PerKlientWystawca
)
SELECT
    KM.KhId,
    KH.Name                                                            AS Klient,
    DANIELDAWID.GlownyWystawca                                         AS Wystawca_PrzedMaja,
    DANIELDAWID.GlowneFaktur                                           AS Faktur_PrzedMaja,
    CAST(DANIELDAWID.GlowneNetto AS decimal(18,2))                     AS Netto_PrzedMaja,
    MAJOWY.GlownyWystawca                                              AS Wystawca_EraMai,
    MAJOWY.GlowneFaktur                                                AS Faktur_EraMai,
    CAST(MAJOWY.GlowneNetto AS decimal(18,2))                          AS Netto_EraMai
FROM #KlienciMai KM
LEFT JOIN SSCommon.STContractors KH ON KH.Id = KM.KhId
LEFT JOIN GlowniWystawcy DANIELDAWID ON DANIELDAWID.khid = KM.KhId AND DANIELDAWID.Era = 'EraPrzedMaja' AND DANIELDAWID.rn = 1
LEFT JOIN GlowniWystawcy MAJOWY      ON MAJOWY.khid      = KM.KhId AND MAJOWY.Era      = 'EraMai'      AND MAJOWY.rn      = 1
ORDER BY ISNULL(DANIELDAWID.GlowneNetto, 0) + ISNULL(MAJOWY.GlowneNetto, 0) DESC;

-- ===========================================================================
-- KROK 7 — Maja jako wystawca: ile sama wystawia faktury (a ile ksiegowa)
-- ===========================================================================
DECLARE @MajaIdsCSV nvarchar(200);
SELECT @MajaIdsCSV = STRING_AGG(CAST(UzytkId AS nvarchar(20)), ',')
FROM #Uzytk
WHERE Etykieta = 'Maja';

SELECT '7.1 Maja IDs w HANDEL (CSV)' AS Raport, @MajaIdsCSV AS MajaIds;

SELECT '7.2 Czy Maja wystawia sama faktury — udzial w fakturach KLIENTOW MAI' AS Raport;
SELECT
    CASE
        WHEN @MajaIdsCSV IS NOT NULL AND CHARINDEX(',' + CAST(DK.wystawil AS varchar(20)) + ',', ',' + @MajaIdsCSV + ',') > 0 THEN 'Maja sama'
        ELSE 'Inny user (ksiegowa/admin)'
    END                                                AS WystawcaTyp,
    COUNT(*)                                           AS LiczbaFaktur,
    CAST(SUM(DK.netto) AS decimal(18,2))               AS SumaNetto,
    CAST(100.0 * COUNT(*) / NULLIF(SUM(COUNT(*)) OVER (), 0) AS decimal(6,2)) AS Proc_Faktur
FROM HM.DK DK
INNER JOIN #KlienciMai KM ON KM.KhId = DK.khid
WHERE DK.typ = 0
  AND DK.anulowany = 0
  AND DK.bufor = 0
  AND DK.data >= '2025-10-01'
GROUP BY
    CASE
        WHEN @MajaIdsCSV IS NOT NULL AND CHARINDEX(',' + CAST(DK.wystawil AS varchar(20)) + ',', ',' + @MajaIdsCSV + ',') > 0 THEN 'Maja sama'
        ELSE 'Inny user (ksiegowa/admin)'
    END;

-- ===========================================================================
-- KROK 8 — Daniel/Dawid jako wystawcy: czy w ogole istnieja w HANDEL?
-- ===========================================================================
DECLARE @DanielIdsCSV nvarchar(200);
DECLARE @DawidIdsCSV  nvarchar(200);

SELECT @DanielIdsCSV = STRING_AGG(CAST(UzytkId AS nvarchar(20)), ',') FROM #Uzytk WHERE Etykieta = 'Daniel';
SELECT @DawidIdsCSV  = STRING_AGG(CAST(UzytkId AS nvarchar(20)), ',') FROM #Uzytk WHERE Etykieta = 'Dawid';

SELECT '8.1 Daniel/Dawid IDs w HANDEL (CSV)' AS Raport, @DanielIdsCSV AS DanielIds, @DawidIdsCSV AS DawidIds;

SELECT '8.2 Faktury wystawione bezposrednio przez Daniela/Dawida (jesli istnieja w HANDEL)' AS Raport;
SELECT
    CASE
        WHEN @DanielIdsCSV IS NOT NULL AND CHARINDEX(',' + CAST(DK.wystawil AS varchar(20)) + ',', ',' + @DanielIdsCSV + ',') > 0 THEN 'Daniel'
        WHEN @DawidIdsCSV  IS NOT NULL AND CHARINDEX(',' + CAST(DK.wystawil AS varchar(20)) + ',', ',' + @DawidIdsCSV  + ',') > 0 THEN 'Dawid'
    END                                                            AS Etykieta,
    DK.wystawil                                                    AS UzytkId,
    U.LoginName                                                    AS Login,
    U.Description                                                  AS Opis,
    COUNT(*)                                                       AS LiczbaFaktur,
    CAST(SUM(DK.netto) AS decimal(18,2))                           AS SumaNetto,
    MIN(DK.data)                                                   AS Najwczesniej,
    MAX(DK.data)                                                   AS Najpozniej
FROM HM.DK DK
LEFT JOIN SSCommon.STUsers       U  ON U.Id          = DK.wystawil
WHERE DK.typ = 0
  AND DK.anulowany = 0
  AND DK.bufor = 0
  AND (
       (@DanielIdsCSV IS NOT NULL AND CHARINDEX(',' + CAST(DK.wystawil AS varchar(20)) + ',', ',' + @DanielIdsCSV + ',') > 0)
    OR (@DawidIdsCSV  IS NOT NULL AND CHARINDEX(',' + CAST(DK.wystawil AS varchar(20)) + ',', ',' + @DawidIdsCSV  + ',') > 0)
  )
GROUP BY
    CASE
        WHEN @DanielIdsCSV IS NOT NULL AND CHARINDEX(',' + CAST(DK.wystawil AS varchar(20)) + ',', ',' + @DanielIdsCSV + ',') > 0 THEN 'Daniel'
        WHEN @DawidIdsCSV  IS NOT NULL AND CHARINDEX(',' + CAST(DK.wystawil AS varchar(20)) + ',', ',' + @DawidIdsCSV  + ',') > 0 THEN 'Dawid'
    END,
    DK.wystawil, U.LoginName, U.Description;

-- ===========================================================================
-- KROK 9 — Klienci ktorych OBECNIE NIE MA pod Maja, ale Daniel/Dawid robil dla
-- nich faktury (= klienci ktorych prawdopodobnie Maja powinna miec, ale ich utraciono)
-- ===========================================================================
SELECT '9.1 Klienci historycznie wystawiani przez Daniela/Dawida (NIE pod Maja dzis)' AS Raport;
WITH DD AS (
    SELECT DK.khid,
           CASE
               WHEN @DanielIdsCSV IS NOT NULL AND CHARINDEX(',' + CAST(DK.wystawil AS varchar(20)) + ',', ',' + @DanielIdsCSV + ',') > 0 THEN 'Daniel'
               WHEN @DawidIdsCSV  IS NOT NULL AND CHARINDEX(',' + CAST(DK.wystawil AS varchar(20)) + ',', ',' + @DawidIdsCSV  + ',') > 0 THEN 'Dawid'
           END                                  AS Etykieta,
           DK.netto                             AS Netto,
           DK.data                              AS Data
    FROM HM.DK DK
    WHERE DK.typ = 0
      AND DK.anulowany = 0
      AND DK.bufor = 0
      AND (
           (@DanielIdsCSV IS NOT NULL AND CHARINDEX(',' + CAST(DK.wystawil AS varchar(20)) + ',', ',' + @DanielIdsCSV + ',') > 0)
        OR (@DawidIdsCSV  IS NOT NULL AND CHARINDEX(',' + CAST(DK.wystawil AS varchar(20)) + ',', ',' + @DawidIdsCSV  + ',') > 0)
      )
)
SELECT
    DD.khid                                          AS KontrahentId,
    KH.Name                                          AS Klient,
    cc.CDim_Handlowiec_Val                           AS ObecnyHandlowiec,
    COUNT(*)                                         AS LiczbaFakturDD,
    CAST(SUM(DD.Netto) AS decimal(18,2))             AS NettoDD,
    MIN(DD.Data)                                     AS Najwczesniej,
    MAX(DD.Data)                                     AS Najpozniej
FROM DD
LEFT JOIN SSCommon.STContractors          KH ON KH.Id        = DD.khid
LEFT JOIN SSCommon.ContractorClassification cc ON cc.ElementId = DD.khid
WHERE (cc.CDim_Handlowiec_Val IS NULL OR cc.CDim_Handlowiec_Val <> N'Maja')
GROUP BY DD.khid, KH.Name, cc.CDim_Handlowiec_Val
ORDER BY SUM(DD.Netto) DESC;

-- ===========================================================================
-- KROK 10 — Tygodniowa intensywnosc Mai vs era Daniela na klientach Mai
-- ===========================================================================
SELECT '10.1 Srednia tygodniowa faktur klientow Mai — era przed Maja vs era Mai' AS Raport;
WITH FaktKlient AS (
    SELECT
        CASE WHEN DK.data < '2025-10-01' THEN 'EraPrzedMaja' ELSE 'EraMai' END AS Era,
        DK.data,
        DK.netto
    FROM HM.DK DK
    INNER JOIN #KlienciMai KM ON KM.KhId = DK.khid
    WHERE DK.typ = 0
      AND DK.anulowany = 0
      AND DK.bufor = 0
      AND DK.data >= '2024-04-01'
)
SELECT
    Era,
    COUNT(*)                                                              AS LiczbaFaktur,
    CAST(SUM(netto) AS decimal(18,2))                                     AS SumaNetto,
    DATEDIFF(DAY, MIN(data), MAX(data)) / 7.0                             AS Tygodni,
    CAST(COUNT(*) / NULLIF(DATEDIFF(DAY, MIN(data), MAX(data)) / 7.0, 0) AS decimal(10,2)) AS FakturNaTydzien,
    CAST(SUM(netto) / NULLIF(DATEDIFF(DAY, MIN(data), MAX(data)) / 7.0, 0) AS decimal(18,2)) AS NettoNaTydzien
FROM FaktKlient
GROUP BY Era;

-- ===========================================================================
-- KROK 11 — Mix produktowy KIEDYS (era Daniela) vs DZISIAJ (era Mai)
-- ===========================================================================
SELECT '11.1 Mix produktowy klientow Mai per era (swieze/mrozone/inne)' AS Raport;
WITH FaktPozycje AS (
    SELECT
        CASE WHEN DK.data < '2025-10-01' THEN 'EraPrzedMaja' ELSE 'EraMai' END AS Era,
        DP.idtw,
        TW.katalog,
        CASE
            WHEN TW.katalog IN (65882, 67095, 67104)               THEN 'Swieze'
            WHEN TW.katalog IN (67153)                              THEN 'Mrozone'
            WHEN TW.katalog = 67094                                 THEN 'Odpady'
            ELSE 'Inne'
        END AS Kategoria,
        DP.ilosc,
        DP.wartNetto
    FROM HM.DK DK
    INNER JOIN #KlienciMai KM ON KM.KhId = DK.khid
    INNER JOIN HM.DP DP       ON DP.super = DK.id
    INNER JOIN HM.TW TW       ON TW.id    = DP.idtw
    WHERE DK.typ = 0
      AND DK.anulowany = 0
      AND DK.bufor = 0
      AND DK.data >= '2024-04-01'
)
SELECT
    Era,
    Kategoria,
    CAST(SUM(ilosc) AS decimal(18,2))                                  AS Kg,
    CAST(SUM(wartNetto) AS decimal(18,2))                              AS Netto,
    CAST(100.0 * SUM(wartNetto) / NULLIF(SUM(SUM(wartNetto)) OVER (PARTITION BY Era), 0) AS decimal(6,2)) AS Udzial_Proc
FROM FaktPozycje
GROUP BY Era, Kategoria
ORDER BY Era, Netto DESC;

-- ===========================================================================
-- KROK 12 — Komu klienci Mai placili: srednia cena/kg era Daniela vs Maja
-- ===========================================================================
SELECT '12.1 Srednia cena/kg klientow Mai per era (TOP towary)' AS Raport;
WITH FaktPozycje AS (
    SELECT
        CASE WHEN DK.data < '2025-10-01' THEN 'EraPrzedMaja' ELSE 'EraMai' END AS Era,
        DP.idtw,
        TW.nazwa AS TowarNazwa,
        DP.ilosc,
        DP.wartNetto
    FROM HM.DK DK
    INNER JOIN #KlienciMai KM ON KM.KhId = DK.khid
    INNER JOIN HM.DP DP       ON DP.super = DK.id
    INNER JOIN HM.TW TW       ON TW.id    = DP.idtw
    WHERE DK.typ = 0
      AND DK.anulowany = 0
      AND DK.bufor = 0
      AND DK.data >= '2024-04-01'
      AND DP.ilosc > 0
)
SELECT
    TowarNazwa,
    CAST(SUM(CASE WHEN Era = 'EraPrzedMaja' THEN ilosc      ELSE 0 END) AS decimal(18,2)) AS Kg_PrzedMaja,
    CAST(SUM(CASE WHEN Era = 'EraPrzedMaja' THEN wartNetto  ELSE 0 END) / NULLIF(SUM(CASE WHEN Era='EraPrzedMaja' THEN ilosc ELSE 0 END), 0) AS decimal(10,2)) AS Cena_PrzedMaja,
    CAST(SUM(CASE WHEN Era = 'EraMai'       THEN ilosc      ELSE 0 END) AS decimal(18,2)) AS Kg_EraMai,
    CAST(SUM(CASE WHEN Era = 'EraMai'       THEN wartNetto  ELSE 0 END) / NULLIF(SUM(CASE WHEN Era='EraMai' THEN ilosc ELSE 0 END), 0) AS decimal(10,2)) AS Cena_EraMai
FROM FaktPozycje
GROUP BY TowarNazwa
HAVING SUM(ilosc) > 5000
ORDER BY SUM(wartNetto) DESC;

-- ===========================================================================
-- KROK 13 — Per klient: trend Daniela/Dawida vs Mai (tempo M-na-M)
-- ===========================================================================
SELECT '13.1 Tempo per klient: srednia netto/mies w erze przed Maja vs erze Mai' AS Raport;
WITH FaktKlient AS (
    SELECT
        DK.khid,
        ISNULL(KH.Name, CONVERT(varchar(20), DK.khid)) AS KlientNazwa,
        CASE WHEN DK.data < '2025-10-01' THEN 'EraPrzedMaja' ELSE 'EraMai' END AS Era,
        DK.data,
        DK.netto
    FROM HM.DK DK
    INNER JOIN #KlienciMai KM ON KM.KhId = DK.khid
    LEFT JOIN SSCommon.STContractors KH ON KH.Id = DK.khid
    WHERE DK.typ = 0
      AND DK.anulowany = 0
      AND DK.bufor = 0
      AND DK.data >= '2024-04-01'
),
PerKlientEra AS (
    SELECT
        khid, KlientNazwa, Era,
        SUM(netto)                                AS Netto,
        COUNT(*)                                  AS Faktur,
        MIN(data)                                 AS Najwczesniej,
        MAX(data)                                 AS Najpozniej,
        DATEDIFF(MONTH, MIN(data), MAX(data)) + 1 AS Miesiecy
    FROM FaktKlient
    GROUP BY khid, KlientNazwa, Era
)
SELECT
    KM.KhId,
    KH.Name AS Klient,
    CAST(PrzedMaja.Netto                              AS decimal(18,2))  AS PrzedMaja_Netto,
    PrzedMaja.Miesiecy                                                   AS PrzedMaja_Mies,
    CAST(PrzedMaja.Netto / NULLIF(PrzedMaja.Miesiecy,0) AS decimal(18,2)) AS PrzedMaja_NettoMies,
    CAST(MajaEra.Netto                                AS decimal(18,2))  AS Maja_Netto,
    MajaEra.Miesiecy                                                     AS Maja_Mies,
    CAST(MajaEra.Netto / NULLIF(MajaEra.Miesiecy,0)   AS decimal(18,2))  AS Maja_NettoMies,
    CAST(100.0 *
         (MajaEra.Netto / NULLIF(MajaEra.Miesiecy,0) - PrzedMaja.Netto / NULLIF(PrzedMaja.Miesiecy,0))
         / NULLIF(PrzedMaja.Netto / NULLIF(PrzedMaja.Miesiecy,0), 0) AS decimal(10,1)) AS ZmianaTempa_Proc
FROM #KlienciMai KM
LEFT JOIN SSCommon.STContractors KH ON KH.Id = KM.KhId
LEFT JOIN PerKlientEra PrzedMaja ON PrzedMaja.khid = KM.KhId AND PrzedMaja.Era = 'EraPrzedMaja'
LEFT JOIN PerKlientEra MajaEra   ON MajaEra.khid   = KM.KhId AND MajaEra.Era   = 'EraMai'
ORDER BY ISNULL(MajaEra.Netto, 0) + ISNULL(PrzedMaja.Netto, 0) DESC;

-- ===========================================================================
-- KROK 14 — Sumaryczny werdykt: era Daniela/Dawida vs era Mai (tylko klienci Mai)
-- ===========================================================================
SELECT '14.1 PODSUMOWANIE: era przed Maja vs era Mai (klienci Mai)' AS Raport;
WITH Sumka AS (
    SELECT
        CASE WHEN DK.data < '2025-10-01' THEN 'EraPrzedMaja' ELSE 'EraMai' END AS Era,
        SUM(DK.netto)                              AS Netto,
        COUNT(*)                                   AS Faktur,
        COUNT(DISTINCT DK.khid)                    AS Klientow,
        MIN(DK.data)                               AS Od,
        MAX(DK.data)                               AS Do,
        DATEDIFF(MONTH, MIN(DK.data), MAX(DK.data)) + 1 AS Miesiecy
    FROM HM.DK DK
    INNER JOIN #KlienciMai KM ON KM.KhId = DK.khid
    WHERE DK.typ = 0
      AND DK.anulowany = 0
      AND DK.bufor = 0
      AND DK.data >= '2024-04-01'
    GROUP BY CASE WHEN DK.data < '2025-10-01' THEN 'EraPrzedMaja' ELSE 'EraMai' END
)
SELECT
    Era, Od, Do, Miesiecy,
    Klientow, Faktur,
    CAST(Netto AS decimal(18,2))                              AS Netto,
    CAST(Netto / NULLIF(Miesiecy,0) AS decimal(18,2))         AS NettoMies,
    CAST(1.0 * Faktur / NULLIF(Miesiecy,0) AS decimal(10,1))  AS FakturMies
FROM Sumka
ORDER BY Era DESC;

-- ===========================================================================
-- KROK 15 — Maja vs Daniel/Dawid jako AUTORZY (na ich wystawionych fakturach,
--          niezaleznie od dzisiejszej klasyfikacji klienta)
-- ===========================================================================
SELECT '15.1 Wystawca FAKTYCZNY: porownanie 18-mies. obrotow Maja vs Daniel+Dawid' AS Raport;
DECLARE @WszyscyCSV nvarchar(500);
SELECT @WszyscyCSV = STRING_AGG(CAST(UzytkId AS nvarchar(20)), ',') FROM #Uzytk WHERE Etykieta IN ('Daniel','Dawid','Maja');

WITH FaktAutor AS (
    SELECT
        CASE
            WHEN @MajaIdsCSV    IS NOT NULL AND CHARINDEX(',' + CAST(DK.wystawil AS varchar(20)) + ',', ',' + @MajaIdsCSV    + ',') > 0 THEN 'Maja'
            WHEN @DanielIdsCSV  IS NOT NULL AND CHARINDEX(',' + CAST(DK.wystawil AS varchar(20)) + ',', ',' + @DanielIdsCSV  + ',') > 0 THEN 'Daniel'
            WHEN @DawidIdsCSV   IS NOT NULL AND CHARINDEX(',' + CAST(DK.wystawil AS varchar(20)) + ',', ',' + @DawidIdsCSV   + ',') > 0 THEN 'Dawid'
        END AS Autor,
        CONVERT(char(7), DK.data, 120) AS RokMiesiac,
        DK.netto
    FROM HM.DK DK
    WHERE DK.typ = 0
      AND DK.anulowany = 0
      AND DK.bufor = 0
      AND DK.data >= '2024-04-01'
      AND @WszyscyCSV IS NOT NULL
      AND CHARINDEX(',' + CAST(DK.wystawil AS varchar(20)) + ',', ',' + @WszyscyCSV + ',') > 0
)
SELECT
    Autor,
    RokMiesiac,
    COUNT(*)                            AS Faktur,
    CAST(SUM(netto) AS decimal(18,2))   AS Netto
FROM FaktAutor
WHERE Autor IS NOT NULL
GROUP BY Autor, RokMiesiac
ORDER BY RokMiesiac, Autor;

-- ===========================================================================
-- KROK 16 — Cleanup
-- ===========================================================================
DROP TABLE IF EXISTS #KlienciMai;
DROP TABLE IF EXISTS #Uzytk;

SELECT '=== KONIEC analizy ERA DANIEL/DAWID/MAJA w HANDEL ===' AS Status;
