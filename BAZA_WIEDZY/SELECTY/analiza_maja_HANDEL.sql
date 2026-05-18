/* ============================================================================
   analiza_maja_HANDEL.sql  (v5 — pełna analiza era Daniela vs era Mai)
   ----------------------------------------------------------------------------
   Uruchom na:  serwer 192.168.0.112, baza Handel (Sage Symfonia)
                user 'sa'

   ZAŁOŻENIE PROJEKTOWE:
   Klienci, którzy DZIŚ mają ContractorClassification.CDim_Handlowiec_Val = 'Maja',
   PRZED 2025-10-01 byli obsługiwani przez Daniela (handlowca który odszedł).
   Sergiusz przepisał ich na Maję w październiku 2025.
   Cała "historyczna sprzedaż" tych klientów retroaktywnie pokazuje się
   pod Mają w polu Handlowiec, ale FAKTYCZNIE to były obroty Daniela.

   Skrypt rozdziela dwa okresy:
     • ERA DANIELA: faktury od 2024-04-01 do 2025-09-30 (klienci Mai = klienci Daniela)
     • ERA MAI:     faktury od 2025-10-01 do dziś

   Dla decyzji "podwyżka Mai" kluczowe pytanie:
     czy klienci Mai ROSNĄ pod nią vs pod Danielem,
     czy spadają (Maja tylko podbiera spadek),
     czy stoją w miejscu.
   ============================================================================ */

USE [Handel];
GO

SET NOCOUNT ON;
SET ANSI_WARNINGS ON;

DECLARE @HandlowiecMaja  NVARCHAR(255) = N'Maja';

DECLARE @DataMaja        DATE = '2025-10-01';                -- początek ery Mai
DECLARE @DataDo          DATE = CAST(GETDATE() AS DATE);     -- dziś
DECLARE @DataOdDaniela   DATE = '2024-04-01';                -- 18 mies. wstecz
DECLARE @DataOdShort     DATE = '2025-07-01';                -- 3 mies. przed Mają

-- ⚠ Po uruchomieniu raportu 0.3 zobaczysz UserID Sage, którzy wystawiali faktury
-- klientom Mai w erze pre-Maja. Wybierz spośród nich Daniela i Dawida i wpisz tu:
DECLARE @UserIdDaniel    INT = NULL;   -- np. 17
DECLARE @UserIdDawid     INT = NULL;   -- np. 23

/* ===========================================================================
   0.01 — Diagnostyka kolumn HM.DK (znaleźć kolumnę "kto wystawił fakturę")
   Po uruchomieniu zobacz kolumny i powiedz która to "wystawił/user/operator"
   =========================================================================== */
SELECT N'0.01 — Kolumny tabeli HM.DK (do znalezienia kolumny user/operator/wystawil)' AS [Raport];
SELECT TABLE_SCHEMA, TABLE_NAME, COLUMN_NAME, DATA_TYPE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = 'HM' AND TABLE_NAME = 'DK'
  AND (COLUMN_NAME LIKE '%user%' OR COLUMN_NAME LIKE '%oper%'
       OR COLUMN_NAME LIKE '%wystaw%' OR COLUMN_NAME LIKE '%create%'
       OR COLUMN_NAME LIKE '%modif%' OR COLUMN_NAME LIKE '%who%'
       OR COLUMN_NAME LIKE '%emp%' OR COLUMN_NAME LIKE '%uzytk%'
       OR COLUMN_NAME LIKE '%kto%' OR DATA_TYPE IN ('int','smallint','tinyint'))
ORDER BY ORDINAL_POSITION;

/* ===========================================================================
   0.0 — Kandydaci na "Maję"
   =========================================================================== */
SELECT N'0.0 — Kandydaci na Maję w ContractorClassification' AS [Raport];

SELECT TOP 30
       WYM.CDim_Handlowiec_Val             AS HandlowiecVal,
       COUNT(DISTINCT WYM.ElementId)       AS LiczbaKontrahentow,
       COUNT(DK.id)                        AS LiczbaFaktur,
       MIN(DK.data)                        AS PierwszaFaktura,
       MAX(DK.data)                        AS OstatniaFaktura
FROM [SSCommon].[ContractorClassification] WYM WITH (NOLOCK)
LEFT JOIN [HM].[DK] DK WITH (NOLOCK)
       ON DK.khid = WYM.ElementId AND DK.anulowany = 0
      AND DK.data >= DATEADD(YEAR, -2, GETDATE())
WHERE WYM.CDim_Handlowiec_Val IS NOT NULL AND WYM.CDim_Handlowiec_Val <> N''
  AND (WYM.CDim_Handlowiec_Val LIKE N'%aj%' OR WYM.CDim_Handlowiec_Val LIKE N'M%')
GROUP BY WYM.CDim_Handlowiec_Val
ORDER BY LiczbaFaktur DESC;

/* ===========================================================================
   0.1 — Budowa #FaktBaza (18 mies. — pokrywa erę Daniela + erę Mai)
   =========================================================================== */
IF OBJECT_ID('tempdb..#FaktBaza') IS NOT NULL DROP TABLE #FaktBaza;

SELECT
    DK.id                                              AS DKId,
    DK.kod                                             AS NumerFaktury,
    DK.khid                                            AS KontrahentId,
    -- TODO po 0.01: podmień placeholder 0 na DK.<właściwa_kolumna_user>
    0                                                  AS WystawilUserId,
    DK.data                                            AS DataFaktury,
    YEAR(DK.data)                                      AS Rok,
    MONTH(DK.data)                                     AS Miesiac,
    DATEPART(WEEKDAY, DK.data)                         AS DzienTyg,   -- 1=Nd, 2=Pn, ..., 7=Sb
    DATEPART(WEEK, DK.data)                            AS TydzienRoku,
    CAST(FORMAT(DK.data, 'yyyy-MM') AS NVARCHAR(10))   AS RokMiesiac,
    DK.walbrutto                                       AS WartoscBrutto,
    DK.plattermin                                      AS TerminPlatnosci,
    DP.idtw                                            AS TowarId,
    TW.kod                                             AS TowarKod,
    TW.nazwa                                           AS TowarNazwa,
    TW.katalog                                         AS TowarKatalog,
    CASE TW.katalog WHEN 67153 THEN N'Mrożone'
                    WHEN 67095 THEN N'Świeże'
                    WHEN 67104 THEN N'Mięso-inne'
                    ELSE N'Inne' END                   AS Kategoria,
    DP.ilosc                                           AS Kg,
    DP.cena                                            AS CenaJednostkowa,
    DP.wartNetto                                       AS WartoscNetto,
    ISNULL(WYM.CDim_Handlowiec_Val, N'Nieprzypisany')  AS Handlowiec,
    -- Era: pre-Maja czy post-Maja
    CASE WHEN DK.data < @DataMaja THEN N'Era Daniela'
         ELSE N'Era Mai' END                           AS Era,
    C.shortcut                                         AS KontrahentSkrot,
    C.shortcut                                         AS KontrahentNazwa,
    C.LimitAmount                                      AS LimitKredytowy
INTO #FaktBaza
FROM [HM].[DK] DK WITH (NOLOCK)
INNER JOIN [HM].[DP] DP WITH (NOLOCK) ON DK.id = DP.super
LEFT JOIN  [HM].[TW] TW WITH (NOLOCK) ON TW.id = DP.idtw
LEFT JOIN  [SSCommon].[ContractorClassification] WYM WITH (NOLOCK) ON DK.khid = WYM.ElementId
LEFT JOIN  [SSCommon].[STContractors] C WITH (NOLOCK) ON DK.khid = C.id
WHERE DK.anulowany = 0
  AND DK.data >= @DataOdDaniela
  AND DK.data <  DATEADD(DAY, 1, @DataDo)
  AND TW.katalog IN (67095, 67104, 67153);

CREATE INDEX IX_FB_Handlowiec ON #FaktBaza(Handlowiec) INCLUDE (DataFaktury, KontrahentId, Kg, WartoscNetto);
CREATE INDEX IX_FB_Towar      ON #FaktBaza(TowarId, Rok, Miesiac) INCLUDE (Handlowiec, Kg, WartoscNetto);
CREATE INDEX IX_FB_Klient_Era ON #FaktBaza(KontrahentId, Era) INCLUDE (Kg, WartoscNetto);

/* ===========================================================================
   0.2 — Klienci Mai i CSV do skryptu LibraNet
   =========================================================================== */
SELECT N'0.2 — Klienci Mai (29 obecnie zaklasyfikowanych pod Maję)' AS [Raport];

SELECT KontrahentId, KontrahentNazwa,
       COUNT(DISTINCT DKId)                          AS LiczbaFaktur,
       CAST(SUM(WartoscNetto) AS DECIMAL(18,2))      AS SumaNetto
FROM #FaktBaza
WHERE DataFaktury BETWEEN @DataMaja AND @DataDo
  AND Handlowiec = @HandlowiecMaja
GROUP BY KontrahentId, KontrahentNazwa
ORDER BY SumaNetto DESC;

SELECT N'0.2b — Klienci Mai CSV (do skryptu LibraNet @KlienciMaiCSV)' AS [Raport];
SELECT STRING_AGG(CAST(KontrahentId AS NVARCHAR(20)), N',')
       WITHIN GROUP (ORDER BY KontrahentId) AS KlienciMaiCSV
FROM (SELECT DISTINCT KontrahentId FROM #FaktBaza
      WHERE DataFaktury BETWEEN @DataMaja AND @DataDo AND Handlowiec = @HandlowiecMaja) x;

-- ┌─────────────────────────────────────────────────────────────────────────┐
-- │ Raporty 0.3 / 0.4 / 0.5 WYŁĄCZONE — wymagają kolumny user_id w HM.DK    │
-- │ Po raporcie 0.01 wskaż prawidłową nazwę, wtedy włączę z powrotem        │
-- └─────────────────────────────────────────────────────────────────────────┘
SELECT N'0.3-0.5 — WYŁĄCZONE. Najpierw zobacz raport 0.01 i powiedz mi nazwę kolumny user_id w HM.DK' AS Info;

/* ===========================================================================
   ===  A. WOLUMEN I WARTOŚĆ  =================================================
   =========================================================================== */
SELECT N'A.1 — Wolumen miesięczny w erze Mai (per handlowiec)' AS [Raport];

WITH PerH AS (
    SELECT Handlowiec, RokMiesiac,
           COUNT(DISTINCT DKId)         AS LiczbaFaktur,
           COUNT(DISTINCT KontrahentId) AS LiczbaKlientow,
           SUM(Kg)                      AS SumaKg,
           SUM(WartoscNetto)            AS SumaNetto,
           SUM(WartoscNetto) / NULLIF(SUM(Kg), 0) AS SredniaCena
    FROM #FaktBaza
    WHERE DataFaktury BETWEEN @DataMaja AND @DataDo
      AND Handlowiec NOT IN (N'Nieprzypisany', N'Ogolne', N'Ogólne')
    GROUP BY Handlowiec, RokMiesiac
)
SELECT Handlowiec, RokMiesiac, LiczbaFaktur, LiczbaKlientow,
       CAST(SumaKg AS DECIMAL(18,1))               AS SumaKg,
       CAST(SumaNetto AS DECIMAL(18,2))            AS SumaNetto,
       CAST(SredniaCena AS DECIMAL(10,2))          AS SredniaCenaZlKg,
       CAST(SumaNetto / NULLIF(LiczbaFaktur, 0) AS DECIMAL(18,2)) AS SredniaWartFaktury,
       CASE WHEN LAG(SumaNetto) OVER (PARTITION BY Handlowiec ORDER BY RokMiesiac) > 0
            THEN CAST((SumaNetto - LAG(SumaNetto) OVER (PARTITION BY Handlowiec ORDER BY RokMiesiac))
                     / LAG(SumaNetto) OVER (PARTITION BY Handlowiec ORDER BY RokMiesiac) * 100 AS DECIMAL(8,1))
            ELSE NULL END AS ZmianaProc
FROM PerH
ORDER BY Handlowiec, RokMiesiac;

SELECT N'A.2 — Udział Mai w sprzedaży firmy per miesiąc (era Mai)' AS [Raport];

WITH M AS (
    SELECT RokMiesiac,
           SUM(CASE WHEN Handlowiec = @HandlowiecMaja THEN Kg ELSE 0 END) AS KgMaja,
           SUM(CASE WHEN Handlowiec = @HandlowiecMaja THEN WartoscNetto ELSE 0 END) AS NettoMaja,
           SUM(CASE WHEN Handlowiec NOT IN (@HandlowiecMaja, N'Nieprzypisany', N'Ogolne', N'Ogólne')
                    THEN Kg ELSE 0 END) AS KgInni,
           SUM(CASE WHEN Handlowiec NOT IN (@HandlowiecMaja, N'Nieprzypisany', N'Ogolne', N'Ogólne')
                    THEN WartoscNetto ELSE 0 END) AS NettoInni
    FROM #FaktBaza
    WHERE DataFaktury BETWEEN @DataMaja AND @DataDo
    GROUP BY RokMiesiac
)
SELECT RokMiesiac,
       CAST(KgMaja AS DECIMAL(18,1))                                            AS Maja_Kg,
       CAST(NettoMaja AS DECIMAL(18,2))                                         AS Maja_Netto,
       CAST(NettoMaja / NULLIF(KgMaja, 0) AS DECIMAL(10,2))                     AS Maja_CenaZlKg,
       CAST(KgMaja * 100.0 / NULLIF(KgMaja + KgInni, 0) AS DECIMAL(6,2))        AS Maja_UdzialKg_Proc,
       CAST(NettoMaja * 100.0 / NULLIF(NettoMaja + NettoInni, 0) AS DECIMAL(6,2)) AS Maja_UdzialNetto_Proc
FROM M
ORDER BY RokMiesiac;

/* ===========================================================================
   ===  B. KLIENCI MAI + KONCENTRACJA  ========================================
   =========================================================================== */
SELECT N'B.1 — Top 100 klientów Mai (era Mai)' AS [Raport];

WITH K AS (
    SELECT KontrahentId, MAX(KontrahentNazwa) AS KontrahentNazwa,
           COUNT(DISTINCT DKId) AS LiczbaFaktur,
           SUM(Kg) AS SumaKg, SUM(WartoscNetto) AS SumaNetto,
           SUM(WartoscNetto) / NULLIF(SUM(Kg), 0) AS SredniaCena,
           MIN(DataFaktury) AS PierwszaF, MAX(DataFaktury) AS OstatniaF,
           COUNT(DISTINCT FORMAT(DataFaktury, 'yyyy-MM')) AS LiczbaAktywMies
    FROM #FaktBaza
    WHERE DataFaktury BETWEEN @DataMaja AND @DataDo AND Handlowiec = @HandlowiecMaja
    GROUP BY KontrahentId
),
S AS (SELECT SUM(SumaNetto) AS Total FROM K)
SELECT TOP 100 KontrahentNazwa, LiczbaFaktur,
       CAST(SumaKg AS DECIMAL(18,1)) AS SumaKg,
       CAST(SumaNetto AS DECIMAL(18,2)) AS SumaNetto,
       CAST(SredniaCena AS DECIMAL(10,2)) AS SredniaCena,
       PierwszaF, OstatniaF, LiczbaAktywMies,
       CAST(SumaNetto * 100.0 / NULLIF(s.Total, 0) AS DECIMAL(6,2)) AS Udzial_Proc,
       DATEDIFF(DAY, OstatniaF, @DataDo) AS DniOdOstatniej
