-- =============================================
-- Skrypt tworzenia tabel dla Kartoteki Odbiorców
-- Baza: LibraNet (192.168.0.109)
-- =============================================

USE [LibraNet]
GO

-- =============================================
-- Tabela: Odbiorcy - główna kartoteka odbiorców
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Odbiorcy]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Odbiorcy](
        [OdbiorcaID] [int] IDENTITY(1,1) NOT NULL,
        [IdOdbiorcy] [int] NULL,  -- ID z systemu Handel
        [NazwaSkrot] [nvarchar](100) NOT NULL,
        [PelnaNazwa] [nvarchar](250) NOT NULL,
        [NIP] [nvarchar](20) NULL,
        [REGON] [nvarchar](20) NULL,
        [Ulica] [nvarchar](150) NULL,
        [KodPocztowy] [nvarchar](10) NULL,
        [Miejscowosc] [nvarchar](100) NULL,
        [Wojewodztwo] [nvarchar](50) NULL,
        [Kraj] [nvarchar](50) NULL DEFAULT('Polska'),
        
        -- Lokalizacja GPS
        [Szerokosc] [decimal](10, 7) NULL,  -- Latitude
        [Dlugosc] [decimal](10, 7) NULL,    -- Longitude
        [OdlegloscKm] [decimal](8, 2) NULL, -- Odległość od firmy
        
        -- Dane finansowe
        [LimitKredytu] [decimal](18, 2) NULL,
        [AktualnaSaldo] [decimal](18, 2) NULL,
        [TerminPlatnosci] [int] NULL, -- Dni
        [FormaPlatnosci] [nvarchar](50) NULL,
        
        -- Klasyfikacja
        [KategoriaOdbiorcy] [nvarchar](50) NULL, -- A, B, C (wg wartości)
        [TypOdbiorcy] [nvarchar](50) NULL, -- Hurtownia, Sklep, Restauracja, Export
        [StatusAktywny] [bit] NOT NULL DEFAULT(1),
        [RatingKredytowy] [nvarchar](10) NULL, -- AAA, AA, A, B, C
        
        -- Dane systemowe
        [DataUtworzenia] [datetime] NOT NULL DEFAULT(GETDATE()),
        [KtoStworzyl] [int] NOT NULL,
        [DataModyfikacji] [datetime] NULL,
        [KtoZmodyfikowal] [int] NULL,
        [DataOstatniegoZakupu] [datetime] NULL,
        [CzestotliwoscZakupow] [nvarchar](50) NULL, -- Codzienny, Tygodniowy, Miesięczny
        
        CONSTRAINT [PK_Odbiorcy] PRIMARY KEY CLUSTERED ([OdbiorcaID] ASC)
    )
END
GO

-- =============================================
-- Tabela: OdbiorcyKontakty - dane kontaktowe
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[OdbiorcyKontakty]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[OdbiorcyKontakty](
        [KontaktID] [int] IDENTITY(1,1) NOT NULL,
        [OdbiorcaID] [int] NOT NULL,
        [TypKontaktu] [nvarchar](50) NOT NULL, -- Zakupowiec, Księgowość, Pojemniki, Dyrektor, Właściciel
        [Imie] [nvarchar](50) NULL,
        [Nazwisko] [nvarchar](50) NULL,
        [Stanowisko] [nvarchar](100) NULL,
        [Telefon] [nvarchar](20) NULL,
        [TelefonKomorkowy] [nvarchar](20) NULL,
        [Email] [nvarchar](100) NULL,
        [Uwagi] [nvarchar](500) NULL,
        [JestGlownyKontakt] [bit] NOT NULL DEFAULT(0),
        [DataUtworzenia] [datetime] NOT NULL DEFAULT(GETDATE()),
        [KtoStworzyl] [int] NOT NULL,
        
        CONSTRAINT [PK_OdbiorcyKontakty] PRIMARY KEY CLUSTERED ([KontaktID] ASC),
        CONSTRAINT [FK_OdbiorcyKontakty_Odbiorcy] FOREIGN KEY([OdbiorcaID])
            REFERENCES [dbo].[Odbiorcy] ([OdbiorcaID]) ON DELETE CASCADE
    )
