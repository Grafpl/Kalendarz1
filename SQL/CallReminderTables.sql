-- =====================================================
-- SYSTEM PRZYPOMNIEŃ O TELEFONACH DO KLIENTÓW CRM
-- =====================================================

-- Tabela konfiguracji przypomnień dla każdego użytkownika
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'CallReminderConfig')
BEGIN
    CREATE TABLE CallReminderConfig (
        ID INT IDENTITY(1,1) PRIMARY KEY,
        UserID NVARCHAR(50) NOT NULL,           -- ID operatora (handlowca)
        IsEnabled BIT DEFAULT 1,                 -- Czy przypomnienia włączone
        ReminderTime1 TIME DEFAULT '10:00:00',   -- Pierwsza godzina przypomnienia
        ReminderTime2 TIME DEFAULT '13:00:00',   -- Druga godzina przypomnienia
        ContactsPerReminder INT DEFAULT 5,       -- Ile kontaktów pokazać
        ShowOnlyNewContacts BIT DEFAULT 1,       -- Tylko status "Do zadzwonienia"
        ShowOnlyAssigned BIT DEFAULT 0,          -- Tylko przypisani do handlowca
        MinutesTolerance INT DEFAULT 15,         -- Tolerancja czasowa (minuty)
        CreatedAt DATETIME DEFAULT GETDATE(),
        ModifiedAt DATETIME DEFAULT GETDATE(),
        ModifiedBy NVARCHAR(50),
        CONSTRAINT UQ_CallReminderConfig_UserID UNIQUE (UserID)
    );
    PRINT 'Utworzono tabelę CallReminderConfig';
END
GO

-- Tabela logów wykonanych przypomnień
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'CallReminderLog')
BEGIN
    CREATE TABLE CallReminderLog (
        ID INT IDENTITY(1,1) PRIMARY KEY,
        UserID NVARCHAR(50) NOT NULL,
        ReminderTime DATETIME NOT NULL,
        ContactsShown INT DEFAULT 0,             -- Ile kontaktów pokazano
        ContactsCalled INT DEFAULT 0,            -- Ile zadzwoniono
        NotesAdded INT DEFAULT 0,                -- Ile notatek dodano
        StatusChanges INT DEFAULT 0,             -- Ile zmian statusu
        WasSkipped BIT DEFAULT 0,                -- Czy pominięto
        SkipReason NVARCHAR(500),
        CompletedAt DATETIME
    );

    CREATE INDEX IX_CallReminderLog_UserID ON CallReminderLog(UserID);
    CREATE INDEX IX_CallReminderLog_ReminderTime ON CallReminderLog(ReminderTime);
    PRINT 'Utworzono tabelę CallReminderLog';
END
GO

-- Tabela szczegółów kontaktów w przypomnieniu
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'CallReminderContacts')
BEGIN
    CREATE TABLE CallReminderContacts (
        ID INT IDENTITY(1,1) PRIMARY KEY,
        ReminderLogID INT NOT NULL,
        ContactID INT NOT NULL,                  -- ID z OdbiorcyCRM
        WasCalled BIT DEFAULT 0,
        NoteAdded BIT DEFAULT 0,
        StatusChanged BIT DEFAULT 0,
        NewStatus NVARCHAR(100),
        ActionTime DATETIME,
        CONSTRAINT FK_CallReminderContacts_Log FOREIGN KEY (ReminderLogID)
            REFERENCES CallReminderLog(ID) ON DELETE CASCADE
    );

    CREATE INDEX IX_CallReminderContacts_LogID ON CallReminderContacts(ReminderLogID);
    CREATE INDEX IX_CallReminderContacts_ContactID ON CallReminderContacts(ContactID);
    PRINT 'Utworzono tabelę CallReminderContacts';
END
GO

-- Procedura pobierania losowych kontaktów do obdzwonienia
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'GetRandomContactsForReminder')
    DROP PROCEDURE GetRandomContactsForReminder;
