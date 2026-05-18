/* ============================================================================
   analiza_maja.sql  — Pełna ocena pracy handlowca "Maja" (od 2025-10-01 do dziś)
   ----------------------------------------------------------------------------
   Cel: dostarczyć TWARDE DANE do decyzji "podnieść Mai pensję czy puścić".
   Wynik: 11 wymiarów (A–K) jako osobne raporty, każdy = osobny resultset w SSMS.
   Każdy resultset można zapisać do CSV (Right-click → Save Results As → CSV)
   i wkleić do Claude w przeglądarce.

   ============================================================================
   ETAP 1 — RECONNAISSANCE (wyniki, do wglądu przed uruchomieniem)
   ============================================================================

   1) Jak handlowiec łączy się z fakturą?
   ---------------------------------------------------------------------------
   Faktury sprzedaży: [HANDEL].[HM].[DK] (nagłówki) + [HANDEL].[HM].[DP] (linie).
   UWAGA: faktury są w HM.DK/DP — NIE w HM.MG/MZ (które to dokumenty magazynowe).
   Powiązanie z handlowcem: DK.khid → STContractors.id → ContractorClassification.ElementId
                            → CDim_Handlowiec_Val (string, np. 'Maja')

   🚨 KRYTYCZNA PUŁAPKA: ContractorClassification trzyma STAN AKTUALNY, nie historyczny!
   Jeśli klient zostanie przepisany z innego handlowca na Maję (przejęcie zakupów
   żywca po Paulinie), wszystkie historyczne faktury tego klienta retroaktywnie
   "przejdą" pod Maję w tej analizie. To może zawyżyć jej liczby.

   ➜ Drugim, alternatywnym źródłem prawdy jest LibraNet.dbo.ZamowieniaMieso.Handlowiec
     (zapisywane PER zamówienie w momencie tworzenia, immutable do edycji ręcznej).
     Rozjazd między oboma źródłami pokażemy w sekcji D-pre + K (scorecard).

   2) Istniejące widoki, które już pokazują CZĘŚĆ tych danych
   ---------------------------------------------------------------------------
   Plik / okno                                                    | Co pokazuje
   ---------------------------------------------------------------+--------------------------------------------------
   HandlowiecDashboard/Views/HandlowiecDashboardWindow.xaml.cs    | 11 zakładek: sprzedaż miesięczna per handlowiec,
                                                                  | Top 15 odbiorców, udział %, analiza cen,
                                                                  | świeże vs mrożone, porównanie lat, trend,
                                                                  | opakowania, płatności (aging 1-7/8-14/15-21/21+)
   HandlowiecDashboard/Views/AnalizaCenHandlowcaWindow.xaml.cs    | Drill-down dla jednego handlowca (ceny per towar)
   HandlowiecDashboard/Views/KontrahentPlatnosciWindow.xaml.cs    | Płatności per kontrahent (saldo, aging)
   WidokFakturSprzedazy.cs                                        | Lista faktur z filtrem handlowca + reklamacje
   AnalizaTygodniowaForm.cs                                       | Analiza tygodniowa cen per kontrahent
                                                                  | ("Okazja!" / "Drogo" vs średnia rynku)
   HandlowiecDashboard/DiagnostykaDashboard.sql                   | Statystyki ZamowieniaMieso per handlowiec

   ⚠ NIE pokazują (luki które ten SQL wypełnia):
     • Lifecycle klientów (nowi / przejęci / utraceni biznesowo)
     • HHI koncentracja portfela (czy Maja siedzi na 1-2 klientach)
     • Częstotliwość zakupów per klient (regularnie kupował → przestał)
     • % reklamacji per handlowiec w stosunku do liczby faktur
     • Marża cenowa Mai vs benchmark w SUMIE PLN (manco/zysk vs średnia firmy)
     • Scorecard 1-do-1 wszystkich handlowców obok siebie

   3) Identyfikator Mai w bazie
   ---------------------------------------------------------------------------
   Wartość ContractorClassification.CDim_Handlowiec_Val ≈ 'Maja' (string, bez nazwiska).
   ➜ Skrypt 0.0 poniżej WYŚWIETLA dokładną listę kandydatów — sprawdź wynik PRZED
     interpretacją wszystkich kolejnych SELECT-ów. Jeśli baza zawiera 'Maja Kowalska'
     albo 'M.' — zmień stałą @HandlowiecMaja u góry.

   4) Założenia / TODO (rzeczy, których NIE zweryfikowałem)
   ---------------------------------------------------------------------------
   • Skrypt zakłada że HM.DK zawiera tylko dokumenty sprzedaży (FVS) + korekty (FKS/FKSB/FWK).
     Korekty mają zwykle ujemne wartości i wchodzą do sumy → liczby są NETTO PO KOREKTACH.
     Jeśli chcesz analizować TYLKO sprzedaż surową, dodaj filtr po DK.seria lub DK.typ.
   • Linked server [192.168.0.112] do bazy Handel — założenie że JEST skonfigurowany
     na serwerze 192.168.0.109 (LibraNet). Jeśli nie ma — patrz README, instrukcja
     alternatywna z dwoma sesjami.
   • Kategorie HM.TW.katalog: 67095=świeże mięso, 67104=mięso inne, 67153=mrożone.
     Pomijam 67094 (odpady) — to nie typowa sprzedaż klientom B2B.
   • [CallReminderConfig] ma kolumnę Handlowiec? Nie zweryfikowałem — sekcja J
     ma fallback (zwróci pustkę jeśli brak kolumny).
   • Płatności: DK.walbrutto - SUM(PN.kwotarozl). DK.plattermin = termin płatności.
     PN to tabela rozliczeń (payments). Wzorzec skopiowany z HandlowiecDashboardWindow.

   ============================================================================
   ETAP 2 — PARAMETRY (edytuj przed uruchomieniem)
   ============================================================================ */

USE [LibraNet];  -- uruchom z LibraNet; HANDEL przez linked server [192.168.0.112]
GO

SET NOCOUNT ON;
SET ANSI_WARNINGS OFF;     -- bez ostrzeżeń "NULL value is eliminated by aggregate"

DECLARE @DataOd       DATE          = '2025-10-01';   -- start pracy Mai
DECLARE @DataDo       DATE          = CAST(GETDATE() AS DATE);
DECLARE @DataOdPrev   DATE          = '2025-07-01';   -- 3 mies. przed Mają (do trendów + lifecycle klientów)
DECLARE @HandlowiecMaja NVARCHAR(255) = N'Maja';      -- ⚠ POTWIERDŹ przez raport 0.0 poniżej

/* ============================================================================
   ETAP 3 — RAPORTY
   ============================================================================ */

/* ---------------------------------------------------------------------------
   0.0  KIM JEST "MAJA" W BAZIE — lista kandydatów (uruchom RAZ, potem usuń)
   --------------------------------------------------------------------------- */
SELECT N'0.0 — Kandydaci na Maję w ContractorClassification' AS [Raport];

SELECT TOP 30
       WYM.CDim_Handlowiec_Val               AS HandlowiecVal,
       COUNT(DISTINCT WYM.ElementId)         AS LiczbaKontrahentow,
       COUNT(DK.id)                          AS LiczbaFaktur,
       MIN(DK.data)                          AS PierwszaFaktura,
       MAX(DK.data)                          AS OstatniaFaktura
FROM [192.168.0.112].[Handel].[SSCommon].[ContractorClassification] WYM WITH (NOLOCK)
LEFT JOIN [192.168.0.112].[Handel].[HM].[DK] DK WITH (NOLOCK)
       ON DK.khid = WYM.ElementId
      AND DK.anulowany = 0
      AND DK.data >= DATEADD(YEAR, -1, GETDATE())
WHERE WYM.CDim_Handlowiec_Val IS NOT NULL
  AND WYM.CDim_Handlowiec_Val <> N''
  AND (WYM.CDim_Handlowiec_Val LIKE N'%aj%'   -- "Maja", "Majka", "Magda" itp.
    OR WYM.CDim_Handlowiec_Val LIKE N'M%')
GROUP BY WYM.CDim_Handlowiec_Val
ORDER BY LiczbaFaktur DESC;

SELECT N'0.0b — Kandydaci na Maję w LibraNet.ZamowieniaMieso.Handlowiec' AS [Raport];

SELECT TOP 30
       z.Handlowiec,
       COUNT(*)                  AS LiczbaZamowien,
       MIN(z.DataOdbioru)        AS PierwszeZamowienie,
       MAX(z.DataOdbioru)        AS OstatnieZamowienie