FROM K CROSS JOIN S s
ORDER BY SumaNetto DESC;

SELECT N'B.2 — Top 5 klientów + skumulowany udział' AS [Raport];

WITH K AS (
    SELECT KontrahentId, MAX(KontrahentNazwa) AS KontrahentNazwa, SUM(WartoscNetto) AS SumaNetto
    FROM #FaktBaza
    WHERE DataFaktury BETWEEN @DataMaja AND @DataDo AND Handlowiec = @HandlowiecMaja
    GROUP BY KontrahentId
),
R AS (SELECT *, ROW_NUMBER() OVER (ORDER BY SumaNetto DESC) AS Pozycja,
              SUM(SumaNetto) OVER () AS Total FROM K)
SELECT Pozycja, KontrahentNazwa, CAST(SumaNetto AS DECIMAL(18,2)) AS SumaNetto,
       CAST(SumaNetto * 100.0 / NULLIF(Total, 0) AS DECIMAL(6,2)) AS Udzial_Proc,
       CAST(SUM(SumaNetto) OVER (ORDER BY SumaNetto DESC) * 100.0 / NULLIF(Total, 0) AS DECIMAL(6,2)) AS Skumul_Proc
FROM R WHERE Pozycja <= 5 ORDER BY Pozycja;

SELECT N'B.3 — HHI koncentracja portfela wszystkich handlowców' AS [Raport];

WITH P AS (
    SELECT Handlowiec, KontrahentId, SUM(WartoscNetto) AS Netto
    FROM #FaktBaza
    WHERE DataFaktury BETWEEN @DataMaja AND @DataDo
      AND Handlowiec NOT IN (N'Nieprzypisany', N'Ogolne', N'Ogólne')
    GROUP BY Handlowiec, KontrahentId
),
S AS (SELECT Handlowiec, SUM(Netto) AS Total FROM P GROUP BY Handlowiec),
U AS (SELECT p.Handlowiec, p.Netto / NULLIF(s.Total, 0) AS Udzial
      FROM P p JOIN S s ON s.Handlowiec = p.Handlowiec)
SELECT u.Handlowiec,
       COUNT(*) AS LiczbaKlientow,
       CAST(s.Total AS DECIMAL(18,2)) AS SumaNetto,
       CAST(SUM(Udzial * Udzial) * 10000 AS DECIMAL(8,1)) AS HHI,
       CASE WHEN SUM(Udzial * Udzial) * 10000 < 1500 THEN N'NISKA (zdrowy portfel)'
            WHEN SUM(Udzial * Udzial) * 10000 < 2500 THEN N'ŚREDNIA'
            ELSE N'WYSOKA (uzależnienie od 1-2 klientów)' END AS Ocena
FROM U u JOIN S s ON s.Handlowiec = u.Handlowiec
GROUP BY u.Handlowiec, s.Total
ORDER BY HHI DESC;

SELECT N'B.4 — Pareto: ilu klientów Mai generuje 50% / 80% / 95% obrotu' AS [Raport];

WITH K AS (
    SELECT KontrahentId, MAX(KontrahentNazwa) AS Nazwa, SUM(WartoscNetto) AS N
    FROM #FaktBaza WHERE DataFaktury BETWEEN @DataMaja AND @DataDo AND Handlowiec = @HandlowiecMaja
    GROUP BY KontrahentId
),
R AS (SELECT *, ROW_NUMBER() OVER (ORDER BY N DESC) AS Poz,
              SUM(N) OVER () AS Total,
              SUM(N) OVER (ORDER BY N DESC ROWS UNBOUNDED PRECEDING) AS Skumul
       FROM K)
SELECT Poz, Nazwa, CAST(N AS DECIMAL(18,2)) AS Netto,
       CAST(N * 100.0 / NULLIF(Total, 0) AS DECIMAL(6,2))         AS Udzial,
       CAST(Skumul * 100.0 / NULLIF(Total, 0) AS DECIMAL(6,2))    AS Skumul_Proc,
       CASE WHEN Skumul * 100.0 / NULLIF(Total, 0) <= 50 THEN N'TOP 50%'
            WHEN Skumul * 100.0 / NULLIF(Total, 0) <= 80 THEN N'TOP 80%'
            WHEN Skumul * 100.0 / NULLIF(Total, 0) <= 95 THEN N'TOP 95%'
            ELSE N'Reszta 5%' END                                AS Bucket
FROM R ORDER BY Poz;

/* ===========================================================================
   ===  C. NOWI / PRZEJĘCI / UTRACENI  ========================================
   =========================================================================== */
SELECT N'C.1 — Klienci Mai: NOWI / PRZEJĘCI / KONTYNUACJA (vs okres 2025-07..2025-09)' AS [Raport];

WITH Przed AS (
    SELECT KontrahentId, MAX(KontrahentNazwa) AS KontrahentNazwa,
           SUM(WartoscNetto) AS NettoPrzed,
           (SELECT TOP 1 Handlowiec FROM #FaktBaza f2
            WHERE f2.KontrahentId = f.KontrahentId
              AND f2.DataFaktury BETWEEN @DataOdShort AND DATEADD(DAY, -1, @DataMaja)
            GROUP BY Handlowiec ORDER BY SUM(WartoscNetto) DESC) AS PoprzedniH
    FROM #FaktBaza f
    WHERE DataFaktury BETWEEN @DataOdShort AND DATEADD(DAY, -1, @DataMaja)
    GROUP BY KontrahentId
),
PodMaja AS (
    SELECT KontrahentId, MAX(KontrahentNazwa) AS KontrahentNazwa,
           SUM(WartoscNetto) AS NettoMaja, MIN(DataFaktury) AS PierwszaF
    FROM #FaktBaza
    WHERE DataFaktury BETWEEN @DataMaja AND @DataDo AND Handlowiec = @HandlowiecMaja
    GROUP BY KontrahentId
)
SELECT CASE WHEN p.KontrahentId IS NULL                THEN N'NOWY (nie kupował w 3 mies. przed Mają)'
            WHEN p.PoprzedniH = @HandlowiecMaja        THEN N'KONTYNUACJA (już pod Mają)'
            WHEN p.PoprzedniH IS NOT NULL              THEN N'PRZEJĘTY od: ' + p.PoprzedniH
            ELSE N'NIEZNANE' END                       AS Kategoria,
       m.KontrahentNazwa, p.PoprzedniH,
       CAST(ISNULL(p.NettoPrzed, 0) AS DECIMAL(18,2))  AS Netto3MiesPrzed,
       CAST(m.NettoMaja AS DECIMAL(18,2))              AS NettoEraMai,
       m.PierwszaF
FROM PodMaja m
LEFT JOIN Przed p ON p.KontrahentId = m.KontrahentId
ORDER BY m.NettoMaja DESC;

SELECT N'C.2 — Klienci UTRACENI (kupowali przed 2025-10, nie kupują pod Mają)' AS [Raport];

WITH Przed AS (
    SELECT KontrahentId, MAX(KontrahentNazwa) AS KontrahentNazwa,
           SUM(WartoscNetto) AS NettoPrzed, MAX(DataFaktury) AS OstatniaP,
           (SELECT TOP 1 Handlowiec FROM #FaktBaza f2
            WHERE f2.KontrahentId = f.KontrahentId
              AND f2.DataFaktury BETWEEN @DataOdShort AND DATEADD(DAY, -1, @DataMaja)
            GROUP BY Handlowiec ORDER BY SUM(WartoscNetto) DESC) AS PoprzedniH
    FROM #FaktBaza f
    WHERE DataFaktury BETWEEN @DataOdShort AND DATEADD(DAY, -1, @DataMaja)
    GROUP BY KontrahentId
),
PodMaja AS (
    SELECT DISTINCT KontrahentId
    FROM #FaktBaza
    WHERE DataFaktury BETWEEN @DataMaja AND @DataDo AND Handlowiec = @HandlowiecMaja
)
SELECT p.KontrahentNazwa, p.PoprzedniH,
       CAST(p.NettoPrzed AS DECIMAL(18,2)) AS NettoPrzed, p.OstatniaP,
       DATEDIFF(DAY, p.OstatniaP, @DataDo) AS DniBezZakupu,
       CASE WHEN p.PoprzedniH = @HandlowiecMaja THEN N'UTRACONY BIZNESOWO (był u Mai, przestał)'
            ELSE N'KLIENT INNEGO HANDLOWCA (informacyjnie)' END AS Kategoria
FROM Przed p
WHERE p.KontrahentId NOT IN (SELECT KontrahentId FROM PodMaja)
  AND p.PoprzedniH IS NOT NULL
ORDER BY p.NettoPrzed DESC;

/* ===========================================================================
   ===  D. CENY MAI vs BENCHMARK  =============================================
   =========================================================================== */
SELECT N'D.1 — Średnia cena Mai vs średnia firmy per towar/miesiąc' AS [Raport];

WITH CenyM AS (
    SELECT TowarId, MAX(TowarNazwa) AS TowarNazwa, Kategoria, RokMiesiac,
           SUM(Kg) AS KgMaja, SUM(WartoscNetto) AS NettoMaja,
           SUM(WartoscNetto) / NULLIF(SUM(Kg), 0) AS CenaMaja
    FROM #FaktBaza
    WHERE DataFaktury BETWEEN @DataMaja AND @DataDo AND Handlowiec = @HandlowiecMaja
    GROUP BY TowarId, Kategoria, RokMiesiac
),
CenyI AS (
    SELECT TowarId, RokMiesiac, SUM(Kg) AS KgInni,
           SUM(WartoscNetto) / NULLIF(SUM(Kg), 0) AS CenaInni
    FROM #FaktBaza
    WHERE DataFaktury BETWEEN @DataMaja AND @DataDo
      AND Handlowiec NOT IN (@HandlowiecMaja, N'Nieprzypisany', N'Ogolne', N'Ogólne')
    GROUP BY TowarId, RokMiesiac
)
SELECT m.RokMiesiac, m.TowarNazwa, m.Kategoria,
       CAST(m.KgMaja AS DECIMAL(18,1))             AS Maja_Kg,
       CAST(m.CenaMaja AS DECIMAL(10,2))           AS Maja_Cena,
       CAST(i.CenaInni AS DECIMAL(10,2))           AS Inni_Cena,
       CAST(m.CenaMaja - i.CenaInni AS DECIMAL(10,2)) AS RoznicaZlKg,
       CAST((m.CenaMaja - i.CenaInni) / NULLIF(i.CenaInni, 0) * 100 AS DECIMAL(8,2)) AS RoznicaProc,
       CAST((m.CenaMaja - i.CenaInni) * m.KgMaja AS DECIMAL(18,2)) AS Marza_vs_Bench_Zl
FROM CenyM m LEFT JOIN CenyI i ON i.TowarId = m.TowarId AND i.RokMiesiac = m.RokMiesiac
ORDER BY m.RokMiesiac, m.NettoMaja DESC;

SELECT N'D.2 — SUMA MARŻY MAI vs benchmark' AS [Raport];

WITH CenyM AS (
    SELECT TowarId, RokMiesiac, SUM(Kg) AS KgMaja, SUM(WartoscNetto) AS NettoMaja
    FROM #FaktBaza WHERE DataFaktury BETWEEN @DataMaja AND @DataDo AND Handlowiec = @HandlowiecMaja
    GROUP BY TowarId, RokMiesiac
),
CenyI AS (
    SELECT TowarId, RokMiesiac, SUM(WartoscNetto) / NULLIF(SUM(Kg), 0) AS CenaInni
    FROM #FaktBaza WHERE DataFaktury BETWEEN @DataMaja AND @DataDo
      AND Handlowiec NOT IN (@HandlowiecMaja, N'Nieprzypisany', N'Ogolne', N'Ogólne')
    GROUP BY TowarId, RokMiesiac
)
SELECT CAST(SUM(m.KgMaja) AS DECIMAL(18,1)) AS Maja_KgRazem,
       CAST(SUM(m.NettoMaja) AS DECIMAL(18,2)) AS Maja_NettoRazem,
       CAST(SUM(m.NettoMaja) / NULLIF(SUM(m.KgMaja), 0) AS DECIMAL(10,2)) AS Maja_SredniaCena,
       CAST(SUM(m.KgMaja * i.CenaInni) / NULLIF(SUM(m.KgMaja), 0) AS DECIMAL(10,2)) AS Benchmark_SredniaCena,
       CAST(SUM(m.NettoMaja - m.KgMaja * i.CenaInni) AS DECIMAL(18,2)) AS Marza_Maja_vs_Bench_Zl,
       CASE WHEN SUM(m.KgMaja * i.CenaInni) > 0
            THEN CAST(SUM(m.NettoMaja - m.KgMaja * i.CenaInni) * 100.0 / SUM(m.KgMaja * i.CenaInni) AS DECIMAL(8,2))
            ELSE NULL END AS Marza_Proc
FROM CenyM m INNER JOIN CenyI i ON i.TowarId = m.TowarId AND i.RokMiesiac = m.RokMiesiac;

SELECT N'D.3 — TOP 10 pozycji, gdzie Maja TRACI MARŻĘ' AS [Raport];

WITH CenyM AS (
    SELECT TowarId, MAX(TowarNazwa) AS TowarNazwa, SUM(Kg) AS KgMaja,
           SUM(WartoscNetto) / NULLIF(SUM(Kg), 0) AS CenaMaja
    FROM #FaktBaza WHERE DataFaktury BETWEEN @DataMaja AND @DataDo AND Handlowiec = @HandlowiecMaja
    GROUP BY TowarId
),
CenyI AS (
    SELECT TowarId, SUM(Kg) AS KgInni, SUM(WartoscNetto) / NULLIF(SUM(Kg), 0) AS CenaInni
    FROM #FaktBaza WHERE DataFaktury BETWEEN @DataMaja AND @DataDo
      AND Handlowiec NOT IN (@HandlowiecMaja, N'Nieprzypisany', N'Ogolne', N'Ogólne')
    GROUP BY TowarId
)
SELECT TOP 10 m.TowarNazwa,
       CAST(m.KgMaja AS DECIMAL(18,1)) AS Maja_Kg,
       CAST(m.CenaMaja AS DECIMAL(10,2)) AS Maja_Cena,
       CAST(i.CenaInni AS DECIMAL(10,2)) AS Inni_Cena,
       CAST(m.CenaMaja - i.CenaInni AS DECIMAL(10,2)) AS Roznica,
       CAST((m.CenaMaja - i.CenaInni) * m.KgMaja AS DECIMAL(18,2)) AS Strata_Marzy_Zl
FROM CenyM m INNER JOIN CenyI i ON i.TowarId = m.TowarId
WHERE i.KgInni > 100
ORDER BY (m.CenaMaja - i.CenaInni) * m.KgMaja ASC;

SELECT N'D.4 — TOP 10 pozycji, gdzie Maja ZARABIA więcej niż średnia' AS [Raport];

