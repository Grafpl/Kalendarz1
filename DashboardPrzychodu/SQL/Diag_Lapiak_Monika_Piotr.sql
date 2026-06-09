-- ============================================================================
-- MEGA-DIAGNOZA: Łapiak Monika & Łapiak Piotr (05.06.2026)
--
-- OBJAW: w prawej tabeli DWA wiersze pokazują POZOSTAŁO = -12 274 kg
--   • wiersz 3: Łapiak Piotr  / 3/3 aut / +87 kg / Pozost -12 274 kg
--   • wiersz 5: Łapiak Monika / 3/3 aut / +247 kg / Pozost -12 274 kg
-- Plus jest wiersz 2: Łapiak Piotr / 1 aut / +7 kg / Pozost brak
--
-- HIPOTEZA: Pozost.Kg jest właściwością HARMONOGRAMU/LpDostawy (grupa), a w tabeli
-- dziedziczy ją "ostatnie auto każdego realnego hodowcy w grupie". Jak realnych
-- hodowców w grupie jest dwóch (Monika+Piotr) → ta sama wartość jest renderowana
-- DWUKROTNIE. Tu są selecty żeby to potwierdzić w danych.
--
-- DB: LibraNet (192.168.0.109)
-- ============================================================================

SET QUOTED_IDENTIFIER ON;
DECLARE @Data DATE = '2026-06-05';

-- =====================================================================
-- SELECT 1: Harmonogram dostaw — wszystkie pozycje na ten dzień
-- (kto jest w planie, ile aut, jaka waga, czy potwierdzony)
-- =====================================================================
PRINT '=== 1. HARMONOGRAM DOSTAW (caly dzien) ===';
SELECT
    hd.Lp,
    hd.Dostawca,
    hd.DostawcaID,
    TRY_CAST(hd.Auta AS INT)        AS PlanAut,
    TRY_CAST(hd.SztukiDek AS INT)   AS SztukiDek,
    TRY_CAST(hd.WagaDek AS DECIMAL(10,3)) AS WagaDek,
    CAST(TRY_CAST(hd.SztukiDek AS INT) * TRY_CAST(hd.WagaDek AS DECIMAL(10,3)) AS DECIMAL(12,2)) AS PlanLacznyKg,
    hd.PotwWaga, hd.PotwSztuki,
    hd.DataOdbioru,
    hd.DataUtw, hd.DataMod
FROM dbo.HarmonogramDostaw hd
WHERE CAST(hd.DataOdbioru AS DATE) = @Data
ORDER BY hd.Lp;

-- =====================================================================
-- SELECT 2: Harmonogram tylko dla Łapiaków
-- =====================================================================
PRINT '';
PRINT '=== 2. HARMONOGRAM — TYLKO ŁAPIAKI ===';
SELECT
    hd.Lp, hd.Dostawca, hd.DostawcaID,
    TRY_CAST(hd.Auta AS INT)        AS PlanAut,
    TRY_CAST(hd.SztukiDek AS INT)   AS SztukiDek,
    TRY_CAST(hd.WagaDek AS DECIMAL(10,3)) AS WagaDek,
    CAST(TRY_CAST(hd.SztukiDek AS INT) * TRY_CAST(hd.WagaDek AS DECIMAL(10,3)) AS DECIMAL(12,2)) AS PlanLacznyKg,
    hd.PotwWaga, hd.PotwSztuki,
    hd.DataUtw, hd.DataMod
FROM dbo.HarmonogramDostaw hd
WHERE CAST(hd.DataOdbioru AS DATE) = @Data
  AND hd.Dostawca LIKE '%Łapiak%' COLLATE Polish_CI_AI
ORDER BY hd.Lp;

