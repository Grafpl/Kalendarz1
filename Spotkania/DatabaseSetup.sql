-- ============================================
-- SYSTEM ZARZĄDZANIA SPOTKANIAMI - SCHEMAT BAZY DANYCH
-- Kalendarz1 - Piórkowscy
-- ============================================

USE LibraNet;
GO

-- ============================================
-- TABELA GŁÓWNA: Spotkania
-- ============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Spotkania')
BEGIN
    CREATE TABLE Spotkania (
        SpotkaniID BIGINT IDENTITY(1,1) PRIMARY KEY,

        -- Podstawowe informacje
        Tytul NVARCHAR(500) NOT NULL,
        Opis NVARCHAR(MAX),
        DataSpotkania DATETIME NOT NULL,
        DataZakonczenia DATETIME,
        CzasTrwaniaMin INT DEFAULT 60,

        -- Typ spotkania
        TypSpotkania NVARCHAR(50) DEFAULT 'Zespół', -- Zespół, Odbiorca, Hodowca, Online
        Lokalizacja NVARCHAR(500), -- Sala konferencyjna, adres, etc.

        -- Status spotkania
        Status NVARCHAR(50) DEFAULT 'Zaplanowane', -- Zaplanowane, W trakcie, Zakończone, Anulowane

        -- Organizator
        OrganizatorID NVARCHAR(50) NOT NULL,
        OrganizatorNazwa NVARCHAR(255),

        -- Kontrahent (opcjonalne - dla spotkań u Odbiorcy/Hodowcy)
        KontrahentID NVARCHAR(50),
        KontrahentNazwa NVARCHAR(255),
        KontrahentTyp NVARCHAR(50), -- Odbiorca, Hodowca

        -- Spotkanie online
        LinkSpotkania NVARCHAR(500), -- Zoom, Teams, Google Meet link

        -- Integracja Fireflies.ai
        FirefliesTranscriptID NVARCHAR(100),
        FirefliesMeetingUrl NVARCHAR(500),

        -- Powiązanie z notatką
        NotatkaID BIGINT,

        -- Przypomnienia (w minutach przed spotkaniem)
        PrzypomnienieMinuty NVARCHAR(100) DEFAULT '1440,60,15', -- 24h, 1h, 15min

        -- Rekurencja (opcjonalne)
        CzyRekurencyjne BIT DEFAULT 0,
        RekurencjaTyp NVARCHAR(50), -- Codziennie, Cotygodniowo, Comiesięcznie
        RekurencjaKoniec DATETIME,
        RekurencjaRodzicID BIGINT,

        -- Priorytety i kategorie
        Priorytet NVARCHAR(20) DEFAULT 'Normalny', -- Niski, Normalny, Wysoki, Pilny
        Kategoria NVARCHAR(100),
        Kolor NVARCHAR(20) DEFAULT '#2196F3', -- Kolor w kalendarzu

        -- Timestamps
        DataUtworzenia DATETIME DEFAULT GETDATE(),
        DataModyfikacji DATETIME,

        -- Foreign Keys
        CONSTRAINT FK_Spotkania_Notatka FOREIGN KEY (NotatkaID)
            REFERENCES NotatkiZeSpotkan(NotatkaID) ON DELETE SET NULL,
        CONSTRAINT FK_Spotkania_Rodzic FOREIGN KEY (RekurencjaRodzicID)
            REFERENCES Spotkania(SpotkaniID) ON DELETE NO ACTION
    );

    -- Indeksy
    CREATE INDEX IX_Spotkania_DataSpotkania ON Spotkania(DataSpotkania);
    CREATE INDEX IX_Spotkania_OrganizatorID ON Spotkania(OrganizatorID);
    CREATE INDEX IX_Spotkania_Status ON Spotkania(Status);
    CREATE INDEX IX_Spotkania_FirefliesID ON Spotkania(FirefliesTranscriptID);

    PRINT 'Tabela Spotkania utworzona pomyślnie.';
END
ELSE
    PRINT 'Tabela Spotkania już istnieje.';
GO

