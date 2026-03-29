-- =====================================================================
-- DIAGNOSTYKA OPAKOWAN — selecty do sprawdzenia wydajnosci i poprawnosci
-- Uruchom w SSMS na serwerze 192.168.0.112 (baza Handel)
-- =====================================================================

-- =====================================================================
-- 1. STRUKTURA TABEL — sprawdz czy istnieja i ile maja rekordow
-- =====================================================================

SELECT 'MG (dokumenty)' AS Tabela, COUNT(*) AS Rekordow FROM [HANDEL].[HM].[MG] WITH (NOLOCK);
SELECT 'MZ (pozycje)' AS Tabela, COUNT(*) AS Rekordow FROM [HANDEL].[HM].[MZ] WITH (NOLOCK);
SELECT 'TW (towary)' AS Tabela, COUNT(*) AS Rekordow FROM [HANDEL].[HM].[TW] WITH (NOLOCK);
SELECT 'STContractors' AS Tabela, COUNT(*) AS Rekordow FROM [HANDEL].[SSCommon].[STContractors] WITH (NOLOCK);
SELECT 'ContractorClass' AS Tabela, COUNT(*) AS Rekordow FROM [HANDEL].[SSCommon].[ContractorClassification] WITH (NOLOCK);

-- =====================================================================
-- 2. TOWARY OPAKOWAN — sprawdz nazwy i ID (czy pasuja do filtra)
-- =====================================================================

SELECT id, nazwa
FROM [HANDEL].[HM].[TW] WITH (NOLOCK)
WHERE nazwa IN ('Pojemnik Drobiowy E2', 'Paleta H1', 'Paleta EURO', 'Paleta plastikowa', 'Paleta Drewniana')
ORDER BY nazwa;

-- Czy sa inne opakowania ktore pomijamy?
SELECT DISTINCT TW.nazwa, COUNT(*) AS IleDokumentow
FROM [HANDEL].[HM].[MZ] MZ WITH (NOLOCK)
INNER JOIN [HANDEL].[HM].[TW] TW WITH (NOLOCK) ON MZ.idtw = TW.id
INNER JOIN [HANDEL].[HM].[MG] MG WITH (NOLOCK) ON MZ.super = MG.id
WHERE MG.magazyn = 65559 AND MG.anulowany = 0
  AND TW.nazwa LIKE '%pojemnik%' OR TW.nazwa LIKE '%paleta%' OR TW.nazwa LIKE '%opakow%'
GROUP BY TW.nazwa
ORDER BY COUNT(*) DESC;

-- =====================================================================
-- 3. DOKUMENTY — wolumen i typy
-- =====================================================================

-- Ile dokumentow opakowan jest per rok?
SELECT YEAR(MG.data) AS Rok, MG.typ_dk AS Typ, COUNT(*) AS IleDokumentow
FROM [HANDEL].[HM].[MG] MG WITH (NOLOCK)
WHERE MG.magazyn = 65559 AND MG.anulowany = 0 AND MG.typ_dk IN ('MW1', 'MP')
GROUP BY YEAR(MG.data), MG.typ_dk
ORDER BY Rok DESC, Typ;

-- Ile pozycji opakowan per typ dokumentu?
SELECT MG.typ_dk, TW.nazwa, COUNT(*) AS IlePozycji, SUM(MZ.Ilosc) AS SumaIlosci
FROM [HANDEL].[HM].[MG] MG WITH (NOLOCK)
INNER JOIN [HANDEL].[HM].[MZ] MZ WITH (NOLOCK) ON MZ.super = MG.id
INNER JOIN [HANDEL].[HM].[TW] TW WITH (NOLOCK) ON MZ.idtw = TW.id
WHERE MG.magazyn = 65559 AND MG.anulowany = 0 AND MG.typ_dk IN ('MW1', 'MP')
  AND TW.nazwa IN ('Pojemnik Drobiowy E2', 'Paleta H1', 'Paleta EURO', 'Paleta plastikowa', 'Paleta Drewniana')
  AND MG.data >= '2024-01-01'
GROUP BY MG.typ_dk, TW.nazwa
ORDER BY MG.typ_dk, TW.nazwa;

-- =====================================================================
-- 4. MAGAZYN 65559 — potwierdz ze to jedyny magazyn opakowan
-- =====================================================================