-- =====================================================================
-- SELECT 3: FarmerCalc — wszystkie auta na ten dzień
-- (kto realnie wjechał, do której grupy LpDostawy przypisany)
-- =====================================================================
PRINT '';
PRINT '=== 3. FARMERCALC — WSZYSTKIE AUTA (z hodowcą z Dostawcy) ===';
SELECT
    fc.ID                       AS FarmerCalcID,
    fc.CarLp                    AS NrAuta,
    fc.LpDostawy                AS LpDostawy_Harm,
    fc.CustomerGID,
    LTRIM(RTRIM(d.Name))        AS HodowcaRealny,
    ISNULL(fc.SztukiExcel, 0)   AS SztukiExcel,
    fc.NettoFarmWeight          AS NettoFarmWeight,
    fc.NettoWeight              AS NettoWeight_Rzecz,
    fc.CalcDate, fc.Deleted
FROM dbo.FarmerCalc fc
LEFT JOIN dbo.Dostawcy d ON LTRIM(RTRIM(d.ID)) = LTRIM(RTRIM(fc.CustomerGID))
WHERE fc.CalcDate = @Data AND ISNULL(fc.Deleted, 0) = 0
ORDER BY fc.CarLp;

-- =====================================================================
-- SELECT 4: FarmerCalc — TYLKO Łapiaki (z dodanymi danymi harmonogramu)
-- =====================================================================
PRINT '';
PRINT '=== 4. FARMERCALC — TYLKO ŁAPIAKI z JOIN harmonogram ===';
SELECT
    fc.CarLp                    AS NrAuta,
    fc.LpDostawy                AS LpHarm,
    LTRIM(RTRIM(d.Name))        AS HodowcaRealny_FarmCalc,
    hd.Dostawca                 AS Hodowca_w_Harmonogramie,
    ISNULL(fc.SztukiExcel, 0)   AS SztukiExcel,
    fc.NettoFarmWeight          AS NettoFarmWeight_Deklarowana,
    fc.NettoWeight              AS NettoWeight_Zwazone,
    TRY_CAST(hd.WagaDek AS DECIMAL(10,3))      AS WagaDek_Harm,
    TRY_CAST(hd.SztukiDek AS INT)              AS SztukiDek_Harm,
    TRY_CAST(hd.Auta AS INT)                   AS PlanAut_Harm,
    CAST(ISNULL(fc.SztukiExcel,0) * ISNULL(TRY_CAST(hd.WagaDek AS DECIMAL(10,3)),0) AS DECIMAL(12,2)) AS PlanAuto_StyleDashboard,
    -- czy hodowca z FarmerCalc != hodowca z harmonogramu? (kluczowe!)
    CASE WHEN LTRIM(RTRIM(d.Name)) <> hd.Dostawca THEN '⚠ MIX!' ELSE '' END AS Sygnal_Mixa
FROM dbo.FarmerCalc fc
LEFT JOIN dbo.Dostawcy d ON LTRIM(RTRIM(d.ID)) = LTRIM(RTRIM(fc.CustomerGID))
LEFT JOIN dbo.HarmonogramDostaw hd ON hd.Lp = fc.LpDostawy
WHERE fc.CalcDate = @Data AND ISNULL(fc.Deleted, 0) = 0
  AND (LTRIM(RTRIM(d.Name)) LIKE '%Łapiak%' COLLATE Polish_CI_AI
       OR hd.Dostawca LIKE '%Łapiak%' COLLATE Polish_CI_AI)
ORDER BY fc.CarLp;

