-- =====================================================
-- CRM MODERNIZATION MIGRATION SCRIPT
-- Rozszerzenie systemu CRM o scoring, priorytety, temperatury leadow
-- =====================================================

-- =====================================================
-- CZESC 1: ROZSZERZENIE TABELI OdbiorcyCRM
-- =====================================================

-- Licznik prob kontaktu
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('OdbiorcyCRM') AND name = 'LiczbaProb')
    ALTER TABLE OdbiorcyCRM ADD LiczbaProb INT DEFAULT 0;

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('OdbiorcyCRM') AND name = 'DataOstatniejProby')
    ALTER TABLE OdbiorcyCRM ADD DataOstatniejProby DATETIME NULL;

-- Scoring
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('OdbiorcyCRM') AND name = 'FitScore')
    ALTER TABLE OdbiorcyCRM ADD FitScore INT DEFAULT 0;

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('OdbiorcyCRM') AND name = 'EngagementScore')
    ALTER TABLE OdbiorcyCRM ADD EngagementScore INT DEFAULT 0;

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('OdbiorcyCRM') AND name = 'Priorytet')
    ALTER TABLE OdbiorcyCRM ADD Priorytet CHAR(1) DEFAULT 'C'; -- A/B/C

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('OdbiorcyCRM') AND name = 'TemperaturaLeada')
    ALTER TABLE OdbiorcyCRM ADD TemperaturaLeada VARCHAR(10) DEFAULT 'COLD'; -- HOT/WARM/COLD

-- Wartosc klienta
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('OdbiorcyCRM') AND name = 'SzacowanaWartoscMiesieczna')
    ALTER TABLE OdbiorcyCRM ADD SzacowanaWartoscMiesieczna DECIMAL(12,2) NULL;

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('OdbiorcyCRM') AND name = 'SzacowanyWolumenKg')
    ALTER TABLE OdbiorcyCRM ADD SzacowanyWolumenKg INT NULL;

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('OdbiorcyCRM') AND name = 'CzestotliwoscDostaw')
    ALTER TABLE OdbiorcyCRM ADD CzestotliwoscDostaw VARCHAR(30) NULL;

-- Dane kontaktowe rozszerzone
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('OdbiorcyCRM') AND name = 'OsobaKontaktowa')
    ALTER TABLE OdbiorcyCRM ADD OsobaKontaktowa NVARCHAR(100) NULL;

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('OdbiorcyCRM') AND name = 'StanowiskoOsoby')
    ALTER TABLE OdbiorcyCRM ADD StanowiskoOsoby NVARCHAR(50) NULL;

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('OdbiorcyCRM') AND name = 'TelefonBezposredni')
    ALTER TABLE OdbiorcyCRM ADD TelefonBezposredni NVARCHAR(20) NULL;

-- Informacje o konkurencji
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('OdbiorcyCRM') AND name = 'AktualnyDostawca')
    ALTER TABLE OdbiorcyCRM ADD AktualnyDostawca NVARCHAR(100) NULL;

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('OdbiorcyCRM') AND name = 'PowodZmianyDostawcy')
    ALTER TABLE OdbiorcyCRM ADD PowodZmianyDostawcy NVARCHAR(500) NULL;

-- Produkty
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('OdbiorcyCRM') AND name = 'InteresujaceProdukty')
    ALTER TABLE OdbiorcyCRM ADD InteresujaceProdukty NVARCHAR(500) NULL;

-- Historia klienta
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('OdbiorcyCRM') AND name = 'DataPierwszegoZamowienia')
    ALTER TABLE OdbiorcyCRM ADD DataPierwszegoZamowienia DATETIME NULL;

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('OdbiorcyCRM') AND name = 'DataOstatniegoZamowienia')
    ALTER TABLE OdbiorcyCRM ADD DataOstatniegoZamowienia DATETIME NULL;

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('OdbiorcyCRM') AND name = 'LiczbaZamowien')
    ALTER TABLE OdbiorcyCRM ADD LiczbaZamowien INT DEFAULT 0;

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('OdbiorcyCRM') AND name = 'WartoscZyciowaKlienta')
    ALTER TABLE OdbiorcyCRM ADD WartoscZyciowaKlienta DECIMAL(14,2) DEFAULT 0;

