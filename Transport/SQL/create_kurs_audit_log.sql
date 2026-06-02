-- ════════════════════════════════════════════════════════════════════════
-- TransportPL.dbo.KursAuditLog — diff per pole nagłówka kursu.
-- Po każdej modyfikacji kursu (kierowca, pojazd, godziny, trasa, status)
-- zapisuje jeden wiersz na pole które się zmieniło.
-- Wpisuje TransportWpfService z poziomu edytora przed AktualizujNaglowekKursuAsync.
-- ════════════════════════════════════════════════════════════════════════

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'KursAuditLog')
BEGIN
    CREATE TABLE dbo.KursAuditLog (
        Id              BIGINT IDENTITY(1,1) PRIMARY KEY,
        KursID          BIGINT NOT NULL,
        Pole            NVARCHAR(50) NOT NULL,
        StareWartosc    NVARCHAR(500) NULL,
        NowaWartosc     NVARCHAR(500) NULL,
        KtoZmienil      NVARCHAR(50) NOT NULL,
        KiedyUTC        DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
    );

    CREATE INDEX IX_KursAudit_Kurs ON dbo.KursAuditLog (KursID, KiedyUTC DESC);
    CREATE INDEX IX_KursAudit_Kto ON dbo.KursAuditLog (KtoZmienil, KiedyUTC DESC);
END;
GO
