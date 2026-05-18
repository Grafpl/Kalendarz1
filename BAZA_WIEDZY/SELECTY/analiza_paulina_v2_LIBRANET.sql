USE [LibraNet]
GO
SET NOCOUNT ON;

-- ============================================================================
-- ANALIZA PAULINY (zakup zywca) — v2 z PRAWIDLOWYM schematem LibraNet
-- ============================================================================
-- Paulina Koncka, UserID=1122 w operators
-- Operuje na:
--   - HarmonogramDostaw       (kontrakty / plan dostaw zywca) — KtoStwo/KtoMod/KtoUtw
--   - FarmerCalc              (rozliczenia ubojni hodowcy)    — CreatedBy/ModifiedBy/FullUser/EmptyUser/VetUser
--   - Pozyskiwanie_Hodowcy    (CRM hodowcow)                  — PrzypisanyDo
--   - Pozyskiwanie_Aktywnosci (notatki / telefony CRM)        — UzytkownikId
--   - PartiaDostawca/listapartii (po produkcji)               — CreateOperator
-- ============================================================================

-- ===========================================================================
-- KROK 0 — Diagnostyka: znajdz wszystkie identyfikatory Pauliny
-- ===========================================================================
SELECT '0.1 Paulina w tabeli operators' AS Raport;
SELECT ID, GUID, Name, CreateData, ModificationData, LastSuccessfulLogin
FROM operators
WHERE Name LIKE '%aulina%' OR ID = '1122';

SELECT '0.2 Paulina w UserHandlowcy (mapowanie z erą Daniela)' AS Raport;
SELECT ID, UserID, HandlowiecName, CreatedAt
FROM UserHandlowcy
WHERE UserID = '1122' OR HandlowiecName LIKE '%aulina%';

-- ===========================================================================
-- KROK 1 — HarmonogramDostaw: plan dostaw zywca tworzony przez Pauline
-- ===========================================================================
SELECT '1.1 HarmonogramDostaw — kto tworzy plan dostaw (TOP 20 userow)' AS Raport;
SELECT TOP 20
    H.KtoStwo                                                AS UserId,
    O.Name                                                   AS UserName,
    COUNT(*)                                                 AS LiczbaWpisow,
    SUM(CAST(H.Auta AS int))                                 AS SumaAut,
    SUM(CAST(H.SztukiDek AS int))                            AS SumaSztDek,
    MIN(H.DataUtw)                                           AS Najwczesniej,
    MAX(H.DataUtw)                                           AS Najpozniej,
    COUNT(DISTINCT H.DostawcaID)                             AS RoznychDostawcow
FROM HarmonogramDostaw H
LEFT JOIN operators O ON CAST(O.ID AS int) = H.KtoStwo
WHERE H.KtoStwo IS NOT NULL
GROUP BY H.KtoStwo, O.Name
ORDER BY LiczbaWpisow DESC;

SELECT '1.2 Paulina HarmonogramDostaw — produktywnosc per miesiac' AS Raport;
SELECT
    CONVERT(char(7), H.DataUtw, 120)                         AS RokMiesiac,
    COUNT(*)                                                 AS LiczbaWpisow,
    COUNT(DISTINCT H.DostawcaID)                             AS RoznychDostawcow,
    SUM(CAST(H.Auta AS int))                                 AS SumaAut,
    SUM(CAST(H.SztukiDek AS int))                            AS SumaSztDek,
    AVG(CAST(H.WagaDek AS decimal(10,2)))                    AS SredniaWagaDek,
    SUM(CAST(CASE WHEN H.Posrednik = 1 THEN 1 ELSE 0 END AS int)) AS PrzezPosrednika,
    SUM(CAST(CASE WHEN H.Utworzone = 1 THEN 1 ELSE 0 END AS int)) AS Utworzono,
    SUM(CAST(CASE WHEN H.Wysłane   = 1 THEN 1 ELSE 0 END AS int)) AS Wyslano,
    SUM(CAST(CASE WHEN H.Otrzymane = 1 THEN 1 ELSE 0 END AS int)) AS Otrzymano
