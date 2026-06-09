-- ============================================================================
-- DIAGNOZA: dlaczego Dashboard pokazuje SUMA Plan = 188 481 a Excel = 188 740,08
--
-- Dashboard liczy: SUM(KgPlanNaAuto) z _dostawy (czyli z FarmerCalc, per WIERSZ DataGrid)
-- Excel liczy:     SUM(SztukiDek × WagaDek) z HarmonogramDostaw (per HODOWCA)
--
-- Roznica = +overflow_aut (więcej aut wjechało niż planowano)
--           − plan_hodowcow_bez_aut (zaplanowany ale 0 wjechało)
-- ============================================================================

SET QUOTED_IDENTIFIER ON;
DECLARE @Data DATE = '2026-06-05';

-- =====================================================================
-- KROK 1: Symulacja DOKLADNIE tego co robi Dashboard (suma per wiersz)
-- =====================================================================
PRINT '=== KROK 1: PER AUTO jak Dashboard (tryb "Nowe" z fixem overflow) ===';

;WITH HD AS (
    SELECT hd.Lp, hd.Dostawca,
           TRY_CAST(hd.WagaDek AS DECIMAL(10,3)) AS WagaDek,
           ISNULL(TRY_CAST(hd.Auta AS INT), 0) AS PlanAut,
           CAST(ISNULL(TRY_CAST(hd.SztukiDek AS INT), 0)
              * ISNULL(TRY_CAST(hd.WagaDek AS DECIMAL(10,3)), 0) AS DECIMAL(12,0)) AS PlanLaczny,
           hd.PotwWaga
    FROM dbo.HarmonogramDostaw hd
    WHERE CAST(hd.DataOdbioru AS DATE) = @Data
      AND ISNULL(hd.PotwWaga, 0) = 1   -- tylko potwierdzone (aktualne)
),
Auta AS (
    SELECT fc.ID, fc.CarLp AS Nr, fc.LpDostawy, ISNULL(fc.SztukiExcel, 0) AS SztExcel,
           fc.NettoWeight AS Netto, fc.NettoFarmWeight,
           d.Name AS HodowcaFarmCalc,
           ROW_NUMBER() OVER (PARTITION BY fc.LpDostawy ORDER BY fc.CarLp) AS NrWGrupie,
           COUNT(*) OVER (PARTITION BY fc.LpDostawy) AS WGrupieAut,
           SUM(ISNULL(fc.SztukiExcel, 0)) OVER (PARTITION BY fc.LpDostawy ORDER BY fc.CarLp ROWS BETWEEN UNBOUNDED PRECEDING AND 1 PRECEDING) AS SumExcelPrzed
    FROM dbo.FarmerCalc fc
    LEFT JOIN dbo.Dostawcy d ON LTRIM(RTRIM(d.ID)) = LTRIM(RTRIM(fc.CustomerGID))
    WHERE fc.CalcDate = @Data AND ISNULL(fc.Deleted, 0) = 0
)
SELECT
    a.Nr,
    a.LpDostawy,
    a.HodowcaFarmCalc AS Hodowca_w_FarmerCalc,
    hd.Dostawca AS Hodowca_w_HarmonogramDostaw,
    a.NrWGrupie AS NrWGr,
    a.WGrupieAut AS WGr,
    hd.PlanAut,
    hd.PlanLaczny AS PlanLaczny_Harm,
    a.SztExcel,
    hd.WagaDek,
    -- Logika dashboardu tryb "Nowe":
    --   OVERFLOW (WGrupieAut > PlanAut)        → SztExcel × WagaDek per auto
    --   non-overflow, ostatnie auto             → PlanLaczny - suma poprzednich
    --   non-overflow, nie-ostatnie              → SztExcel × WagaDek
    --   BRAK harmonogramu (LpDostawy=NULL)      → fallback do NettoFarmWeight
    CASE
        WHEN a.LpDostawy IS NULL OR hd.Lp IS NULL
            THEN ISNULL(a.NettoFarmWeight, 0)
        WHEN a.WGrupieAut > hd.PlanAut
            THEN CAST(a.SztExcel * hd.WagaDek AS DECIMAL(12,0))
        WHEN a.NrWGrupie = a.WGrupieAut
            THEN hd.PlanLaczny - ISNULL(a.SumExcelPrzed, 0) * hd.WagaDek
        ELSE CAST(a.SztExcel * hd.WagaDek AS DECIMAL(12,0))
    END AS Plan_DashboardLive,
    a.Netto
