-- ============================================================================
-- Skrzynka Zakupu — tabela stanu "przeczytane/nieprzeczytane" PER UŻYTKOWNIK
-- Baza: LibraNet (192.168.0.109). Folder serwerowy IMAP jest WSPÓLNY,
-- ale każdy z działu zakupu ma własny widok przeczytanych maili.
-- Flagi serwerowej \Seen NIE dotykamy.
-- ============================================================================

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Mail_ReadState')
BEGIN
    CREATE TABLE dbo.Mail_ReadState
    (
        Id          INT IDENTITY(1,1) PRIMARY KEY,
        UserID      NVARCHAR(50)  NOT NULL,   -- App.UserID użytkownika ZPSP
        FolderName  NVARCHAR(255) NOT NULL,   -- pełna nazwa folderu IMAP (np. INBOX)
        Uid         BIGINT        NOT NULL,   -- IMAP UID wiadomości w folderze
        IsRead      BIT           NOT NULL DEFAULT 1,
        ReadAt      DATETIME      NOT NULL DEFAULT GETDATE(),
        CONSTRAINT UQ_MailReadState UNIQUE (UserID, FolderName, Uid)
    );

    CREATE INDEX IX_MailReadState_User_Folder
        ON dbo.Mail_ReadState (UserID, FolderName);
END
GO

-- ============================================================================
-- Książka adresowa działu zakupu — WSPÓLNA dla wszystkich.
-- Zasilana automatycznie ze skrzynki IMAP oraz z importu Thunderbirda.
-- Służy do podpowiedzi adresów w polu "Do" przy pisaniu wiadomości.
-- ============================================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Mail_Contacts')
BEGIN
    CREATE TABLE dbo.Mail_Contacts
    (
        Email       NVARCHAR(320) NOT NULL PRIMARY KEY,   -- klucz = adres (lower)
        DisplayName NVARCHAR(255) NULL,
        UseCount    INT           NOT NULL DEFAULT 1,
        LastSeen    DATETIME      NOT NULL DEFAULT GETDATE(),
        Source      NVARCHAR(30)  NULL                     -- 'imap' / 'thunderbird' / 'send'
    );

    CREATE INDEX IX_MailContacts_Name ON dbo.Mail_Contacts (DisplayName);
END
GO

-- (opcjonalnie) gwiazdki/flagi per użytkownik w przyszłości — rezerwacja schematu
-- IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Mail_Flag') ...