FROM HarmonogramDostaw H
WHERE H.KtoStwo = 1122
  AND H.DataUtw IS NOT NULL
GROUP BY CONVERT(char(7), H.DataUtw, 120)
ORDER BY RokMiesiac;

SELECT '1.3 Paulina vs inni — udzial w tworzeniu harmonogramu (od 2024-10)' AS Raport;
WITH Tworcy AS (
    SELECT
        H.KtoStwo                                            AS UserId,
        O.Name                                               AS UserName,
        COUNT(*)                                             AS LiczbaWpisow
    FROM HarmonogramDostaw H
    LEFT JOIN operators O ON CAST(O.ID AS int) = H.KtoStwo
    WHERE H.KtoStwo IS NOT NULL
      AND H.DataUtw >= '2024-10-01'
    GROUP BY H.KtoStwo, O.Name
)
SELECT
    UserId,
    ISNULL(UserName, '(brak w operators)')                                    AS UserName,
    LiczbaWpisow,
    CAST(100.0 * LiczbaWpisow / NULLIF(SUM(LiczbaWpisow) OVER (), 0) AS decimal(6,2)) AS Udzial_Proc
FROM Tworcy
ORDER BY LiczbaWpisow DESC;

-- ===========================================================================
-- KROK 2 — Typy umow i ceny w harmonogramie Pauliny
-- ===========================================================================
SELECT '2.1 Paulina — typy umow w harmonogramie (Kontrakt / Wolnyrynek / etc.)' AS Raport;
SELECT
    H.TypUmowy,
    H.TypCeny,
    COUNT(*)                                                 AS LiczbaWpisow,
    SUM(CAST(H.SztukiDek AS int))                            AS SumaSzt,
    AVG(CAST(H.Cena AS decimal(10,2)))                       AS SredniaCena,
    MIN(CAST(H.Cena AS decimal(10,2)))                       AS MinCena,
    MAX(CAST(H.Cena AS decimal(10,2)))                       AS MaxCena
FROM HarmonogramDostaw H
WHERE H.KtoStwo = 1122
  AND H.DataUtw >= '2024-10-01'
GROUP BY H.TypUmowy, H.TypCeny
ORDER BY LiczbaWpisow DESC;

SELECT '2.2 Paulina — sredni czas miedzy stworzeniem a potwierdzeniem wagi' AS Raport;
SELECT
    AVG(DATEDIFF(DAY, H.DataUtw, H.DataPotwWaga))            AS SredniaDniDoWagi,
    AVG(DATEDIFF(DAY, H.DataUtw, H.DataPotwSztuki))          AS SredniaDniDoSzt,
    COUNT(*)                                                 AS LiczbaProb
FROM HarmonogramDostaw H
WHERE H.KtoStwo = 1122
  AND H.DataPotwWaga IS NOT NULL
  AND H.DataUtw IS NOT NULL
  AND H.DataUtw >= '2024-10-01';

-- ===========================================================================
-- KROK 3 — FarmerCalc: rozliczenia ubojni hodowcy
-- ===========================================================================
SELECT '3.1 FarmerCalc — kto tworzy/modyfikuje rozliczenia (TOP 15)' AS Raport;
WITH UzeOperows AS (
    -- Operators ma ID jako varchar(15), wiec ostroznie:
    SELECT ID, Name, TRY_CAST(ID AS int) AS IdInt FROM operators WHERE Name IS NOT NULL
)
SELECT TOP 15
    F.CreatedBy                                              AS CreatedBy,
    O1.Name                                                  AS CreatedByName,
    COUNT(*)                                                 AS LiczbaRozliczen,
    CAST(SUM(F.NettoWeight) AS decimal(18,2))                AS SumaNettoKg,
    CAST(SUM(F.NettoWeight * F.Price1) AS decimal(18,2))     AS SumaWartosc,
    MIN(F.CalcDate)                                          AS Najwczesniej,
    MAX(F.CalcDate)                                          AS Najpozniej
