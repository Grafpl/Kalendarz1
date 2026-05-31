-- ════════════════════════════════════════════════════════════════════════════
-- KONTRAKTY — kontraktacja: nowe pola + tabela harmonogramu (część 4)
-- Idempotentny. Target: LibraNet (192.168.0.109). Po 03_Kontrakty_wersje_rozszerzenia.sql.
-- ════════════════════════════════════════════════════════════════════════════
USE LibraNet;
GO

-- ── Nowe pola wg mapowania szablonu Umowa_Kontraktacji_2026 ──────────────────
IF COL_LENGTH('dbo.Kontrakty','EmailRODO') IS NULL
    ALTER TABLE dbo.Kontrakty ADD EmailRODO NVARCHAR(100) NULL;
IF COL_LENGTH('dbo.KontraktyWersje','DostawcaPaszyNazwa') IS NULL
    ALTER TABLE dbo.KontraktyWersje ADD DostawcaPaszyNazwa NVARCHAR(200) NULL;   -- NIE FK, tylko nazwa
IF COL_LENGTH('dbo.KontraktyWersje','DostawcaPisklatNazwa') IS NULL
    ALTER TABLE dbo.KontraktyWersje ADD DostawcaPisklatNazwa NVARCHAR(200) NULL; -- NIE FK, tylko nazwa
IF COL_LENGTH('dbo.KontraktyWersje','BonusOpis') IS NULL
    ALTER TABLE dbo.KontraktyWersje ADD BonusOpis NVARCHAR(500) NULL;            -- tekst opisowy
GO
PRINT '✅ Nowe kolumny: Kontrakty.EmailRODO, KontraktyWersje.DostawcaPaszyNazwa/DostawcaPisklatNazwa/BonusOpis';
GO

-- ── Harmonogram cykli — PER WERSJA (przedłużenie = nowy harmonogram) ─────────
IF OBJECT_ID('dbo.KontraktyHarmonogram','U') IS NULL
BEGIN
    CREATE TABLE dbo.KontraktyHarmonogram (
        Id                 INT IDENTITY(1,1) PRIMARY KEY,
        KontraktId         INT          NOT NULL,                 -- dla wygody zapytań (bez FK — unikamy multi-cascade)
        WersjaId           INT          NOT NULL,                 -- FK → KontraktyWersje (cascade)
        NrCyklu            INT          NOT NULL,
        DataWstawienia     DATE         NULL,
        IloscWstawiona     INT          NULL,
        IloscUbiorki       INT          NULL,
        DzienUbiorki       INT          NULL,
        DataUbojuKoncowego DATE         NULL,
        IloscUboju         INT          NULL,
        Status             VARCHAR(20)  NOT NULL DEFAULT 'PLANOWANY',
        CONSTRAINT FK_KH_Wersja FOREIGN KEY (WersjaId) REFERENCES dbo.KontraktyWersje(Id) ON DELETE CASCADE,
        CONSTRAINT UQ_KH_WersjaCykl UNIQUE (WersjaId, NrCyklu),
        CONSTRAINT CK_KH_Status CHECK (Status IN ('PLANOWANY','ZREALIZOWANY','ANULOWANY'))
    );
    CREATE INDEX IX_KH_Wersja   ON dbo.KontraktyHarmonogram(WersjaId);
    CREATE INDEX IX_KH_Kontrakt ON dbo.KontraktyHarmonogram(KontraktId);
    PRINT '✅ Tabela dbo.KontraktyHarmonogram utworzona';
END
ELSE
    PRINT 'ℹ️ dbo.KontraktyHarmonogram już istnieje — pominięto';
GO
PRINT '✅ Migracja 04 (kontraktacja) gotowa.';
GO
