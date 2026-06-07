-- LibraNet wymaga QUOTED_IDENTIFIER ON dla FOR XML PATH (uzywane w sekcji RealniHodowcy)
SET QUOTED_IDENTIFIER ON;

-- ============================================================================
-- Dashboard Przychod Zywca LIVE - skonsolidowane zapytanie
-- Jeden round-trip do LibraNet zamiast 4 osobnych. CTE DaneDostawy skanuje
-- FarmerCalc raz, wszystkie 4 result-sety korzystaja z niej.
--
-- Parametry: @Data (DATE)
--
-- Zwraca 4 result-sety w kolejnosci:
--   1. Dostawy        (per auto, kolumny dla DataGrid)
--   2. Podsumowanie   (1 wiersz, KPI Strip + sidebar)
--   3. Prognoza dnia  (1 wiersz, alert redukcji zamowien)
--   4. Postepy        (per harmonogram/hodowca, karty sidebar)
--
-- Krytyczne gotchas (CLAUDE.md):
--   * hd.SztukiDek / WagaDek / Auta / SztSzuflada to VARCHAR(?) -> TRY_CAST
--   * FarmerCalc.LpDostawy typ trzeba castowac do INT przy JOIN do hd.Lp
--   * Deleted flag w FarmerCalc - zawsze ISNULL(Deleted, 0) = 0
-- ============================================================================

-- ============================================================================
-- WSPOLNY CTE: DaneDostawy (skanowane raz, reuse w 4 result-setach)
-- ============================================================================
;WITH DaneDostawy AS (
    SELECT fc.*, RTRIM(fc.CustomerGID) AS CustGID
    FROM dbo.FarmerCalc fc
    WHERE fc.CalcDate = @Data AND ISNULL(fc.Deleted, 0) = 0
),
DostawcyMap AS (
    SELECT LTRIM(RTRIM(d.ID)) AS TrimID, d.Name, d.ShortName
    FROM dbo.Dostawcy d
    WHERE LTRIM(RTRIM(d.ID)) IN (SELECT DISTINCT CustGID FROM DaneDostawy)
),
SumaZwazonychPerHarmonogram AS (
    SELECT
        fc.LpDostawy,
        COUNT(*) AS AutaZwazone,
        SUM(ISNULL(fc.LumQnt, 0)) AS SztukiZwazoneSuma,
        SUM(ISNULL(fc.NettoWeight, 0)) AS KgZwazoneSuma
    FROM DaneDostawy fc
    WHERE ISNULL(fc.FullWeight, 0) > 0 AND ISNULL(fc.EmptyWeight, 0) > 0
    GROUP BY fc.LpDostawy
),
SumaWszystkichPerHarmonogram AS (
    SELECT
        fc.LpDostawy,
        COUNT(*) AS AutaOgolem
    FROM DaneDostawy fc
    GROUP BY fc.LpDostawy
),
PozostaloPerHarmonogram AS (
    SELECT
        fc.LpDostawy,
        hd.Lp AS HarmonogramLp,
        hd.Dostawca AS HodowcaHarmonogram,
        ISNULL(TRY_CAST(hd.SztukiDek AS INT), 0) AS PlanSztukiLacznie,
        CAST(ISNULL(TRY_CAST(hd.SztukiDek AS INT), 0) * ISNULL(TRY_CAST(hd.WagaDek AS DECIMAL(10,3)), 0) AS DECIMAL(12,0)) AS PlanKgLacznie,
        ISNULL(TRY_CAST(hd.WagaDek AS DECIMAL(10,3)), 0) AS WagaDekl,
        TRY_CAST(hd.SztSzuflada AS DECIMAL(10,2)) AS SztPojPlan,
        ISNULL(TRY_CAST(hd.Auta AS INT), 1) AS AutaPlanowane,
        ISNULL(sz.AutaZwazone, 0) AS AutaZwazone,
        ISNULL(sw.AutaOgolem, 0) AS AutaOgolem,
        ISNULL(sz.SztukiZwazoneSuma, 0) AS SztukiZwazoneSuma,
        ISNULL(sz.KgZwazoneSuma, 0) AS KgZwazoneSuma,
        ISNULL(TRY_CAST(hd.SztukiDek AS INT), 0) - ISNULL(sz.SztukiZwazoneSuma, 0) AS SztukiPozostalo,
        CAST(ISNULL(TRY_CAST(hd.SztukiDek AS INT), 0) * ISNULL(TRY_CAST(hd.WagaDek AS DECIMAL(10,3)), 0) AS DECIMAL(12,0)) - ISNULL(sz.KgZwazoneSuma, 0) AS KgPozostalo,
        ISNULL(sw.AutaOgolem, 0) - ISNULL(sz.AutaZwazone, 0) AS AutaCzekajacych,
        CASE WHEN CAST(ISNULL(TRY_CAST(hd.SztukiDek AS INT), 0) * ISNULL(TRY_CAST(hd.WagaDek AS DECIMAL(10,3)), 0) AS DECIMAL(12,0)) > 0
             THEN CAST(ISNULL(sz.KgZwazoneSuma, 0) * 100.0 / (ISNULL(TRY_CAST(hd.SztukiDek AS INT), 0) * ISNULL(TRY_CAST(hd.WagaDek AS DECIMAL(10,3)), 0)) AS DECIMAL(5,1))
             ELSE 0 END AS RealizacjaProc,
        CASE WHEN ISNULL(sz.AutaZwazone, 0) > 0 AND ISNULL(TRY_CAST(hd.Auta AS INT), 1) > 0
             THEN CAST((ISNULL(sz.KgZwazoneSuma, 0) / sz.AutaZwazone)
                  / NULLIF((CAST(ISNULL(TRY_CAST(hd.SztukiDek AS INT), 0) * ISNULL(TRY_CAST(hd.WagaDek AS DECIMAL(10,3)), 0) AS DECIMAL(12,0)) / ISNULL(TRY_CAST(hd.Auta AS INT), 1)), 0) * 100 AS DECIMAL(5,1))
             ELSE 100 END AS TrendProc
    FROM (SELECT DISTINCT LpDostawy FROM DaneDostawy) fc
    LEFT JOIN dbo.HarmonogramDostaw hd ON TRY_CAST(fc.LpDostawy AS INT) = hd.Lp
    LEFT JOIN SumaZwazonychPerHarmonogram sz ON fc.LpDostawy = sz.LpDostawy
    LEFT JOIN SumaWszystkichPerHarmonogram sw ON fc.LpDostawy = sw.LpDostawy
)

