-- ================================================================
-- Kartoteka Towarow - dodatkowe tabele
-- Uruchom raz w SSMS na bazie LibraNet (192.168.0.109)
-- ================================================================

-- 1. Historia zmian (Audit Log)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ArticleAuditLog')
CREATE TABLE ArticleAuditLog (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    ArticleGUID     VARCHAR(50)   NOT NULL,
    ArticleID       VARCHAR(10)   NOT NULL,
    FieldName       VARCHAR(50)   NOT NULL,
    OldValue        NVARCHAR(200) NULL,
    NewValue        NVARCHAR(200) NULL,
    ChangedBy       VARCHAR(50)   NOT NULL,
    ChangedAt       DATETIME      NOT NULL DEFAULT GETDATE()
);
GO

CREATE INDEX IX_ArticleAuditLog_GUID ON ArticleAuditLog(ArticleGUID);
GO

-- 2. Ulubione artykuly (per user)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ArticleFavorites')
CREATE TABLE ArticleFavorites (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    ArticleGUID     VARCHAR(50)   NOT NULL,
    UserID          VARCHAR(50)   NOT NULL,
    AddedAt         DATETIME      NOT NULL DEFAULT GETDATE(),
    CONSTRAINT UQ_ArticleFavorites UNIQUE (ArticleGUID, UserID)
);
GO
