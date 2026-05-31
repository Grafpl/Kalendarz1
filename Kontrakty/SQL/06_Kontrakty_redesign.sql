-- ════════════════════════════════════════════════════════════════════════════
-- KONTRAKTY — redesign: fundament nowych funkcji (część 6)
-- Idempotentny. Target: LibraNet (192.168.0.109). BEZ e-podpisu (wycięte).
-- ════════════════════════════════════════════════════════════════════════════
USE LibraNet;
GO

-- ── 1. Konfigurowalna numeracja ──────────────────────────────────────────────
IF OBJECT_ID('dbo.KontraktyNumeracjaConfig','U') IS NULL
BEGIN
    CREATE TABLE dbo.KontraktyNumeracjaConfig(
        Id            INT IDENTITY PRIMARY KEY,
        FormatSzablon NVARCHAR(50) NOT NULL,   -- {ROK}=rrrr {RR}=rr {NNNN}=numer z zerami {N}=numer
        ResetRoczny   BIT NOT NULL,
        Rok           SMALLINT NOT NULL,
        NastepnyNumer INT NOT NULL
    );
    INSERT INTO dbo.KontraktyNumeracjaConfig(FormatSzablon, ResetRoczny, Rok, NastepnyNumer)
        VALUES ('K/{ROK}/{NNNN}', 1, YEAR(GETDATE()), 1);
    PRINT '✅ KontraktyNumeracjaConfig (format K/{ROK}/{NNNN}, edytowalny)';
END
GO

-- ── 2. Wolumen roczny (kg) — do „martwych umów" i symulatora ────────────────
IF COL_LENGTH('dbo.KontraktyWersje','WolumenRocznyKg') IS NULL
    ALTER TABLE dbo.KontraktyWersje ADD WolumenRocznyKg DECIMAL(18,2) NULL;
GO

-- ── 3. Typy kontraktu: + POLROCZNY / NA_CYKLE / INNE ────────────────────────
IF OBJECT_ID('dbo.CK_Kontrakty_Typ','C') IS NOT NULL
    ALTER TABLE dbo.Kontrakty DROP CONSTRAINT CK_Kontrakty_Typ;
ALTER TABLE dbo.Kontrakty ADD CONSTRAINT CK_Kontrakty_Typ
    CHECK (TypKontraktu IN ('ARIMR_3LAT','ROCZNY','POLROCZNY','SEZONOWY','NA_CYKLE','WIECZNY','SPOT','INNE'));
GO

-- ── 4. Alerty: HIGH + eskalacja ──────────────────────────────────────────────
IF COL_LENGTH('dbo.KontraktyAlerty','WyslanoEskalacje') IS NULL
    ALTER TABLE dbo.KontraktyAlerty ADD WyslanoEskalacje DATETIME2 NULL;
GO
IF OBJECT_ID('dbo.CK_KontraktyAlerty_Severity','C') IS NOT NULL
    ALTER TABLE dbo.KontraktyAlerty DROP CONSTRAINT CK_KontraktyAlerty_Severity;
ALTER TABLE dbo.KontraktyAlerty ADD CONSTRAINT CK_KontraktyAlerty_Severity
    CHECK (Severity IN ('INFO','WARN','HIGH','CRIT'));
GO

-- ── 5. Snapshot compliance (trend ARiMR) — idempotentny per dzień ───────────
IF OBJECT_ID('dbo.KontraktyComplianceSnapshot','U') IS NULL
BEGIN
    CREATE TABLE dbo.KontraktyComplianceSnapshot(
        Id INT IDENTITY PRIMARY KEY,
        DataSnapshotu DATE NOT NULL,
        ProcentArimr DECIMAL(5,2) NOT NULL,
        SurowiecCaloscKg DECIMAL(18,2) NOT NULL,
        SurowiecArimrKg DECIMAL(18,2) NOT NULL,
        HodowcowArimr INT NOT NULL, HodowcowOgolem INT NOT NULL,
        Status NVARCHAR(20) NOT NULL,
        CONSTRAINT UQ_KCS_Data UNIQUE (DataSnapshotu)
    );
    PRINT '✅ KontraktyComplianceSnapshot';