FROM Auta a
LEFT JOIN HD hd ON hd.Lp = a.LpDostawy
ORDER BY a.Nr;

-- =====================================================================
-- KROK 2: SUMA Dashboardu (per wiersz)
-- =====================================================================
PRINT '';
PRINT '=== KROK 2: SUMA dashboardu (jak w wierszu SUMA) ===';

;WITH HD AS (
    SELECT hd.Lp,
           TRY_CAST(hd.WagaDek AS DECIMAL(10,3)) AS WagaDek,
           ISNULL(TRY_CAST(hd.Auta AS INT), 0) AS PlanAut,
           CAST(ISNULL(TRY_CAST(hd.SztukiDek AS INT), 0)
              * ISNULL(TRY_CAST(hd.WagaDek AS DECIMAL(10,3)), 0) AS DECIMAL(12,0)) AS PlanLaczny
    FROM dbo.HarmonogramDostaw hd
    WHERE CAST(hd.DataOdbioru AS DATE) = @Data AND ISNULL(hd.PotwWaga, 0) = 1
),
Auta AS (
    SELECT fc.ID, fc.LpDostawy, ISNULL(fc.SztukiExcel, 0) AS SztExcel,
           fc.NettoWeight AS Netto, ISNULL(fc.NettoFarmWeight, 0) AS NettoFarm,
           ROW_NUMBER() OVER (PARTITION BY fc.LpDostawy ORDER BY fc.CarLp) AS NrWGrupie,
           COUNT(*) OVER (PARTITION BY fc.LpDostawy) AS WGrupieAut,
           SUM(ISNULL(fc.SztukiExcel, 0)) OVER (PARTITION BY fc.LpDostawy ORDER BY fc.CarLp ROWS BETWEEN UNBOUNDED PRECEDING AND 1 PRECEDING) AS SumExcelPrzed
    FROM dbo.FarmerCalc fc
    WHERE fc.CalcDate = @Data AND ISNULL(fc.Deleted, 0) = 0
),
APlan AS (
    SELECT a.*,
        CASE
            WHEN a.LpDostawy IS NULL OR hd.Lp IS NULL THEN a.NettoFarm
            WHEN a.WGrupieAut > hd.PlanAut THEN CAST(a.SztExcel * hd.WagaDek AS DECIMAL(12,0))
            WHEN a.NrWGrupie = a.WGrupieAut THEN hd.PlanLaczny - ISNULL(a.SumExcelPrzed, 0) * hd.WagaDek
            ELSE CAST(a.SztExcel * hd.WagaDek AS DECIMAL(12,0))
        END AS PlanAuto
    FROM Auta a LEFT JOIN HD hd ON hd.Lp = a.LpDostawy
)
SELECT
    COUNT(*) AS Liczba_Wierszy_DataGrid,
    SUM(PlanAuto) AS SUMA_Plan_per_Auto_DashboardLive,
    SUM(Netto)    AS SUMA_Rzecz_Netto,
    SUM(Netto) - SUM(PlanAuto) AS SUMA_Odchylenie
FROM APlan;

-- =====================================================================
-- KROK 3: SUMA z Excela (per hodowca z harmonogramu)
-- =====================================================================
PRINT '';
PRINT '=== KROK 3: SUMA jak Excel (per hodowca z harmonogramu) ===';
SELECT
    COUNT(*) AS Liczba_Hodowcow,
    SUM(CAST(TRY_CAST(hd.SztukiDek AS INT) * TRY_CAST(hd.WagaDek AS DECIMAL(10,3)) AS DECIMAL(12,2))) AS SUMA_Plan_per_Hodowca_Excel
FROM dbo.HarmonogramDostaw hd
WHERE CAST(hd.DataOdbioru AS DATE) = @Data AND ISNULL(hd.PotwWaga, 0) = 1;

-- =====================================================================
-- KROK 4: BILANS RÓŻNICY 188 740 vs 188 481
-- =====================================================================
PRINT '';
PRINT '=== KROK 4: BILANS RÓŻNICY (rozkład 259 kg) ===';