SELECT DISTINCT MG.magazyn, COUNT(*) AS IleDokumentow
FROM [HANDEL].[HM].[MG] MG WITH (NOLOCK)
INNER JOIN [HANDEL].[HM].[MZ] MZ WITH (NOLOCK) ON MZ.super = MG.id
INNER JOIN [HANDEL].[HM].[TW] TW WITH (NOLOCK) ON MZ.idtw = TW.id
WHERE MG.anulowany = 0 AND MG.typ_dk IN ('MW1', 'MP')
  AND TW.nazwa IN ('Pojemnik Drobiowy E2', 'Paleta H1', 'Paleta EURO', 'Paleta plastikowa', 'Paleta Drewniana')
GROUP BY MG.magazyn
ORDER BY IleDokumentow DESC;

-- =====================================================================
-- 5. ANULOWANE — ile pomijamy?
-- =====================================================================

SELECT MG.anulowany, COUNT(*) AS IleDokumentow
FROM [HANDEL].[HM].[MG] MG WITH (NOLOCK)
INNER JOIN [HANDEL].[HM].[MZ] MZ WITH (NOLOCK) ON MZ.super = MG.id
INNER JOIN [HANDEL].[HM].[TW] TW WITH (NOLOCK) ON MZ.idtw = TW.id
WHERE MG.magazyn = 65559 AND MG.typ_dk IN ('MW1', 'MP')
  AND TW.nazwa IN ('Pojemnik Drobiowy E2', 'Paleta H1', 'Paleta EURO', 'Paleta plastikowa', 'Paleta Drewniana')
GROUP BY MG.anulowany;

-- =====================================================================
-- 6. SALDA GLOBALNE — sumy per typ opakowania (stan na dzis)
-- =====================================================================

SELECT
    TW.nazwa AS Opakowanie,
    SUM(MZ.Ilosc) AS SaldoGlobalne,
    COUNT(DISTINCT MG.khid) AS IluKontrahentow,
    SUM(CASE WHEN MZ.Ilosc > 0 THEN MZ.Ilosc ELSE 0 END) AS SumaWydan,
    SUM(CASE WHEN MZ.Ilosc < 0 THEN MZ.Ilosc ELSE 0 END) AS SumaPrzyjec,
    MIN(MG.data) AS NajstarszyDok,
    MAX(MG.data) AS NajnowszyDok
FROM [HANDEL].[HM].[MG] MG WITH (NOLOCK)
INNER JOIN [HANDEL].[HM].[MZ] MZ WITH (NOLOCK) ON MZ.super = MG.id
INNER JOIN [HANDEL].[HM].[TW] TW WITH (NOLOCK) ON MZ.idtw = TW.id
WHERE MG.anulowany = 0 AND MG.magazyn = 65559 AND MG.typ_dk IN ('MW1', 'MP')
  AND TW.nazwa IN ('Pojemnik Drobiowy E2', 'Paleta H1', 'Paleta EURO', 'Paleta plastikowa', 'Paleta Drewniana')
GROUP BY TW.nazwa
ORDER BY TW.nazwa;

-- =====================================================================
-- 7. TOP 20 KONTRAHENTOW — kto ma najwieksze salda?
-- =====================================================================

SELECT TOP 20
    C.Shortcut AS Kontrahent,
    SUM(CASE WHEN TW.nazwa = 'Pojemnik Drobiowy E2' THEN MZ.Ilosc ELSE 0 END) AS E2,
    SUM(CASE WHEN TW.nazwa = 'Paleta H1' THEN MZ.Ilosc ELSE 0 END) AS H1,
    SUM(CASE WHEN TW.nazwa = 'Paleta EURO' THEN MZ.Ilosc ELSE 0 END) AS EURO,
    SUM(CASE WHEN TW.nazwa = 'Paleta plastikowa' THEN MZ.Ilosc ELSE 0 END) AS PCV,
    SUM(CASE WHEN TW.nazwa = 'Paleta Drewniana' THEN MZ.Ilosc ELSE 0 END) AS DREW,
    SUM(ABS(MZ.Ilosc)) AS RAZEM_ABS
