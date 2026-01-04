-- =============================================================================
-- SPECYFIKACJA SUROWCA - AUDIT & PDF HISTORY
-- Autor: Claude AI
-- Data: 2026-01-04
-- =============================================================================

-- =============================================================================
-- 1. TABELA PdfHistory - Historia generowania PDF
-- =============================================================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[PdfHistory]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[PdfHistory] (
        [ID] INT IDENTITY(1,1) PRIMARY KEY,
        [FarmerCalcIDs] NVARCHAR(500) NOT NULL,      -- Lista ID rozdzielona przecinkami
        [DostawcaGID] INT NULL,                       -- GID dostawcy
        [DostawcaNazwa] NVARCHAR(200) NULL,          -- Nazwa dostawcy (dla czytelności)
        [CalcDate] DATE NOT NULL,                     -- Dzień ubojowy
        [PdfPath] NVARCHAR(500) NOT NULL,            -- Pełna ścieżka do pliku PDF
        [PdfFileName] NVARCHAR(200) NOT NULL,        -- Nazwa pliku PDF
        [GeneratedBy] NVARCHAR(100) NOT NULL,        -- Użytkownik który wygenerował
        [GeneratedAt] DATETIME NOT NULL DEFAULT GETDATE(),  -- Data i czas generowania
        [FileSize] BIGINT NULL,                       -- Rozmiar pliku w bajtach
        [IsDeleted] BIT NOT NULL DEFAULT 0           -- Czy plik został usunięty
    );

    CREATE INDEX IX_PdfHistory_FarmerCalcIDs ON [dbo].[PdfHistory] ([FarmerCalcIDs]);
    CREATE INDEX IX_PdfHistory_DostawcaGID ON [dbo].[PdfHistory] ([DostawcaGID]);
    CREATE INDEX IX_PdfHistory_CalcDate ON [dbo].[PdfHistory] ([CalcDate]);

    PRINT 'Utworzono tabelę PdfHistory';
END
ELSE
    PRINT 'Tabela PdfHistory już istnieje';
GO

-- =============================================================================
-- 2. TABELA ChangeLog - Historia zmian w FarmerCalc
-- =============================================================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[FarmerCalcChangeLog]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[FarmerCalcChangeLog] (
        [ID] INT IDENTITY(1,1) PRIMARY KEY,
        [FarmerCalcID] INT NOT NULL,                  -- ID rekordu w FarmerCalc
        [FieldName] NVARCHAR(100) NOT NULL,          -- Nazwa zmienionego pola
        [OldValue] NVARCHAR(MAX) NULL,               -- Poprzednia wartość
        [NewValue] NVARCHAR(MAX) NULL,               -- Nowa wartość
        [ChangedBy] NVARCHAR(100) NOT NULL,          -- Użytkownik który zmienił
        [ChangedAt] DATETIME NOT NULL DEFAULT GETDATE(),  -- Data i czas zmiany
        [ChangeSource] NVARCHAR(50) NULL DEFAULT 'APP'  -- Źródło zmiany (APP/TRIGGER/IMPORT)
    );

    CREATE INDEX IX_FarmerCalcChangeLog_FarmerCalcID ON [dbo].[FarmerCalcChangeLog] ([FarmerCalcID]);
    CREATE INDEX IX_FarmerCalcChangeLog_ChangedAt ON [dbo].[FarmerCalcChangeLog] ([ChangedAt]);
    CREATE INDEX IX_FarmerCalcChangeLog_FieldName ON [dbo].[FarmerCalcChangeLog] ([FieldName]);

    PRINT 'Utworzono tabelę FarmerCalcChangeLog';
END
ELSE
    PRINT 'Tabela FarmerCalcChangeLog już istnieje';
GO

-- =============================================================================
-- 3. TRIGGER - Automatyczne logowanie zmian w FarmerCalc
-- =============================================================================
IF EXISTS (SELECT * FROM sys.triggers WHERE name = 'TR_FarmerCalc_AuditLog')
    DROP TRIGGER [dbo].[TR_FarmerCalc_AuditLog];
