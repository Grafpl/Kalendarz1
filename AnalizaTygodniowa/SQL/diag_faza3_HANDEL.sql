-- ════════════════════════════════════════════════════════════════════
-- FAZA 3 — HANDEL (192.168.0.112)
-- Cel: poprawić poprzednie błędy + zebrać szczegółowe dane
-- Wszędzie używam ABS(ilosc) lub przychod/rozchod kolumnę
-- ════════════════════════════════════════════════════════════════════
USE HANDEL;
SET NOCOUNT ON;
GO

-- ─────────────────────────────────────────────────────────────────
-- 1. ⭐ POPRAWIONY BILANS UBOJU za ostatnie 7 dni
-- ─────────────────────────────────────────────────────────────────
PRINT '═══ H3-1. Bilans uboju 7 dni (z ABS) ═══';
WITH Zywiec AS (
    SELECT
        CAST(MG.data AS DATE) AS Dzien,
        SUM(ABS(MZ.ilosc)) AS ZywiecKg
    FROM HM.MZ MZ
    JOIN HM.MG MG ON MG.id = MZ.super
    JOIN HM.TW TW ON TW.id = MZ.idtw
    WHERE MG.seria = 'sPZ'
      AND TW.kod LIKE 'Kurczak żywy%'
      AND MG.data >= DATEADD(DAY, -7, GETDATE())
      AND MG.anulowany = 0
      AND MG.magazyn = 65554
    GROUP BY CAST(MG.data AS DATE)
),
ProdukcjaPW AS (
    SELECT
        CAST(MG.data AS DATE) AS Dzien,
        SUM(ABS(MZ.ilosc)) AS ProdPwKg
    FROM HM.MZ MZ
    JOIN HM.MG MG ON MG.id = MZ.super
    JOIN HM.TW TW ON TW.id = MZ.idtw
    WHERE MG.seria IN ('sPWU', 'PWP')
      AND TW.katalog = 67095
      AND MG.data >= DATEADD(DAY, -7, GETDATE())
      AND MG.anulowany = 0
    GROUP BY CAST(MG.data AS DATE)
),
RozchodKrojenie AS (
    SELECT
        CAST(MG.data AS DATE) AS Dzien,
        SUM(ABS(MZ.ilosc)) AS RozKg
    FROM HM.MZ MZ
    JOIN HM.MG MG ON MG.id = MZ.super
    WHERE MG.seria = 'RWP'
      AND MG.data >= DATEADD(DAY, -7, GETDATE())
      AND MG.anulowany = 0
    GROUP BY CAST(MG.data AS DATE)
),
Sprzedaz AS (
    SELECT
        CAST(DK.data AS DATE) AS Dzien,
        SUM(DP.ilosc) AS SprzKg
    FROM HM.DK DK
    JOIN HM.DP DP ON DP.super = DK.id
    JOIN HM.TW TW ON TW.id = DP.idtw
    WHERE DK.anulowany = 0
      AND TW.katalog = 67095
      AND DK.data >= DATEADD(DAY, -7, GETDATE())
    GROUP BY CAST(DK.data AS DATE)
)
SELECT
    COALESCE(Z.Dzien, P.Dzien, R.Dzien, S.Dzien) AS Dzien,
    Z.ZywiecKg, P.ProdPwKg, R.RozKg, S.SprzKg,
    -- uzysk: ProdPw / Zywiec
    CASE WHEN Z.ZywiecKg > 0
         THEN CAST(P.ProdPwKg * 100.0 / Z.ZywiecKg AS DECIMAL(5,1)) END AS Uzysk_proc
FROM Zywiec Z
FULL OUTER JOIN ProdukcjaPW P ON P.Dzien = Z.Dzien
FULL OUTER JOIN RozchodKrojenie R ON R.Dzien = COALESCE(Z.Dzien, P.Dzien)
FULL OUTER JOIN Sprzedaz S ON S.Dzien = COALESCE(Z.Dzien, P.Dzien, R.Dzien)
ORDER BY Dzien DESC;
GO

