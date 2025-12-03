-- ============================================
-- SKRYPT SQL: TABELE I PROCEDURY DLA MODUŁU REKLAMACJI
-- Baza danych: LibraNet (serwer 192.168.0.109)
-- Autor: System Kalendarz1
-- Data: 2025
-- ============================================

USE [LibraNet]
GO

-- ============================================
-- 1. TABELA GŁÓWNA: Reklamacje
-- ============================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Reklamacje]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Reklamacje](
        [Id] INT IDENTITY(1,1) NOT NULL,
        [DataZgloszenia] DATETIME NOT NULL DEFAULT GETDATE(),
        [UserID] NVARCHAR(50) NOT NULL,                    -- Handlowiec zgłaszający
        [IdDokumentu] INT NOT NULL,                        -- ID faktury z HM.ND (serwer .112)
        [NumerDokumentu] NVARCHAR(100) NOT NULL,           -- Numer faktury
        [IdKontrahenta] INT NOT NULL,                      -- ID kontrahenta z HM.KH
        [NazwaKontrahenta] NVARCHAR(255) NOT NULL,         -- Nazwa firmy
        [Opis] NVARCHAR(MAX) NOT NULL,                     -- Opis problemu
        [SumaKg] DECIMAL(18,2) NULL DEFAULT 0,             -- Suma kg reklamowanych towarów
        [SumaWartosc] DECIMAL(18,2) NULL DEFAULT 0,        -- Suma wartości reklamowanych towarów
        [Status] NVARCHAR(50) NOT NULL DEFAULT 'Nowa',     -- Nowa, W trakcie, Zaakceptowana, Odrzucona, Zamknięta
        [OsobaRozpatrujaca] NVARCHAR(50) NULL,             -- Kto rozpatruje
        [Komentarz] NVARCHAR(MAX) NULL,                    -- Komentarz osoby rozpatrującej
        [Rozwiazanie] NVARCHAR(MAX) NULL,                  -- Opis rozwiązania
        [DataModyfikacji] DATETIME NULL,
        [DataZamkniecia] DATETIME NULL,
        [TypReklamacji] NVARCHAR(100) NULL,                -- Jakość, Ilość, Uszkodzenie, Termin, Inne
        [Priorytet] NVARCHAR(20) NULL DEFAULT 'Normalny',  -- Niski, Normalny, Wysoki, Krytyczny
        [KosztReklamacji] DECIMAL(18,2) NULL DEFAULT 0,    -- Wartość reklamacji
        CONSTRAINT [PK_Reklamacje] PRIMARY KEY CLUSTERED ([Id] ASC)
    )

    PRINT 'Utworzono tabelę Reklamacje'
END
ELSE
    PRINT 'Tabela Reklamacje już istnieje'
GO

-- ============================================
-- 2. TABELA: ReklamacjeTowary
-- Towary z faktury objęte reklamacją
-- ============================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[ReklamacjeTowary]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[ReklamacjeTowary](
        [Id] INT IDENTITY(1,1) NOT NULL,
        [IdReklamacji] INT NOT NULL,
        [IdTowaru] INT NOT NULL,                           -- ID pozycji z HM.DP
        [Symbol] NVARCHAR(100) NULL,
        [Nazwa] NVARCHAR(255) NULL,
        [Waga] DECIMAL(18,2) NULL,
        [Cena] DECIMAL(18,2) NULL,
        [Wartosc] DECIMAL(18,2) NULL,
        [PrzyczynaReklamacji] NVARCHAR(500) NULL,          -- Opcjonalny opis problemu dla pozycji
        CONSTRAINT [PK_ReklamacjeTowary] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_ReklamacjeTowary_Reklamacje] FOREIGN KEY([IdReklamacji])
            REFERENCES [dbo].[Reklamacje] ([Id]) ON DELETE CASCADE
    )

    PRINT 'Utworzono tabelę ReklamacjeTowary'
END
ELSE
    PRINT 'Tabela ReklamacjeTowary już istnieje'
GO

-- ============================================
-- 3. TABELA: ReklamacjePartie
-- Partie dostawcy powiązane z reklamacją
-- ============================================
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

    PRINT 'Utworzono tabelę ReklamacjePartie'
END
ELSE
    PRINT 'Tabela ReklamacjePartie już istnieje'
GO

-- ============================================
-- 4. TABELA: ReklamacjeZdjecia
-- Zdjęcia dokumentujące reklamację
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
        [DaneZdjecia] VARBINARY(MAX) NULL,  -- Dane binarne zdjęcia (BLOB)
        CONSTRAINT [PK_ReklamacjeZdjecia] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_ReklamacjeZdjecia_Reklamacje] FOREIGN KEY([IdReklamacji])
            REFERENCES [dbo].[Reklamacje] ([Id]) ON DELETE CASCADE
    )

    PRINT 'Utworzono tabelę ReklamacjeZdjecia'
END
ELSE
    PRINT 'Tabela ReklamacjeZdjecia już istnieje'
