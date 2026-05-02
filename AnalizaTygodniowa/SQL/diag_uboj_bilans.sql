-- ════════════════════════════════════════════════════════════════════
-- BILANS UBOJU: Kurczak żywy → Tuszka + elementy + odpady
-- Sergiusz: chcę zobaczyć jak Kurczak żywy zamienia się na produkty
-- Wklej do SSMS na 192.168.0.112 (HANDEL)
-- ════════════════════════════════════════════════════════════════════
USE HANDEL;
SET NOCOUNT ON;
GO

-- ─────────────────────────────────────────────────────────────────
-- 1. Znajdź wszystkie towary „Kurczak żywy*" — w jakim katalogu są
-- ─────────────────────────────────────────────────────────────────
PRINT '═══ 1. Towary "Kurczak żywy" — gdzie są w bazie ═══';
SELECT TW.id, TW.kod, TW.nazwa, TW.katalog, TW.jm, TW.przelicz, TW.przelkg, TW.aktywny
FROM HM.TW
WHERE TW.nazwa LIKE 'Kurczak żywy%'
   OR TW.nazwa LIKE 'Kurczak zywy%'
   OR TW.kod LIKE '%żywy%'
   OR TW.kod LIKE '%zywy%'
   OR TW.nazwa LIKE 'Brojler%'
ORDER BY TW.aktywny DESC, TW.nazwa;
GO

-- ─────────────────────────────────────────────────────────────────
-- 2. Przyjęcia żywca w ostatnich 7 dniach (sPZ z dostawców)
-- ─────────────────────────────────────────────────────────────────
PRINT '═══ 2. Przyjęcia ŻYWCA (sPZ) — ostatnie 7 dni: ile, od kogo ═══';
SELECT
    CAST(MG.data AS DATE) AS Dzien,
    TW.kod AS KodTowaru,
    TW.nazwa AS NazwaTowaru,
    K.Shortcut AS Dostawca,
    SUM(MZ.ilosc) AS Lacznie_kg,
    COUNT(*) AS LiczbaWierszy,
    MG.magazyn AS Magazyn
FROM HM.MZ MZ
JOIN HM.MG MG ON MG.id = MZ.super
JOIN HM.TW TW ON TW.id = MZ.idtw
LEFT JOIN SSCommon.STContractors K ON K.id = MG.khid
WHERE MG.seria = 'sPZ'
  AND (TW.nazwa LIKE 'Kurczak żywy%' OR TW.nazwa LIKE 'Kurczak zywy%' OR TW.nazwa LIKE 'Brojler%')
  AND MG.data >= DATEADD(DAY, -7, GETDATE())
  AND MG.anulowany = 0
GROUP BY CAST(MG.data AS DATE), TW.kod, TW.nazwa, K.Shortcut, MG.magazyn
ORDER BY Dzien DESC, Lacznie_kg DESC;
GO

-- ─────────────────────────────────────────────────────────────────
-- 3. ⭐ BILANS DZIENNY: ŻYWIEC vs TUSZKA + ELEMENTY (dla wczoraj)
-- ─────────────────────────────────────────────────────────────────
PRINT '═══ 3. BILANS UBOJU — wczoraj: żywiec wjechał, co wyszło ═══';
DECLARE @data date = CAST(DATEADD(DAY, -1, GETDATE()) AS DATE);
PRINT 'Data analizy: ' + CONVERT(varchar, @data, 120);

