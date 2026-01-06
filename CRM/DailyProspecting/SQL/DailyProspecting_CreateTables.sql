-- ============================================
-- DAILY PROSPECTING - SKRYPT SQL
-- System automatycznego przydzielania telefonów
-- ============================================
-- Uruchom ten skrypt w SQL Server Management Studio
-- na bazie LibraNet (192.168.0.109)
-- ============================================

USE LibraNet;
GO

-- ============================================
-- KROK 1: ROZSZERZENIE TABELI OdbiorcyCRM
-- ============================================
-- Dodanie nowych kolumn potrzebnych do prospectingu

-- 1.1 Priorytet (1-5, gdzie 5 = najwyższy)
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('OdbiorcyCRM') AND name = 'Priorytet')
BEGIN
    ALTER TABLE OdbiorcyCRM ADD Priorytet INT DEFAULT 3;
    PRINT 'Dodano kolumnę Priorytet do OdbiorcyCRM';
END
GO

-- 1.2 Źródło leada (skąd pochodzi)
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('OdbiorcyCRM') AND name = 'Zrodlo')
BEGIN
    ALTER TABLE OdbiorcyCRM ADD Zrodlo NVARCHAR(100) DEFAULT 'Import';
    PRINT 'Dodano kolumnę Zrodlo do OdbiorcyCRM';
END
GO

-- 1.3 Typ klienta (Hurtownia/Sieć/HoReCa/Przetwórnia/Cash&Carry/Inny)
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('OdbiorcyCRM') AND name = 'TypKlienta')
BEGIN
    ALTER TABLE OdbiorcyCRM ADD TypKlienta NVARCHAR(50) DEFAULT 'Inny';
    PRINT 'Dodano kolumnę TypKlienta do OdbiorcyCRM';
END
GO

-- 1.4 Data dodania leada
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('OdbiorcyCRM') AND name = 'DataDodania')
BEGIN
    ALTER TABLE OdbiorcyCRM ADD DataDodania DATETIME DEFAULT GETDATE();
    PRINT 'Dodano kolumnę DataDodania do OdbiorcyCRM';
END
GO

-- 1.5 Kto dodał leada
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('OdbiorcyCRM') AND name = 'DodanyPrzez')
BEGIN
    ALTER TABLE OdbiorcyCRM ADD DodanyPrzez NVARCHAR(50) NULL;
    PRINT 'Dodano kolumnę DodanyPrzez do OdbiorcyCRM';
END
GO

-- 1.6 Ostatni rezultat kontaktu (do priorytetyzacji)
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('OdbiorcyCRM') AND name = 'OstatniRezultat')
BEGIN
    ALTER TABLE OdbiorcyCRM ADD OstatniRezultat NVARCHAR(50) NULL;
    PRINT 'Dodano kolumnę OstatniRezultat do OdbiorcyCRM';
END
GO

-- 1.7 Data ostatniego kontaktu
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('OdbiorcyCRM') AND name = 'DataOstatniegoKontaktu')
BEGIN
    ALTER TABLE OdbiorcyCRM ADD DataOstatniegoKontaktu DATETIME NULL;
    PRINT 'Dodano kolumnę DataOstatniegoKontaktu do OdbiorcyCRM';
END
GO