FROM dbo.ZamowieniaMieso z
WHERE z.Handlowiec LIKE N'M%' OR z.Handlowiec LIKE N'%aj%'
GROUP BY z.Handlowiec
ORDER BY LiczbaZamowien DESC;

/* ---------------------------------------------------------------------------
   0.1  BUDOWA #FaktBaza — wszystkie pozycje faktur w okresie + okres porównawczy
        Filtr: tylko mięso (kat. 67095, 67104, 67153), DK.anulowany = 0
   --------------------------------------------------------------------------- */
IF OBJECT_ID('tempdb..#FaktBaza') IS NOT NULL DROP TABLE #FaktBaza;

SELECT
    DK.id                                              AS DKId,
    DK.kod                                             AS NumerFaktury,
    DK.khid                                            AS KontrahentId,
    DK.data                                            AS DataFaktury,
    YEAR(DK.data)                                      AS Rok,
    MONTH(DK.data)                                     AS Miesiac,
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
    C.shortcut                                         AS KontrahentSkrot,
    ISNULL(C.nazwa1, C.shortcut)                       AS KontrahentNazwa,
    C.LimitAmount                                      AS LimitKredytowy
INTO #FaktBaza
FROM [192.168.0.112].[Handel].[HM].[DK] DK WITH (NOLOCK)
INNER JOIN [192.168.0.112].[Handel].[HM].[DP] DP WITH (NOLOCK)
        ON DK.id = DP.super
LEFT JOIN  [192.168.0.112].[Handel].[HM].[TW] TW WITH (NOLOCK)
        ON TW.id = DP.idtw
LEFT JOIN  [192.168.0.112].[Handel].[SSCommon].[ContractorClassification] WYM WITH (NOLOCK)
        ON DK.khid = WYM.ElementId
LEFT JOIN  [192.168.0.112].[Handel].[SSCommon].[STContractors] C WITH (NOLOCK)
        ON DK.khid = C.id
WHERE DK.anulowany = 0
  AND DK.data >= @DataOdPrev
  AND DK.data <  DATEADD(DAY, 1, @DataDo)
  AND TW.katalog IN (67095, 67104, 67153);

CREATE INDEX IX_FB_Handlowiec ON #FaktBaza(Handlowiec) INCLUDE (DataFaktury, KontrahentId, Kg, WartoscNetto);
CREATE INDEX IX_FB_Towar      ON #FaktBaza(TowarId, Rok, Miesiac) INCLUDE (Handlowiec, Kg, WartoscNetto);

/* ---------------------------------------------------------------------------
   0.2  BUDOWA #ZamBaza — zamówienia Mai z LibraNet
   --------------------------------------------------------------------------- */
IF OBJECT_ID('tempdb..#ZamBaza') IS NOT NULL DROP TABLE #ZamBaza;

SELECT
    z.ID                                  AS ZamowienieId,
    z.Handlowiec                          AS Handlowiec,
    z.OdbiorcaId                          AS OdbiorcaId,
    z.Odbiorca                            AS OdbiorcaNazwa,
    z.DataZamowienia                      AS DataZamowienia,
    z.DataOdbioru                         AS DataOdbioru,
    z.Status                              AS Status,
    ISNULL(z.Anulowane, 0)                AS Anulowane,
    z.TransportStatus                     AS TransportStatus,
    SUM(zt.Ilosc)                         AS SumaKg,
    SUM(zt.Ilosc * ISNULL(TRY_CAST(NULLIF(zt.Cena, N'') AS DECIMAL(18,2)), 0)) AS SumaWartosc,
    COUNT(zt.ZamowienieId)                AS LiczbaPozycji
INTO #ZamBaza
FROM dbo.ZamowieniaMieso z
LEFT JOIN dbo.ZamowieniaMiesoTowar zt ON zt.ZamowienieId = z.ID
WHERE z.DataOdbioru >= @DataOdPrev
  AND z.DataOdbioru <  DATEADD(DAY, 1, @DataDo)
GROUP BY z.ID, z.Handlowiec, z.OdbiorcaId, z.Odbiorca,
         z.DataZamowienia, z.DataOdbioru, z.Status, ISNULL(z.Anulowane, 0), z.TransportStatus;

CREATE INDEX IX_ZB_Handlowiec ON #ZamBaza(Handlowiec, DataOdbioru) INCLUDE (OdbiorcaId, SumaKg, SumaWartosc, Anulowane);

/* ===========================================================================
   ===  A. WOLUMEN I WARTOŚĆ — per miesiąc, per handlowiec  ===================
   =========================================================================== */
SELECT N'A.1 — Wolumen miesięczny Maja vs benchmark (4 inni handlowcy)' AS [Raport];

WITH PerHandlowiecMiesiac AS (
    SELECT Handlowiec, RokMiesiac, Rok, Miesiac,
           COUNT(DISTINCT DKId)               AS LiczbaFaktur,
           COUNT(DISTINCT KontrahentId)       AS LiczbaKlientow,
           SUM(Kg)                            AS SumaKg,
           SUM(WartoscNetto)                  AS SumaNetto,
           SUM(WartoscNetto) / NULLIF(SUM(Kg), 0) AS SredniaCenaPerKg
    FROM #FaktBaza
    WHERE DataFaktury BETWEEN @DataOd AND @DataDo
      AND Handlowiec NOT IN (N'Nieprzypisany', N'Ogolne', N'Ogólne')
    GROUP BY Handlowiec, RokMiesiac, Rok, Miesiac
)
SELECT Handlowiec, RokMiesiac, LiczbaFaktur, LiczbaKlientow,
       CAST(SumaKg AS DECIMAL(18,1))          AS SumaKg,
       CAST(SumaNetto AS DECIMAL(18,2))       AS SumaNetto,
       CAST(SredniaCenaPerKg AS DECIMAL(10,2)) AS SredniaCenaZlKg,
       CAST(SumaNetto / NULLIF(LiczbaFaktur, 0) AS DECIMAL(18,2)) AS SredniaWartoscFaktury,
       LAG(CAST(SumaNetto AS DECIMAL(18,2))) OVER (PARTITION BY Handlowiec ORDER BY RokMiesiac) AS NettoPoprzMies,
       CAST(SumaNetto - LAG(SumaNetto) OVER (PARTITION BY Handlowiec ORDER BY RokMiesiac) AS DECIMAL(18,2)) AS ZmianaZl,
       CASE WHEN LAG(SumaNetto) OVER (PARTITION BY Handlowiec ORDER BY RokMiesiac) > 0
            THEN CAST((SumaNetto - LAG(SumaNetto) OVER (PARTITION BY Handlowiec ORDER BY RokMiesiac))
                     / LAG(SumaNetto) OVER (PARTITION BY Handlowiec ORDER BY RokMiesiac) * 100 AS DECIMAL(8,1))
            ELSE NULL END AS ZmianaProc
FROM PerHandlowiecMiesiac
ORDER BY Handlowiec, RokMiesiac;

SELECT N'A.2 — Wolumen Maja vs Średnia firmy (wszyscy oprócz Mai) per miesiąc' AS [Raport];

WITH PerMiesiac AS (
    SELECT RokMiesiac,
           SUM(CASE WHEN Handlowiec = @HandlowiecMaja THEN Kg ELSE 0 END)          AS KgMaja,
           SUM(CASE WHEN Handlowiec = @HandlowiecMaja THEN WartoscNetto ELSE 0 END) AS NettoMaja,
           SUM(CASE WHEN Handlowiec NOT IN (@HandlowiecMaja, N'Nieprzypisany', N'Ogolne', N'Ogólne')
                    THEN Kg ELSE 0 END) AS KgInni,
           SUM(CASE WHEN Handlowiec NOT IN (@HandlowiecMaja, N'Nieprzypisany', N'Ogolne', N'Ogólne')
                    THEN WartoscNetto ELSE 0 END) AS NettoInni,
           COUNT(DISTINCT CASE WHEN Handlowiec NOT IN (@HandlowiecMaja, N'Nieprzypisany', N'Ogolne', N'Ogólne')
                          THEN Handlowiec END) AS LiczbaInnychHandlowcow
    FROM #FaktBaza
    WHERE DataFaktury BETWEEN @DataOd AND @DataDo
    GROUP BY RokMiesiac
)
SELECT RokMiesiac,
       CAST(KgMaja AS DECIMAL(18,1))                            AS Maja_Kg,
       CAST(NettoMaja AS DECIMAL(18,2))                         AS Maja_Netto,
       CAST(NettoMaja / NULLIF(KgMaja, 0) AS DECIMAL(10,2))     AS Maja_CenaZlKg,
       CAST(KgInni / NULLIF(LiczbaInnychHandlowcow, 0) AS DECIMAL(18,1))  AS Inni_KgSrednio,
       CAST(NettoInni / NULLIF(LiczbaInnychHandlowcow, 0) AS DECIMAL(18,2)) AS Inni_NettoSrednio,
       CAST(NettoInni / NULLIF(KgInni, 0) AS DECIMAL(10,2))     AS Inni_CenaZlKg,
       CAST(KgMaja * 100.0 / NULLIF(KgMaja + KgInni, 0) AS DECIMAL(6,2)) AS Maja_UdzialKg_Proc,
       CAST(NettoMaja * 100.0 / NULLIF(NettoMaja + NettoInni, 0) AS DECIMAL(6,2)) AS Maja_UdzialNetto_Proc
