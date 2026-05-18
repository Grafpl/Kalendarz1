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