-- ============================================
-- TABELA: SpotkaniaUczestnicy
-- ============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'SpotkaniaUczestnicy')
BEGIN
    CREATE TABLE SpotkaniaUczestnicy (
        SpotkaniID BIGINT NOT NULL,
        OperatorID NVARCHAR(50) NOT NULL,
        OperatorNazwa NVARCHAR(255),

        -- Status uczestnika
        StatusZaproszenia NVARCHAR(50) DEFAULT 'Oczekuje', -- Oczekuje, Zaakceptowane, Odrzucone, Może
        CzyObowiazkowy BIT DEFAULT 0, -- Czy obecność jest wymagana

        -- Powiadomienia
        CzyPowiadomiony BIT DEFAULT 0,
        DataPowiadomienia DATETIME,

        -- Uczestnictwo
        CzyUczestniczyl BIT,
        DataDolaczenia DATETIME,

        -- Notatki uczestnika
        NotatkaUczestnika NVARCHAR(MAX),

        PRIMARY KEY (SpotkaniID, OperatorID),
        CONSTRAINT FK_SpotkaniaUczestnicy_Spotkanie FOREIGN KEY (SpotkaniID)
            REFERENCES Spotkania(SpotkaniID) ON DELETE CASCADE
    );

    CREATE INDEX IX_SpotkaniaUczestnicy_OperatorID ON SpotkaniaUczestnicy(OperatorID);

    PRINT 'Tabela SpotkaniaUczestnicy utworzona pomyślnie.';
END
ELSE
    PRINT 'Tabela SpotkaniaUczestnicy już istnieje.';
GO

-- ============================================
-- TABELA: SpotkaniaNotyfikacje
-- ============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'SpotkaniaNotyfikacje')
BEGIN
    CREATE TABLE SpotkaniaNotyfikacje (
        NotyfikacjaID BIGINT IDENTITY(1,1) PRIMARY KEY,

        -- Powiązania
        SpotkaniID BIGINT NOT NULL,
        OperatorID NVARCHAR(50) NOT NULL,

        -- Typ i treść
        TypNotyfikacji NVARCHAR(50) NOT NULL, -- Zaproszenie, Przypomnienie24h, Przypomnienie1h, Przypomnienie15m, Zmiana, Anulowanie
        Tytul NVARCHAR(500),
        Tresc NVARCHAR(MAX),

        -- Dane spotkania (snapshot)
        SpotkanieDataSpotkania DATETIME,
        SpotkanieTytul NVARCHAR(500),

        -- Status
        CzyWyslana BIT DEFAULT 0,
        DataWyslania DATETIME,
        CzyPrzeczytana BIT DEFAULT 0,
        DataPrzeczytania DATETIME,

        -- Akcje
        LinkAkcji NVARCHAR(500), -- Link do spotkania lub akcji

        -- Timestamps
        DataUtworzenia DATETIME DEFAULT GETDATE(),
        DataWygasniecia DATETIME, -- Po tym czasie powiadomienie jest nieaktualne

        CONSTRAINT FK_SpotkaniaNotyfikacje_Spotkanie FOREIGN KEY (SpotkaniID)
            REFERENCES Spotkania(SpotkaniID) ON DELETE CASCADE
    );

    CREATE INDEX IX_SpotkaniaNotyfikacje_OperatorID ON SpotkaniaNotyfikacje(OperatorID);
    CREATE INDEX IX_SpotkaniaNotyfikacje_CzyPrzeczytana ON SpotkaniaNotyfikacje(CzyPrzeczytana);
    CREATE INDEX IX_SpotkaniaNotyfikacje_DataUtworzenia ON SpotkaniaNotyfikacje(DataUtworzenia DESC);

    PRINT 'Tabela SpotkaniaNotyfikacje utworzona pomyślnie.';
END
ELSE
    PRINT 'Tabela SpotkaniaNotyfikacje już istnieje.';
GO

