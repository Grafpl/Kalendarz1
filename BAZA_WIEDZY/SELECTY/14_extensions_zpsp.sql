-- ============================================================
-- 14 — Tabele rozszerzen ZPSP (PartiaStatus, QC_*, Flota, etc.)
-- ============================================================
USE LibraNet;
GO

-- A) Struktury tabel rozszerzen
SELECT
    TABLE_NAME,
    COLUMN_NAME,
    DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH,
    IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME IN (
    'PartiaStatus', 'PartiaAuditLog', 'QC_Normy', 'QC_Zdjecia',
    'TransportZmiany', 'DriverDetails', 'VehicleDetails',
    'DriverVehicleAssignment', 'VehicleServiceLog',
    'ArticleAuditLog', 'ArticleFavorites',
    'Pozyskiwanie_Hodowcy', 'Pozyskiwanie_Aktywnosci',
    'CallReminderLog', 'CallReminderContacts',
    'AuditLog_Dostawy', 'OdpadyRejestr',
    'StanyMagazynowe', 'DokumentyWZ', 'TowarZdjecia',
    'DostawaFeedback', 'DashboardWidoki',
    'KonfiguracjaProdukty', 'KonfiguracjaWydajnosc'
)
ORDER BY TABLE_NAME, ORDINAL_POSITION;
GO

-- B) Liczba wierszy w tabelach rozszerzen
SELECT 'PartiaStatus' AS tabela, COUNT(*) AS rekordow FROM dbo.PartiaStatus
UNION ALL SELECT 'PartiaAuditLog', COUNT(*) FROM dbo.PartiaAuditLog
UNION ALL SELECT 'QC_Normy', COUNT(*) FROM dbo.QC_Normy
UNION ALL SELECT 'QC_Zdjecia', COUNT(*) FROM dbo.QC_Zdjecia
UNION ALL SELECT 'TransportZmiany', COUNT(*) FROM dbo.TransportZmiany
UNION ALL SELECT 'DriverDetails', COUNT(*) FROM dbo.DriverDetails
UNION ALL SELECT 'VehicleDetails', COUNT(*) FROM dbo.VehicleDetails
UNION ALL SELECT 'DriverVehicleAssignment', COUNT(*) FROM dbo.DriverVehicleAssignment
UNION ALL SELECT 'VehicleServiceLog', COUNT(*) FROM dbo.VehicleServiceLog
UNION ALL SELECT 'ArticleAuditLog', COUNT(*) FROM dbo.ArticleAuditLog
UNION ALL SELECT 'ArticleFavorites', COUNT(*) FROM dbo.ArticleFavorites
UNION ALL SELECT 'Pozyskiwanie_Hodowcy', COUNT(*) FROM dbo.Pozyskiwanie_Hodowcy
UNION ALL SELECT 'Pozyskiwanie_Aktywnosci', COUNT(*) FROM dbo.Pozyskiwanie_Aktywnosci
UNION ALL SELECT 'CallReminderLog', COUNT(*) FROM dbo.CallReminderLog
UNION ALL SELECT 'CallReminderContacts', COUNT(*) FROM dbo.CallReminderContacts
UNION ALL SELECT 'AuditLog_Dostawy', COUNT(*) FROM dbo.AuditLog_Dostawy
UNION ALL SELECT 'OdpadyRejestr', COUNT(*) FROM dbo.OdpadyRejestr
UNION ALL SELECT 'StanyMagazynowe', COUNT(*) FROM dbo.StanyMagazynowe
UNION ALL SELECT 'DokumentyWZ', COUNT(*) FROM dbo.DokumentyWZ
UNION ALL SELECT 'TowarZdjecia', COUNT(*) FROM dbo.TowarZdjecia
UNION ALL SELECT 'DashboardWidoki', COUNT(*) FROM dbo.DashboardWidoki;
GO

-- C) PartiaStatus (10 najnowszych)
SELECT TOP 10 *
FROM dbo.PartiaStatus
ORDER BY 1 DESC;
GO

-- D) QC_Normy (wszystkie)
SELECT *
FROM dbo.QC_Normy
ORDER BY 1;
GO

-- E) Pozyskiwanie_Hodowcy - statystyki
SELECT TOP 10 *
FROM dbo.Pozyskiwanie_Hodowcy
ORDER BY 1 DESC;
GO
