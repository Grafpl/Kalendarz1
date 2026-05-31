-- ════════════════════════════════════════════════════════════════════════════
-- KONTRAKTY HODOWCÓW — rozszerzenie wersji o dodatkowe warunki (część 3)
-- Dodaje kolumny do dbo.KontraktyWersje. Idempotentny (IF COL_LENGTH ... IS NULL).
-- Target: LibraNet (192.168.0.109). Bezpieczny do wielokrotnego uruchomienia.
-- ════════════════════════════════════════════════════════════════════════════
USE LibraNet;
GO

-- Cena / rozliczenie
IF COL_LENGTH('dbo.KontraktyWersje','CenaMin') IS NULL
    ALTER TABLE dbo.KontraktyWersje ADD CenaMin DECIMAL(8,4) NULL;
IF COL_LENGTH('dbo.KontraktyWersje','CenaMax') IS NULL
    ALTER TABLE dbo.KontraktyWersje ADD CenaMax DECIMAL(8,4) NULL;
IF COL_LENGTH('dbo.KontraktyWersje','Indeksacja') IS NULL
    ALTER TABLE dbo.KontraktyWersje ADD Indeksacja VARCHAR(40) NULL;  -- STALA/RYNKOWA/GUS/MINISTERIALNA

-- Dostawa / logistyka
IF COL_LENGTH('dbo.KontraktyWersje','CzestotliwoscDostaw') IS NULL
    ALTER TABLE dbo.KontraktyWersje ADD CzestotliwoscDostaw VARCHAR(40) NULL;
IF COL_LENGTH('dbo.KontraktyWersje','MaxIloscSzt') IS NULL
    ALTER TABLE dbo.KontraktyWersje ADD MaxIloscSzt INT NULL;
IF COL_LENGTH('dbo.KontraktyWersje','TransportCzyj') IS NULL
    ALTER TABLE dbo.KontraktyWersje ADD TransportCzyj VARCHAR(20) NULL;  -- NASZ/HODOWCY/WSPOLNY
IF COL_LENGTH('dbo.KontraktyWersje','PaszaOdNas') IS NULL
    ALTER TABLE dbo.KontraktyWersje ADD PaszaOdNas BIT NOT NULL DEFAULT 0;
IF COL_LENGTH('dbo.KontraktyWersje','PisklakiOdNas') IS NULL
    ALTER TABLE dbo.KontraktyWersje ADD PisklakiOdNas BIT NOT NULL DEFAULT 0;

-- Klauzule / zabezpieczenia
IF COL_LENGTH('dbo.KontraktyWersje','KaraUmownaZl') IS NULL
    ALTER TABLE dbo.KontraktyWersje ADD KaraUmownaZl DECIMAL(10,2) NULL;
IF COL_LENGTH('dbo.KontraktyWersje','AutoOdnowienie') IS NULL
    ALTER TABLE dbo.KontraktyWersje ADD AutoOdnowienie BIT NOT NULL DEFAULT 0;
IF COL_LENGTH('dbo.KontraktyWersje','PrawoPierwokupu') IS NULL
    ALTER TABLE dbo.KontraktyWersje ADD PrawoPierwokupu BIT NOT NULL DEFAULT 0;

-- Kontakt po stronie hodowcy
IF COL_LENGTH('dbo.KontraktyWersje','OsobaKontaktowa') IS NULL
    ALTER TABLE dbo.KontraktyWersje ADD OsobaKontaktowa NVARCHAR(120) NULL;
IF COL_LENGTH('dbo.KontraktyWersje','TelefonKontaktowy') IS NULL
    ALTER TABLE dbo.KontraktyWersje ADD TelefonKontaktowy VARCHAR(30) NULL;
GO

PRINT '✅ KontraktyWersje rozszerzone o dodatkowe warunki (część 3).';
GO
