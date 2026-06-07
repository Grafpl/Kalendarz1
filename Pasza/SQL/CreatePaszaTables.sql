-- ═════════════════════════════════════════════════════════════════
-- LibraNet — schema dla modulu "Zakup Paszy" (paszarnia -> hodowca z marza)
-- Idempotentne. Wykonac na serwerze 192.168.0.109 / LibraNet.
-- Sergiusz Piorko, 2026-06-03
-- ═════════════════════════════════════════════════════════════════

USE LibraNet;
GO

-- ─── 1) CENNIK marz per (hodowca x towar) ───
--      Klucz lookup: (HodowcaKhKod, TowarKod) + zakres dat.
--      Po wybraniu hodowcy+towaru w kreatorze, WPF szuka tu marzy i autofilluje pole.
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'PaszaCennik')
BEGIN
    CREATE TABLE dbo.PaszaCennik
    (
        Id              INT IDENTITY(1,1) PRIMARY KEY,
        HodowcaKhKod    NVARCHAR(50)  NOT NULL,           -- Symfonia STContractors.Shortcut (klient)
        HodowcaNazwa    NVARCHAR(200) NOT NULL,           -- kopia dla wygody UI (snapshot)
        TowarKod        NVARCHAR(50)  NOT NULL,           -- HM.TW.kod (pasza)
        TowarNazwa      NVARCHAR(200) NOT NULL,           -- kopia
        MarzaKwota      DECIMAL(10,2) NOT NULL,           -- zl / jednostka (t lub kg)
        DataOd          DATE          NOT NULL DEFAULT CAST(GETDATE() AS DATE),
        DataDo          DATE          NULL,               -- NULL = bez konca
        Aktywny         BIT           NOT NULL DEFAULT 1,
        Uwagi           NVARCHAR(500) NULL,
        UtworzonoPrzez  NVARCHAR(50)  NULL,
        UtworzonoKiedy  DATETIME      NOT NULL DEFAULT GETDATE(),
        ZmienionoPrzez  NVARCHAR(50)  NULL,
        ZmienionoKiedy  DATETIME      NULL
    );

    CREATE INDEX IX_PaszaCennik_Lookup
        ON dbo.PaszaCennik (HodowcaKhKod, TowarKod, Aktywny, DataOd DESC);

    PRINT '+ Utworzono tabele PaszaCennik';
END
ELSE
    PRINT '. PaszaCennik juz istnieje';
GO

-- ─── 2) KOLEJKA importu do Symfonii ───
--      WPF wstawia status='NOWY' po zatwierdzeniu kreatora.
--      Symfonia (mini-.sc) czyta NOWY -> tworzy 4 dokumenty -> UPDATE status='IMPORTOWANE' lub 'BLAD'.
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'PaszaImportQueue')
BEGIN
    CREATE TABLE dbo.PaszaImportQueue
    (
        Id                 INT IDENTITY(1,1) PRIMARY KEY,

        -- ── inputy z kreatora ──
        PaszarniaKhKod     NVARCHAR(50)  NOT NULL,
        PaszarniaNazwa     NVARCHAR(200) NOT NULL,
        HodowcaKhKod       NVARCHAR(50)  NOT NULL,
        HodowcaNazwa       NVARCHAR(200) NOT NULL,
        TowarKod           NVARCHAR(50)  NOT NULL,
        TowarNazwa         NVARCHAR(200) NOT NULL,
        TowarJm            NVARCHAR(10)  NOT NULL DEFAULT 't',
        Ilosc              DECIMAL(12,3) NOT NULL,
        CenaZakNetto       DECIMAL(10,2) NOT NULL,
        MarzaKwota         DECIMAL(10,2) NOT NULL,
        VatProc            DECIMAL(5,2)  NOT NULL DEFAULT 8.00,
        NumerObcy          NVARCHAR(100) NULL,
        DataWystawienia    DATE          NOT NULL,
        TerminDni          INT           NOT NULL DEFAULT 45,

        -- ── wyliczone (snapshot, zeby Symfonia nie liczyla od nowa) ──
        CenaSprzNetto      AS (CenaZakNetto + MarzaKwota) PERSISTED,
        CenaSprzBrutto     AS (ROUND((CenaZakNetto + MarzaKwota) * (1 + VatProc/100.0), 2)) PERSISTED,
        WartoscZakNetto    AS (ROUND(Ilosc * CenaZakNetto, 2)) PERSISTED,
        WartoscSprzNetto   AS (ROUND(Ilosc * (CenaZakNetto + MarzaKwota), 2)) PERSISTED,
        WartoscSprzBrutto  AS (ROUND(Ilosc * (CenaZakNetto + MarzaKwota) * (1 + VatProc/100.0), 2)) PERSISTED,
        MarzaLaczna        AS (ROUND(Ilosc * MarzaKwota, 2)) PERSISTED,

        -- ── status workflow ──
        Status             NVARCHAR(20)  NOT NULL DEFAULT 'NOWY',  -- NOWY / IMPORTOWANE / BLAD / ANULOWANE
        NrPZ               NVARCHAR(50)  NULL,
        NrFVZ              NVARCHAR(50)  NULL,
        NrWZ               NVARCHAR(50)  NULL,
        NrFPP              NVARCHAR(50)  NULL,
        BladKomunikat      NVARCHAR(1000) NULL,

        -- ── audit ──
        UtworzonoPrzez     NVARCHAR(50)  NULL,
        UtworzonoKiedy     DATETIME      NOT NULL DEFAULT GETDATE(),
        ImportowanoKiedy   DATETIME      NULL,
        AnulowanoPrzez     NVARCHAR(50)  NULL,
        AnulowanoKiedy     DATETIME      NULL,

        CONSTRAINT CK_PaszaImportQueue_Status
            CHECK (Status IN ('NOWY','IMPORTOWANE','BLAD','ANULOWANE'))
    );

    CREATE INDEX IX_PaszaImportQueue_Status
        ON dbo.PaszaImportQueue (Status, UtworzonoKiedy DESC);

    PRINT '+ Utworzono tabele PaszaImportQueue';
END
ELSE
    PRINT '. PaszaImportQueue juz istnieje';
GO

PRINT '';
PRINT '═══ Schema gotowa ═══';
PRINT 'PaszaCennik       — cennik marz per hodowca x towar';
PRINT 'PaszaImportQueue  — kolejka dokumentow do Symfonii';
GO
