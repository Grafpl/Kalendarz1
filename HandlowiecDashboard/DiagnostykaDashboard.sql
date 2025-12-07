-- ============================================
-- DIAGNOSTYKA DASHBOARD HANDLOWCA
-- Uruchom te zapytania w bazie LibraNet (192.168.0.109)
-- ============================================

-- 1. SPRAWDŹ CZY TABELA ZamowieniaMieso ISTNIEJE I MA DANE
SELECT 'ZamowieniaMieso - liczba rekordow' as Info, COUNT(*) as Wartosc FROM ZamowieniaMieso;

-- 2. SPRAWDŹ CZY TABELA ZamowieniaMiesoTowar ISTNIEJE I MA DANE
SELECT 'ZamowieniaMiesoTowar - liczba rekordow' as Info, COUNT(*) as Wartosc FROM ZamowieniaMiesoTowar;

-- 3. OSTATNIE 10 ZAMÓWIEŃ (sprawdzenie struktury danych)
SELECT TOP 10
    z.ID,
    z.Odbiorca,
    z.Handlowiec,
    z.DataOdbioru,
    z.DataZamowienia,
    z.Status,
    z.Anulowane,
    z.TransportStatus,
    z.OdbiorcaId
FROM ZamowieniaMieso z
ORDER BY z.DataOdbioru DESC;

-- 4. OSTATNIE 10 POZYCJI ZAMÓWIEŃ (sprawdzenie struktury)
SELECT TOP 10
    zp.ZamowienieId,
    zp.KodTowaru,
    zp.Ilosc,
    zp.Cena,
    zp.Pojemniki,
    zp.Palety
FROM ZamowieniaMiesoTowar zp;

-- 5. LISTA HANDLOWCÓW W SYSTEMIE
SELECT DISTINCT Handlowiec, COUNT(*) as LiczbaZamowien
FROM ZamowieniaMieso
WHERE Handlowiec IS NOT NULL AND Handlowiec != ''
GROUP BY Handlowiec
ORDER BY LiczbaZamowien DESC;

-- 6. ZAMÓWIENIA Z DZISIAJ
DECLARE @Dzis DATE = CAST(GETDATE() AS DATE);
SELECT
    'Zamowienia dzis' as Info,
    COUNT(DISTINCT z.ID) as LiczbaZamowien,
    ISNULL(SUM(zp.Ilosc), 0) as SumaKg
FROM ZamowieniaMieso z
LEFT JOIN ZamowieniaMiesoTowar zp ON z.ID = zp.ZamowienieId
WHERE z.DataOdbioru = @Dzis
  AND (z.Anulowane IS NULL OR z.Anulowane = 0);

-- 7. ZAMÓWIENIA Z OSTATNICH 7 DNI
SELECT
    'Zamowienia ostatnie 7 dni' as Info,
    COUNT(DISTINCT z.ID) as LiczbaZamowien,
    ISNULL(SUM(zp.Ilosc), 0) as SumaKg,
    ISNULL(SUM(zp.Ilosc * ISNULL(TRY_CAST(NULLIF(zp.Cena, '') AS DECIMAL(18,2)), 0)), 0) as SumaWartosc
FROM ZamowieniaMieso z
LEFT JOIN ZamowieniaMiesoTowar zp ON z.ID = zp.ZamowienieId
WHERE z.DataOdbioru >= DATEADD(DAY, -7, GETDATE())
  AND (z.Anulowane IS NULL OR z.Anulowane = 0);

-- 8. ZAMÓWIENIA Z OSTATNICH 30 DNI
SELECT
    'Zamowienia ostatnie 30 dni' as Info,
    COUNT(DISTINCT z.ID) as LiczbaZamowien,
    ISNULL(SUM(zp.Ilosc), 0) as SumaKg,
    ISNULL(SUM(zp.Ilosc * ISNULL(TRY_CAST(NULLIF(zp.Cena, '') AS DECIMAL(18,2)), 0)), 0) as SumaWartosc
FROM ZamowieniaMieso z
LEFT JOIN ZamowieniaMiesoTowar zp ON z.ID = zp.ZamowienieId
WHERE z.DataOdbioru >= DATEADD(DAY, -30, GETDATE())
  AND (z.Anulowane IS NULL OR z.Anulowane = 0);

-- 9. ZAMÓWIENIA Z BIEŻĄCEGO MIESIĄCA
DECLARE @PierwszyDzienMiesiaca DATE = DATEFROMPARTS(YEAR(GETDATE()), MONTH(GETDATE()), 1);
SELECT
    'Zamowienia ten miesiac' as Info,
    COUNT(DISTINCT z.ID) as LiczbaZamowien,
    ISNULL(SUM(zp.Ilosc), 0) as SumaKg,
    ISNULL(SUM(zp.Ilosc * ISNULL(TRY_CAST(NULLIF(zp.Cena, '') AS DECIMAL(18,2)), 0)), 0) as SumaWartosc
