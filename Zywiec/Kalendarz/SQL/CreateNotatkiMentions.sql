-- =====================================================================
-- NotatkiMentions - tabela wzmianek @user w notatkach
-- Baza: LibraNet (192.168.0.109)
-- Uruchom raz w SSMS. ParentNotatkaID już istnieje w Notatki, nie trzeba ALTER.
-- =====================================================================

USE [LibraNet]
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'NotatkiMentions')
BEGIN
    CREATE TABLE [dbo].[NotatkiMentions]
    (
        [MentionID]        INT IDENTITY(1,1) NOT NULL,
        [NotatkaID]        INT NOT NULL,
        [MentionedUserID]  INT NOT NULL,
        [IsRead]           BIT NOT NULL CONSTRAINT DF_NotatkiMentions_IsRead DEFAULT(0),
        [CreatedAt]        DATETIME NOT NULL CONSTRAINT DF_NotatkiMentions_CreatedAt DEFAULT(GETDATE()),
        [ReadAt]           DATETIME NULL,
        CONSTRAINT [PK_NotatkiMentions] PRIMARY KEY CLUSTERED ([MentionID] ASC)
    );

    -- Indeks: szybkie pobieranie nieprzeczytanych dla usera
    CREATE NONCLUSTERED INDEX [IX_NotatkiMentions_User_Unread]
        ON [dbo].[NotatkiMentions] ([MentionedUserID], [IsRead])
        INCLUDE ([NotatkaID], [CreatedAt]);

    -- Indeks: zapobiega duplikatom (ta sama notatka + ten sam user)
    CREATE UNIQUE NONCLUSTERED INDEX [IX_NotatkiMentions_NotatkaUser]
        ON [dbo].[NotatkiMentions] ([NotatkaID], [MentionedUserID]);

    PRINT 'Tabela NotatkiMentions utworzona.';
END
ELSE
BEGIN
    PRINT 'Tabela NotatkiMentions już istnieje - pomijam tworzenie.';
END
GO

-- Sprawdzenie struktury
SELECT
    c.COLUMN_NAME,
    c.DATA_TYPE,
    c.IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS c
WHERE c.TABLE_NAME = 'NotatkiMentions'
ORDER BY c.ORDINAL_POSITION;
GO