WITH CenyM AS (
    SELECT TowarId, MAX(TowarNazwa) AS TowarNazwa, SUM(Kg) AS KgMaja,
           SUM(WartoscNetto) / NULLIF(SUM(Kg), 0) AS CenaMaja
    FROM #FaktBaza WHERE DataFaktury BETWEEN @DataMaja AND @DataDo AND Handlowiec = @HandlowiecMaja
    GROUP BY TowarId
),
CenyI AS (
    SELECT TowarId, SUM(Kg) AS KgInni, SUM(WartoscNetto) / NULLIF(SUM(Kg), 0) AS CenaInni
    FROM #FaktBaza WHERE DataFaktury BETWEEN @DataMaja AND @DataDo
      AND Handlowiec NOT IN (@HandlowiecMaja, N'Nieprzypisany', N'Ogolne', N'Ogólne')
    GROUP BY TowarId
)
SELECT TOP 10 m.TowarNazwa,
       CAST(m.KgMaja AS DECIMAL(18,1)) AS Maja_Kg,
       CAST(m.CenaMaja AS DECIMAL(10,2)) AS Maja_Cena,
       CAST(i.CenaInni AS DECIMAL(10,2)) AS Inni_Cena,
       CAST(m.CenaMaja - i.CenaInni AS DECIMAL(10,2)) AS Roznica,
       CAST((m.CenaMaja - i.CenaInni) * m.KgMaja AS DECIMAL(18,2)) AS Zysk_Marzy_Zl
FROM CenyM m INNER JOIN CenyI i ON i.TowarId = m.TowarId
WHERE i.KgInni > 100
ORDER BY (m.CenaMaja - i.CenaInni) * m.KgMaja DESC;

/* ===========================================================================
   ===  E. MIX PRODUKTOWY  ====================================================
   =========================================================================== */
SELECT N'E.1 — Top 20 towarów Mai (era Mai)' AS [Raport];

SELECT TOP 20 TowarNazwa, Kategoria,
       CAST(SUM(Kg) AS DECIMAL(18,1)) AS SumaKg,
       CAST(SUM(WartoscNetto) AS DECIMAL(18,2)) AS SumaNetto,
       CAST(SUM(WartoscNetto) / NULLIF(SUM(Kg), 0) AS DECIMAL(10,2)) AS SredniaCena,
       COUNT(DISTINCT KontrahentId) AS LiczbaKlientow,
       COUNT(DISTINCT DKId) AS LiczbaFaktur
FROM #FaktBaza
WHERE DataFaktury BETWEEN @DataMaja AND @DataDo AND Handlowiec = @HandlowiecMaja
GROUP BY TowarId, TowarNazwa, Kategoria
ORDER BY SumaNetto DESC;

SELECT N'E.2 — Mix Maja vs Mix firmy (świeże/mrożone/inne)' AS [Raport];

WITH M AS (
    SELECT Handlowiec, Kategoria, SUM(Kg) AS Kg, SUM(WartoscNetto) AS Netto
    FROM #FaktBaza WHERE DataFaktury BETWEEN @DataMaja AND @DataDo
      AND Handlowiec NOT IN (N'Nieprzypisany', N'Ogolne', N'Ogólne')
    GROUP BY Handlowiec, Kategoria
),
S AS (SELECT Handlowiec, SUM(Netto) AS Total FROM M GROUP BY Handlowiec)
SELECT m.Handlowiec, m.Kategoria,
       CAST(m.Kg AS DECIMAL(18,1)) AS Kg,
       CAST(m.Netto AS DECIMAL(18,2)) AS Netto,
       CAST(m.Netto * 100.0 / NULLIF(s.Total, 0) AS DECIMAL(6,2)) AS Udzial_Proc
FROM M m JOIN S s ON s.Handlowiec = m.Handlowiec
ORDER BY m.Handlowiec, m.Netto DESC;

/* ===========================================================================
   ===  F. FREKWENCJA KLIENTÓW  ===============================================
   =========================================================================== */
SELECT N'F.1 — Frekwencja klientów Mai (sygnał ucieczki: 30/60/90 dni)' AS [Raport];

WITH D AS (
    SELECT KontrahentId, MAX(KontrahentNazwa) AS KontrahentNazwa,
           COUNT(DISTINCT CAST(DataFaktury AS DATE)) AS DniKupna,
           MIN(DataFaktury) AS PierwszaF, MAX(DataFaktury) AS OstatniaF,
           DATEDIFF(DAY, MIN(DataFaktury), MAX(DataFaktury)) AS RozpietoscDni,
           SUM(WartoscNetto) AS SumaNetto
    FROM #FaktBaza
    WHERE DataFaktury BETWEEN @DataMaja AND @DataDo AND Handlowiec = @HandlowiecMaja
    GROUP BY KontrahentId
)
SELECT KontrahentNazwa, DniKupna, PierwszaF, OstatniaF, RozpietoscDni,
       CAST(SumaNetto AS DECIMAL(18,2)) AS SumaNetto,
       CAST(RozpietoscDni * 1.0 / NULLIF(DniKupna - 1, 0) AS DECIMAL(8,1)) AS SrednioDniMiedzy,
       DATEDIFF(DAY, OstatniaF, @DataDo) AS DniOdOstatniej,
       CASE WHEN DATEDIFF(DAY, OstatniaF, @DataDo) > 90 THEN N'CZERWONY 90+ dni (utracony?)'
            WHEN DATEDIFF(DAY, OstatniaF, @DataDo) > 60 THEN N'POMARANCZA 60-90 dni (ryzyko)'
            WHEN DATEDIFF(DAY, OstatniaF, @DataDo) > 30 THEN N'ZOLTY 30-60 dni (uwaga)'
            ELSE N'ZIELONY aktywny' END AS Sygnal
FROM D ORDER BY DniOdOstatniej DESC, SumaNetto DESC;

SELECT N'F.2 — Współczynnik aktywności bazy Mai per miesiąc' AS [Raport];

WITH Baza AS (
    SELECT DISTINCT KontrahentId
    FROM #FaktBaza WHERE DataFaktury BETWEEN @DataMaja AND @DataDo AND Handlowiec = @HandlowiecMaja
),
Akt AS (
    SELECT RokMiesiac, COUNT(DISTINCT KontrahentId) AS AktywnychKl
    FROM #FaktBaza WHERE DataFaktury BETWEEN @DataMaja AND @DataDo AND Handlowiec = @HandlowiecMaja
    GROUP BY RokMiesiac
)
SELECT a.RokMiesiac, a.AktywnychKl,
       (SELECT COUNT(*) FROM Baza) AS BazaRazem,
       CAST(a.AktywnychKl * 100.0 / NULLIF((SELECT COUNT(*) FROM Baza), 0) AS DECIMAL(6,2)) AS Aktywnosc_Proc
FROM Akt a ORDER BY a.RokMiesiac;

/* ===========================================================================
   ===  I. PŁATNOŚCI  =========================================================
   =========================================================================== */
SELECT N'I.1 — Stan należności klientów Mai (na dziś)' AS [Raport];

WITH PNAgg AS (
    SELECT PN.dkid, SUM(ISNULL(PN.kwotarozl, 0)) AS Rozliczone, MAX(PN.Termin) AS TerminPraw
    FROM [HM].[PN] PN WITH (NOLOCK) GROUP BY PN.dkid
),
Saldo AS (
    SELECT DK.id, DK.khid, DK.kod AS NumerFaktury,
           DK.walbrutto AS Brutto, DK.walbrutto - ISNULL(PA.Rozliczone, 0) AS DoZaplaty,
           ISNULL(PA.TerminPraw, DK.plattermin) AS Termin,
           CASE WHEN DK.walbrutto - ISNULL(PA.Rozliczone, 0) > 0.01
                 AND GETDATE() > ISNULL(PA.TerminPraw, DK.plattermin)
                THEN DATEDIFF(DAY, ISNULL(PA.TerminPraw, DK.plattermin), GETDATE())
                ELSE 0 END AS DniPrzeterm
    FROM [HM].[DK] DK WITH (NOLOCK)
    LEFT JOIN PNAgg PA ON PA.dkid = DK.id
    WHERE DK.anulowany = 0 AND DK.walbrutto - ISNULL(PA.Rozliczone, 0) > 0.01
)
SELECT C.shortcut AS Kontrahent, WYM.CDim_Handlowiec_Val AS Handlowiec,
       ISNULL(C.LimitAmount, 0) AS LimitKredytu,
       CAST(SUM(S.DoZaplaty) AS DECIMAL(18,2)) AS DoZaplaty,
       CAST(SUM(CASE WHEN S.DniPrzeterm = 0 THEN S.DoZaplaty ELSE 0 END) AS DECIMAL(18,2)) AS Terminowe,
       CAST(SUM(CASE WHEN S.DniPrzeterm > 0 THEN S.DoZaplaty ELSE 0 END) AS DECIMAL(18,2)) AS Przeterminowane,
       MAX(S.DniPrzeterm) AS MaxDniPrzeterm,
       COUNT(*) AS LiczbaFaktur
FROM Saldo S
JOIN [SSCommon].[STContractors] C WITH (NOLOCK) ON C.id = S.khid
JOIN [SSCommon].[ContractorClassification] WYM WITH (NOLOCK) ON WYM.ElementId = S.khid
WHERE WYM.CDim_Handlowiec_Val = @HandlowiecMaja
GROUP BY C.shortcut, WYM.CDim_Handlowiec_Val, C.LimitAmount
ORDER BY Przeterminowane DESC, DoZaplaty DESC;

SELECT N'I.2 — Średnia jakość płatności klientów per handlowiec' AS [Raport];

WITH PNAgg AS (
    SELECT PN.dkid, SUM(ISNULL(PN.kwotarozl, 0)) AS Rozliczone, MAX(PN.Termin) AS TerminPraw
    FROM [HM].[PN] PN WITH (NOLOCK) GROUP BY PN.dkid
),
Saldo AS (
    SELECT DK.khid, DK.walbrutto - ISNULL(PA.Rozliczone, 0) AS DoZaplaty,
           CASE WHEN DK.walbrutto - ISNULL(PA.Rozliczone, 0) > 0.01
                 AND GETDATE() > ISNULL(PA.TerminPraw, DK.plattermin)
                THEN DATEDIFF(DAY, ISNULL(PA.TerminPraw, DK.plattermin), GETDATE())
                ELSE 0 END AS DniPrzeterm
    FROM [HM].[DK] DK WITH (NOLOCK)
    LEFT JOIN PNAgg PA ON PA.dkid = DK.id
    WHERE DK.anulowany = 0 AND DK.walbrutto - ISNULL(PA.Rozliczone, 0) > 0.01
)
SELECT ISNULL(WYM.CDim_Handlowiec_Val, N'Nieprzypisany') AS Handlowiec,
       COUNT(*) AS LiczbaFakturOtw,
       CAST(SUM(S.DoZaplaty) AS DECIMAL(18,2)) AS Naleznosci,
       CAST(SUM(CASE WHEN S.DniPrzeterm > 0 THEN S.DoZaplaty ELSE 0 END) AS DECIMAL(18,2)) AS Przeterminowane,
       CAST(SUM(CASE WHEN S.DniPrzeterm > 0 THEN S.DoZaplaty ELSE 0 END) * 100.0
            / NULLIF(SUM(S.DoZaplaty), 0) AS DECIMAL(6,2)) AS Przeterm_Proc,
       CAST(AVG(CAST(S.DniPrzeterm AS DECIMAL(10,2))) AS DECIMAL(8,2)) AS SredniaDniPrzeterm
FROM Saldo S
LEFT JOIN [SSCommon].[ContractorClassification] WYM WITH (NOLOCK) ON WYM.ElementId = S.khid
GROUP BY ISNULL(WYM.CDim_Handlowiec_Val, N'Nieprzypisany')
ORDER BY Przeterm_Proc DESC;

/* ===========================================================================
   ===  K1 — SCORECARD FAKTUROWY  =============================================
   =========================================================================== */
SELECT N'K1 — SCORECARD FAKTUROWY (era Mai, wszyscy handlowcy)' AS [Raport];

WITH F AS (
    SELECT Handlowiec, KontrahentId, DKId, Kg, WartoscNetto
    FROM #FaktBaza WHERE DataFaktury BETWEEN @DataMaja AND @DataDo
      AND Handlowiec NOT IN (N'Nieprzypisany', N'Ogolne', N'Ogólne')
),
P AS (SELECT Handlowiec, KontrahentId, SUM(WartoscNetto) AS Netto FROM F GROUP BY Handlowiec, KontrahentId),
S AS (SELECT Handlowiec, SUM(Netto) AS Total FROM P GROUP BY Handlowiec),
HHI AS (SELECT p.Handlowiec, SUM(POWER(p.Netto / NULLIF(s.Total, 0), 2)) * 10000 AS HHI
        FROM P p JOIN S s ON s.Handlowiec = p.Handlowiec GROUP BY p.Handlowiec),
KlPrzed AS (SELECT DISTINCT Handlowiec, KontrahentId FROM #FaktBaza
            WHERE DataFaktury BETWEEN @DataOdShort AND DATEADD(DAY, -1, @DataMaja)),
KlTeraz AS (SELECT DISTINCT Handlowiec, KontrahentId FROM F),
Nowi AS (SELECT t.Handlowiec, COUNT(*) AS LiczbaNowych
         FROM KlTeraz t LEFT JOIN KlPrzed p ON p.Handlowiec = t.Handlowiec AND p.KontrahentId = t.KontrahentId
         WHERE p.KontrahentId IS NULL GROUP BY t.Handlowiec),
Utrac AS (SELECT p.Handlowiec, COUNT(*) AS LiczbaUtraconych
          FROM KlPrzed p LEFT JOIN KlTeraz t ON t.Handlowiec = p.Handlowiec AND t.KontrahentId = p.KontrahentId
          WHERE t.KontrahentId IS NULL GROUP BY p.Handlowiec),
BenchM AS (
    SELECT m.Handlowiec, SUM(m.NettoMaja - m.KgMaja * i.CenaInni) AS MarzaVsBench
    FROM (
        SELECT Handlowiec, TowarId, RokMiesiac, SUM(Kg) AS KgMaja, SUM(WartoscNetto) AS NettoMaja
        FROM #FaktBaza WHERE DataFaktury BETWEEN @DataMaja AND @DataDo
        GROUP BY Handlowiec, TowarId, RokMiesiac
    ) m INNER JOIN (
        SELECT TowarId, RokMiesiac, SUM(WartoscNetto) / NULLIF(SUM(Kg), 0) AS CenaInni
        FROM #FaktBaza WHERE DataFaktury BETWEEN @DataMaja AND @DataDo
        GROUP BY TowarId, RokMiesiac
    ) i ON i.TowarId = m.TowarId AND i.RokMiesiac = m.RokMiesiac
    GROUP BY m.Handlowiec
),
Agg AS (
    SELECT f.Handlowiec, COUNT(DISTINCT f.KontrahentId) AS LiczbaKlientow,
           COUNT(DISTINCT f.DKId) AS LiczbaFaktur, SUM(f.Kg) AS SumaKg,
           SUM(f.WartoscNetto) AS SumaNetto, SUM(f.WartoscNetto) / NULLIF(SUM(f.Kg), 0) AS SredniaCena
    FROM F f GROUP BY f.Handlowiec
)
SELECT a.Handlowiec, a.LiczbaKlientow, a.LiczbaFaktur,
       CAST(a.SumaKg AS DECIMAL(18,1)) AS SumaKg,
       CAST(a.SumaNetto AS DECIMAL(18,2)) AS SumaNetto,
       CAST(a.SredniaCena AS DECIMAL(10,2)) AS SredniaCenaZlKg,
       ISNULL(n.LiczbaNowych, 0) AS NowiKlienci,
       ISNULL(u.LiczbaUtraconych, 0) AS UtraceniKlienci,
       CAST(h.HHI AS DECIMAL(8,1)) AS HHI,
       CAST(b.MarzaVsBench AS DECIMAL(18,2)) AS Marza_vs_Bench_Zl
