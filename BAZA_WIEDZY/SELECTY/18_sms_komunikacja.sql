-- ============================================================
-- 18 — SMS + ContactHistory + komunikacja
-- ============================================================
USE LibraNet;
GO

-- A) Struktury
SELECT
    TABLE_NAME, COLUMN_NAME, DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME IN ('SmsHistory', 'SmsChangeLog', 'ContactHistory',
                     'CallReminderLog', 'CallReminderContacts')
ORDER BY TABLE_NAME, ORDINAL_POSITION;
GO

-- B) Liczba rekordow
SELECT 'SmsHistory' AS tabela, COUNT(*) AS rekordow FROM dbo.SmsHistory
UNION ALL SELECT 'SmsChangeLog', COUNT(*) FROM dbo.SmsChangeLog
UNION ALL SELECT 'ContactHistory', COUNT(*) FROM dbo.ContactHistory
UNION ALL SELECT 'CallReminderLog', COUNT(*) FROM dbo.CallReminderLog
UNION ALL SELECT 'CallReminderContacts', COUNT(*) FROM dbo.CallReminderContacts;
GO

-- C) Sample 5 SmsHistory
SELECT TOP 5 *
FROM dbo.SmsHistory
ORDER BY 1 DESC;
GO

-- D) Sample 5 ContactHistory
SELECT TOP 5 *
FROM dbo.ContactHistory
ORDER BY 1 DESC;
GO

-- E) Sample 5 CallReminderLog
SELECT TOP 5 *
FROM dbo.CallReminderLog
ORDER BY 1 DESC;
GO
