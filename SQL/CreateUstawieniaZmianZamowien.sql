-- Skrypt informacyjny - tabele tworzone automatycznie przez ZmianyZamowienSettingsService.EnsureTablesSync()
-- Baza: LibraNet (192.168.0.109)

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[UstawieniaZmianZamowien]') AND type = N'U')
BEGIN
    CREATE TABLE [dbo].[UstawieniaZmianZamowien](
        [GodzinaOdKtorejPowiadamiac] TIME NOT NULL DEFAULT '11:00',
        [GodzinaBlokadyEdycji] TIME NULL,
        [CzyBlokowacEdycjePoGodzinie] BIT NOT NULL DEFAULT 0,
        [KafelkiDocelowe] NVARCHAR(MAX) NOT NULL DEFAULT '',
        [RodzajPowiadomienia] VARCHAR(20) NOT NULL DEFAULT 'MessageBox',
        [MinimalnaZmianaKgDoPowiadomienia] DECIMAL(10,2) NOT NULL DEFAULT 0,
        [CzyWymagacKomentarzaPrzyZmianie] BIT NOT NULL DEFAULT 0,
        [CzyLogowacZmianyDoHistorii] BIT NOT NULL DEFAULT 1,
        [DniTygodniaAktywne] VARCHAR(20) NOT NULL DEFAULT '1,2,3,4,5',
        [ModifiedAt] DATETIME NOT NULL DEFAULT GETDATE(),
        [ModifiedBy] NVARCHAR(100) NULL
    );

    INSERT INTO [dbo].[UstawieniaZmianZamowien] DEFAULT VALUES;
END
ELSE
BEGIN
    -- Migracja: dodaj KafelkiDocelowe jeśli nie istnieje (zastępuje stare kolumny CzyPowiadamiac*)
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('UstawieniaZmianZamowien') AND name = 'KafelkiDocelowe')
        ALTER TABLE [dbo].[UstawieniaZmianZamowien] ADD [KafelkiDocelowe] NVARCHAR(MAX) NOT NULL DEFAULT '';
END
GO

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[UstawieniaZmianZamowien_Wylaczenia]') AND type = N'U')
BEGIN
    CREATE TABLE [dbo].[UstawieniaZmianZamowien_Wylaczenia](
        [Id] INT IDENTITY(1,1) PRIMARY KEY,
        [UserID] NVARCHAR(50) NOT NULL,
        [UserName] NVARCHAR(200) NOT NULL,
        [CzyZwolnionyZPowiadomien] BIT NOT NULL DEFAULT 1,
        [IndywidualnaGodzina] TIME NULL,
        [Powod] NVARCHAR(500) NULL,
        [DodanoPrzez] NVARCHAR(100) NOT NULL DEFAULT 'SYSTEM',
        [DodanoData] DATETIME NOT NULL DEFAULT GETDATE()
    );

    CREATE UNIQUE INDEX [IX_Wylaczenia_UserID]
    ON [dbo].[UstawieniaZmianZamowien_Wylaczenia] ([UserID]);
END
GO