FROM Agg a
LEFT JOIN Nowi n ON n.Handlowiec = a.Handlowiec
LEFT JOIN Utrac u ON u.Handlowiec = a.Handlowiec
LEFT JOIN HHI h ON h.Handlowiec = a.Handlowiec
LEFT JOIN BenchM b ON b.Handlowiec = a.Handlowiec
ORDER BY CASE WHEN a.Handlowiec = @HandlowiecMaja THEN 0 ELSE 1 END, a.SumaNetto DESC;

/* ###########################################################################
   ##  ==== NOWE SEKCJE (L-T): ERA DANIELA vs ERA MAI + DRILL-DOWNS ====
   ########################################################################### */

/* ===========================================================================
   ===  L. ERA DANIELA vs ERA MAI (per klient)  ===============================
   =========================================================================== */
SELECT N'L.1 — Klient po kliencie: ERA DANIELA (przed 2025-10) vs ERA MAI' AS [Raport];

WITH KlienciMai AS (
    SELECT DISTINCT KontrahentId
    FROM #FaktBaza
    WHERE DataFaktury BETWEEN @DataMaja AND @DataDo AND Handlowiec = @HandlowiecMaja
),
EraDaniela AS (
    SELECT KontrahentId, MAX(KontrahentNazwa) AS Nazwa,
           SUM(WartoscNetto)                              AS NettoDaniel,
           SUM(Kg)                                        AS KgDaniel,
           COUNT(DISTINCT DKId)                           AS FakturDaniel,
           MIN(DataFaktury)                               AS PierwszaDaniel,
           MAX(DataFaktury)                               AS OstatniaDaniel,
           DATEDIFF(MONTH, MIN(DataFaktury), MAX(DataFaktury)) + 1 AS MiesiecyDaniel
    FROM #FaktBaza
    WHERE DataFaktury < @DataMaja
      AND KontrahentId IN (SELECT KontrahentId FROM KlienciMai)
    GROUP BY KontrahentId
),
EraMai AS (
    SELECT KontrahentId,
           SUM(WartoscNetto)                              AS NettoMaja,
           SUM(Kg)                                        AS KgMaja,
           COUNT(DISTINCT DKId)                           AS FakturMaja,
           MIN(DataFaktury)                               AS PierwszaMaja,
           MAX(DataFaktury)                               AS OstatniaMaja,
           DATEDIFF(MONTH, @DataMaja, @DataDo) + 1        AS MiesiecyMai
    FROM #FaktBaza
    WHERE DataFaktury BETWEEN @DataMaja AND @DataDo
      AND Handlowiec = @HandlowiecMaja
    GROUP BY KontrahentId
)
SELECT ISNULL(d.Nazwa, CAST(m.KontrahentId AS NVARCHAR(20)))  AS Klient,
       -- Era Daniela
       CAST(d.NettoDaniel AS DECIMAL(18,2))                   AS Daniel_Netto,
       CAST(d.KgDaniel AS DECIMAL(18,1))                      AS Daniel_Kg,
       d.FakturDaniel                                          AS Daniel_Faktur,
       d.MiesiecyDaniel                                        AS Daniel_Mies,
       CAST(d.NettoDaniel / NULLIF(d.MiesiecyDaniel,0) AS DECIMAL(18,2)) AS Daniel_NettoMies,
       d.PierwszaDaniel, d.OstatniaDaniel,
       -- Era Mai
       CAST(m.NettoMaja AS DECIMAL(18,2))                     AS Maja_Netto,
       CAST(m.KgMaja AS DECIMAL(18,1))                        AS Maja_Kg,
       m.FakturMaja                                            AS Maja_Faktur,
       m.MiesiecyMai                                           AS Maja_Mies,
       CAST(m.NettoMaja / NULLIF(m.MiesiecyMai,0) AS DECIMAL(18,2)) AS Maja_NettoMies,
       m.PierwszaMaja, m.OstatniaMaja,
       -- Porównanie tempo miesięczne
       CASE WHEN d.NettoDaniel IS NULL THEN N'NOWY POD MAJĄ'
            WHEN m.NettoMaja IS NULL  THEN N'UTRACONY (był u Daniela)'
            WHEN (m.NettoMaja / NULLIF(m.MiesiecyMai,0)) > (d.NettoDaniel / NULLIF(d.MiesiecyDaniel,0)) * 1.1
                                       THEN N'ROŚNIE pod Mają'
            WHEN (m.NettoMaja / NULLIF(m.MiesiecyMai,0)) < (d.NettoDaniel / NULLIF(d.MiesiecyDaniel,0)) * 0.9
                                       THEN N'SPADA pod Mają'
            ELSE N'STABILNY (±10%)' END                       AS Trend,
       CASE WHEN d.NettoDaniel > 0 AND m.NettoMaja > 0
            THEN CAST(((m.NettoMaja / NULLIF(m.MiesiecyMai,0))
                     - (d.NettoDaniel / NULLIF(d.MiesiecyDaniel,0)))
                     / NULLIF((d.NettoDaniel / NULLIF(d.MiesiecyDaniel,0)), 0) * 100 AS DECIMAL(8,1))
            ELSE NULL END                                     AS Trend_Proc
FROM EraMai m
FULL OUTER JOIN EraDaniela d ON d.KontrahentId = m.KontrahentId
ORDER BY ISNULL(m.NettoMaja, 0) + ISNULL(d.NettoDaniel, 0) DESC;

SELECT N'L.2 — Łączne podsumowanie: ERA DANIELA vs ERA MAI (klienci Mai)' AS [Raport];

WITH KlienciMai AS (
    SELECT DISTINCT KontrahentId FROM #FaktBaza
    WHERE DataFaktury BETWEEN @DataMaja AND @DataDo AND Handlowiec = @HandlowiecMaja
),
ED AS (
    SELECT SUM(WartoscNetto) AS NettoD, SUM(Kg) AS KgD,
           COUNT(DISTINCT DKId) AS FakturD, COUNT(DISTINCT KontrahentId) AS KlientowD,
           DATEDIFF(MONTH, MIN(DataFaktury), MAX(DataFaktury)) + 1 AS MiesD
    FROM #FaktBaza
    WHERE DataFaktury < @DataMaja AND KontrahentId IN (SELECT KontrahentId FROM KlienciMai)
),
EM AS (
    SELECT SUM(WartoscNetto) AS NettoM, SUM(Kg) AS KgM,
           COUNT(DISTINCT DKId) AS FakturM, COUNT(DISTINCT KontrahentId) AS KlientowM,
           DATEDIFF(MONTH, @DataMaja, @DataDo) + 1 AS MiesM
    FROM #FaktBaza
    WHERE DataFaktury BETWEEN @DataMaja AND @DataDo AND Handlowiec = @HandlowiecMaja
)
SELECT
    CAST(ED.NettoD AS DECIMAL(18,2))             AS Daniel_Netto_Razem,
    CAST(ED.NettoD / NULLIF(ED.MiesD,0) AS DECIMAL(18,2)) AS Daniel_NettoMies,
    ED.KlientowD                                  AS Daniel_Klientow,
    ED.FakturD                                    AS Daniel_Faktur,
    ED.MiesD                                      AS Daniel_Mies,
    CAST(EM.NettoM AS DECIMAL(18,2))             AS Maja_Netto_Razem,
    CAST(EM.NettoM / NULLIF(EM.MiesM,0) AS DECIMAL(18,2)) AS Maja_NettoMies,
    EM.KlientowM                                  AS Maja_Klientow,
    EM.FakturM                                    AS Maja_Faktur,
    EM.MiesM                                      AS Maja_Mies,
    -- Indeks zmiany tempa
    CAST(((EM.NettoM / NULLIF(EM.MiesM,0)) - (ED.NettoD / NULLIF(ED.MiesD,0)))
         / NULLIF((ED.NettoD / NULLIF(ED.MiesD,0)), 0) * 100 AS DECIMAL(8,1)) AS Zmiana_Tempa_Proc,
    CASE WHEN (EM.NettoM / NULLIF(EM.MiesM,0)) > (ED.NettoD / NULLIF(ED.MiesD,0)) * 1.05
         THEN N'✅ MAJA ROZWIJA portfel (>5%)'
         WHEN (EM.NettoM / NULLIF(EM.MiesM,0)) < (ED.NettoD / NULLIF(ED.MiesD,0)) * 0.95
         THEN N'❌ MAJA TRACI tempo (<-5%)'
         ELSE N'➡ STABILNIE (±5%)' END             AS Werdykt
FROM ED, EM;

SELECT N'L.3 — TOP 10 największych ROSNĄCYCH klientów pod Mają (vs era Daniela)' AS [Raport];

WITH KlienciMai AS (
    SELECT DISTINCT KontrahentId FROM #FaktBaza
    WHERE DataFaktury BETWEEN @DataMaja AND @DataDo AND Handlowiec = @HandlowiecMaja
),
D AS (
    SELECT KontrahentId, MAX(KontrahentNazwa) AS Nazwa,
           SUM(WartoscNetto) / NULLIF(DATEDIFF(MONTH, MIN(DataFaktury), MAX(DataFaktury)) + 1, 0) AS NettoMiesD,
           SUM(WartoscNetto) AS NettoD
    FROM #FaktBaza WHERE DataFaktury < @DataMaja
      AND KontrahentId IN (SELECT KontrahentId FROM KlienciMai)
    GROUP BY KontrahentId
),
M AS (
    SELECT KontrahentId,
           SUM(WartoscNetto) / NULLIF(DATEDIFF(MONTH, @DataMaja, @DataDo) + 1, 0) AS NettoMiesM,
           SUM(WartoscNetto) AS NettoM
    FROM #FaktBaza WHERE DataFaktury BETWEEN @DataMaja AND @DataDo AND Handlowiec = @HandlowiecMaja
    GROUP BY KontrahentId
)
SELECT TOP 10 d.Nazwa,
       CAST(d.NettoMiesD AS DECIMAL(18,2)) AS Daniel_NettoMies,
       CAST(m.NettoMiesM AS DECIMAL(18,2)) AS Maja_NettoMies,
       CAST(m.NettoMiesM - d.NettoMiesD AS DECIMAL(18,2)) AS Wzrost_Mies_Zl,
       CAST((m.NettoMiesM - d.NettoMiesD) / NULLIF(d.NettoMiesD,0) * 100 AS DECIMAL(8,1)) AS Wzrost_Proc,
       CAST(d.NettoD AS DECIMAL(18,2)) AS Daniel_TotalNetto,
       CAST(m.NettoM AS DECIMAL(18,2)) AS Maja_TotalNetto
FROM D d INNER JOIN M m ON m.KontrahentId = d.KontrahentId
WHERE d.NettoMiesD > 0
ORDER BY (m.NettoMiesM - d.NettoMiesD) DESC;

SELECT N'L.4 — TOP 10 największych SPADAJĄCYCH klientów pod Mają' AS [Raport];

WITH KlienciMai AS (
    SELECT DISTINCT KontrahentId FROM #FaktBaza
    WHERE DataFaktury BETWEEN @DataMaja AND @DataDo AND Handlowiec = @HandlowiecMaja
),
D AS (
    SELECT KontrahentId, MAX(KontrahentNazwa) AS Nazwa,
           SUM(WartoscNetto) / NULLIF(DATEDIFF(MONTH, MIN(DataFaktury), MAX(DataFaktury)) + 1, 0) AS NettoMiesD,
           SUM(WartoscNetto) AS NettoD
    FROM #FaktBaza WHERE DataFaktury < @DataMaja
      AND KontrahentId IN (SELECT KontrahentId FROM KlienciMai)
    GROUP BY KontrahentId
),
M AS (
    SELECT KontrahentId,
           SUM(WartoscNetto) / NULLIF(DATEDIFF(MONTH, @DataMaja, @DataDo) + 1, 0) AS NettoMiesM,
           SUM(WartoscNetto) AS NettoM
    FROM #FaktBaza WHERE DataFaktury BETWEEN @DataMaja AND @DataDo AND Handlowiec = @HandlowiecMaja
    GROUP BY KontrahentId
)
SELECT TOP 10 d.Nazwa,
       CAST(d.NettoMiesD AS DECIMAL(18,2)) AS Daniel_NettoMies,
       CAST(m.NettoMiesM AS DECIMAL(18,2)) AS Maja_NettoMies,
       CAST(m.NettoMiesM - d.NettoMiesD AS DECIMAL(18,2)) AS Spadek_Mies_Zl,
       CAST((m.NettoMiesM - d.NettoMiesD) / NULLIF(d.NettoMiesD,0) * 100 AS DECIMAL(8,1)) AS Spadek_Proc,
       CAST(d.NettoD AS DECIMAL(18,2)) AS Daniel_TotalNetto,
       CAST(m.NettoM AS DECIMAL(18,2)) AS Maja_TotalNetto
FROM D d INNER JOIN M m ON m.KontrahentId = d.KontrahentId
WHERE d.NettoMiesD > 0
ORDER BY (m.NettoMiesM - d.NettoMiesD) ASC;

SELECT N'L.5 — Klienci, których Maja CAŁKOWICIE STRACIŁA (był u Daniela, brak u Mai)' AS [Raport];

WITH KlienciMai AS (
    SELECT DISTINCT KontrahentId FROM #FaktBaza
    WHERE DataFaktury BETWEEN @DataMaja AND @DataDo AND Handlowiec = @HandlowiecMaja
),
D AS (
    SELECT KontrahentId, MAX(KontrahentNazwa) AS Nazwa,
           SUM(WartoscNetto) AS NettoD,
           MIN(DataFaktury) AS PierwszaD, MAX(DataFaktury) AS OstatniaD,
           COUNT(DISTINCT DKId) AS FakturD
    FROM #FaktBaza WHERE DataFaktury < @DataMaja
      AND KontrahentId IN (SELECT KontrahentId FROM KlienciMai)
    GROUP BY KontrahentId
)
SELECT d.Nazwa,
       CAST(d.NettoD AS DECIMAL(18,2)) AS Daniel_TotalNetto,
       d.FakturD AS Daniel_Faktur,
       d.PierwszaD, d.OstatniaD,
       DATEDIFF(DAY, d.OstatniaD, @DataDo) AS DniBezZakupu
FROM D d
WHERE d.KontrahentId NOT IN (SELECT KontrahentId FROM #FaktBaza
                             WHERE DataFaktury BETWEEN @DataMaja AND @DataDo
                               AND Handlowiec = @HandlowiecMaja)
ORDER BY d.NettoD DESC;

SELECT N'L.6 — Klienci CAŁKOWICIE NOWI pod Mają (zero w erze Daniela)' AS [Raport];

