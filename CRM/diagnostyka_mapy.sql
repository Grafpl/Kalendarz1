-- ============================================
-- DIAGNOSTYKA I NAPRAWA MAPY CRM
-- ============================================
-- Uruchom te zapytania w SQL Server Management Studio
-- aby zdiagnozować i naprawić problemy z mapą

-- ============================================
-- KROK 1: SPRAWDŹ CZY TABELA KodyPocztowe ISTNIEJE
-- ============================================

-- Jeśli tabela nie istnieje, utwórz ją:
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'KodyPocztowe')
BEGIN
    CREATE TABLE KodyPocztowe (
        Kod NVARCHAR(10) PRIMARY KEY,
        miej NVARCHAR(100),
        Latitude FLOAT,
        Longitude FLOAT
    );
    PRINT 'Utworzono tabelę KodyPocztowe';
END
ELSE
BEGIN
    PRINT 'Tabela KodyPocztowe już istnieje';
END
GO

-- ============================================
-- KROK 2: DODAJ BRAKUJĄCE KODY Z OdbiorcyCRM
-- ============================================

INSERT INTO KodyPocztowe (Kod, miej)
SELECT DISTINCT o.KOD, MAX(o.MIASTO)
FROM OdbiorcyCRM o
WHERE o.KOD IS NOT NULL AND o.KOD <> ''
  AND NOT EXISTS (SELECT 1 FROM KodyPocztowe kp WHERE kp.Kod = o.KOD)
GROUP BY o.KOD;

PRINT 'Dodano brakujące kody pocztowe z tabeli OdbiorcyCRM';
GO

-- ============================================
-- KROK 3: DIAGNOSTYKA - STAN DANYCH
-- ============================================

PRINT '=== DIAGNOSTYKA MAPY CRM ===';
PRINT '';

-- 3.1 Ile jest odbiorców w sumie?
SELECT 'Liczba odbiorców CRM' as Info, COUNT(*) as Wartosc FROM OdbiorcyCRM;

-- 3.2 Ile odbiorców ma kod pocztowy?
SELECT 'Odbiorcy z kodem pocztowym' as Info, COUNT(*) as Wartosc
FROM OdbiorcyCRM WHERE KOD IS NOT NULL AND KOD <> '';

-- 3.3 Ile jest kodów pocztowych w tabeli?
SELECT 'Kodów w tabeli KodyPocztowe' as Info, COUNT(*) as Wartosc FROM KodyPocztowe;

-- 3.4 Ile kodów ma współrzędne?
SELECT 'Kodów ze współrzędnymi' as Info, COUNT(*) as Wartosc
FROM KodyPocztowe WHERE Latitude IS NOT NULL AND Longitude IS NOT NULL;

-- 3.5 Ile kodów NIE MA współrzędnych (używanych przez odbiorców)?
SELECT 'Kodów BEZ współrzędnych (używanych)' as Info, COUNT(DISTINCT kp.Kod) as Wartosc
FROM KodyPocztowe kp
INNER JOIN OdbiorcyCRM o ON o.KOD = kp.Kod
WHERE kp.Latitude IS NULL OR kp.Longitude IS NULL;

-- 3.6 Ile odbiorców MOŻE być pokazanych na mapie?
SELECT 'Odbiorcy gotowi do wyświetlenia na mapie' as Info, COUNT(*) as Wartosc
FROM OdbiorcyCRM o
INNER JOIN KodyPocztowe kp ON o.KOD = kp.Kod
WHERE kp.Latitude IS NOT NULL AND kp.Longitude IS NOT NULL;

GO

-- ============================================
-- KROK 4: TOP 20 NAJPOPULARNIEJSZYCH KODÓW
-- ============================================

PRINT '';
PRINT '=== TOP 20 KODÓW UŻYWANYCH PRZEZ ODBIORCÓW ===';

SELECT TOP 20
    o.KOD as [Kod pocztowy],
    COUNT(*) as [Liczba odbiorców],
    kp.Latitude,
    kp.Longitude,
    CASE
        WHEN kp.Kod IS NULL THEN 'BRAK W TABELI'
        WHEN kp.Latitude IS NULL THEN 'BRAK WSPÓŁRZĘDNYCH'
        ELSE 'OK'
    END as [Status]