WITH Zywiec AS (
    SELECT
        'A. ZYWIEC (przyjęcie sPZ)' AS Etap,
        TW.kod, TW.nazwa,
        SUM(MZ.ilosc) AS Ilosc_kg
    FROM HM.MZ MZ
    JOIN HM.MG MG ON MG.id = MZ.super
    JOIN HM.TW TW ON TW.id = MZ.idtw
    WHERE MG.seria = 'sPZ'
      AND (TW.nazwa LIKE 'Kurczak żywy%' OR TW.nazwa LIKE 'Kurczak zywy%' OR TW.nazwa LIKE 'Brojler%')
      AND CAST(MG.data AS DATE) = @data
      AND MG.anulowany = 0
    GROUP BY TW.kod, TW.nazwa
),
Uboj AS (
    SELECT
        'B. UBOJ - przyjęcia produkcji (sPWU/PWP)' AS Etap,
        TW.kod, TW.nazwa,
        SUM(MZ.ilosc) AS Ilosc_kg
    FROM HM.MZ MZ
    JOIN HM.MG MG ON MG.id = MZ.super
    JOIN HM.TW TW ON TW.id = MZ.idtw
    WHERE MG.seria IN ('sPWU', 'PWP')
      AND TW.katalog = 67095
      AND CAST(MG.data AS DATE) = @data
      AND MG.anulowany = 0
    GROUP BY TW.kod, TW.nazwa
),
Krojenie AS (
    SELECT
        'C. KROJENIE - rozchód do rozbioru (RWP)' AS Etap,
        TW.kod, TW.nazwa,
        SUM(MZ.ilosc) AS Ilosc_kg
    FROM HM.MZ MZ
    JOIN HM.MG MG ON MG.id = MZ.super
    JOIN HM.TW TW ON TW.id = MZ.idtw
    WHERE MG.seria = 'RWP'
      AND CAST(MG.data AS DATE) = @data
      AND MG.anulowany = 0
    GROUP BY TW.kod, TW.nazwa
)
SELECT * FROM Zywiec
UNION ALL SELECT * FROM Uboj
UNION ALL SELECT * FROM Krojenie
ORDER BY Etap, Ilosc_kg DESC;
GO

-- ─────────────────────────────────────────────────────────────────
-- 4. UZYSK — % tuszka A z całkowitej produkcji (per dzień, ostatnie 14)
-- ─────────────────────────────────────────────────────────────────
PRINT '═══ 4. UZYSK kurczaka klasy A z całkowitej produkcji (14 dni) ═══';
WITH Produkcja14 AS (
    SELECT
        CAST(MG.data AS DATE) AS Dzien,
        CASE
            WHEN TW.nazwa LIKE 'Kurczak A%'                  THEN 'KurczakA'
            WHEN TW.nazwa LIKE 'Kurczak B%'                  THEN 'KurczakB'
            WHEN TW.nazwa LIKE 'Filet A%' OR TW.nazwa LIKE 'Filet z piersi%' THEN 'FiletA'
            WHEN TW.nazwa LIKE 'Filet II%' OR TW.nazwa LIKE 'Filet B%'        THEN 'FiletII'
            WHEN TW.nazwa LIKE 'Ćwiartka%' AND TW.nazwa NOT LIKE '%II%'       THEN 'CwiartkaA'
            WHEN TW.nazwa LIKE 'Ćwiartka II%'                                  THEN 'CwiartkaII'
            WHEN TW.nazwa LIKE 'Korpus%'                                       THEN 'Korpus'
            WHEN TW.nazwa LIKE 'Skrzydło%' OR TW.nazwa LIKE 'Skrzydlo%'       THEN 'Skrzydlo'
            WHEN TW.nazwa LIKE 'Wątroba%' OR TW.nazwa LIKE 'Watroba%'         THEN 'Podroby'
            WHEN TW.nazwa LIKE 'Serce%' OR TW.nazwa LIKE 'Żołądki%' OR TW.nazwa LIKE 'Zoladki%' THEN 'Podroby'
            WHEN TW.nazwa LIKE 'Noga%' OR TW.nazwa LIKE 'Pałka%' OR TW.nazwa LIKE 'Palka%' OR TW.nazwa LIKE 'Udo%' THEN 'NogiUdo'
            ELSE 'Inne'
        END AS Kategoria,
        SUM(MZ.ilosc) AS kg
    FROM HM.MZ MZ
    JOIN HM.MG MG ON MG.id = MZ.super
    JOIN HM.TW TW ON TW.id = MZ.idtw
    WHERE MG.seria IN ('sPWU', 'PWP')
      AND TW.katalog = 67095
      AND MG.data >= DATEADD(DAY, -14, GETDATE())
      AND MG.anulowany = 0
    GROUP BY CAST(MG.data AS DATE), TW.nazwa
)
SELECT
    Dzien,
    SUM(CASE WHEN Kategoria = 'KurczakA'   THEN kg ELSE 0 END) AS KurczakA_kg,
    SUM(CASE WHEN Kategoria = 'KurczakB'   THEN kg ELSE 0 END) AS KurczakB_kg,
    SUM(CASE WHEN Kategoria = 'FiletA'     THEN kg ELSE 0 END) AS FiletA_kg,
    SUM(CASE WHEN Kategoria = 'FiletII'    THEN kg ELSE 0 END) AS FiletII_kg,
    SUM(CASE WHEN Kategoria = 'CwiartkaA'  THEN kg ELSE 0 END) AS CwiartkaA_kg,
    SUM(CASE WHEN Kategoria = 'CwiartkaII' THEN kg ELSE 0 END) AS CwiartkaII_kg,
    SUM(CASE WHEN Kategoria = 'Korpus'     THEN kg ELSE 0 END) AS Korpus_kg,
    SUM(CASE WHEN Kategoria = 'Podroby'    THEN kg ELSE 0 END) AS Podroby_kg,
    SUM(kg) AS Lacznie_kg,
    -- % tuszka A z calkowitej produkcji
    CAST(SUM(CASE WHEN Kategoria = 'KurczakA' THEN kg ELSE 0 END) * 100.0 / NULLIF(SUM(kg), 0) AS DECIMAL(5,1)) AS Procent_KurczakA