FROM FarmerCalc F
LEFT JOIN operators O1 ON O1.Name = F.CreatedBy OR O1.ID = F.CreatedBy
WHERE F.CreatedBy IS NOT NULL
  AND F.Deleted = 0
GROUP BY F.CreatedBy, O1.Name
ORDER BY LiczbaRozliczen DESC;

SELECT '3.2 Paulina w FarmerCalc — szukam po nazwie (jesli CreatedBy=name)' AS Raport;
SELECT
    F.CreatedBy,
    COUNT(*)                                                 AS LiczbaRozliczen,
    CAST(SUM(F.NettoWeight) AS decimal(18,2))                AS SumaNettoKg,
    CAST(SUM(F.NettoWeight * F.Price1) AS decimal(18,2))     AS SumaWartosc
FROM FarmerCalc F
WHERE F.CreatedBy LIKE '%aulina%' OR F.CreatedBy = '1122' OR F.CreatedBy LIKE '%oncka%'
  AND F.Deleted = 0
GROUP BY F.CreatedBy;

-- ===========================================================================
-- KROK 4 — Pozyskiwanie_Hodowcy (CRM): jacy hodowcy sa "przypisani" do Pauliny
-- ===========================================================================
SELECT '4.1 Pozyskiwanie_Hodowcy — kto ma przypisanych hodowcow (TOP 10)' AS Raport;
SELECT TOP 10
    PrzypisanyDo,
    COUNT(*)                                                 AS LiczbaHodowcow,
    SUM(CASE WHEN Aktywny = 1 THEN 1 ELSE 0 END)             AS Aktywnych
FROM Pozyskiwanie_Hodowcy
WHERE PrzypisanyDo IS NOT NULL
GROUP BY PrzypisanyDo
ORDER BY LiczbaHodowcow DESC;

SELECT '4.2 Paulina — przypisani hodowcy po statusach' AS Raport;
SELECT
    Status,
    COUNT(*)                                                 AS Liczba,
    SUM(CASE WHEN Aktywny = 1 THEN 1 ELSE 0 END)             AS Aktywnych
FROM Pozyskiwanie_Hodowcy
WHERE PrzypisanyDo LIKE '%aulina%' OR PrzypisanyDo = '1122'
GROUP BY Status
ORDER BY Liczba DESC;

-- ===========================================================================
-- KROK 5 — Pozyskiwanie_Aktywnosci: kontakty z hodowcami CRM
-- ===========================================================================
SELECT '5.1 Pozyskiwanie_Aktywnosci — top userzy CRM (telefony/notatki)' AS Raport;
SELECT TOP 15
    UzytkownikId,
    UzytkownikNazwa,
    TypAktywnosci,
    COUNT(*)                                                 AS Liczba,
    MIN(DataUtworzenia)                                      AS Najwczesniej,
    MAX(DataUtworzenia)                                      AS Najpozniej
FROM Pozyskiwanie_Aktywnosci
WHERE UzytkownikId IS NOT NULL
GROUP BY UzytkownikId, UzytkownikNazwa, TypAktywnosci
ORDER BY Liczba DESC;

SELECT '5.2 Paulina — aktywnosci CRM per miesiac' AS Raport;
SELECT
    CONVERT(char(7), DataUtworzenia, 120)                    AS RokMiesiac,
    TypAktywnosci,
    COUNT(*)                                                 AS Liczba,
    SUM(CASE WHEN WynikTelefonu = 'rozmowa'    THEN 1 ELSE 0 END) AS UdaneRozmowy,
    SUM(CASE WHEN WynikTelefonu = 'brak'       THEN 1 ELSE 0 END) AS BrakOdbioru,
    SUM(CASE WHEN WynikTelefonu = 'rozlaczyl'  THEN 1 ELSE 0 END) AS Rozlaczono