END
GO

-- =============================================
-- Tabela: OdbiorcyDaneFinansowe - historia finansowa
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[OdbiorcyDaneFinansowe]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[OdbiorcyDaneFinansowe](
        [DaneFinansoweID] [int] IDENTITY(1,1) NOT NULL,
        [OdbiorcaID] [int] NOT NULL,
        [Rok] [int] NOT NULL,
        [Miesiac] [int] NOT NULL,
        [ObrotNetto] [decimal](18, 2) NULL,
        [ObrotBrutto] [decimal](18, 2) NULL,
        [IloscFaktur] [int] NULL,
        [SredniaCenaKg] [decimal](10, 2) NULL,
        [TonazSprzedana] [decimal](10, 2) NULL,
        [DataAktualizacji] [datetime] NOT NULL DEFAULT(GETDATE()),
        
        CONSTRAINT [PK_OdbiorcyDaneFinansowe] PRIMARY KEY CLUSTERED ([DaneFinansoweID] ASC),
        CONSTRAINT [FK_OdbiorcyDaneFinansowe_Odbiorcy] FOREIGN KEY([OdbiorcaID])
            REFERENCES [dbo].[Odbiorcy] ([OdbiorcaID]) ON DELETE CASCADE
    )
END
GO

-- =============================================
-- Tabela: OdbiorcyTransport - preferencje transportowe
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[OdbiorcyTransport]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[OdbiorcyTransport](
        [TransportID] [int] IDENTITY(1,1) NOT NULL,
        [OdbiorcaID] [int] NOT NULL,
        [TypTransportu] [nvarchar](50) NULL, -- Własny, Wynajęty, Odbiorca
        [KosztTransportuKm] [decimal](8, 2) NULL,
        [MinimalneZamowienie] [decimal](10, 2) NULL, -- kg
        [PreferowaneDniDostawy] [nvarchar](100) NULL, -- Pn, Wt, Śr, Czw, Pt
        [PreferowaneGodzinyOd] [time](7) NULL,
        [PreferowaneGodzinyDo] [time](7) NULL,
        [CzasRozladunku] [int] NULL, -- minuty
        [WymaganyTypPojazdu] [nvarchar](100) NULL,
        [Uwagi] [nvarchar](500) NULL,
        [DataUtworzenia] [datetime] NOT NULL DEFAULT(GETDATE()),

        -- Koszty stałe transportu
        [KosztStalyDostawy] [decimal](10, 2) NULL DEFAULT(0), -- PLN za dostawę
        [KosztKmWDwieStrony] [bit] NULL DEFAULT(1), -- Czy liczyć km w obie strony
        [KosztGodzinyKierowcy] [decimal](8, 2) NULL DEFAULT(50), -- PLN/h
        [SredniPrzebiegLitr] [decimal](5, 2) NULL DEFAULT(25), -- km/l paliwa
        [CenaPaliwaLitr] [decimal](6, 2) NULL DEFAULT(6.50), -- PLN/l
        [MinWartoscDlaDarmowegoTransportu] [decimal](10, 2) NULL, -- PLN
        [ProcentMarzyNaTransport] [decimal](5, 2) NULL DEFAULT(0), -- % doliczany do kosztu

        -- Dane rozliczeniowe
        [CzyDoliczaTransportDoFaktury] [bit] NULL DEFAULT(0),
        [StalaCenaTransportu] [decimal](10, 2) NULL, -- jeśli cena stała zamiast kalkulacji

        CONSTRAINT [PK_OdbiorcyTransport] PRIMARY KEY CLUSTERED ([TransportID] ASC),
        CONSTRAINT [FK_OdbiorcyTransport_Odbiorcy] FOREIGN KEY([OdbiorcaID])
            REFERENCES [dbo].[Odbiorcy] ([OdbiorcaID]) ON DELETE CASCADE
    )