FROM PerMiesiac
ORDER BY RokMiesiac;

/* ===========================================================================
   ===  B. KLIENCI MAI — szczegóły + koncentracja portfela (HHI)  =============
   =========================================================================== */
SELECT N'B.1 — Lista klientów Mai (per kontrahent w okresie)' AS [Raport];

WITH KlMaja AS (
    SELECT KontrahentId, MAX(KontrahentNazwa) AS KontrahentNazwa,
           COUNT(DISTINCT DKId)              AS LiczbaFaktur,
           SUM(Kg)                           AS SumaKg,
           SUM(WartoscNetto)                 AS SumaNetto,
           SUM(WartoscNetto) / NULLIF(SUM(Kg), 0) AS SredniaCenaZlKg,
           MIN(DataFaktury)                  AS PierwszaFaktura,
           MAX(DataFaktury)                  AS OstatniaFaktura,
           COUNT(DISTINCT FORMAT(DataFaktury,'yyyy-MM')) AS LiczbaAktywnychMies
    FROM #FaktBaza
    WHERE DataFaktury BETWEEN @DataOd AND @DataDo
      AND Handlowiec = @HandlowiecMaja
    GROUP BY KontrahentId
),
Sumka AS (SELECT SUM(SumaNetto) AS TotalNetto FROM KlMaja)
SELECT TOP 100
       KontrahentNazwa,
       LiczbaFaktur,
       CAST(SumaKg AS DECIMAL(18,1))          AS SumaKg,
       CAST(SumaNetto AS DECIMAL(18,2))       AS SumaNetto,
       CAST(SredniaCenaZlKg AS DECIMAL(10,2)) AS SredniaCenaZlKg,
       PierwszaFaktura, OstatniaFaktura, LiczbaAktywnychMies,
       CAST(SumaNetto * 100.0 / NULLIF(s.TotalNetto, 0) AS DECIMAL(6,2)) AS UdzialNetto_Proc,
       DATEDIFF(DAY, OstatniaFaktura, @DataDo) AS DniOdOstatniej
FROM KlMaja CROSS JOIN Sumka s
ORDER BY SumaNetto DESC;

SELECT N'B.2 — Top 5 klientów Mai + udział łączny (koncentracja portfela)' AS [Raport];

WITH KlMaja AS (
    SELECT KontrahentId, MAX(KontrahentNazwa) AS KontrahentNazwa,
           SUM(WartoscNetto)                 AS SumaNetto
    FROM #FaktBaza
    WHERE DataFaktury BETWEEN @DataOd AND @DataDo
      AND Handlowiec = @HandlowiecMaja
    GROUP BY KontrahentId
),
Rangi AS (SELECT *, ROW_NUMBER() OVER (ORDER BY SumaNetto DESC) AS Pozycja,
                  SUM(SumaNetto) OVER ()                       AS Total FROM KlMaja)
SELECT Pozycja, KontrahentNazwa,
       CAST(SumaNetto AS DECIMAL(18,2)) AS SumaNetto,
       CAST(SumaNetto * 100.0 / NULLIF(Total, 0) AS DECIMAL(6,2)) AS UdzialNetto_Proc,
       CAST(SUM(SumaNetto) OVER (ORDER BY SumaNetto DESC) * 100.0 / NULLIF(Total, 0) AS DECIMAL(6,2))
                                                                AS Skumulowany_Proc
FROM Rangi
WHERE Pozycja <= 5
ORDER BY Pozycja;

SELECT N'B.3 — HHI (koncentracja portfela) wszystkich handlowców' AS [Raport];

-- HHI = Σ(udział_i)² × 10000 ; 0–1500 niska, 1500–2500 średnia, >2500 wysoka koncentracja
WITH PerHandlKlient AS (
    SELECT Handlowiec, KontrahentId, SUM(WartoscNetto) AS Netto
    FROM #FaktBaza
    WHERE DataFaktury BETWEEN @DataOd AND @DataDo
      AND Handlowiec NOT IN (N'Nieprzypisany', N'Ogolne', N'Ogólne')
    GROUP BY Handlowiec, KontrahentId
),
Sumy AS (SELECT Handlowiec, SUM(Netto) AS Total FROM PerHandlKlient GROUP BY Handlowiec),
Udzialy AS (
    SELECT p.Handlowiec, p.KontrahentId, p.Netto / NULLIF(s.Total, 0) AS Udzial
    FROM PerHandlKlient p JOIN Sumy s ON s.Handlowiec = p.Handlowiec
)
SELECT u.Handlowiec,
       COUNT(*)                                       AS LiczbaKlientow,
       CAST(s.Total AS DECIMAL(18,2))                 AS SumaNetto,
       CAST(SUM(Udzial * Udzial) * 10000 AS DECIMAL(8,1)) AS HHI,
       CASE WHEN SUM(Udzial * Udzial) * 10000 < 1500 THEN N'NISKA (zdrowy portfel)'
            WHEN SUM(Udzial * Udzial) * 10000 < 2500 THEN N'ŚREDNIA'
            ELSE N'WYSOKA (uzależnienie od 1-2 klientów)' END AS Ocena
FROM Udzialy u JOIN Sumy s ON s.Handlowiec = u.Handlowiec
GROUP BY u.Handlowiec, s.Total
ORDER BY HHI DESC;

/* ===========================================================================
   ===  C. NOWI / PRZEJĘCI / UTRACENI klienci  ================================
   =========================================================================== */
SELECT N'C.1 — Klienci Mai: NOWI vs PRZEJĘCI (kto kupował od kogo przed 2025-10)' AS [Raport];

WITH KlientPrzed AS (
    SELECT KontrahentId,
           MAX(KontrahentNazwa)                    AS KontrahentNazwa,
           SUM(WartoscNetto)                       AS NettoPrzed,
           -- handlowiec, do którego klient miał NAJWIĘCEJ faktur przed Mają:
           (SELECT TOP 1 Handlowiec FROM #FaktBaza f2
            WHERE f2.KontrahentId = f.KontrahentId
              AND f2.DataFaktury BETWEEN @DataOdPrev AND DATEADD(DAY, -1, @DataOd)
            GROUP BY Handlowiec ORDER BY SUM(WartoscNetto) DESC) AS PoprzedniHandlowiec
    FROM #FaktBaza f
    WHERE DataFaktury BETWEEN @DataOdPrev AND DATEADD(DAY, -1, @DataOd)
    GROUP BY KontrahentId
),
KlientPodMaja AS (
    SELECT KontrahentId, MAX(KontrahentNazwa) AS KontrahentNazwa,
           SUM(WartoscNetto) AS NettoMaja,
           MIN(DataFaktury)  AS PierwszaFakturaPodMaja
    FROM #FaktBaza
    WHERE DataFaktury BETWEEN @DataOd AND @DataDo
      AND Handlowiec = @HandlowiecMaja
    GROUP BY KontrahentId
)
SELECT
    CASE
        WHEN p.KontrahentId IS NULL                       THEN N'NOWY (nie kupował przed 2025-10)'
        WHEN p.PoprzedniHandlowiec = @HandlowiecMaja      THEN N'KONTYNUACJA (pod Mają już wcześniej)'
        WHEN p.PoprzedniHandlowiec IS NOT NULL            THEN N'PRZEJĘTY od: ' + p.PoprzedniHandlowiec
        ELSE N'NIEZNANE'
    END                                              AS Kategoria,
    m.KontrahentNazwa,
    p.PoprzedniHandlowiec,
    CAST(ISNULL(p.NettoPrzed, 0) AS DECIMAL(18,2))   AS NettoPrzedMaja,
    CAST(m.NettoMaja AS DECIMAL(18,2))               AS NettoPodMaja,
    m.PierwszaFakturaPodMaja
FROM KlientPodMaja m
LEFT JOIN KlientPrzed p ON p.KontrahentId = m.KontrahentId
ORDER BY m.NettoMaja DESC;

