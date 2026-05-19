-- ═══════════════════════════════════════════════════════════════════════════
-- Faza 9-A — TransportZmianyQueue + 3 triggery na LibraNet.ZamowieniaMieso
-- ═══════════════════════════════════════════════════════════════════════════
-- Cel: zamiana polling (snapshot+diff co 2 minuty) na trigger-based queue
--      z latencją sekundową.
--
-- Mechanizm:
--   1. INSERT/UPDATE/DELETE na ZamowieniaMieso → trigger fires
--   2. Trigger zapisuje 1 wiersz do TransportZmianyQueue (Id+OperationType)
--   3. Aplikacja co 30s woła ConsumeQueueAsync — która sprawdza queue,
--      uruchamia DetectNewOrdersAsync gdy są nowe wpisy, marks processed
--
-- Bezpieczeństwo:
--   - Każdy trigger ma TRY/CATCH — błąd zapisu do kolejki nie blokuje
--     main INSERT/UPDATE/DELETE handlowca w ZamowieniaMieso (krytyczne!)
--   - Queue table ma filtered index na Processed=0 (skanowanie tylko
--     nieprzetworzonych)
--
-- Uruchom w SSMS na bazie LibraNet (192.168.0.109).
-- IDEMPOTENT — można uruchamiać wielokrotnie bezpiecznie.
-- ═══════════════════════════════════════════════════════════════════════════

USE LibraNet;
GO

-- ────────────────────────────────────────────────────────────
-- KROK 1: Tabela TransportZmianyQueue
-- ────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'TransportZmianyQueue')
BEGIN
    CREATE TABLE dbo.TransportZmianyQueue (
        Id              bigint        IDENTITY(1,1) NOT NULL PRIMARY KEY,
        OperationType   varchar(10)   NOT NULL,
        ZamowienieId    int           NOT NULL,
        OccurredAtUTC   datetime2     NOT NULL DEFAULT SYSUTCDATETIME(),
        Processed       bit           NOT NULL DEFAULT 0,
        ProcessedAtUTC  datetime2     NULL,
        CONSTRAINT CK_TZQ_OperationType CHECK (OperationType IN ('INSERT', 'UPDATE', 'DELETE'))
    );
    PRINT 'Utworzono tabele dbo.TransportZmianyQueue';
END
ELSE
BEGIN
    PRINT 'Tabela dbo.TransportZmianyQueue juz istnieje — pomijam.';
END
GO

-- Filtered index na nieprzetworzonych (ultra szybki scan unprocessed)
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_TZQ_Unprocessed')
BEGIN
    CREATE INDEX IX_TZQ_Unprocessed
        ON dbo.TransportZmianyQueue(Processed, Id)
        WHERE Processed = 0;
    PRINT 'Utworzono indeks IX_TZQ_Unprocessed (filtered).';
END
GO

-- ────────────────────────────────────────────────────────────
-- KROK 2: Trigger AFTER INSERT
-- ────────────────────────────────────────────────────────────
CREATE OR ALTER TRIGGER dbo.tr_ZamowieniaMieso_AfterInsert
    ON dbo.ZamowieniaMieso
    AFTER INSERT
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRY
        INSERT INTO dbo.TransportZmianyQueue (OperationType, ZamowienieId)
        SELECT 'INSERT', Id FROM inserted;
    END TRY
    BEGIN CATCH
        -- Defensive: blad zapisu do kolejki NIE moze zablokowac INSERT do ZamowieniaMieso
        -- (handlowiec stracilby mozliwosc zapisania zamowienia). Log do trace, ignoruj.
        DECLARE @msg nvarchar(2048) = ERROR_MESSAGE();
        RAISERROR(N'Trigger tr_ZamowieniaMieso_AfterInsert error: %s', 0, 1, @msg) WITH NOWAIT;
    END CATCH
END
GO
PRINT 'Trigger tr_ZamowieniaMieso_AfterInsert OK';

-- ────────────────────────────────────────────────────────────
-- KROK 3: Trigger AFTER UPDATE
-- ────────────────────────────────────────────────────────────
CREATE OR ALTER TRIGGER dbo.tr_ZamowieniaMieso_AfterUpdate
    ON dbo.ZamowieniaMieso
    AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRY
        INSERT INTO dbo.TransportZmianyQueue (OperationType, ZamowienieId)
        SELECT 'UPDATE', Id FROM inserted;
    END TRY
    BEGIN CATCH
        DECLARE @msg nvarchar(2048) = ERROR_MESSAGE();
        RAISERROR(N'Trigger tr_ZamowieniaMieso_AfterUpdate error: %s', 0, 1, @msg) WITH NOWAIT;
    END CATCH
END
GO
PRINT 'Trigger tr_ZamowieniaMieso_AfterUpdate OK';