GO

CREATE PROCEDURE GetRandomContactsForReminder
    @UserID NVARCHAR(50),
    @Count INT = 5,
    @OnlyNew BIT = 1,
    @OnlyAssigned BIT = 0
AS
BEGIN
    SET NOCOUNT ON;

    -- Pobierz kontakty które nie były pokazane dzisiaj
    SELECT TOP (@Count)
        o.ID,
        o.Nazwa,
        o.Telefon_K as Telefon,
        o.Email,
        o.MIASTO,
        o.Wojewodztwo,
        ISNULL(o.Status, 'Do zadzwonienia') as Status,
        o.PKD_Opis as Branza,
        (SELECT TOP 1 n.Tresc FROM NotatkiCRM n WHERE n.IDOdbiorcy = o.ID ORDER BY n.DataDodania DESC) as OstatniaNota,
        (SELECT TOP 1 n.DataDodania FROM NotatkiCRM n WHERE n.IDOdbiorcy = o.ID ORDER BY n.DataDodania DESC) as DataOstatniejNotatki
    FROM OdbiorcyCRM o
    LEFT JOIN WlascicieleOdbiorcow w ON o.ID = w.IDOdbiorcy
    WHERE
        -- Filtr statusu
        (@OnlyNew = 0 OR ISNULL(o.Status, 'Do zadzwonienia') = 'Do zadzwonienia')
        -- Filtr przypisania
        AND (@OnlyAssigned = 0 OR w.OperatorID = @UserID)
        -- Wyklucz już obsłużone dziś
        AND o.ID NOT IN (
            SELECT crc.ContactID
            FROM CallReminderContacts crc
            INNER JOIN CallReminderLog crl ON crc.ReminderLogID = crl.ID
            WHERE crl.UserID = @UserID
              AND CAST(crl.ReminderTime AS DATE) = CAST(GETDATE() AS DATE)
        )
        -- Wyklucz usunięte/błędne
        AND ISNULL(o.Status, '') NOT IN ('Poprosił o usunięcie', 'Błędny rekord (do raportu)')
        -- Musi mieć telefon
        AND o.Telefon_K IS NOT NULL AND o.Telefon_K <> ''
    ORDER BY NEWID(); -- Losowe sortowanie
END
GO

-- Procedura pobierania statystyk dla panelu admina
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'GetCallReminderStats')
    DROP PROCEDURE GetCallReminderStats;
GO

CREATE PROCEDURE GetCallReminderStats
    @DateFrom DATE = NULL,
    @DateTo DATE = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF @DateFrom IS NULL SET @DateFrom = DATEADD(DAY, -30, GETDATE());
    IF @DateTo IS NULL SET @DateTo = GETDATE();

    SELECT
        crl.UserID,
        ISNULL(op.Name, crl.UserID) as UserName,
        COUNT(DISTINCT crl.ID) as TotalReminders,
        SUM(crl.ContactsShown) as TotalContactsShown,
        SUM(crl.ContactsCalled) as TotalCalls,
        SUM(crl.NotesAdded) as TotalNotes,
        SUM(crl.StatusChanges) as TotalStatusChanges,
        SUM(CASE WHEN crl.WasSkipped = 1 THEN 1 ELSE 0 END) as SkippedCount,
        CAST(AVG(CAST(crl.ContactsCalled AS FLOAT) / NULLIF(crl.ContactsShown, 0) * 100) AS DECIMAL(5,1)) as CallRate
    FROM CallReminderLog crl
    LEFT JOIN operators op ON crl.UserID = CAST(op.ID AS NVARCHAR)
    WHERE CAST(crl.ReminderTime AS DATE) BETWEEN @DateFrom AND @DateTo
    GROUP BY crl.UserID, op.Name
    ORDER BY TotalCalls DESC;
END
GO

PRINT 'Skrypt zakończony pomyślnie';
