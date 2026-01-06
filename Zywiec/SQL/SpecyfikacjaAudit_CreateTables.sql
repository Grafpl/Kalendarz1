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
-- 3. TRIGGER - USUNIĘTY (logowanie obsługiwane przez aplikację)
-- =============================================================================
-- Trigger został usunięty, ponieważ logowanie zmian jest teraz obsługiwane
-- przez aplikację w LogChangeToDatabase() z pełnymi danymi:
-- - Nr (LP wiersza)
-- - CarID (numer rejestracyjny auta)
-- - UserID (ID użytkownika z logowania)
-- - ChangedBy (nazwa użytkownika)
-- - Dostawca (nazwa dostawcy)
-- - CalcDate (data specyfikacji)
-- =============================================================================
IF EXISTS (SELECT * FROM sys.triggers WHERE name = 'TR_FarmerCalc_AuditLog')
BEGIN
    DROP TRIGGER [dbo].[TR_FarmerCalc_AuditLog];
    PRINT 'Usunięto trigger TR_FarmerCalc_AuditLog - logowanie obsługiwane przez aplikację';
END
GO

-- =============================================================================
-- 3a. CZYSZCZENIE - Usuń stare wpisy bez pełnych danych (z triggera)
-- =============================================================================
-- Usuń wpisy które mają:
-- - ChangedBy = 'pronova' lub podobne (wpisy z triggera SQL)
-- - Brak Nr (LP = 0 lub NULL)
-- - Brak CarID
-- - Brak UserID
-- Te wpisy pochodzą ze starego triggera i nie zawierają pełnych informacji
-- =============================================================================
DELETE FROM [dbo].[FarmerCalcChangeLog]
WHERE (Nr IS NULL OR Nr = 0)
  AND (CarID IS NULL OR CarID = '')
  AND (UserID IS NULL OR UserID = '');

PRINT 'Usunięto stare wpisy bez pełnych danych (z triggera)';
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