-- ============================================
-- TABELA: FirefliesKonfiguracja
-- ============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'FirefliesKonfiguracja')
BEGIN
    CREATE TABLE FirefliesKonfiguracja (
        ID INT IDENTITY(1,1) PRIMARY KEY,

        -- API
        ApiKey NVARCHAR(500), -- Zaszyfrowany klucz API
        ApiKeyPlain NVARCHAR(500), -- Dla uproszczenia (w produkcji należy szyfrować!)

        -- Ustawienia synchronizacji
        AutoImportNotatek BIT DEFAULT 1,
        AutoSynchronizacja BIT DEFAULT 1,
        InterwalSynchronizacjiMin INT DEFAULT 15,

        -- Filtry importu
        ImportujOdDaty DATETIME,
        MinimalnyCzasSpotkaniaSek INT DEFAULT 60, -- Nie importuj spotkań krótszych niż 1 min

        -- Status
        OstatniaSynchronizacja DATETIME,
        OstatniBladSynchronizacji NVARCHAR(MAX),
        Aktywna BIT DEFAULT 1,

        -- Timestamps
        DataUtworzenia DATETIME DEFAULT GETDATE(),
        DataModyfikacji DATETIME
    );

    PRINT 'Tabela FirefliesKonfiguracja utworzona pomyślnie.';
END
ELSE
    PRINT 'Tabela FirefliesKonfiguracja już istnieje.';
GO

-- ============================================
-- TABELA: FirefliesTranskrypcje
-- ============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'FirefliesTranskrypcje')
BEGIN
    CREATE TABLE FirefliesTranskrypcje (
        TranskrypcjaID BIGINT IDENTITY(1,1) PRIMARY KEY,

        -- Fireflies ID
        FirefliesID NVARCHAR(100) NOT NULL UNIQUE,

        -- Podstawowe dane spotkania
        Tytul NVARCHAR(500),
        DataSpotkania DATETIME,
        CzasTrwaniaSekundy INT,

        -- Uczestnicy (JSON array)
        Uczestnicy NVARCHAR(MAX),
        HostEmail NVARCHAR(255),

        -- Transkrypcja i podsumowanie
        Transkrypcja NVARCHAR(MAX), -- Pełna transkrypcja
        TranskrypcjaUrl NVARCHAR(500),

        -- Analiza NLP (JSON)
        Podsumowanie NVARCHAR(MAX),
        AkcjeDoDziałania NVARCHAR(MAX), -- Action items
        SlowKluczowe NVARCHAR(MAX), -- Keywords
        NastepneKroki NVARCHAR(MAX), -- Next steps

        -- Sentyment
        SentymentOgolny NVARCHAR(50),
        SentymentSzczegoly NVARCHAR(MAX), -- JSON

        -- Powiązania
        SpotkaniID BIGINT,
        NotatkaID BIGINT,

        -- Status
        StatusImportu NVARCHAR(50) DEFAULT 'Zaimportowane', -- Zaimportowane, Przetworzone, Błąd
        BladImportu NVARCHAR(MAX),

        -- Timestamps
        DataImportu DATETIME DEFAULT GETDATE(),
        DataModyfikacji DATETIME,

        CONSTRAINT FK_FirefliesTranskrypcje_Spotkanie FOREIGN KEY (SpotkaniID)
            REFERENCES Spotkania(SpotkaniID) ON DELETE SET NULL,
        CONSTRAINT FK_FirefliesTranskrypcje_Notatka FOREIGN KEY (NotatkaID)
            REFERENCES NotatkiZeSpotkan(NotatkaID) ON DELETE SET NULL
    );

    CREATE INDEX IX_FirefliesTranskrypcje_FirefliesID ON FirefliesTranskrypcje(FirefliesID);
    CREATE INDEX IX_FirefliesTranskrypcje_DataSpotkania ON FirefliesTranskrypcje(DataSpotkania);

    PRINT 'Tabela FirefliesTranskrypcje utworzona pomyślnie.';
END
ELSE
    PRINT 'Tabela FirefliesTranskrypcje już istnieje.';
GO