GO

CREATE TRIGGER [dbo].[TR_FarmerCalc_AuditLog]
ON [dbo].[FarmerCalc]
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @UserName NVARCHAR(100) = SUSER_SNAME();

    -- Loguj zmiany tylko dla wybranych pól (te które edytuje użytkownik w Specyfikacji)

    -- Price (Cena)
    INSERT INTO [dbo].[FarmerCalcChangeLog] ([FarmerCalcID], [FieldName], [OldValue], [NewValue], [ChangedBy], [ChangeSource])
    SELECT i.ID, 'Price', CAST(d.Price AS NVARCHAR(MAX)), CAST(i.Price AS NVARCHAR(MAX)), @UserName, 'TRIGGER'
    FROM inserted i INNER JOIN deleted d ON i.ID = d.ID
    WHERE ISNULL(i.Price, 0) <> ISNULL(d.Price, 0);

    -- Addition (Dodatek)
    INSERT INTO [dbo].[FarmerCalcChangeLog] ([FarmerCalcID], [FieldName], [OldValue], [NewValue], [ChangedBy], [ChangeSource])
    SELECT i.ID, 'Addition', CAST(d.Addition AS NVARCHAR(MAX)), CAST(i.Addition AS NVARCHAR(MAX)), @UserName, 'TRIGGER'
    FROM inserted i INNER JOIN deleted d ON i.ID = d.ID
    WHERE ISNULL(i.Addition, 0) <> ISNULL(d.Addition, 0);

    -- Loss (Ubytek%)
    INSERT INTO [dbo].[FarmerCalcChangeLog] ([FarmerCalcID], [FieldName], [OldValue], [NewValue], [ChangedBy], [ChangeSource])
    SELECT i.ID, 'Loss', CAST(d.Loss AS NVARCHAR(MAX)), CAST(i.Loss AS NVARCHAR(MAX)), @UserName, 'TRIGGER'
    FROM inserted i INNER JOIN deleted d ON i.ID = d.ID
    WHERE ISNULL(i.Loss, 0) <> ISNULL(d.Loss, 0);

    -- PriceTypeID (Typ Ceny)
    INSERT INTO [dbo].[FarmerCalcChangeLog] ([FarmerCalcID], [FieldName], [OldValue], [NewValue], [ChangedBy], [ChangeSource])
    SELECT i.ID, 'PriceTypeID', CAST(d.PriceTypeID AS NVARCHAR(MAX)), CAST(i.PriceTypeID AS NVARCHAR(MAX)), @UserName, 'TRIGGER'
    FROM inserted i INNER JOIN deleted d ON i.ID = d.ID
    WHERE ISNULL(i.PriceTypeID, 0) <> ISNULL(d.PriceTypeID, 0);

    -- CustomerGID (Dostawca)
    INSERT INTO [dbo].[FarmerCalcChangeLog] ([FarmerCalcID], [FieldName], [OldValue], [NewValue], [ChangedBy], [ChangeSource])
    SELECT i.ID, 'CustomerGID', CAST(d.CustomerGID AS NVARCHAR(MAX)), CAST(i.CustomerGID AS NVARCHAR(MAX)), @UserName, 'TRIGGER'
    FROM inserted i INNER JOIN deleted d ON i.ID = d.ID
    WHERE ISNULL(i.CustomerGID, 0) <> ISNULL(d.CustomerGID, 0);

    -- LumQnt (LUMEL)
    INSERT INTO [dbo].[FarmerCalcChangeLog] ([FarmerCalcID], [FieldName], [OldValue], [NewValue], [ChangedBy], [ChangeSource])
    SELECT i.ID, 'LumQnt', CAST(d.LumQnt AS NVARCHAR(MAX)), CAST(i.LumQnt AS NVARCHAR(MAX)), @UserName, 'TRIGGER'
    FROM inserted i INNER JOIN deleted d ON i.ID = d.ID
    WHERE ISNULL(i.LumQnt, 0) <> ISNULL(d.LumQnt, 0);

    -- DeclI1 (SztukiDek)
    INSERT INTO [dbo].[FarmerCalcChangeLog] ([FarmerCalcID], [FieldName], [OldValue], [NewValue], [ChangedBy], [ChangeSource])
    SELECT i.ID, 'DeclI1', CAST(d.DeclI1 AS NVARCHAR(MAX)), CAST(i.DeclI1 AS NVARCHAR(MAX)), @UserName, 'TRIGGER'
    FROM inserted i INNER JOIN deleted d ON i.ID = d.ID
    WHERE ISNULL(i.DeclI1, 0) <> ISNULL(d.DeclI1, 0);

    -- DeclI2 (Padłe)
    INSERT INTO [dbo].[FarmerCalcChangeLog] ([FarmerCalcID], [FieldName], [OldValue], [NewValue], [ChangedBy], [ChangeSource])
    SELECT i.ID, 'DeclI2', CAST(d.DeclI2 AS NVARCHAR(MAX)), CAST(i.DeclI2 AS NVARCHAR(MAX)), @UserName, 'TRIGGER'
    FROM inserted i INNER JOIN deleted d ON i.ID = d.ID
    WHERE ISNULL(i.DeclI2, 0) <> ISNULL(d.DeclI2, 0);

    -- DeclI3 (CH)
    INSERT INTO [dbo].[FarmerCalcChangeLog] ([FarmerCalcID], [FieldName], [OldValue], [NewValue], [ChangedBy], [ChangeSource])
    SELECT i.ID, 'DeclI3', CAST(d.DeclI3 AS NVARCHAR(MAX)), CAST(i.DeclI3 AS NVARCHAR(MAX)), @UserName, 'TRIGGER'
    FROM inserted i INNER JOIN deleted d ON i.ID = d.ID
    WHERE ISNULL(i.DeclI3, 0) <> ISNULL(d.DeclI3, 0);

    -- DeclI4 (NW)
    INSERT INTO [dbo].[FarmerCalcChangeLog] ([FarmerCalcID], [FieldName], [OldValue], [NewValue], [ChangedBy], [ChangeSource])
    SELECT i.ID, 'DeclI4', CAST(d.DeclI4 AS NVARCHAR(MAX)), CAST(i.DeclI4 AS NVARCHAR(MAX)), @UserName, 'TRIGGER'
    FROM inserted i INNER JOIN deleted d ON i.ID = d.ID
    WHERE ISNULL(i.DeclI4, 0) <> ISNULL(d.DeclI4, 0);

    -- DeclI5 (ZM)
    INSERT INTO [dbo].[FarmerCalcChangeLog] ([FarmerCalcID], [FieldName], [OldValue], [NewValue], [ChangedBy], [ChangeSource])
    SELECT i.ID, 'DeclI5', CAST(d.DeclI5 AS NVARCHAR(MAX)), CAST(i.DeclI5 AS NVARCHAR(MAX)), @UserName, 'TRIGGER'
    FROM inserted i INNER JOIN deleted d ON i.ID = d.ID
    WHERE ISNULL(i.DeclI5, 0) <> ISNULL(d.DeclI5, 0);

    -- Opasienie
    INSERT INTO [dbo].[FarmerCalcChangeLog] ([FarmerCalcID], [FieldName], [OldValue], [NewValue], [ChangedBy], [ChangeSource])
    SELECT i.ID, 'Opasienie', CAST(d.Opasienie AS NVARCHAR(MAX)), CAST(i.Opasienie AS NVARCHAR(MAX)), @UserName, 'TRIGGER'
    FROM inserted i INNER JOIN deleted d ON i.ID = d.ID
    WHERE ISNULL(i.Opasienie, 0) <> ISNULL(d.Opasienie, 0);

    -- KlasaB
    INSERT INTO [dbo].[FarmerCalcChangeLog] ([FarmerCalcID], [FieldName], [OldValue], [NewValue], [ChangedBy], [ChangeSource])
    SELECT i.ID, 'KlasaB', CAST(d.KlasaB AS NVARCHAR(MAX)), CAST(i.KlasaB AS NVARCHAR(MAX)), @UserName, 'TRIGGER'
    FROM inserted i INNER JOIN deleted d ON i.ID = d.ID
    WHERE ISNULL(i.KlasaB, 0) <> ISNULL(d.KlasaB, 0);

    -- IncDeadConf (PiK)
    INSERT INTO [dbo].[FarmerCalcChangeLog] ([FarmerCalcID], [FieldName], [OldValue], [NewValue], [ChangedBy], [ChangeSource])
    SELECT i.ID, 'IncDeadConf', CAST(d.IncDeadConf AS NVARCHAR(MAX)), CAST(i.IncDeadConf AS NVARCHAR(MAX)), @UserName, 'TRIGGER'
    FROM inserted i INNER JOIN deleted d ON i.ID = d.ID
    WHERE ISNULL(i.IncDeadConf, 0) <> ISNULL(d.IncDeadConf, 0);

    -- CarLp (Kolejność)
    INSERT INTO [dbo].[FarmerCalcChangeLog] ([FarmerCalcID], [FieldName], [OldValue], [NewValue], [ChangedBy], [ChangeSource])
    SELECT i.ID, 'CarLp', CAST(d.CarLp AS NVARCHAR(MAX)), CAST(i.CarLp AS NVARCHAR(MAX)), @UserName, 'TRIGGER'
    FROM inserted i INNER JOIN deleted d ON i.ID = d.ID
    WHERE ISNULL(i.CarLp, 0) <> ISNULL(d.CarLp, 0);
