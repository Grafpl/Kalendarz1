-- ============================================
-- MODUŁ KADROWY ZPSP - STRUKTURA BAZY DANYCH
-- Uruchom na bazie ZPSP (nie UNISYSTEM!)
-- ============================================

-- Użyj swojej bazy ZPSP
-- USE ZPSP;
-- GO

-- =============================================
-- TABELA 1: TYPY NIEOBECNOŚCI
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'HR_TypyNieobecnosci')
BEGIN
    CREATE TABLE HR_TypyNieobecnosci (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Kod NVARCHAR(10) NOT NULL UNIQUE,           -- UW, UZ, L4, OK, OP, UB, IN
        Nazwa NVARCHAR(100) NOT NULL,
        Platne BIT DEFAULT 1,                        -- Czy płatne
        WymagaZatwierdzenia BIT DEFAULT 1,          -- Czy wymaga zatwierdzenia kierownika
        LimitDniRoczny INT NULL,                     -- Limit dni w roku (NULL = bez limitu)
        Kolor NVARCHAR(7) DEFAULT '#3182CE',        -- Kolor do wyświetlania
        Aktywny BIT DEFAULT 1,
        Kolejnosc INT DEFAULT 0
    );

    INSERT INTO HR_TypyNieobecnosci (Kod, Nazwa, Platne, WymagaZatwierdzenia, LimitDniRoczny, Kolor, Kolejnosc) VALUES
    ('UW', 'Urlop wypoczynkowy', 1, 1, 26, '#38A169', 1),
    ('UZ', 'Urlop na żądanie', 1, 1, 4, '#DD6B20', 2),
    ('L4', 'Zwolnienie lekarskie (L4)', 1, 0, NULL, '#E53E3E', 3),
    ('OK', 'Urlop okolicznościowy', 1, 1, NULL, '#805AD5', 4),
    ('OP', 'Opieka nad dzieckiem', 1, 1, 2, '#D69E2E', 5),
    ('UB', 'Urlop bezpłatny', 0, 1, NULL, '#718096', 6),
    ('NN', 'Nieobecność nieusprawiedliwiona', 0, 0, NULL, '#C53030', 7),
    ('IN', 'Inna nieobecność', 1, 1, NULL, '#4A5568', 8);
END
GO

-- =============================================
-- TABELA 2: NIEOBECNOŚCI PRACOWNIKÓW
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'HR_Nieobecnosci')
BEGIN
    CREATE TABLE HR_Nieobecnosci (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        PracownikId INT NOT NULL,                   -- ID z UNICARD (RCINE_EMPLOYEE_ID)
        PracownikImie NVARCHAR(100),
        PracownikNazwisko NVARCHAR(100),
        PracownikDzial NVARCHAR(100),
        TypNieobecnosciId INT NOT NULL FOREIGN KEY REFERENCES HR_TypyNieobecnosci(Id),
        DataOd DATE NOT NULL,
        DataDo DATE NOT NULL,
        IloscDni INT NOT NULL,
        IloscGodzin DECIMAL(5,2) NULL,              -- Dla niepełnych dni
        Uwagi NVARCHAR(500),
        Status NVARCHAR(20) DEFAULT 'Oczekuje',     -- Oczekuje, Zatwierdzona, Odrzucona
        DataZgloszenia DATETIME DEFAULT GETDATE(),
        ZatwierdzilId INT NULL,
        ZatwierdzilNazwa NVARCHAR(100) NULL,
        DataZatwierdzenia DATETIME NULL,
        PowodOdrzucenia NVARCHAR(500) NULL
    );

    CREATE INDEX IX_HR_Nieobecnosci_Pracownik ON HR_Nieobecnosci(PracownikId);
    CREATE INDEX IX_HR_Nieobecnosci_Data ON HR_Nieobecnosci(DataOd, DataDo);
    CREATE INDEX IX_HR_Nieobecnosci_Status ON HR_Nieobecnosci(Status);
END
GO