GO

-- ============================================
-- 5. TABELA: ReklamacjeHistoria
-- Historia zmian statusów i działań
-- ============================================
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
        [TypAkcji] NVARCHAR(50) NULL,                      -- ZmianaStatusu, DodanieKomentarza, DodanieZdjecia, itp.
        CONSTRAINT [PK_ReklamacjeHistoria] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_ReklamacjeHistoria_Reklamacje] FOREIGN KEY([IdReklamacji])
            REFERENCES [dbo].[Reklamacje] ([Id]) ON DELETE CASCADE
    )

    PRINT 'Utworzono tabelę ReklamacjeHistoria'
END
ELSE
    PRINT 'Tabela ReklamacjeHistoria już istnieje'
GO

-- ============================================
-- 6. INDEKSY
-- ============================================
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Reklamacje_Status')
    CREATE INDEX IX_Reklamacje_Status ON [dbo].[Reklamacje](Status);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Reklamacje_DataZgloszenia')
    CREATE INDEX IX_Reklamacje_DataZgloszenia ON [dbo].[Reklamacje](DataZgloszenia DESC);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Reklamacje_IdKontrahenta')
    CREATE INDEX IX_Reklamacje_IdKontrahenta ON [dbo].[Reklamacje](IdKontrahenta);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Reklamacje_UserID')
    CREATE INDEX IX_Reklamacje_UserID ON [dbo].[Reklamacje](UserID);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_ReklamacjeTowary_IdReklamacji')
    CREATE INDEX IX_ReklamacjeTowary_IdReklamacji ON [dbo].[ReklamacjeTowary](IdReklamacji);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_ReklamacjeHistoria_IdReklamacji')
    CREATE INDEX IX_ReklamacjeHistoria_IdReklamacji ON [dbo].[ReklamacjeHistoria](IdReklamacji);

PRINT 'Utworzono indeksy'
GO

-- ============================================
-- 7. WIDOK: vw_ReklamacjePelneInfo
-- Widok z pełnymi informacjami o reklamacjach
-- ============================================
IF EXISTS (SELECT * FROM sys.views WHERE name = 'vw_ReklamacjePelneInfo')
    DROP VIEW vw_ReklamacjePelneInfo
GO

CREATE VIEW [dbo].[vw_ReklamacjePelneInfo]
AS
SELECT
    r.Id,
    r.DataZgloszenia,
    r.UserID,
    r.IdDokumentu,
    r.NumerDokumentu,
    r.IdKontrahenta,
    r.NazwaKontrahenta,
    r.Opis,
    r.SumaKg,
    r.SumaWartosc,
    r.Status,
    r.OsobaRozpatrujaca,
    r.Komentarz,
    r.Rozwiazanie,
    r.DataModyfikacji,
    r.DataZamkniecia,
    r.TypReklamacji,
    r.Priorytet,
    r.KosztReklamacji,
    -- Obliczenia
    DATEDIFF(DAY, r.DataZgloszenia, ISNULL(r.DataZamkniecia, GETDATE())) AS DniRozpatrywania,
    (SELECT COUNT(*) FROM ReklamacjeTowary rt WHERE rt.IdReklamacji = r.Id) AS LiczbaTowrow,
    (SELECT COUNT(*) FROM ReklamacjeZdjecia rz WHERE rz.IdReklamacji = r.Id) AS LiczbaZdjec,
    (SELECT COUNT(*) FROM ReklamacjePartie rp WHERE rp.IdReklamacji = r.Id) AS LiczbaPartii,
    -- Kolorowanie statusu
    CASE r.Status
        WHEN 'Nowa' THEN '#3498db'
        WHEN 'W trakcie' THEN '#f39c12'
        WHEN 'Zaakceptowana' THEN '#27ae60'
        WHEN 'Odrzucona' THEN '#e74c3c'
        WHEN 'Zamknięta' THEN '#95a5a6'
        ELSE '#000000'
    END AS KolorStatusu
FROM [dbo].[Reklamacje] r
GO

PRINT 'Utworzono widok vw_ReklamacjePelneInfo'
GO

-- ============================================
-- 8. PROCEDURA: sp_PobierzSzczegolyReklamacji
-- Pobiera wszystkie szczegóły reklamacji
-- ============================================
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

    -- 4. Zdjęcia (z danymi binarnymi BLOB)
    SELECT
        Id, NazwaPliku, SciezkaPliku, DataDodania, DodanePrzez, DaneZdjecia
    FROM [dbo].[ReklamacjeZdjecia]
    WHERE IdReklamacji = @IdReklamacji
    ORDER BY DataDodania;

    -- 5. Historia zmian
    SELECT
        Id, DataZmiany, UserID, PoprzedniStatus, NowyStatus, Komentarz, TypAkcji
    FROM [dbo].[ReklamacjeHistoria]
    WHERE IdReklamacji = @IdReklamacji
    ORDER BY DataZmiany DESC;