-- ============================================
-- TABELA: SpotkaniaZalaczniki
-- ============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'SpotkaniaZalaczniki')
BEGIN
    CREATE TABLE SpotkaniaZalaczniki (
        ZalacznikID BIGINT IDENTITY(1,1) PRIMARY KEY,

        SpotkaniID BIGINT NOT NULL,

        NazwaPliku NVARCHAR(255) NOT NULL,
        SciezkaPliku NVARCHAR(500),
        TypPliku NVARCHAR(100),
        RozmiarBajtow BIGINT,

        -- Treść (dla małych plików)
        Zawartosc VARBINARY(MAX),

        -- Metadata
        Opis NVARCHAR(500),
        DodanyPrzez NVARCHAR(50),
        DataDodania DATETIME DEFAULT GETDATE(),

        CONSTRAINT FK_SpotkaniaZalaczniki_Spotkanie FOREIGN KEY (SpotkaniID)
            REFERENCES Spotkania(SpotkaniID) ON DELETE CASCADE
    );

    PRINT 'Tabela SpotkaniaZalaczniki utworzona pomyślnie.';
END
ELSE
    PRINT 'Tabela SpotkaniaZalaczniki już istnieje.';
GO

-- ============================================
-- WIDOK: vw_SpotkaniaKalendarz
-- ============================================
IF EXISTS (SELECT * FROM sys.views WHERE name = 'vw_SpotkaniaKalendarz')
    DROP VIEW vw_SpotkaniaKalendarz;
GO

CREATE VIEW vw_SpotkaniaKalendarz AS
SELECT
    s.SpotkaniID,
    s.Tytul,
    s.Opis,
    s.DataSpotkania,
    s.DataZakonczenia,
    s.CzasTrwaniaMin,
    s.TypSpotkania,
    s.Status,
    s.OrganizatorID,
    s.OrganizatorNazwa,
    s.KontrahentNazwa,
    s.Lokalizacja,
    s.LinkSpotkania,
    s.Priorytet,
    s.Kolor,
    s.NotatkaID,
    s.FirefliesTranscriptID,
    (SELECT COUNT(*) FROM SpotkaniaUczestnicy u WHERE u.SpotkaniID = s.SpotkaniID) AS LiczbaUczestnikow,
    (SELECT COUNT(*) FROM SpotkaniaUczestnicy u WHERE u.SpotkaniID = s.SpotkaniID AND u.StatusZaproszenia = 'Zaakceptowane') AS LiczbaPotwierdzonych
FROM Spotkania s
WHERE s.Status != 'Anulowane';
GO

PRINT 'Widok vw_SpotkaniaKalendarz utworzony pomyślnie.';
GO

-- ============================================
-- WIDOK: vw_NadchodzaceSpotkania
-- ============================================
IF EXISTS (SELECT * FROM sys.views WHERE name = 'vw_NadchodzaceSpotkania')
    DROP VIEW vw_NadchodzaceSpotkania;
GO

CREATE VIEW vw_NadchodzaceSpotkania AS
SELECT
    s.SpotkaniID,
    s.Tytul,
    s.DataSpotkania,
    s.CzasTrwaniaMin,
    s.TypSpotkania,
    s.Status,
    s.OrganizatorID,
    s.OrganizatorNazwa,
    s.Lokalizacja,
    s.LinkSpotkania,
    s.Priorytet,
    u.OperatorID AS UczestnikID,
    u.OperatorNazwa AS UczestnikNazwa,
    u.StatusZaproszenia,
    u.CzyObowiazkowy,
    u.CzyPowiadomiony,
    DATEDIFF(MINUTE, GETDATE(), s.DataSpotkania) AS MinutyDoSpotkania
FROM Spotkania s
INNER JOIN SpotkaniaUczestnicy u ON s.SpotkaniID = u.SpotkaniID
WHERE s.Status = 'Zaplanowane'
  AND s.DataSpotkania > GETDATE()
  AND s.DataSpotkania < DATEADD(DAY, 7, GETDATE());
GO

PRINT 'Widok vw_NadchodzaceSpotkania utworzony pomyślnie.';
GO

