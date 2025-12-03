-- ============================================
-- SKRYPT ALTER: Dodanie WSZYSTKICH brakujących kolumn
-- Baza danych: LibraNet (serwer 192.168.0.109)
-- Uruchom jeśli tabele już istnieją
-- ============================================

USE [LibraNet]
GO

-- ============================================
-- 0. USUNIECIE CHECK CONSTRAINT dla kolumny Status
-- ============================================
PRINT 'Usuwanie CHECK constraint dla Status...'

IF EXISTS (SELECT * FROM sys.check_constraints WHERE name = 'CK_Reklamacje_Status')
BEGIN
    ALTER TABLE [dbo].[Reklamacje] DROP CONSTRAINT [CK_Reklamacje_Status]
    PRINT 'Usunieto constraint CK_Reklamacje_Status'
END
GO

-- Dodaj nowy CHECK constraint z poprawnymi wartosciami
IF NOT EXISTS (SELECT * FROM sys.check_constraints WHERE name = 'CK_Reklamacje_Status')
BEGIN
    ALTER TABLE [dbo].[Reklamacje] ADD CONSTRAINT [CK_Reklamacje_Status]
    CHECK ([Status] IN ('Nowa', 'W trakcie', 'Zaakceptowana', 'Odrzucona', 'Zamknieta', 'Zamknięta'))
    PRINT 'Dodano nowy constraint CK_Reklamacje_Status'
END
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

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Reklamacje]') AND name = 'TypReklamacji')
BEGIN
    ALTER TABLE [dbo].[Reklamacje] ADD [TypReklamacji] NVARCHAR(100) NULL
    PRINT 'Dodano kolumne TypReklamacji'
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Reklamacje]') AND name = 'Priorytet')
BEGIN
    ALTER TABLE [dbo].[Reklamacje] ADD [Priorytet] NVARCHAR(20) NULL DEFAULT 'Normalny'
    PRINT 'Dodano kolumne Priorytet'
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Reklamacje]') AND name = 'KosztReklamacji')
BEGIN
    ALTER TABLE [dbo].[Reklamacje] ADD [KosztReklamacji] DECIMAL(18,2) NULL DEFAULT 0
    PRINT 'Dodano kolumne KosztReklamacji'
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Reklamacje]') AND name = 'Komentarz')
BEGIN
    ALTER TABLE [dbo].[Reklamacje] ADD [Komentarz] NVARCHAR(MAX) NULL
    PRINT 'Dodano kolumne Komentarz'
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Reklamacje]') AND name = 'Rozwiazanie')
BEGIN
    ALTER TABLE [dbo].[Reklamacje] ADD [Rozwiazanie] NVARCHAR(MAX) NULL
    PRINT 'Dodano kolumne Rozwiazanie'
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Reklamacje]') AND name = 'DataZamkniecia')
BEGIN
    ALTER TABLE [dbo].[Reklamacje] ADD [DataZamkniecia] DATETIME NULL
    PRINT 'Dodano kolumne DataZamkniecia'
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Reklamacje]') AND name = 'DataModyfikacji')
BEGIN
    ALTER TABLE [dbo].[Reklamacje] ADD [DataModyfikacji] DATETIME NULL
    PRINT 'Dodano kolumne DataModyfikacji'
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Reklamacje]') AND name = 'OsobaRozpatrujaca')
BEGIN
    ALTER TABLE [dbo].[Reklamacje] ADD [OsobaRozpatrujaca] NVARCHAR(50) NULL
    PRINT 'Dodano kolumne OsobaRozpatrujaca'
END

GO