-- =====================================================================
-- SELECT 5: AGREGACJA PER GRUPA LpDostawy (per harmonogram)
-- "Pozostało" to atrybut GRUPY: PlanLacznyHarm - SUM(NettoWeight w grupie)
-- =====================================================================
PRINT '';
PRINT '=== 5. AGREGACJA PER GRUPA LpDostawy (kluczowe dla "Pozostało") ===';
;WITH G AS (
    SELECT
        fc.LpDostawy,
        COUNT(*)                                    AS AutaRealnie,
        SUM(ISNULL(fc.SztukiExcel,0))               AS SumSztExcel,
        SUM(ISNULL(fc.NettoWeight,0))               AS SumNettoWeight,
        SUM(ISNULL(fc.NettoFarmWeight,0))           AS SumNettoFarm,
        STUFF((SELECT ', ' + LTRIM(RTRIM(d2.Name))
               FROM dbo.FarmerCalc fc2
               LEFT JOIN dbo.Dostawcy d2 ON LTRIM(RTRIM(d2.ID)) = LTRIM(RTRIM(fc2.CustomerGID))
               WHERE fc2.CalcDate = @Data
                 AND ISNULL(fc2.Deleted,0) = 0
                 AND fc2.LpDostawy = fc.LpDostawy
               ORDER BY fc2.CarLp
               FOR XML PATH('')), 1, 2, '')         AS RealniHodowcy
    FROM dbo.FarmerCalc fc
    WHERE fc.CalcDate = @Data AND ISNULL(fc.Deleted,0) = 0
    GROUP BY fc.LpDostawy
)
SELECT
    g.LpDostawy,
    hd.Dostawca                 AS Harmonogram_Mowi,
    g.RealniHodowcy             AS Realnie_Wjechalo,
    g.AutaRealnie,
    TRY_CAST(hd.Auta AS INT)    AS AutaPlan,
    CASE WHEN g.AutaRealnie > TRY_CAST(hd.Auta AS INT) THEN '⚠ OVERFLOW' ELSE '' END AS Status,
    g.SumSztExcel               AS SumSztExcel_Real,
    TRY_CAST(hd.SztukiDek AS INT) * TRY_CAST(hd.WagaDek AS DECIMAL(10,3)) AS PlanLacznyHarm,
    g.SumNettoFarm              AS SumNettoFarm_Deklarowana,
    g.SumNettoWeight            AS SumNettoWeight_Zwazone,
    -- "Pozostało" tak jak Dashboard liczy:
    (TRY_CAST(hd.SztukiDek AS INT) * TRY_CAST(hd.WagaDek AS DECIMAL(10,3)))
        - g.SumNettoWeight      AS Pozostalo_Plan_minus_Rzecz,
    CASE WHEN g.SumNettoWeight > TRY_CAST(hd.SztukiDek AS INT) * TRY_CAST(hd.WagaDek AS DECIMAL(10,3))
         THEN '⚠ NEGATYWNE Pozost (overflow)' ELSE '' END AS Status_Pozost
FROM G g
LEFT JOIN dbo.HarmonogramDostaw hd
       ON hd.Lp = g.LpDostawy AND CAST(hd.DataOdbioru AS DATE) = @Data
ORDER BY g.LpDostawy;

-- =====================================================================
-- SELECT 6: TYLKO ŁAPIAKI — szczegóły bilansu grupy
-- =====================================================================
PRINT '';
PRINT '=== 6. ŁAPIAK GRUPY (LpDostawy bilans) ===';
;WITH L AS (
    SELECT fc.LpDostawy, fc.CarLp, fc.CustomerGID,
           LTRIM(RTRIM(d.Name)) AS HodowcaReal,
           ISNULL(fc.NettoWeight,0) AS Netto,
           ISNULL(fc.NettoFarmWeight,0) AS NettoFarm,
           ISNULL(fc.SztukiExcel,0) AS SztExcel
    FROM dbo.FarmerCalc fc
    LEFT JOIN dbo.Dostawcy d ON LTRIM(RTRIM(d.ID)) = LTRIM(RTRIM(fc.CustomerGID))
    WHERE fc.CalcDate = @Data AND ISNULL(fc.Deleted,0) = 0
      AND LTRIM(RTRIM(d.Name)) LIKE '%Łapiak%' COLLATE Polish_CI_AI
)
SELECT
    L.LpDostawy,
    hd.Dostawca AS Harm_Mowi,
    COUNT(*) AS Aut_Total,
    SUM(CASE WHEN L.HodowcaReal LIKE '%Monika%' THEN 1 ELSE 0 END) AS AutMoniki,
    SUM(CASE WHEN L.HodowcaReal LIKE '%Piotr%'  THEN 1 ELSE 0 END) AS AutPiotra,
    SUM(L.SztExcel) AS Szt_Excel_Real,
    SUM(L.Netto)    AS KgRzecz_Real,
    SUM(L.NettoFarm) AS KgDeklarowane_Real,
    (TRY_CAST(hd.SztukiDek AS INT) * TRY_CAST(hd.WagaDek AS DECIMAL(10,3))) AS PlanLaczny_Harm,
    (TRY_CAST(hd.SztukiDek AS INT) * TRY_CAST(hd.WagaDek AS DECIMAL(10,3))) - SUM(L.Netto) AS Pozostalo