END
GO

PRINT 'Utworzono procedurę sp_PobierzSzczegolyReklamacji'
GO

-- ============================================
-- 9. PROCEDURA: sp_ZmienStatusReklamacji
-- Zmienia status reklamacji i zapisuje historię
-- ============================================
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
        DataZamkniecia = CASE WHEN @NowyStatus = 'Zamknięta' THEN GETDATE() ELSE DataZamkniecia END,
        Komentarz = CASE WHEN @Komentarz IS NOT NULL THEN @Komentarz ELSE Komentarz END,
        Rozwiazanie = CASE WHEN @Rozwiazanie IS NOT NULL THEN @Rozwiazanie ELSE Rozwiazanie END
    WHERE Id = @IdReklamacji;

    -- Dodaj wpis do historii
    INSERT INTO [dbo].[ReklamacjeHistoria]
        (IdReklamacji, UserID, PoprzedniStatus, NowyStatus, Komentarz, TypAkcji)
    VALUES
        (@IdReklamacji, @UserID, @PoprzedniStatus, @NowyStatus, @Komentarz, 'ZmianaStatusu');

    SELECT 'OK' AS Wynik, @IdReklamacji AS IdReklamacji;
END
GO

PRINT 'Utworzono procedurę sp_ZmienStatusReklamacji'
GO

-- ============================================
-- 10. PROCEDURA: sp_StatystykiReklamacji
-- Statystyki reklamacji dla dashboardu
-- ============================================
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_StatystykiReklamacji')
    DROP PROCEDURE sp_StatystykiReklamacji
GO

CREATE PROCEDURE [dbo].[sp_StatystykiReklamacji]
    @DataOd DATETIME = NULL,
    @DataDo DATETIME = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF @DataOd IS NULL SET @DataOd = DATEADD(MONTH, -1, GETDATE());
    IF @DataDo IS NULL SET @DataDo = GETDATE();

    -- Statystyki według statusu
    SELECT
        Status,
        COUNT(*) AS Liczba,
        SUM(SumaKg) AS SumaKg,
        SUM(SumaWartosc) AS SumaWartosc,
        AVG(DATEDIFF(DAY, DataZgloszenia, ISNULL(DataZamkniecia, GETDATE()))) AS SredniCzasRozpatrywania
    FROM [dbo].[Reklamacje]
    WHERE DataZgloszenia BETWEEN @DataOd AND @DataDo
    GROUP BY Status
    ORDER BY
        CASE Status
            WHEN 'Nowa' THEN 1
            WHEN 'W trakcie' THEN 2
            WHEN 'Zaakceptowana' THEN 3
            WHEN 'Odrzucona' THEN 4
            WHEN 'Zamknięta' THEN 5
        END;

    -- Top 10 kontrahentów z reklamacjami
    SELECT TOP 10
        NazwaKontrahenta,
        IdKontrahenta,
        COUNT(*) AS LiczbaReklamacji,
        SUM(SumaKg) AS SumaKg,
        SUM(SumaWartosc) AS SumaWartosc,
        SUM(KosztReklamacji) AS SumaKosztow
    FROM [dbo].[Reklamacje]
    WHERE DataZgloszenia BETWEEN @DataOd AND @DataDo
    GROUP BY NazwaKontrahenta, IdKontrahenta
    ORDER BY COUNT(*) DESC;

    -- Reklamacje według handlowca
    SELECT
        UserID,
        COUNT(*) AS LiczbaZgloszonych,
        SUM(CASE WHEN Status = 'Zaakceptowana' THEN 1 ELSE 0 END) AS Zaakceptowane,
        SUM(CASE WHEN Status = 'Odrzucona' THEN 1 ELSE 0 END) AS Odrzucone
    FROM [dbo].[Reklamacje]
    WHERE DataZgloszenia BETWEEN @DataOd AND @DataDo
    GROUP BY UserID
    ORDER BY LiczbaZgloszonych DESC;
END
GO

PRINT 'Utworzono procedurę sp_StatystykiReklamacji'
GO

-- ============================================
-- KONIEC SKRYPTU
-- ============================================
PRINT '=========================================='
PRINT 'Skrypt zakończony pomyślnie!'
PRINT 'Baza: LibraNet (serwer 192.168.0.109)'
PRINT 'Utworzono:'
PRINT '  - Tabela: Reklamacje'
PRINT '  - Tabela: ReklamacjeTowary'
PRINT '  - Tabela: ReklamacjePartie'
PRINT '  - Tabela: ReklamacjeZdjecia'
PRINT '  - Tabela: ReklamacjeHistoria'
PRINT '  - Widok: vw_ReklamacjePelneInfo'
PRINT '  - Procedura: sp_PobierzSzczegolyReklamacji'
PRINT '  - Procedura: sp_ZmienStatusReklamacji'
PRINT '  - Procedura: sp_StatystykiReklamacji'
PRINT '=========================================='
GO