FROM [HANDEL].[HM].[MG] MG WITH (NOLOCK)
INNER JOIN [HANDEL].[HM].[MZ] MZ WITH (NOLOCK) ON MZ.super = MG.id
INNER JOIN [HANDEL].[HM].[TW] TW WITH (NOLOCK) ON MZ.idtw = TW.id
INNER JOIN [HANDEL].[SSCommon].[STContractors] C WITH (NOLOCK) ON C.id = MG.khid
WHERE MG.anulowany = 0 AND MG.magazyn = 65559 AND MG.typ_dk IN ('MW1', 'MP')
  AND TW.nazwa IN ('Pojemnik Drobiowy E2', 'Paleta H1', 'Paleta EURO', 'Paleta plastikowa', 'Paleta Drewniana')
GROUP BY C.Shortcut
ORDER BY RAZEM_ABS DESC;

-- =====================================================================
-- 8. HANDLOWCY — ilu kontrahentow per handlowiec?
-- =====================================================================

SELECT
    ISNULL(WYM.CDim_Handlowiec_Val, '(brak)') AS Handlowiec,
    COUNT(DISTINCT MG.khid) AS IluKontrahentow,
    COUNT(*) AS IleDokumentow,
    SUM(MZ.Ilosc) AS SaldoGlobalne
FROM [HANDEL].[HM].[MG] MG WITH (NOLOCK)
INNER JOIN [HANDEL].[HM].[MZ] MZ WITH (NOLOCK) ON MZ.super = MG.id
INNER JOIN [HANDEL].[HM].[TW] TW WITH (NOLOCK) ON MZ.idtw = TW.id
LEFT JOIN [HANDEL].[SSCommon].[ContractorClassification] WYM WITH (NOLOCK) ON MG.khid = WYM.ElementId
WHERE MG.anulowany = 0 AND MG.magazyn = 65559 AND MG.typ_dk IN ('MW1', 'MP')
  AND TW.nazwa IN ('Pojemnik Drobiowy E2', 'Paleta H1', 'Paleta EURO', 'Paleta plastikowa', 'Paleta Drewniana')
GROUP BY WYM.CDim_Handlowiec_Val
ORDER BY IluKontrahentow DESC;

-- =====================================================================
-- 9. KONTRAHENCI BEZ HANDLOWCA — czy tracimy dane?
-- =====================================================================

SELECT COUNT(*) AS KontrahenciBezHandlowca
FROM (
    SELECT DISTINCT MG.khid
    FROM [HANDEL].[HM].[MG] MG WITH (NOLOCK)
    INNER JOIN [HANDEL].[HM].[MZ] MZ WITH (NOLOCK) ON MZ.super = MG.id
    INNER JOIN [HANDEL].[HM].[TW] TW WITH (NOLOCK) ON MZ.idtw = TW.id
    WHERE MG.anulowany = 0 AND MG.magazyn = 65559 AND MG.typ_dk IN ('MW1', 'MP')
      AND TW.nazwa IN ('Pojemnik Drobiowy E2', 'Paleta H1', 'Paleta EURO', 'Paleta plastikowa', 'Paleta Drewniana')
    EXCEPT
    SELECT WYM.ElementId
    FROM [HANDEL].[SSCommon].[ContractorClassification] WYM WITH (NOLOCK)
    WHERE WYM.CDim_Handlowiec_Val IS NOT NULL
) AS BezHandlowca;

-- =====================================================================
-- 10. HAVING ABS(SUM) > 0 — ile kontrahentow pomijamy przez ten filtr?
-- =====================================================================

-- Kontrahenci z saldem = 0 (pomijani przez nasze zapytanie)
SELECT COUNT(*) AS KontrahenciZSaldemZero
FROM (
    SELECT MG.khid, SUM(MZ.Ilosc) AS Saldo
    FROM [HANDEL].[HM].[MG] MG WITH (NOLOCK)
    INNER JOIN [HANDEL].[HM].[MZ] MZ WITH (NOLOCK) ON MZ.super = MG.id
    INNER JOIN [HANDEL].[HM].[TW] TW WITH (NOLOCK) ON MZ.idtw = TW.id
    WHERE MG.anulowany = 0 AND MG.magazyn = 65559 AND MG.typ_dk IN ('MW1', 'MP')
      AND TW.nazwa IN ('Pojemnik Drobiowy E2', 'Paleta H1', 'Paleta EURO', 'Paleta plastikowa', 'Paleta Drewniana')
    GROUP BY MG.khid
    HAVING SUM(MZ.Ilosc) = 0
) AS ZeroSaldo;