FROM L
LEFT JOIN dbo.HarmonogramDostaw hd
       ON hd.Lp = L.LpDostawy AND CAST(hd.DataOdbioru AS DATE) = @Data
GROUP BY L.LpDostawy, hd.Dostawca, hd.SztukiDek, hd.WagaDek;

-- =====================================================================
-- SELECT 7: Skok do najgłębszego poziomu — wszystkie auta Łapiaków
-- (po jednym wierszu na auto, plus per-auto plan jak liczy Dashboard)
-- =====================================================================
PRINT '';
PRINT '=== 7. PER AUTO ŁAPIAKÓW (jak w prawej tabeli Dashboardu) ===';
;WITH HD AS (
    SELECT hd.Lp, hd.Dostawca,
           TRY_CAST(hd.WagaDek AS DECIMAL(10,3)) AS WagaDek,
           TRY_CAST(hd.SztukiDek AS INT)        AS SztukiDek,
           TRY_CAST(hd.Auta AS INT)             AS PlanAut,
           CAST(TRY_CAST(hd.SztukiDek AS INT) * TRY_CAST(hd.WagaDek AS DECIMAL(10,3)) AS DECIMAL(12,2)) AS PlanLaczny
    FROM dbo.HarmonogramDostaw hd
    WHERE CAST(hd.DataOdbioru AS DATE) = @Data AND ISNULL(hd.PotwWaga,0) = 1
),
A AS (
    SELECT fc.ID, fc.CarLp AS Nr, fc.LpDostawy, ISNULL(fc.SztukiExcel,0) AS Szt,
           ISNULL(fc.NettoFarmWeight,0) AS NettoFarm,
           ISNULL(fc.NettoWeight,0) AS Netto,
           LTRIM(RTRIM(d.Name)) AS HodowcaReal,
           ROW_NUMBER() OVER (PARTITION BY fc.LpDostawy ORDER BY fc.CarLp) AS Nr_w_grupie,
           COUNT(*)       OVER (PARTITION BY fc.LpDostawy)                  AS Aut_w_grupie,
           SUM(ISNULL(fc.SztukiExcel,0)) OVER (
               PARTITION BY fc.LpDostawy ORDER BY fc.CarLp
               ROWS BETWEEN UNBOUNDED PRECEDING AND 1 PRECEDING)            AS SumSztPrzed
    FROM dbo.FarmerCalc fc
    LEFT JOIN dbo.Dostawcy d ON LTRIM(RTRIM(d.ID)) = LTRIM(RTRIM(fc.CustomerGID))
    WHERE fc.CalcDate = @Data AND ISNULL(fc.Deleted,0) = 0
)
SELECT
    A.Nr, A.LpDostawy,
    A.HodowcaReal       AS HodowcaReal,
    HD.Dostawca         AS HodowcaHarm,
    CASE WHEN A.HodowcaReal <> HD.Dostawca THEN '⚠' ELSE '' END AS MIX,
    A.Nr_w_grupie, A.Aut_w_grupie, HD.PlanAut,
    A.Szt, HD.WagaDek,
    -- Dashboard: per-auto plan
    CASE
        WHEN A.LpDostawy IS NULL OR HD.Lp IS NULL
            THEN A.NettoFarm
        WHEN A.Aut_w_grupie > HD.PlanAut
            THEN CAST(A.Szt * HD.WagaDek AS DECIMAL(12,2))
        WHEN A.Nr_w_grupie = A.Aut_w_grupie
            THEN HD.PlanLaczny - ISNULL(A.SumSztPrzed,0) * HD.WagaDek
        ELSE CAST(A.Szt * HD.WagaDek AS DECIMAL(12,2))
    END AS Plan_per_Auto,
    A.Netto AS Rzecz_per_Auto
FROM A
LEFT JOIN HD ON HD.Lp = A.LpDostawy
WHERE A.HodowcaReal LIKE '%Łapiak%' COLLATE Polish_CI_AI
   OR HD.Dostawca   LIKE '%Łapiak%' COLLATE Polish_CI_AI