-- =============================================
-- TABELA 3: USTAWIENIA DZIAŁÓW (godziny pracy)
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'HR_UstawieniaDzialow')
BEGIN
    CREATE TABLE HR_UstawieniaDzialow (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        NazwaDzialu NVARCHAR(100) NOT NULL UNIQUE,
        GodzinaRozpoczecia TIME NOT NULL DEFAULT '06:00',
        GodzinaZakonczenia TIME NOT NULL DEFAULT '14:00',
        CzasPracyGodzin DECIMAL(4,2) DEFAULT 8.0,
        TolerancjaMinut INT DEFAULT 5,              -- Tolerancja spóźnienia
        MaxGodzinAgencja DECIMAL(4,2) DEFAULT 12.0, -- Max godzin dla agencji
        MaxGodzinEtat DECIMAL(4,2) DEFAULT 13.0,    -- Max godzin dla etatu
        Aktywny BIT DEFAULT 1
    );

    -- Domyślne ustawienia dla znanych działów
    INSERT INTO HR_UstawieniaDzialow (NazwaDzialu, GodzinaRozpoczecia, GodzinaZakonczenia, CzasPracyGodzin) VALUES
    ('PRODUKCJA', '06:00', '14:00', 8.0),
    ('BRUDNA', '06:00', '14:00', 8.0),
    ('CZYSTA', '06:00', '14:00', 8.0),
    ('MROŹNIA', '06:00', '14:00', 8.0),
    ('MYJKA', '06:00', '14:00', 8.0),
    ('BIURO', '08:00', '16:00', 8.0),
    ('MECHANIK', '06:00', '14:00', 8.0),
    ('KIEROWCA', '05:00', '13:00', 8.0),
    ('SPRZEDAWCA', '07:00', '15:00', 8.0),
    ('PORTIERZY', '06:00', '18:00', 12.0),
    ('MASARNIA', '06:00', '14:00', 8.0),
    ('-- DOMYŚLNY --', '06:00', '14:00', 8.0);
END
GO

-- =============================================
-- TABELA 4: PRZERWY NA PRODUKCJI
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'HR_Przerwy')
BEGIN
    CREATE TABLE HR_Przerwy (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Nazwa NVARCHAR(100) NOT NULL,               -- np. "Śniadanie", "Obiad"
        Dzial NVARCHAR(100) NULL,                   -- NULL = wszystkie działy
        GodzinaOd TIME NOT NULL,
        GodzinaDo TIME NOT NULL,
        CzasTrwaniaMinut INT NOT NULL,
        DniTygodnia NVARCHAR(20) DEFAULT '1,2,3,4,5', -- 1=Pon, 7=Niedz
        Aktywna BIT DEFAULT 1,
        DataOd DATE NULL,                           -- Od kiedy obowiązuje
        DataDo DATE NULL,                           -- Do kiedy (NULL = bezterminowo)
        UtworzylId INT NULL,
        UtworzylNazwa NVARCHAR(100),
        DataUtworzenia DATETIME DEFAULT GETDATE()
    );

    -- Przykładowe przerwy
    INSERT INTO HR_Przerwy (Nazwa, Dzial, GodzinaOd, GodzinaDo, CzasTrwaniaMinut, DniTygodnia) VALUES
    ('Śniadanie', 'PRODUKCJA', '09:00', '09:15', 15, '1,2,3,4,5'),
    ('Obiad', 'PRODUKCJA', '12:00', '12:30', 30, '1,2,3,4,5'),
    ('Śniadanie', 'BRUDNA', '09:00', '09:15', 15, '1,2,3,4,5'),
    ('Obiad', 'BRUDNA', '12:00', '12:30', 30, '1,2,3,4,5'),
    ('Śniadanie', 'CZYSTA', '09:00', '09:15', 15, '1,2,3,4,5'),
    ('Obiad', 'CZYSTA', '12:00', '12:30', 30, '1,2,3,4,5');
END
GO

