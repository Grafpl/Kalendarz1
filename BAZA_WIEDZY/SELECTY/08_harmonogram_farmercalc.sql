-- ============================================================
-- 08 — HarmonogramDostaw + FarmerCalc + WstawieniaKurczakow
-- ============================================================
USE LibraNet;
GO

-- A) Struktura HarmonogramDostaw
SELECT
    ORDINAL_POSITION, COLUMN_NAME, DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'HarmonogramDostaw'
ORDER BY ORDINAL_POSITION;
GO

-- B) Struktura FarmerCalc
SELECT
    ORDINAL_POSITION, COLUMN_NAME, DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'FarmerCalc'
ORDER BY ORDINAL_POSITION;
GO

-- C) Struktura WstawieniaKurczakow
SELECT
    ORDINAL_POSITION, COLUMN_NAME, DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'WstawieniaKurczakow'
ORDER BY ORDINAL_POSITION;
GO

-- D) 10 najnowszych pozycji harmonogramu
SELECT TOP 10 *
FROM dbo.HarmonogramDostaw
ORDER BY DataOdbioru DESC;
GO

-- E) 10 najnowszych farmer calc
SELECT TOP 10 *
FROM dbo.FarmerCalc
ORDER BY 1 DESC;
GO

-- F) 10 najnowszych wstawien
SELECT TOP 10 *
FROM dbo.WstawieniaKurczakow
ORDER BY 1 DESC;
GO

-- G) Statystyki harmonogramu na ostatnie 30 dni
SELECT
    DataOdbioru,
    COUNT(*) AS liczba_dostaw,
    SUM(SztukiDek) AS suma_sztuk,
    SUM(WagaDek) AS suma_kg,
    COUNT(DISTINCT Dostawca) AS liczba_hodowcow
FROM dbo.HarmonogramDostaw
WHERE DataOdbioru >= CONVERT(varchar(10), DATEADD(DAY, -30, GETDATE()), 120)
GROUP BY DataOdbioru
ORDER BY DataOdbioru DESC;
GO

-- H) Liczba wierszy
SELECT 'HarmonogramDostaw' AS tabela, COUNT(*) AS rekordow FROM dbo.HarmonogramDostaw
UNION ALL
SELECT 'FarmerCalc', COUNT(*) FROM dbo.FarmerCalc
UNION ALL
SELECT 'WstawieniaKurczakow', COUNT(*) FROM dbo.WstawieniaKurczakow;
GO