ORDER BY A.Nr;

-- =====================================================================
-- SELECT 8: Z perspektywy KART po lewej (per harmonogram)
-- = co Dashboard ładuje do "PostepHarmonogramu" (kart hodowców)
-- =====================================================================
PRINT '';
PRINT '=== 8. KARTY HARMONOGRAMU (lewa strona dashboardu) ===';
;WITH ARealni AS (
    SELECT fc.LpDostawy,
           STUFF((SELECT ', ' + LTRIM(RTRIM(d2.Name))
                  FROM dbo.FarmerCalc fc2
                  LEFT JOIN dbo.Dostawcy d2 ON LTRIM(RTRIM(d2.ID)) = LTRIM(RTRIM(fc2.CustomerGID))
                  WHERE fc2.CalcDate = @Data
                    AND ISNULL(fc2.Deleted,0) = 0
                    AND fc2.LpDostawy = fc.LpDostawy
                  ORDER BY fc2.CarLp
                  FOR XML PATH('')), 1, 2, '') AS RealniHodowcy,
           COUNT(*)                    AS AutaRealnie,
           SUM(ISNULL(fc.NettoWeight,0))     AS KgRzecz,
           SUM(ISNULL(fc.NettoFarmWeight,0)) AS KgDekl
    FROM dbo.FarmerCalc fc
    WHERE fc.CalcDate = @Data AND ISNULL(fc.Deleted,0) = 0
    GROUP BY fc.LpDostawy
)
SELECT
    hd.Lp,
    hd.Dostawca                 AS HodowcaPlan,
    ISNULL(a.RealniHodowcy, '— brak aut —') AS RealniHodowcy,
    TRY_CAST(hd.Auta AS INT)    AS PlanAut,
    ISNULL(a.AutaRealnie,0)     AS RealAut,
    CASE
        WHEN ISNULL(a.AutaRealnie,0) = 0 THEN 'ZAPLANOWANY, NIE WJECHAŁ'
        WHEN a.AutaRealnie > TRY_CAST(hd.Auta AS INT) THEN 'OVERFLOW (+aut)'
        WHEN a.AutaRealnie < TRY_CAST(hd.Auta AS INT) THEN 'NIEDOMIAR (-aut)'
        ELSE 'OK'
    END                         AS Status,
    CAST(TRY_CAST(hd.SztukiDek AS INT) * TRY_CAST(hd.WagaDek AS DECIMAL(10,3)) AS DECIMAL(12,2)) AS PlanKg,
    ISNULL(a.KgRzecz,0)         AS RzeczKg,
    ISNULL(a.KgRzecz,0) -
      CAST(TRY_CAST(hd.SztukiDek AS INT) * TRY_CAST(hd.WagaDek AS DECIMAL(10,3)) AS DECIMAL(12,2)) AS OdchylKg
FROM dbo.HarmonogramDostaw hd
LEFT JOIN ARealni a ON a.LpDostawy = hd.Lp
WHERE CAST(hd.DataOdbioru AS DATE) = @Data
  AND hd.Dostawca LIKE '%Łapiak%' COLLATE Polish_CI_AI
ORDER BY hd.Lp;

-- =====================================================================
-- SELECT 9: Sygnał alarmowy — czy istnieją AUTA bez LpDostawy
-- (czyli auta które nie są przypisane do żadnego harmonogramu)
-- =====================================================================
PRINT '';
PRINT '=== 9. AUTA BEZ LpDostawy (sieroty) ===';
SELECT
    fc.CarLp, fc.ID, fc.LpDostawy,
    LTRIM(RTRIM(d.Name)) AS HodowcaReal,
    fc.SztukiExcel, fc.NettoFarmWeight, fc.NettoWeight,
    fc.CalcDate
FROM dbo.FarmerCalc fc
LEFT JOIN dbo.Dostawcy d ON LTRIM(RTRIM(d.ID)) = LTRIM(RTRIM(fc.CustomerGID))
WHERE fc.CalcDate = @Data
  AND ISNULL(fc.Deleted,0) = 0
  AND fc.LpDostawy IS NULL