SELECT N'C.2 — Klienci UTRACENI: kupowali przed 2025-10, NIE kupują pod Mają' AS [Raport];

-- Klienci, którzy w okresie poprzednim mieli faktury z handlowcem = Maja (lub generalnie kupowali),
-- ale w erze Mai już nic nie kupili. Sygnał "utraconych biznesowo".
WITH PrzedRazem AS (
    SELECT KontrahentId, MAX(KontrahentNazwa) AS KontrahentNazwa,
           SUM(WartoscNetto) AS NettoPrzed,
           MAX(DataFaktury) AS OstatniaPrzed,
           (SELECT TOP 1 Handlowiec FROM #FaktBaza f2
            WHERE f2.KontrahentId = f.KontrahentId
              AND f2.DataFaktury BETWEEN @DataOdPrev AND DATEADD(DAY, -1, @DataOd)
            GROUP BY Handlowiec ORDER BY SUM(WartoscNetto) DESC) AS PoprzedniHandlowiec
    FROM #FaktBaza f
    WHERE DataFaktury BETWEEN @DataOdPrev AND DATEADD(DAY, -1, @DataOd)
    GROUP BY KontrahentId
),
PodMaja AS (
    SELECT DISTINCT KontrahentId
    FROM #FaktBaza
    WHERE DataFaktury BETWEEN @DataOd AND @DataDo
      AND Handlowiec = @HandlowiecMaja
)
SELECT p.KontrahentNazwa, p.PoprzedniHandlowiec,
       CAST(p.NettoPrzed AS DECIMAL(18,2)) AS NettoPrzed,
       p.OstatniaPrzed,
       DATEDIFF(DAY, p.OstatniaPrzed, @DataDo) AS DniBezZakupu,
       CASE WHEN p.PoprzedniHandlowiec = @HandlowiecMaja
            THEN N'UTRACONY BIZNESOWO (był u Mai, przestał)'
            ELSE N'KLIENT INNEGO HANDLOWCA (informacyjnie)'
       END AS Kategoria
FROM PrzedRazem p
WHERE p.KontrahentId NOT IN (SELECT KontrahentId FROM PodMaja)
  AND p.PoprzedniHandlowiec IS NOT NULL
ORDER BY p.NettoPrzed DESC;

/* ===========================================================================
   ===  D. CENY MAI vs BENCHMARK — KLUCZOWY WYMIAR  ===========================
   =========================================================================== */
SELECT N'D.1 — Średnia cena Mai vs średnia firmy per towar/miesiąc' AS [Raport];

WITH CenyMaja AS (
    SELECT TowarId, MAX(TowarNazwa) AS TowarNazwa, Kategoria, RokMiesiac,
           SUM(Kg)           AS KgMaja,
           SUM(WartoscNetto) AS NettoMaja,
           SUM(WartoscNetto) / NULLIF(SUM(Kg), 0) AS CenaMaja
    FROM #FaktBaza
    WHERE DataFaktury BETWEEN @DataOd AND @DataDo
      AND Handlowiec = @HandlowiecMaja
    GROUP BY TowarId, Kategoria, RokMiesiac
),
CenyInni AS (
    SELECT TowarId, RokMiesiac,
           SUM(Kg)                           AS KgInni,
           SUM(WartoscNetto)                 AS NettoInni,
           SUM(WartoscNetto) / NULLIF(SUM(Kg), 0) AS CenaInni
    FROM #FaktBaza
    WHERE DataFaktury BETWEEN @DataOd AND @DataDo
      AND Handlowiec NOT IN (@HandlowiecMaja, N'Nieprzypisany', N'Ogolne', N'Ogólne')
    GROUP BY TowarId, RokMiesiac
)
SELECT m.RokMiesiac, m.TowarNazwa, m.Kategoria,
       CAST(m.KgMaja AS DECIMAL(18,1))          AS Maja_Kg,
       CAST(m.CenaMaja AS DECIMAL(10,2))        AS Maja_CenaZlKg,
       CAST(i.CenaInni AS DECIMAL(10,2))        AS Inni_CenaZlKg,
       CAST(m.CenaMaja - i.CenaInni AS DECIMAL(10,2))                   AS RoznicaZlKg,
       CAST((m.CenaMaja - i.CenaInni) / NULLIF(i.CenaInni, 0) * 100 AS DECIMAL(8,2)) AS RoznicaProc,
       CAST((m.CenaMaja - i.CenaInni) * m.KgMaja AS DECIMAL(18,2)) AS Marza_vs_Benchmark_Zl
FROM CenyMaja m
LEFT JOIN CenyInni i ON i.TowarId = m.TowarId AND i.RokMiesiac = m.RokMiesiac
ORDER BY m.RokMiesiac, m.NettoMaja DESC;

SELECT N'D.2 — SUMA MARŻY MAI vs benchmark (gdyby sprzedawała po średniej firmy, zarobiłaby...)' AS [Raport];

WITH CenyMaja AS (
    SELECT TowarId, RokMiesiac, SUM(Kg) AS KgMaja, SUM(WartoscNetto) AS NettoMaja,
           SUM(WartoscNetto) / NULLIF(SUM(Kg), 0) AS CenaMaja
    FROM #FaktBaza
    WHERE DataFaktury BETWEEN @DataOd AND @DataDo AND Handlowiec = @HandlowiecMaja
    GROUP BY TowarId, RokMiesiac
),
CenyInni AS (
    SELECT TowarId, RokMiesiac, SUM(WartoscNetto) / NULLIF(SUM(Kg), 0) AS CenaInni
    FROM #FaktBaza
    WHERE DataFaktury BETWEEN @DataOd AND @DataDo
      AND Handlowiec NOT IN (@HandlowiecMaja, N'Nieprzypisany', N'Ogolne', N'Ogólne')
    GROUP BY TowarId, RokMiesiac
)
SELECT
    CAST(SUM(m.KgMaja) AS DECIMAL(18,1))                                AS Maja_KgRazem,
    CAST(SUM(m.NettoMaja) AS DECIMAL(18,2))                             AS Maja_NettoRazem,
    CAST(SUM(m.NettoMaja) / NULLIF(SUM(m.KgMaja), 0) AS DECIMAL(10,2))  AS Maja_SredniaCena,
    CAST(SUM(m.KgMaja * i.CenaInni) / NULLIF(SUM(m.KgMaja), 0) AS DECIMAL(10,2))
                                                                        AS Benchmark_SredniaCena,
    CAST(SUM(m.NettoMaja - m.KgMaja * i.CenaInni) AS DECIMAL(18,2))     AS Marza_Maja_vs_Benchmark_Zl,
    CASE WHEN SUM(m.KgMaja * i.CenaInni) > 0
         THEN CAST(SUM(m.NettoMaja - m.KgMaja * i.CenaInni) * 100.0 / SUM(m.KgMaja * i.CenaInni) AS DECIMAL(8,2))
         ELSE NULL END                                                  AS Marza_Proc
FROM CenyMaja m
INNER JOIN CenyInni i ON i.TowarId = m.TowarId AND i.RokMiesiac = m.RokMiesiac;

SELECT N'D.3 — TOP 10 pozycji, gdzie Maja TRACI MARŻĘ (sprzedaje taniej niż średnia)' AS [Raport];

WITH CenyM AS (
    SELECT TowarId, MAX(TowarNazwa) AS TowarNazwa,
           SUM(Kg) AS KgMaja, SUM(WartoscNetto) AS NettoMaja,
           SUM(WartoscNetto) / NULLIF(SUM(Kg), 0) AS CenaMaja
    FROM #FaktBaza
    WHERE DataFaktury BETWEEN @DataOd AND @DataDo AND Handlowiec = @HandlowiecMaja
    GROUP BY TowarId
),
CenyI AS (
    SELECT TowarId, SUM(WartoscNetto) / NULLIF(SUM(Kg), 0) AS CenaInni, SUM(Kg) AS KgInni
    FROM #FaktBaza
    WHERE DataFaktury BETWEEN @DataOd AND @DataDo
      AND Handlowiec NOT IN (@HandlowiecMaja, N'Nieprzypisany', N'Ogolne', N'Ogólne')
    GROUP BY TowarId
)
SELECT TOP 10
       m.TowarNazwa,
       CAST(m.KgMaja AS DECIMAL(18,1))                                       AS Maja_Kg,
       CAST(m.CenaMaja AS DECIMAL(10,2))                                     AS Maja_Cena,
       CAST(i.CenaInni AS DECIMAL(10,2))                                     AS Inni_Cena,
       CAST(m.CenaMaja - i.CenaInni AS DECIMAL(10,2))                        AS Roznica_Cena,
       CAST((m.CenaMaja - i.CenaInni) * m.KgMaja AS DECIMAL(18,2))           AS Strata_Marzy_Zl