WITH KlienciMai AS (
    SELECT DISTINCT KontrahentId FROM #FaktBaza
    WHERE DataFaktury BETWEEN @DataMaja AND @DataDo AND Handlowiec = @HandlowiecMaja
),
KlienciDaniel AS (
    SELECT DISTINCT KontrahentId FROM #FaktBaza
    WHERE DataFaktury < @DataMaja
      AND KontrahentId IN (SELECT KontrahentId FROM KlienciMai)
),
M AS (
    SELECT KontrahentId, MAX(KontrahentNazwa) AS Nazwa,
           SUM(WartoscNetto) AS NettoM, SUM(Kg) AS KgM,
           COUNT(DISTINCT DKId) AS Faktur,
           MIN(DataFaktury) AS PierwszaF, MAX(DataFaktury) AS OstatniaF,
           SUM(WartoscNetto) / NULLIF(DATEDIFF(MONTH, MIN(DataFaktury), @DataDo) + 1, 0) AS NettoMies
    FROM #FaktBaza WHERE DataFaktury BETWEEN @DataMaja AND @DataDo AND Handlowiec = @HandlowiecMaja
    GROUP BY KontrahentId
)
SELECT m.Nazwa, CAST(m.NettoM AS DECIMAL(18,2)) AS Maja_Netto,
       CAST(m.KgM AS DECIMAL(18,1)) AS Maja_Kg, m.Faktur, m.PierwszaF, m.OstatniaF,
       CAST(m.NettoMies AS DECIMAL(18,2)) AS Maja_NettoMies,
       DATEDIFF(DAY, m.OstatniaF, @DataDo) AS DniOdOstatniej
FROM M m
WHERE m.KontrahentId NOT IN (SELECT KontrahentId FROM KlienciDaniel)
ORDER BY m.NettoM DESC;

/* ===========================================================================
   ===  L.7-L.10 — WYŁĄCZONE do czasu identyfikacji kolumny user_id w HM.DK
   =========================================================================== */
SELECT N'L.7-L.10 — WYŁĄCZONE. Wpisz nazwę kolumny user_id z raportu 0.01' AS Info;

/* L.7-L.10 (Daniel + Dawid portfolio analysis) zostały usunięte do czasu
   identyfikacji właściwej kolumny user_id w HM.DK. Po uruchomieniu raportu 0.01
   wskażemy poprawną kolumnę i dorobimy raporty. Skrypt kontynuuje sekcją M.
=========================================================================== */

/* USUNIĘTY MARTWY KOD L.7-L.10:
DUMMY_NEVER_EXECUTED_BLOCK_START
    ;WITH KlienciDaniela AS (
        SELECT KontrahentId, MAX(KontrahentNazwa) AS Nazwa,
               COUNT(DISTINCT DKId) AS Faktur,
               CAST(SUM(WartoscNetto) AS DECIMAL(18,2)) AS NettoUDaniela,
               MIN(DataFaktury) AS PierwszaUDaniela,
               MAX(DataFaktury) AS OstatniaUDaniela
        FROM #FaktBaza
        WHERE WystawilUserId_NIE_ISTNIEJE = @UserIdDaniel AND DataFaktury < @DataMaja
        GROUP BY KontrahentId
    ),
    StanObecny AS (
        SELECT KontrahentId, Handlowiec AS HandlowiecObecny,
               SUM(WartoscNetto) AS NettoObecnie,
               MAX(DataFaktury) AS OstatniaFakturaObecnie
        FROM #FaktBaza
        WHERE DataFaktury BETWEEN @DataMaja AND @DataDo
        GROUP BY KontrahentId, Handlowiec
    ),
    TopObecny AS (
        SELECT s.*, ROW_NUMBER() OVER (PARTITION BY s.KontrahentId ORDER BY s.NettoObecnie DESC) AS Poz
        FROM StanObecny s
    )
    SELECT d.Nazwa, d.Faktur AS Daniel_Faktur, d.NettoUDaniela,
           d.PierwszaUDaniela, d.OstatniaUDaniela,
           DATEDIFF(DAY, d.OstatniaUDaniela, @DataDo) AS DniOdOstatniejUDaniela,
           ISNULL(t.HandlowiecObecny, N'(nikt — utracony)') AS GdzieJestTeraz,
           CAST(ISNULL(t.NettoObecnie, 0) AS DECIMAL(18,2)) AS Netto_PoMai,
           CASE WHEN t.HandlowiecObecny IS NULL              THEN N'❌ UTRACONY z firmy'
                WHEN t.HandlowiecObecny = @HandlowiecMaja   THEN N'✅ ODZIEDZICZONY przez Maję'
                ELSE N'➡ PRZESZEDŁ pod: ' + t.HandlowiecObecny END AS Los
    FROM KlienciDaniela d
    LEFT JOIN TopObecny t ON t.KontrahentId = d.KontrahentId AND t.Poz = 1
    ORDER BY d.NettoUDaniela DESC;
END;

SELECT N'L.8 — Portfel DAWIDA: wszyscy klienci których kiedykolwiek obsługiwał + gdzie są teraz' AS [Raport];

IF @UserIdDawid IS NULL
    SELECT N'⚠ @UserIdDawid = NULL — wpisz UserID Dawida na początku skryptu (zob. raport 0.3)' AS Info;
ELSE
BEGIN
    ;WITH KlienciDawida AS (
        SELECT KontrahentId, MAX(KontrahentNazwa) AS Nazwa,
               COUNT(DISTINCT DKId) AS Faktur,
               CAST(SUM(WartoscNetto) AS DECIMAL(18,2)) AS NettoUDawida,
               MIN(DataFaktury) AS PierwszaUDawida,
               MAX(DataFaktury) AS OstatniaUDawida
        FROM #FaktBaza
        WHERE WystawilUserId = @UserIdDawid AND DataFaktury < @DataMaja
        GROUP BY KontrahentId
    ),
    StanObecny AS (
        SELECT KontrahentId, Handlowiec AS HandlowiecObecny,
               SUM(WartoscNetto) AS NettoObecnie,
               MAX(DataFaktury) AS OstatniaFakturaObecnie
        FROM #FaktBaza
        WHERE DataFaktury BETWEEN @DataMaja AND @DataDo
        GROUP BY KontrahentId, Handlowiec
    ),
    TopObecny AS (
        SELECT s.*, ROW_NUMBER() OVER (PARTITION BY s.KontrahentId ORDER BY s.NettoObecnie DESC) AS Poz
        FROM StanObecny s
    )
    SELECT d.Nazwa, d.Faktur AS Dawid_Faktur, d.NettoUDawida,
           d.PierwszaUDawida, d.OstatniaUDawida,
           DATEDIFF(DAY, d.OstatniaUDawida, @DataDo) AS DniOdOstatniejUDawida,
           ISNULL(t.HandlowiecObecny, N'(nikt — utracony)') AS GdzieJestTeraz,
           CAST(ISNULL(t.NettoObecnie, 0) AS DECIMAL(18,2)) AS Netto_PoMai,
           CASE WHEN t.HandlowiecObecny IS NULL              THEN N'❌ UTRACONY z firmy'
                WHEN t.HandlowiecObecny = @HandlowiecMaja   THEN N'✅ ODZIEDZICZONY przez Maję'
                ELSE N'➡ PRZESZEDŁ pod: ' + t.HandlowiecObecny END AS Los
    FROM KlienciDawida d
    LEFT JOIN TopObecny t ON t.KontrahentId = d.KontrahentId AND t.Poz = 1
    ORDER BY d.NettoUDawida DESC;
END;

SELECT N'L.9 — PODSUMOWANIE LOSÓW portfeli Daniela + Dawida (co dostała Maja, co straciliśmy)' AS [Raport];

IF @UserIdDaniel IS NULL AND @UserIdDawid IS NULL
    SELECT N'⚠ Wpisz @UserIdDaniel i/lub @UserIdDawid (raport 0.3)' AS Info;
ELSE
BEGIN
    ;WITH PortfelByly AS (
        SELECT KontrahentId, MAX(KontrahentNazwa) AS Nazwa,
               CASE WHEN WystawilUserId = @UserIdDaniel THEN N'Daniel'
                    WHEN WystawilUserId = @UserIdDawid  THEN N'Dawid' END AS Byly,
               SUM(WartoscNetto) AS NettoUByl
        FROM #FaktBaza
        WHERE DataFaktury < @DataMaja
          AND WystawilUserId IN (@UserIdDaniel, @UserIdDawid)
        GROUP BY KontrahentId, CASE WHEN WystawilUserId = @UserIdDaniel THEN N'Daniel'
                                     WHEN WystawilUserId = @UserIdDawid  THEN N'Dawid' END
    ),
    Obecnie AS (
        SELECT KontrahentId, Handlowiec, SUM(WartoscNetto) AS Netto
        FROM #FaktBaza
        WHERE DataFaktury BETWEEN @DataMaja AND @DataDo
        GROUP BY KontrahentId, Handlowiec
    ),
    TopRow AS (
        SELECT *, ROW_NUMBER() OVER (PARTITION BY KontrahentId ORDER BY Netto DESC) AS Poz FROM Obecnie
    )
    SELECT pb.Byly AS BylyHandlowiec,
           CASE WHEN t.Handlowiec IS NULL                  THEN N'❌ UTRACONY z firmy'
                WHEN t.Handlowiec = @HandlowiecMaja        THEN N'✅ ODZIEDZICZONY przez Maję'
                ELSE                                            N'➡ PRZESZEDŁ pod innego' END AS Los,
           COUNT(*)                                                  AS LiczbaKlientow,
           CAST(SUM(pb.NettoUByl) AS DECIMAL(18,2))                  AS NettoLacznie_uBylego,
           CAST(SUM(ISNULL(t.Netto, 0)) AS DECIMAL(18,2))             AS Netto_PoMai
    FROM PortfelByly pb
    LEFT JOIN TopRow t ON t.KontrahentId = pb.KontrahentId AND t.Poz = 1
    GROUP BY pb.Byly,
             CASE WHEN t.Handlowiec IS NULL                  THEN N'❌ UTRACONY z firmy'
                  WHEN t.Handlowiec = @HandlowiecMaja        THEN N'✅ ODZIEDZICZONY przez Maję'
                  ELSE                                            N'➡ PRZESZEDŁ pod innego' END
    ORDER BY pb.Byly, NettoLacznie_uBylego DESC;
END;

SELECT N'L.10 — Klienci ex-Daniel/Dawid PRZEJĘCI PRZEZ INNYCH HANDLOWCÓW (nie Maję)' AS [Raport];

IF @UserIdDaniel IS NULL AND @UserIdDawid IS NULL
    SELECT N'⚠ Wpisz @UserIdDaniel i/lub @UserIdDawid' AS Info;
ELSE
BEGIN
    ;WITH ExKlienci AS (
        SELECT KontrahentId, MAX(KontrahentNazwa) AS Nazwa,
               CASE WHEN WystawilUserId = @UserIdDaniel THEN N'Daniel'
                    WHEN WystawilUserId = @UserIdDawid  THEN N'Dawid' END AS Byly,
               SUM(WartoscNetto) AS NettoUBylego
        FROM #FaktBaza
        WHERE DataFaktury < @DataMaja
          AND WystawilUserId IN (@UserIdDaniel, @UserIdDawid)
        GROUP BY KontrahentId, CASE WHEN WystawilUserId = @UserIdDaniel THEN N'Daniel'
                                     WHEN WystawilUserId = @UserIdDawid  THEN N'Dawid' END
    ),
    Obecnie AS (
        SELECT KontrahentId, Handlowiec, SUM(WartoscNetto) AS NettoTeraz
        FROM #FaktBaza
        WHERE DataFaktury BETWEEN @DataMaja AND @DataDo
        GROUP BY KontrahentId, Handlowiec
    ),
    TopRow AS (SELECT *, ROW_NUMBER() OVER (PARTITION BY KontrahentId ORDER BY NettoTeraz DESC) AS Poz FROM Obecnie)
    SELECT ex.Nazwa, ex.Byly, CAST(ex.NettoUBylego AS DECIMAL(18,2)) AS NettoUBylego,
           t.Handlowiec AS GdzieJestTeraz,
           CAST(t.NettoTeraz AS DECIMAL(18,2)) AS Netto_PoMai
    FROM ExKlienci ex INNER JOIN TopRow t ON t.KontrahentId = ex.KontrahentId AND t.Poz = 1
    WHERE t.Handlowiec IS NOT NULL AND t.Handlowiec <> @HandlowiecMaja
    ORDER BY ex.NettoUBylego DESC;
DUMMY_NEVER_EXECUTED_BLOCK_END
*/

/* ===========================================================================
   ===  M. UDZIAŁ FIRMY DŁUGOTERMINOWY  =======================================
   =========================================================================== */
SELECT N'M.1 — Udział Mai (jako klienci Mai obecnie) vs reszta firmy — 18 mies. wstecz' AS [Raport];

WITH KlienciMai AS (
    SELECT DISTINCT KontrahentId FROM #FaktBaza
    WHERE DataFaktury BETWEEN @DataMaja AND @DataDo AND Handlowiec = @HandlowiecMaja
),
FaktFlagged AS (
    SELECT f.RokMiesiac, f.WartoscNetto,
           CASE WHEN km.KontrahentId IS NOT NULL THEN 1 ELSE 0 END AS JestKlientMai
    FROM #FaktBaza f
    LEFT JOIN KlienciMai km ON km.KontrahentId = f.KontrahentId
)
SELECT RokMiesiac,
       CAST(SUM(CASE WHEN JestKlientMai = 1 THEN WartoscNetto ELSE 0 END) AS DECIMAL(18,2)) AS Netto_KlienciMai,
       CAST(SUM(CASE WHEN JestKlientMai = 0 THEN WartoscNetto ELSE 0 END) AS DECIMAL(18,2)) AS Netto_Reszta,
       CAST(SUM(WartoscNetto) AS DECIMAL(18,2)) AS Netto_Total,
       CAST(SUM(CASE WHEN JestKlientMai = 1 THEN WartoscNetto ELSE 0 END) * 100.0
            / NULLIF(SUM(WartoscNetto), 0) AS DECIMAL(6,2)) AS Udzial_KlienciMai_Proc
FROM FaktFlagged
GROUP BY RokMiesiac
ORDER BY RokMiesiac;

SELECT N'M.2 — Per handlowiec: obrót miesięczny 18 mies. wstecz' AS [Raport];

SELECT RokMiesiac,
       CAST(SUM(CASE WHEN Handlowiec = N'Maja' THEN WartoscNetto ELSE 0 END) AS DECIMAL(18,2)) AS Maja,
       CAST(SUM(CASE WHEN Handlowiec = N'Jola' THEN WartoscNetto ELSE 0 END) AS DECIMAL(18,2)) AS Jola,
       CAST(SUM(CASE WHEN Handlowiec = N'Teresa' THEN WartoscNetto ELSE 0 END) AS DECIMAL(18,2)) AS Teresa,
       CAST(SUM(CASE WHEN Handlowiec = N'Ania' THEN WartoscNetto ELSE 0 END) AS DECIMAL(18,2)) AS Ania,
       CAST(SUM(CASE WHEN Handlowiec NOT IN (N'Maja',N'Jola',N'Teresa',N'Ania',
                                              N'Nieprzypisany',N'Ogolne',N'Ogólne')
                     THEN WartoscNetto ELSE 0 END) AS DECIMAL(18,2)) AS Inni,
       CAST(SUM(WartoscNetto) AS DECIMAL(18,2)) AS Total