-- vs kontrahenci z saldem <> 0 (pokazywani w naszym widoku)
SELECT COUNT(*) AS KontrahenciZSaldem
FROM (
    SELECT MG.khid, SUM(MZ.Ilosc) AS Saldo
    FROM [HANDEL].[HM].[MG] MG WITH (NOLOCK)
    INNER JOIN [HANDEL].[HM].[MZ] MZ WITH (NOLOCK) ON MZ.super = MG.id
    INNER JOIN [HANDEL].[HM].[TW] TW WITH (NOLOCK) ON MZ.idtw = TW.id
    WHERE MG.anulowany = 0 AND MG.magazyn = 65559 AND MG.typ_dk IN ('MW1', 'MP')
      AND TW.nazwa IN ('Pojemnik Drobiowy E2', 'Paleta H1', 'Paleta EURO', 'Paleta plastikowa', 'Paleta Drewniana')
    GROUP BY MG.khid
    HAVING ABS(SUM(MZ.Ilosc)) > 0
) AS NieZero;

-- =====================================================================
-- 11. POROWNANIE SALD — SaldaService vs OpakowaniaDataService
-- Sprawdz czy oba daja te same wyniki
-- =====================================================================

-- SaldaService: salda per kontrahent (wszystkie typy)
;WITH SaldaV1 AS (
    SELECT MG.khid AS KontrahentId,
        SUM(CASE WHEN TW.nazwa = 'Pojemnik Drobiowy E2' THEN MZ.Ilosc ELSE 0 END) AS E2,
        SUM(CASE WHEN TW.nazwa = 'Paleta H1' THEN MZ.Ilosc ELSE 0 END) AS H1,
        SUM(CASE WHEN TW.nazwa = 'Paleta EURO' THEN MZ.Ilosc ELSE 0 END) AS EURO,
        SUM(CASE WHEN TW.nazwa = 'Paleta plastikowa' THEN MZ.Ilosc ELSE 0 END) AS PCV,
        SUM(CASE WHEN TW.nazwa = 'Paleta Drewniana' THEN MZ.Ilosc ELSE 0 END) AS DREW
    FROM [HANDEL].[HM].[MG] MG WITH (NOLOCK)
    INNER JOIN [HANDEL].[HM].[MZ] MZ WITH (NOLOCK) ON MZ.super = MG.id
    INNER JOIN [HANDEL].[HM].[TW] TW WITH (NOLOCK) ON MZ.idtw = TW.id
    WHERE MG.anulowany = 0 AND MG.magazyn = 65559 AND MG.typ_dk IN ('MW1','MP')
      AND MG.data <= GETDATE()
      AND TW.nazwa IN ('Pojemnik Drobiowy E2','Paleta H1','Paleta EURO','Paleta plastikowa','Paleta Drewniana')
    GROUP BY MG.khid
    HAVING ABS(SUM(MZ.Ilosc)) > 0
)
SELECT 'SaldaService' AS Zrodlo, COUNT(*) AS Kontrahentow, SUM(E2) AS SumaE2, SUM(H1) AS SumaH1, SUM(EURO) AS SumaEURO, SUM(PCV) AS SumaPCV, SUM(DREW) AS SumaDREW
FROM SaldaV1;

-- =====================================================================
-- 12. WYDAJNOSC — ile czasu trwa kazde zapytanie?
-- =====================================================================

SET STATISTICS TIME ON;
SET STATISTICS IO ON;

-- Test SaldaService (Wszystkie typy)
;WITH DokumentyOpakowan AS (
    SELECT MG.khid AS KontrahentId, TW.nazwa AS TowarNazwa, MZ.Ilosc, MG.Data
    FROM [HANDEL].[HM].[MG] MG WITH (NOLOCK)
    INNER JOIN [HANDEL].[HM].[MZ] MZ WITH (NOLOCK) ON MZ.super = MG.id
    INNER JOIN [HANDEL].[HM].[TW] TW WITH (NOLOCK) ON MZ.idtw = TW.id
    WHERE MG.anulowany = 0 AND MG.magazyn = 65559 AND MG.typ_dk IN ('MW1','MP')
      AND MG.data <= GETDATE()
      AND TW.nazwa IN ('Pojemnik Drobiowy E2','Paleta H1','Paleta EURO','Paleta plastikowa','Paleta Drewniana')
),
SaldaKontrahentow AS (
    SELECT KontrahentId,
        CAST(ISNULL(SUM(CASE WHEN TowarNazwa = 'Pojemnik Drobiowy E2' THEN Ilosc ELSE 0 END),0) AS INT) AS E2,
        CAST(ISNULL(SUM(CASE WHEN TowarNazwa = 'Paleta H1' THEN Ilosc ELSE 0 END),0) AS INT) AS H1,
        MAX(Data) AS OstatniDokument
    FROM DokumentyOpakowan
    GROUP BY KontrahentId
    HAVING ABS(SUM(Ilosc)) > 0
)
SELECT COUNT(*) AS Rekordow FROM SaldaKontrahentow;