-- ─────────────────────────────────────────────────────────────────
-- 2. ⭐ PRODUKCJA per produkt (filtrowanie po kod, nie nazwa)
-- ─────────────────────────────────────────────────────────────────
PRINT '═══ H3-2. Produkcja per produkt 14 dni — filtrujemy po kod ═══';
SELECT
    CAST(MG.data AS DATE) AS Dzien,
    TW.kod, TW.nazwa,
    SUM(ABS(MZ.ilosc)) AS ProdKg
FROM HM.MZ MZ
JOIN HM.MG MG ON MG.id = MZ.super
JOIN HM.TW TW ON TW.id = MZ.idtw
WHERE MG.seria IN ('sPWU', 'PWP')
  AND TW.katalog = 67095
  AND MG.data >= DATEADD(DAY, -14, GETDATE())
  AND MG.anulowany = 0
GROUP BY CAST(MG.data AS DATE), TW.kod, TW.nazwa
ORDER BY Dzien DESC, ProdKg DESC;
GO

-- ─────────────────────────────────────────────────────────────────
-- 3. ⭐ SPRZEDAŻ per produkt 14 dni — łącznie kg, średnia cena, marża szac.
-- ─────────────────────────────────────────────────────────────────
PRINT '═══ H3-3. Sprzedaż per produkt + szacunkowa marża ═══';
SELECT
    TW.kod,
    TW.nazwa,
    COUNT(*) AS Wierszy,
    SUM(DP.ilosc) AS Kg,
    AVG(DP.cena) AS SrCena,
    SUM(DP.ilosc * DP.cena) AS Wartosc_zl,
    SUM(DP.kosztAproksymowany) AS KosztApr_zl,
    -- marża szacunkowa (tylko gdy koszt > 0 i sensowny)
    CASE WHEN SUM(DP.kosztAproksymowany) > 0
         THEN CAST((SUM(DP.ilosc * DP.cena) - SUM(DP.kosztAproksymowany)) * 100.0
                  / NULLIF(SUM(DP.ilosc * DP.cena), 0) AS DECIMAL(5,1)) END AS MarzaProc_szac,
    -- ile wierszy ma sensowny koszt
    SUM(CASE WHEN DP.kosztAproksymowany > 0 THEN 1 ELSE 0 END) AS WierszyZeKosztem
FROM HM.DK DK
JOIN HM.DP DP ON DP.super = DK.id
JOIN HM.TW TW ON TW.id = DP.idtw
WHERE DK.anulowany = 0
  AND TW.katalog = 67095
  AND DK.data >= DATEADD(DAY, -14, GETDATE())
  AND DP.ilosc > 0
GROUP BY TW.kod, TW.nazwa
ORDER BY Kg DESC;
GO

-- ─────────────────────────────────────────────────────────────────
-- 4. TOP 30 KLIENTÓW 30 dni — kto kupuje najwięcej
-- ─────────────────────────────────────────────────────────────────
PRINT '═══ H3-4. Top 30 klientów 30 dni ═══';
SELECT TOP 30
    K.id, K.Shortcut, K.Name,
    CC.CDim_Handlowiec_Val AS Handlowiec,
    K.NIP,
    COUNT(DISTINCT DK.id) AS Faktur,
    SUM(DP.ilosc) AS Kg,
    SUM(DP.ilosc * DP.cena) AS Wartosc_zl,
    AVG(DP.cena) AS SrCena
FROM HM.DK DK
JOIN HM.DP DP ON DP.super = DK.id
JOIN HM.TW TW ON TW.id = DP.idtw
JOIN SSCommon.STContractors K ON K.Id = DK.khid
LEFT JOIN SSCommon.ContractorClassification CC ON CC.ElementId = K.MainElement
WHERE DK.anulowany = 0
  AND TW.katalog = 67095
  AND DK.data >= DATEADD(DAY, -30, GETDATE())
  AND DP.ilosc > 0
GROUP BY K.id, K.Shortcut, K.Name, K.NIP, CC.CDim_Handlowiec_Val
ORDER BY Wartosc_zl DESC;
GO