FROM #FaktBaza
GROUP BY RokMiesiac
ORDER BY RokMiesiac;

SELECT N'M.3 — TOP 5 klientów Mai: miesięczny szereg czasowy (18 mies.)' AS [Raport];

WITH Top5 AS (
    SELECT TOP 5 KontrahentId, MAX(KontrahentNazwa) AS Nazwa
    FROM #FaktBaza
    WHERE DataFaktury BETWEEN @DataMaja AND @DataDo AND Handlowiec = @HandlowiecMaja
    GROUP BY KontrahentId
    ORDER BY SUM(WartoscNetto) DESC
)
SELECT f.RokMiesiac, t.Nazwa,
       CAST(SUM(f.WartoscNetto) AS DECIMAL(18,2)) AS Netto,
       CAST(SUM(f.Kg) AS DECIMAL(18,1)) AS Kg,
       CASE WHEN f.DataFaktury < @DataMaja THEN N'Daniel' ELSE N'Maja' END AS Era
FROM #FaktBaza f INNER JOIN Top5 t ON t.KontrahentId = f.KontrahentId
GROUP BY f.RokMiesiac, t.Nazwa, CASE WHEN f.DataFaktury < @DataMaja THEN N'Daniel' ELSE N'Maja' END
ORDER BY t.Nazwa, f.RokMiesiac;

/* ===========================================================================
   ===  N. CYKL ŻYCIA KLIENTÓW  ===============================================
   =========================================================================== */
SELECT N'N.1 — Lejek klientów Mai: pierwsza/ostatnia faktura, status, "age"' AS [Raport];

WITH WszyscyKlienci AS (
    SELECT DISTINCT KontrahentId FROM #FaktBaza
    WHERE DataFaktury BETWEEN @DataMaja AND @DataDo AND Handlowiec = @HandlowiecMaja
)
SELECT f.KontrahentNazwa,
       MIN(f.DataFaktury)                            AS PierwszaKiedykolwiek,
       MAX(f.DataFaktury)                            AS OstatniaKiedykolwiek,
       MIN(CASE WHEN f.DataFaktury >= @DataMaja THEN f.DataFaktury END) AS Pierwsza_Pod_Maja,
       MAX(CASE WHEN f.DataFaktury < @DataMaja THEN f.DataFaktury END)  AS Ostatnia_Pod_Daniel,
       CAST(SUM(CASE WHEN f.DataFaktury < @DataMaja THEN f.WartoscNetto ELSE 0 END) AS DECIMAL(18,2)) AS Total_Daniel,
       CAST(SUM(CASE WHEN f.DataFaktury >= @DataMaja THEN f.WartoscNetto ELSE 0 END) AS DECIMAL(18,2)) AS Total_Maja,
       CAST(SUM(f.WartoscNetto) AS DECIMAL(18,2))    AS Total_Razem,
       DATEDIFF(DAY, MAX(f.DataFaktury), @DataDo)    AS DniOdOstatniej,
       DATEDIFF(MONTH, MIN(f.DataFaktury), @DataDo)  AS WiekKlienta_Mies,
       CASE WHEN DATEDIFF(DAY, MAX(f.DataFaktury), @DataDo) > 90  THEN N'🔴 90+ dni'
            WHEN DATEDIFF(DAY, MAX(f.DataFaktury), @DataDo) > 60  THEN N'🟠 60-90 dni'
            WHEN DATEDIFF(DAY, MAX(f.DataFaktury), @DataDo) > 30  THEN N'🟡 30-60 dni'
            ELSE N'🟢 aktywny' END                    AS Sygnal
FROM #FaktBaza f
WHERE f.KontrahentId IN (SELECT KontrahentId FROM WszyscyKlienci)
GROUP BY f.KontrahentId, f.KontrahentNazwa
ORDER BY Total_Razem DESC;

SELECT N'N.2 — Akwizycja nowych klientów Mai per miesiąc (pierwsza faktura w okresie)' AS [Raport];

WITH PierwszeFaktury AS (
    SELECT KontrahentId, MAX(KontrahentNazwa) AS Nazwa, MIN(DataFaktury) AS PierwszaF
    FROM #FaktBaza
    WHERE Handlowiec = @HandlowiecMaja
    GROUP BY KontrahentId
)
SELECT CONVERT(CHAR(7), PierwszaF, 120) AS Miesiac_Akwizycji,
       COUNT(*) AS LiczbaNowychKlientow,
       STRING_AGG(Nazwa, N' | ') WITHIN GROUP (ORDER BY Nazwa) AS Klienci
FROM PierwszeFaktury
WHERE PierwszaF >= @DataMaja
GROUP BY CONVERT(CHAR(7), PierwszaF, 120)
ORDER BY Miesiac_Akwizycji;

SELECT N'N.3 — Wpływ akwizycji nowych klientów: wartość obrotu generowana przez kohortę' AS [Raport];

WITH PierwszeFaktury AS (
    SELECT KontrahentId, MAX(KontrahentNazwa) AS Nazwa, MIN(DataFaktury) AS PierwszaF
    FROM #FaktBaza WHERE Handlowiec = @HandlowiecMaja
    GROUP BY KontrahentId
)
SELECT CONVERT(CHAR(7), pf.PierwszaF, 120) AS Kohorta_Miesiac,
       COUNT(DISTINCT pf.KontrahentId)     AS LiczbaKlientow,
       CAST(SUM(f.WartoscNetto) AS DECIMAL(18,2)) AS Total_Netto_Kohorta,
       CAST(SUM(f.WartoscNetto) / COUNT(DISTINCT pf.KontrahentId) AS DECIMAL(18,2)) AS Sredni_Netto_Per_Klient
FROM PierwszeFaktury pf
INNER JOIN #FaktBaza f ON f.KontrahentId = pf.KontrahentId
                      AND f.DataFaktury BETWEEN @DataMaja AND @DataDo
                      AND f.Handlowiec = @HandlowiecMaja
WHERE pf.PierwszaF >= @DataMaja
GROUP BY CONVERT(CHAR(7), pf.PierwszaF, 120)
ORDER BY Kohorta_Miesiac;

/* ===========================================================================
   ===  O. GEOGRAFIA / EKSPORT  ===============================================
   =========================================================================== */
SELECT N'O.1 — Eksport vs krajowy mix klientów Mai' AS [Raport];

WITH Klasyfikacja AS (
    SELECT KontrahentId, MAX(KontrahentNazwa) AS Nazwa,
           CASE
             WHEN UPPER(MAX(KontrahentNazwa)) LIKE N'%ESTONIA%'   THEN N'Estonia'
             WHEN UPPER(MAX(KontrahentNazwa)) LIKE N'%BELGIA%'    THEN N'Belgia'
             WHEN UPPER(MAX(KontrahentNazwa)) LIKE N'%HOLANDIA%' OR
                  UPPER(MAX(KontrahentNazwa)) LIKE N'%NIDERLAN%' OR
                  UPPER(MAX(KontrahentNazwa)) LIKE N'%B.V.%'      THEN N'Holandia'
             WHEN UPPER(MAX(KontrahentNazwa)) LIKE N'%SZWEC%' OR
                  UPPER(MAX(KontrahentNazwa)) LIKE N'%AB %' OR
                  UPPER(MAX(KontrahentNazwa)) LIKE N'% AB'        THEN N'Szwecja'
             WHEN UPPER(MAX(KontrahentNazwa)) LIKE N'%DANIA%' OR
                  UPPER(MAX(KontrahentNazwa)) LIKE N'%A/S%'       THEN N'Dania'
             WHEN UPPER(MAX(KontrahentNazwa)) LIKE N'%RUMUN%' OR
                  UPPER(MAX(KontrahentNazwa)) LIKE N'%SRL%'       THEN N'Rumunia'
             WHEN UPPER(MAX(KontrahentNazwa)) LIKE N'%OU %' OR
                  UPPER(MAX(KontrahentNazwa)) LIKE N'%EOOD%'      THEN N'Bałkany/Bałtyk'
             WHEN UPPER(MAX(KontrahentNazwa)) LIKE N'%GMBH%' OR
                  UPPER(MAX(KontrahentNazwa)) LIKE N'%NIEMC%'     THEN N'Niemcy'
             WHEN UPPER(MAX(KontrahentNazwa)) LIKE N'%SP. Z O.O.%' OR
                  UPPER(MAX(KontrahentNazwa)) LIKE N'%SPÓŁKA%'  OR
                  UPPER(MAX(KontrahentNazwa)) LIKE N'%SPOLKA%'  OR
                  UPPER(MAX(KontrahentNazwa)) LIKE N'%SP.%'      THEN N'Polska'
             ELSE N'Polska (default)' END AS Kraj,
           SUM(WartoscNetto) AS Netto, SUM(Kg) AS Kg
    FROM #FaktBaza
    WHERE DataFaktury BETWEEN @DataMaja AND @DataDo AND Handlowiec = @HandlowiecMaja
    GROUP BY KontrahentId
)
SELECT Kraj,
       COUNT(*) AS LiczbaKlientow,
       CAST(SUM(Netto) AS DECIMAL(18,2)) AS Netto,
       CAST(SUM(Kg) AS DECIMAL(18,1)) AS Kg,
       CAST(SUM(Netto) / NULLIF(SUM(Kg),0) AS DECIMAL(10,2)) AS SredniaCenaZlKg,
       CAST(SUM(Netto) * 100.0 / SUM(SUM(Netto)) OVER () AS DECIMAL(6,2)) AS Udzial_Proc
FROM Klasyfikacja
GROUP BY Kraj
ORDER BY Netto DESC;

SELECT N'O.2 — Maja jako eksporter: czy zagraniczni klienci rosną' AS [Raport];

WITH Klienci AS (
    SELECT DISTINCT KontrahentId, KontrahentNazwa
    FROM #FaktBaza WHERE DataFaktury BETWEEN @DataMaja AND @DataDo AND Handlowiec = @HandlowiecMaja
),
Eksport AS (
    SELECT KontrahentId FROM Klienci
    WHERE UPPER(KontrahentNazwa) LIKE N'%ESTONIA%'
       OR UPPER(KontrahentNazwa) LIKE N'%BELGIA%'
       OR UPPER(KontrahentNazwa) LIKE N'%HOLANDIA%' OR UPPER(KontrahentNazwa) LIKE N'%NIDERLAN%'
       OR UPPER(KontrahentNazwa) LIKE N'%SZWEC%'    OR UPPER(KontrahentNazwa) LIKE N'%DANIA%'
       OR UPPER(KontrahentNazwa) LIKE N'%RUMUN%'    OR UPPER(KontrahentNazwa) LIKE N'%B.V.%'
       OR UPPER(KontrahentNazwa) LIKE N'%A/S%'      OR UPPER(KontrahentNazwa) LIKE N'%OU %'
       OR UPPER(KontrahentNazwa) LIKE N'%EOOD%'     OR UPPER(KontrahentNazwa) LIKE N'%SRL%'
),
FFlag AS (
    SELECT f.RokMiesiac, f.WartoscNetto,
           CASE WHEN e.KontrahentId IS NOT NULL THEN 1 ELSE 0 END AS JestEksport
    FROM #FaktBaza f
    LEFT JOIN Eksport e ON e.KontrahentId = f.KontrahentId
    WHERE f.DataFaktury BETWEEN @DataMaja AND @DataDo AND f.Handlowiec = @HandlowiecMaja
)
SELECT RokMiesiac,
       CAST(SUM(CASE WHEN JestEksport = 1 THEN WartoscNetto ELSE 0 END) AS DECIMAL(18,2)) AS Eksport_Netto,
       CAST(SUM(CASE WHEN JestEksport = 0 THEN WartoscNetto ELSE 0 END) AS DECIMAL(18,2)) AS Krajowy_Netto,
       CAST(SUM(CASE WHEN JestEksport = 1 THEN WartoscNetto ELSE 0 END) * 100.0
            / NULLIF(SUM(WartoscNetto), 0) AS DECIMAL(6,2)) AS Eksport_Udzial_Proc
FROM FFlag
GROUP BY RokMiesiac
ORDER BY RokMiesiac;

SELECT N'O.3 — Eksport: Maja vs inni handlowcy' AS [Raport];

WITH KlasaFirmy AS (
    SELECT KontrahentId, MAX(KontrahentNazwa) AS Nazwa,
           CASE WHEN UPPER(MAX(KontrahentNazwa)) LIKE N'%ESTONIA%' OR
                     UPPER(MAX(KontrahentNazwa)) LIKE N'%BELGIA%' OR
                     UPPER(MAX(KontrahentNazwa)) LIKE N'%HOLANDIA%' OR UPPER(MAX(KontrahentNazwa)) LIKE N'%NIDERLAN%' OR
                     UPPER(MAX(KontrahentNazwa)) LIKE N'%SZWEC%' OR UPPER(MAX(KontrahentNazwa)) LIKE N'%DANIA%' OR
                     UPPER(MAX(KontrahentNazwa)) LIKE N'%RUMUN%' OR UPPER(MAX(KontrahentNazwa)) LIKE N'%B.V.%' OR
                     UPPER(MAX(KontrahentNazwa)) LIKE N'%A/S%' OR UPPER(MAX(KontrahentNazwa)) LIKE N'%OU %' OR
                     UPPER(MAX(KontrahentNazwa)) LIKE N'%EOOD%' OR UPPER(MAX(KontrahentNazwa)) LIKE N'%SRL%'
                THEN 1 ELSE 0 END AS JestEksport
    FROM #FaktBaza GROUP BY KontrahentId
)
SELECT f.Handlowiec,
       CAST(SUM(CASE WHEN k.JestEksport = 1 THEN f.WartoscNetto ELSE 0 END) AS DECIMAL(18,2)) AS Eksport_Netto,
       CAST(SUM(CASE WHEN k.JestEksport = 0 THEN f.WartoscNetto ELSE 0 END) AS DECIMAL(18,2)) AS Krajowy_Netto,
       CAST(SUM(CASE WHEN k.JestEksport = 1 THEN f.WartoscNetto ELSE 0 END) * 100.0
            / NULLIF(SUM(f.WartoscNetto), 0) AS DECIMAL(6,2)) AS Eksport_Udzial_Proc,
       COUNT(DISTINCT CASE WHEN k.JestEksport = 1 THEN f.KontrahentId END) AS KlientowEksport,
       COUNT(DISTINCT CASE WHEN k.JestEksport = 0 THEN f.KontrahentId END) AS KlientowKrajowych
FROM #FaktBaza f INNER JOIN KlasaFirmy k ON k.KontrahentId = f.KontrahentId
WHERE f.DataFaktury BETWEEN @DataMaja AND @DataDo
  AND f.Handlowiec NOT IN (N'Nieprzypisany', N'Ogolne', N'Ogólne')
GROUP BY f.Handlowiec
ORDER BY Eksport_Netto DESC;

/* ===========================================================================
   ===  P. MARŻA PER KATEGORIA  ===============================================
   =========================================================================== */
