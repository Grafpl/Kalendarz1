-- ============================================================
-- 19 — DashboardWidoki + AppSettings + KonfiguracjaProdukty
-- ============================================================
USE LibraNet;
GO

-- A) Struktury
SELECT
    TABLE_NAME, COLUMN_NAME, DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME IN ('DashboardWidoki', 'AppSettings',
                     'KonfiguracjaProdukty', 'KonfiguracjaWydajnosc',
                     'KolejnoscTowarow', 'PriceType')
ORDER BY TABLE_NAME, ORDINAL_POSITION;
GO

-- B) Liczba rekordow
SELECT 'DashboardWidoki' AS tabela, COUNT(*) AS rekordow FROM dbo.DashboardWidoki
UNION ALL SELECT 'AppSettings', COUNT(*) FROM dbo.AppSettings
UNION ALL SELECT 'KonfiguracjaProdukty', COUNT(*) FROM dbo.KonfiguracjaProdukty
UNION ALL SELECT 'KonfiguracjaWydajnosc', COUNT(*) FROM dbo.KonfiguracjaWydajnosc
UNION ALL SELECT 'KolejnoscTowarow', COUNT(*) FROM dbo.KolejnoscTowarow
UNION ALL SELECT 'PriceType', COUNT(*) FROM dbo.PriceType;
GO

-- C) Sample
SELECT TOP 20 * FROM dbo.AppSettings;
GO

SELECT TOP 10 * FROM dbo.DashboardWidoki;
GO

SELECT TOP 20 * FROM dbo.KonfiguracjaWydajnosc;
GO

SELECT TOP 20 * FROM dbo.KolejnoscTowarow;
GO

SELECT TOP 20 * FROM dbo.PriceType;
GO

-- D) CenaTuszki / CenaMinisterialna / CenaRolnicza
SELECT
    TABLE_NAME, COLUMN_NAME, DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME IN ('CenaTuszki', 'CenaMinisterialna', 'CenaRolnicza')
ORDER BY TABLE_NAME, ORDINAL_POSITION;
GO

SELECT 'CenaTuszki' AS tabela, COUNT(*) AS rekordow FROM dbo.CenaTuszki
UNION ALL SELECT 'CenaMinisterialna', COUNT(*) FROM dbo.CenaMinisterialna
UNION ALL SELECT 'CenaRolnicza', COUNT(*) FROM dbo.CenaRolnicza;
GO

-- E) 10 najnowszych cen tuszki
SELECT TOP 10 *
FROM dbo.CenaTuszki
ORDER BY 1 DESC;
GO