-- Powod odmowy
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('OdbiorcyCRM') AND name = 'PowodOdmowy')
    ALTER TABLE OdbiorcyCRM ADD PowodOdmowy NVARCHAR(200) NULL;

-- Data "Nie teraz" - kiedy wrocic
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('OdbiorcyCRM') AND name = 'DataPonownegoKontaktu')
    ALTER TABLE OdbiorcyCRM ADD DataPonownegoKontaktu DATETIME NULL;

GO

-- =====================================================
-- CZESC 2: TABELA WYNIKOW ROZMOW
-- =====================================================

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'WynikiRozmowCRM')
BEGIN
    CREATE TABLE WynikiRozmowCRM (
        ID INT IDENTITY(1,1) PRIMARY KEY,
        IDOdbiorcy INT NOT NULL,
        DataRozmowy DATETIME DEFAULT GETDATE(),
        TypWyniku VARCHAR(50) NOT NULL,
        -- Typy: 'nie_odebral', 'zajety', 'rozmowa_pozytywna', 'rozmowa_neutralna',
        --       'rozmowa_negatywna', 'bledny_numer', 'sekretarka', 'poczta_glosowa'
        Notatka NVARCHAR(1000) NULL,
        NastepnaAkcja VARCHAR(100) NULL,
        DataNastepnejAkcji DATETIME NULL,
        KtoWykonal NVARCHAR(50) NOT NULL,
        CzasTrwaniaSekundy INT NULL,
        CONSTRAINT FK_WynikiRozmow_Odbiorca FOREIGN KEY (IDOdbiorcy)
            REFERENCES OdbiorcyCRM(ID) ON DELETE CASCADE
    );

    CREATE INDEX IX_WynikiRozmow_IDOdbiorcy ON WynikiRozmowCRM(IDOdbiorcy);
    CREATE INDEX IX_WynikiRozmow_Data ON WynikiRozmowCRM(DataRozmowy);
END
GO

-- =====================================================
-- CZESC 3: TABELA WYSLANYCH OFERT
-- =====================================================

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'WyslaneOfertyCRM')
BEGIN
    CREATE TABLE WyslaneOfertyCRM (
        ID INT IDENTITY(1,1) PRIMARY KEY,
        IDOdbiorcy INT NOT NULL,
        DataWyslania DATETIME DEFAULT GETDATE(),
        EmailOdbiorcy NVARCHAR(100) NOT NULL,
        SzablonOferty VARCHAR(50) NULL,
        Zalaczniki NVARCHAR(500) NULL,
        TrescDodatkowa NVARCHAR(1000) NULL,
        KtoWyslal NVARCHAR(50) NOT NULL,
        CzyOtwarta BIT DEFAULT 0,
        DataOtwarcia DATETIME NULL,
        IloscOtwarc INT DEFAULT 0,
        CONSTRAINT FK_WyslaneOferty_Odbiorca FOREIGN KEY (IDOdbiorcy)
            REFERENCES OdbiorcyCRM(ID) ON DELETE CASCADE
    );

    CREATE INDEX IX_WyslaneOferty_IDOdbiorcy ON WyslaneOfertyCRM(IDOdbiorcy);
END
GO