-- ─────────────────────────────────────────────────────────────────
-- 5. ⭐ TOP 50 DOSTAWCÓW ŻYWCA 30 dni — kg + udział %
-- ─────────────────────────────────────────────────────────────────
PRINT '═══ H3-5. Top 50 dostawców żywca 30 dni ═══';
WITH Calosc AS (
    SELECT SUM(ABS(MZ.ilosc)) AS LacznieKg
    FROM HM.MZ MZ
    JOIN HM.MG MG ON MG.id = MZ.super
    JOIN HM.TW TW ON TW.id = MZ.idtw
    WHERE MG.seria = 'sPZ'
      AND TW.kod LIKE 'Kurczak żywy%'
      AND MG.data >= DATEADD(DAY, -30, GETDATE())
      AND MG.anulowany = 0
)
SELECT TOP 50
    K.id, K.Shortcut, K.Name, K.NIP,
    COUNT(DISTINCT MG.id) AS Dostaw,
    SUM(ABS(MZ.ilosc)) AS Kg,
    AVG(ABS(MZ.ilosc)) AS SrDostawa_kg,
    CAST(SUM(ABS(MZ.ilosc)) * 100.0 / (SELECT LacznieKg FROM Calosc) AS DECIMAL(5,2)) AS Udzial_proc,
    MIN(MG.data) AS PierwszaDost, MAX(MG.data) AS OstatniaDost
FROM HM.MZ MZ
JOIN HM.MG MG ON MG.id = MZ.super
JOIN HM.TW TW ON TW.id = MZ.idtw
JOIN SSCommon.STContractors K ON K.Id = MG.khid
WHERE MG.seria = 'sPZ'
  AND TW.kod LIKE 'Kurczak żywy%'
  AND MG.data >= DATEADD(DAY, -30, GETDATE())
  AND MG.anulowany = 0
GROUP BY K.id, K.Shortcut, K.Name, K.NIP
ORDER BY Kg DESC;
GO

-- ─────────────────────────────────────────────────────────────────
-- 6. SPRZEDAŻ per HANDLOWIEC 30 dni
-- ─────────────────────────────────────────────────────────────────
PRINT '═══ H3-6. Sprzedaż per handlowiec ═══';
SELECT
    ISNULL(CC.CDim_Handlowiec_Val, '(BRAK)') AS Handlowiec,
    COUNT(DISTINCT DK.id) AS Faktur,
    COUNT(DISTINCT DK.khid) AS Klientow,
    SUM(DP.ilosc) AS Kg,
    SUM(DP.ilosc * DP.cena) AS Wartosc_zl,
    AVG(DP.cena) AS SrCena
FROM HM.DK DK
JOIN HM.DP DP ON DP.super = DK.id
JOIN HM.TW TW ON TW.id = DP.idtw
JOIN SSCommon.STContractors K ON K.Id = DK.khid
LEFT JOIN SSCommon.ContractorClassification CC ON CC.ElementId = K.MainElement
WHERE DK.anulowany = 0
  AND TW.katalog = 67095
  AND DK.data >= DATEADD(DAY, -30, GETDATE())
  AND DP.ilosc > 0
GROUP BY CC.CDim_Handlowiec_Val
ORDER BY Wartosc_zl DESC;
GO

-- ─────────────────────────────────────────────────────────────────
-- 7. ŚREDNIA CENA SPRZEDAŻY per dzień (vs CenaTuszki rynkowej)
-- ─────────────────────────────────────────────────────────────────
PRINT '═══ H3-7. Średnia cena sprzedaży tuszki Kurczak A per dzień ═══';
SELECT
    CAST(DK.data AS DATE) AS Dzien,
    TW.kod,
    SUM(DP.ilosc) AS Kg,
    SUM(DP.ilosc * DP.cena) AS Wart,
    CAST(SUM(DP.ilosc * DP.cena) / NULLIF(SUM(DP.ilosc), 0) AS DECIMAL(8,2)) AS SrCena_zl_kg
FROM HM.DK DK
JOIN HM.DP DP ON DP.super = DK.id
JOIN HM.TW TW ON TW.id = DP.idtw
WHERE DK.anulowany = 0
  AND TW.katalog = 67095
  AND TW.kod IN ('Kurczak A', 'Korpus', 'Filet A', 'Ćwiartka')
  AND DK.data >= DATEADD(DAY, -30, GETDATE())
  AND DP.ilosc > 0
GROUP BY CAST(DK.data AS DATE), TW.kod
ORDER BY Dzien DESC, TW.kod;
GO