-- ============================================
-- PROCEDURA: sp_UtworzPrzypomnienia
-- Tworzy powiadomienia dla nadchodzących spotkań
-- ============================================
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_UtworzPrzypomnienia')
    DROP PROCEDURE sp_UtworzPrzypomnienia;
GO

CREATE PROCEDURE sp_UtworzPrzypomnienia
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Teraz DATETIME = GETDATE();

    -- Przypomnienie 24h przed
    INSERT INTO SpotkaniaNotyfikacje (SpotkaniID, OperatorID, TypNotyfikacji, Tytul, Tresc, SpotkanieDataSpotkania, SpotkanieTytul, DataWygasniecia)
    SELECT
        s.SpotkaniID,
        u.OperatorID,
        'Przypomnienie24h',
        'Spotkanie jutro: ' + s.Tytul,
        'Przypomnienie o spotkaniu "' + s.Tytul + '" zaplanowanym na ' + FORMAT(s.DataSpotkania, 'dd.MM.yyyy HH:mm') +
        CASE WHEN s.Lokalizacja IS NOT NULL THEN '. Miejsce: ' + s.Lokalizacja ELSE '' END,
        s.DataSpotkania,
        s.Tytul,
        s.DataSpotkania
    FROM Spotkania s
    INNER JOIN SpotkaniaUczestnicy u ON s.SpotkaniID = u.SpotkaniID
    WHERE s.Status = 'Zaplanowane'
      AND s.DataSpotkania BETWEEN DATEADD(HOUR, 23, @Teraz) AND DATEADD(HOUR, 25, @Teraz)
      AND s.PrzypomnienieMinuty LIKE '%1440%'
      AND NOT EXISTS (
          SELECT 1 FROM SpotkaniaNotyfikacje n
          WHERE n.SpotkaniID = s.SpotkaniID
            AND n.OperatorID = u.OperatorID
            AND n.TypNotyfikacji = 'Przypomnienie24h'
      );

    -- Przypomnienie 1h przed
    INSERT INTO SpotkaniaNotyfikacje (SpotkaniID, OperatorID, TypNotyfikacji, Tytul, Tresc, SpotkanieDataSpotkania, SpotkanieTytul, DataWygasniecia)
    SELECT
        s.SpotkaniID,
        u.OperatorID,
        'Przypomnienie1h',
        'Spotkanie za godzinę: ' + s.Tytul,
        'Za godzinę rozpocznie się spotkanie "' + s.Tytul + '".' +
        CASE WHEN s.LinkSpotkania IS NOT NULL THEN ' Link: ' + s.LinkSpotkania ELSE '' END,
        s.DataSpotkania,
        s.Tytul,
        s.DataSpotkania
    FROM Spotkania s
    INNER JOIN SpotkaniaUczestnicy u ON s.SpotkaniID = u.SpotkaniID
    WHERE s.Status = 'Zaplanowane'
      AND s.DataSpotkania BETWEEN DATEADD(MINUTE, 55, @Teraz) AND DATEADD(MINUTE, 65, @Teraz)
      AND s.PrzypomnienieMinuty LIKE '%60%'
      AND NOT EXISTS (
          SELECT 1 FROM SpotkaniaNotyfikacje n
          WHERE n.SpotkaniID = s.SpotkaniID
            AND n.OperatorID = u.OperatorID
            AND n.TypNotyfikacji = 'Przypomnienie1h'
      );

    -- Przypomnienie 15 min przed
    INSERT INTO SpotkaniaNotyfikacje (SpotkaniID, OperatorID, TypNotyfikacji, Tytul, Tresc, SpotkanieDataSpotkania, SpotkanieTytul, DataWygasniecia)
    SELECT
        s.SpotkaniID,
        u.OperatorID,
        'Przypomnienie15m',
        'Spotkanie za 15 minut: ' + s.Tytul,
        'Za 15 minut rozpocznie się spotkanie "' + s.Tytul + '"!' +
        CASE WHEN s.LinkSpotkania IS NOT NULL THEN ' Dołącz teraz: ' + s.LinkSpotkania ELSE '' END,
        s.DataSpotkania,
        s.Tytul,
        s.DataSpotkania
    FROM Spotkania s
    INNER JOIN SpotkaniaUczestnicy u ON s.SpotkaniID = u.SpotkaniID
    WHERE s.Status = 'Zaplanowane'
      AND s.DataSpotkania BETWEEN DATEADD(MINUTE, 13, @Teraz) AND DATEADD(MINUTE, 17, @Teraz)
      AND s.PrzypomnienieMinuty LIKE '%15%'
      AND NOT EXISTS (
          SELECT 1 FROM SpotkaniaNotyfikacje n
          WHERE n.SpotkaniID = s.SpotkaniID
            AND n.OperatorID = u.OperatorID
            AND n.TypNotyfikacji = 'Przypomnienie15m'
      );

    -- Zwróć liczbę utworzonych powiadomień
    SELECT @@ROWCOUNT AS UtworzoneNotyfikacje;
