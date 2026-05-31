-- ============================================================================
-- Dashboard Przychod Zywca - faktyczny przychod z systemu Symfonia
-- Baza: HANDEL (cross-DB, osobny connection string)
-- Seria sPWU = Przychod Wewnetrzny Uboju
-- Klasy A/B identyfikowane po kodzie towaru (TW.kod LIKE '%Kurczak A/B%')
-- Katalogi: 67095 (Mieso swieze), 67153 (Mrozone)
--
-- Gotchas (CLAUDE.md sek. 4):
--   * MG.anulowany / aktywny - filtrujemy aktywne
--   * ABS(MZ.ilosc) - znaki niespojne
--   * MG.data jest datetime -> CAST do DATE w WHERE BETWEEN, tu = bezpieczne
--
-- Parametry: @Data (DATE)
-- ============================================================================

SELECT
    CASE
        WHEN TW.kod LIKE '%Kurczak A%' THEN 'A'
        WHEN TW.kod LIKE '%Kurczak B%' THEN 'B'
        ELSE 'X'
    END AS Klasa,
    SUM(ABS(MZ.ilosc)) AS Ilosc
FROM [HM].[MZ] MZ
JOIN [HM].[MG] MG ON MZ.super = MG.id
JOIN [HM].[TW] TW ON MZ.idtw = TW.ID
WHERE MG.seria = 'sPWU'
  AND MG.aktywny = 1
  AND MG.data = @Data
  AND TW.katalog IN (67095, 67153)
GROUP BY CASE
    WHEN TW.kod LIKE '%Kurczak A%' THEN 'A'
    WHEN TW.kod LIKE '%Kurczak B%' THEN 'B'
    ELSE 'X'
END;