FROM Produkcja14
GROUP BY Dzien
ORDER BY Dzien DESC;
GO

-- ─────────────────────────────────────────────────────────────────
-- 5. REKLAMACJE — przegląd ostatnich 90 dni
-- ─────────────────────────────────────────────────────────────────
USE LibraNet;
SET NOCOUNT ON;
GO

PRINT '═══ 5a. Reklamacje — przegląd statusów (ostatnie 90 dni) ═══';
SELECT
    Status, StatusV2, TypReklamacji,
    COUNT(*) AS Liczba,
    SUM(SumaKg) AS LacznieKg,
    SUM(SumaWartosc) AS LacznieZl,
    SUM(KosztReklamacji) AS LacznyKoszt
FROM dbo.Reklamacje
WHERE DataZgloszenia >= DATEADD(MONTH, -3, GETDATE())
GROUP BY Status, StatusV2, TypReklamacji
ORDER BY Liczba DESC;
GO

PRINT '═══ 5b. Top 10 powodów reklamacji (PrzyczynaGlowna) ═══';
SELECT TOP 10
    ISNULL(PrzyczynaGlowna, '(brak)') AS PrzyczynaGlowna,
    ISNULL(KategoriaPrzyczyny, '(brak)') AS Kategoria,
    COUNT(*) AS Liczba,
    SUM(SumaKg) AS Kg,
    SUM(SumaWartosc) AS Zl
FROM dbo.Reklamacje
WHERE DataZgloszenia >= DATEADD(MONTH, -6, GETDATE())
GROUP BY PrzyczynaGlowna, KategoriaPrzyczyny
ORDER BY Liczba DESC;
GO

PRINT '═══ 5c. Reklamacje per handlowiec (6 mies) ═══';
SELECT
    Handlowiec,
    COUNT(*) AS Liczba,
    SUM(SumaKg) AS Kg,
    SUM(SumaWartosc) AS Zl,
    AVG(CAST(SumaKg AS FLOAT)) AS SrKgPerReklamacja
FROM dbo.Reklamacje
WHERE DataZgloszenia >= DATEADD(MONTH, -6, GETDATE())
  AND Handlowiec IS NOT NULL
GROUP BY Handlowiec
ORDER BY Liczba DESC;
GO

PRINT '═══ KONIEC bilansu uboju ═══';