-- ============================================
-- 2. ALTER: Tabela ReklamacjeTowary
-- ============================================
PRINT 'Sprawdzanie tabeli ReklamacjeTowary...'

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[ReklamacjeTowary]') AND name = 'Symbol')
BEGIN
    ALTER TABLE [dbo].[ReklamacjeTowary] ADD [Symbol] NVARCHAR(50) NULL
    PRINT 'Dodano kolumne Symbol'
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[ReklamacjeTowary]') AND name = 'Nazwa')
BEGIN
    ALTER TABLE [dbo].[ReklamacjeTowary] ADD [Nazwa] NVARCHAR(255) NULL
    PRINT 'Dodano kolumne Nazwa'
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[ReklamacjeTowary]') AND name = 'Waga')
BEGIN
    ALTER TABLE [dbo].[ReklamacjeTowary] ADD [Waga] DECIMAL(18,2) NULL
    PRINT 'Dodano kolumne Waga'
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[ReklamacjeTowary]') AND name = 'Cena')
BEGIN
    ALTER TABLE [dbo].[ReklamacjeTowary] ADD [Cena] DECIMAL(18,2) NULL
    PRINT 'Dodano kolumne Cena'
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[ReklamacjeTowary]') AND name = 'Wartosc')
BEGIN
    ALTER TABLE [dbo].[ReklamacjeTowary] ADD [Wartosc] DECIMAL(18,2) NULL
    PRINT 'Dodano kolumne Wartosc'
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[ReklamacjeTowary]') AND name = 'PrzyczynaReklamacji')
BEGIN
    ALTER TABLE [dbo].[ReklamacjeTowary] ADD [PrzyczynaReklamacji] NVARCHAR(500) NULL
    PRINT 'Dodano kolumne PrzyczynaReklamacji'
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[ReklamacjeTowary]') AND name = 'IdTowaru')
BEGIN
    ALTER TABLE [dbo].[ReklamacjeTowary] ADD [IdTowaru] INT NULL
    PRINT 'Dodano kolumne IdTowaru'
END

GO

-- ============================================
-- 3. ALTER: Tabela ReklamacjePartie
-- ============================================
PRINT 'Sprawdzanie tabeli ReklamacjePartie...'

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[ReklamacjePartie]') AND name = 'GuidPartii')
BEGIN
    ALTER TABLE [dbo].[ReklamacjePartie] ADD [GuidPartii] UNIQUEIDENTIFIER NULL
    PRINT 'Dodano kolumne GuidPartii'
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[ReklamacjePartie]') AND name = 'NumerPartii')
BEGIN
    ALTER TABLE [dbo].[ReklamacjePartie] ADD [NumerPartii] NVARCHAR(100) NULL
    PRINT 'Dodano kolumne NumerPartii'
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[ReklamacjePartie]') AND name = 'CustomerID')
BEGIN
    ALTER TABLE [dbo].[ReklamacjePartie] ADD [CustomerID] NVARCHAR(50) NULL
    PRINT 'Dodano kolumne CustomerID'
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[ReklamacjePartie]') AND name = 'CustomerName')
BEGIN
    ALTER TABLE [dbo].[ReklamacjePartie] ADD [CustomerName] NVARCHAR(255) NULL
    PRINT 'Dodano kolumne CustomerName'
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[ReklamacjePartie]') AND name = 'DataDodania')
BEGIN
    ALTER TABLE [dbo].[ReklamacjePartie] ADD [DataDodania] DATETIME NULL DEFAULT GETDATE()
    PRINT 'Dodano kolumne DataDodania'
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[ReklamacjePartie]') AND name = 'IdReklamacji')
BEGIN
    ALTER TABLE [dbo].[ReklamacjePartie] ADD [IdReklamacji] INT NULL
    PRINT 'Dodano kolumne IdReklamacji'
END

GO