FROM Pozyskiwanie_Aktywnosci
WHERE (UzytkownikId LIKE '%aulina%' OR UzytkownikId = '1122' OR UzytkownikNazwa LIKE '%aulina%')
  AND DataUtworzenia IS NOT NULL
GROUP BY CONVERT(char(7), DataUtworzenia, 120), TypAktywnosci
ORDER BY RokMiesiac, TypAktywnosci;

SELECT '5.3 Paulina — konwersja statusow (StatusPrzed -> StatusPo)' AS Raport;
SELECT
    StatusPrzed,
    StatusPo,
    COUNT(*)                                                 AS Liczba
FROM Pozyskiwanie_Aktywnosci
WHERE (UzytkownikId LIKE '%aulina%' OR UzytkownikId = '1122' OR UzytkownikNazwa LIKE '%aulina%')
  AND StatusPrzed IS NOT NULL
  AND StatusPo IS NOT NULL
  AND StatusPrzed <> StatusPo
GROUP BY StatusPrzed, StatusPo
ORDER BY Liczba DESC;

-- ===========================================================================
-- KROK 6 — Hodowcy, ktorych Paulina sciagnela do produkcji (CRM → realizacja)
-- ===========================================================================
SELECT '6.1 Hodowcy Pauliny, ktorzy POJAWILI SIE w PartiaDostawca (lejek konwersji)' AS Raport;
WITH HodowcyPauliny AS (
    SELECT
        Id,
        Dostawca,
        Status,
        Aktywny
    FROM Pozyskiwanie_Hodowcy
    WHERE PrzypisanyDo LIKE '%aulina%' OR PrzypisanyDo = '1122'
)
SELECT
    HP.Status,
    COUNT(DISTINCT HP.Id)                                    AS HodowcowVCRM,
    COUNT(DISTINCT PD.CustomerName)                          AS HodowcowZRealizacja,
    CAST(100.0 * COUNT(DISTINCT PD.CustomerName) / NULLIF(COUNT(DISTINCT HP.Id), 0) AS decimal(6,2)) AS Konwersja_Proc
FROM HodowcyPauliny HP
LEFT JOIN PartiaDostawca PD
       ON LOWER(LTRIM(RTRIM(PD.CustomerName))) = LOWER(LTRIM(RTRIM(HP.Dostawca)))
GROUP BY HP.Status
ORDER BY HodowcowVCRM DESC;

-- ===========================================================================
-- KROK 7 — Sezonowosc i intensywnosc pracy Pauliny
-- ===========================================================================
SELECT '7.1 Paulina — godziny pracy (kiedy tworzy w aplikacji)' AS Raport;
SELECT
    DATEPART(HOUR, H.DataUtw)                                AS Godzina,
    COUNT(*)                                                 AS LiczbaWpisow
FROM HarmonogramDostaw H
WHERE H.KtoStwo = 1122
  AND H.DataUtw IS NOT NULL
GROUP BY DATEPART(HOUR, H.DataUtw)
ORDER BY Godzina;

SELECT '7.2 Paulina — dni tygodnia' AS Raport;
SELECT
    DATEPART(WEEKDAY, H.DataUtw)                             AS DzienTyg,
    CASE DATEPART(WEEKDAY, H.DataUtw)
        WHEN 1 THEN 'Niedziela' WHEN 2 THEN 'Pn' WHEN 3 THEN 'Wt'
        WHEN 4 THEN 'Sr' WHEN 5 THEN 'Cz' WHEN 6 THEN 'Pt' WHEN 7 THEN 'Sob'
    END                                                      AS Dzien,
    COUNT(*)                                                 AS LiczbaWpisow
FROM HarmonogramDostaw H
WHERE H.KtoStwo = 1122
  AND H.DataUtw IS NOT NULL
GROUP BY DATEPART(WEEKDAY, H.DataUtw)
ORDER BY DzienTyg;