FROM CenyM m INNER JOIN CenyI i ON i.TowarId = m.TowarId
WHERE i.KgInni > 100  -- benchmark istotny statystycznie
ORDER BY (m.CenaMaja - i.CenaInni) * m.KgMaja ASC;  -- najbardziej ujemna strata

SELECT N'D.4 — TOP 10 pozycji, gdzie Maja ZARABIA (sprzedaje drożej niż średnia)' AS [Raport];

WITH CenyM AS (
    SELECT TowarId, MAX(TowarNazwa) AS TowarNazwa, SUM(Kg) AS KgMaja,
           SUM(WartoscNetto) AS NettoMaja, SUM(WartoscNetto) / NULLIF(SUM(Kg), 0) AS CenaMaja
    FROM #FaktBaza
    WHERE DataFaktury BETWEEN @DataOd AND @DataDo AND Handlowiec = @HandlowiecMaja
    GROUP BY TowarId
),
CenyI AS (
    SELECT TowarId, SUM(WartoscNetto) / NULLIF(SUM(Kg), 0) AS CenaInni, SUM(Kg) AS KgInni
    FROM #FaktBaza
    WHERE DataFaktury BETWEEN @DataOd AND @DataDo
      AND Handlowiec NOT IN (@HandlowiecMaja, N'Nieprzypisany', N'Ogolne', N'Ogólne')
    GROUP BY TowarId
)
SELECT TOP 10
       m.TowarNazwa,
       CAST(m.KgMaja AS DECIMAL(18,1))                              AS Maja_Kg,
       CAST(m.CenaMaja AS DECIMAL(10,2))                            AS Maja_Cena,
       CAST(i.CenaInni AS DECIMAL(10,2))                            AS Inni_Cena,
       CAST(m.CenaMaja - i.CenaInni AS DECIMAL(10,2))               AS Roznica_Cena,
       CAST((m.CenaMaja - i.CenaInni) * m.KgMaja AS DECIMAL(18,2))  AS Zysk_Marzy_Zl
FROM CenyM m INNER JOIN CenyI i ON i.TowarId = m.TowarId
WHERE i.KgInni > 100
ORDER BY (m.CenaMaja - i.CenaInni) * m.KgMaja DESC;  -- najwięcej dodatnia

/* ===========================================================================
   ===  E. MIX PRODUKTOWY  ====================================================
   =========================================================================== */
SELECT N'E.1 — Top 20 towarów Mai (po wartości netto)' AS [Raport];

SELECT TOP 20
       TowarNazwa, Kategoria,
       CAST(SUM(Kg) AS DECIMAL(18,1))               AS SumaKg,
       CAST(SUM(WartoscNetto) AS DECIMAL(18,2))     AS SumaNetto,
       CAST(SUM(WartoscNetto) / NULLIF(SUM(Kg), 0) AS DECIMAL(10,2)) AS SredniaCenaZlKg,
       COUNT(DISTINCT KontrahentId)                 AS LiczbaKlientow,
       COUNT(DISTINCT DKId)                         AS LiczbaFaktur
FROM #FaktBaza
WHERE DataFaktury BETWEEN @DataOd AND @DataDo
  AND Handlowiec = @HandlowiecMaja
GROUP BY TowarId, TowarNazwa, Kategoria
ORDER BY SumaNetto DESC;

SELECT N'E.2 — Mix Maja vs Mix firmy (świeże / mrożone / inne)' AS [Raport];

WITH M AS (
    SELECT Handlowiec, Kategoria, SUM(Kg) AS Kg, SUM(WartoscNetto) AS Netto
    FROM #FaktBaza
    WHERE DataFaktury BETWEEN @DataOd AND @DataDo
      AND Handlowiec NOT IN (N'Nieprzypisany', N'Ogolne', N'Ogólne')
    GROUP BY Handlowiec, Kategoria
),
Sumy AS (SELECT Handlowiec, SUM(Netto) AS Total FROM M GROUP BY Handlowiec)
SELECT m.Handlowiec, m.Kategoria,
       CAST(m.Kg AS DECIMAL(18,1))                       AS Kg,
       CAST(m.Netto AS DECIMAL(18,2))                    AS Netto,
       CAST(m.Netto * 100.0 / NULLIF(s.Total, 0) AS DECIMAL(6,2)) AS UdzialNetto_Proc
FROM M m JOIN Sumy s ON s.Handlowiec = m.Handlowiec
ORDER BY m.Handlowiec, m.Netto DESC;

/* ===========================================================================
   ===  F. CZĘSTOTLIWOŚĆ ZAKUPÓW (signal "ucieka klient")  ====================
   =========================================================================== */
SELECT N'F.1 — Frekwencja zakupów per klient Mai' AS [Raport];

WITH DaneKlienta AS (
    SELECT KontrahentId, MAX(KontrahentNazwa) AS KontrahentNazwa,
           COUNT(DISTINCT CAST(DataFaktury AS DATE)) AS DniKupna,
           MIN(DataFaktury) AS PierwszaFaktura,
           MAX(DataFaktury) AS OstatniaFaktura,
           DATEDIFF(DAY, MIN(DataFaktury), MAX(DataFaktury)) AS RozpietoscDni,
           SUM(WartoscNetto) AS SumaNetto
    FROM #FaktBaza
    WHERE DataFaktury BETWEEN @DataOd AND @DataDo
      AND Handlowiec = @HandlowiecMaja
    GROUP BY KontrahentId
)
SELECT KontrahentNazwa, DniKupna, PierwszaFaktura, OstatniaFaktura, RozpietoscDni,
       CAST(SumaNetto AS DECIMAL(18,2)) AS SumaNetto,
       CAST(RozpietoscDni * 1.0 / NULLIF(DniKupna - 1, 0) AS DECIMAL(8,1)) AS SrednioDniMiedzyZakupami,
       DATEDIFF(DAY, OstatniaFaktura, @DataDo) AS DniOdOstatniejFaktury,
       CASE WHEN DATEDIFF(DAY, OstatniaFaktura, @DataDo) > 90 THEN N'🔴 90+ dni (utracony?)'
            WHEN DATEDIFF(DAY, OstatniaFaktura, @DataDo) > 60 THEN N'🟠 60-90 dni (ryzyko)'
            WHEN DATEDIFF(DAY, OstatniaFaktura, @DataDo) > 30 THEN N'🟡 30-60 dni (uważać)'
            ELSE N'🟢 aktywny' END AS Sygnal
FROM DaneKlienta
ORDER BY DniOdOstatniejFaktury DESC, SumaNetto DESC;

SELECT N'F.2 — Współczynnik aktywności bazy Mai per miesiąc (% klientów co kupowali w danym mies.)' AS [Raport];

WITH BazaMaja AS (
    SELECT DISTINCT KontrahentId
    FROM #FaktBaza
    WHERE DataFaktury BETWEEN @DataOd AND @DataDo AND Handlowiec = @HandlowiecMaja
),
AktywniMies AS (
    SELECT RokMiesiac, COUNT(DISTINCT KontrahentId) AS AktywnychKlientow
    FROM #FaktBaza
    WHERE DataFaktury BETWEEN @DataOd AND @DataDo AND Handlowiec = @HandlowiecMaja
    GROUP BY RokMiesiac
)
SELECT a.RokMiesiac, a.AktywnychKlientow,
       (SELECT COUNT(*) FROM BazaMaja) AS BazaRazem,
       CAST(a.AktywnychKlientow * 100.0 / NULLIF((SELECT COUNT(*) FROM BazaMaja), 0) AS DECIMAL(6,2)) AS AktywnoscProc
FROM AktywniMies a
ORDER BY a.RokMiesiac;

/* ===========================================================================
   ===  G. ZAMÓWIENIA vs REALIZACJA  ==========================================
   =========================================================================== */
SELECT N'G.1 — Zamówienia Mai per miesiąc (z LibraNet.ZamowieniaMieso)' AS [Raport];

SELECT FORMAT(DataOdbioru, 'yyyy-MM') AS RokMiesiac,
       COUNT(*)                                          AS LiczbaZamowien,
       SUM(CASE WHEN Anulowane = 1 THEN 1 ELSE 0 END)    AS LiczbaAnulowanych,
       CAST(SUM(SumaKg) AS DECIMAL(18,1))                AS SumaKg,
       CAST(SUM(SumaWartosc) AS DECIMAL(18,2))           AS SumaWartosc,
       CAST(SUM(SumaWartosc) / NULLIF(SUM(SumaKg), 0) AS DECIMAL(10,2)) AS SredniaCenaZlKg,
       CAST(SUM(CASE WHEN Anulowane = 1 THEN 1 ELSE 0 END) * 100.0 / NULLIF(COUNT(*), 0) AS DECIMAL(6,2))
                                                         AS AnulowanychProc
