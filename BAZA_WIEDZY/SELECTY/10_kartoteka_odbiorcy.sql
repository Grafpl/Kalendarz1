-- ============================================================
-- 10 — Kartoteka Odbiorcy (CRM klientow)
-- ============================================================
USE LibraNet;
GO

-- A) Struktury 5 tabel CRM
SELECT
    TABLE_NAME,
    ORDINAL_POSITION,
    COLUMN_NAME,
    DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH,
    IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME IN ('KartotekaOdbiorcyDane',
                     'KartotekaOdbiorcyKontakty',
                     'KartotekaOdbiorcyNotatki',
                     'KartotekaPrzypomnienia',
                     'KartotekaScoring',
                     'KartotekaHistoriaZmian')
ORDER BY TABLE_NAME, ORDINAL_POSITION;
GO

-- B) Liczba rekordow per tabela
SELECT 'KartotekaOdbiorcyDane' AS tabela, COUNT(*) AS rekordow FROM dbo.KartotekaOdbiorcyDane
UNION ALL
SELECT 'KartotekaOdbiorcyKontakty', COUNT(*) FROM dbo.KartotekaOdbiorcyKontakty
UNION ALL
SELECT 'KartotekaOdbiorcyNotatki', COUNT(*) FROM dbo.KartotekaOdbiorcyNotatki
UNION ALL
SELECT 'KartotekaPrzypomnienia', COUNT(*) FROM dbo.KartotekaPrzypomnienia
UNION ALL
SELECT 'KartotekaScoring', COUNT(*) FROM dbo.KartotekaScoring
UNION ALL
SELECT 'KartotekaHistoriaZmian', COUNT(*) FROM dbo.KartotekaHistoriaZmian;
GO

-- C) Sample 5 rekordow z kazdej (jesli istnieja)
SELECT TOP 5 * FROM dbo.KartotekaOdbiorcyDane;
GO

SELECT TOP 5 * FROM dbo.KartotekaOdbiorcyKontakty;
GO

SELECT TOP 5 * FROM dbo.KartotekaOdbiorcyNotatki;
GO

SELECT TOP 5 * FROM dbo.KartotekaPrzypomnienia;
GO

SELECT TOP 5 * FROM dbo.KartotekaScoring;
GO

-- D) ContactHistory + SmsHistory
SELECT
    TABLE_NAME, COLUMN_NAME, DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME IN ('ContactHistory', 'SmsHistory', 'SmsChangeLog')
ORDER BY TABLE_NAME, ORDINAL_POSITION;
GO

SELECT 'ContactHistory' AS tabela, COUNT(*) AS rekordow FROM dbo.ContactHistory
UNION ALL
SELECT 'SmsHistory', COUNT(*) FROM dbo.SmsHistory
UNION ALL
SELECT 'SmsChangeLog', COUNT(*) FROM dbo.SmsChangeLog;
GO
