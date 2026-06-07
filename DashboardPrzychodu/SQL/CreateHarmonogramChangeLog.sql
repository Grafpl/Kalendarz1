-- ============================================================================
-- Audit log dla HarmonogramDostaw - SztukiDek, WagaDek, Auta
-- Wczesniej zmiany w tych polach NIE byly nigdzie logowane.
-- Po wykonaniu tego skryptu kazda zmiana w UI/SQL automatycznie wpada do
-- HarmonogramDostaw_ChangeLog (przez trigger AFTER UPDATE).
--
-- Wykonaj raz na LibraNet (192.168.0.109).
-- ============================================================================

-- Tabela logu zmian
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'HarmonogramDostaw_ChangeLog')
BEGIN
    CREATE TABLE dbo.HarmonogramDostaw_ChangeLog (
        ID            INT IDENTITY(1,1) PRIMARY KEY,
        LP            INT NOT NULL,                  -- FK do HarmonogramDostaw.Lp
        DataOdbioru   DATE NOT NULL,                 -- skopiowane z HarmonogramDostaw dla szybkiego filtra
        Dostawca      NVARCHAR(200) NULL,            -- nazwa hodowcy (kopia)
        FieldName     NVARCHAR(50) NOT NULL,         -- SztukiDek / WagaDek / Auta
        OldValue      NVARCHAR(100) NULL,
        NewValue      NVARCHAR(100) NULL,
        UserName      NVARCHAR(100) NULL,            -- SYSTEM_USER lub przekazane przez aplikacje
        ChangedAt     DATETIME NOT NULL DEFAULT GETDATE()
    );

    CREATE INDEX IX_HarmonogramDostaw_ChangeLog_DataOdbioru
        ON dbo.HarmonogramDostaw_ChangeLog (DataOdbioru, ChangedAt DESC);
    CREATE INDEX IX_HarmonogramDostaw_ChangeLog_Lp
        ON dbo.HarmonogramDostaw_ChangeLog (LP);
END
GO

-- Trigger ktory loguje zmiany SztukiDek / WagaDek / Auta po UPDATE
IF OBJECT_ID('dbo.trg_HarmonogramDostaw_LogChanges', 'TR') IS NOT NULL
    DROP TRIGGER dbo.trg_HarmonogramDostaw_LogChanges;
GO

CREATE TRIGGER dbo.trg_HarmonogramDostaw_LogChanges
ON dbo.HarmonogramDostaw
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    -- SZTUKIDEK
    IF UPDATE(SztukiDek)
    BEGIN
        INSERT INTO dbo.HarmonogramDostaw_ChangeLog (LP, DataOdbioru, Dostawca, FieldName, OldValue, NewValue, UserName, ChangedAt)
        SELECT i.Lp, i.DataOdbioru, i.Dostawca, 'SztukiDek',
               CAST(d.SztukiDek AS NVARCHAR(100)),
               CAST(i.SztukiDek AS NVARCHAR(100)),
               SYSTEM_USER, GETDATE()
        FROM inserted i
        INNER JOIN deleted d ON d.Lp = i.Lp
        WHERE ISNULL(CAST(d.SztukiDek AS NVARCHAR(100)), '') <> ISNULL(CAST(i.SztukiDek AS NVARCHAR(100)), '');
    END

    -- WAGADEK
    IF UPDATE(WagaDek)
    BEGIN
        INSERT INTO dbo.HarmonogramDostaw_ChangeLog (LP, DataOdbioru, Dostawca, FieldName, OldValue, NewValue, UserName, ChangedAt)
        SELECT i.Lp, i.DataOdbioru, i.Dostawca, 'WagaDek',
               CAST(d.WagaDek AS NVARCHAR(100)),
               CAST(i.WagaDek AS NVARCHAR(100)),
               SYSTEM_USER, GETDATE()
        FROM inserted i
        INNER JOIN deleted d ON d.Lp = i.Lp
        WHERE ISNULL(CAST(d.WagaDek AS NVARCHAR(100)), '') <> ISNULL(CAST(i.WagaDek AS NVARCHAR(100)), '');
    END

    -- AUTA
    IF UPDATE(Auta)
    BEGIN
        INSERT INTO dbo.HarmonogramDostaw_ChangeLog (LP, DataOdbioru, Dostawca, FieldName, OldValue, NewValue, UserName, ChangedAt)
        SELECT i.Lp, i.DataOdbioru, i.Dostawca, 'Auta',
               CAST(d.Auta AS NVARCHAR(100)),
               CAST(i.Auta AS NVARCHAR(100)),
               SYSTEM_USER, GETDATE()
        FROM inserted i
        INNER JOIN deleted d ON d.Lp = i.Lp
        WHERE ISNULL(CAST(d.Auta AS NVARCHAR(100)), '') <> ISNULL(CAST(i.Auta AS NVARCHAR(100)), '');
    END
END
GO

PRINT 'OK - HarmonogramDostaw_ChangeLog + trigger utworzone';