-- =============================================
-- TABELA 5: BILANS GODZIN (do odebrania/odpracowania)
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'HR_BilansGodzin')
BEGIN
    CREATE TABLE HR_BilansGodzin (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        PracownikId INT NOT NULL,
        PracownikImie NVARCHAR(100),
        PracownikNazwisko NVARCHAR(100),
        Data DATE NOT NULL,
        GodzinyPrzepracowane DECIMAL(5,2) NOT NULL,
        GodzinyNorma DECIMAL(5,2) NOT NULL,         -- Ile powinien przepracować
        Roznica DECIMAL(5,2) NOT NULL,              -- + nadpracowane, - niedopracowane
        Typ NVARCHAR(20) NOT NULL,                  -- 'NADPRACOWANE', 'NIEDOPRACOWANE', 'NORMA'
        Uwagi NVARCHAR(500),
        Odebrane BIT DEFAULT 0,                     -- Czy już odebrano/odpracowano
        DataOdebrania DATE NULL,
        CONSTRAINT UQ_BilansGodzin UNIQUE (PracownikId, Data)
    );

    CREATE INDEX IX_HR_BilansGodzin_Pracownik ON HR_BilansGodzin(PracownikId);
    CREATE INDEX IX_HR_BilansGodzin_Data ON HR_BilansGodzin(Data);
END
GO

-- =============================================
-- TABELA 6: KOREKTY REJESTRACJI (brak odbicia)
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'HR_Korekty')
BEGIN
    CREATE TABLE HR_Korekty (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        PracownikId INT NOT NULL,
        PracownikImie NVARCHAR(100),
        PracownikNazwisko NVARCHAR(100),
        Data DATE NOT NULL,
        TypKorekty NVARCHAR(20) NOT NULL,           -- 'BRAK_WEJSCIA', 'BRAK_WYJSCIA', 'RECZNA'
        GodzinaOryginalna TIME NULL,                -- Oryginalna godzina (jeśli była)
        GodzinaPoprawiona TIME NOT NULL,            -- Poprawiona/dodana godzina
        TypRejestracji NVARCHAR(10) NOT NULL,       -- 'WE' lub 'WY'
        Powod NVARCHAR(500),
        KorygujacyId INT NULL,
        KorygujacyNazwa NVARCHAR(100),
        DataKorekty DATETIME DEFAULT GETDATE()
    );

    CREATE INDEX IX_HR_Korekty_Pracownik ON HR_Korekty(PracownikId);
    CREATE INDEX IX_HR_Korekty_Data ON HR_Korekty(Data);
END
GO

-- =============================================
-- TABELA 7: SPÓŹNIENIA
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'HR_Spoznienia')
BEGIN
    CREATE TABLE HR_Spoznienia (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        PracownikId INT NOT NULL,
        PracownikImie NVARCHAR(100),
        PracownikNazwisko NVARCHAR(100),
        PracownikDzial NVARCHAR(100),
        Data DATE NOT NULL,
        GodzinaPlanowaStart TIME NOT NULL,
        GodzinaFaktycznaStart TIME NOT NULL,
        SpoznienieMinut INT NOT NULL,
        Usprawiedliwione BIT DEFAULT 0,
        PowodUsprawiedliwienia NVARCHAR(500),
        CONSTRAINT UQ_Spoznienia UNIQUE (PracownikId, Data)
    );

    CREATE INDEX IX_HR_Spoznienia_Pracownik ON HR_Spoznienia(PracownikId);
    CREATE INDEX IX_HR_Spoznienia_Data ON HR_Spoznienia(Data);
END
GO

-- =============================================
-- TABELA 8: ALERTY (historia)
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'HR_Alerty')
BEGIN
    CREATE TABLE HR_Alerty (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        PracownikId INT NOT NULL,
        PracownikImie NVARCHAR(100),
        PracownikNazwisko NVARCHAR(100),
        PracownikDzial NVARCHAR(100),
        Data DATE NOT NULL,
        TypAlertu NVARCHAR(50) NOT NULL,            -- 'PRZEKROCZENIE_12H', 'PRZEKROCZENIE_13H', 'SPOZNIENIE', 'BRAK_ODBICIA'
        Opis NVARCHAR(500),
        Wartosc DECIMAL(5,2) NULL,                  -- np. ile godzin przepracował
        Przeczytany BIT DEFAULT 0,
        DataUtworzenia DATETIME DEFAULT GETDATE()
    );

    CREATE INDEX IX_HR_Alerty_Data ON HR_Alerty(Data);
    CREATE INDEX IX_HR_Alerty_Przeczytany ON HR_Alerty(Przeczytany);
END
GO