-- ============================================
-- KROK 2: TABELA KonfiguracjaProspectingu
-- ============================================
-- Konfiguracja limitów i filtrów per handlowiec

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'KonfiguracjaProspectingu')
BEGIN
    CREATE TABLE KonfiguracjaProspectingu (
        KonfigID INT PRIMARY KEY IDENTITY(1,1),
        HandlowiecID NVARCHAR(50) NOT NULL,          -- ID operatora z tabeli operators
        HandlowiecNazwa NVARCHAR(100) NULL,          -- Nazwa handlowca (cache)
        LimitDzienny INT DEFAULT 8,                   -- Ile leadów dziennie przydzielać
        GodzinaStart TIME DEFAULT '09:00',           -- Kiedy zaczyna dzwonić
        GodzinaKoniec TIME DEFAULT '10:30',          -- Kiedy kończy blok prospectingu
        DniTygodnia NVARCHAR(20) DEFAULT '1,2,3,4,5', -- Dni tygodnia (1=pon, 5=pt)
        Wojewodztwa NVARCHAR(500) NULL,              -- Lista województw (NULL = wszystkie)
        TypyKlientow NVARCHAR(200) NULL,             -- Lista typów (NULL = wszystkie)
        PKD NVARCHAR(500) NULL,                      -- Lista kodów PKD/branż (NULL = wszystkie)
        PriorytetMin INT DEFAULT 1,                  -- Minimalny priorytet leadów
        PriorytetMax INT DEFAULT 5,                  -- Maksymalny priorytet leadów
        Aktywny BIT DEFAULT 1,                       -- Czy konfiguracja aktywna
        DataUtworzenia DATETIME DEFAULT GETDATE(),
        DataModyfikacji DATETIME NULL,

        CONSTRAINT UQ_KonfiguracjaProspectingu_Handlowiec UNIQUE (HandlowiecID)
    );
    PRINT 'Utworzono tabelę KonfiguracjaProspectingu';
END
GO

-- ============================================
-- KROK 3: TABELA CodzienaKolejkaTelefonow
-- ============================================
-- Przechowuje dzienne przydziały telefonów

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'CodzienaKolejkaTelefonow')
BEGIN
    CREATE TABLE CodzienaKolejkaTelefonow (
        KolejkaID INT PRIMARY KEY IDENTITY(1,1),
        HandlowiecID NVARCHAR(50) NOT NULL,          -- ID operatora
        OdbiorcaID INT NOT NULL,                     -- ID z OdbiorcyCRM
        DataPrzydzialu DATE NOT NULL,                -- Na który dzień przydzielono
        Priorytet INT DEFAULT 0,                     -- Priorytet w kolejce (wyższy = ważniejszy)
        PowodPriorytetu NVARCHAR(200) NULL,          -- Dlaczego ten priorytet (np. "Follow-up", "Gorący lead")

        -- Status realizacji
        StatusRealizacji NVARCHAR(20) DEFAULT 'Oczekuje',  -- Oczekuje/Wykonano/Pominięto/Przeniesiono
        GodzinaWykonania DATETIME NULL,              -- Kiedy wykonano telefon
        RezultatRozmowy NVARCHAR(50) NULL,           -- Rezultat: Rozmowa/Nieodebrany/Callback/Odmowa/Oferta
        Notatka NVARCHAR(500) NULL,                  -- Krótka notatka z rozmowy

        DataUtworzenia DATETIME DEFAULT GETDATE(),

        -- Indeksy i klucze
        CONSTRAINT FK_Kolejka_Odbiorca FOREIGN KEY (OdbiorcaID) REFERENCES OdbiorcyCRM(ID),
        INDEX IX_Kolejka_HandlowiecData (HandlowiecID, DataPrzydzialu),
        INDEX IX_Kolejka_Data (DataPrzydzialu),
        INDEX IX_Kolejka_Status (StatusRealizacji)
    );
    PRINT 'Utworzono tabelę CodzienaKolejkaTelefonow';
END
GO