END;
GO

PRINT 'Utworzono trigger TR_FarmerCalc_AuditLog';
GO

-- =============================================================================
-- 4. WIDOK - Podgląd ostatnich zmian (opcjonalny)
-- =============================================================================
IF EXISTS (SELECT * FROM sys.views WHERE name = 'VW_FarmerCalcRecentChanges')
    DROP VIEW [dbo].[VW_FarmerCalcRecentChanges];
GO

CREATE VIEW [dbo].[VW_FarmerCalcRecentChanges]
AS
SELECT TOP 1000
    cl.ID,
    cl.FarmerCalcID,
    fc.CalcDate AS DzienUbojowy,
    cl.FieldName,
    cl.OldValue,
    cl.NewValue,
    cl.ChangedBy,
    cl.ChangedAt,
    cl.ChangeSource
FROM [dbo].[FarmerCalcChangeLog] cl
LEFT JOIN [dbo].[FarmerCalc] fc ON cl.FarmerCalcID = fc.ID;
GO

PRINT 'Utworzono widok VW_FarmerCalcRecentChanges';
GO

-- =============================================================================
-- 5. WIDOK - Historia PDF dla danego dostawcy
-- =============================================================================
IF EXISTS (SELECT * FROM sys.views WHERE name = 'VW_PdfHistoryWithDetails')
    DROP VIEW [dbo].[VW_PdfHistoryWithDetails];
GO

CREATE VIEW [dbo].[VW_PdfHistoryWithDetails]
AS
SELECT
    ph.ID,
    ph.FarmerCalcIDs,
    ph.DostawcaGID,
    ph.DostawcaNazwa,
    ph.CalcDate,
    ph.PdfPath,
    ph.PdfFileName,
    ph.GeneratedBy,
    ph.GeneratedAt,
    ph.FileSize,
    ph.IsDeleted,
    CASE WHEN ph.FileSize IS NOT NULL
         THEN CAST(ROUND(ph.FileSize / 1024.0, 1) AS NVARCHAR(20)) + ' KB'
         ELSE 'N/A'
    END AS FileSizeFormatted
FROM [dbo].[PdfHistory] ph
WHERE ph.IsDeleted = 0;
GO

PRINT 'Utworzono widok VW_PdfHistoryWithDetails';
GO

PRINT '=== INSTALACJA ZAKOŃCZONA POMYŚLNIE ===';
