-- ============================================================================
-- DIAGNOZA: porownanie 3 metod liczenia "PLAN KG" na ten sam dzien
--
-- METODA A: Per HODOWCA = SztukiDek × WagaDek z HarmonogramDostaw
--           (Excel uzytkownika, modul Szczegoly Dnia, suma 188 740,08)
--
-- METODA B: Per AUTO = NowyPlanKg z trybu "Nowe" w Dashboardzie LIVE
--           (SztukiExcel × WagaDek dla nie-ostatnich, reszta dla ostatniego)
--
-- METODA C: NettoFarmWeight per auto (deklaracja hodowcy przy odjezdzie)
--
-- Pokazuje per LP i sumy. Roznice = bug "Nowe" w aplikacji?
-- ============================================================================

SET QUOTED_IDENTIFIER ON;
DECLARE @Data DATE = '2026-06-05';

PRINT '=== METODA A: PER HODOWCA z HarmonogramDostaw (jak Excel) ===';
SELECT hd.Lp,
       hd.Dostawca,
       TRY_CAST(hd.SztukiDek AS INT) AS Sztuki,
       TRY_CAST(hd.WagaDek AS DECIMAL(10,3)) AS Waga,
       TRY_CAST(hd.Auta AS INT) AS Plan_Aut,
       CAST(TRY_CAST(hd.SztukiDek AS INT) * TRY_CAST(hd.WagaDek AS DECIMAL(10,3)) AS DECIMAL(12,2)) AS Plan_Kg_HodA
FROM dbo.HarmonogramDostaw hd
WHERE CAST(hd.DataOdbioru AS DATE) = @Data
  AND ISNULL(hd.PotwWaga, 0) = 1   -- tylko potwierdzone (zeby nie liczyc duchow)
ORDER BY hd.Dostawca;

PRINT '';
PRINT '=== METODA A: SUMA per hodowca ===';
SELECT SUM(CAST(TRY_CAST(hd.SztukiDek AS INT) * TRY_CAST(hd.WagaDek AS DECIMAL(10,3)) AS DECIMAL(12,2))) AS SUMA_PlanA_Hodowca
FROM dbo.HarmonogramDostaw hd
WHERE CAST(hd.DataOdbioru AS DATE) = @Data
  AND ISNULL(hd.PotwWaga, 0) = 1;

PRINT '';
PRINT '=== METODA B: PER AUTO z FarmerCalc (SztukiExcel x WagaDekHarmonogram) ===';
;WITH HD AS (
    SELECT hd.Lp,
           hd.Dostawca,
           TRY_CAST(hd.WagaDek AS DECIMAL(10,3)) AS WagaDek,
           TRY_CAST(hd.Auta AS INT) AS Auta,
           CAST(TRY_CAST(hd.SztukiDek AS INT) * TRY_CAST(hd.WagaDek AS DECIMAL(10,3)) AS DECIMAL(12,0)) AS PlanLaczny
    FROM dbo.HarmonogramDostaw hd
    WHERE CAST(hd.DataOdbioru AS DATE) = @Data AND ISNULL(hd.PotwWaga, 0) = 1
),
Auta AS (
    SELECT fc.ID, fc.CarLp AS Nr, fc.LpDostawy, ISNULL(fc.SztukiExcel, 0) AS SztExcel,
           fc.NettoWeight AS Netto, fc.NettoFarmWeight,
           ROW_NUMBER() OVER (PARTITION BY fc.LpDostawy ORDER BY fc.CarLp) AS NrWGrupie,
           COUNT(*) OVER (PARTITION BY fc.LpDostawy) AS WGrupieAut,
           SUM(ISNULL(fc.SztukiExcel, 0)) OVER (PARTITION BY fc.LpDostawy ORDER BY fc.CarLp ROWS BETWEEN UNBOUNDED PRECEDING AND 1 PRECEDING) AS SumExcelPrzed
    FROM dbo.FarmerCalc fc
    WHERE fc.CalcDate = @Data AND ISNULL(fc.Deleted, 0) = 0 AND fc.LpDostawy IS NOT NULL
)
SELECT a.Nr, a.LpDostawy, hd.Dostawca, a.NrWGrupie, a.WGrupieAut, hd.Auta AS PlanAut,
       a.SztExcel, hd.WagaDek,
       CAST(a.SztExcel * hd.WagaDek AS DECIMAL(12,0)) AS PlanB_PerAutoSurowe,
       -- Symulacja trybu "Nowe" z fixem overflow:
       CASE
         WHEN a.WGrupieAut > hd.Auta  -- OVERFLOW: wszystkie wg SztExcel × WagaDek
             THEN CAST(a.SztExcel * hd.WagaDek AS DECIMAL(12,0))
         WHEN a.NrWGrupie = a.WGrupieAut  -- ostatnie = reszta
             THEN hd.PlanLaczny - ISNULL(a.SumExcelPrzed, 0) * hd.WagaDek
         ELSE CAST(a.SztExcel * hd.WagaDek AS DECIMAL(12,0))
       END AS PlanB_NoweWApce,
       a.NettoFarmWeight AS PlanC_NettoFarmWeight,
       a.Netto AS Rzeczywiste
FROM Auta a
LEFT JOIN HD hd ON hd.Lp = a.LpDostawy
ORDER BY a.Nr;