-- ============================================================================
-- RESULT SET 1: DOSTAWY (per auto, dla DataGrid)
-- ============================================================================
SELECT
    fc.ID,
    ISNULL(TRY_CAST(fc.CarLp AS INT), 0) AS NrKursu,
    fc.CalcDate AS Data,
    fc.LpDostawy,

    -- Hodowca
    ISNULL(d.Name, ISNULL(ph.HodowcaHarmonogram, 'Nieznany')) AS Hodowca,
    ISNULL(d.ShortName, '') AS HodowcaSkrot,

    -- ========== PLAN LACZNY Z HARMONOGRAMU ==========
    ISNULL(ph.PlanSztukiLacznie, 0) AS PlanSztukiLacznie,
    ISNULL(ph.PlanKgLacznie, 0) AS PlanKgLacznie,
    ISNULL(ph.WagaDekl, 0) AS WagaDeklHarmonogram,
    ph.SztPojPlan,
    ISNULL(ph.AutaPlanowane, 1) AS AutaPlanowane,

    -- ========== POSTEP HARMONOGRAMU ==========
    ISNULL(ph.AutaZwazone, 0) AS AutaZwazone,
    ISNULL(ph.AutaOgolem, 0) AS AutaOgolem,
    ISNULL(ph.AutaCzekajacych, 0) AS AutaCzekajacych,
    ISNULL(ph.SztukiZwazoneSuma, 0) AS SztukiZwazoneSuma,
    ISNULL(ph.KgZwazoneSuma, 0) AS KgZwazoneSuma,
    ISNULL(ph.SztukiPozostalo, 0) AS SztukiPozostalo,
    ISNULL(ph.KgPozostalo, 0) AS KgPozostalo,
    ISNULL(ph.RealizacjaProc, 0) AS RealizacjaProc,
    ISNULL(ph.TrendProc, 100) AS TrendProc,

    -- ========== PLAN NA POJEDYNCZE AUTO ==========
    -- Sztuki: z harmonogramu (proporcjonalny) lub DeclI1
    CASE WHEN ISNULL(ph.AutaPlanowane, 0) > 0
         THEN CAST(ISNULL(ph.PlanSztukiLacznie, 0) / ph.AutaPlanowane AS INT)
         ELSE ISNULL(fc.DeclI1, 0) END AS SztukiPlan,
    -- KgPlan z 3 priorytetow:
    --   1. NettoFarmWeight - waga deklarowana per auto przez hodowce (najprecyzyjniej, zawsze gdy wpisana)
    --   2. PlanKgLacznie / AutaPlanowane - usredniony z harmonogramu (gdy NettoFarmWeight=0)
    --   3. DeclI1 × WagaDek - sztuki × waga deklarowana (ostatecznosc)
    -- Zaden z tych priorytetow nie da 0 jesli hodowca poda waqe odjazdu.
    CASE
        WHEN ISNULL(fc.NettoFarmWeight, 0) > 0
            THEN CAST(fc.NettoFarmWeight AS DECIMAL(12,0))
        WHEN ISNULL(ph.AutaPlanowane, 0) > 0 AND ISNULL(ph.PlanKgLacznie, 0) > 0
            THEN CAST(ISNULL(ph.PlanKgLacznie, 0) / ph.AutaPlanowane AS DECIMAL(12,0))
        ELSE CAST(ISNULL(fc.DeclI1, 0) * COALESCE(ph.WagaDekl, fc.WagaDek, 0) AS DECIMAL(12,0))
    END AS KgPlan,

    -- Srednia waga deklarowana
    CAST(ISNULL(ph.WagaDekl, COALESCE(fc.WagaDek, 0)) AS DECIMAL(10,3)) AS SredniaWagaPlan,

    -- ========== RZECZYWISTE (z FarmerCalc) ==========
    CAST(ISNULL(fc.FullWeight, 0) AS DECIMAL(18,2)) AS Brutto,
    CAST(ISNULL(fc.EmptyWeight, 0) AS DECIMAL(18,2)) AS Tara,
    CAST(ISNULL(fc.NettoWeight, 0) AS DECIMAL(18,2)) AS KgRzeczywiste,
    ISNULL(fc.LumQnt, 0) AS SztukiRzeczywiste,

    CASE WHEN ISNULL(fc.LumQnt, 0) > 0 AND ISNULL(fc.NettoWeight, 0) > 0
         THEN CAST(fc.NettoWeight / fc.LumQnt AS DECIMAL(10,3))
         ELSE NULL END AS SredniaWagaRzeczywista,

    TRY_CAST(fc.SztPoj AS DECIMAL(10,2)) AS SztPojRzecz,

    -- ========== ODCHYLENIE vs PLAN-NA-AUTO ==========
    -- (Netto - plan proporcjonalny na auto). NULL gdy brak harmonogramu lub auto nie zwazone.
    CASE WHEN ISNULL(ph.AutaPlanowane, 0) > 0 AND ISNULL(fc.NettoWeight, 0) > 0 AND ISNULL(ph.PlanKgLacznie, 0) > 0
         THEN CAST(fc.NettoWeight - (ISNULL(ph.PlanKgLacznie, 0) / ph.AutaPlanowane) AS DECIMAL(12,0))
         ELSE NULL END AS OdchylenieVsPlanAutoKg,

    CASE WHEN ISNULL(ph.AutaPlanowane, 0) > 0 AND ISNULL(fc.NettoWeight, 0) > 0
              AND (ISNULL(ph.PlanKgLacznie, 0) / ph.AutaPlanowane) > 0
         THEN CAST(
              (fc.NettoWeight - (ISNULL(ph.PlanKgLacznie, 0) / ph.AutaPlanowane))
              / (ISNULL(ph.PlanKgLacznie, 0) / ph.AutaPlanowane) * 100
              AS DECIMAL(5,2))
         ELSE NULL END AS OdchylenieVsPlanAutoProc,

    -- ========== ODCHYLENIE vs DEKLARACJA HODOWCY ==========
    -- (Netto - waga deklarowana przez hodowce). Niezalezne od harmonogramu, sluzy
    -- do wykrywania klamstw hodowcy (deklarowal X, przyjechalo Y).
    CASE WHEN ISNULL(fc.NettoWeight, 0) > 0 AND COALESCE(fc.NettoFarmWeight, fc.WagaDek, 0) > 0
         THEN CAST(fc.NettoWeight - COALESCE(fc.NettoFarmWeight, fc.WagaDek, 0) AS DECIMAL(18,2))
         ELSE NULL END AS OdchylenieVsDeklHodowcaKg,

    CASE WHEN ISNULL(fc.NettoWeight, 0) > 0 AND COALESCE(fc.NettoFarmWeight, fc.WagaDek, 0) > 0
         THEN CAST((fc.NettoWeight - COALESCE(fc.NettoFarmWeight, fc.WagaDek, 0))
              / NULLIF(COALESCE(fc.NettoFarmWeight, fc.WagaDek, 0), 0) * 100 AS DECIMAL(10,2))
         ELSE NULL END AS OdchylenieVsDeklHodowcaProc,

    -- ========== ODCHYLENIE SREDNIEJ WAGI (rzecz - dekl) ==========
    CASE WHEN ISNULL(fc.LumQnt, 0) > 0 AND ISNULL(fc.NettoWeight, 0) > 0
              AND ISNULL(ph.WagaDekl, COALESCE(fc.WagaDek, 0)) > 0
         THEN CAST((fc.NettoWeight / fc.LumQnt) - ISNULL(ph.WagaDekl, COALESCE(fc.WagaDek, 0)) AS DECIMAL(10,3))
         ELSE NULL END AS OdchylenieWagi,

    -- ========== STATUS ==========
    CASE
        WHEN ISNULL(fc.FullWeight, 0) > 0 AND ISNULL(fc.EmptyWeight, 0) > 0 THEN 2
        WHEN ISNULL(fc.FullWeight, 0) > 0 THEN 1
        ELSE 0
    END AS StatusId,

    ISNULL(fc.DeclI2, 0) AS Padle,
    ISNULL(TRY_CAST(fc.DeclI3 AS INT), 0) + ISNULL(TRY_CAST(fc.DeclI4 AS INT), 0) + ISNULL(TRY_CAST(fc.DeclI5 AS INT), 0) AS Konfiskaty,

    fc.Przyjazd,
    fc.SlaughterWeightDate AS GodzinaWazenia,
    fc.SlaughterWeightUser AS KtoWazyl,

    ISNULL(fc.SztukiExcel, 0) AS SztukiExcel

