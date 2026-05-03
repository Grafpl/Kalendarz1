-- ============================================================
-- 09 — ZamowieniaMieso + ZamowieniaMiesoTowar (zamowienia od klientow)
-- ============================================================
USE LibraNet;
GO

-- A) Struktura ZamowieniaMieso
SELECT
    ORDINAL_POSITION, COLUMN_NAME, DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'ZamowieniaMieso'
ORDER BY ORDINAL_POSITION;
GO

-- B) Struktura ZamowieniaMiesoTowar
SELECT
    ORDINAL_POSITION, COLUMN_NAME, DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'ZamowieniaMiesoTowar'
ORDER BY ORDINAL_POSITION;
GO

-- C) Inne tabele zwiazane
SELECT
    TABLE_NAME, COLUMN_NAME, DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME IN ('ZamowieniaMiesoProdukcjaNotatki',
                     'ZamowieniaMiesoSnapshot',
                     'SzablonyZamowien',
                     'SzablonyZamowienTowar',
                     'HistoriaZmianZamowien',
                     'ZamowienieWydanieRoznice')
ORDER BY TABLE_NAME, ORDINAL_POSITION;
GO

-- D) 10 najnowszych zamowien
SELECT TOP 10 *
FROM dbo.ZamowieniaMieso
ORDER BY Id DESC;
GO

-- E) Rozklad statusow zamowien (ostatnie 90 dni)
SELECT
    Status,
    COUNT(*) AS liczba
FROM dbo.ZamowieniaMieso
WHERE DataZamowienia >= CONVERT(varchar(10), DATEADD(DAY, -90, GETDATE()), 120)
GROUP BY Status
ORDER BY liczba DESC;
GO

-- F) Rozklad TransportStatus
SELECT
    TransportStatus,
    COUNT(*) AS liczba
FROM dbo.ZamowieniaMieso
WHERE DataZamowienia >= CONVERT(varchar(10), DATEADD(DAY, -90, GETDATE()), 120)
  AND TransportStatus IS NOT NULL
GROUP BY TransportStatus
ORDER BY liczba DESC;
GO

-- G) Top 30 klientow po liczbie zamowien (90 dni)
SELECT TOP 30
    KlientId,
    COUNT(*) AS liczba_zamowien,
    SUM(LiczbaPojemnikow) AS suma_pojemnikow,
    SUM(LiczbaPalet) AS suma_palet
FROM dbo.ZamowieniaMieso
WHERE DataZamowienia >= CONVERT(varchar(10), DATEADD(DAY, -90, GETDATE()), 120)
GROUP BY KlientId
ORDER BY liczba_zamowien DESC;
GO

-- H) Anulacje per dzien (30 dni)
SELECT
    CONVERT(varchar(10), DataZamowienia, 120) AS data_,
    COUNT(*) AS razem,
    SUM(CASE WHEN Status = 'Anulowane' THEN 1 ELSE 0 END) AS anulowane,
    SUM(CASE WHEN Status = 'Zrealizowane' THEN 1 ELSE 0 END) AS zrealizowane
FROM dbo.ZamowieniaMieso
WHERE DataZamowienia >= CONVERT(varchar(10), DATEADD(DAY, -30, GETDATE()), 120)
GROUP BY CONVERT(varchar(10), DataZamowienia, 120)
ORDER BY data_ DESC;
GO

-- I) Liczba wierszy w wszystkich tabelach zamowien
SELECT 'ZamowieniaMieso' AS tabela, COUNT(*) AS rekordow FROM dbo.ZamowieniaMieso
UNION ALL
SELECT 'ZamowieniaMiesoTowar', COUNT(*) FROM dbo.ZamowieniaMiesoTowar
UNION ALL
SELECT 'ZamowieniaMiesoProdukcjaNotatki', COUNT(*) FROM dbo.ZamowieniaMiesoProdukcjaNotatki
UNION ALL
SELECT 'ZamowieniaMiesoSnapshot', COUNT(*) FROM dbo.ZamowieniaMiesoSnapshot
UNION ALL
SELECT 'SzablonyZamowien', COUNT(*) FROM dbo.SzablonyZamowien
UNION ALL
SELECT 'SzablonyZamowienTowar', COUNT(*) FROM dbo.SzablonyZamowienTowar
UNION ALL
SELECT 'HistoriaZmianZamowien', COUNT(*) FROM dbo.HistoriaZmianZamowien;
GO