-- ===========================================================================
-- KROK 8 — Paulina jako "zatwierdzajacy" wage/sztuki
-- ===========================================================================
SELECT '8.1 Paulina — czy potwierdza wage i sztuki dostaw' AS Raport;
SELECT
    COUNT(*)                                                                                   AS PotwierdzonychWagi,
    AVG(DATEDIFF(DAY, H.DataUtw, H.KiedyWaga))                                                 AS SredniaDniDoPotw,
    MIN(H.KiedyWaga)                                                                           AS Najwczesniej,
    MAX(H.KiedyWaga)                                                                           AS Najpozniej
FROM HarmonogramDostaw H
WHERE H.KtoWaga = 1122
  AND H.KiedyWaga IS NOT NULL;

SELECT '8.2 Paulina — czy potwierdza sztuki dostaw' AS Raport;
SELECT
    COUNT(*)                                                                                   AS PotwierdzonychSzt,
    AVG(DATEDIFF(DAY, H.DataUtw, H.KiedySztuki))                                               AS SredniaDniDoPotw
FROM HarmonogramDostaw H
WHERE H.KtoSztuki = 1122
  AND H.KiedySztuki IS NOT NULL;

-- ===========================================================================
-- KROK 9 — TOP hodowcy Pauliny po wolumenie (kg/sztuk dostarczonych)
-- ===========================================================================
SELECT '9.1 TOP 30 hodowcow w harmonogramie Pauliny (po sumie sztukDek)' AS Raport;
SELECT TOP 30
    H.Dostawca,
    COUNT(*)                                                 AS LiczbaDostaw,
    SUM(CAST(H.SztukiDek AS int))                            AS SumaSzt,
    AVG(CAST(H.Auta AS int))                                 AS SredniaAut,
    AVG(CAST(H.WagaDek AS decimal(10,2)))                    AS SredniaWagaDek,
    MIN(H.DataOdbioru)                                       AS PierwszaDostawa,
    MAX(H.DataOdbioru)                                       AS OstatniaDostawa,
    -- STRING_AGG(DISTINCT) nieobsługiwane — używam STUFF z subquery
    STUFF((
        SELECT DISTINCT ', ' + H2.TypUmowy
        FROM HarmonogramDostaw H2
        WHERE H2.Dostawca = H.Dostawca AND H2.KtoStwo = 1122 AND H2.TypUmowy IS NOT NULL
        FOR XML PATH(''), TYPE
    ).value('.', 'nvarchar(max)'), 1, 2, '')                  AS TypyUmow
FROM HarmonogramDostaw H
WHERE H.KtoStwo = 1122
GROUP BY H.Dostawca
ORDER BY SumaSzt DESC;

-- ===========================================================================
-- KROK 10 — Paulina vs benchmark (wszyscy userzy zakupu zywca)
-- ===========================================================================
SELECT '10.1 SCORECARD: Paulina vs wszyscy aktywni twoercy harmonogramu' AS Raport;
WITH PerUser AS (
    SELECT
        H.KtoStwo                                            AS UserId,
        O.Name                                               AS UserName,
        COUNT(*)                                             AS LiczbaWpisow,
        COUNT(DISTINCT H.DostawcaID)                         AS RoznychDostawcow,
        SUM(CAST(H.Auta AS int))                             AS SumaAut,
        SUM(CAST(H.SztukiDek AS int))                        AS SumaSztDek,
        AVG(CAST(H.WagaDek AS decimal(10,2)))                AS SredniaWagaDek,
        AVG(CAST(H.Cena    AS decimal(10,2)))                AS SredniaCena,
        AVG(CAST(H.KmK     AS int))                          AS SredniaKmK,
        AVG(CAST(H.KmH     AS int))                          AS SredniaKmH
    FROM HarmonogramDostaw H
    LEFT JOIN operators O ON CAST(O.ID AS int) = H.KtoStwo
    WHERE H.KtoStwo IS NOT NULL
      AND H.DataUtw >= '2024-10-01'
    GROUP BY H.KtoStwo, O.Name
    HAVING COUNT(*) >= 50
)
SELECT * FROM PerUser ORDER BY LiczbaWpisow DESC;