FROM DaneDostawy fc
LEFT JOIN PozostaloPerHarmonogram ph ON fc.LpDostawy = ph.LpDostawy
LEFT JOIN DostawcyMap d ON d.TrimID = fc.CustGID
ORDER BY TRY_CAST(fc.CarLp AS INT);

-- ============================================================================
-- RESULT SET 2: PODSUMOWANIE (1 wiersz, KPI Strip)
-- ============================================================================
;WITH DaneDostawy AS (
    SELECT fc.LpDostawy, fc.DeclI1, fc.NettoFarmWeight, fc.WagaDek,
           fc.FullWeight, fc.EmptyWeight, fc.NettoWeight, fc.LumQnt,
           fc.SlaughterWeightDate
    FROM dbo.FarmerCalc fc
    WHERE fc.CalcDate = @Data AND ISNULL(fc.Deleted, 0) = 0
),
UnikalneHarmonogramy AS (
    SELECT DISTINCT
        fc.LpDostawy,
        TRY_CAST(hd.SztukiDek AS INT) AS SztukiDek,
        TRY_CAST(hd.WagaDek AS DECIMAL(10,3)) AS WagaDek,
        TRY_CAST(hd.SztSzuflada AS DECIMAL(10,2)) AS SztSzuflada,
        TRY_CAST(hd.Auta AS INT) AS Auta,
        CAST(ISNULL(TRY_CAST(hd.SztukiDek AS INT), 0) * ISNULL(TRY_CAST(hd.WagaDek AS DECIMAL(10,3)), 0) AS DECIMAL(12,0)) AS KgPlanLacznie
    FROM DaneDostawy fc
    INNER JOIN dbo.HarmonogramDostaw hd ON TRY_CAST(fc.LpDostawy AS INT) = hd.Lp
    WHERE fc.LpDostawy IS NOT NULL
),
PlanZFarmerCalc AS (
    SELECT
        SUM(ISNULL(fc.DeclI1, 0)) AS SztukiPlan,
        SUM(CAST(COALESCE(fc.NettoFarmWeight, fc.WagaDek, 0) AS DECIMAL(18,2))) AS KgPlan
    FROM DaneDostawy fc
    WHERE fc.LpDostawy IS NULL
),
PlanDlaZwazonych AS (
    SELECT
        SUM(
            CASE
                WHEN hd.Lp IS NOT NULL AND ISNULL(TRY_CAST(hd.Auta AS INT), 1) > 0
                THEN CAST(ISNULL(TRY_CAST(hd.SztukiDek AS INT), 0) * ISNULL(TRY_CAST(hd.WagaDek AS DECIMAL(10,3)), 0) AS DECIMAL(12,0))
                     / ISNULL(TRY_CAST(hd.Auta AS INT), 1)
                ELSE CAST(COALESCE(fc.NettoFarmWeight, fc.WagaDek, 0) AS DECIMAL(18,2))
            END
        ) AS KgPlanDoZwazonych
    FROM DaneDostawy fc
    LEFT JOIN dbo.HarmonogramDostaw hd ON TRY_CAST(fc.LpDostawy AS INT) = hd.Lp
    WHERE ISNULL(fc.FullWeight, 0) > 0 AND ISNULL(fc.EmptyWeight, 0) > 0
),
RzeczywisteDnia AS (
    SELECT
        SUM(ISNULL(fc.LumQnt, 0)) AS SztukiRzeczSuma,
        SUM(ISNULL(fc.NettoWeight, 0)) AS KgRzeczSuma,
        COUNT(*) AS LiczbaDostawOgolem,
        SUM(CASE WHEN ISNULL(fc.FullWeight,0) > 0 AND ISNULL(fc.EmptyWeight,0) > 0 THEN 1 ELSE 0 END) AS LiczbaZwazonych,
        SUM(CASE WHEN ISNULL(fc.FullWeight,0) > 0 AND ISNULL(fc.EmptyWeight,0) = 0 THEN 1 ELSE 0 END) AS LiczbaCzekaNaTare,
        SUM(CASE WHEN ISNULL(fc.FullWeight,0) = 0 THEN 1 ELSE 0 END) AS LiczbaOczekujacych,
        SUM(CASE WHEN ISNULL(fc.FullWeight,0) > 0 AND ISNULL(fc.EmptyWeight,0) > 0
            THEN CAST(ISNULL(fc.NettoWeight, 0) AS DECIMAL(18,2)) ELSE 0 END) AS KgZwazoneSuma,
        SUM(CASE WHEN ISNULL(fc.FullWeight,0) > 0 AND ISNULL(fc.EmptyWeight,0) > 0
            THEN ISNULL(fc.LumQnt, 0) ELSE 0 END) AS SztukiZwazoneSuma,
        -- Do liczenia tempa: najwczesniejsze i najpozniejsze wazenie dnia
        MIN(CASE WHEN ISNULL(fc.FullWeight,0) > 0 AND ISNULL(fc.EmptyWeight,0) > 0 THEN fc.SlaughterWeightDate END) AS PierwszeWazenie,
        MAX(CASE WHEN ISNULL(fc.FullWeight,0) > 0 AND ISNULL(fc.EmptyWeight,0) > 0 THEN fc.SlaughterWeightDate END) AS OstatnieWazenie
    FROM DaneDostawy fc
)
SELECT
    ISNULL((SELECT SUM(uh.SztukiDek) FROM UnikalneHarmonogramy uh), 0) + ISNULL((SELECT SztukiPlan FROM PlanZFarmerCalc), 0) AS SztukiPlanSuma,
    ISNULL((SELECT SUM(uh.KgPlanLacznie) FROM UnikalneHarmonogramy uh), 0) + ISNULL((SELECT KgPlan FROM PlanZFarmerCalc), 0) AS KgPlanSuma,

    CASE WHEN (SELECT SUM(uh.SztukiDek) FROM UnikalneHarmonogramy uh) > 0
         THEN CAST((SELECT SUM(uh.KgPlanLacznie) FROM UnikalneHarmonogramy uh) / NULLIF((SELECT SUM(uh.SztukiDek) FROM UnikalneHarmonogramy uh), 0) AS DECIMAL(10,3))
         ELSE NULL END AS SrWagaPlanSrednia,

    r.SztukiRzeczSuma,
    r.KgRzeczSuma,
    r.KgZwazoneSuma,
    r.SztukiZwazoneSuma,

    CASE WHEN r.SztukiZwazoneSuma > 0
         THEN CAST(r.KgZwazoneSuma / NULLIF(r.SztukiZwazoneSuma, 0) AS DECIMAL(10,3))
         ELSE NULL END AS SrWagaRzeczSrednia,

    r.KgZwazoneSuma - ISNULL(pz.KgPlanDoZwazonych, 0) AS OdchylenieKgSuma,
    ISNULL(pz.KgPlanDoZwazonych, 0) AS KgPlanDoZwazonych,

    r.LiczbaDostawOgolem,
    r.LiczbaZwazonych,
    r.LiczbaCzekaNaTare,
    r.LiczbaOczekujacych,

    -- Tempo: do liczenia ETA i pace - momenty pierwszego/ostatniego wazenia w dniu
    r.PierwszeWazenie,
    r.OstatnieWazenie