END
GO

-- Dodanie nowych kolumn jeśli tabela istnieje
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[OdbiorcyTransport]') AND type in (N'U'))
BEGIN
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[OdbiorcyTransport]') AND name = 'KosztStalyDostawy')
        ALTER TABLE [dbo].[OdbiorcyTransport] ADD [KosztStalyDostawy] [decimal](10, 2) NULL DEFAULT(0);

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[OdbiorcyTransport]') AND name = 'KosztKmWDwieStrony')
        ALTER TABLE [dbo].[OdbiorcyTransport] ADD [KosztKmWDwieStrony] [bit] NULL DEFAULT(1);

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[OdbiorcyTransport]') AND name = 'KosztGodzinyKierowcy')
        ALTER TABLE [dbo].[OdbiorcyTransport] ADD [KosztGodzinyKierowcy] [decimal](8, 2) NULL DEFAULT(50);

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[OdbiorcyTransport]') AND name = 'SredniPrzebiegLitr')
        ALTER TABLE [dbo].[OdbiorcyTransport] ADD [SredniPrzebiegLitr] [decimal](5, 2) NULL DEFAULT(25);

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[OdbiorcyTransport]') AND name = 'CenaPaliwaLitr')
        ALTER TABLE [dbo].[OdbiorcyTransport] ADD [CenaPaliwaLitr] [decimal](6, 2) NULL DEFAULT(6.50);

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[OdbiorcyTransport]') AND name = 'MinWartoscDlaDarmowegoTransportu')
        ALTER TABLE [dbo].[OdbiorcyTransport] ADD [MinWartoscDlaDarmowegoTransportu] [decimal](10, 2) NULL;

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[OdbiorcyTransport]') AND name = 'ProcentMarzyNaTransport')
        ALTER TABLE [dbo].[OdbiorcyTransport] ADD [ProcentMarzyNaTransport] [decimal](5, 2) NULL DEFAULT(0);

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[OdbiorcyTransport]') AND name = 'CzyDoliczaTransportDoFaktury')
        ALTER TABLE [dbo].[OdbiorcyTransport] ADD [CzyDoliczaTransportDoFaktury] [bit] NULL DEFAULT(0);

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[OdbiorcyTransport]') AND name = 'StalaCenaTransportu')
        ALTER TABLE [dbo].[OdbiorcyTransport] ADD [StalaCenaTransportu] [decimal](10, 2) NULL;
END
GO

-- =============================================
-- Tabela: OdbiorcyCertyfikaty - certyfikaty i wymagania jakościowe
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[OdbiorcyCertyfikaty]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[OdbiorcyCertyfikaty](
        [CertyfikatID] [int] IDENTITY(1,1) NOT NULL,
        [OdbiorcaID] [int] NOT NULL,
        [NazwaCertyfikatu] [nvarchar](100) NOT NULL,
        [NumerCertyfikatu] [nvarchar](50) NULL,
        [DataWaznosci] [date] NULL,
        [SciezkaPliku] [nvarchar](500) NULL,
        [Uwagi] [nvarchar](500) NULL,
        [DataDodania] [datetime] NOT NULL DEFAULT(GETDATE()),
        [KtoDodal] [int] NOT NULL,
        
        CONSTRAINT [PK_OdbiorcyCertyfikaty] PRIMARY KEY CLUSTERED ([CertyfikatID] ASC),
        CONSTRAINT [FK_OdbiorcyCertyfikaty_Odbiorcy] FOREIGN KEY([OdbiorcaID])
            REFERENCES [dbo].[Odbiorcy] ([OdbiorcaID]) ON DELETE CASCADE
    )
END
GO

-- =============================================
-- Modyfikacja tabeli Notatki - jeśli jeszcze nie ma TypID
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Notatki]') AND name = 'TypID')
BEGIN
    ALTER TABLE [dbo].[Notatki] ADD [TypID] [int] NULL DEFAULT(1)