-- ============================================
-- 4. ALTER: Tabela ReklamacjeZdjecia
-- ============================================
PRINT 'Sprawdzanie tabeli ReklamacjeZdjecia...'

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[ReklamacjeZdjecia]') AND name = 'NazwaPliku')
BEGIN
    ALTER TABLE [dbo].[ReklamacjeZdjecia] ADD [NazwaPliku] NVARCHAR(255) NULL
    PRINT 'Dodano kolumne NazwaPliku'
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[ReklamacjeZdjecia]') AND name = 'SciezkaPliku')
BEGIN
    ALTER TABLE [dbo].[ReklamacjeZdjecia] ADD [SciezkaPliku] NVARCHAR(500) NULL
    PRINT 'Dodano kolumne SciezkaPliku'
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[ReklamacjeZdjecia]') AND name = 'DataDodania')
BEGIN
    ALTER TABLE [dbo].[ReklamacjeZdjecia] ADD [DataDodania] DATETIME NULL DEFAULT GETDATE()
    PRINT 'Dodano kolumne DataDodania'
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[ReklamacjeZdjecia]') AND name = 'DodanePrzez')
BEGIN
    ALTER TABLE [dbo].[ReklamacjeZdjecia] ADD [DodanePrzez] NVARCHAR(50) NULL
    PRINT 'Dodano kolumne DodanePrzez'
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[ReklamacjeZdjecia]') AND name = 'IdReklamacji')
BEGIN
    ALTER TABLE [dbo].[ReklamacjeZdjecia] ADD [IdReklamacji] INT NULL
    PRINT 'Dodano kolumne IdReklamacji'
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[ReklamacjeZdjecia]') AND name = 'DaneZdjecia')
BEGIN
    ALTER TABLE [dbo].[ReklamacjeZdjecia] ADD [DaneZdjecia] VARBINARY(MAX) NULL
    PRINT 'Dodano kolumne DaneZdjecia (BLOB)'
END

GO

-- ============================================
-- 5. ALTER: Tabela ReklamacjeHistoria
-- ============================================
PRINT 'Sprawdzanie tabeli ReklamacjeHistoria...'

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[ReklamacjeHistoria]') AND name = 'IdReklamacji')
BEGIN
    ALTER TABLE [dbo].[ReklamacjeHistoria] ADD [IdReklamacji] INT NULL
    PRINT 'Dodano kolumne IdReklamacji'
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[ReklamacjeHistoria]') AND name = 'DataZmiany')
BEGIN
    ALTER TABLE [dbo].[ReklamacjeHistoria] ADD [DataZmiany] DATETIME NULL DEFAULT GETDATE()
    PRINT 'Dodano kolumne DataZmiany'
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[ReklamacjeHistoria]') AND name = 'UserID')
BEGIN
    ALTER TABLE [dbo].[ReklamacjeHistoria] ADD [UserID] NVARCHAR(50) NULL
    PRINT 'Dodano kolumne UserID'
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[ReklamacjeHistoria]') AND name = 'PoprzedniStatus')
BEGIN
    ALTER TABLE [dbo].[ReklamacjeHistoria] ADD [PoprzedniStatus] NVARCHAR(50) NULL
    PRINT 'Dodano kolumne PoprzedniStatus'
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[ReklamacjeHistoria]') AND name = 'StatusNowy')
BEGIN
    ALTER TABLE [dbo].[ReklamacjeHistoria] ADD [StatusNowy] NVARCHAR(50) NOT NULL DEFAULT 'Nowa'
    PRINT 'Dodano kolumne StatusNowy'
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[ReklamacjeHistoria]') AND name = 'Komentarz')
BEGIN
    ALTER TABLE [dbo].[ReklamacjeHistoria] ADD [Komentarz] NVARCHAR(MAX) NULL
    PRINT 'Dodano kolumne Komentarz'
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[ReklamacjeHistoria]') AND name = 'TypAkcji')
BEGIN
    ALTER TABLE [dbo].[ReklamacjeHistoria] ADD [TypAkcji] NVARCHAR(50) NULL
    PRINT 'Dodano kolumne TypAkcji'
END

GO

-- ============================================
-- 6. Ponowne utworzenie procedur składowanych
-- ============================================
PRINT 'Odtwarzanie procedur skladowanych...'

-- Procedura sp_PobierzSzczegolyReklamacji
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_PobierzSzczegolyReklamacji')
    DROP PROCEDURE sp_PobierzSzczegolyReklamacji
GO

CREATE PROCEDURE [dbo].[sp_PobierzSzczegolyReklamacji]
    @IdReklamacji INT