-- ────────────────────────────────────────────────────────────
-- KROK 4: Trigger AFTER DELETE
-- ────────────────────────────────────────────────────────────
CREATE OR ALTER TRIGGER dbo.tr_ZamowieniaMieso_AfterDelete
    ON dbo.ZamowieniaMieso
    AFTER DELETE
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRY
        INSERT INTO dbo.TransportZmianyQueue (OperationType, ZamowienieId)
        SELECT 'DELETE', Id FROM deleted;
    END TRY
    BEGIN CATCH
        DECLARE @msg nvarchar(2048) = ERROR_MESSAGE();
        RAISERROR(N'Trigger tr_ZamowieniaMieso_AfterDelete error: %s', 0, 1, @msg) WITH NOWAIT;
    END CATCH
END
GO
PRINT 'Trigger tr_ZamowieniaMieso_AfterDelete OK';

-- ────────────────────────────────────────────────────────────
-- KROK 5: Procedura cleanup (opcjonalnie wywoływana z aplikacji co 24h)
-- ────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE dbo.sp_TransportZmianyQueueCleanup
    @KeepHours int = 168  -- domyślnie 7 dni
AS
BEGIN
    SET NOCOUNT ON;
    DELETE FROM dbo.TransportZmianyQueue
    WHERE Processed = 1
      AND ProcessedAtUTC < DATEADD(HOUR, -@KeepHours, SYSUTCDATETIME());
    SELECT @@ROWCOUNT AS Deleted;
END
GO
PRINT 'Procedura sp_TransportZmianyQueueCleanup OK';

-- ────────────────────────────────────────────────────────────
-- KROK 6: Weryfikacja
-- ────────────────────────────────────────────────────────────
PRINT '';
PRINT '═══ Weryfikacja Faza 9-A ═══';

SELECT 'Tabela TransportZmianyQueue' AS Element,
       CASE WHEN EXISTS (SELECT 1 FROM sys.tables WHERE name = 'TransportZmianyQueue')
            THEN N'✓ Istnieje' ELSE N'✗ BRAK' END AS Status,
       (SELECT COUNT(*) FROM dbo.TransportZmianyQueue) AS LiczbaWierszy
UNION ALL
SELECT 'Filtered index IX_TZQ_Unprocessed',
       CASE WHEN EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_TZQ_Unprocessed')
            THEN N'✓ Istnieje' ELSE N'✗ BRAK' END,
       NULL
UNION ALL
SELECT 'Trigger tr_ZamowieniaMieso_AfterInsert',
       CASE WHEN EXISTS (SELECT 1 FROM sys.triggers WHERE name = 'tr_ZamowieniaMieso_AfterInsert')
            THEN N'✓ Aktywny' ELSE N'✗ BRAK' END,
       NULL
UNION ALL
SELECT 'Trigger tr_ZamowieniaMieso_AfterUpdate',
       CASE WHEN EXISTS (SELECT 1 FROM sys.triggers WHERE name = 'tr_ZamowieniaMieso_AfterUpdate')
            THEN N'✓ Aktywny' ELSE N'✗ BRAK' END,
       NULL
UNION ALL
SELECT 'Trigger tr_ZamowieniaMieso_AfterDelete',
       CASE WHEN EXISTS (SELECT 1 FROM sys.triggers WHERE name = 'tr_ZamowieniaMieso_AfterDelete')
            THEN N'✓ Aktywny' ELSE N'✗ BRAK' END,
       NULL
UNION ALL
SELECT 'Procedura sp_TransportZmianyQueueCleanup',
       CASE WHEN EXISTS (SELECT 1 FROM sys.procedures WHERE name = 'sp_TransportZmianyQueueCleanup')
            THEN N'✓ Istnieje' ELSE N'✗ BRAK' END,
       NULL;

PRINT '';
PRINT 'Po uruchomieniu SQL:';
PRINT '  1. Restartuj aplikacje (zaladuje nowy Menu.cs z timerem ConsumeQueueAsync co 30s)';
PRINT '  2. Otworz Transport Hub > Tab Zmiany';
PRINT '  3. Edytuj zamowienie w innym oknie (LibraNet ZamowieniaMieso)';
PRINT '  4. Zmiana powinna pojawic sie w Tab Zmiany w ciagu 30 sekund (zamiast 2 minut polling)';
GO

-- ────────────────────────────────────────────────────────────
-- ROLLBACK (gdyby trzeba cofnac)
-- ────────────────────────────────────────────────────────────
-- DROP TRIGGER dbo.tr_ZamowieniaMieso_AfterInsert;
-- DROP TRIGGER dbo.tr_ZamowieniaMieso_AfterUpdate;
-- DROP TRIGGER dbo.tr_ZamowieniaMieso_AfterDelete;
-- DROP PROCEDURE dbo.sp_TransportZmianyQueueCleanup;
-- DROP TABLE dbo.TransportZmianyQueue;
