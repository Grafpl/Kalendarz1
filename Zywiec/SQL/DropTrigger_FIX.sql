-- =============================================================================
-- FIX: Usunięcie triggera powodującego niechciane wpisy PiK w logu zmian
-- URUCHOM TEN SKRYPT W SQL SERVER MANAGEMENT STUDIO (SSMS)
-- =============================================================================

-- 1. Sprawdź czy trigger istnieje
IF EXISTS (SELECT * FROM sys.triggers WHERE name = 'TR_FarmerCalc_AuditLog')
BEGIN
    PRINT '!!! Trigger TR_FarmerCalc_AuditLog ISTNIEJE - USUWANIE...'
    DROP TRIGGER [dbo].[TR_FarmerCalc_AuditLog];
    PRINT '>>> Trigger został USUNIĘTY'
END
ELSE
BEGIN
    PRINT 'OK: Trigger TR_FarmerCalc_AuditLog nie istnieje'
END
GO

-- 2. Usuń nieprawidłowe wpisy (bez pełnych danych z aplikacji)
PRINT ''
PRINT 'Czyszczenie starych wpisów bez pełnych danych...'

DECLARE @deleted INT;

DELETE FROM [dbo].[FarmerCalcChangeLog]
WHERE (Nr IS NULL OR Nr = 0)
  AND (CarID IS NULL OR CarID = '')
  AND (UserID IS NULL OR UserID = '');

SET @deleted = @@ROWCOUNT;
PRINT 'Usunięto ' + CAST(@deleted AS NVARCHAR(10)) + ' starych wpisów'
GO

-- 3. Weryfikacja - pokaż ostatnie wpisy
PRINT ''
PRINT 'Ostatnie 20 wpisów w logu zmian:'
SELECT TOP 20
    ID,
    FarmerCalcID,
    FieldName,
    OldValue,
    NewValue,
    Nr,
    CarID,
    UserID,
    ChangedBy,
    ChangedAt
FROM [dbo].[FarmerCalcChangeLog]
ORDER BY ChangedAt DESC;
GO

PRINT ''
PRINT '=== SKRYPT ZAKOŃCZONY ==='