-- ============================================
-- KROK 4: TABELA StatystykiProspectingu
-- ============================================
-- Agregowane statystyki dzienne (dla szybkich raportów)

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'StatystykiProspectingu')
BEGIN
    CREATE TABLE StatystykiProspectingu (
        StatID INT PRIMARY KEY IDENTITY(1,1),
        HandlowiecID NVARCHAR(50) NOT NULL,
        Data DATE NOT NULL,

        -- Liczniki
        Przydzielone INT DEFAULT 0,                  -- Ile przydzielono
        Wykonane INT DEFAULT 0,                      -- Ile telefonów wykonano
        Rozmowy INT DEFAULT 0,                       -- Ile rozmów odbyto
        Nieodebrane INT DEFAULT 0,                   -- Ile nieodebranych
        Callbacki INT DEFAULT 0,                     -- Ile próśb o callback
        Odmowy INT DEFAULT 0,                        -- Ile odmów
        Oferty INT DEFAULT 0,                        -- Ile ofert wysłano
        Pominiete INT DEFAULT 0,                     -- Ile pominięto

        -- Metryki
        ProcentRealizacji AS (CASE WHEN Przydzielone > 0 THEN CAST(Wykonane * 100.0 / Przydzielone AS DECIMAL(5,1)) ELSE 0 END),
        ProcentSkutecznosci AS (CASE WHEN Wykonane > 0 THEN CAST(Rozmowy * 100.0 / Wykonane AS DECIMAL(5,1)) ELSE 0 END),

        DataUtworzenia DATETIME DEFAULT GETDATE(),

        CONSTRAINT UQ_StatystykiProspectingu UNIQUE (HandlowiecID, Data),
        INDEX IX_Statystyki_Data (Data)
    );
    PRINT 'Utworzono tabelę StatystykiProspectingu';
END
GO

-- ============================================
-- KROK 5: PROCEDURA GenerujCodzienaKolejke
-- ============================================
-- Generuje kolejkę telefonów na dany dzień dla wszystkich handlowców

IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'GenerujCodzienaKolejke')
    DROP PROCEDURE GenerujCodzienaKolejke;
GO

CREATE PROCEDURE [dbo].[GenerujCodzienaKolejke]
    @Data DATE = NULL,           -- Data na którą generować (domyślnie dziś)
    @HandlowiecID NVARCHAR(50) = NULL  -- Opcjonalnie tylko dla jednego handlowca