-- Test PerTyp E2 (najwolniejszy)
;WITH Dane AS (
    SELECT MG.khid AS KontrahentId, MZ.Ilosc, MG.Data
    FROM [HANDEL].[HM].[MG] MG WITH (NOLOCK)
    INNER JOIN [HANDEL].[HM].[MZ] MZ WITH (NOLOCK) ON MZ.super = MG.id
    INNER JOIN [HANDEL].[HM].[TW] TW WITH (NOLOCK) ON MZ.idtw = TW.id
    WHERE MG.anulowany = 0 AND MG.magazyn = 65559 AND MG.typ_dk IN ('MW1','MP')
      AND MG.data <= GETDATE()
      AND TW.nazwa = 'Pojemnik Drobiowy E2'
),
Salda AS (
    SELECT KontrahentId,
        CAST(ISNULL(SUM(CASE WHEN Data <= DATEADD(WEEK,-3,GETDATE()) THEN Ilosc ELSE 0 END),0) AS INT) AS SaldoDoOd,
        CAST(ISNULL(SUM(Ilosc),0) AS INT) AS SaldoDoDo,
        MAX(Data) AS OstatniDokument
    FROM Dane
    GROUP BY KontrahentId
    HAVING ABS(SUM(Ilosc)) > 0 OR ABS(SUM(CASE WHEN Data <= DATEADD(WEEK,-3,GETDATE()) THEN Ilosc ELSE 0 END)) > 0
)
SELECT COUNT(*) AS Rekordow FROM Salda;

SET STATISTICS TIME OFF;
SET STATISTICS IO OFF;

-- =====================================================================
-- 13. INDEKSY — sprawdz jakie istnieja na glownych tabelach
-- =====================================================================

SELECT i.name AS IndexName, i.type_desc,
    STRING_AGG(c.name, ', ') WITHIN GROUP (ORDER BY ic.key_ordinal) AS Kolumny
FROM sys.indexes i
INNER JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
INNER JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
WHERE i.object_id = OBJECT_ID('[HANDEL].[HM].[MG]')
GROUP BY i.name, i.type_desc
ORDER BY i.type_desc, i.name;

SELECT i.name AS IndexName, i.type_desc,
    STRING_AGG(c.name, ', ') WITHIN GROUP (ORDER BY ic.key_ordinal) AS Kolumny
FROM sys.indexes i
INNER JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
INNER JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
WHERE i.object_id = OBJECT_ID('[HANDEL].[HM].[MZ]')
GROUP BY i.name, i.type_desc
ORDER BY i.type_desc, i.name;

-- =====================================================================
-- 14. OUTER APPLY — czy TypOstatniegoDok dziala poprawnie?
-- =====================================================================

-- Sprawdz TOP 10 kontrahentow — czy OUTER APPLY zwraca poprawny typ
;WITH SK AS (
    SELECT MG.khid AS KontrahentId, MAX(MG.data) AS OstatniDokument
    FROM [HANDEL].[HM].[MG] MG WITH (NOLOCK)
    INNER JOIN [HANDEL].[HM].[MZ] MZ WITH (NOLOCK) ON MZ.super = MG.id
    INNER JOIN [HANDEL].[HM].[TW] TW WITH (NOLOCK) ON MZ.idtw = TW.id
    WHERE MG.anulowany = 0 AND MG.magazyn = 65559 AND MG.typ_dk IN ('MW1','MP')
      AND TW.nazwa IN ('Pojemnik Drobiowy E2','Paleta H1','Paleta EURO','Paleta plastikowa','Paleta Drewniana')
    GROUP BY MG.khid
)
SELECT TOP 10
    C.Shortcut,
    SK.OstatniDokument,
    OD.TypDok,
    OD.NrDokumentu,
    OD.SeriaOrg