-- ─────────────────────────────────────────────────────────────────
-- 8. NIESPRZEDANE — co produkujemy ale nie sprzedajemy?
-- ─────────────────────────────────────────────────────────────────
PRINT '═══ H3-8. Bilans 14-dniowy: produkcja vs sprzedaż per produkt ═══';
WITH Prod AS (
    SELECT TW.id, TW.kod, TW.nazwa, SUM(ABS(MZ.ilosc)) AS ProdKg
    FROM HM.MZ MZ
    JOIN HM.MG MG ON MG.id = MZ.super
    JOIN HM.TW TW ON TW.id = MZ.idtw
    WHERE MG.seria IN ('sPWU', 'PWP')
      AND TW.katalog = 67095
      AND MG.data >= DATEADD(DAY, -14, GETDATE())
      AND MG.anulowany = 0
    GROUP BY TW.id, TW.kod, TW.nazwa
),
Sprz AS (
    SELECT TW.id, SUM(DP.ilosc) AS SprzKg
    FROM HM.DK DK
    JOIN HM.DP DP ON DP.super = DK.id
    JOIN HM.TW TW ON TW.id = DP.idtw
    WHERE DK.anulowany = 0
      AND TW.katalog = 67095
      AND DK.data >= DATEADD(DAY, -14, GETDATE())
      AND DP.ilosc > 0
    GROUP BY TW.id
)
SELECT
    P.kod, P.nazwa,
    P.ProdKg,
    ISNULL(S.SprzKg, 0) AS SprzKg,
    P.ProdKg - ISNULL(S.SprzKg, 0) AS Saldo_kg,
    CASE WHEN P.ProdKg > 0
         THEN CAST(ISNULL(S.SprzKg, 0) * 100.0 / P.ProdKg AS DECIMAL(5,1)) END AS Procent_zsprzedanego
FROM Prod P
LEFT JOIN Sprz S ON S.id = P.id
ORDER BY Saldo_kg DESC;
GO

-- ─────────────────────────────────────────────────────────────────
-- 9. PIĄTEK / WEEKEND — co zostało z piątkowej produkcji
-- ─────────────────────────────────────────────────────────────────
PRINT '═══ H3-9. Piątkowy bilans (4 ostatnie piątki) ═══';
WITH Piatki AS (
    SELECT DISTINCT TOP 4 CAST(MG.data AS DATE) AS Piatek
    FROM HM.MG
    WHERE MG.seria IN ('sPWU', 'PWP')
      AND DATEPART(WEEKDAY, MG.data) = 6  -- piątek (gdy pierwszy dzień tygodnia = niedziela)
      AND MG.data <= GETDATE()
    ORDER BY CAST(MG.data AS DATE) DESC
)
SELECT
    P.Piatek,
    SUM(CASE WHEN MG.seria IN ('sPWU','PWP') THEN ABS(MZ.ilosc) ELSE 0 END) AS ProdKg_piatek,
    SUM(CASE WHEN MG.seria = 'sWZ' THEN ABS(MZ.ilosc) ELSE 0 END) AS WydaneKg_piatek,
    -- co zostało:
    SUM(CASE WHEN MG.seria IN ('sPWU','PWP') THEN ABS(MZ.ilosc) ELSE 0 END) -
    SUM(CASE WHEN MG.seria = 'sWZ' THEN ABS(MZ.ilosc) ELSE 0 END) AS Niesprzedane_kg
FROM Piatki P
JOIN HM.MG MG ON CAST(MG.data AS DATE) = P.Piatek
JOIN HM.MZ MZ ON MZ.super = MG.id
JOIN HM.TW TW ON TW.id = MZ.idtw
WHERE MG.anulowany = 0 AND TW.katalog = 67095
GROUP BY P.Piatek
ORDER BY P.Piatek DESC;
GO

-- ─────────────────────────────────────────────────────────────────
-- 10. SPRAWDZENIE: czy istnieje tabela mapowania kontrahentów Symfonia↔CRM
-- ─────────────────────────────────────────────────────────────────
PRINT '═══ H3-10. Sprawdź mapy kontrahentów (zewnętrzne ID) ═══';
SELECT TOP 5 *
FROM SSCommon.STContractors
WHERE LinkedUnit = 1 OR MainElement IS NOT NULL;
GO

PRINT '═══ KONIEC HANDEL faza 3 ═══';
