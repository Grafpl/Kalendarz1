-- =====================================================
-- System SMS dla handlowców - SMSAPI.pl
-- Tabele: Historia SMS, Konfiguracja API
-- =====================================================

USE [LibraNet]
GO

-- =====================================================
-- 1. Tabela konfiguracji SMSAPI.pl
-- =====================================================

IF NOT EXISTS (SELECT * FROM sys.objects WHERE name='SmsApiKonfiguracja' AND type='U')
BEGIN
    CREATE TABLE dbo.SmsApiKonfiguracja (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        ApiToken NVARCHAR(200) NOT NULL,           -- Token z panelu SMSAPI.pl
        NadawcaNazwa NVARCHAR(11) NULL,            -- Nazwa nadawcy (max 11 znaków)
        Aktywny BIT NOT NULL DEFAULT 1,            -- Czy wysyłanie aktywne
        TestMode BIT NOT NULL DEFAULT 0,           -- Tryb testowy (nie wysyła SMS)
        DataUtworzenia DATETIME NOT NULL DEFAULT GETDATE(),
        DataModyfikacji DATETIME NULL
    );

    -- Wstaw domyślny rekord (UZUPEŁNIJ TOKEN!)
    INSERT INTO dbo.SmsApiKonfiguracja (ApiToken, NadawcaNazwa, Aktywny, TestMode)
    VALUES ('TUTAJ_WKLEJ_TOKEN_Z_SMSAPI', 'PRONOVA', 1, 1);
    -- TestMode = 1 oznacza tryb testowy (SMS nie będą wysyłane, tylko logowane)
    -- Zmień na TestMode = 0 żeby włączyć wysyłanie

    PRINT 'Tabela SmsApiKonfiguracja utworzona.'
    PRINT 'WAŻNE: Uzupełnij ApiToken tokenem z panelu SMSAPI.pl!'
END
GO

-- =====================================================
-- 2. Tabela historii SMS-ów wysyłanych do handlowców
-- =====================================================

IF NOT EXISTS (SELECT * FROM sys.objects WHERE name='SmsSprzedazHistoria' AND type='U')
BEGIN
    CREATE TABLE dbo.SmsSprzedazHistoria (
        Id BIGINT IDENTITY(1,1) PRIMARY KEY,
        ZamowienieId INT NOT NULL,
        KlientId INT NOT NULL,
        KlientNazwa NVARCHAR(200) NULL,
        Handlowiec NVARCHAR(100) NULL,
        TelefonHandlowca NVARCHAR(20) NULL,
        TrescSms NVARCHAR(500) NULL,
        IloscKg DECIMAL(18,2) NULL,
        CzasWyjazdu DATETIME NULL,
        DataWyslania DATETIME NOT NULL DEFAULT GETDATE(),
        KtoWyslal NVARCHAR(100) NULL,
        Status NVARCHAR(20) NOT NULL DEFAULT 'Wyslany',  -- Wyslany, Test, Kopiowany, Blad
        BladOpis NVARCHAR(500) NULL
    );

    CREATE INDEX IX_SmsSprzedaz_ZamowienieId ON dbo.SmsSprzedazHistoria(ZamowienieId);
    CREATE INDEX IX_SmsSprzedaz_Handlowiec ON dbo.SmsSprzedazHistoria(Handlowiec);
    CREATE INDEX IX_SmsSprzedaz_DataWyslania ON dbo.SmsSprzedazHistoria(DataWyslania);
    CREATE INDEX IX_SmsSprzedaz_Status ON dbo.SmsSprzedazHistoria(Status);

    PRINT 'Tabela SmsSprzedazHistoria utworzona.'
END
GO

-- =====================================================
-- 3. Tabela kontaktów operatorów (telefony handlowców)
-- =====================================================

IF NOT EXISTS (SELECT * FROM sys.objects WHERE name='OperatorzyKontakt' AND type='U')
BEGIN
    CREATE TABLE dbo.OperatorzyKontakt (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        OperatorID INT NOT NULL,                   -- Powiązanie z dbo.operators.ID
        Telefon NVARCHAR(20) NULL,                 -- Numer telefonu (+48123456789)
        Email NVARCHAR(100) NULL,
        DataUtworzenia DATETIME NOT NULL DEFAULT GETDATE(),
        CONSTRAINT FK_OperatorzyKontakt_Operator FOREIGN KEY (OperatorID)
            REFERENCES dbo.operators(ID)
    );

    CREATE UNIQUE INDEX IX_OperatorzyKontakt_OperatorID ON dbo.OperatorzyKontakt(OperatorID);

    PRINT 'Tabela OperatorzyKontakt utworzona.'
    PRINT 'WAŻNE: Uzupełnij numery telefonów handlowców!'
END
GO

-- =====================================================
-- 4. Widok raportowy SMS
-- =====================================================

IF EXISTS (SELECT * FROM sys.views WHERE name='vw_SmsSprzedazRaport')
    DROP VIEW dbo.vw_SmsSprzedazRaport
GO