-- ===========================================================================
-- KROK 11 — Conflict Teresa/Paulina (jesli istnieja wspolnie obslugiwani dostawcy)
-- ===========================================================================
SELECT '11.1 Dostawcy obslugiwani i przez Pauline (1122) i przez Terese (2121)' AS Raport;
WITH PaulinaDost AS (
    SELECT DISTINCT DostawcaID, Dostawca FROM HarmonogramDostaw WHERE KtoStwo = 1122 AND DataUtw >= '2024-10-01'
),
TeresaDost AS (
    SELECT DISTINCT DostawcaID, Dostawca FROM HarmonogramDostaw WHERE KtoStwo = 2121 AND DataUtw >= '2024-10-01'
)
SELECT
    P.DostawcaID,
    P.Dostawca,
    'OBOJE' AS Status
FROM PaulinaDost P
INNER JOIN TeresaDost T ON T.Dostawca = P.Dostawca;

SELECT '11.2 Czyje dostawy modyfikuje druga osoba (KtoStwo vs KtoMod conflict)' AS Raport;
SELECT TOP 30
    H.Dostawca,
    OS.Name                                                  AS Stworzyl,
    OM.Name                                                  AS Zmodyfikowal,
    H.DataUtw,
    H.DataMod,
    H.DataOdbioru,
    H.TypUmowy,
    H.Cena
FROM HarmonogramDostaw H
LEFT JOIN operators OS ON CAST(OS.ID AS int) = H.KtoStwo
LEFT JOIN operators OM ON CAST(OM.ID AS int) = H.KtoMod
WHERE H.KtoStwo IS NOT NULL
  AND H.KtoMod IS NOT NULL
  AND H.KtoStwo <> H.KtoMod
  AND (H.KtoStwo IN (1122, 2121) OR H.KtoMod IN (1122, 2121))
  AND H.DataUtw >= '2024-10-01'
ORDER BY H.DataMod DESC;

-- ===========================================================================
-- KROK 12 — Tempo pracy: ile sztuk Paulina "zaplanowala" miesiecznie
-- ===========================================================================
SELECT '12.1 Tempo planowania Pauliny per miesiac' AS Raport;
WITH Miesiace AS (
    SELECT
        CONVERT(char(7), H.DataUtw, 120)                     AS RokMiesiac,
        SUM(CAST(H.SztukiDek AS bigint))                     AS SumaSzt,
        COUNT(*)                                             AS LiczbaWpisow
    FROM HarmonogramDostaw H
    WHERE H.KtoStwo = 1122
      AND H.DataUtw IS NOT NULL
    GROUP BY CONVERT(char(7), H.DataUtw, 120)
)
SELECT
    RokMiesiac,
    LiczbaWpisow,
    SumaSzt,
    LAG(SumaSzt) OVER (ORDER BY RokMiesiac)                  AS PoprzedniSzt,
    CASE
        WHEN LAG(SumaSzt) OVER (ORDER BY RokMiesiac) > 0
        THEN CAST(100.0 * (SumaSzt - LAG(SumaSzt) OVER (ORDER BY RokMiesiac))
                  / LAG(SumaSzt) OVER (ORDER BY RokMiesiac) AS decimal(10,2))
    END                                                      AS ZmianaProc
FROM Miesiace
ORDER BY RokMiesiac;

-- ===========================================================================
-- KROK 13 — Czy listapartii i PartiaDostawca pokazuja "operatorow" Pauliny
-- ===========================================================================
SELECT '13.1 listapartii — kto otwiera partie produkcyjne' AS Raport;
SELECT TOP 15
    L.CreateOperator                                         AS Operator,
    O.Name                                                   AS OperatorName,
    COUNT(*)                                                 AS LiczbaPartii,
    MIN(L.CreateData)                                        AS Najwczesniej,
    MAX(L.CreateData)                                        AS Najpozniej