END
GO

-- Dodanie indeksów dla TypID=2 (notatki do odbiorców)
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[Notatki]') AND name = N'IX_Notatki_TypID')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Notatki_TypID] ON [dbo].[Notatki]
    (
        [TypID] ASC,
        [IndeksID] ASC,
        [DataUtworzenia] DESC
    )
END
GO

-- =============================================
-- Indeksy dodatkowe
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[Odbiorcy]') AND name = N'IX_Odbiorcy_NazwaSkrot')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Odbiorcy_NazwaSkrot] ON [dbo].[Odbiorcy]
    (
        [NazwaSkrot] ASC
    )
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[Odbiorcy]') AND name = N'IX_Odbiorcy_IdOdbiorcy')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Odbiorcy_IdOdbiorcy] ON [dbo].[Odbiorcy]
    (
        [IdOdbiorcy] ASC
    )
END
GO

-- =============================================
-- Widok zbiorczy - wszystkie dane o odbiorcy
-- =============================================
IF EXISTS (SELECT * FROM sys.views WHERE object_id = OBJECT_ID(N'[dbo].[vw_OdbiorcyPelneInfo]'))
    DROP VIEW [dbo].[vw_OdbiorcyPelneInfo]
GO

CREATE VIEW [dbo].[vw_OdbiorcyPelneInfo]
AS
SELECT 
    o.OdbiorcaID,
    o.IdOdbiorcy,
    o.NazwaSkrot,
    o.PelnaNazwa,
    o.NIP,
    o.REGON,
    o.Ulica,
    o.KodPocztowy,
    o.Miejscowosc,
    o.Wojewodztwo,
    o.Kraj,
    o.Szerokosc,
    o.Dlugosc,
    o.OdlegloscKm,
    o.LimitKredytu,
    o.AktualnaSaldo,
    o.TerminPlatnosci,
    o.FormaPlatnosci,
    o.KategoriaOdbiorcy,
    o.TypOdbiorcy,
    o.StatusAktywny,
    o.RatingKredytowy,
    o.DataOstatniegoZakupu,
    o.CzestotliwoscZakupow,
    
    -- Liczba kontaktów
    (SELECT COUNT(*) FROM OdbiorcyKontakty WHERE OdbiorcaID = o.OdbiorcaID) AS LiczbaKontaktow,
    
    -- Liczba notatek CRM
    (SELECT COUNT(*) FROM Notatki WHERE IndeksID = o.OdbiorcaID AND TypID = 2) AS LiczbaNotatek,
    
    -- Ostatnia notatka
    (SELECT TOP 1 Tresc FROM Notatki WHERE IndeksID = o.OdbiorcaID AND TypID = 2 ORDER BY DataUtworzenia DESC) AS OstatniaNotatka,
    
    -- Suma obrotów z ostatnich 12 miesięcy
    (SELECT ISNULL(SUM(ObrotNetto), 0) 
     FROM OdbiorcyDaneFinansowe 
     WHERE OdbiorcaID = o.OdbiorcaID 
     AND DATEADD(MONTH, DATEDIFF(MONTH, 0, GETDATE()) - Miesiac + (YEAR(GETDATE()) - Rok) * 12, 0) >= DATEADD(MONTH, -12, GETDATE())
    ) AS ObrotRoczny,
    
    -- Transport
    t.TypTransportu,
    t.PreferowaneDniDostawy,
    t.KosztTransportuKm AS KosztKm,
    
    o.DataUtworzenia,
    o.KtoStworzyl,
    o.DataModyfikacji,
    o.KtoZmodyfikowal
FROM 
    Odbiorcy o
LEFT JOIN 
    OdbiorcyTransport t ON o.OdbiorcaID = t.OdbiorcaID
GO

PRINT '✓ Tabele kartoteki odbiorców zostały utworzone pomyślnie!'
GO
