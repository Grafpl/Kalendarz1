-- ============================================
-- SKRYPT ALTER: Dodanie brakujących kolumn
-- Baza danych: LibraNet (serwer 192.168.0.109)
-- Uruchom jeśli tabele już istnieją
-- ============================================

USE [LibraNet]
GO

-- ============================================
-- 1. ALTER: Tabela Reklamacje
-- ============================================
PRINT 'Sprawdzanie tabeli Reklamacje...'

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Reklamacje]') AND name = 'SumaWartosc')
BEGIN
    ALTER TABLE [dbo].[Reklamacje] ADD [SumaWartosc] DECIMAL(18,2) NULL DEFAULT 0
    PRINT 'Dodano kolumne SumaWartosc'
END
ELSE
    PRINT 'Kolumna SumaWartosc juz istnieje'

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Reklamacje]') AND name = 'TypReklamacji')
BEGIN
    ALTER TABLE [dbo].[Reklamacje] ADD [TypReklamacji] NVARCHAR(100) NULL
    PRINT 'Dodano kolumne TypReklamacji'
END
ELSE
    PRINT 'Kolumna TypReklamacji juz istnieje'

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Reklamacje]') AND name = 'Priorytet')
BEGIN
    ALTER TABLE [dbo].[Reklamacje] ADD [Priorytet] NVARCHAR(20) NULL DEFAULT 'Normalny'
    PRINT 'Dodano kolumne Priorytet'
END
ELSE
    PRINT 'Kolumna Priorytet juz istnieje'

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Reklamacje]') AND name = 'KosztReklamacji')
BEGIN
    ALTER TABLE [dbo].[Reklamacje] ADD [KosztReklamacji] DECIMAL(18,2) NULL DEFAULT 0
    PRINT 'Dodano kolumne KosztReklamacji'
END
ELSE
    PRINT 'Kolumna KosztReklamacji juz istnieje'

GO

-- ============================================
-- 2. ALTER: Tabela ReklamacjeTowary
-- ============================================
PRINT 'Sprawdzanie tabeli ReklamacjeTowary...'

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[ReklamacjeTowary]') AND name = 'Cena')
BEGIN
    ALTER TABLE [dbo].[ReklamacjeTowary] ADD [Cena] DECIMAL(18,2) NULL
    PRINT 'Dodano kolumne Cena'
END
ELSE
    PRINT 'Kolumna Cena juz istnieje'

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[ReklamacjeTowary]') AND name = 'Wartosc')
BEGIN
    ALTER TABLE [dbo].[ReklamacjeTowary] ADD [Wartosc] DECIMAL(18,2) NULL
    PRINT 'Dodano kolumne Wartosc'
END
ELSE
    PRINT 'Kolumna Wartosc juz istnieje'

GO

-- ============================================
-- 3. ALTER: Tabela ReklamacjePartie
-- ============================================
PRINT 'Sprawdzanie tabeli ReklamacjePartie...'

-- Sprawdź czy tabela istnieje
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[ReklamacjePartie]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[ReklamacjePartie](
        [Id] INT IDENTITY(1,1) NOT NULL,
        [IdReklamacji] INT NOT NULL,
        [GuidPartii] UNIQUEIDENTIFIER NULL,
        [NumerPartii] NVARCHAR(100) NULL,
        [CustomerID] NVARCHAR(50) NULL,
        [CustomerName] NVARCHAR(255) NULL,
        [DataDodania] DATETIME NOT NULL DEFAULT GETDATE(),
        CONSTRAINT [PK_ReklamacjePartie] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_ReklamacjePartie_Reklamacje] FOREIGN KEY([IdReklamacji])
            REFERENCES [dbo].[Reklamacje] ([Id]) ON DELETE CASCADE
    )
    PRINT 'Utworzono tabele ReklamacjePartie'
END
ELSE
    PRINT 'Tabela ReklamacjePartie juz istnieje'

GO

-- ============================================
-- 4. Sprawdź/Utwórz tabele pomocnicze
-- ============================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[ReklamacjeZdjecia]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[ReklamacjeZdjecia](
        [Id] INT IDENTITY(1,1) NOT NULL,
        [IdReklamacji] INT NOT NULL,
        [NazwaPliku] NVARCHAR(255) NOT NULL,
        [SciezkaPliku] NVARCHAR(500) NOT NULL,
        [DataDodania] DATETIME NOT NULL DEFAULT GETDATE(),
        [DodanePrzez] NVARCHAR(50) NULL,
        CONSTRAINT [PK_ReklamacjeZdjecia] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_ReklamacjeZdjecia_Reklamacje] FOREIGN KEY([IdReklamacji])
            REFERENCES [dbo].[Reklamacje] ([Id]) ON DELETE CASCADE
    )
    PRINT 'Utworzono tabele ReklamacjeZdjecia'
END
ELSE
    PRINT 'Tabela ReklamacjeZdjecia juz istnieje'

GO

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[ReklamacjeHistoria]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[ReklamacjeHistoria](
        [Id] INT IDENTITY(1,1) NOT NULL,
        [IdReklamacji] INT NOT NULL,
        [DataZmiany] DATETIME NOT NULL DEFAULT GETDATE(),
        [UserID] NVARCHAR(50) NOT NULL,
        [PoprzedniStatus] NVARCHAR(50) NULL,
        [NowyStatus] NVARCHAR(50) NOT NULL,
        [Komentarz] NVARCHAR(MAX) NULL,
        [TypAkcji] NVARCHAR(50) NULL,
        CONSTRAINT [PK_ReklamacjeHistoria] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_ReklamacjeHistoria_Reklamacje] FOREIGN KEY([IdReklamacji])
            REFERENCES [dbo].[Reklamacje] ([Id]) ON DELETE CASCADE
    )
    PRINT 'Utworzono tabele ReklamacjeHistoria'
END
ELSE
    PRINT 'Tabela ReklamacjeHistoria juz istnieje'

GO

-- ============================================
-- KONIEC SKRYPTU
-- ============================================
PRINT '=========================================='
PRINT 'Skrypt ALTER zakonczony pomyslnie!'
PRINT '=========================================='
GO