SELECT N'P.1 — Marża per kategoria towaru: Maja vs benchmark' AS [Raport];

WITH M AS (
    SELECT Kategoria, TowarId, RokMiesiac, SUM(Kg) AS KgMaja, SUM(WartoscNetto) AS NettoMaja
    FROM #FaktBaza WHERE DataFaktury BETWEEN @DataMaja AND @DataDo AND Handlowiec = @HandlowiecMaja
    GROUP BY Kategoria, TowarId, RokMiesiac
),
I AS (
    SELECT TowarId, RokMiesiac, SUM(WartoscNetto) / NULLIF(SUM(Kg), 0) AS CenaInni
    FROM #FaktBaza WHERE DataFaktury BETWEEN @DataMaja AND @DataDo
      AND Handlowiec NOT IN (@HandlowiecMaja, N'Nieprzypisany', N'Ogolne', N'Ogólne')
    GROUP BY TowarId, RokMiesiac
)
SELECT m.Kategoria,
       CAST(SUM(m.KgMaja) AS DECIMAL(18,1)) AS Maja_Kg,
       CAST(SUM(m.NettoMaja) AS DECIMAL(18,2)) AS Maja_Netto,
       CAST(SUM(m.NettoMaja) / NULLIF(SUM(m.KgMaja), 0) AS DECIMAL(10,2)) AS Maja_SredniaCena,
       CAST(SUM(m.KgMaja * i.CenaInni) / NULLIF(SUM(m.KgMaja), 0) AS DECIMAL(10,2)) AS Benchmark_SredniaCena,
       CAST(SUM(m.NettoMaja - m.KgMaja * i.CenaInni) AS DECIMAL(18,2)) AS Marza_vs_Bench_Zl,
       CASE WHEN SUM(m.KgMaja * i.CenaInni) > 0
            THEN CAST(SUM(m.NettoMaja - m.KgMaja * i.CenaInni) * 100.0 / SUM(m.KgMaja * i.CenaInni) AS DECIMAL(8,2))
            ELSE NULL END AS Marza_Proc
FROM M m INNER JOIN I i ON i.TowarId = m.TowarId AND i.RokMiesiac = m.RokMiesiac
GROUP BY m.Kategoria
ORDER BY Marza_vs_Bench_Zl DESC;

SELECT N'P.2 — Cena świeże vs mrożone per handlowiec' AS [Raport];

SELECT Handlowiec, Kategoria,
       CAST(SUM(Kg) AS DECIMAL(18,1)) AS Kg,
       CAST(SUM(WartoscNetto) AS DECIMAL(18,2)) AS Netto,
       CAST(SUM(WartoscNetto) / NULLIF(SUM(Kg), 0) AS DECIMAL(10,2)) AS SredniaCena
FROM #FaktBaza
WHERE DataFaktury BETWEEN @DataMaja AND @DataDo
  AND Handlowiec NOT IN (N'Nieprzypisany', N'Ogolne', N'Ogólne')
GROUP BY Handlowiec, Kategoria
ORDER BY Handlowiec, Netto DESC;

/* ===========================================================================
   ===  R. SEZONOWOŚĆ / DNI TYGODNIA  =========================================
   =========================================================================== */
SELECT N'R.1 — Dzień tygodnia: kiedy Maja wystawia faktury (1=Nd, 2=Pn, ..., 7=Sb)' AS [Raport];

SELECT DzienTyg,
       CASE DzienTyg WHEN 1 THEN N'Niedziela' WHEN 2 THEN N'Poniedziałek'
                     WHEN 3 THEN N'Wtorek'     WHEN 4 THEN N'Środa'
                     WHEN 5 THEN N'Czwartek'   WHEN 6 THEN N'Piątek'
                     WHEN 7 THEN N'Sobota' END AS Dzien,
       COUNT(DISTINCT DKId) AS LiczbaFaktur,
       CAST(SUM(Kg) AS DECIMAL(18,1)) AS SumaKg,
       CAST(SUM(WartoscNetto) AS DECIMAL(18,2)) AS SumaNetto,
       CAST(SUM(WartoscNetto) / NULLIF(COUNT(DISTINCT DKId), 0) AS DECIMAL(18,2)) AS SredniaFaktura
FROM #FaktBaza
WHERE DataFaktury BETWEEN @DataMaja AND @DataDo AND Handlowiec = @HandlowiecMaja
GROUP BY DzienTyg
ORDER BY DzienTyg;

SELECT N'R.2 — Sezonowość Mai: miesiąc roku (czy są martwe okresy)' AS [Raport];

SELECT Miesiac,
       COUNT(DISTINCT DKId) AS LiczbaFaktur,
       CAST(SUM(Kg) AS DECIMAL(18,1)) AS SumaKg,
       CAST(SUM(WartoscNetto) AS DECIMAL(18,2)) AS SumaNetto,
       COUNT(DISTINCT KontrahentId) AS LiczbaKlientow,
       CAST(AVG(WartoscNetto) AS DECIMAL(18,2)) AS SredniaPozycja
FROM #FaktBaza
WHERE Handlowiec = @HandlowiecMaja
GROUP BY Miesiac
ORDER BY Miesiac;

SELECT N'R.3 — Liczba pozycji na fakturze Mai (czy faktury rosną w pojemności)' AS [Raport];

WITH FperZam AS (
    SELECT DKId, RokMiesiac, COUNT(*) AS LiczbaPozycji, SUM(WartoscNetto) AS Netto
    FROM #FaktBaza
    WHERE DataFaktury BETWEEN @DataMaja AND @DataDo AND Handlowiec = @HandlowiecMaja
    GROUP BY DKId, RokMiesiac
)
SELECT RokMiesiac,
       COUNT(*) AS LiczbaFaktur,
       CAST(AVG(CAST(LiczbaPozycji AS DECIMAL(10,2))) AS DECIMAL(8,2)) AS SredniaPozycjiNaFakture,
       MIN(LiczbaPozycji) AS Min, MAX(LiczbaPozycji) AS Max,
       CAST(AVG(Netto) AS DECIMAL(18,2)) AS SredniaWartoscFaktury
FROM FperZam
GROUP BY RokMiesiac
ORDER BY RokMiesiac;

/* ===========================================================================
   ===  S. STATYSTYKI ROZKŁADÓW  ==============================================
   =========================================================================== */
SELECT N'S.1 — Statystyki wartości faktur per handlowiec (era Mai)' AS [Raport];

WITH F AS (
    SELECT Handlowiec, DKId, SUM(WartoscNetto) AS NettoFak
    FROM #FaktBaza WHERE DataFaktury BETWEEN @DataMaja AND @DataDo
      AND Handlowiec NOT IN (N'Nieprzypisany', N'Ogolne', N'Ogólne')
    GROUP BY Handlowiec, DKId
),
Pct AS (
    SELECT DISTINCT Handlowiec,
           PERCENTILE_CONT(0.25) WITHIN GROUP (ORDER BY NettoFak) OVER (PARTITION BY Handlowiec) AS P25,
           PERCENTILE_CONT(0.50) WITHIN GROUP (ORDER BY NettoFak) OVER (PARTITION BY Handlowiec) AS Mediana,
           PERCENTILE_CONT(0.75) WITHIN GROUP (ORDER BY NettoFak) OVER (PARTITION BY Handlowiec) AS P75
    FROM F
),
Agg AS (
    SELECT Handlowiec, COUNT(*) AS LiczbaFaktur,
           AVG(NettoFak) AS Srednia, STDEV(NettoFak) AS OdchylenieStd,
           MIN(NettoFak) AS MinV, MAX(NettoFak) AS MaxV
    FROM F GROUP BY Handlowiec
)
SELECT a.Handlowiec, a.LiczbaFaktur,
       CAST(a.Srednia AS DECIMAL(18,2))        AS Srednia,
       CAST(a.OdchylenieStd AS DECIMAL(18,2))  AS OdchylenieStd,
       CAST(a.MinV AS DECIMAL(18,2))           AS MinV,
       CAST(p.P25 AS DECIMAL(18,2))            AS P25,
       CAST(p.Mediana AS DECIMAL(18,2))        AS Mediana,
       CAST(p.P75 AS DECIMAL(18,2))            AS P75,
       CAST(a.MaxV AS DECIMAL(18,2))           AS MaxV
FROM Agg a JOIN Pct p ON p.Handlowiec = a.Handlowiec
ORDER BY a.Srednia DESC;

SELECT N'S.2 — Distribution wartości faktur Mai (histogram per bucket)' AS [Raport];

WITH F AS (
    SELECT DKId, SUM(WartoscNetto) AS NettoFak
    FROM #FaktBaza WHERE DataFaktury BETWEEN @DataMaja AND @DataDo AND Handlowiec = @HandlowiecMaja
    GROUP BY DKId
)
SELECT
    CASE
        WHEN NettoFak <    1000 THEN N'01 < 1k'
        WHEN NettoFak <    5000 THEN N'02 1-5k'
        WHEN NettoFak <   10000 THEN N'03 5-10k'
        WHEN NettoFak <   25000 THEN N'04 10-25k'
        WHEN NettoFak <   50000 THEN N'05 25-50k'
        WHEN NettoFak <  100000 THEN N'06 50-100k'
        WHEN NettoFak <  250000 THEN N'07 100-250k'
        ELSE                          N'08 250k+'
    END AS Bucket,
    COUNT(*) AS LiczbaFaktur,
    CAST(SUM(NettoFak) AS DECIMAL(18,2)) AS Netto_W_Buckcie,
    CAST(AVG(NettoFak) AS DECIMAL(18,2)) AS SredniaFaktura
FROM F GROUP BY
    CASE
        WHEN NettoFak <    1000 THEN N'01 < 1k'
        WHEN NettoFak <    5000 THEN N'02 1-5k'
        WHEN NettoFak <   10000 THEN N'03 5-10k'
        WHEN NettoFak <   25000 THEN N'04 10-25k'
        WHEN NettoFak <   50000 THEN N'05 25-50k'
        WHEN NettoFak <  100000 THEN N'06 50-100k'
        WHEN NettoFak <  250000 THEN N'07 100-250k'
        ELSE                          N'08 250k+' END
ORDER BY Bucket;

/* ===========================================================================
   ===  T. FINAŁOWY KONTEKST  =================================================
   =========================================================================== */
SELECT N'T.1 — SUPER-SCORECARD: Maja vs reszta (najważniejsza tabela)' AS [Raport];

WITH Eokres AS (
    SELECT Handlowiec, KontrahentId, DKId, Kg, WartoscNetto
    FROM #FaktBaza WHERE DataFaktury BETWEEN @DataMaja AND @DataDo
      AND Handlowiec NOT IN (N'Nieprzypisany', N'Ogolne', N'Ogólne')
),
A AS (
    SELECT Handlowiec,
           COUNT(DISTINCT DKId) AS LFak, COUNT(DISTINCT KontrahentId) AS LKli,
           SUM(Kg) AS Kg, SUM(WartoscNetto) AS Netto,
           SUM(WartoscNetto) / NULLIF(SUM(Kg), 0) AS CenaSr
    FROM Eokres GROUP BY Handlowiec
),
P AS (SELECT Handlowiec, KontrahentId, SUM(WartoscNetto) AS N FROM Eokres GROUP BY Handlowiec, KontrahentId),
S AS (SELECT Handlowiec, SUM(N) AS Tot FROM P GROUP BY Handlowiec),
HHI AS (SELECT p.Handlowiec, SUM(POWER(p.N / NULLIF(s.Tot, 0), 2)) * 10000 AS HHI
        FROM P p JOIN S s ON s.Handlowiec = p.Handlowiec GROUP BY p.Handlowiec),
Cn AS (
    SELECT Handlowiec,
           SUM(CASE WHEN Kategoria = N'Świeże' THEN WartoscNetto ELSE 0 END) AS Swieze_N,
           SUM(CASE WHEN Kategoria = N'Mrożone' THEN WartoscNetto ELSE 0 END) AS Mrozone_N
    FROM #FaktBaza WHERE DataFaktury BETWEEN @DataMaja AND @DataDo
      AND Handlowiec NOT IN (N'Nieprzypisany', N'Ogolne', N'Ogólne')
    GROUP BY Handlowiec
),
KlasaFirmy AS (
    SELECT KontrahentId, MAX(KontrahentNazwa) AS Nazwa,
           CASE WHEN UPPER(MAX(KontrahentNazwa)) LIKE N'%ESTONIA%' OR
                     UPPER(MAX(KontrahentNazwa)) LIKE N'%BELGIA%' OR
                     UPPER(MAX(KontrahentNazwa)) LIKE N'%HOLANDIA%' OR UPPER(MAX(KontrahentNazwa)) LIKE N'%NIDERLAN%' OR
                     UPPER(MAX(KontrahentNazwa)) LIKE N'%SZWEC%' OR UPPER(MAX(KontrahentNazwa)) LIKE N'%DANIA%' OR
                     UPPER(MAX(KontrahentNazwa)) LIKE N'%RUMUN%' OR UPPER(MAX(KontrahentNazwa)) LIKE N'%B.V.%' OR
                     UPPER(MAX(KontrahentNazwa)) LIKE N'%A/S%' OR UPPER(MAX(KontrahentNazwa)) LIKE N'%OU %'
                THEN 1 ELSE 0 END AS JestEksport
    FROM #FaktBaza GROUP BY KontrahentId
),
Ex AS (
    SELECT f.Handlowiec,
           SUM(CASE WHEN k.JestEksport = 1 THEN f.WartoscNetto ELSE 0 END) AS EksportN,
           COUNT(DISTINCT CASE WHEN k.JestEksport = 1 THEN f.KontrahentId END) AS EksKlient
    FROM #FaktBaza f INNER JOIN KlasaFirmy k ON k.KontrahentId = f.KontrahentId
    WHERE f.DataFaktury BETWEEN @DataMaja AND @DataDo
      AND f.Handlowiec NOT IN (N'Nieprzypisany', N'Ogolne', N'Ogólne')
    GROUP BY f.Handlowiec
)
SELECT a.Handlowiec,
       a.LKli                                                 AS Klientow,
       a.LFak                                                 AS Faktur,
       CAST(a.Kg AS DECIMAL(18,1))                             AS SumaKg,
       CAST(a.Netto AS DECIMAL(18,2))                          AS SumaNetto,
       CAST(a.CenaSr AS DECIMAL(10,2))                         AS SredCenaZlKg,
       CAST(a.Netto / NULLIF(a.LFak, 0) AS DECIMAL(18,2))      AS SredFaktura,
       CAST(h.HHI AS DECIMAL(8,1))                             AS HHI,
       CAST(cn.Mrozone_N * 100.0 / NULLIF(a.Netto, 0) AS DECIMAL(6,2)) AS Mrozone_Proc,
       CAST(ex.EksportN * 100.0 / NULLIF(a.Netto, 0) AS DECIMAL(6,2))  AS Eksport_Proc,
       ex.EksKlient                                            AS EksportKlientow,
       CAST(a.Netto * 100.0 / SUM(a.Netto) OVER () AS DECIMAL(6,2)) AS Udzial_Firmy_Proc