FROM ZamowieniaMieso z
LEFT JOIN ZamowieniaMiesoTowar zp ON z.ID = zp.ZamowienieId
WHERE z.DataOdbioru >= @PierwszyDzienMiesiaca
  AND (z.Anulowane IS NULL OR z.Anulowane = 0);

-- 10. ZAMÓWIENIA Z 2024 ROKU
SELECT
    'Zamowienia 2024' as Info,
    COUNT(DISTINCT z.ID) as LiczbaZamowien,
    ISNULL(SUM(zp.Ilosc), 0) as SumaKg,
    ISNULL(SUM(zp.Ilosc * ISNULL(TRY_CAST(NULLIF(zp.Cena, '') AS DECIMAL(18,2)), 0)), 0) as SumaWartosc
FROM ZamowieniaMieso z
LEFT JOIN ZamowieniaMiesoTowar zp ON z.ID = zp.ZamowienieId
WHERE z.DataOdbioru >= '2024-01-01' AND z.DataOdbioru < '2025-01-01'
  AND (z.Anulowane IS NULL OR z.Anulowane = 0);

-- 11. ZAMÓWIENIA Z 2025 ROKU
SELECT
    'Zamowienia 2025' as Info,
    COUNT(DISTINCT z.ID) as LiczbaZamowien,
    ISNULL(SUM(zp.Ilosc), 0) as SumaKg,
    ISNULL(SUM(zp.Ilosc * ISNULL(TRY_CAST(NULLIF(zp.Cena, '') AS DECIMAL(18,2)), 0)), 0) as SumaWartosc
FROM ZamowieniaMieso z
LEFT JOIN ZamowieniaMiesoTowar zp ON z.ID = zp.ZamowienieId
WHERE z.DataOdbioru >= '2025-01-01' AND z.DataOdbioru < '2026-01-01'
  AND (z.Anulowane IS NULL OR z.Anulowane = 0);

-- 12. TOP 10 ODBIORCÓW (ostatnie 3 miesiące)
SELECT TOP 10
    z.OdbiorcaId,
    z.Odbiorca as Nazwa,
    COUNT(DISTINCT z.ID) as LiczbaZamowien,
    ISNULL(SUM(zp.Ilosc), 0) as SumaKg,
    ISNULL(SUM(zp.Ilosc * ISNULL(TRY_CAST(NULLIF(zp.Cena, '') AS DECIMAL(18,2)), 0)), 0) as SumaWartosc
FROM ZamowieniaMieso z
LEFT JOIN ZamowieniaMiesoTowar zp ON z.ID = zp.ZamowienieId
WHERE z.DataOdbioru >= DATEADD(MONTH, -3, GETDATE())
    AND (z.Anulowane IS NULL OR z.Anulowane = 0)
GROUP BY z.OdbiorcaId, z.Odbiorca
ORDER BY SumaWartosc DESC;

-- 13. SPRZEDAŻ WG HANDLOWCÓW (ostatnie 3 miesiące)
SELECT TOP 10
    ISNULL(z.Handlowiec, 'Nieznany') as Handlowiec,
    COUNT(DISTINCT z.ID) as LiczbaZamowien,
    ISNULL(SUM(zp.Ilosc), 0) as SumaKg,
    ISNULL(SUM(zp.Ilosc * ISNULL(TRY_CAST(NULLIF(zp.Cena, '') AS DECIMAL(18,2)), 0)), 0) as SumaWartosc,
    COUNT(DISTINCT z.OdbiorcaId) as LiczbaOdbiorcow
FROM ZamowieniaMieso z
LEFT JOIN ZamowieniaMiesoTowar zp ON z.ID = zp.ZamowienieId
WHERE z.DataOdbioru >= DATEADD(MONTH, -3, GETDATE())
    AND (z.Anulowane IS NULL OR z.Anulowane = 0)
GROUP BY ISNULL(z.Handlowiec, 'Nieznany')
ORDER BY SumaWartosc DESC;

-- 14. STATYSTYKI TYP DOSTAWY
SELECT
    ISNULL(z.TransportStatus, 'Firma') as TypDostawy,
    COUNT(*) as Liczba
FROM ZamowieniaMieso z
WHERE z.DataOdbioru >= DATEADD(MONTH, -1, GETDATE())
    AND (z.Anulowane IS NULL OR z.Anulowane = 0)
GROUP BY ISNULL(z.TransportStatus, 'Firma');

-- 15. SPRZEDAŻ DZIENNA (ostatnie 14 dni)
SELECT
    CAST(z.DataOdbioru AS DATE) as Dzien,
    COUNT(DISTINCT z.ID) as LiczbaZamowien,
    ISNULL(SUM(zp.Ilosc), 0) as SumaKg,
    ISNULL(SUM(zp.Ilosc * ISNULL(TRY_CAST(NULLIF(zp.Cena, '') AS DECIMAL(18,2)), 0)), 0) as SumaWartosc
FROM ZamowieniaMieso z
LEFT JOIN ZamowieniaMiesoTowar zp ON z.ID = zp.ZamowienieId
WHERE z.DataOdbioru >= DATEADD(DAY, -14, GETDATE())
    AND (z.Anulowane IS NULL OR z.Anulowane = 0)