END;
GO

PRINT 'Procedura sp_UtworzPrzypomnienia utworzona pomyślnie.';
GO

-- ============================================
-- PROCEDURA: sp_PobierzNieprzeczytaneNotyfikacje
-- ============================================
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_PobierzNieprzeczytaneNotyfikacje')
    DROP PROCEDURE sp_PobierzNieprzeczytaneNotyfikacje;
GO

CREATE PROCEDURE sp_PobierzNieprzeczytaneNotyfikacje
    @OperatorID NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        n.NotyfikacjaID,
        n.SpotkaniID,
        n.TypNotyfikacji,
        n.Tytul,
        n.Tresc,
        n.SpotkanieDataSpotkania,
        n.SpotkanieTytul,
        n.CzyPrzeczytana,
        n.DataUtworzenia,
        n.LinkAkcji,
        s.LinkSpotkania,
        s.Lokalizacja,
        DATEDIFF(MINUTE, GETDATE(), n.SpotkanieDataSpotkania) AS MinutyDoSpotkania
    FROM SpotkaniaNotyfikacje n
    LEFT JOIN Spotkania s ON n.SpotkaniID = s.SpotkaniID
    WHERE n.OperatorID = @OperatorID
      AND n.CzyPrzeczytana = 0
      AND (n.DataWygasniecia IS NULL OR n.DataWygasniecia > GETDATE())
    ORDER BY n.DataUtworzenia DESC;
END;
GO

PRINT 'Procedura sp_PobierzNieprzeczytaneNotyfikacje utworzona pomyślnie.';
GO

-- ============================================
-- PROCEDURA: sp_OznaczNotyfikacjePrzeczytane
-- ============================================
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_OznaczNotyfikacjePrzeczytane')
    DROP PROCEDURE sp_OznaczNotyfikacjePrzeczytane;
GO

CREATE PROCEDURE sp_OznaczNotyfikacjePrzeczytane
    @OperatorID NVARCHAR(50),
    @NotyfikacjaID BIGINT = NULL -- NULL = oznacz wszystkie
AS
BEGIN
    SET NOCOUNT ON;

    IF @NotyfikacjaID IS NOT NULL
    BEGIN
        UPDATE SpotkaniaNotyfikacje
        SET CzyPrzeczytana = 1, DataPrzeczytania = GETDATE()
        WHERE NotyfikacjaID = @NotyfikacjaID AND OperatorID = @OperatorID;
    END
    ELSE
    BEGIN
        UPDATE SpotkaniaNotyfikacje
        SET CzyPrzeczytana = 1, DataPrzeczytania = GETDATE()
        WHERE OperatorID = @OperatorID AND CzyPrzeczytana = 0;
    END

    SELECT @@ROWCOUNT AS OznaczoneNotyfikacje;
END;
GO

PRINT 'Procedura sp_OznaczNotyfikacjePrzeczytane utworzona pomyślnie.';
GO

PRINT '';
PRINT '============================================';
PRINT 'SCHEMAT BAZY DANYCH UTWORZONY POMYŚLNIE!';
PRINT '============================================';
GO