FROM #ZamBaza
WHERE Handlowiec = @HandlowiecMaja
  AND DataOdbioru BETWEEN @DataOd AND @DataDo
GROUP BY FORMAT(DataOdbioru, 'yyyy-MM')
ORDER BY RokMiesiac;

SELECT N'G.2 — Zamówienia per handlowiec (benchmark)' AS [Raport];

SELECT Handlowiec,
       COUNT(*)                                          AS LiczbaZamowien,
       SUM(CASE WHEN Anulowane = 1 THEN 1 ELSE 0 END)    AS Anulowanych,
       CAST(SUM(CASE WHEN Anulowane = 1 THEN 1 ELSE 0 END) * 100.0 / NULLIF(COUNT(*), 0) AS DECIMAL(6,2)) AS Anulow_Proc,
       CAST(SUM(SumaKg) AS DECIMAL(18,1))                AS SumaKg,
       CAST(SUM(SumaWartosc) AS DECIMAL(18,2))           AS SumaWartosc,
       COUNT(DISTINCT OdbiorcaId)                        AS LiczbaKlientow
FROM #ZamBaza
WHERE DataOdbioru BETWEEN @DataOd AND @DataDo
  AND Handlowiec NOT IN (N'', N'Nieprzypisany')
GROUP BY Handlowiec
ORDER BY SumaWartosc DESC;

SELECT N'G.3 — Średni czas: data zamówienia → data odbioru (Maja vs inni)' AS [Raport];

SELECT Handlowiec,
       COUNT(*)                                                                          AS LiczbaZam,
       CAST(AVG(CAST(DATEDIFF(DAY, DataZamowienia, DataOdbioru) AS DECIMAL(10,2))) AS DECIMAL(8,2))
                                                                                         AS SredniDniDoOdbioru,
       MIN(DATEDIFF(DAY, DataZamowienia, DataOdbioru))                                   AS Min_Dni,
       MAX(DATEDIFF(DAY, DataZamowienia, DataOdbioru))                                   AS Max_Dni
FROM #ZamBaza
WHERE DataOdbioru BETWEEN @DataOd AND @DataDo
  AND DataZamowienia IS NOT NULL
  AND Handlowiec NOT IN (N'', N'Nieprzypisany')
GROUP BY Handlowiec
ORDER BY SredniDniDoOdbioru;

/* ===========================================================================
   ===  H. REKLAMACJE  ========================================================
   =========================================================================== */
SELECT N'H.1 — Reklamacje klientów Mai (z Reklamacje + ContractorClassification)' AS [Raport];

WITH ReklMaja AS (
    SELECT r.Id, r.DataZgloszenia, r.NumerDokumentu, r.NazwaKontrahenta,
           r.TypReklamacji, r.Status, r.Priorytet,
           r.SumaKg, r.SumaWartosc, r.KosztReklamacji,
           r.DataZamkniecia,
           DATEDIFF(DAY, r.DataZgloszenia, ISNULL(r.DataZamkniecia, GETDATE())) AS DniRozpatrywania
    FROM dbo.Reklamacje r
    INNER JOIN [192.168.0.112].[Handel].[SSCommon].[ContractorClassification] WYM WITH (NOLOCK)
            ON WYM.ElementId = r.IdKontrahenta
    WHERE WYM.CDim_Handlowiec_Val = @HandlowiecMaja
      AND r.DataZgloszenia BETWEEN @DataOd AND @DataDo
)
SELECT * FROM ReklMaja
ORDER BY DataZgloszenia DESC;

SELECT N'H.2 — Reklamacje per handlowiec (benchmark, % w stosunku do faktur)' AS [Raport];

WITH ReklPerH AS (
    SELECT ISNULL(WYM.CDim_Handlowiec_Val, N'Nieprzypisany') AS Handlowiec,
           COUNT(r.Id)                                       AS LiczbaReklamacji,
           SUM(ISNULL(r.SumaWartosc, 0))                     AS WartoscReklamacji,
           SUM(ISNULL(r.KosztReklamacji, 0))                 AS KosztReklamacji,
           AVG(CAST(DATEDIFF(DAY, r.DataZgloszenia, ISNULL(r.DataZamkniecia, GETDATE())) AS DECIMAL(10,2)))
                                                             AS SredniDniRozpatrywania
    FROM dbo.Reklamacje r
    LEFT JOIN [192.168.0.112].[Handel].[SSCommon].[ContractorClassification] WYM WITH (NOLOCK)
           ON WYM.ElementId = r.IdKontrahenta
    WHERE r.DataZgloszenia BETWEEN @DataOd AND @DataDo
    GROUP BY ISNULL(WYM.CDim_Handlowiec_Val, N'Nieprzypisany')
),
FakturyPerH AS (
    SELECT Handlowiec, COUNT(DISTINCT DKId) AS LiczbaFaktur
    FROM #FaktBaza
    WHERE DataFaktury BETWEEN @DataOd AND @DataDo
      AND Handlowiec NOT IN (N'Nieprzypisany', N'Ogolne', N'Ogólne')
    GROUP BY Handlowiec
)
SELECT f.Handlowiec,
       f.LiczbaFaktur,
       ISNULL(r.LiczbaReklamacji, 0)                                  AS LiczbaReklamacji,
       CAST(ISNULL(r.LiczbaReklamacji, 0) * 100.0 / NULLIF(f.LiczbaFaktur, 0) AS DECIMAL(6,2))
                                                                      AS Reklamacji_na_100_Faktur,
       CAST(ISNULL(r.WartoscReklamacji, 0) AS DECIMAL(18,2))           AS WartoscReklamacji,
       CAST(ISNULL(r.KosztReklamacji, 0) AS DECIMAL(18,2))             AS KosztReklamacji,
       CAST(r.SredniDniRozpatrywania AS DECIMAL(8,2))                 AS SredniDniRozpatrywania
FROM FakturyPerH f
LEFT JOIN ReklPerH r ON r.Handlowiec = f.Handlowiec
ORDER BY Reklamacji_na_100_Faktur DESC;

/* ===========================================================================
   ===  I. PŁATNOŚCI KLIENTÓW MAI  ============================================
   =========================================================================== */
SELECT N'I.1 — Stan należności i przeterminowań klientów Mai' AS [Raport];

WITH PNAgg AS (
    SELECT PN.dkid, SUM(ISNULL(PN.kwotarozl, 0)) AS Rozliczone,
           MAX(PN.Termin)                         AS TerminPrawdziwy
    FROM [192.168.0.112].[Handel].[HM].[PN] PN WITH (NOLOCK)
    GROUP BY PN.dkid
),
SaldoFaktur AS (
    SELECT DK.id, DK.khid, DK.kod AS NumerFaktury,
           DK.walbrutto                                                AS Brutto,
           DK.walbrutto - ISNULL(PA.Rozliczone, 0)                     AS DoZaplaty,
           ISNULL(PA.TerminPrawdziwy, DK.plattermin)                   AS TerminPlatnosci,
           CASE WHEN DK.walbrutto - ISNULL(PA.Rozliczone, 0) > 0.01
                 AND GETDATE() > ISNULL(PA.TerminPrawdziwy, DK.plattermin)
                THEN DATEDIFF(DAY, ISNULL(PA.TerminPrawdziwy, DK.plattermin), GETDATE())
                ELSE 0 END                                             AS DniPrzeterminowania
    FROM [192.168.0.112].[Handel].[HM].[DK] DK WITH (NOLOCK)
    LEFT JOIN PNAgg PA ON PA.dkid = DK.id
    WHERE DK.anulowany = 0
      AND DK.walbrutto - ISNULL(PA.Rozliczone, 0) > 0.01
)
SELECT C.shortcut AS Kontrahent,
       WYM.CDim_Handlowiec_Val AS Handlowiec,
       ISNULL(C.LimitAmount, 0) AS LimitKredytu,
       CAST(SUM(S.DoZaplaty) AS DECIMAL(18,2))   AS DoZaplaty,
       CAST(SUM(CASE WHEN S.DniPrzeterminowania = 0 THEN S.DoZaplaty ELSE 0 END) AS DECIMAL(18,2)) AS Terminowe,
       CAST(SUM(CASE WHEN S.DniPrzeterminowania > 0 THEN S.DoZaplaty ELSE 0 END) AS DECIMAL(18,2)) AS Przeterminowane,
       MAX(S.DniPrzeterminowania)               AS MaxDniPrzeterminowania,
       COUNT(*)                                  AS LiczbaFaktur
