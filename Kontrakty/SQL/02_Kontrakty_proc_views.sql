-- ════════════════════════════════════════════════════════════════════════════
-- KONTRAKTY HODOWCÓW — PROCEDURA + WIDOKI (część 2)
-- Uruchom, gdy tabele z 01_Kontrakty_v2_schema.sql już istnieją, ale procedura/widoki
-- nie powstały (np. przez błąd batchowania GO). CREATE OR ALTER = bezpieczne wielokrotnie.
-- Target: LibraNet (192.168.0.109)
-- ════════════════════════════════════════════════════════════════════════════
USE LibraNet;
GO

-- ── 1. Procedura: następny numer kontraktu (atomowo, reset roczny) ──────────
CREATE OR ALTER PROCEDURE dbo.sp_KontraktyNastepnyNumer
    @Rok SMALLINT,
    @NumerOut VARCHAR(20) OUTPUT,
    @LpOut INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRANSACTION;
        DECLARE @lp INT = ISNULL((SELECT MAX(LpRoku) FROM dbo.Kontrakty WITH (TABLOCKX) WHERE Rok = @Rok), 0) + 1;
        SET @LpOut = @lp;
        SET @NumerOut = CAST(@lp AS VARCHAR(10)) + '/' + RIGHT(CAST(@Rok AS VARCHAR(4)), 2);
    COMMIT TRANSACTION;
END;
GO
PRINT '✅ dbo.sp_KontraktyNastepnyNumer';
GO

-- ── 2. VIEW: płaska lista do grida (nagłówek + AKTUALNA wersja) ─────────────
CREATE OR ALTER VIEW dbo.v_KontraktyAktualne AS
SELECT
    k.Id, k.NumerKontraktu, k.DostawcaId,
    ISNULL(k.NazwaHodowcySnapshot, d.Name) AS Hodowca,
    k.TypKontraktu, k.LiczySieDoArimr, k.Podmiot,
    w.Id              AS WersjaId,
    w.NrWersji,
    w.Status,
    w.DataPodpisania, w.ObowiazujeOd, w.ObowiazujeDo,
    w.ProcentUbytku, w.TypCeny, w.Cena, w.TerminPlatnosciDni,
    w.SciezkaWord, w.SciezkaPdfSkan,
    CASE WHEN w.ObowiazujeDo IS NULL THEN NULL
         ELSE DATEDIFF(DAY, CAST(GETDATE() AS DATE), w.ObowiazujeDo) END AS DniDoWygasniecia,
    k.UtworzylUserId, k.UtworzylKiedy,
    ISNULL(NULLIF(LTRIM(RTRIM(d.Nip)),''),    '') AS HodowcaNip,
    ISNULL(NULLIF(LTRIM(RTRIM(d.AnimNo)),''), '') AS HodowcaGospodarstwo
FROM dbo.Kontrakty k
LEFT JOIN dbo.KontraktyWersje w ON w.KontraktId = k.Id AND w.IsAktualna = 1
LEFT JOIN dbo.Dostawcy d ON d.ID = k.DostawcaId;
GO
PRINT '✅ dbo.v_KontraktyAktualne';
GO