AS
BEGIN
    SET NOCOUNT ON;

    -- 1. Podstawowe informacje o reklamacji
    SELECT
        Id, DataZgloszenia, UserID, IdDokumentu, NumerDokumentu,
        IdKontrahenta, NazwaKontrahenta, Opis, SumaKg, SumaWartosc, Status,
        OsobaRozpatrujaca, Komentarz, Rozwiazanie, DataModyfikacji, DataZamkniecia,
        TypReklamacji, Priorytet, KosztReklamacji
    FROM [dbo].[Reklamacje]
    WHERE Id = @IdReklamacji;

    -- 2. Towary w reklamacji
    SELECT
        Id, IdTowaru, Symbol, Nazwa, Waga, Cena, Wartosc, PrzyczynaReklamacji
    FROM [dbo].[ReklamacjeTowary]
    WHERE IdReklamacji = @IdReklamacji
    ORDER BY Id;

    -- 3. Partie powiązane
    SELECT
        Id, GuidPartii, NumerPartii AS Partia, CustomerID, CustomerName, DataDodania
    FROM [dbo].[ReklamacjePartie]
    WHERE IdReklamacji = @IdReklamacji
    ORDER BY DataDodania DESC;

    -- 4. Zdjęcia (z obsluga brakujacych kolumn i BLOB)
    SELECT
        Id,
        ISNULL(NazwaPliku, '') AS NazwaPliku,
        ISNULL(SciezkaPliku, '') AS SciezkaPliku,
        DataDodania,
        ISNULL(DodanePrzez, '') AS DodanePrzez,
        DaneZdjecia
    FROM [dbo].[ReklamacjeZdjecia]
    WHERE IdReklamacji = @IdReklamacji
    ORDER BY DataDodania;

    -- 5. Historia zmian
    SELECT
        Id, DataZmiany, UserID, PoprzedniStatus, StatusNowy, Komentarz, TypAkcji
    FROM [dbo].[ReklamacjeHistoria]
    WHERE IdReklamacji = @IdReklamacji
    ORDER BY DataZmiany DESC;
END
GO

PRINT 'Utworzono procedure sp_PobierzSzczegolyReklamacji'
GO

-- Procedura sp_ZmienStatusReklamacji
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_ZmienStatusReklamacji')
    DROP PROCEDURE sp_ZmienStatusReklamacji
GO

CREATE PROCEDURE [dbo].[sp_ZmienStatusReklamacji]
    @IdReklamacji INT,
    @NowyStatus NVARCHAR(50),
    @UserID NVARCHAR(50),
    @Komentarz NVARCHAR(MAX) = NULL,
    @Rozwiazanie NVARCHAR(MAX) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @PoprzedniStatus NVARCHAR(50);

    -- Pobierz poprzedni status
    SELECT @PoprzedniStatus = Status
    FROM [dbo].[Reklamacje]
    WHERE Id = @IdReklamacji;

    -- Aktualizuj reklamację
    UPDATE [dbo].[Reklamacje]
    SET
        Status = @NowyStatus,
        OsobaRozpatrujaca = @UserID,
        DataModyfikacji = GETDATE(),
        DataZamkniecia = CASE WHEN @NowyStatus = 'Zamknieta' THEN GETDATE() ELSE DataZamkniecia END,
        Komentarz = CASE WHEN @Komentarz IS NOT NULL THEN @Komentarz ELSE Komentarz END,
        Rozwiazanie = CASE WHEN @Rozwiazanie IS NOT NULL THEN @Rozwiazanie ELSE Rozwiazanie END
    WHERE Id = @IdReklamacji;

    -- Dodaj wpis do historii
    INSERT INTO [dbo].[ReklamacjeHistoria]
        (IdReklamacji, UserID, PoprzedniStatus, StatusNowy, Komentarz, TypAkcji)
    VALUES
        (@IdReklamacji, @UserID, @PoprzedniStatus, @NowyStatus, @Komentarz, 'ZmianaStatusu');

    SELECT 'OK' AS Wynik, @IdReklamacji AS IdReklamacji;
END
GO

PRINT 'Utworzono procedure sp_ZmienStatusReklamacji'
GO

-- ============================================
-- KONIEC SKRYPTU
-- ============================================
PRINT '=========================================='
PRINT 'Skrypt ALTER zakonczony pomyslnie!'
PRINT 'Wszystkie brakujace kolumny dodane.'
PRINT 'Procedury skladowane odtworzone.'
PRINT '=========================================='
GO