FROM SK
INNER JOIN [HANDEL].[SSCommon].[STContractors] C WITH (NOLOCK) ON C.id = SK.KontrahentId
OUTER APPLY (
    SELECT TOP 1
        CASE WHEN MG2.typ_dk = 'MW1' THEN 'Wydanie' ELSE 'Przyjecie' END AS TypDok,
        MG2.kod AS NrDokumentu,
        MG2.seria AS SeriaOrg
    FROM [HANDEL].[HM].[MG] MG2 WITH (NOLOCK)
    WHERE MG2.khid = SK.KontrahentId AND MG2.anulowany = 0 AND MG2.magazyn = 65559
      AND MG2.typ_dk IN ('MW1','MP') AND MG2.Data = SK.OstatniDokument
    ORDER BY MG2.id DESC
) OD
ORDER BY C.Shortcut;

-- =====================================================================
-- 15. ZALEGLOSCI — kontrahenci bez dokumentu od 30/60/90 dni
-- =====================================================================

;WITH OstatnieDokumenty AS (
    SELECT MG.khid AS KontrahentId, MAX(MG.data) AS OstatniDok,
        SUM(CASE WHEN TW.nazwa = 'Pojemnik Drobiowy E2' THEN MZ.Ilosc ELSE 0 END) AS SaldoE2
    FROM [HANDEL].[HM].[MG] MG WITH (NOLOCK)
    INNER JOIN [HANDEL].[HM].[MZ] MZ WITH (NOLOCK) ON MZ.super = MG.id
    INNER JOIN [HANDEL].[HM].[TW] TW WITH (NOLOCK) ON MZ.idtw = TW.id
    WHERE MG.anulowany = 0 AND MG.magazyn = 65559 AND MG.typ_dk IN ('MW1','MP')
      AND TW.nazwa IN ('Pojemnik Drobiowy E2','Paleta H1','Paleta EURO','Paleta plastikowa','Paleta Drewniana')
    GROUP BY MG.khid
    HAVING ABS(SUM(MZ.Ilosc)) > 0
)
SELECT
    CASE
        WHEN DATEDIFF(DAY, OstatniDok, GETDATE()) > 90 THEN '90+ dni'
        WHEN DATEDIFF(DAY, OstatniDok, GETDATE()) > 60 THEN '60-90 dni'
        WHEN DATEDIFF(DAY, OstatniDok, GETDATE()) > 30 THEN '30-60 dni'
        ELSE '< 30 dni'
    END AS Zaleglosc,
    COUNT(*) AS IluKontrahentow
FROM OstatnieDokumenty
GROUP BY
    CASE
        WHEN DATEDIFF(DAY, OstatniDok, GETDATE()) > 90 THEN '90+ dni'
        WHEN DATEDIFF(DAY, OstatniDok, GETDATE()) > 60 THEN '60-90 dni'
        WHEN DATEDIFF(DAY, OstatniDok, GETDATE()) > 30 THEN '30-60 dni'
        ELSE '< 30 dni'
    END
ORDER BY Zaleglosc;

-- =====================================================================
-- 16. POTWIERDZENIA — na serwerze 192.168.0.109 (LibraNet)
-- Uruchom na 192.168.0.109!
-- =====================================================================

-- SELECT COUNT(*) AS LiczbaPotwierdzeh FROM [LibraNet].[dbo].[PotwierdzeniaSaldaOpakowan] WITH (NOLOCK);
--
-- SELECT StatusPotwierdzenia, COUNT(*) AS Ile
-- FROM [LibraNet].[dbo].[PotwierdzeniaSaldaOpakowan] WITH (NOLOCK)
-- GROUP BY StatusPotwierdzenia;
--
-- SELECT TOP 10 KontrahentId, KodOpakowania, DataPotwierdzenia, IloscPotwierdzona, SaldoSystemowe, StatusPotwierdzenia
-- FROM [LibraNet].[dbo].[PotwierdzeniaSaldaOpakowan] WITH (NOLOCK)
-- ORDER BY DataPotwierdzenia DESC;
--
-- -- Czy tabela MapowanieHandlowcow istnieje i ile ma rekordow?
-- SELECT COUNT(*) FROM [LibraNet].[dbo].[MapowanieHandlowcow] WITH (NOLOCK);
--
-- -- Czy tabela UserHandlowcy istnieje?
-- SELECT COUNT(*) FROM [LibraNet].[dbo].[UserHandlowcy] WITH (NOLOCK);
-- SELECT TOP 10 * FROM [LibraNet].[dbo].[UserHandlowcy] WITH (NOLOCK);
--
-- -- Operators — zrodlo avatrow
-- SELECT TOP 20 ID, Name FROM [LibraNet].[dbo].[operators] WITH (NOLOCK) WHERE Name IS NOT NULL ORDER BY Name;