FROM RzeczywisteDnia r
CROSS JOIN PlanDlaZwazonych pz;

-- ============================================================================
-- RESULT SET 3: PROGNOZA (1 wiersz, alert redukcji zamowien)
-- ============================================================================
;WITH DaneDostawy AS (
    SELECT fc.LpDostawy, fc.FullWeight, fc.EmptyWeight, fc.NettoWeight
    FROM dbo.FarmerCalc fc
    WHERE fc.CalcDate = @Data AND ISNULL(fc.Deleted, 0) = 0
),
DaneDnia AS (
    SELECT
        SUM(CASE WHEN ISNULL(fc.FullWeight,0) > 0 AND ISNULL(fc.EmptyWeight,0) > 0
                 THEN ISNULL(fc.NettoWeight, 0) ELSE 0 END) AS KgZwazone,
        SUM(CASE WHEN ISNULL(fc.FullWeight,0) > 0 AND ISNULL(fc.EmptyWeight,0) > 0
                 THEN 1 ELSE 0 END) AS AutaZwazone,
        COUNT(*) AS AutaOgolem
    FROM DaneDostawy fc
),
PlanDnia AS (
    SELECT
        SUM(CAST(ISNULL(TRY_CAST(hd.SztukiDek AS INT), 0) * ISNULL(TRY_CAST(hd.WagaDek AS DECIMAL(10,3)), 0) AS DECIMAL(12,0))) AS KgPlanLacznie
    FROM (SELECT DISTINCT LpDostawy FROM DaneDostawy WHERE LpDostawy IS NOT NULL) fc
    INNER JOIN dbo.HarmonogramDostaw hd ON TRY_CAST(fc.LpDostawy AS INT) = hd.Lp
)
SELECT
    ISNULL(p.KgPlanLacznie, 0) AS KgPlanLacznie,
    ISNULL(d.KgZwazone, 0) AS KgZwazone,
    ISNULL(d.AutaZwazone, 0) AS AutaZwazone,
    ISNULL(d.AutaOgolem, 0) AS AutaOgolem
