-- =====================================================
-- KONFIGURACJA SMSAPI.pl - PRONOVA
-- Skrypt do uruchomienia na serwerze 192.168.0.109 (LibraNet)
-- =====================================================

USE [LibraNet]
GO

-- =====================================================
-- 1. Utwórz tabelę konfiguracji jeśli nie istnieje
-- =====================================================

IF NOT EXISTS (SELECT * FROM sys.objects WHERE name='SmsApiKonfiguracja' AND type='U')
BEGIN
    CREATE TABLE dbo.SmsApiKonfiguracja (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        ApiToken NVARCHAR(200) NOT NULL,
        NadawcaNazwa NVARCHAR(11) NULL,
        Aktywny BIT NOT NULL DEFAULT 1,
        TestMode BIT NOT NULL DEFAULT 0,
        DataUtworzenia DATETIME NOT NULL DEFAULT GETDATE(),
        DataModyfikacji DATETIME NULL
    );
    PRINT 'Tabela SmsApiKonfiguracja utworzona.'
END
GO

-- =====================================================
-- 2. Wstaw lub zaktualizuj konfigurację z tokenem API
-- =====================================================

-- Usuń poprzednią konfigurację (jeśli istnieje)
DELETE FROM dbo.SmsApiKonfiguracja;

-- Wstaw nową konfigurację z tokenem
INSERT INTO dbo.SmsApiKonfiguracja (ApiToken, NadawcaNazwa, Aktywny, TestMode)
VALUES (
    'jyg1x0FcHQd4n4IWUJWkTpt8w5LMFoDzyPDQNeao',  -- Token API z SMSAPI.pl
    'PRONOVA',                                     -- Nazwa nadawcy (max 11 znaków)
    1,                                             -- Aktywny = TAK
    1                                              -- TestMode = TAK (najpierw testuj!)
);

PRINT 'Konfiguracja SMSAPI.pl dodana.'
PRINT ''
PRINT '=========================================='
PRINT 'UWAGA: TestMode = 1 (tryb testowy)'
PRINT 'SMS-y nie będą wysyłane, tylko logowane!'
PRINT ''
PRINT 'Po przetestowaniu uruchom:'
PRINT 'UPDATE dbo.SmsApiKonfiguracja SET TestMode = 0'
PRINT '=========================================='
GO

-- =====================================================
-- 3. Utwórz tabelę kontaktów operatorów (telefony)
-- =====================================================

IF NOT EXISTS (SELECT * FROM sys.objects WHERE name='OperatorzyKontakt' AND type='U')
BEGIN
    CREATE TABLE dbo.OperatorzyKontakt (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        OperatorID INT NOT NULL,
        Telefon NVARCHAR(20) NULL,
        Email NVARCHAR(100) NULL,
        Stanowisko NVARCHAR(100) NULL,
        DataUtworzenia DATETIME NOT NULL DEFAULT GETDATE(),
        DataModyfikacji DATETIME NULL,
        CONSTRAINT FK_OperatorzyKontakt_Operator FOREIGN KEY (OperatorID)
            REFERENCES dbo.operators(ID)
    );

    CREATE UNIQUE INDEX IX_OperatorzyKontakt_OperatorID ON dbo.OperatorzyKontakt(OperatorID);
    PRINT 'Tabela OperatorzyKontakt utworzona.'
END
GO

-- =====================================================
-- 4. Utwórz tabelę historii SMS
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
        Status NVARCHAR(20) NOT NULL DEFAULT 'Wyslany',
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
-- 5. Sprawdź aktualną konfigurację
-- =====================================================

PRINT ''
PRINT '=========================================='
PRINT 'AKTUALNA KONFIGURACJA SMSAPI:'
PRINT '=========================================='
SELECT
    Id,
    LEFT(ApiToken, 10) + '...' AS [Token (początek)],
    NadawcaNazwa AS [Nadawca],
    CASE WHEN Aktywny = 1 THEN 'TAK' ELSE 'NIE' END AS [Aktywny],
    CASE WHEN TestMode = 1 THEN 'TAK (nie wysyła SMS!)' ELSE 'NIE (wysyła SMS!)' END AS [Tryb testowy]
FROM dbo.SmsApiKonfiguracja
GO

-- =====================================================
-- 6. Pokaż dostępnych operatorów (handlowców)
-- =====================================================

PRINT ''
PRINT '=========================================='
PRINT 'OPERATORZY DO SKONFIGUROWANIA (telefony):'
PRINT '=========================================='
SELECT TOP 30
    o.ID,
    o.Name AS [Nazwa operatora],
    ISNULL(k.Telefon, '-- BRAK --') AS [Telefon],
    ISNULL(k.Email, '') AS [Email]
FROM dbo.operators o
LEFT JOIN dbo.OperatorzyKontakt k ON o.ID = k.OperatorID
WHERE o.Name IS NOT NULL AND o.Name != ''
ORDER BY o.Name
GO

PRINT ''
PRINT '=========================================='
PRINT 'KOLEJNE KROKI:'
PRINT '=========================================='
PRINT '1. Dodaj telefony handlowców (przykład):'
PRINT ''
PRINT '   INSERT INTO dbo.OperatorzyKontakt (OperatorID, Telefon)'
PRINT '   SELECT ID, ''+48123456789'''
PRINT '   FROM dbo.operators'
PRINT '   WHERE Name = ''Jan Kowalski'''
PRINT ''
PRINT '2. Po dodaniu telefonów, wyłącz tryb testowy:'
PRINT ''
PRINT '   UPDATE dbo.SmsApiKonfiguracja SET TestMode = 0'
PRINT ''
PRINT '=========================================='
GO
