-- ============================================================
-- 20 — Haccp + QC_Normy + QC_Zdjecia + jakosc
-- ============================================================
USE LibraNet;
GO

-- A) Struktury
SELECT
    TABLE_NAME, COLUMN_NAME, DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME IN ('Haccp', 'QC_Normy', 'QC_Zdjecia', 'OdpadyRejestr',
                     'Out1A', 'In0E')
ORDER BY TABLE_NAME, ORDINAL_POSITION;
GO

-- B) Liczba rekordow
SELECT 'Haccp' AS tabela, COUNT(*) AS rekordow FROM dbo.Haccp
UNION ALL SELECT 'QC_Normy', COUNT(*) FROM dbo.QC_Normy
UNION ALL SELECT 'QC_Zdjecia', COUNT(*) FROM dbo.QC_Zdjecia
UNION ALL SELECT 'OdpadyRejestr', COUNT(*) FROM dbo.OdpadyRejestr
UNION ALL SELECT 'Out1A', COUNT(*) FROM dbo.Out1A;
GO

-- C) Sample 10 najnowszych Haccp
SELECT TOP 10 *
FROM dbo.Haccp
ORDER BY 1 DESC;
GO

-- D) Wszystkie QC_Normy
SELECT *
FROM dbo.QC_Normy
ORDER BY 1;
GO

-- E) Sample 10 QC_Zdjecia
SELECT TOP 10 *
FROM dbo.QC_Zdjecia
ORDER BY 1 DESC;
GO

-- F) Sample 10 OdpadyRejestr
SELECT TOP 10 *
FROM dbo.OdpadyRejestr
ORDER BY 1 DESC;
GO

-- G) vw_QC_Podsum + vw_QC_WadySkale (jesli istnieja)
SELECT TOP 10 *
FROM dbo.vw_QC_Podsum;
GO

SELECT TOP 10 *
FROM dbo.vw_QC_WadySkale;
GO

-- H) Out1A — czy zywa? Jakie ostatnie wpisy
SELECT TOP 10 *
FROM dbo.Out1A
ORDER BY 1 DESC;
GO

SELECT
    MIN(Data) AS najstarszy_wpis,
    MAX(Data) AS najnowszy_wpis,
    COUNT(*) AS razem
FROM dbo.Out1A;
GO