-- =====================================================
-- CZESC 4: TABELA ALERTOW DLA MANAGERA
-- =====================================================

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AlertyCRM')
BEGIN
    CREATE TABLE AlertyCRM (
        ID INT IDENTITY(1,1) PRIMARY KEY,
        TypAlertu VARCHAR(50) NOT NULL,
        -- Typy: 'hot_lead_nieobsluzony', 'handlowiec_nieaktywny', 'oferta_nieodpowiedziana',
        --       'duzy_deal', 'lead_utkniety', 'spadek_aktywnosci', 'cel_zagrozon'
        Priorytet INT DEFAULT 2, -- 1=krytyczny, 2=ostrzezenie, 3=info
        Tytul NVARCHAR(200) NOT NULL,
        Tresc NVARCHAR(500) NULL,
        IDOdbiorcy INT NULL,
        IDOperatora NVARCHAR(50) NULL,
        DataUtworzenia DATETIME DEFAULT GETDATE(),
        CzyPrzeczytany BIT DEFAULT 0,
        DataPrzeczytania DATETIME NULL,
        PrzeczytanyPrzez NVARCHAR(50) NULL
    );

    CREATE INDEX IX_Alerty_Nieprzeczytane ON AlertyCRM(CzyPrzeczytany, Priorytet);
END
GO

-- =====================================================
-- CZESC 5: TABELA CELOW SPRZEDAZOWYCH
-- =====================================================

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'CeleSprzedazoweCRM')
BEGIN
    CREATE TABLE CeleSprzedazoweCRM (
        ID INT IDENTITY(1,1) PRIMARY KEY,
        IDOperatora NVARCHAR(50) NOT NULL,
        Miesiac DATE NOT NULL, -- pierwszy dzien miesiaca
        CelAkcjiDziennie INT DEFAULT 50,
        CelNowychKlientow INT DEFAULT 5,
        CelWyslanychOfert INT DEFAULT 20,
        CelWartoscPipeline DECIMAL(14,2) NULL,
        CONSTRAINT UQ_Cele_Operator_Miesiac UNIQUE (IDOperatora, Miesiac)
    );
END
GO

-- =====================================================
-- CZESC 6: TABELA SZABLONOW OFERT
-- =====================================================

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'SzablonyOfertCRM')
BEGIN
    CREATE TABLE SzablonyOfertCRM (
        ID INT IDENTITY(1,1) PRIMARY KEY,
        NazwaSzablonu NVARCHAR(100) NOT NULL,
        OpisSzablonu NVARCHAR(500) NULL,
        TematEmaila NVARCHAR(200) NOT NULL,
        TrescEmaila NVARCHAR(MAX) NOT NULL,
        DomyslneZalaczniki NVARCHAR(500) NULL,
        CzyAktywny BIT DEFAULT 1,
        DataUtworzenia DATETIME DEFAULT GETDATE()
    );

    -- Dodaj domyslne szablony
    INSERT INTO SzablonyOfertCRM (NazwaSzablonu, OpisSzablonu, TematEmaila, TrescEmaila, DomyslneZalaczniki)
    VALUES
    ('Oferta standardowa', 'Podstawowa oferta z cennikiem',
     'Oferta wspolpracy - [NAZWA_FIRMY]',
     'Szanowni Panstwo,

W nawiazaniu do naszej rozmowy telefonicznej przesylam oferte wspolpracy.

W zalaczeniu znajda Panstwo aktualny cennik naszych produktow.

Jestesmy ubojnia drobiu specjalizujaca sie w [SPECJALIZACJA]. Oferujemy:
- Tuszki kurczaka i indyka
- Elementy drobiowe (filety, cwiartki, skrzydla, podroby)
- Regularne dostawy wlasnym transportem chlodniczym
- Elastyczne warunki platnosci

Chetnie odpowiem na wszelkie pytania i omowie szczegoly wspolpracy.

Z powazaniem,
[HANDLOWIEC]
[TELEFON]',
     'cennik_aktualny.pdf'),

    ('Oferta dla hurtowni', 'Oferta z rabatami ilosciowymi dla hurtowni',
     'Oferta hurtowa - [NAZWA_FIRMY]',
     'Szanowni Panstwo,

Dziekuje za zainteresowanie nasza oferta.

Dla hurtowni oferujemy specjalne warunki:
- Rabaty ilosciowe od 500 kg/tydzien
- Dedykowany opiekun handlowy
- Priorytetowa realizacja zamowien
- Mozliwosc zamowien awaryjnych

W zalaczeniu cennik z uwzglednieniem rabatow ilosciowych.

Zapraszam do kontaktu w celu omowienia szczegolow.

Z powazaniem,
[HANDLOWIEC]',
     'cennik_hurtowy.pdf;warunki_wspolpracy.pdf'),

    ('Oferta dla gastronomii', 'Oferta dla restauracji i hoteli',
     'Oferta dla gastronomii - [NAZWA_FIRMY]',
     'Szanowni Panstwo,

Specjalnie dla sektora HoReCa przygotowalismy oferte uwzgledniajaca:
- Dostawy nawet codziennie
- Porcjowanie wedlug specyfikacji
- Stale ceny przez okres kontraktu
- Mozliwosc zamowien do godziny 14:00 na nastepny dzien

W zalaczeniu cennik oraz katalog produktow.

Zapraszam na degustacje naszych produktow.

Z powazaniem,
[HANDLOWIEC]',
     'cennik_gastronomia.pdf;katalog_produktow.pdf');
