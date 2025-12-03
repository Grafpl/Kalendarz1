-- =============================================
-- Skrypt tworzenia tabel dla panelu fakturzystek
-- i systemu historii zmian zamówień
-- Baza danych: LibraNet (192.168.0.109)
-- =============================================

-- Sprawdź czy tabela istnieje i utwórz jeśli nie
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[HistoriaZmianZamowien]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[HistoriaZmianZamowien](
        [Id] INT IDENTITY(1,1) PRIMARY KEY,

        -- Identyfikator zamówienia (z tabeli ZamowieniaMieso w bazie Handel)
        [ZamowienieId] INT NOT NULL,

        -- Typ zmiany: UTWORZENIE, EDYCJA, ANULOWANIE, PRZYWROCENIE, USUNIECIE
        [TypZmiany] NVARCHAR(50) NOT NULL,

        -- Nazwa pola które zostało zmienione (dla EDYCJA)
        [PoleZmienione] NVARCHAR(100) NULL,

        -- Wartość przed zmianą
        [WartoscPoprzednia] NVARCHAR(MAX) NULL,

        -- Wartość po zmianie
        [WartoscNowa] NVARCHAR(MAX) NULL,

        -- ID użytkownika który dokonał zmiany
        [Uzytkownik] NVARCHAR(50) NOT NULL,

        -- Pełna nazwa użytkownika (opcjonalna)
        [UzytkownikNazwa] NVARCHAR(200) NULL,

        -- Data i czas zmiany
        [DataZmiany] DATETIME NOT NULL DEFAULT GETDATE(),

        -- Pełny opis zmiany (opcjonalny, do wyświetlania)
        [OpisZmiany] NVARCHAR(MAX) NULL,

        -- Dodatkowe informacje w formacie JSON (opcjonalne)
        [DodatkoweInfo] NVARCHAR(MAX) NULL,

        -- Adres IP użytkownika (opcjonalne)
        [AdresIP] NVARCHAR(50) NULL,

        -- Nazwa komputera użytkownika (opcjonalne)
        [NazwaKomputera] NVARCHAR(100) NULL
    );

    PRINT 'Utworzono tabelę HistoriaZmianZamowien';
END
ELSE
BEGIN
    PRINT 'Tabela HistoriaZmianZamowien już istnieje';
END
GO

-- Indeksy dla wydajności wyszukiwania
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_HistoriaZmianZamowien_ZamowienieId')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_HistoriaZmianZamowien_ZamowienieId]
    ON [dbo].[HistoriaZmianZamowien] ([ZamowienieId])
    INCLUDE ([TypZmiany], [DataZmiany], [Uzytkownik]);

    PRINT 'Utworzono indeks IX_HistoriaZmianZamowien_ZamowienieId';
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_HistoriaZmianZamowien_DataZmiany')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_HistoriaZmianZamowien_DataZmiany]
    ON [dbo].[HistoriaZmianZamowien] ([DataZmiany] DESC)
    INCLUDE ([ZamowienieId], [TypZmiany], [Uzytkownik]);

    PRINT 'Utworzono indeks IX_HistoriaZmianZamowien_DataZmiany';
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_HistoriaZmianZamowien_Uzytkownik')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_HistoriaZmianZamowien_Uzytkownik]
    ON [dbo].[HistoriaZmianZamowien] ([Uzytkownik])
    INCLUDE ([ZamowienieId], [DataZmiany]);

    PRINT 'Utworzono indeks IX_HistoriaZmianZamowien_Uzytkownik';
END
GO

-- =============================================
-- Procedura do logowania zmian zamówienia
-- =============================================
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[sp_LogujZmianeZamowienia]') AND type in (N'P'))
BEGIN
    DROP PROCEDURE [dbo].[sp_LogujZmianeZamowienia];
END
GO

CREATE PROCEDURE [dbo].[sp_LogujZmianeZamowienia]
    @ZamowienieId INT,
    @TypZmiany NVARCHAR(50),
    @PoleZmienione NVARCHAR(100) = NULL,
    @WartoscPoprzednia NVARCHAR(MAX) = NULL,
    @WartoscNowa NVARCHAR(MAX) = NULL,
    @Uzytkownik NVARCHAR(50),
    @UzytkownikNazwa NVARCHAR(200) = NULL,
    @OpisZmiany NVARCHAR(MAX) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO [dbo].[HistoriaZmianZamowien]
        ([ZamowienieId], [TypZmiany], [PoleZmienione], [WartoscPoprzednia],
         [WartoscNowa], [Uzytkownik], [UzytkownikNazwa], [OpisZmiany],
         [NazwaKomputera])
    VALUES
        (@ZamowienieId, @TypZmiany, @PoleZmienione, @WartoscPoprzednia,
         @WartoscNowa, @Uzytkownik, @UzytkownikNazwa, @OpisZmiany,
         HOST_NAME());

    SELECT SCOPE_IDENTITY() AS NowyId;