FROM DaneDnia d
CROSS JOIN PlanDnia p;

-- ============================================================================
-- RESULT SET 4: POSTEPY HARMONOGRAMOW (per hodowca, karty sidebar)
--
-- WAZNE: pokazujemy WSZYSTKIE harmonogramy dnia (RIGHT JOIN do hd),
-- nie tylko te ktore maja auta w FarmerCalc. Dzieki temu hodowca ktory
-- mial zaplanowane auta ale nic nie przyjechalo (np. Lapiak Monika 0/2)
-- tez sie pojawi w sidebarze - z badge'em "0 z X aut".
--
-- Kolumna Overflow = AutaOgolem - AutaPlanowane:
--   > 0  : OVERFLOW (wiecej aut niz planowano, np. Piotr 3 z 1)
--   = 0  : OK (zgadza sie)
--   < 0  : UNDERFLOW (mniej aut niz planowano, np. Monika 0 z 2)
-- Sluzy do detekcji niezgodnosci harmonogram vs rzeczywistosc.
-- ============================================================================
;WITH DaneDostawy AS (
    SELECT fc.LpDostawy, fc.LumQnt, fc.NettoWeight, fc.FullWeight, fc.EmptyWeight
    FROM dbo.FarmerCalc fc
    WHERE fc.CalcDate = @Data AND ISNULL(fc.Deleted, 0) = 0
),
SumaZwazonych AS (
    SELECT fc.LpDostawy, COUNT(*) AS AutaZwazone,
           SUM(ISNULL(fc.LumQnt, 0)) AS SztukiZwazoneSuma,
           SUM(ISNULL(fc.NettoWeight, 0)) AS KgZwazoneSuma
    FROM DaneDostawy fc
    WHERE ISNULL(fc.FullWeight, 0) > 0 AND ISNULL(fc.EmptyWeight, 0) > 0
    GROUP BY fc.LpDostawy
),
SumaWszystkich AS (
    SELECT fc.LpDostawy, COUNT(*) AS AutaOgolem
    FROM DaneDostawy fc
    GROUP BY fc.LpDostawy
),
RealniHodowcyPerLpRaw AS (
    -- Unikalne pary (LpDostawy, Hodowca) z FarmerCalc - poprzez Dostawcy.Name
    SELECT DISTINCT fc.LpDostawy, ISNULL(d.Name, N'(brak nazwy)') AS Nazwa
    FROM dbo.FarmerCalc fc
    LEFT JOIN dbo.Dostawcy d ON LTRIM(RTRIM(d.ID)) = LTRIM(RTRIM(fc.CustomerGID))
    WHERE fc.CalcDate = @Data AND ISNULL(fc.Deleted, 0) = 0 AND fc.LpDostawy IS NOT NULL
),
RealniHodowcyPerLp AS (
    -- Agregacja w stylu SQL 2008 (LibraNet ma compatibility_level=100, brak STRING_AGG):
    -- STUFF + FOR XML PATH zlepia DISTINCT nazwy posortowane po Nazwa.
    -- Wynik: "Ferma Sobota W, Ferma Wyborów, Pietrasik Katarzyna"
    SELECT outer_r.LpDostawy,
           STUFF((
               SELECT N', ' + inner_r.Nazwa
               FROM RealniHodowcyPerLpRaw inner_r
               WHERE inner_r.LpDostawy = outer_r.LpDostawy
               ORDER BY inner_r.Nazwa
               FOR XML PATH('')
           ), 1, 2, '') AS RealniHodowcy,
           COUNT(*) AS LiczbaRealnychHodowcow
    FROM RealniHodowcyPerLpRaw outer_r
    GROUP BY outer_r.LpDostawy
),
HarmonogramyDnia AS (
    -- Wszystkie harmonogramy zaplanowane na ten dzien + statusy biznesowe.
    -- WYKLUCZAMY puste harmonogramy: AutaPlanowane=0 AND PlanKg=0 - to placeholder'y
    -- ktore dispatcher dodal "na probe" i nigdy nie dotarly do realizacji.
    --
    -- PotwWaga/PotwSztuki = 1 oznacza ze Asia potwierdzila w Menu>Specyfikacja Surowca.
    -- Sluzy do odrozniania AKTUALNYCH harmonogramow od STARYCH (anulowanych).
    SELECT hd.Lp,
           hd.DostawcaID,
           ISNULL(hd.Dostawca, 'Nieznany') AS Dostawca,
           ISNULL(TRY_CAST(hd.Auta AS INT), 0) AS AutaPlanowane,
           ISNULL(TRY_CAST(hd.SztukiDek AS INT), 0) AS PlanSztukiLacznie,
           CAST(ISNULL(TRY_CAST(hd.SztukiDek AS INT), 0)
              * ISNULL(TRY_CAST(hd.WagaDek AS DECIMAL(10,3)), 0)
              AS DECIMAL(12,0)) AS PlanKgLacznie,
           TRY_CAST(hd.WagaDek AS DECIMAL(10,3)) AS SredniaWagaPlan,
           -- Klucz biznesowy do deduplikacji aktualne vs stare:
           ISNULL(hd.PotwWaga, 0) AS PotwWaga,
           ISNULL(hd.PotwSztuki, 0) AS PotwSztuki,
           ISNULL(hd.DataMod, hd.DataUtw) AS DataOstatniejZmiany
    FROM dbo.HarmonogramDostaw hd
    WHERE CAST(hd.DataOdbioru AS DATE) = @Data
      AND (ISNULL(TRY_CAST(hd.Auta AS INT), 0) > 0
           OR ISNULL(TRY_CAST(hd.SztukiDek AS INT), 0) > 0)
),
SamotneDostawy AS (
    -- Auta ktore wjechaly ale ich LpDostawy nie ma w HarmonogramDostaw dnia
    -- (rzadkie - dispatcher wpisal LP z innego dnia / spoza harmonogramu).
    SELECT DISTINCT fc.LpDostawy
    FROM DaneDostawy fc
    WHERE fc.LpDostawy IS NOT NULL
      AND NOT EXISTS (SELECT 1 FROM HarmonogramyDnia hd WHERE hd.Lp = fc.LpDostawy)
)
SELECT
    hd.Lp AS LpDostawy,
    hd.Dostawca AS Hodowca,
    ISNULL(sz.AutaZwazone, 0) AS AutaZwazone,
    ISNULL(sw.AutaOgolem, 0) AS AutaOgolem,
    hd.AutaPlanowane,
    hd.PlanSztukiLacznie,
    hd.PlanKgLacznie,
    ISNULL(sz.SztukiZwazoneSuma, 0) AS SztukiZwazoneSuma,
    ISNULL(sz.KgZwazoneSuma, 0) AS KgZwazoneSuma,
    hd.SredniaWagaPlan,
    CASE WHEN ISNULL(sz.SztukiZwazoneSuma, 0) > 0
         THEN CAST(sz.KgZwazoneSuma AS DECIMAL(12,3)) / sz.SztukiZwazoneSuma
         ELSE NULL END AS SredniaWagaRzecz,
    -- NOWE: statusy biznesowe do deduplikacji (Asia potwierdzila w Specyfikacji Surowca)
    hd.PotwWaga,
    hd.PotwSztuki,
    hd.DataOstatniejZmiany,
    -- Lista realnych hodowcow w tym LP (komu naprawde nalezy auto)
    ISNULL(rh.RealniHodowcy, '') AS RealniHodowcy,
    ISNULL(rh.LiczbaRealnychHodowcow, 0) AS LiczbaRealnychHodowcow