FROM SaldoFaktur S
JOIN [192.168.0.112].[Handel].[SSCommon].[STContractors] C WITH (NOLOCK) ON C.id = S.khid
JOIN [192.168.0.112].[Handel].[SSCommon].[ContractorClassification] WYM WITH (NOLOCK) ON WYM.ElementId = S.khid
WHERE WYM.CDim_Handlowiec_Val = @HandlowiecMaja
GROUP BY C.shortcut, WYM.CDim_Handlowiec_Val, C.LimitAmount
ORDER BY Przeterminowane DESC, DoZaplaty DESC;

SELECT N'I.2 — Średnia jakość płatności klientów per handlowiec' AS [Raport];

WITH PNAgg AS (
    SELECT PN.dkid, SUM(ISNULL(PN.kwotarozl, 0)) AS Rozliczone, MAX(PN.Termin) AS TerminPrawdziwy
    FROM [192.168.0.112].[Handel].[HM].[PN] PN WITH (NOLOCK)
    GROUP BY PN.dkid
),
Saldo AS (
    SELECT DK.khid, DK.walbrutto - ISNULL(PA.Rozliczone, 0) AS DoZaplaty,
           CASE WHEN DK.walbrutto - ISNULL(PA.Rozliczone, 0) > 0.01
                 AND GETDATE() > ISNULL(PA.TerminPrawdziwy, DK.plattermin)
                THEN DATEDIFF(DAY, ISNULL(PA.TerminPrawdziwy, DK.plattermin), GETDATE())
                ELSE 0 END AS DniPrzeterm
    FROM [192.168.0.112].[Handel].[HM].[DK] DK WITH (NOLOCK)
    LEFT JOIN PNAgg PA ON PA.dkid = DK.id
    WHERE DK.anulowany = 0 AND DK.walbrutto - ISNULL(PA.Rozliczone, 0) > 0.01
)
SELECT ISNULL(WYM.CDim_Handlowiec_Val, N'Nieprzypisany') AS Handlowiec,
       COUNT(*)                                           AS LiczbaFakturOtwartych,
       CAST(SUM(S.DoZaplaty) AS DECIMAL(18,2))            AS NaleznosciRazem,
       CAST(SUM(CASE WHEN S.DniPrzeterm > 0 THEN S.DoZaplaty ELSE 0 END) AS DECIMAL(18,2))
                                                          AS Przeterminowane,
       CAST(SUM(CASE WHEN S.DniPrzeterm > 0 THEN S.DoZaplaty ELSE 0 END) * 100.0
            / NULLIF(SUM(S.DoZaplaty), 0) AS DECIMAL(6,2)) AS Przeterm_Proc,
       CAST(AVG(CAST(S.DniPrzeterm AS DECIMAL(10,2))) AS DECIMAL(8,2)) AS SredniaDniPrzeterminowania
FROM Saldo S
LEFT JOIN [192.168.0.112].[Handel].[SSCommon].[ContractorClassification] WYM WITH (NOLOCK)
       ON WYM.ElementId = S.khid
GROUP BY ISNULL(WYM.CDim_Handlowiec_Val, N'Nieprzypisany')
ORDER BY Przeterm_Proc DESC;

/* ===========================================================================
   ===  J. CRM / NOTATKI / KONTAKTY  ==========================================
   =========================================================================== */
SELECT N'J.1 — Aktywność notatek per handlowiec (NotatkiUzycia, jeśli używana)' AS [Raport];

-- NotatkiUzycia łączy się z UserHandlowcy via UserId; mapowanie user→handlowiec
IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'NotatkiUzycia')
BEGIN
    SELECT uh.HandlowiecName AS Handlowiec,
           COUNT(*)                                      AS LiczbaUzycNotatek,
           SUM(CASE WHEN nu.Typ = N'Wpisana' THEN 1 ELSE 0 END)    AS WpisanaRecznie,
           SUM(CASE WHEN nu.Typ = N'Wstawiona' THEN 1 ELSE 0 END)  AS Wstawiona,
           MIN(nu.Data)                                  AS PierwszeUzycie,
           MAX(nu.Data)                                  AS OstatnieUzycie
    FROM dbo.NotatkiUzycia nu
    INNER JOIN dbo.UserHandlowcy uh ON uh.UserID = nu.UserId
    WHERE nu.Data BETWEEN @DataOd AND @DataDo
    GROUP BY uh.HandlowiecName
    ORDER BY LiczbaUzycNotatek DESC;
END
ELSE
    SELECT N'⚠ Tabela NotatkiUzycia nie istnieje — pomijam' AS Info;

SELECT N'J.2 — Przypomnienia o kontaktach (CallReminderConfig, jeśli ma kolumnę Handlowiec)' AS [Raport];

-- Sprawdzenie: czy tabela ma kolumnę Handlowiec lub UserID
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
           WHERE TABLE_NAME = 'CallReminderConfig' AND COLUMN_NAME = 'UserID')
BEGIN
    SELECT TOP 50 c.*
    FROM dbo.CallReminderConfig c
    WHERE c.UserID IN (SELECT UserID FROM dbo.UserHandlowcy WHERE HandlowiecName = @HandlowiecMaja);
END
ELSE
    SELECT N'⚠ CallReminderConfig nie ma kolumny UserID lub nie istnieje — pomijam' AS Info;

/* ===========================================================================
   ===  K. SCORECARD — wszystkie metryki, wszyscy handlowcy, OBOK SIEBIE  =====
   =========================================================================== */
SELECT N'K.1 — SCORECARD: wszyscy handlowcy obok siebie (główny wynik dla Claude web)' AS [Raport];