GROUP BY CAST(z.DataOdbioru AS DATE)
ORDER BY Dzien;

-- 16. SPRAWDZENIE KOLUMNY CENA (czy są problemy z formatem)
SELECT
    'Cena - puste' as Info,
    COUNT(*) as Liczba
FROM ZamowieniaMiesoTowar WHERE Cena IS NULL OR Cena = '';

SELECT
    'Cena - niepuste' as Info,
    COUNT(*) as Liczba
FROM ZamowieniaMiesoTowar WHERE Cena IS NOT NULL AND Cena != '';

SELECT TOP 10
    'Przykladowe ceny' as Info,
    Cena,
    TRY_CAST(NULLIF(Cena, '') AS DECIMAL(18,2)) as CenaPoParsowaniu
FROM ZamowieniaMiesoTowar
WHERE Cena IS NOT NULL AND Cena != '';

-- 17. PORÓWNANIE MIESIĘCY (ostatnie 6 miesięcy)
;WITH Miesiace AS (
    SELECT
        YEAR(z.DataOdbioru) as Rok,
        MONTH(z.DataOdbioru) as Miesiac,
        COUNT(DISTINCT z.ID) as LiczbaZamowien,
        ISNULL(SUM(zp.Ilosc), 0) as SumaKg,
        ISNULL(SUM(zp.Ilosc * ISNULL(TRY_CAST(NULLIF(zp.Cena, '') AS DECIMAL(18,2)), 0)), 0) as SumaWartosc
    FROM ZamowieniaMieso z
    LEFT JOIN ZamowieniaMiesoTowar zp ON z.ID = zp.ZamowienieId
    WHERE z.DataOdbioru >= DATEADD(MONTH, -6, GETDATE())
        AND (z.Anulowane IS NULL OR z.Anulowane = 0)
    GROUP BY YEAR(z.DataOdbioru), MONTH(z.DataOdbioru)
)
SELECT * FROM Miesiace ORDER BY Rok DESC, Miesiac DESC;

-- 18. SPRAWDZENIE DATY ODBIORU - zakres dat w danych
SELECT
    MIN(DataOdbioru) as NajstarszeZamowienie,
    MAX(DataOdbioru) as NajnowszeZamowienie,
    COUNT(*) as LacznaLiczbaZamowien
FROM ZamowieniaMieso;

-- 19. SPRAWDZENIE KOLUMN W ZamowieniaMieso
SELECT
    COLUMN_NAME,
    DATA_TYPE,
    IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'ZamowieniaMieso'
ORDER BY ORDINAL_POSITION;

-- 20. SPRAWDZENIE KOLUMN W ZamowieniaMiesoTowar
SELECT
    COLUMN_NAME,
    DATA_TYPE,
    IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'ZamowieniaMiesoTowar'
ORDER BY ORDINAL_POSITION;

-- 21. ZAMÓWIENIA DZIS I JUTRO (dokładnie jak w dashboardzie)
DECLARE @DzisDzis DATE = CAST(GETDATE() AS DATE);
DECLARE @Jutro DATE = DATEADD(DAY, 1, CAST(GETDATE() AS DATE));

SELECT
    SUM(CASE WHEN z.DataOdbioru = @DzisDzis THEN 1 ELSE 0 END) as ZamDzis,
    SUM(CASE WHEN z.DataOdbioru = @DzisDzis THEN ISNULL(zp.Ilosc, 0) ELSE 0 END) as KgDzis,
    SUM(CASE WHEN z.DataOdbioru = @DzisDzis THEN ISNULL(zp.Ilosc * ISNULL(TRY_CAST(NULLIF(zp.Cena, '') AS DECIMAL(18,2)), 0), 0) ELSE 0 END) as WartoscDzis,
    SUM(CASE WHEN z.DataOdbioru = @Jutro THEN 1 ELSE 0 END) as ZamJutro,
    SUM(CASE WHEN z.DataOdbioru = @Jutro THEN ISNULL(zp.Ilosc, 0) ELSE 0 END) as KgJutro,
    SUM(CASE WHEN z.DataOdbioru = @Jutro THEN ISNULL(zp.Ilosc * ISNULL(TRY_CAST(NULLIF(zp.Cena, '') AS DECIMAL(18,2)), 0), 0) ELSE 0 END) as WartoscJutro
FROM ZamowieniaMieso z
LEFT JOIN ZamowieniaMiesoTowar zp ON z.ID = zp.ZamowienieId
WHERE z.DataOdbioru IN (@DzisDzis, @Jutro)
  AND (z.Anulowane IS NULL OR z.Anulowane = 0);

-- 22. SPRAWDZENIE CZY SĄ ZAMÓWIENIA W PRZYSZŁOŚCI
SELECT
    'Zamowienia w przyszlosci' as Info,
    COUNT(*) as Liczba
FROM ZamowieniaMieso
WHERE DataOdbioru > GETDATE();