END
GO

-- =====================================================
-- CZESC 7: PROCEDURY SKLADOWANE
-- =====================================================

-- Procedura: Rejestruj probe kontaktu
CREATE OR ALTER PROCEDURE sp_RejestrujProbeKontaktu
    @IDOdbiorcy INT,
    @TypWyniku VARCHAR(50),
    @Notatka NVARCHAR(1000) = NULL,
    @KtoWykonal NVARCHAR(50),
    @DataNastepnejAkcji DATETIME = NULL
AS
BEGIN
    DECLARE @LiczbaProb INT = 0;

    -- Zwieksz licznik prob jesli nie odebral
    IF @TypWyniku IN ('nie_odebral', 'zajety', 'poczta_glosowa', 'sekretarka')
    BEGIN
        UPDATE OdbiorcyCRM
        SET LiczbaProb = LiczbaProb + 1,
            DataOstatniejProby = GETDATE()
        WHERE ID = @IDOdbiorcy;

        -- Sprawdz czy osiagnieto 8 prob
        SELECT @LiczbaProb = LiczbaProb FROM OdbiorcyCRM WHERE ID = @IDOdbiorcy;

        IF @LiczbaProb >= 8
        BEGIN
            UPDATE OdbiorcyCRM
            SET Status = 'Nieosiagalny',
                DataPonownegoKontaktu = DATEADD(day, 90, GETDATE())
            WHERE ID = @IDOdbiorcy;

            -- Zmien status w historii
            INSERT INTO HistoriaZmianCRM (IDOdbiorcy, TypZmiany, WartoscStara, WartoscNowa, KtoWykonal)
            VALUES (@IDOdbiorcy, 'Zmiana statusu', 'Proba kontaktu', 'Nieosiagalny', @KtoWykonal);
        END
    END
    ELSE IF @TypWyniku = 'bledny_numer'
    BEGIN
        UPDATE OdbiorcyCRM SET Status = 'Do weryfikacji' WHERE ID = @IDOdbiorcy;
    END

    -- Zapisz wynik rozmowy
    INSERT INTO WynikiRozmowCRM (IDOdbiorcy, TypWyniku, Notatka, KtoWykonal, DataNastepnejAkcji)
    VALUES (@IDOdbiorcy, @TypWyniku, @Notatka, @KtoWykonal, @DataNastepnejAkcji);

    -- Ustaw date nastepnego kontaktu jesli podano
    IF @DataNastepnejAkcji IS NOT NULL
    BEGIN
        UPDATE OdbiorcyCRM SET DataNastepnegoKontaktu = @DataNastepnejAkcji WHERE ID = @IDOdbiorcy;
    END

    SELECT @LiczbaProb AS LiczbaProb;
