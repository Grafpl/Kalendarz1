-- ============================================================================
-- CallLog — historia połączeń wykonanych przez MacroDroid (/call endpoint).
-- Każde kliknięcie "📞 Zadzwoń" zapisuje wpis z wynikiem (sukces/błąd).
--
-- Baza: LibraNet (192.168.0.109)
-- Uruchamiać raz na bazę (tabela tworzy się też automatycznie przy pierwszym
-- użyciu — patrz CallLogService.cs)
-- ============================================================================

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'CallLog' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    CREATE TABLE dbo.CallLog
    (
        Id           INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_CallLog PRIMARY KEY,
        Dostawca     NVARCHAR(255)    NULL,
        Numer        NVARCHAR(32)     NOT NULL,
        UserID       NVARCHAR(50)     NOT NULL,
        Zrodlo       NVARCHAR(50)     NOT NULL,            -- "Kalendarz Dostaw", "Cykle Wstawien" itp.
        Sukces       BIT              NOT NULL,
        StatusKod    INT              NULL,                -- HTTP status z MacroDroid
        Komunikat    NVARCHAR(500)    NULL,                -- treść błędu jeśli brak
        CreatedAt    DATETIME         NOT NULL CONSTRAINT DF_CallLog_CreatedAt DEFAULT (GETDATE())
    );

    CREATE INDEX IX_CallLog_Dostawca_CreatedAt ON dbo.CallLog (Dostawca, CreatedAt DESC);
    CREATE INDEX IX_CallLog_UserID_CreatedAt   ON dbo.CallLog (UserID, CreatedAt DESC);
    CREATE INDEX IX_CallLog_CreatedAt          ON dbo.CallLog (CreatedAt DESC);

    PRINT 'Utworzono tabelę dbo.CallLog + 3 indeksy.';
END
ELSE
    PRINT 'Tabela dbo.CallLog już istnieje — pomijam.';
GO