END
GO

PRINT 'Utworzono procedurę sp_LogujZmianeZamowienia';
GO

-- =============================================
-- Procedura do pobierania historii zmian zamówienia
-- =============================================
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[sp_PobierzHistorieZamowienia]') AND type in (N'P'))
BEGIN
    DROP PROCEDURE [dbo].[sp_PobierzHistorieZamowienia];
END
GO

CREATE PROCEDURE [dbo].[sp_PobierzHistorieZamowienia]
    @ZamowienieId INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        [Id],
        [ZamowienieId],
        [TypZmiany],
        [PoleZmienione],
        [WartoscPoprzednia],
        [WartoscNowa],
        [Uzytkownik],
        [UzytkownikNazwa],
        [DataZmiany],
        [OpisZmiany],
        [NazwaKomputera]
    FROM [dbo].[HistoriaZmianZamowien]
    WHERE [ZamowienieId] = @ZamowienieId
    ORDER BY [DataZmiany] DESC;
END
GO

PRINT 'Utworzono procedurę sp_PobierzHistorieZamowienia';
GO

-- =============================================
-- Procedura do pobierania ostatnich zmian (dla dashboardu)
-- =============================================
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[sp_PobierzOstatnieZmiany]') AND type in (N'P'))
BEGIN
    DROP PROCEDURE [dbo].[sp_PobierzOstatnieZmiany];
END
GO

CREATE PROCEDURE [dbo].[sp_PobierzOstatnieZmiany]
    @IloscDni INT = 7,
    @LimitWierszy INT = 100
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP (@LimitWierszy)
        h.[Id],
        h.[ZamowienieId],
        h.[TypZmiany],
        h.[PoleZmienione],
        h.[WartoscPoprzednia],
        h.[WartoscNowa],
        h.[Uzytkownik],
        h.[UzytkownikNazwa],
        h.[DataZmiany],
        h.[OpisZmiany]
    FROM [dbo].[HistoriaZmianZamowien] h
    WHERE h.[DataZmiany] >= DATEADD(DAY, -@IloscDni, GETDATE())
    ORDER BY h.[DataZmiany] DESC;
END
GO

PRINT 'Utworzono procedurę sp_PobierzOstatnieZmiany';
GO

-- =============================================
-- Widok do podsumowania aktywności użytkowników
-- =============================================
IF EXISTS (SELECT * FROM sys.views WHERE object_id = OBJECT_ID(N'[dbo].[vw_AktywnoscUzytkownikow]'))
BEGIN
    DROP VIEW [dbo].[vw_AktywnoscUzytkownikow];
END
GO

CREATE VIEW [dbo].[vw_AktywnoscUzytkownikow]
AS
SELECT
    [Uzytkownik],
    [UzytkownikNazwa],
    COUNT(*) AS [IloscZmian],
    COUNT(DISTINCT [ZamowienieId]) AS [IloscZamowien],
    MAX([DataZmiany]) AS [OstatniaAktywnosc],
    SUM(CASE WHEN [TypZmiany] = 'UTWORZENIE' THEN 1 ELSE 0 END) AS [Utworzenia],
    SUM(CASE WHEN [TypZmiany] = 'EDYCJA' THEN 1 ELSE 0 END) AS [Edycje],
    SUM(CASE WHEN [TypZmiany] = 'ANULOWANIE' THEN 1 ELSE 0 END) AS [Anulowania]
FROM [dbo].[HistoriaZmianZamowien]
WHERE [DataZmiany] >= DATEADD(DAY, -30, GETDATE())
GROUP BY [Uzytkownik], [UzytkownikNazwa];
GO

PRINT 'Utworzono widok vw_AktywnoscUzytkownikow';
GO

-- =============================================
-- Przykładowe zapytania do użycia
-- =============================================
/*
-- Pobierz historię konkretnego zamówienia
EXEC sp_PobierzHistorieZamowienia @ZamowienieId = 123;

-- Pobierz ostatnie zmiany z 7 dni
EXEC sp_PobierzOstatnieZmiany @IloscDni = 7, @LimitWierszy = 50;

-- Zaloguj nową zmianę
EXEC sp_LogujZmianeZamowienia
    @ZamowienieId = 123,
    @TypZmiany = 'EDYCJA',
    @PoleZmienione = 'Notatka',
    @WartoscPoprzednia = 'Stara notatka',
    @WartoscNowa = 'Nowa notatka',
    @Uzytkownik = 'jan.kowalski',
    @UzytkownikNazwa = 'Jan Kowalski',
    @OpisZmiany = 'Zmieniono notatkę zamówienia';

-- Podsumowanie aktywności użytkowników
SELECT * FROM vw_AktywnoscUzytkownikow ORDER BY IloscZmian DESC;
*/

PRINT 'Skrypt zakończony pomyślnie';
GO