CREATE VIEW dbo.vw_SmsSprzedazRaport AS
SELECT
    h.Handlowiec,
    CAST(h.DataWyslania AS DATE) AS Data,
    COUNT(*) AS LiczbaSms,
    SUM(h.IloscKg) AS SumaKg,
    COUNT(DISTINCT h.KlientId) AS LiczbaKlientow,
    SUM(CASE WHEN h.Status = 'Wyslany' THEN 1 ELSE 0 END) AS Wyslane,
    SUM(CASE WHEN h.Status = 'Test' THEN 1 ELSE 0 END) AS Testowe,
    SUM(CASE WHEN h.Status = 'Kopiowany' THEN 1 ELSE 0 END) AS Kopiowane,
    SUM(CASE WHEN h.Status = 'Blad' THEN 1 ELSE 0 END) AS Bledy
FROM dbo.SmsSprzedazHistoria h
GROUP BY h.Handlowiec, CAST(h.DataWyslania AS DATE)
GO

PRINT 'Widok vw_SmsSprzedazRaport utworzony.'
GO

-- =====================================================
-- 5. Procedura statystyk SMS
-- =====================================================

IF EXISTS (SELECT * FROM sys.procedures WHERE name='sp_SmsSprzedazStatystyki')
    DROP PROCEDURE dbo.sp_SmsSprzedazStatystyki
GO

CREATE PROCEDURE dbo.sp_SmsSprzedazStatystyki
    @DataOd DATE = NULL,
    @DataDo DATE = NULL,
    @Handlowiec NVARCHAR(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF @DataOd IS NULL SET @DataOd = DATEADD(DAY, -30, GETDATE())
    IF @DataDo IS NULL SET @DataDo = GETDATE()

    SELECT
        h.Handlowiec,
        COUNT(*) AS LiczbaSms,
        SUM(h.IloscKg) AS SumaKg,
        COUNT(DISTINCT h.KlientId) AS LiczbaKlientow,
        COUNT(DISTINCT CAST(h.DataWyslania AS DATE)) AS LiczbaDni,
        MIN(h.DataWyslania) AS PierwszySms,
        MAX(h.DataWyslania) AS OstatniSms,
        SUM(CASE WHEN h.Status = 'Wyslany' THEN 1 ELSE 0 END) AS Wyslane,
        SUM(CASE WHEN h.Status = 'Blad' THEN 1 ELSE 0 END) AS Bledy
    FROM dbo.SmsSprzedazHistoria h
    WHERE h.DataWyslania >= @DataOd
      AND h.DataWyslania <= DATEADD(DAY, 1, @DataDo)
      AND (@Handlowiec IS NULL OR h.Handlowiec = @Handlowiec)
    GROUP BY h.Handlowiec
    ORDER BY SumaKg DESC
END
GO

PRINT 'Procedura sp_SmsSprzedazStatystyki utworzona.'
GO

-- =====================================================
-- 6. Przykładowe zapytania konfiguracyjne
-- =====================================================

-- Sprawdź aktualną konfigurację SMSAPI:
-- SELECT * FROM dbo.SmsApiKonfiguracja

-- Włącz wysyłanie SMS (wyłącz tryb testowy):
-- UPDATE dbo.SmsApiKonfiguracja SET TestMode = 0 WHERE Id = 1

-- Wyłącz wysyłanie SMS:
-- UPDATE dbo.SmsApiKonfiguracja SET Aktywny = 0 WHERE Id = 1

-- Dodaj telefon handlowca (przykład dla operatora ID=5):
-- INSERT INTO dbo.OperatorzyKontakt (OperatorID, Telefon, Email)
-- VALUES (5, '+48123456789', 'handlowiec@firma.pl')

-- Sprawdź wysłane SMS dzisiaj:
-- SELECT * FROM dbo.SmsSprzedazHistoria WHERE CAST(DataWyslania AS DATE) = CAST(GETDATE() AS DATE)

-- Raport dzienny:
-- SELECT * FROM dbo.vw_SmsSprzedazRaport WHERE Data = CAST(GETDATE() AS DATE)

PRINT ''
PRINT '============================================='
PRINT 'KONFIGURACJA SMSAPI.pl - INSTRUKCJA:'
PRINT '============================================='
PRINT '1. Zaloguj się do panelu SMSAPI.pl'
PRINT '2. Przejdź do: Ustawienia -> Tokeny API'
PRINT '3. Utwórz nowy token z uprawnieniami SMS'
PRINT '4. Skopiuj token i wykonaj:'
PRINT ''
PRINT '   UPDATE dbo.SmsApiKonfiguracja'
PRINT '   SET ApiToken = ''TWOJ_TOKEN_API'''
PRINT '   WHERE Id = 1'
PRINT ''
PRINT '5. Dodaj telefony handlowców:'
PRINT ''
PRINT '   INSERT INTO dbo.OperatorzyKontakt (OperatorID, Telefon)'
PRINT '   SELECT ID, ''+48XXXXXXXXX'' FROM dbo.operators WHERE Name = ''Jan Kowalski'''
PRINT ''
PRINT '6. Włącz wysyłanie (wyłącz tryb testowy):'
PRINT ''
PRINT '   UPDATE dbo.SmsApiKonfiguracja SET TestMode = 0'
PRINT ''
PRINT '============================================='
GO
