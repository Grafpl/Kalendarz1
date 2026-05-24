-- ════════════════════════════════════════════════════════════════════
-- TRACEABILITY — śledzenie palet wyrobu (lot number) + recall
-- Baza: LibraNet (192.168.0.109)
-- Pomysł #3 z BAZA_WIEDZY/30_POMYSLY
-- Reverse trace (lot → hodowca) + recall management.
-- ════════════════════════════════════════════════════════════════════

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'PaletaWyrob')
BEGIN
    CREATE TABLE dbo.PaletaWyrob (
        Id              BIGINT IDENTITY(1,1) PRIMARY KEY,
        LotNumber       NVARCHAR(50)  NOT NULL UNIQUE,   -- 'PIO-2026-05-24-001'
        DataProdukcji   DATE          NOT NULL,
        Smiana          NVARCHAR(20)  NULL,
        Linia           NVARCHAR(20)  NULL,
        OperatorId      NVARCHAR(50)  NULL,
        KodTowaru       NVARCHAR(50)  NULL,
        NazwaTowaru     NVARCHAR(200) NULL,
        LiczbaSztuk     INT           NULL,
        WagaKg          DECIMAL(10,2) NOT NULL DEFAULT 0,
        DataWaznosci    DATE          NULL,
        Status          NVARCHAR(20)  NOT NULL DEFAULT 'NA_MAGAZYNIE',
        -- NA_MAGAZYNIE / WYSLANO / ZWROT / WYCOFANO / ZUTYLIZOWANO
        DataUtworzenia  DATETIME      NOT NULL DEFAULT GETDATE()
    );
    CREATE INDEX IX_PalW_Lot ON dbo.PaletaWyrob(LotNumber);
    CREATE INDEX IX_PalW_Data ON dbo.PaletaWyrob(DataProdukcji);
    CREATE INDEX IX_PalW_Status ON dbo.PaletaWyrob(Status, KodTowaru);
    PRINT 'Utworzono PaletaWyrob';
END
ELSE PRINT 'PaletaWyrob już istnieje';
GO

-- Skład palety wyrobu — z których partii surowych (N:M)
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'PaletaWyrobSklad')
BEGIN
    CREATE TABLE dbo.PaletaWyrobSklad (
        Id              BIGINT IDENTITY(1,1) PRIMARY KEY,
        PaletaWyrobId   BIGINT NOT NULL FOREIGN KEY REFERENCES dbo.PaletaWyrob(Id),
        Partia          VARCHAR(50) NOT NULL,        -- listapartii / PartiaDostawca.Partia
        CustomerID      VARCHAR(50) NULL,            -- hodowca (snapshot)
        CustomerName    NVARCHAR(200) NULL,
        WagaKgUdzial    DECIMAL(10,2) NULL,
        Notatki         NVARCHAR(300) NULL
    );
    CREATE INDEX IX_PalWS_Paleta ON dbo.PaletaWyrobSklad(PaletaWyrobId);
    CREATE INDEX IX_PalWS_Partia ON dbo.PaletaWyrobSklad(Partia);
    PRINT 'Utworzono PaletaWyrobSklad';
END
ELSE PRINT 'PaletaWyrobSklad już istnieje';
GO

-- Wydania palet do klientów (link do dokumentu sprzedaży HANDEL)
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'DokumentPaletWydania')
BEGIN
    CREATE TABLE dbo.DokumentPaletWydania (
        Id              BIGINT IDENTITY(1,1) PRIMARY KEY,
        PaletaWyrobId   BIGINT NOT NULL FOREIGN KEY REFERENCES dbo.PaletaWyrob(Id),
        DokumentMGId    BIGINT NULL,                 -- HANDEL HM.MG.id (sWZ)
        NumerDokumentu  NVARCHAR(50) NULL,
        KlientId        INT NULL,
        KlientNazwa     NVARCHAR(200) NULL,
        WagaKgWydana    DECIMAL(10,2) NULL,
        DataWydania     DATETIME NOT NULL DEFAULT GETDATE()
    );
    CREATE INDEX IX_DPW_Paleta ON dbo.DokumentPaletWydania(PaletaWyrobId);
    CREATE INDEX IX_DPW_Klient ON dbo.DokumentPaletWydania(KlientId);
    PRINT 'Utworzono DokumentPaletWydania';
END
ELSE PRINT 'DokumentPaletWydania już istnieje';
GO

-- Recall (wycofanie)
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Recall')
BEGIN
    CREATE TABLE dbo.Recall (
        Id              BIGINT IDENTITY(1,1) PRIMARY KEY,
        RecallNumber    NVARCHAR(50) NOT NULL UNIQUE,    -- 'REC-2026-001'
        DataInicjacji   DATETIME NOT NULL DEFAULT GETDATE(),
        InicjowanyPrzez NVARCHAR(50) NULL,
        Powod           NVARCHAR(500) NULL,
        Kategoria       NVARCHAR(50) NOT NULL DEFAULT 'JAKOSC',
        -- BEZPIECZENSTWO / ALERGENY / MIKROBIOLOGIA / JAKOSC / INNE
        TypZakresu      NVARCHAR(20) NOT NULL,           -- PARTIA / HODOWCA / DATA / TOWAR
        ZakresIdent     NVARCHAR(200) NULL,
        LiczbaPalet     INT NULL,
        LiczbaKlientow  INT NULL,
        WagaKg          DECIMAL(12,2) NULL,
        Status          NVARCHAR(20) NOT NULL DEFAULT 'OTWARTY',  -- OTWARTY / ZAMKNIETY
        DataZamkniecia  DATETIME NULL,
        Notatki         NVARCHAR(MAX) NULL
    );
    PRINT 'Utworzono Recall';
END
ELSE PRINT 'Recall już istnieje';
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'RecallPalety')
BEGIN
    CREATE TABLE dbo.RecallPalety (
        Id              BIGINT IDENTITY(1,1) PRIMARY KEY,
        RecallId        BIGINT NOT NULL FOREIGN KEY REFERENCES dbo.Recall(Id),
        PaletaWyrobId   BIGINT NOT NULL FOREIGN KEY REFERENCES dbo.PaletaWyrob(Id),
        Status          NVARCHAR(20) NOT NULL DEFAULT 'OBJETA',
        -- OBJETA / KONTAKT_KLIENT / ODEBRANA / ZUTYLIZOWANA / NIEODNALEZIONA
        KlientPowiadomiony BIT NOT NULL DEFAULT 0,
        Notatki         NVARCHAR(500) NULL
    );
    CREATE INDEX IX_RP_Recall ON dbo.RecallPalety(RecallId);
    PRINT 'Utworzono RecallPalety';
END
ELSE PRINT 'RecallPalety już istnieje';
GO