-- =============================================
-- TABELA 9: NADGODZINY (osobno od bilansu)
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'HR_Nadgodziny')
BEGIN
    CREATE TABLE HR_Nadgodziny (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        PracownikId INT NOT NULL,
        PracownikImie NVARCHAR(100),
        PracownikNazwisko NVARCHAR(100),
        PracownikDzial NVARCHAR(100),
        Data DATE NOT NULL,
        GodzinyNadliczbowe DECIMAL(5,2) NOT NULL,
        TypNadgodzin NVARCHAR(20) DEFAULT 'ZWYKLE', -- 'ZWYKLE', 'NOCNE', 'SWIATECZNE'
        Rozliczone BIT DEFAULT 0,                   -- Czy wypłacone/odebrane
        SposobRozliczenia NVARCHAR(20) NULL,        -- 'WYPLATA', 'ODBIÓR'
        DataRozliczenia DATE NULL,
        Uwagi NVARCHAR(500),
        CONSTRAINT UQ_Nadgodziny UNIQUE (PracownikId, Data, TypNadgodzin)
    );

    CREATE INDEX IX_HR_Nadgodziny_Pracownik ON HR_Nadgodziny(PracownikId);
    CREATE INDEX IX_HR_Nadgodziny_Data ON HR_Nadgodziny(Data);
END
GO

-- =============================================
-- WIDOKI POMOCNICZE
-- =============================================

-- Widok: Podsumowanie bilansu pracownika
IF EXISTS (SELECT * FROM sys.views WHERE name = 'V_HR_BilansPodsumowanie')
    DROP VIEW V_HR_BilansPodsumowanie;
GO

CREATE VIEW V_HR_BilansPodsumowanie AS
SELECT 
    PracownikId,
    PracownikImie,
    PracownikNazwisko,
    SUM(CASE WHEN Roznica > 0 AND Odebrane = 0 THEN Roznica ELSE 0 END) AS GodzinyDoOdebrania,
    SUM(CASE WHEN Roznica < 0 AND Odebrane = 0 THEN ABS(Roznica) ELSE 0 END) AS GodzinyDoOdpracowania,
    SUM(CASE WHEN Odebrane = 0 THEN Roznica ELSE 0 END) AS BilansNetto,
    COUNT(*) AS IloscWpisow
FROM HR_BilansGodzin
GROUP BY PracownikId, PracownikImie, PracownikNazwisko;
GO

-- Widok: Nieodczytane alerty
IF EXISTS (SELECT * FROM sys.views WHERE name = 'V_HR_AlertyNieodczytane')
    DROP VIEW V_HR_AlertyNieodczytane;
GO

CREATE VIEW V_HR_AlertyNieodczytane AS
SELECT *
FROM HR_Alerty
WHERE Przeczytany = 0
  AND Data >= DATEADD(DAY, -7, GETDATE());  -- Ostatnie 7 dni
GO

-- Widok: Urlopy do zatwierdzenia
IF EXISTS (SELECT * FROM sys.views WHERE name = 'V_HR_UrlopydoZatwierdzenia')
    DROP VIEW V_HR_UrlopydoZatwierdzenia;
GO

CREATE VIEW V_HR_UrlopydoZatwierdzenia AS
SELECT 
    n.*,
    t.Nazwa AS TypNazwa,
    t.Kolor
FROM HR_Nieobecnosci n
INNER JOIN HR_TypyNieobecnosci t ON n.TypNieobecnosciId = t.Id
WHERE n.Status = 'Oczekuje';
GO

PRINT '=== TABELE MODUŁU KADROWEGO UTWORZONE POMYŚLNIE ==='
PRINT 'Utworzono tabele:'
PRINT '  - HR_TypyNieobecnosci'
PRINT '  - HR_Nieobecnosci'
PRINT '  - HR_UstawieniaDzialow'
PRINT '  - HR_Przerwy'
PRINT '  - HR_BilansGodzin'
PRINT '  - HR_Korekty'
PRINT '  - HR_Spoznienia'
PRINT '  - HR_Alerty'
PRINT '  - HR_Nadgodziny'
PRINT ''
PRINT 'Utworzono widoki:'
PRINT '  - V_HR_BilansPodsumowanie'
PRINT '  - V_HR_AlertyNieodczytane'
PRINT '  - V_HR_UrlopydoZatwierdzenia'
GO