WITH Fakt AS (
    SELECT Handlowiec, KontrahentId, DKId, Kg, WartoscNetto
    FROM #FaktBaza
    WHERE DataFaktury BETWEEN @DataOd AND @DataDo
      AND Handlowiec NOT IN (N'Nieprzypisany', N'Ogolne', N'Ogólne')
),
PerHKlient AS (
    SELECT Handlowiec, KontrahentId, SUM(WartoscNetto) AS Netto
    FROM Fakt GROUP BY Handlowiec, KontrahentId
),
SumyH AS (SELECT Handlowiec, SUM(Netto) AS Total FROM PerHKlient GROUP BY Handlowiec),
HHI AS (
    SELECT p.Handlowiec, SUM(POWER(p.Netto / NULLIF(s.Total, 0), 2)) * 10000 AS HHI
    FROM PerHKlient p JOIN SumyH s ON s.Handlowiec = p.Handlowiec
    GROUP BY p.Handlowiec
),
KlPrzed AS (   -- klienci aktywni w okresie przed Mają
    SELECT DISTINCT Handlowiec, KontrahentId
    FROM #FaktBaza
    WHERE DataFaktury BETWEEN @DataOdPrev AND DATEADD(DAY, -1, @DataOd)
),
KlTeraz AS (   -- klienci aktywni teraz per handlowiec
    SELECT DISTINCT Handlowiec, KontrahentId
    FROM Fakt
),
NowiKlienci AS (
    SELECT t.Handlowiec, COUNT(*) AS LiczbaNowych
    FROM KlTeraz t
    LEFT JOIN KlPrzed p ON p.Handlowiec = t.Handlowiec AND p.KontrahentId = t.KontrahentId
    WHERE p.KontrahentId IS NULL  -- nie kupował u tego samego handlowca przed
    GROUP BY t.Handlowiec
),
UtracKlienci AS (   -- był u handlowca, nie kupuje teraz
    SELECT p.Handlowiec, COUNT(*) AS LiczbaUtraconych
    FROM KlPrzed p
    LEFT JOIN KlTeraz t ON t.Handlowiec = p.Handlowiec AND t.KontrahentId = p.KontrahentId
    WHERE t.KontrahentId IS NULL
    GROUP BY p.Handlowiec
),
ReklH AS (
    SELECT ISNULL(WYM.CDim_Handlowiec_Val, N'Nieprzypisany') AS Handlowiec,
           COUNT(*) AS LiczbaReklamacji
    FROM dbo.Reklamacje r
    LEFT JOIN [192.168.0.112].[Handel].[SSCommon].[ContractorClassification] WYM WITH (NOLOCK)
           ON WYM.ElementId = r.IdKontrahenta
    WHERE r.DataZgloszenia BETWEEN @DataOd AND @DataDo
    GROUP BY ISNULL(WYM.CDim_Handlowiec_Val, N'Nieprzypisany')
),
PNAgg AS (
    SELECT PN.dkid, SUM(ISNULL(PN.kwotarozl, 0)) AS Rozliczone, MAX(PN.Termin) AS TerminPrawdziwy
    FROM [192.168.0.112].[Handel].[HM].[PN] PN WITH (NOLOCK) GROUP BY PN.dkid
),
SaldoH AS (
    SELECT ISNULL(WYM.CDim_Handlowiec_Val, N'Nieprzypisany') AS Handlowiec,
           SUM(CASE WHEN DK.walbrutto - ISNULL(PA.Rozliczone, 0) > 0.01
                     AND GETDATE() > ISNULL(PA.TerminPrawdziwy, DK.plattermin)
                    THEN DATEDIFF(DAY, ISNULL(PA.TerminPrawdziwy, DK.plattermin), GETDATE()) * (DK.walbrutto - ISNULL(PA.Rozliczone, 0))
                    ELSE 0 END) / NULLIF(SUM(CASE WHEN DK.walbrutto - ISNULL(PA.Rozliczone, 0) > 0.01
                                                   AND GETDATE() > ISNULL(PA.TerminPrawdziwy, DK.plattermin)
                                                  THEN DK.walbrutto - ISNULL(PA.Rozliczone, 0) ELSE 0 END), 0)
               AS SredniaDniPrzeterm
    FROM [192.168.0.112].[Handel].[HM].[DK] DK WITH (NOLOCK)
    LEFT JOIN PNAgg PA ON PA.dkid = DK.id
    LEFT JOIN [192.168.0.112].[Handel].[SSCommon].[ContractorClassification] WYM WITH (NOLOCK) ON WYM.ElementId = DK.khid
    WHERE DK.anulowany = 0
    GROUP BY ISNULL(WYM.CDim_Handlowiec_Val, N'Nieprzypisany')
),
BenchMarza AS (   -- D.2 inline, per handlowiec
    SELECT m.Handlowiec, SUM(m.NettoMaja - m.KgMaja * i.CenaInni) AS MarzaVsBench
    FROM (
        SELECT Handlowiec, TowarId, RokMiesiac,
               SUM(Kg) AS KgMaja, SUM(WartoscNetto) AS NettoMaja
        FROM #FaktBaza
        WHERE DataFaktury BETWEEN @DataOd AND @DataDo
        GROUP BY Handlowiec, TowarId, RokMiesiac
    ) m
    INNER JOIN (
        SELECT TowarId, RokMiesiac, SUM(WartoscNetto) / NULLIF(SUM(Kg), 0) AS CenaInni
        FROM #FaktBaza
        WHERE DataFaktury BETWEEN @DataOd AND @DataDo
        GROUP BY TowarId, RokMiesiac
    ) i ON i.TowarId = m.TowarId AND i.RokMiesiac = m.RokMiesiac
    GROUP BY m.Handlowiec
),
AggH AS (
    SELECT f.Handlowiec,
           COUNT(DISTINCT f.KontrahentId) AS LiczbaKlientow,
           COUNT(DISTINCT f.DKId)         AS LiczbaFaktur,
           SUM(f.Kg)                      AS SumaKg,
           SUM(f.WartoscNetto)            AS SumaNetto,
           SUM(f.WartoscNetto) / NULLIF(SUM(f.Kg), 0) AS SredniaCena
    FROM Fakt f
    GROUP BY f.Handlowiec
)
SELECT a.Handlowiec,
       a.LiczbaKlientow,
       a.LiczbaFaktur,
       CAST(a.SumaKg AS DECIMAL(18,1))                 AS SumaKg,
       CAST(a.SumaNetto AS DECIMAL(18,2))              AS SumaNetto,
       CAST(a.SredniaCena AS DECIMAL(10,2))            AS SredniaCenaZlKg,
       ISNULL(n.LiczbaNowych, 0)                       AS NowiKlienci,
       ISNULL(u.LiczbaUtraconych, 0)                   AS UtraceniKlienci,
       ISNULL(r.LiczbaReklamacji, 0)                   AS LiczbaReklamacji,
       CAST(ISNULL(r.LiczbaReklamacji, 0) * 100.0 / NULLIF(a.LiczbaFaktur, 0) AS DECIMAL(6,2)) AS Reklamacji_per_100_Faktur,
       CAST(h.HHI AS DECIMAL(8,1))                     AS HHI_Koncentracja,
       CAST(s.SredniaDniPrzeterm AS DECIMAL(8,2))      AS Sredni_Dni_Przeterm,
       CAST(b.MarzaVsBench AS DECIMAL(18,2))           AS Marza_vs_Benchmark_Zl
FROM AggH a
LEFT JOIN NowiKlienci  n ON n.Handlowiec = a.Handlowiec
LEFT JOIN UtracKlienci u ON u.Handlowiec = a.Handlowiec
LEFT JOIN ReklH        r ON r.Handlowiec = a.Handlowiec
LEFT JOIN HHI          h ON h.Handlowiec = a.Handlowiec
LEFT JOIN SaldoH       s ON s.Handlowiec = a.Handlowiec
LEFT JOIN BenchMarza   b ON b.Handlowiec = a.Handlowiec
ORDER BY CASE WHEN a.Handlowiec = @HandlowiecMaja THEN 0 ELSE 1 END, a.SumaNetto DESC;

/* ===========================================================================
   CLEANUP
   =========================================================================== */
IF OBJECT_ID('tempdb..#FaktBaza') IS NOT NULL DROP TABLE #FaktBaza;
IF OBJECT_ID('tempdb..#ZamBaza')  IS NOT NULL DROP TABLE #ZamBaza;

SET ANSI_WARNINGS ON;

/* ============================================================================
   ETAP 3 — SAMOSPRAWDZENIE (odpowiedzi dla użytkownika)
   ============================================================================

   ✅ Czy zapytanie pokrywa wszystkie 11 wymiarów (A–K)?
      A. Wolumen miesięczny           → A.1, A.2
      B. Klienci + koncentracja       → B.1, B.2, B.3 (HHI)
      C. Nowi/Przejęci/Utraceni       → C.1, C.2
      D. Ceny vs benchmark            → D.1, D.2, D.3, D.4
      E. Mix produktowy               → E.1, E.2
      F. Częstotliwość                → F.1, F.2
      G. Zamówienia vs realizacja     → G.1, G.2, G.3
      H. Reklamacje                   → H.1, H.2
      I. Płatności                    → I.1, I.2
      J. CRM                          → J.1, J.2 (defensywne, sprawdza istnienie tabel)
      K. Scorecard                    → K.1

   ⚠ Czego NIE wykorzystałem (świadomie):
      • HM.MG/MZ — to dokumenty magazynowe (WZ, MM±, PWP), nie faktury sprzedaży.
        Faktury siedzą w HM.DK/DP — które są używane konsekwentnie w istniejących widokach.
      • HandlowcyCRM, WlascicieleOdbiorcow — wspomniane w dokumentacji, ale w żadnym
        aktywnym widoku nie znalazłem ich wykorzystania. Pominę, żeby nie generować
        martwych kolumn.
      • Sage [STContractors].nazwa1/2 zawiera czasem różne warianty nazw — używam
        shortcut (zgodnie z konwencją w HandlowiecDashboard).

   ⚠ Ryzyko niespójności (raport A.1 + scorecard K.1 może być sprzeczny z G.x):
      Maja może być w bazie jako:
        (a) CDim_Handlowiec_Val = N'Maja' (na kontrahencie w ContractorClassification)
        (b) ZamowieniaMieso.Handlowiec = N'Maja' (na zamówieniu, historyczne)
      Te dwa źródła mogą rozjeżdżać się jeśli klient przeszedł z innego handlowca.
      Faktura "przejdzie" pod Maję retro (a), zamówienia historyczne zostaną (b).
      Raport 0.0 + 0.0b pokażą rozjazd — porównaj liczby przed wnioskami.

   ⚠ Czy benchmark "inni handlowcy" jest fair?
      • Paulina, która odchodzi z działu sprzedaży i przechodzi na żywiec, może mieć
        nietypowy mix (większy udział żywca, mniej mięsa).
      • Jola ma ~60% sprzedaży firmy (Damak/Trzepałka — wielkie konta) → jej cena/kg
        może być sztucznie niska bo wielkie wolumeny.
      • Klucz interpretacyjny: porównuj Maję głównie z Anią/Radkiem/Teresą (handlowcy
        średniej wielkości portfela), nie z Jolą.
      • W scorecard (K.1) Maja jest wymuszenie na pozycji 1 (ORDER BY) — porównuj
        ją parami z każdym innym handlowcem, nie z "wszyscy łącznie".

   ============================================================================ */