END
GO

-- Procedura: Aktualizuj EngagementScore i Temperature
CREATE OR ALTER PROCEDURE sp_AktualizujEngagement
    @IDOdbiorcy INT,
    @Akcja VARCHAR(50) -- 'spotkanie', 'oferta', 'zainteresowany', 'moze_pozniej', 'rozmowa', 'odmowa', 'brak_kontaktu'
AS
BEGIN
    DECLARE @DeltaScore INT = 0;
    DECLARE @NowyScore INT;

    -- Okresl zmiane score
    SET @DeltaScore = CASE @Akcja
        WHEN 'spotkanie' THEN 100
        WHEN 'probki' THEN 80
        WHEN 'oferta' THEN 50
        WHEN 'zainteresowany' THEN 40
        WHEN 'moze_pozniej' THEN 20
        WHEN 'rozmowa' THEN 10
        WHEN 'brak_kontaktu_7dni' THEN -10
        WHEN 'brak_kontaktu_14dni' THEN -20
        WHEN 'brak_kontaktu_30dni' THEN -30
        WHEN 'odmowa' THEN -50
        ELSE 0
    END;

    -- Aktualizuj score
    UPDATE OdbiorcyCRM
    SET EngagementScore = CASE
            WHEN EngagementScore + @DeltaScore < 0 THEN 0
            WHEN EngagementScore + @DeltaScore > 200 THEN 200
            ELSE EngagementScore + @DeltaScore
        END
    WHERE ID = @IDOdbiorcy;

    -- Pobierz nowy score
    SELECT @NowyScore = EngagementScore FROM OdbiorcyCRM WHERE ID = @IDOdbiorcy;

    -- Ustal temperature
    DECLARE @Temperatura VARCHAR(10);
    IF @NowyScore >= 75
        SET @Temperatura = 'HOT'
    ELSE IF @NowyScore >= 30
        SET @Temperatura = 'WARM'
    ELSE
        SET @Temperatura = 'COLD';

    UPDATE OdbiorcyCRM SET TemperaturaLeada = @Temperatura WHERE ID = @IDOdbiorcy;

    SELECT @NowyScore AS EngagementScore, @Temperatura AS Temperatura;
END
GO

-- Procedura: Oblicz FitScore dla odbiorcy
CREATE OR ALTER PROCEDURE sp_ObliczFitScore
    @IDOdbiorcy INT