FROM HarmonogramyDnia hd
LEFT JOIN SumaZwazonych sz ON sz.LpDostawy = hd.Lp
LEFT JOIN SumaWszystkich sw ON sw.LpDostawy = hd.Lp
LEFT JOIN RealniHodowcyPerLp rh ON rh.LpDostawy = hd.Lp
-- Pomijaj harmonogramy ktore maja AutaPlanowane=0 AND nic nie wjechalo
-- (placeholder'y w bazie). Niezgodnosci dalej wykrywamy bo Overflow=0 dla nich.
WHERE hd.AutaPlanowane > 0 OR ISNULL(sw.AutaOgolem, 0) > 0

UNION ALL

-- Sieroty: auta ktore maja LpDostawy spoza harmonogramow dnia
SELECT
    sd.LpDostawy AS LpDostawy,
    '(brak harmonogramu LP=' + CAST(sd.LpDostawy AS VARCHAR(20)) + ')' AS Hodowca,
    ISNULL(sz.AutaZwazone, 0) AS AutaZwazone,
    ISNULL(sw.AutaOgolem, 0) AS AutaOgolem,
    0 AS AutaPlanowane,
    0 AS PlanSztukiLacznie,
    CAST(0 AS DECIMAL(12,0)) AS PlanKgLacznie,
    ISNULL(sz.SztukiZwazoneSuma, 0) AS SztukiZwazoneSuma,
    ISNULL(sz.KgZwazoneSuma, 0) AS KgZwazoneSuma,
    CAST(NULL AS DECIMAL(10,3)) AS SredniaWagaPlan,
    CASE WHEN ISNULL(sz.SztukiZwazoneSuma, 0) > 0
         THEN CAST(sz.KgZwazoneSuma AS DECIMAL(12,3)) / sz.SztukiZwazoneSuma
         ELSE NULL END AS SredniaWagaRzecz,
    CAST(0 AS BIT) AS PotwWaga,
    CAST(0 AS BIT) AS PotwSztuki,
    CAST(NULL AS DATETIME) AS DataOstatniejZmiany,
    ISNULL(rh.RealniHodowcy, '') AS RealniHodowcy,
    ISNULL(rh.LiczbaRealnychHodowcow, 0) AS LiczbaRealnychHodowcow
FROM SamotneDostawy sd
LEFT JOIN SumaZwazonych sz ON sd.LpDostawy = sz.LpDostawy
LEFT JOIN SumaWszystkich sw ON sd.LpDostawy = sw.LpDostawy
LEFT JOIN RealniHodowcyPerLp rh ON rh.LpDostawy = sd.LpDostawy

ORDER BY Hodowca;