AS
BEGIN
    SET NOCOUNT ON;

    -- Domyślnie dzisiejsza data
    IF @Data IS NULL SET @Data = CAST(GETDATE() AS DATE);

    -- Sprawdź czy to dzień roboczy (pon-pt)
    DECLARE @DzienTygodnia INT = DATEPART(WEEKDAY, @Data);
    -- W SQL Server: 1=niedziela, 2=pon, 3=wt, 4=śr, 5=czw, 6=pt, 7=sob
    -- Konwertujemy na 1=pon, 7=niedz
    SET @DzienTygodnia = CASE @DzienTygodnia
        WHEN 1 THEN 7  -- niedziela
        WHEN 2 THEN 1  -- poniedziałek
        WHEN 3 THEN 2
        WHEN 4 THEN 3
        WHEN 5 THEN 4
        WHEN 6 THEN 5  -- piątek
        WHEN 7 THEN 6  -- sobota
    END;

    DECLARE @HandlowiecIDLoop NVARCHAR(50);
    DECLARE @LimitDzienny INT;
    DECLARE @Wojewodztwa NVARCHAR(500);
    DECLARE @TypyKlientow NVARCHAR(200);
    DECLARE @PKD NVARCHAR(500);
    DECLARE @PriorytetMin INT;
    DECLARE @PriorytetMax INT;
    DECLARE @DniTygodnia NVARCHAR(20);
    DECLARE @Licznik INT;

    -- Kursor po aktywnych konfiguracjach handlowców
    DECLARE cur_handlowcy CURSOR FOR
        SELECT HandlowiecID, LimitDzienny, Wojewodztwa, TypyKlientow, PKD, PriorytetMin, PriorytetMax, DniTygodnia
        FROM KonfiguracjaProspectingu
        WHERE Aktywny = 1
          AND (@HandlowiecID IS NULL OR HandlowiecID = @HandlowiecID);

    OPEN cur_handlowcy;
    FETCH NEXT FROM cur_handlowcy INTO @HandlowiecIDLoop, @LimitDzienny, @Wojewodztwa, @TypyKlientow, @PKD, @PriorytetMin, @PriorytetMax, @DniTygodnia;

    WHILE @@FETCH_STATUS = 0
    BEGIN
        -- Sprawdź czy handlowiec pracuje w ten dzień
        IF CHARINDEX(CAST(@DzienTygodnia AS NVARCHAR), @DniTygodnia) > 0
        BEGIN
            -- Usuń starą kolejkę na ten dzień dla tego handlowca (jeśli regenerujemy)
            DELETE FROM CodzienaKolejkaTelefonow
            WHERE HandlowiecID = @HandlowiecIDLoop
              AND DataPrzydzialu = @Data
              AND StatusRealizacji = 'Oczekuje';  -- Tylko niewykonane

            -- Generuj nową kolejkę
            -- Algorytm priorytetyzacji:
            -- 100 pkt - DataNastepnegoKontaktu = dziś (umówiony follow-up)
            -- 90 pkt - Status = 'Zgoda na dalszy kontakt' (gorący lead)
            -- 80 pkt - Status = 'Do wysłania oferta' i brak kontaktu > 3 dni
            -- 70 pkt - Status = 'Do zadzwonienia' (świeży lead)
            -- 60 pkt - OstatniRezultat = 'Callback'
            -- 50 pkt - Brak kontaktu > 14 dni
            -- + Priorytet leada (1-5) * 5 punktów

            INSERT INTO CodzienaKolejkaTelefonow (HandlowiecID, OdbiorcaID, DataPrzydzialu, Priorytet, PowodPriorytetu)
            SELECT TOP (@LimitDzienny)
                @HandlowiecIDLoop,
                o.ID,
                @Data,
                -- Oblicz priorytet
                CASE
                    WHEN CAST(o.DataNastepnegoKontaktu AS DATE) = @Data THEN 100
                    WHEN o.Status = 'Zgoda na dalszy kontakt' THEN 90
                    WHEN o.Status = 'Do wysłania oferta' AND DATEDIFF(DAY, o.DataOstatniegoKontaktu, @Data) > 3 THEN 80
                    WHEN o.Status = 'Do zadzwonienia' OR o.Status IS NULL THEN 70
                    WHEN o.OstatniRezultat = 'Callback' THEN 60
                    WHEN DATEDIFF(DAY, o.DataOstatniegoKontaktu, @Data) > 14 THEN 50
                    ELSE 30
                END + ISNULL(o.Priorytet, 3) * 5 AS Priorytet,
                -- Powód priorytetu
                CASE
                    WHEN CAST(o.DataNastepnegoKontaktu AS DATE) = @Data THEN 'Umówiony follow-up'
                    WHEN o.Status = 'Zgoda na dalszy kontakt' THEN 'Gorący lead - zgoda na kontakt'
                    WHEN o.Status = 'Do wysłania oferta' THEN 'Follow-up oferty'
                    WHEN o.Status = 'Do zadzwonienia' OR o.Status IS NULL THEN 'Nowy lead'
                    WHEN o.OstatniRezultat = 'Callback' THEN 'Prośba o callback'
                    WHEN DATEDIFF(DAY, o.DataOstatniegoKontaktu, @Data) > 14 THEN 'Dawno nieaktywny'
                    ELSE 'Standardowy kontakt'
                END AS PowodPriorytetu
            FROM OdbiorcyCRM o
            LEFT JOIN WlascicieleOdbiorcow w ON o.ID = w.IDOdbiorcy
            WHERE
                -- Tylko leady przypisane do tego handlowca lub nieprzypisane
                (w.OperatorID = @HandlowiecIDLoop OR w.OperatorID IS NULL)
                -- Tylko aktywne statusy (nie stracone, nie DNC)
                AND ISNULL(o.Status, 'Do zadzwonienia') NOT IN ('Nie zainteresowany', 'Poprosił o usunięcie', 'Błędny rekord (do raportu)', 'DNC', 'Stracony')
                -- Priorytet w zakresie
                AND ISNULL(o.Priorytet, 3) BETWEEN @PriorytetMin AND @PriorytetMax
                -- Filtr województw (jeśli ustawiony)
                AND (@Wojewodztwa IS NULL OR o.Wojewodztwo IN (SELECT value FROM STRING_SPLIT(@Wojewodztwa, ',')))
                -- Filtr typów klientów (jeśli ustawiony)
                AND (@TypyKlientow IS NULL OR o.TypKlienta IN (SELECT value FROM STRING_SPLIT(@TypyKlientow, ',')))
                -- Filtr branż PKD (jeśli ustawiony)
                AND (@PKD IS NULL OR o.PKD_Opis IN (SELECT value FROM STRING_SPLIT(@PKD, ',')))
                -- Nie był jeszcze w kolejce dzisiaj
                AND NOT EXISTS (
                    SELECT 1 FROM CodzienaKolejkaTelefonow k
                    WHERE k.OdbiorcaID = o.ID AND k.DataPrzydzialu = @Data
                )
                -- Ma telefon
                AND o.Telefon_K IS NOT NULL AND o.Telefon_K <> ''
            ORDER BY
                -- Najpierw priorytet (DESC = najwyższy pierwszy)
                CASE
                    WHEN CAST(o.DataNastepnegoKontaktu AS DATE) = @Data THEN 100
                    WHEN o.Status = 'Zgoda na dalszy kontakt' THEN 90
                    WHEN o.Status = 'Do wysłania oferta' THEN 80
                    WHEN o.Status = 'Do zadzwonienia' OR o.Status IS NULL THEN 70
                    WHEN o.OstatniRezultat = 'Callback' THEN 60
                    ELSE 30
                END + ISNULL(o.Priorytet, 3) * 5 DESC,
                -- Potem data następnego kontaktu (najwcześniejsze najpierw)
                o.DataNastepnegoKontaktu ASC,
                -- Na końcu losowo (żeby nie zawsze te same)
                NEWID();

            SET @Licznik = @@ROWCOUNT;
            PRINT 'Wygenerowano ' + CAST(@Licznik AS NVARCHAR) + ' telefonów dla handlowca ' + @HandlowiecIDLoop;

            -- Aktualizuj/dodaj statystyki
            IF EXISTS (SELECT 1 FROM StatystykiProspectingu WHERE HandlowiecID = @HandlowiecIDLoop AND Data = @Data)
            BEGIN
                UPDATE StatystykiProspectingu
                SET Przydzielone = @Licznik
                WHERE HandlowiecID = @HandlowiecIDLoop AND Data = @Data;
            END
            ELSE
            BEGIN
                INSERT INTO StatystykiProspectingu (HandlowiecID, Data, Przydzielone)
                VALUES (@HandlowiecIDLoop, @Data, @Licznik);
            END
        END

        FETCH NEXT FROM cur_handlowcy INTO @HandlowiecIDLoop, @LimitDzienny, @Wojewodztwa, @TypyKlientow, @PKD, @PriorytetMin, @PriorytetMax, @DniTygodnia;
    END

    CLOSE cur_handlowcy;
    DEALLOCATE cur_handlowcy;

    -- Zwróć podsumowanie
    SELECT
        k.HandlowiecID,
        o.Name as HandlowiecNazwa,
        COUNT(*) as LiczbaWKolejce,
        @Data as DataKolejki
    FROM CodzienaKolejkaTelefonow k
    LEFT JOIN operators o ON k.HandlowiecID = CAST(o.ID AS NVARCHAR)
    WHERE k.DataPrzydzialu = @Data
    GROUP BY k.HandlowiecID, o.Name;