AS
BEGIN
    DECLARE @Score INT = 0;
    DECLARE @Branza NVARCHAR(500);
    DECLARE @Wojewodztwo NVARCHAR(100);
    DECLARE @Dystans FLOAT;
    DECLARE @Email NVARCHAR(100);

    SELECT
        @Branza = PKD_Opis,
        @Wojewodztwo = Wojewodztwo,
        @Email = Email
    FROM OdbiorcyCRM WHERE ID = @IDOdbiorcy;

    -- SCORING BRANZY (max 40 pkt)
    IF @Branza LIKE '%hurtow%' OR @Branza LIKE '%dystrybu%'
        SET @Score = @Score + 40
    ELSE IF @Branza LIKE '%przetw%' OR @Branza LIKE '%mies%'
        SET @Score = @Score + 35
    ELSE IF @Branza LIKE '%masarn%' OR @Branza LIKE '%sklep%'
        SET @Score = @Score + 30
    ELSE IF @Branza LIKE '%gastro%' OR @Branza LIKE '%restaur%' OR @Branza LIKE '%hotel%'
        SET @Score = @Score + 25
    ELSE
        SET @Score = @Score + 10;

    -- SCORING LOKALIZACJI (max 20 pkt)
    SELECT @Dystans =
        CASE
            WHEN kp.Latitude IS NOT NULL THEN
                6371 * ACOS(
                    COS(RADIANS(51.907335)) * COS(RADIANS(kp.Latitude)) *
                    COS(RADIANS(kp.Longitude) - RADIANS(19.678605)) +
                    SIN(RADIANS(51.907335)) * SIN(RADIANS(kp.Latitude))
                )
            ELSE 999
        END
    FROM OdbiorcyCRM o
    LEFT JOIN KodyPocztowe kp ON o.KOD = kp.Kod
    WHERE o.ID = @IDOdbiorcy;

    IF @Dystans < 100
        SET @Score = @Score + 20
    ELSE IF @Dystans < 200
        SET @Score = @Score + 15
    ELSE IF @Dystans < 300
        SET @Score = @Score + 10
    ELSE IF @Dystans < 500
        SET @Score = @Score + 5
    ELSE
        SET @Score = @Score - 5;

    -- SCORING WOJEWODZTWA (max 15 pkt)
    IF @Wojewodztwo IN ('lodzkie', 'mazowieckie', 'wielkopolskie', 'slaskie')
        SET @Score = @Score + 15
    ELSE IF @Wojewodztwo IN ('dolnoslaskie', 'malopolskie', 'kujawsko-pomorskie')
        SET @Score = @Score + 10
    ELSE
        SET @Score = @Score + 5;

    -- SCORING EMAILA (max 10 pkt)
    IF @Email IS NOT NULL AND @Email NOT LIKE '%@gmail%' AND @Email NOT LIKE '%@wp.pl%'
       AND @Email NOT LIKE '%@o2.pl%' AND @Email NOT LIKE '%@onet.pl%'
        SET @Score = @Score + 10
    ELSE IF @Email IS NOT NULL
        SET @Score = @Score + 5;

    -- Ogranicz do 0-100
    IF @Score < 0 SET @Score = 0;
    IF @Score > 100 SET @Score = 100;

    -- Ustal priorytet
    DECLARE @Priorytet CHAR(1);
    IF @Score >= 70
        SET @Priorytet = 'A'
    ELSE IF @Score >= 45
        SET @Priorytet = 'B'
    ELSE
        SET @Priorytet = 'C';

    -- Aktualizuj
    UPDATE OdbiorcyCRM
    SET FitScore = @Score, Priorytet = @Priorytet
    WHERE ID = @IDOdbiorcy;

    SELECT @Score AS FitScore, @Priorytet AS Priorytet;
END
GO

-- =====================================================
-- CZESC 8: TRIGGER DLA NOWYCH REKORDOW
-- =====================================================

-- Trigger: Automatyczne obliczanie FitScore przy INSERT
IF EXISTS (SELECT * FROM sys.triggers WHERE name = 'tr_ObliczFitScore_Insert')
    DROP TRIGGER tr_ObliczFitScore_Insert;
GO

CREATE TRIGGER tr_ObliczFitScore_Insert
ON OdbiorcyCRM
AFTER INSERT
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @ID INT;
    DECLARE cur CURSOR FOR SELECT ID FROM inserted;
    OPEN cur;
    FETCH NEXT FROM cur INTO @ID;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        EXEC sp_ObliczFitScore @ID;
        FETCH NEXT FROM cur INTO @ID;
    END
    CLOSE cur;
    DEALLOCATE cur;
END
GO

-- =====================================================
-- CZESC 9: PRZELICZENIE ISTNIEJACYCH REKORDOW
-- =====================================================

-- Przelicz FitScore dla wszystkich istniejacych rekordow
DECLARE @ID INT;
DECLARE cur CURSOR FOR SELECT ID FROM OdbiorcyCRM WHERE FitScore = 0 OR FitScore IS NULL;
OPEN cur;
FETCH NEXT FROM cur INTO @ID;
WHILE @@FETCH_STATUS = 0
BEGIN
    EXEC sp_ObliczFitScore @ID;
    FETCH NEXT FROM cur INTO @ID;
END
CLOSE cur;
DEALLOCATE cur;
GO

PRINT 'CRM Modernization Migration completed successfully!';
GO