FROM A a
LEFT JOIN HHI h ON h.Handlowiec = a.Handlowiec
LEFT JOIN Cn  cn ON cn.Handlowiec = a.Handlowiec
LEFT JOIN Ex  ex ON ex.Handlowiec = a.Handlowiec
ORDER BY CASE WHEN a.Handlowiec = @HandlowiecMaja THEN 0 ELSE 1 END, a.Netto DESC;

/* ###########################################################################
   ##  U-W: CO MAJA POWINNA ZROBIĆ ŻEBY ROZWINĄĆ SPRZEDAŻ
   ##  (lista konkretnych klientów do akcji + cross-sell + reaktywacja)
   ########################################################################### */

/* ===========================================================================
   ===  U. KLIENCI MAI DO REAKTYWACJI (śpiący 30-90 dni)  =====================
   =========================================================================== */
SELECT N'U.1 — Klienci Mai śpiący 30-90 dni (do telefonu w pierwszej kolejności)' AS [Raport];

WITH D AS (
    SELECT KontrahentId, MAX(KontrahentNazwa) AS Nazwa,
           MAX(DataFaktury) AS OstatniaF,
           SUM(WartoscNetto) AS NettoRazem,
           SUM(Kg) AS KgRazem,
           COUNT(DISTINCT DKId) AS Faktur
    FROM #FaktBaza
    WHERE DataFaktury BETWEEN @DataMaja AND @DataDo AND Handlowiec = @HandlowiecMaja
    GROUP BY KontrahentId
)
SELECT Nazwa, OstatniaF,
       DATEDIFF(DAY, OstatniaF, @DataDo) AS DniBezZakupu,
       CAST(NettoRazem AS DECIMAL(18,2)) AS Total_Netto_Era_Mai,
       CAST(KgRazem AS DECIMAL(18,1)) AS Total_Kg,
       Faktur,
       CASE WHEN DATEDIFF(DAY, OstatniaF, @DataDo) BETWEEN 30 AND 60 THEN N'🟡 SKONTAKTUJ SIĘ (30-60 dni)'
            WHEN DATEDIFF(DAY, OstatniaF, @DataDo) BETWEEN 61 AND 90 THEN N'🟠 PILNE (60-90 dni)'
            WHEN DATEDIFF(DAY, OstatniaF, @DataDo) > 90              THEN N'🔴 KRYTYCZNE (90+ dni)'
            ELSE N'🟢 aktywny' END AS Priorytet
FROM D
WHERE DATEDIFF(DAY, OstatniaF, @DataDo) > 30
ORDER BY NettoRazem DESC;

/* ===========================================================================
   ===  V. CROSS-SELL: klienci kupujący tylko 1-2 produkty  ===================
   =========================================================================== */
SELECT N'V.1 — Klienci Mai z niskim mixem produktowym (potencjał cross-sell)' AS [Raport];

WITH MixPerKlient AS (
    SELECT KontrahentId, MAX(KontrahentNazwa) AS Nazwa,
           COUNT(DISTINCT TowarId) AS RoznychTowarow,
           SUM(WartoscNetto) AS Netto,
           SUM(Kg) AS Kg,
           SUM(CASE WHEN Kategoria = N'Świeże'  THEN WartoscNetto ELSE 0 END) AS Netto_Swieze,
           SUM(CASE WHEN Kategoria = N'Mrożone' THEN WartoscNetto ELSE 0 END) AS Netto_Mrozone,
           SUM(CASE WHEN Kategoria = N'Mięso-inne' THEN WartoscNetto ELSE 0 END) AS Netto_Inne
    FROM #FaktBaza
    WHERE DataFaktury BETWEEN @DataMaja AND @DataDo AND Handlowiec = @HandlowiecMaja
    GROUP BY KontrahentId
)
SELECT Nazwa, RoznychTowarow,
       CAST(Netto AS DECIMAL(18,2)) AS Netto_Razem,
       CAST(Netto_Swieze AS DECIMAL(18,2))  AS Netto_Swieze,
       CAST(Netto_Mrozone AS DECIMAL(18,2)) AS Netto_Mrozone,
       CAST(Netto_Inne AS DECIMAL(18,2))    AS Netto_Inne,
       CASE WHEN Netto_Swieze > 0 AND Netto_Mrozone = 0 THEN N'➕ DODAĆ MROŻONE (wyższa marża)'
            WHEN Netto_Mrozone > 0 AND Netto_Swieze = 0 THEN N'➕ DODAĆ ŚWIEŻE'
            WHEN RoznychTowarow <= 3 AND Netto > 50000   THEN N'➕ Niski mix przy dużym obrocie'
            ELSE N'OK' END AS Sugestia
FROM MixPerKlient
WHERE Netto > 20000  -- istotni klienci, nie drobni
ORDER BY Netto DESC;

SELECT N'V.2 — TOP towary Mai: które klienci kupują NAJWIĘCEJ a które najmniej' AS [Raport];

WITH TPK AS (
    SELECT TowarId, MAX(TowarNazwa) AS TowarNazwa, Kategoria,
           COUNT(DISTINCT KontrahentId) AS LiczbaKlientow,
           SUM(Kg) AS Kg, SUM(WartoscNetto) AS Netto
    FROM #FaktBaza
    WHERE DataFaktury BETWEEN @DataMaja AND @DataDo AND Handlowiec = @HandlowiecMaja
    GROUP BY TowarId, Kategoria
),
WszKli AS (SELECT COUNT(DISTINCT KontrahentId) AS TotalKli
           FROM #FaktBaza WHERE DataFaktury BETWEEN @DataMaja AND @DataDo AND Handlowiec = @HandlowiecMaja)
SELECT t.TowarNazwa, t.Kategoria, t.LiczbaKlientow,
       w.TotalKli AS WszystkichKlientowMai,
       CAST(t.LiczbaKlientow * 100.0 / NULLIF(w.TotalKli, 0) AS DECIMAL(6,2)) AS PenetracjaProc,
       CAST(t.Kg AS DECIMAL(18,1)) AS Kg,
       CAST(t.Netto AS DECIMAL(18,2)) AS Netto,
       CASE WHEN t.LiczbaKlientow * 100.0 / NULLIF(w.TotalKli, 0) < 30 AND t.Netto > 100000
            THEN N'🎯 NISKA PENETRACJA + DUŻY OBRÓT (cross-sell do reszty bazy)'
            ELSE N'' END AS Sugestia
FROM TPK t CROSS JOIN WszKli w
ORDER BY t.Netto DESC;

/* ===========================================================================
   ===  W. UPGRADE — klienci kupujący wartościowo (potencjał wzrostu)  ========
   =========================================================================== */
SELECT N'W.1 — Klienci o wzrostowym trendzie (rosną M-na-M)' AS [Raport];

WITH Mies AS (
    SELECT KontrahentId, MAX(KontrahentNazwa) AS Nazwa, RokMiesiac,
           SUM(WartoscNetto) AS Netto
    FROM #FaktBaza
    WHERE DataFaktury BETWEEN @DataMaja AND @DataDo AND Handlowiec = @HandlowiecMaja
    GROUP BY KontrahentId, RokMiesiac
),
Rank AS (
    SELECT KontrahentId, Nazwa, RokMiesiac, Netto,
           LAG(Netto) OVER (PARTITION BY KontrahentId ORDER BY RokMiesiac) AS NettoPrev,
           ROW_NUMBER() OVER (PARTITION BY KontrahentId ORDER BY RokMiesiac DESC) AS Recencja
    FROM Mies
),
Suma AS (
    SELECT KontrahentId, MAX(Nazwa) AS Nazwa,
           SUM(Netto) AS TotalNetto, COUNT(*) AS LiczbaAktywnychMies,
           SUM(CASE WHEN Netto > NettoPrev THEN 1 ELSE 0 END) AS MiesiecyWzrostu,
           SUM(CASE WHEN Netto < NettoPrev THEN 1 ELSE 0 END) AS MiesiecySpadku
    FROM Rank
    GROUP BY KontrahentId
)
SELECT Nazwa, TotalNetto AS Total_Netto_Maja, LiczbaAktywnychMies,
       MiesiecyWzrostu, MiesiecySpadku,
       CASE WHEN MiesiecyWzrostu > MiesiecySpadku AND LiczbaAktywnychMies >= 3
            THEN N'📈 ROSNĄCY (warto rozwijać)'
            WHEN MiesiecySpadku > MiesiecyWzrostu AND LiczbaAktywnychMies >= 3
            THEN N'📉 MALEJĄCY (interwencja!)'
            ELSE N'➡ STABILNY' END AS Trend
FROM Suma
WHERE TotalNetto > 50000
ORDER BY TotalNetto DESC;

SELECT N'W.2 — Klienci HANDEL którzy NIE są pod Mają ale są w jej "branży" (potencjał akwizycji)' AS [Raport];

-- Klienci z innych handlowców, których kategorie zakupów pasują do mixu Mai (eksport / krajowe średnie B2B)
WITH MajaKlienci AS (
    SELECT DISTINCT KontrahentId FROM #FaktBaza
    WHERE DataFaktury BETWEEN @DataMaja AND @DataDo AND Handlowiec = @HandlowiecMaja
)
SELECT TOP 30
       f.KontrahentNazwa,
       f.Handlowiec AS ObecnyHandlowiec,
       COUNT(DISTINCT f.DKId) AS Faktur,
       CAST(SUM(f.WartoscNetto) AS DECIMAL(18,2)) AS Netto_12m,
       CAST(SUM(f.Kg) AS DECIMAL(18,1)) AS Kg_12m,
       CAST(SUM(CASE WHEN f.Kategoria = N'Mrożone' THEN f.WartoscNetto ELSE 0 END) * 100.0
            / NULLIF(SUM(f.WartoscNetto), 0) AS DECIMAL(6,2)) AS Mrozone_Proc,
       CASE WHEN UPPER(f.KontrahentNazwa) LIKE N'%ESTONIA%' OR UPPER(f.KontrahentNazwa) LIKE N'%BELGIA%'
              OR UPPER(f.KontrahentNazwa) LIKE N'%HOLANDIA%' OR UPPER(f.KontrahentNazwa) LIKE N'%B.V.%'
              OR UPPER(f.KontrahentNazwa) LIKE N'%A/S%'    OR UPPER(f.KontrahentNazwa) LIKE N'%OU %'
              OR UPPER(f.KontrahentNazwa) LIKE N'%SRL%'    OR UPPER(f.KontrahentNazwa) LIKE N'%EOOD%'
              OR UPPER(f.KontrahentNazwa) LIKE N'%RUMUN%'  OR UPPER(f.KontrahentNazwa) LIKE N'%SZWEC%'
              OR UPPER(f.KontrahentNazwa) LIKE N'%DANIA%'
            THEN N'🌍 EKSPORTOWY' ELSE N'' END AS Kraj
FROM #FaktBaza f
WHERE f.DataFaktury BETWEEN @DataMaja AND @DataDo
  AND f.KontrahentId NOT IN (SELECT KontrahentId FROM MajaKlienci)
  AND f.Handlowiec NOT IN (N'Nieprzypisany', N'Ogolne', N'Ogólne')
GROUP BY f.KontrahentNazwa, f.Handlowiec
HAVING SUM(f.WartoscNetto) > 100000  -- istotni klienci
ORDER BY Netto_12m DESC;

/* ===========================================================================
   ===  X. PRZEWAGA CENOWA — gdzie Maja może podnieść ceny  ===================
   =========================================================================== */
SELECT N'X.1 — Towary gdzie Maja jest NAJTAŃSZA ze wszystkich handlowców (potencjał +0.20 zł/kg)' AS [Raport];

WITH PerH AS (
    SELECT TowarId, Handlowiec, SUM(Kg) AS Kg, SUM(WartoscNetto) AS Netto,
           SUM(WartoscNetto) / NULLIF(SUM(Kg), 0) AS Cena
    FROM #FaktBaza WHERE DataFaktury BETWEEN @DataMaja AND @DataDo
      AND Handlowiec NOT IN (N'Nieprzypisany', N'Ogolne', N'Ogólne')
    GROUP BY TowarId, Handlowiec
),
Maja AS (SELECT * FROM PerH WHERE Handlowiec = @HandlowiecMaja),
Inni AS (
    SELECT TowarId,
           MIN(Cena) AS MinCena, MAX(Cena) AS MaxCena, AVG(Cena) AS SrCena
    FROM PerH WHERE Handlowiec <> @HandlowiecMaja
    GROUP BY TowarId
)
SELECT TOP 20 (SELECT MAX(TowarNazwa) FROM #FaktBaza WHERE TowarId = m.TowarId) AS TowarNazwa,
       CAST(m.Kg AS DECIMAL(18,1)) AS Maja_Kg,
       CAST(m.Cena AS DECIMAL(10,2)) AS Maja_Cena,
       CAST(i.MinCena AS DECIMAL(10,2)) AS Min_Cena_Inni,
       CAST(i.SrCena AS DECIMAL(10,2))  AS Sr_Cena_Inni,
       CAST(i.MaxCena AS DECIMAL(10,2)) AS Max_Cena_Inni,
       CAST(i.SrCena - m.Cena AS DECIMAL(10,2)) AS Potencjal_Podwyzki_ZlKg,
       CAST((i.SrCena - m.Cena) * m.Kg AS DECIMAL(18,2)) AS Potencjal_Zysku_Zl
FROM Maja m INNER JOIN Inni i ON i.TowarId = m.TowarId
WHERE m.Cena < i.SrCena AND m.Kg > 500
ORDER BY (i.SrCena - m.Cena) * m.Kg DESC;

/* ===========================================================================
   ===  Y. KPI PROPOZYCJA — co Maja musi osiągnąć w nadchodzącym Q  ===========
   =========================================================================== */
SELECT N'Y.1 — Baseline (3 ostatnie miesiące) — punkt odniesienia do KPI' AS [Raport];

SELECT
    CAST(AVG(SumaKg) AS DECIMAL(18,1))                AS Avg_Kg_Mies,
    CAST(AVG(SumaNetto) AS DECIMAL(18,2))             AS Avg_Netto_Mies,
    CAST(AVG(CenaSr) AS DECIMAL(10,2))                AS Avg_Cena_ZlKg,
    CAST(AVG(LiczbaKlientow) AS DECIMAL(8,1))         AS Avg_LiczbaKlientow,
    CAST(AVG(LiczbaFaktur) AS DECIMAL(8,1))           AS Avg_LiczbaFaktur
FROM (
    SELECT CONVERT(CHAR(7), DataFaktury, 120) AS RokMies,
           SUM(Kg) AS SumaKg, SUM(WartoscNetto) AS SumaNetto,
           SUM(WartoscNetto) / NULLIF(SUM(Kg), 0) AS CenaSr,
           COUNT(DISTINCT KontrahentId) AS LiczbaKlientow,
           COUNT(DISTINCT DKId) AS LiczbaFaktur
    FROM #FaktBaza
    WHERE DataFaktury BETWEEN DATEADD(MONTH, -3, @DataDo) AND @DataDo
      AND Handlowiec = @HandlowiecMaja
    GROUP BY CONVERT(CHAR(7), DataFaktury, 120)
) m;

/* ---------------------------------------------------------------------------
   CLEANUP
   --------------------------------------------------------------------------- */
IF OBJECT_ID('tempdb..#FaktBaza') IS NOT NULL DROP TABLE #FaktBaza;
SET ANSI_WARNINGS ON;