END
GO

PRINT 'Utworzono procedurę GenerujCodzienaKolejke';
GO

-- ============================================
-- KROK 6: PROCEDURA AktualizujStatystykiProspectingu
-- ============================================
-- Aktualizuje statystyki na podstawie wykonanych telefonów

IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'AktualizujStatystykiProspectingu')
    DROP PROCEDURE AktualizujStatystykiProspectingu;
GO

CREATE PROCEDURE [dbo].[AktualizujStatystykiProspectingu]
    @HandlowiecID NVARCHAR(50),
    @Data DATE = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF @Data IS NULL SET @Data = CAST(GETDATE() AS DATE);

    -- Oblicz statystyki z kolejki
    DECLARE @Przydzielone INT, @Wykonane INT, @Rozmowy INT, @Nieodebrane INT;
    DECLARE @Callbacki INT, @Odmowy INT, @Oferty INT, @Pominiete INT;

    SELECT
        @Przydzielone = COUNT(*),
        @Wykonane = SUM(CASE WHEN StatusRealizacji = 'Wykonano' THEN 1 ELSE 0 END),
        @Pominiete = SUM(CASE WHEN StatusRealizacji = 'Pominięto' THEN 1 ELSE 0 END),
        @Rozmowy = SUM(CASE WHEN RezultatRozmowy = 'Rozmowa' THEN 1 ELSE 0 END),
        @Nieodebrane = SUM(CASE WHEN RezultatRozmowy = 'Nieodebrany' THEN 1 ELSE 0 END),
        @Callbacki = SUM(CASE WHEN RezultatRozmowy = 'Callback' THEN 1 ELSE 0 END),
        @Odmowy = SUM(CASE WHEN RezultatRozmowy = 'Odmowa' THEN 1 ELSE 0 END),
        @Oferty = SUM(CASE WHEN RezultatRozmowy = 'Oferta' THEN 1 ELSE 0 END)
    FROM CodzienaKolejkaTelefonow
    WHERE HandlowiecID = @HandlowiecID AND DataPrzydzialu = @Data;

    -- Aktualizuj lub wstaw rekord
    IF EXISTS (SELECT 1 FROM StatystykiProspectingu WHERE HandlowiecID = @HandlowiecID AND Data = @Data)
    BEGIN
        UPDATE StatystykiProspectingu SET
            Przydzielone = @Przydzielone,
            Wykonane = @Wykonane,
            Rozmowy = @Rozmowy,
            Nieodebrane = @Nieodebrane,
            Callbacki = @Callbacki,
            Odmowy = @Odmowy,
            Oferty = @Oferty,
            Pominiete = @Pominiete
        WHERE HandlowiecID = @HandlowiecID AND Data = @Data;
    END
    ELSE
    BEGIN
        INSERT INTO StatystykiProspectingu (HandlowiecID, Data, Przydzielone, Wykonane, Rozmowy, Nieodebrane, Callbacki, Odmowy, Oferty, Pominiete)
        VALUES (@HandlowiecID, @Data, @Przydzielone, @Wykonane, @Rozmowy, @Nieodebrane, @Callbacki, @Odmowy, @Oferty, @Pominiete);
    END