-- ── 3. VIEW: ARiMR compliance (% surowca pod aktywnym 3-letnim, 12 mies.) ──
CREATE OR ALTER VIEW dbo.v_ArimrCompliance AS
WITH Okres AS (
    SELECT DATEADD(MONTH, -12, CAST(GETDATE() AS DATE)) AS Od, CAST(GETDATE() AS DATE) AS Do
),
Calosc AS (
    SELECT
        ISNULL(SUM(ISNULL(fc.NettoFarmWeight, ISNULL(fc.FullFarmWeight,0) - ISNULL(fc.EmptyFarmWeight,0))), 0) AS WagaKg,
        COUNT(DISTINCT LTRIM(RTRIM(fc.CustomerGID))) AS Hodowcow
    FROM dbo.FarmerCalc fc CROSS JOIN Okres o
    WHERE fc.CalcDate BETWEEN o.Od AND o.Do
),
Arimr AS (
    SELECT
        ISNULL(SUM(ISNULL(fc.NettoFarmWeight, ISNULL(fc.FullFarmWeight,0) - ISNULL(fc.EmptyFarmWeight,0))), 0) AS WagaKg,
        COUNT(DISTINCT LTRIM(RTRIM(fc.CustomerGID))) AS Hodowcow
    FROM dbo.FarmerCalc fc CROSS JOIN Okres o
    WHERE fc.CalcDate BETWEEN o.Od AND o.Do
      AND EXISTS (
        SELECT 1
        FROM dbo.Kontrakty k
        JOIN dbo.KontraktyWersje w ON w.KontraktId = k.Id
        WHERE k.DostawcaId = LTRIM(RTRIM(fc.CustomerGID))
          AND k.LiczySieDoArimr = 1
          AND w.Status IN ('ACTIVE','EXPIRING','SIGNED')
          AND w.ObowiazujeOd <= fc.CalcDate
          AND (w.ObowiazujeDo IS NULL OR w.ObowiazujeDo >= fc.CalcDate)
      )
)
SELECT
    c.WagaKg AS SurowiecCaloscKg,
    a.WagaKg AS SurowiecArimrKg,
    c.Hodowcow AS HodowcowOgolem,
    a.Hodowcow AS HodowcowArimr,
    CAST(CASE WHEN c.WagaKg = 0 THEN 0 ELSE a.WagaKg * 100.0 / c.WagaKg END AS DECIMAL(5,2)) AS ProcentArimr,
    CASE
        WHEN c.WagaKg = 0 THEN 'BRAK_DANYCH'
        WHEN a.WagaKg * 100.0 / c.WagaKg >= 50 THEN 'OK'
        WHEN a.WagaKg * 100.0 / c.WagaKg >= 45 THEN 'WARN'
        ELSE 'CRIT'
    END AS Status,
    GETDATE() AS WyliczonoKiedy
FROM Calosc c CROSS JOIN Arimr a;
GO
PRINT '✅ dbo.v_ArimrCompliance';
GO

-- ── 4. VIEW: hodowcy z dostawami (12 mies.) BEZ aktywnego kontraktu ─────────
CREATE OR ALTER VIEW dbo.v_HodowcyBezKontraktu AS
WITH Okres AS (
    SELECT DATEADD(MONTH, -12, CAST(GETDATE() AS DATE)) AS Od, CAST(GETDATE() AS DATE) AS Do
),
Dostawy AS (
    SELECT
        LTRIM(RTRIM(fc.CustomerGID)) AS DostawcaId,
        COUNT(*) AS LiczbaDostaw,
        SUM(ISNULL(fc.NettoFarmWeight, ISNULL(fc.FullFarmWeight,0) - ISNULL(fc.EmptyFarmWeight,0))) AS WagaKg12m,
        MAX(fc.CalcDate) AS OstatniaDostawa
    FROM dbo.FarmerCalc fc CROSS JOIN Okres o
    WHERE fc.CalcDate BETWEEN o.Od AND o.Do AND fc.CustomerGID IS NOT NULL
    GROUP BY LTRIM(RTRIM(fc.CustomerGID))
)
SELECT
    dst.DostawcaId,
    ISNULL(d.Name, '(brak nazwy)') AS Hodowca,
    dst.LiczbaDostaw, dst.WagaKg12m, dst.OstatniaDostawa
FROM Dostawy dst
LEFT JOIN dbo.Dostawcy d ON d.ID = dst.DostawcaId
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.Kontrakty k
    JOIN dbo.KontraktyWersje w ON w.KontraktId = k.Id AND w.IsAktualna = 1
    WHERE k.DostawcaId = dst.DostawcaId
      AND w.Status IN ('ACTIVE','EXPIRING','SIGNED')
);
GO
PRINT '✅ dbo.v_HodowcyBezKontraktu';
GO

-- ── Smoke test ──────────────────────────────────────────────────────────────
DECLARE @num VARCHAR(20), @lp INT;
EXEC dbo.sp_KontraktyNastepnyNumer @Rok = 2027, @NumerOut = @num OUTPUT, @LpOut = @lp OUTPUT;
PRINT 'sp_KontraktyNastepnyNumer(2027) → ' + @num;
SELECT TABLE_NAME AS Widok FROM INFORMATION_SCHEMA.VIEWS
WHERE TABLE_NAME IN ('v_KontraktyAktualne','v_ArimrCompliance','v_HodowcyBezKontraktu');
PRINT '✅ Część 2 (procedura + widoki) gotowa.';
GO