;WITH HD AS (
    SELECT hd.Lp, hd.Dostawca,
           TRY_CAST(hd.WagaDek AS DECIMAL(10,3)) AS WagaDek,
           ISNULL(TRY_CAST(hd.Auta AS INT), 0) AS PlanAut,
           CAST(ISNULL(TRY_CAST(hd.SztukiDek AS INT), 0) * ISNULL(TRY_CAST(hd.WagaDek AS DECIMAL(10,3)), 0) AS DECIMAL(12,0)) AS PlanLaczny
    FROM dbo.HarmonogramDostaw hd
    WHERE CAST(hd.DataOdbioru AS DATE) = @Data AND ISNULL(hd.PotwWaga, 0) = 1
),
Auta AS (
    SELECT fc.ID, fc.LpDostawy, ISNULL(fc.SztukiExcel, 0) AS SztExcel,
           ISNULL(fc.NettoFarmWeight, 0) AS NettoFarm, fc.NettoWeight AS Netto,
           ROW_NUMBER() OVER (PARTITION BY fc.LpDostawy ORDER BY fc.CarLp) AS NrWGrupie,
           COUNT(*) OVER (PARTITION BY fc.LpDostawy) AS WGrupieAut,
           SUM(ISNULL(fc.SztukiExcel, 0)) OVER (PARTITION BY fc.LpDostawy ORDER BY fc.CarLp ROWS BETWEEN UNBOUNDED PRECEDING AND 1 PRECEDING) AS SumExcelPrzed
    FROM dbo.FarmerCalc fc
    WHERE fc.CalcDate = @Data AND ISNULL(fc.Deleted, 0) = 0
),
APlan AS (
    SELECT a.*, hd.Dostawca, hd.PlanLaczny, hd.WagaDek, hd.PlanAut,
        CASE
            WHEN a.LpDostawy IS NULL OR hd.Lp IS NULL THEN a.NettoFarm
            WHEN a.WGrupieAut > hd.PlanAut THEN CAST(a.SztExcel * hd.WagaDek AS DECIMAL(12,0))
            WHEN a.NrWGrupie = a.WGrupieAut THEN hd.PlanLaczny - ISNULL(a.SumExcelPrzed, 0) * hd.WagaDek
            ELSE CAST(a.SztExcel * hd.WagaDek AS DECIMAL(12,0))
        END AS PlanAuto
    FROM Auta a LEFT JOIN HD hd ON hd.Lp = a.LpDostawy
),
PerLp AS (
    SELECT a.LpDostawy AS Lp, a.Dostawca AS HodowcaHarm,
           a.WGrupieAut AS Aut_realnie, a.PlanAut AS Aut_planowane,
           a.PlanLaczny AS Plan_Excel,
           SUM(a.PlanAuto) AS Plan_Dashboard
    FROM APlan a
    GROUP BY a.LpDostawy, a.Dostawca, a.WGrupieAut, a.PlanAut, a.PlanLaczny
)
SELECT
    'Excel suma' AS Pozycja, NULL AS Lp, NULL AS Hodowca, NULL AS Aut_real, NULL AS Aut_plan,
    (SELECT SUM(PlanLaczny) FROM HD) AS PlanExcel,
    NULL AS PlanDashboard,
    NULL AS RoznicaKg
UNION ALL
SELECT 'Dashboard suma', NULL, NULL, NULL, NULL,
    NULL,
    (SELECT SUM(PlanAuto) FROM APlan),
    NULL
UNION ALL
SELECT '── BILANS RÓŻNICY PER HODOWCA ──', NULL, NULL, NULL, NULL, NULL, NULL, NULL
UNION ALL
SELECT
    CASE WHEN p.Plan_Dashboard - p.Plan_Excel > 0 THEN '⬆ Dashboard więcej'
         WHEN p.Plan_Dashboard - p.Plan_Excel < 0 THEN '⬇ Dashboard mniej'
         ELSE 'OK' END AS Pozycja,
    p.Lp,
    p.HodowcaHarm,
    p.Aut_realnie,
    p.Aut_planowane,
    p.Plan_Excel,
    p.Plan_Dashboard,
    p.Plan_Dashboard - p.Plan_Excel AS RoznicaKg
FROM PerLp p
UNION ALL
SELECT 'Hodowcy w Excelu BEZ AUT', hd.Lp, hd.Dostawca, 0, hd.PlanAut,
       hd.PlanLaczny, 0, -hd.PlanLaczny AS RoznicaKg
FROM HD hd
WHERE NOT EXISTS (SELECT 1 FROM PerLp p WHERE p.Lp = hd.Lp);