END
GO

PRINT 'Utworzono procedurę AktualizujStatystykiProspectingu';
GO

-- ============================================
-- KROK 7: WIDOK vw_KolejkaDzisiejsza
-- ============================================
-- Widok dla szybkiego pobierania dzisiejszej kolejki

IF EXISTS (SELECT * FROM sys.views WHERE name = 'vw_KolejkaDzisiejsza')
    DROP VIEW vw_KolejkaDzisiejsza;
GO

CREATE VIEW [dbo].[vw_KolejkaDzisiejsza]
AS
SELECT
    k.KolejkaID,
    k.HandlowiecID,
    op.Name as HandlowiecNazwa,
    k.OdbiorcaID,
    o.Nazwa as NazwaFirmy,
    o.Telefon_K as Telefon,
    o.Email,
    o.MIASTO as Miasto,
    o.Wojewodztwo,
    o.PKD_Opis as Branza,
    o.TypKlienta,
    ISNULL(o.Status, 'Do zadzwonienia') as StatusCRM,
    o.DataNastepnegoKontaktu,
    k.Priorytet,
    k.PowodPriorytetu,
    k.StatusRealizacji,
    k.GodzinaWykonania,
    k.RezultatRozmowy,
    k.Notatka,
    -- Ostatnia notatka z CRM
    (SELECT TOP 1 n.Tresc FROM NotatkiCRM n WHERE n.IDOdbiorcy = o.ID ORDER BY n.DataUtworzenia DESC) as OstatniaNot,
    -- Liczba wszystkich notatek
    (SELECT COUNT(*) FROM NotatkiCRM n WHERE n.IDOdbiorcy = o.ID) as LiczbaNotatek
