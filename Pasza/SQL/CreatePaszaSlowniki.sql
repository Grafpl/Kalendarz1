-- ═════════════════════════════════════════════════════════════════
-- LibraNet — słowniki: ręcznie kuratowane paszarnie + towary
-- ComboBoxy w Kreatorze ładują się WYŁĄCZNIE z tych tabel.
-- Wykonać na 192.168.0.109 / LibraNet po CreatePaszaTables.sql.
-- ═════════════════════════════════════════════════════════════════

USE LibraNet;
GO

-- ─── PASZARNIE: lista dostawców paszy (Sergiusz wybiera ręcznie z STContractors) ───
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'PaszaPaszarnie')
BEGIN
    CREATE TABLE dbo.PaszaPaszarnie
    (
        Id              INT IDENTITY(1,1) PRIMARY KEY,
        KhKod           NVARCHAR(50)  NOT NULL UNIQUE,   -- Symfonia STContractors.Shortcut
        Nazwa           NVARCHAR(200) NOT NULL,          -- snapshot z Symfonii
        NIP             NVARCHAR(20)  NULL,
        Kolejnosc       INT           NOT NULL DEFAULT 0,
        Aktywny         BIT           NOT NULL DEFAULT 1,
        Notatki         NVARCHAR(500) NULL,
        UtworzonoPrzez  NVARCHAR(50)  NULL,
        UtworzonoKiedy  DATETIME      NOT NULL DEFAULT GETDATE()
    );

    CREATE INDEX IX_PaszaPaszarnie_Aktywny ON dbo.PaszaPaszarnie (Aktywny, Kolejnosc, Nazwa);
    PRINT '+ Utworzono PaszaPaszarnie';
END
ELSE
    PRINT '. PaszaPaszarnie juz istnieje';
GO

-- ─── TOWARY: kuratowana lista pasz (Sergiusz wybiera z HM.TW z katalogu Pasza) ───
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'PaszaTowary')
BEGIN
    CREATE TABLE dbo.PaszaTowary
    (
        Id              INT IDENTITY(1,1) PRIMARY KEY,
        TowarKod        NVARCHAR(50)  NOT NULL UNIQUE,   -- HM.TW.kod
        TowarNazwa      NVARCHAR(200) NOT NULL,
        Jm              NVARCHAR(10)  NOT NULL DEFAULT 't',
        KatalogId       INT           NULL,              -- HM.TW.katalog snapshot
        KatalogNazwa    NVARCHAR(100) NULL,
        Kolejnosc       INT           NOT NULL DEFAULT 0,
        Aktywny         BIT           NOT NULL DEFAULT 1,
        Notatki         NVARCHAR(500) NULL,
        UtworzonoPrzez  NVARCHAR(50)  NULL,
        UtworzonoKiedy  DATETIME      NOT NULL DEFAULT GETDATE()
    );

    CREATE INDEX IX_PaszaTowary_Aktywny ON dbo.PaszaTowary (Aktywny, Kolejnosc, TowarNazwa);
    PRINT '+ Utworzono PaszaTowary';
END
ELSE
    PRINT '. PaszaTowary juz istnieje';
GO

PRINT '';
PRINT '═══ Slowniki gotowe ═══';
PRINT 'PaszaPaszarnie   — kuratowane paszarnie (ComboBox w Kreatorze)';
PRINT 'PaszaTowary      — kuratowane towary pasz (ComboBox w Kreatorze)';
GO