ORDER BY fc.CarLp;

-- =====================================================================
-- SELECT 10: MATRYCA — historia importu z AVILOG dla CalcDate=@Data
-- =====================================================================
PRINT '';
PRINT '=== 10. MATRYCATRANSFERLOG dla @Data (kiedy/ile rekordow zaimportowano) ===';
IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'MatrycaTransferLog')
BEGIN
    SELECT TOP 50 *
    FROM dbo.MatrycaTransferLog m
    WHERE m.CalcDate = @Data
    ORDER BY m.TransferDate DESC;
END
ELSE
BEGIN
    PRINT 'Tabela MatrycaTransferLog nie istnieje — pomijam';
END

-- =====================================================================
-- SELECT 10b: ChangeLog dla Łapiaków (jesli wdrozony trigger)
-- =====================================================================
PRINT '';
PRINT '=== 10b. HARMONOGRAM CHANGE LOG dla Łapiakow ===';
IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'HarmonogramDostaw_ChangeLog')
BEGIN
    SELECT TOP 200 *
    FROM dbo.HarmonogramDostaw_ChangeLog
    WHERE DataOdbioru = @Data
      AND Dostawca LIKE '%Łapiak%' COLLATE Polish_CI_AI
    ORDER BY ChangedAt DESC;
END
ELSE
BEGIN
    PRINT 'Tabela HarmonogramDostaw_ChangeLog nie istnieje — uruchom CreateHarmonogramChangeLog.sql';
END

-- =====================================================================
-- PODSUMOWANIE: ODPOWIEDŹ NA "DLACZEGO -12 274 KG WIDAĆ DWA RAZY"
-- =====================================================================
PRINT '';
PRINT '=== 11. BUSINESS BILANS ŁAPIAKÓW ===';
;WITH GrupaLapiak AS (
    SELECT fc.LpDostawy,
           SUM(ISNULL(fc.NettoWeight,0)) AS KgRzeczGrupa
    FROM dbo.FarmerCalc fc
    LEFT JOIN dbo.Dostawcy d ON LTRIM(RTRIM(d.ID)) = LTRIM(RTRIM(fc.CustomerGID))
    WHERE fc.CalcDate = @Data AND ISNULL(fc.Deleted,0) = 0
      AND LTRIM(RTRIM(d.Name)) LIKE '%Łapiak%' COLLATE Polish_CI_AI
    GROUP BY fc.LpDostawy
)
SELECT
    hd.Lp,
    hd.Dostawca                                     AS HarmonogramKto,
    g.KgRzeczGrupa,
    CAST(TRY_CAST(hd.SztukiDek AS INT) * TRY_CAST(hd.WagaDek AS DECIMAL(10,3)) AS DECIMAL(12,2)) AS PlanLacznyHarm,
    CAST(TRY_CAST(hd.SztukiDek AS INT) * TRY_CAST(hd.WagaDek AS DECIMAL(10,3)) AS DECIMAL(12,2))
       - g.KgRzeczGrupa                             AS Pozostalo_Grupy
FROM GrupaLapiak g
LEFT JOIN dbo.HarmonogramDostaw hd
       ON hd.Lp = g.LpDostawy AND CAST(hd.DataOdbioru AS DATE) = @Data;

PRINT '';
PRINT '=== INTERPRETACJA ===';
PRINT 'Jeśli SELECT 5/6 pokazuje że LpDostawy = X ma SumNettoWeight większe od PlanLaczny';
PRINT 'a SELECT 4 pokazuje że w grupie są obaj realni hodowcy (Monika i Piotr),';
PRINT 'to "Pozostało" -12 274 kg jest właściwością GRUPY harmonogramu, a Dashboard';
PRINT 'wyświetla ją na ostatnim aucie KAŻDEGO realnego hodowcy w grupie → 2x render.';
PRINT '';
PRINT 'FIX (jeśli to mylące): pokazać "Pozostało" tylko na ostatnim aucie CAŁEJ grupy.';
PRINT 'Alternatywa: rozdzielić proporcjonalnie między realnych hodowców.';
