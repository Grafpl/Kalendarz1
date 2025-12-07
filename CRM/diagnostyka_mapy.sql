-- DIAGNOSTYKA MAPY CRM
-- Uruchom te zapytania w SQL Server Management Studio

-- 1. Ile jest odbiorców w sumie?
SELECT COUNT(*) as LiczbaOdbiorcow FROM OdbiorcyCRM;

-- 2. Ile odbiorców ma kod pocztowy?
SELECT COUNT(*) as OdbiorcyZKodem FROM OdbiorcyCRM WHERE KOD IS NOT NULL AND KOD <> '';

-- 3. Ile kodów pocztowych ma współrzędne?
SELECT COUNT(*) as KodyZeWspolrzednymi FROM KodyPocztowe WHERE Latitude IS NOT NULL AND Longitude IS NOT NULL;

-- 4. Ile UNIKALNYCH kodów używają odbiorcy?
SELECT COUNT(DISTINCT KOD) as UnikatkoweKodyOdbiorcow FROM OdbiorcyCRM WHERE KOD IS NOT NULL AND KOD <> '';

-- 5. TOP 20 najpopularniejszych kodów używanych przez odbiorców - CZY MAJĄ WSPÓŁRZĘDNE?
SELECT TOP 20
    o.KOD,
    COUNT(*) as IleOdbiorcow,
    kp.Latitude,
    kp.Longitude,
    CASE WHEN kp.Kod IS NULL THEN 'BRAK W TABELI KodyPocztowe'
         WHEN kp.Latitude IS NULL THEN 'BRAK WSPÓŁRZĘDNYCH'
         ELSE 'OK' END as Status
FROM OdbiorcyCRM o
LEFT JOIN KodyPocztowe kp ON o.KOD = kp.Kod
WHERE o.KOD IS NOT NULL AND o.KOD <> ''
GROUP BY o.KOD, kp.Kod, kp.Latitude, kp.Longitude
ORDER BY COUNT(*) DESC;

-- 6. Przykładowe kody - porównanie formatów
SELECT TOP 10 'OdbiorcyCRM' as Tabela, KOD, LEN(KOD) as Dlugosc FROM OdbiorcyCRM WHERE KOD IS NOT NULL
UNION ALL
SELECT TOP 10 'KodyPocztowe' as Tabela, Kod, LEN(Kod) as Dlugosc FROM KodyPocztowe;

-- 7. Ile odbiorców MA pasujący kod Z WSPÓŁRZĘDNYMI (to powinno się wyświetlić na mapie)
SELECT COUNT(*) as OdbiorcyGotoviDoMapy
FROM OdbiorcyCRM o
INNER JOIN KodyPocztowe kp ON o.KOD = kp.Kod
WHERE kp.Latitude IS NOT NULL AND kp.Longitude IS NOT NULL;

-- 8. Struktura tabeli KodyPocztowe
SELECT COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'KodyPocztowe';
