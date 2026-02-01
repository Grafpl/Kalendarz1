-- ═══════════════════════════════════════════════════════════════════════
-- Kartoteka Odbiorców - Tabele w LibraNet (192.168.0.109)
-- ═══════════════════════════════════════════════════════════════════════

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'KartotekaOdbiorcyDane')
BEGIN
    CREATE TABLE dbo.KartotekaOdbiorcyDane (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        IdSymfonia INT NOT NULL,
        OsobaKontaktowa NVARCHAR(100),
        TelefonKontakt NVARCHAR(50),
        EmailKontakt NVARCHAR(100),
        Asortyment NVARCHAR(500),
        PreferencjePakowania NVARCHAR(200),
        PreferencjeJakosci NVARCHAR(200),
        PreferencjeDostawy NVARCHAR(200),
        PreferowanyDzienDostawy NVARCHAR(100),
        PreferowanaGodzinaDostawy NVARCHAR(50),
        AdresDostawyInny NVARCHAR(300),
        Trasa NVARCHAR(50),
        KategoriaHandlowca CHAR(1) DEFAULT 'C',
        Notatki NVARCHAR(MAX),
        DataUtworzenia DATETIME DEFAULT GETDATE(),
        DataModyfikacji DATETIME,
        ModyfikowalPrzez NVARCHAR(50),
        CONSTRAINT UQ_KartotekaOdbiorcyDane_IdSymfonia UNIQUE (IdSymfonia)
    );
    CREATE INDEX IX_KartotekaOdbiorcyDane_IdSymfonia ON dbo.KartotekaOdbiorcyDane(IdSymfonia);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'KartotekaOdbiorcyKontakty')
BEGIN
    CREATE TABLE dbo.KartotekaOdbiorcyKontakty (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        IdSymfonia INT NOT NULL,
        TypKontaktu NVARCHAR(50) NOT NULL,
        Imie NVARCHAR(50),
        Nazwisko NVARCHAR(50),
        Telefon NVARCHAR(50),
        Email NVARCHAR(100),
        Stanowisko NVARCHAR(100),
        Notatka NVARCHAR(500),
        DataUtworzenia DATETIME DEFAULT GETDATE(),
        DataModyfikacji DATETIME
    );
    CREATE INDEX IX_KartotekaOdbiorcyKontakty_IdSymfonia ON dbo.KartotekaOdbiorcyKontakty(IdSymfonia);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'KartotekaTypyKontaktow')
BEGIN
    CREATE TABLE dbo.KartotekaTypyKontaktow (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Nazwa NVARCHAR(50) NOT NULL,
        Ikona NVARCHAR(10),
        Kolejnosc INT DEFAULT 0
    );
    INSERT INTO dbo.KartotekaTypyKontaktow (Nazwa, Ikona, Kolejnosc) VALUES
    (N'Główny', N'👤', 1),
    (N'Księgowość', N'📊', 2),
    (N'Opakowania', N'📦', 3),
    (N'Właściciel', N'👔', 4),
    (N'Magazyn', N'🏭', 5),
    (N'Inny', N'📋', 99);
END
GO
