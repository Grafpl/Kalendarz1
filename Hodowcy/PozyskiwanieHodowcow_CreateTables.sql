-- ============================================================
-- Pozyskiwanie Hodowców - tworzenie bazy, tabel i import danych
-- Serwer: 192.168.0.109
-- ============================================================

-- 1. Utwórz bazę danych jeśli nie istnieje
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'PozyskiwanieDB')
BEGIN
    CREATE DATABASE PozyskiwanieDB;
END
GO

USE PozyskiwanieDB;
GO

-- 2. Utwórz tabelę Hodowcy
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Hodowcy')
BEGIN
    CREATE TABLE Hodowcy (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Dostawca NVARCHAR(200),
        Towar NVARCHAR(50),
        Ulica NVARCHAR(200),
        KodPocztowy NVARCHAR(10),
        Miejscowosc NVARCHAR(100),
        KM DECIMAL(6,1),
        Tel1 NVARCHAR(50),
        Tel2 NVARCHAR(50),
        Tel3 NVARCHAR(50),
        Status NVARCHAR(30) DEFAULT N'Nowy',
        Kontrakt NVARCHAR(200),
        Notatka NVARCHAR(500),
        PrzypisanyDo NVARCHAR(50),
        DataOstatniegoKontaktu DATETIME,
        DataNastepnegoKontaktu DATETIME,
        DataDodania DATETIME DEFAULT GETDATE(),
        Aktywny BIT DEFAULT 1
    );

    CREATE INDEX IX_Hodowcy_Status ON Hodowcy(Status);
    CREATE INDEX IX_Hodowcy_Towar ON Hodowcy(Towar);
    CREATE INDEX IX_Hodowcy_PrzypisanyDo ON Hodowcy(PrzypisanyDo);
    CREATE INDEX IX_Hodowcy_Miejscowosc ON Hodowcy(Miejscowosc);
END
GO

-- 3. Utwórz tabelę Hodowcy_Aktywnosci
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Hodowcy_Aktywnosci')
BEGIN
    CREATE TABLE Hodowcy_Aktywnosci (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        HodowcaId INT FOREIGN KEY REFERENCES Hodowcy(Id),
        TypAktywnosci NVARCHAR(30),
        Tresc NVARCHAR(MAX),
        WynikTelefonu NVARCHAR(50),
        StatusPrzed NVARCHAR(30),
        StatusPo NVARCHAR(30),
        UzytkownikId NVARCHAR(50),
        UzytkownikNazwa NVARCHAR(100),
        DataUtworzenia DATETIME DEFAULT GETDATE()
    );

    CREATE INDEX IX_Aktywnosci_HodowcaId ON Hodowcy_Aktywnosci(HodowcaId);
    CREATE INDEX IX_Aktywnosci_Data ON Hodowcy_Aktywnosci(DataUtworzenia DESC);
    CREATE INDEX IX_Aktywnosci_Typ ON Hodowcy_Aktywnosci(TypAktywnosci);
END
GO

-- 4. Import danych z Excel (1874 hodowców)
-- Wygenerowane automatycznie z pliku "Baza Hodowców Asia 2.xlsx"
-- Uruchom osobno plik _inserts.sql po utworzeniu tabel
PRINT N'Tabele utworzone pomyślnie. Uruchom _inserts.sql aby zaimportować dane hodowców.';
GO