FROM CodzienaKolejkaTelefonow k
INNER JOIN OdbiorcyCRM o ON k.OdbiorcaID = o.ID
LEFT JOIN operators op ON k.HandlowiecID = CAST(op.ID AS NVARCHAR)
WHERE k.DataPrzydzialu = CAST(GETDATE() AS DATE);
GO

PRINT 'Utworzono widok vw_KolejkaDzisiejsza';
GO

-- ============================================
-- KROK 8: DOMYŚLNE DANE KONFIGURACJI
-- ============================================
-- Możesz dostosować te wartości do swoich handlowców

-- Przykładowa konfiguracja (zakomentowana - odkomentuj i dostosuj)
/*
-- Sprawdź ID operatorów
SELECT ID, Name FROM operators WHERE Name LIKE '%handlowiec%' OR Access LIKE '%SPRZEDAZ%';

-- Dodaj konfigurację dla handlowców
INSERT INTO KonfiguracjaProspectingu (HandlowiecID, HandlowiecNazwa, LimitDzienny, Wojewodztwa, TypyKlientow)
VALUES
('12345', 'Jan Kowalski', 10, 'łódzkie,mazowieckie', NULL),
('12346', 'Anna Nowak', 8, 'łódzkie,wielkopolskie', 'Hurtownia,HoReCa'),
('12347', 'Piotr Wiśniewski', 6, 'łódzkie', 'HoReCa');
*/

-- ============================================
-- KROK 9: DIAGNOSTYKA I WERYFIKACJA
-- ============================================

PRINT '';
PRINT '=== WERYFIKACJA INSTALACJI ===';
PRINT '';

-- Sprawdź nowe kolumny w OdbiorcyCRM
SELECT
    'Kolumny OdbiorcyCRM' as Info,
    COLUMN_NAME as Kolumna,
    DATA_TYPE as Typ
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'OdbiorcyCRM'
  AND COLUMN_NAME IN ('Priorytet', 'Zrodlo', 'TypKlienta', 'DataDodania', 'DodanyPrzez', 'OstatniRezultat', 'DataOstatniegoKontaktu')
ORDER BY COLUMN_NAME;

-- Sprawdź nowe tabele
SELECT
    'Nowe tabele' as Info,
    TABLE_NAME as Tabela
FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_NAME IN ('KonfiguracjaProspectingu', 'CodzienaKolejkaTelefonow', 'StatystykiProspectingu');

-- Sprawdź procedury
SELECT
    'Procedury' as Info,
    name as Procedura
FROM sys.procedures
WHERE name IN ('GenerujCodzienaKolejke', 'AktualizujStatystykiProspectingu');

PRINT '';
PRINT '=== INSTALACJA ZAKOŃCZONA ===';
PRINT 'Następne kroki:';
PRINT '1. Dodaj konfigurację dla handlowców do tabeli KonfiguracjaProspectingu';
PRINT '2. Uruchom EXEC GenerujCodzienaKolejke aby wygenerować pierwszą kolejkę';
PRINT '3. Opcjonalnie: Skonfiguruj SQL Agent Job do automatycznego uruchamiania o 6:00';
GO