FROM OdbiorcyCRM o
LEFT JOIN KodyPocztowe kp ON o.KOD = kp.Kod
WHERE o.KOD IS NOT NULL AND o.KOD <> ''
GROUP BY o.KOD, kp.Kod, kp.Latitude, kp.Longitude
ORDER BY COUNT(*) DESC;

GO

-- ============================================
-- KROK 5: SPRAWDŹ STATUSY ODBIORCÓW
-- ============================================

PRINT '';
PRINT '=== ROZKŁAD STATUSÓW ODBIORCÓW ===';

SELECT
    ISNULL(Status, 'Do zadzwonienia') as [Status],
    COUNT(*) as [Liczba],
    SUM(CASE WHEN kp.Latitude IS NOT NULL THEN 1 ELSE 0 END) as [Na mapie]
FROM OdbiorcyCRM o
LEFT JOIN KodyPocztowe kp ON o.KOD = kp.Kod
GROUP BY ISNULL(Status, 'Do zadzwonienia')
ORDER BY COUNT(*) DESC;

GO

-- ============================================
-- KROK 6: SPRAWDŹ FORMAT KODÓW (czy się zgadzają)
-- ============================================

PRINT '';
PRINT '=== PRZYKŁADOWE KODY - SPRAWDZENIE FORMATU ===';

SELECT TOP 10
    'OdbiorcyCRM' as Tabela,
    KOD as Kod,
    LEN(KOD) as Dlugosc,
    CASE WHEN KOD LIKE '[0-9][0-9]-[0-9][0-9][0-9]' THEN 'OK (XX-XXX)' ELSE 'INNY FORMAT' END as Format
FROM OdbiorcyCRM
WHERE KOD IS NOT NULL
UNION ALL
SELECT TOP 10
    'KodyPocztowe' as Tabela,
    Kod,
    LEN(Kod) as Dlugosc,
    CASE WHEN Kod LIKE '[0-9][0-9]-[0-9][0-9][0-9]' THEN 'OK (XX-XXX)' ELSE 'INNY FORMAT' END as Format
FROM KodyPocztowe;

GO

-- ============================================
-- OPCJONALNIE: RĘCZNE DODANIE WSPÓŁRZĘDNYCH
-- ============================================

-- Jeśli geokodowanie automatyczne nie działa, możesz ręcznie
-- dodać współrzędne dla najpopularniejszych kodów:

/*
-- Przykład dla Łodzi (kod 90-001)
UPDATE KodyPocztowe SET Latitude = 51.7592, Longitude = 19.4560 WHERE Kod = '90-001';

-- Przykład dla Warszawy (kod 00-001)
UPDATE KodyPocztowe SET Latitude = 52.2297, Longitude = 21.0122 WHERE Kod = '00-001';

-- Przykład dla Krakowa (kod 30-001)
UPDATE KodyPocztowe SET Latitude = 50.0647, Longitude = 19.9450 WHERE Kod = '30-001';
*/

-- ============================================
-- KROK 7: OSTATECZNA WERYFIKACJA
-- ============================================

PRINT '';
PRINT '=== PODSUMOWANIE ===';

SELECT
    (SELECT COUNT(*) FROM OdbiorcyCRM) as [Wszystkich odbiorców],
    (SELECT COUNT(*) FROM OdbiorcyCRM o
     INNER JOIN KodyPocztowe kp ON o.KOD = kp.Kod
     WHERE kp.Latitude IS NOT NULL) as [Gotowych na mapę],
    CAST(
        (SELECT COUNT(*) FROM OdbiorcyCRM o
         INNER JOIN KodyPocztowe kp ON o.KOD = kp.Kod
         WHERE kp.Latitude IS NOT NULL) * 100.0 /
        NULLIF((SELECT COUNT(*) FROM OdbiorcyCRM), 0)
    as DECIMAL(5,1)) as [Procent gotowych];

GO