-- =====================================================================
-- 17. PROPOZYCJA INDEKSOW — co mogloby przyspieszyc zapytania
-- =====================================================================

-- Najwazniejszy indeks: MG po magazynie + typ_dk + anulowany + data (covering)
-- CREATE NONCLUSTERED INDEX IX_MG_Opakowania
-- ON [HANDEL].[HM].[MG] (magazyn, typ_dk, anulowany, data)
-- INCLUDE (khid, kod, seria, opis)
-- WHERE magazyn = 65559 AND anulowany = 0;

-- Indeks MZ po super (FK do MG)
-- Ten prawdopodobnie juz istnieje:
-- CREATE NONCLUSTERED INDEX IX_MZ_Super
-- ON [HANDEL].[HM].[MZ] (super)
-- INCLUDE (idtw, Ilosc, data);

-- =====================================================================
-- 18. POROWNANIE 3 TYGODNIE — czy PerTyp daje poprawne wyniki?
-- =====================================================================

-- Sprawdz saldo E2 na dzis vs 3 tygodnie temu dla TOP 5 kontrahentow
DECLARE @DataOd DATE = DATEADD(WEEK, -3, GETDATE());
DECLARE @DataDo DATE = GETDATE();

;WITH Dane AS (
    SELECT MG.khid AS KontrahentId, MZ.Ilosc, MG.Data
    FROM [HANDEL].[HM].[MG] MG WITH (NOLOCK)
    INNER JOIN [HANDEL].[HM].[MZ] MZ WITH (NOLOCK) ON MZ.super = MG.id
    INNER JOIN [HANDEL].[HM].[TW] TW WITH (NOLOCK) ON MZ.idtw = TW.id
    WHERE MG.anulowany = 0 AND MG.magazyn = 65559 AND MG.typ_dk IN ('MW1','MP')
      AND MG.data <= @DataDo AND TW.nazwa = 'Pojemnik Drobiowy E2'
)
SELECT TOP 10
    C.Shortcut,
    SUM(CASE WHEN D.Data <= @DataOd THEN D.Ilosc ELSE 0 END) AS Saldo3TygTemu,
    SUM(D.Ilosc) AS SaldoDzis,
    SUM(D.Ilosc) - SUM(CASE WHEN D.Data <= @DataOd THEN D.Ilosc ELSE 0 END) AS Zmiana
FROM Dane D
INNER JOIN [HANDEL].[SSCommon].[STContractors] C WITH (NOLOCK) ON C.id = D.KontrahentId
GROUP BY C.Shortcut
HAVING ABS(SUM(D.Ilosc)) > 0
ORDER BY ABS(SUM(D.Ilosc)) DESC;

-- =====================================================================
-- 19. DUPLIKATY — czy ContractorClassification ma duplikaty?
-- =====================================================================

SELECT ElementId, COUNT(*) AS Ile
FROM [HANDEL].[SSCommon].[ContractorClassification] WITH (NOLOCK)
GROUP BY ElementId
HAVING COUNT(*) > 1
ORDER BY Ile DESC;
-- Jesli sa duplikaty, to LEFT JOIN moze zwracac wiecej wierszy niz oczekiwano!

-- =====================================================================
-- 20. INNE TYPY DOKUMENTOW — czy pomijamy cos waznego?
-- =====================================================================

SELECT DISTINCT MG.typ_dk, COUNT(*) AS Ile
FROM [HANDEL].[HM].[MG] MG WITH (NOLOCK)
WHERE MG.magazyn = 65559 AND MG.anulowany = 0
GROUP BY MG.typ_dk
ORDER BY Ile DESC;
-- Jesli sa typy inne niz MW1/MP ktore tez dotycza opakowan — trzeba je dodac!
