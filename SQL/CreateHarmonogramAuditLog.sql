-- ============================================================================
-- Audit log dla zmian flag w HarmonogramDostaw (Utworzone/Wysłane/Otrzymane/Posrednik)
-- ============================================================================
-- Cel: odpowiadać na pytania "kto i kiedy odznaczył 'Wysłane' dla LP=1234?".
-- Zapis: każde toggle checkboxa w SprawdzalkaUmow → 1 wiersz tutaj.
-- Odczyt: prawy klik na wiersz → "Pokaż historię zmian".
-- ============================================================================

USE [LibraNet];
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'HarmonogramDostaw_AuditLog' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    PRINT 'Tworzenie tabeli HarmonogramDostaw_AuditLog...';

    CREATE TABLE [dbo].[HarmonogramDostaw_AuditLog] (
        [ID]         INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [LP]         INT NOT NULL,                  -- HarmonogramDostaw.LP
        [ColumnName] NVARCHAR(50) NOT NULL,         -- Utworzone | Wysłane | Otrzymane | Posrednik
        [OldValue]   BIT NULL,                      -- NULL = wcześniej brak rekordu
        [NewValue]   BIT NOT NULL,
        [UserID]     INT NULL,                      -- operators.ID, NULL gdy system
        [ChangedAt]  DATETIME NOT NULL DEFAULT GETDATE()
    );

    -- Indeks po LP do szybkich queries "historia konkretnej dostawy"
    CREATE NONCLUSTERED INDEX [IX_HarmonogramAudit_LP_ChangedAt]
    ON [dbo].[HarmonogramDostaw_AuditLog] ([LP], [ChangedAt] DESC);

    -- Indeks po ChangedAt do raportów aktywności (kto zmienił najwięcej w tygodniu)
    CREATE NONCLUSTERED INDEX [IX_HarmonogramAudit_ChangedAt]
    ON [dbo].[HarmonogramDostaw_AuditLog] ([ChangedAt] DESC)
    INCLUDE ([UserID], [ColumnName]);

    PRINT '✓ Tabela + 2 indeksy utworzone.';
END
ELSE
BEGIN
    PRINT '✓ Tabela już istnieje - pomijam.';
END
GO

-- ============================================================================
-- Test: pokaż 20 ostatnich zmian
-- ============================================================================
SELECT TOP 20
    a.ChangedAt, a.LP, a.ColumnName,
    CASE WHEN a.OldValue = 1 THEN 'TAK' WHEN a.OldValue = 0 THEN 'NIE' ELSE '(brak)' END AS OldValue,
    CASE WHEN a.NewValue = 1 THEN 'TAK' ELSE 'NIE' END AS NewValue,
    ISNULL(o.Name, CAST(a.UserID AS VARCHAR(20))) AS UserName
FROM dbo.HarmonogramDostaw_AuditLog a
LEFT JOIN dbo.operators o ON a.UserID = o.ID
ORDER BY a.ChangedAt DESC;
GO

-- ============================================================================
-- ROLLBACK
-- ============================================================================
-- DROP TABLE [dbo].[HarmonogramDostaw_AuditLog];