PRINT '';
PRINT '=== SUMA per LP - METODA A vs B vs C ===';
;WITH HD AS (
    SELECT hd.Lp, hd.Dostawca,
           TRY_CAST(hd.WagaDek AS DECIMAL(10,3)) AS WagaDek,
           TRY_CAST(hd.Auta AS INT) AS PlanAut,
           CAST(TRY_CAST(hd.SztukiDek AS INT) * TRY_CAST(hd.WagaDek AS DECIMAL(10,3)) AS DECIMAL(12,0)) AS PlanLaczny
    FROM dbo.HarmonogramDostaw hd
    WHERE CAST(hd.DataOdbioru AS DATE) = @Data AND ISNULL(hd.PotwWaga, 0) = 1
),
Auta AS (
    SELECT fc.LpDostawy, ISNULL(fc.SztukiExcel, 0) AS SztExcel, fc.NettoFarmWeight, fc.NettoWeight,
           ROW_NUMBER() OVER (PARTITION BY fc.LpDostawy ORDER BY fc.CarLp) AS NrWGrupie,
           COUNT(*) OVER (PARTITION BY fc.LpDostawy) AS WGrupieAut,
           SUM(ISNULL(fc.SztukiExcel, 0)) OVER (PARTITION BY fc.LpDostawy ORDER BY fc.CarLp ROWS BETWEEN UNBOUNDED PRECEDING AND 1 PRECEDING) AS SumExcelPrzed
    FROM dbo.FarmerCalc fc
    WHERE fc.CalcDate = @Data AND ISNULL(fc.Deleted, 0) = 0 AND fc.LpDostawy IS NOT NULL
),
AutaZPlanem AS (
    SELECT a.*, hd.Dostawca, hd.PlanLaczny, hd.WagaDek, hd.PlanAut,
        CASE
          WHEN a.WGrupieAut > hd.PlanAut THEN CAST(a.SztExcel * hd.WagaDek AS DECIMAL(12,0))
          WHEN a.NrWGrupie = a.WGrupieAut THEN hd.PlanLaczny - ISNULL(a.SumExcelPrzed, 0) * hd.WagaDek
          ELSE CAST(a.SztExcel * hd.WagaDek AS DECIMAL(12,0))
        END AS PlanB
    FROM Auta a LEFT JOIN HD hd ON hd.Lp = a.LpDostawy
)
SELECT a.LpDostawy AS Lp, a.Dostawca, a.WGrupieAut AS Aut,
       a.PlanLaczny AS PlanA_Hodowca,
       SUM(a.PlanB) AS PlanB_SumaPerAuto,
       SUM(ISNULL(a.NettoFarmWeight, 0)) AS PlanC_NettoFarmSum,
       SUM(a.NettoWeight) AS RzeczNetto,
       a.PlanLaczny - SUM(a.PlanB) AS Roznica_A_minus_B
FROM AutaZPlanem a
GROUP BY a.LpDostawy, a.Dostawca, a.PlanLaczny, a.WGrupieAut
ORDER BY a.Dostawca;

PRINT '';
PRINT '=== SUMA CALKOWITA ===';
;WITH HD AS (
    SELECT hd.Lp,
           TRY_CAST(hd.WagaDek AS DECIMAL(10,3)) AS WagaDek,
           TRY_CAST(hd.Auta AS INT) AS PlanAut,
           CAST(TRY_CAST(hd.SztukiDek AS INT) * TRY_CAST(hd.WagaDek AS DECIMAL(10,3)) AS DECIMAL(12,0)) AS PlanLaczny
    FROM dbo.HarmonogramDostaw hd
    WHERE CAST(hd.DataOdbioru AS DATE) = @Data AND ISNULL(hd.PotwWaga, 0) = 1
),
Auta AS (
    SELECT fc.LpDostawy, ISNULL(fc.SztukiExcel, 0) AS SztExcel, fc.NettoFarmWeight, fc.NettoWeight,
           ROW_NUMBER() OVER (PARTITION BY fc.LpDostawy ORDER BY fc.CarLp) AS NrWGrupie,
           COUNT(*) OVER (PARTITION BY fc.LpDostawy) AS WGrupieAut,
           SUM(ISNULL(fc.SztukiExcel, 0)) OVER (PARTITION BY fc.LpDostawy ORDER BY fc.CarLp ROWS BETWEEN UNBOUNDED PRECEDING AND 1 PRECEDING) AS SumExcelPrzed
    FROM dbo.FarmerCalc fc
    WHERE fc.CalcDate = @Data AND ISNULL(fc.Deleted, 0) = 0 AND fc.LpDostawy IS NOT NULL
)
SELECT
    (SELECT SUM(PlanLaczny) FROM HD) AS METODA_A_HODOWCA,
    (SELECT SUM(
        CASE
          WHEN a.WGrupieAut > hd.PlanAut THEN CAST(a.SztExcel * hd.WagaDek AS DECIMAL(12,0))
          WHEN a.NrWGrupie = a.WGrupieAut THEN hd.PlanLaczny - ISNULL(a.SumExcelPrzed, 0) * hd.WagaDek
          ELSE CAST(a.SztExcel * hd.WagaDek AS DECIMAL(12,0))
        END
     ) FROM Auta a LEFT JOIN HD hd ON hd.Lp = a.LpDostawy) AS METODA_B_PER_AUTO,
    (SELECT SUM(ISNULL(NettoFarmWeight,0)) FROM Auta) AS METODA_C_NETTO_FARM_WEIGHT,
    (SELECT SUM(NettoWeight) FROM Auta) AS RZECZYWISTE_NETTO;