END
GO

-- ── 6. Audyt zmian pól ──────────────────────────────────────────────────────
IF OBJECT_ID('dbo.KontraktyAuditLog','U') IS NULL
BEGIN
    CREATE TABLE dbo.KontraktyAuditLog(
        Id BIGINT IDENTITY PRIMARY KEY,
        KontraktId INT NULL, WersjaId INT NULL,
        Tabela NVARCHAR(50) NOT NULL, Operacja NVARCHAR(10) NOT NULL,
        Pole NVARCHAR(50) NULL, WartoscPrzed NVARCHAR(MAX) NULL, WartoscPo NVARCHAR(MAX) NULL,
        UserId NVARCHAR(50) NULL, Kiedy DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
    );
    CREATE INDEX IX_KAL_Kontrakt ON dbo.KontraktyAuditLog(KontraktId, Kiedy DESC);
    PRINT '✅ KontraktyAuditLog';
END
GO

-- ── 7. Decyzje transformacji JDG→sp. z o.o. (aport/dzierżawa) ───────────────
IF OBJECT_ID('dbo.KontraktyTransformacja','U') IS NULL
BEGIN
    CREATE TABLE dbo.KontraktyTransformacja(
        Id INT IDENTITY PRIMARY KEY, KontraktId INT NOT NULL,
        Decyzja NVARCHAR(20) NOT NULL,  -- APORT/DZIERZAWA/NIEOKRESLONE
        DataDecyzji DATE NULL, UserId NVARCHAR(50) NULL, Uzasadnienie NVARCHAR(1000) NULL,
        CONSTRAINT UQ_KT_Kontrakt UNIQUE (KontraktId),
        CONSTRAINT CK_KT_Decyzja CHECK (Decyzja IN ('APORT','DZIERZAWA','NIEOKRESLONE'))
    );
    PRINT '✅ KontraktyTransformacja';
END
GO
-- ── 8. Widok listy: + podtytuł hodowcy (NIP + nr gospodarstwa) ──────────────
-- (dwuliniowa komórka HODOWCA w nowej liście; bez tego subtytuł będzie pusty)
GO
CREATE OR ALTER VIEW dbo.v_KontraktyAktualne AS
SELECT
    k.Id, k.NumerKontraktu, k.DostawcaId,
    ISNULL(k.NazwaHodowcySnapshot, d.Name) AS Hodowca,
    k.TypKontraktu, k.LiczySieDoArimr, k.Podmiot,
    w.Id              AS WersjaId,
    w.NrWersji,
    w.Status,
    w.DataPodpisania, w.ObowiazujeOd, w.ObowiazujeDo,
    w.ProcentUbytku, w.TypCeny, w.Cena, w.TerminPlatnosciDni,
    w.SciezkaWord, w.SciezkaPdfSkan,
    CASE WHEN w.ObowiazujeDo IS NULL THEN NULL
         ELSE DATEDIFF(DAY, CAST(GETDATE() AS DATE), w.ObowiazujeDo) END AS DniDoWygasniecia,
    k.UtworzylUserId, k.UtworzylKiedy,
    ISNULL(NULLIF(LTRIM(RTRIM(d.Nip)),''),    '') AS HodowcaNip,
    ISNULL(NULLIF(LTRIM(RTRIM(d.AnimNo)),''), '') AS HodowcaGospodarstwo
FROM dbo.Kontrakty k
LEFT JOIN dbo.KontraktyWersje w ON w.KontraktId = k.Id AND w.IsAktualna = 1
LEFT JOIN dbo.Dostawcy d ON d.ID = k.DostawcaId;
GO
PRINT '✅ v_KontraktyAktualne (+ HodowcaNip, HodowcaGospodarstwo)';
GO

PRINT '✅ Migracja 06 (redesign) gotowa.';
GO