FROM listapartii L
LEFT JOIN operators O ON O.ID = L.CreateOperator
WHERE L.CreateOperator IS NOT NULL
GROUP BY L.CreateOperator, O.Name
ORDER BY LiczbaPartii DESC;

SELECT '13.2 Hodowcy w PartiaDostawca przypisani Paulina (CustomerName matchuje z Pozyskiwanie_Hodowcy)' AS Raport;
WITH HodowcyPauliny AS (
    SELECT DISTINCT LOWER(LTRIM(RTRIM(Dostawca))) AS DostawcaNorm
    FROM Pozyskiwanie_Hodowcy
    WHERE PrzypisanyDo LIKE '%aulina%' OR PrzypisanyDo = '1122'
)
SELECT
    PD.CustomerID,
    PD.CustomerName,
    COUNT(*)                                                 AS LiczbaPartii,
    MIN(PD.CreateData)                                       AS Najwczesniej,
    MAX(PD.CreateData)                                       AS Najpozniej
FROM PartiaDostawca PD
INNER JOIN HodowcyPauliny HP ON HP.DostawcaNorm = LOWER(LTRIM(RTRIM(PD.CustomerName)))
GROUP BY PD.CustomerID, PD.CustomerName
ORDER BY LiczbaPartii DESC;

-- ===========================================================================
-- KROK 14 — Sumaryczne PODSUMOWANIE: czy Paulina jest "zastapywalna"
-- ===========================================================================
SELECT '14.1 SUMARYCZNE PODSUMOWANIE PAULINY' AS Raport;
WITH PaulinaStats AS (
    SELECT
        'Harmonogram_Wpisow'                                                 AS Metryka,
        CAST(COUNT(*) AS varchar(20))                                        AS Wartosc
    FROM HarmonogramDostaw WHERE KtoStwo = 1122 AND DataUtw >= '2024-10-01'

    UNION ALL
    SELECT 'Harmonogram_Dostawcow',
           CAST(COUNT(DISTINCT DostawcaID) AS varchar(20))
    FROM HarmonogramDostaw WHERE KtoStwo = 1122 AND DataUtw >= '2024-10-01'

    UNION ALL
    SELECT 'Harmonogram_SumaSzt',
           CAST(SUM(CAST(SztukiDek AS bigint)) AS varchar(20))
    FROM HarmonogramDostaw WHERE KtoStwo = 1122 AND DataUtw >= '2024-10-01'

    UNION ALL
    SELECT 'CRM_PrzypisanychHodowcow',
           CAST(COUNT(*) AS varchar(20))
    FROM Pozyskiwanie_Hodowcy WHERE PrzypisanyDo LIKE '%aulina%' OR PrzypisanyDo = '1122'

    UNION ALL
    SELECT 'CRM_Aktywnosci',
           CAST(COUNT(*) AS varchar(20))
    FROM Pozyskiwanie_Aktywnosci WHERE UzytkownikId LIKE '%aulina%' OR UzytkownikId = '1122' OR UzytkownikNazwa LIKE '%aulina%'

    UNION ALL
    SELECT 'Harmonogram_PotwierdzonychWag',
           CAST(COUNT(*) AS varchar(20))
    FROM HarmonogramDostaw WHERE KtoWaga = 1122 AND KiedyWaga IS NOT NULL

    UNION ALL
    SELECT 'Harmonogram_PotwierdzonychSzt',
           CAST(COUNT(*) AS varchar(20))
    FROM HarmonogramDostaw WHERE KtoSztuki = 1122 AND KiedySztuki IS NOT NULL
)
SELECT * FROM PaulinaStats;

SELECT '=== KONIEC analizy PAULINY v2 ===' AS Status;
