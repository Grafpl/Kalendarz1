-- ════════════════════════════════════════════════════════════════════════════
-- MODUŁ "KONTRAKTY HODOWCÓW" — SCHEMA v2 (wersjonowana)
-- Target: LibraNet (192.168.0.109)   |   Data: 2026-05-25
--
-- Różnice vs v1 z audytu (BAZA_WIEDZY/AUDYT_ZAKUPY_2026_05_23/SQL/01_Kontrakty_v1_schema.sql):
--   1. WERSJONOWANIE: warunki handlowe + okres ważności wyniesione do dbo.KontraktyWersje
--      (każda renegocjacja/przedłużenie = nowa wersja, stare read-only). Nagłówek = tożsamość.
--   2. POPRAWKA FK: Dostawcy.ID to VARCHAR(10) (PK), nie INT. (v1 miał DostawcaId INT — błąd.)
--   3. POPRAWKA compliance: FarmerCalc linkuje do hodowcy przez CustomerGID = Dostawcy.ID
--      (v1 joinował po nieistniejącym fc.Dostawca).
--   4. Brak twardego FK do Dostawcy (legacy SQL 2008 R2 — ID może nie mieć formalnego
--      UNIQUE constraint wymaganego przez FK; walidacja po stronie .NET). Patrz sekcja 0.
--
-- Uruchom JEDNORAZOWO. Idempotentny guard na początku.
-- Pełna spec koncepcji: BAZA_WIEDZY/AUDYT_ZAKUPY_2026_05_23/04_MODUL_KONTRAKTY_SPEC.md
-- ════════════════════════════════════════════════════════════════════════════

USE LibraNet;
GO

IF OBJECT_ID('dbo.Kontrakty', 'U') IS NOT NULL
BEGIN
    PRINT 'BŁĄD: dbo.Kontrakty już istnieje — skrypt przerwany. (Sprawdź INFORMATION_SCHEMA.TABLES LIKE ''Kontrakty%'')';
    RETURN;
END
GO

-- ════════════════════════════════════════════════════════════════════════════
-- 0. (OPCJONALNIE) sprawdzenie czy Dostawcy.ID nadaje się pod twardy FK
--    FK wymaga PK/UNIQUE na kolumnie referencyjnej. Jeśli poniższe zwróci wiersz,
--    możesz odkomentować constraint FK_Kontrakty_Dostawcy w sekcji 1.
-- ════════════════════════════════════════════════════════════════════════════
-- SELECT kcu.TABLE_NAME, kcu.COLUMN_NAME, tc.CONSTRAINT_TYPE
-- FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE kcu
-- JOIN INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc ON kcu.CONSTRAINT_NAME = tc.CONSTRAINT_NAME
-- WHERE kcu.TABLE_NAME = 'Dostawcy' AND tc.CONSTRAINT_TYPE IN ('PRIMARY KEY','UNIQUE')
--   AND kcu.COLUMN_NAME = 'ID';

-- ════════════════════════════════════════════════════════════════════════════
-- 1. NAGŁÓWEK KONTRAKTU — stabilna tożsamość relacji z hodowcą
--    (1 hodowca = N kontraktów w czasie; 1 kontrakt = N wersji warunków)
-- ════════════════════════════════════════════════════════════════════════════
CREATE TABLE dbo.Kontrakty (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    NumerKontraktu  VARCHAR(20)   NOT NULL,                  -- "1/27" (LpRoku/Rok2cyfry, reset 1 stycznia)
    Rok             SMALLINT      NOT NULL,
    LpRoku          INT           NOT NULL,

    DostawcaId      VARCHAR(10)   NOT NULL,                  -- -> Dostawcy.ID (varchar(10)!)
    TypKontraktu    VARCHAR(20)   NOT NULL,                  -- ARIMR_3LAT / ROCZNY / SEZONOWY / WIECZNY / SPOT
    LiczySieDoArimr BIT           NOT NULL DEFAULT 0,        -- czy liczy się do progu 50% (dotacja ARiMR)
    Podmiot         VARCHAR(20)   NOT NULL DEFAULT 'PIORKOWSCY_SC', -- SC / SPZOO (transformacja 01.08.2026)

    -- Snapshot hodowcy z dnia utworzenia (chroni przed mutacjami dbo.Dostawcy)
    NazwaHodowcySnapshot   NVARCHAR(200) NULL,               -- Dostawcy.Name
    NipSnapshot            VARCHAR(15)   NULL,               -- Dostawcy.Nip
    NrGospodarstwaSnapshot VARCHAR(20)   NULL,               -- Dostawcy.AnimNo (numer ARiMR)
    AdresSnapshot          NVARCHAR(300) NULL,               -- Dostawcy.AdresPelny

    -- Powiązanie z poprzednikiem (gdy formalnie NOWA umowa zastępuje starą, nie tylko wersja)
    PoprzedniKontraktId INT        NULL,

    -- Audyt nagłówka
    UtworzylUserId  VARCHAR(20)   NOT NULL,
    UtworzylKiedy   DATETIME2     NOT NULL DEFAULT SYSDATETIME(),
    ZamknietyKiedy  DATETIME2     NULL,
    PowodZamkniecia NVARCHAR(500) NULL,

    CONSTRAINT UQ_Kontrakty_Numer UNIQUE (NumerKontraktu),
    CONSTRAINT FK_Kontrakty_Poprzedni FOREIGN KEY (PoprzedniKontraktId) REFERENCES dbo.Kontrakty(Id),
    CONSTRAINT CK_Kontrakty_Typ    CHECK (TypKontraktu  IN ('ARIMR_3LAT','ROCZNY','SEZONOWY','WIECZNY','SPOT')),
    CONSTRAINT CK_Kontrakty_Podmiot CHECK (Podmiot IN ('PIORKOWSCY_SC','PIORKOWSCY_SPZOO'))
    -- ,CONSTRAINT FK_Kontrakty_Dostawcy FOREIGN KEY (DostawcaId) REFERENCES dbo.Dostawcy(ID)  -- patrz sekcja 0
);
GO
CREATE INDEX IX_Kontrakty_Dostawca ON dbo.Kontrakty(DostawcaId);
CREATE INDEX IX_Kontrakty_Arimr    ON dbo.Kontrakty(LiczySieDoArimr) INCLUDE (DostawcaId);
CREATE INDEX IX_Kontrakty_Rok      ON dbo.Kontrakty(Rok, LpRoku);
GO
PRINT '✅ dbo.Kontrakty (nagłówek)';

-- ════════════════════════════════════════════════════════════════════════════
-- 2. WERSJE KONTRAKTU — snapshot warunków + okresu ważności (HISTORIA)
--    Każda zmiana warunków = nowy wiersz. Aktualna = IsAktualna=1 (max 1/kontrakt).
--    Stare wersje read-only w UI (decyduje Status, nie IsAktualna).
-- ════════════════════════════════════════════════════════════════════════════
CREATE TABLE dbo.KontraktyWersje (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    KontraktId      INT           NOT NULL,
    NrWersji        INT           NOT NULL,                  -- 1, 2, 3...
    IsAktualna      BIT           NOT NULL DEFAULT 0,
    Status          VARCHAR(20)   NOT NULL DEFAULT 'DRAFT',  -- cykl życia tej wersji (patrz CK)

    -- Okres obowiązywania — ŹRÓDŁO alertów "wygasa"
    DataPodpisania  DATE          NULL,
    ObowiazujeOd    DATE          NOT NULL,
    ObowiazujeDo    DATE          NULL,                      -- NULL = wieczny / czas nieokreślony
    OkresWypowiedzeniaDni INT     NOT NULL DEFAULT 90,

    -- Warunki handlowe (snapshot per wersja)
    ProcentUbytku   DECIMAL(5,2)  NULL,                      -- 3.00 = 3%
    TypCeny         VARCHAR(20)   NOT NULL DEFAULT 'wolnorynkowa', -- wolnorynkowa/rolnicza/ministerialna/laczona
    Cena            DECIMAL(8,4)  NULL,                      -- zł/kg (NULL = wg cennika dnia)
    DodatekZl       DECIMAL(8,4)  NULL,                      -- dopłata zł/kg
    TerminPlatnosciDni INT        NOT NULL DEFAULT 21,
    RozliczanaWaga  VARCHAR(20)   NOT NULL DEFAULT 'NETTO_HODOWCY', -- NETTO_HODOWCY / NETTO_UBOJNI
    MinimalnaIloscSzt INT         NULL,                      -- klauzula wolumenu (min szt/cykl)
    Ekskluzywnosc   BIT           NOT NULL DEFAULT 0,        -- klauzula wyłączności
    KlauzuleSzczegolne NVARCHAR(MAX) NULL,                   -- wolny tekst (niestandardowe zapisy)

    -- Pliki tej wersji
    SciezkaWord     NVARCHAR(500) NULL,
    SciezkaPdfSkan  NVARCHAR(500) NULL,
    SzablonId       INT           NULL,                      -- -> KontraktyTemplates.Id

    -- Dlaczego powstała ta wersja
    PowodZmiany     NVARCHAR(500) NULL,                      -- "przedłużenie", "renegocjacja ceny", "transformacja sp. z o.o."
    UtworzylUserId  VARCHAR(20)   NOT NULL,
    UtworzylKiedy   DATETIME2     NOT NULL DEFAULT SYSDATETIME(),

    CONSTRAINT FK_KontraktyWersje_Kontrakt FOREIGN KEY (KontraktId) REFERENCES dbo.Kontrakty(Id) ON DELETE CASCADE,
    CONSTRAINT UQ_KontraktyWersje_Nr UNIQUE (KontraktId, NrWersji),
    CONSTRAINT CK_KontraktyWersje_Status CHECK (Status IN ('DRAFT','NEGOCJACJE','SENT','SIGNED','ACTIVE','EXPIRING','EXPIRED','TERMINATED','SUPERSEDED')),
    CONSTRAINT CK_KontraktyWersje_TypCeny CHECK (TypCeny IN ('wolnorynkowa','rolnicza','ministerialna','laczona')),
    CONSTRAINT CK_KontraktyWersje_Waga CHECK (RozliczanaWaga IN ('NETTO_HODOWCY','NETTO_UBOJNI')),
    CONSTRAINT CK_KontraktyWersje_Daty CHECK (ObowiazujeDo IS NULL OR ObowiazujeOd <= ObowiazujeDo),
    CONSTRAINT CK_KontraktyWersje_Ubytek CHECK (ProcentUbytku IS NULL OR (ProcentUbytku >= 0 AND ProcentUbytku <= 20)),
    CONSTRAINT CK_KontraktyWersje_Termin CHECK (TerminPlatnosciDni > 0 AND TerminPlatnosciDni <= 120)
);
GO
-- tylko JEDNA aktualna wersja per kontrakt
CREATE UNIQUE INDEX UX_KontraktyWersje_Aktualna ON dbo.KontraktyWersje(KontraktId) WHERE IsAktualna = 1;
-- szybkie wyszukiwanie wygasających (job alertowy operuje na aktualnych wersjach)
CREATE INDEX IX_KontraktyWersje_Wygasanie ON dbo.KontraktyWersje(ObowiazujeDo) INCLUDE (KontraktId, Status) WHERE IsAktualna = 1;
GO
PRINT '✅ dbo.KontraktyWersje (wersje warunków)';

-- ════════════════════════════════════════════════════════════════════════════
-- 3. ALERTY — notyfikacje (Asia/Ser) o wygaśnięciu / brakach / compliance
-- ════════════════════════════════════════════════════════════════════════════
CREATE TABLE dbo.KontraktyAlerty (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    KontraktId      INT           NOT NULL,
    WersjaId        INT           NULL,                      -- której wersji dotyczy (zwykle aktualnej)
    TypAlertu       VARCHAR(30)   NOT NULL,                  -- WYGASA_90 / WYGASA_60 / WYGASA_30 / WYGASNAL / BRAK_SKANU / ARIMR_PROG
    Severity        VARCHAR(10)   NOT NULL,                  -- INFO / WARN / CRIT
    DlaUserId       VARCHAR(20)   NOT NULL,
    Wiadomosc       NVARCHAR(500) NOT NULL,
    DataWygenerowania DATETIME2   NOT NULL DEFAULT SYSDATETIME(),
    Przeczytany     BIT           NOT NULL DEFAULT 0,
    PrzeczytanyKiedy DATETIME2    NULL,
    PrzeczytanyKto  VARCHAR(20)   NULL,
    AkcjaPodjeta    BIT           NOT NULL DEFAULT 0,        -- "załatwione" (przedłużono/wypowiedziano)
    CONSTRAINT FK_KontraktyAlerty_Kontrakt FOREIGN KEY (KontraktId) REFERENCES dbo.Kontrakty(Id) ON DELETE CASCADE,
    CONSTRAINT CK_KontraktyAlerty_Severity CHECK (Severity IN ('INFO','WARN','CRIT'))
);
GO
CREATE INDEX IX_KontraktyAlerty_Nieprzeczytane ON dbo.KontraktyAlerty(DlaUserId, Przeczytany, Severity) WHERE Przeczytany = 0;
CREATE INDEX IX_KontraktyAlerty_Kontrakt ON dbo.KontraktyAlerty(KontraktId, DataWygenerowania DESC);
GO
PRINT '✅ dbo.KontraktyAlerty';

-- ════════════════════════════════════════════════════════════════════════════
-- 4. (POMOCNICZE) ZAŁĄCZNIKI — aneksy/korespondencja PONAD główny skan wersji
-- ════════════════════════════════════════════════════════════════════════════
CREATE TABLE dbo.KontraktyZalaczniki (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    KontraktId      INT           NOT NULL,
    WersjaId        INT           NULL,
    TypZalacznika   VARCHAR(30)   NOT NULL,                  -- SKAN_PODPISANY / ANEKS / OSWIADCZENIE / KORESPONDENCJA / INNE
    NazwaPliku      NVARCHAR(200) NOT NULL,
    SciezkaUnc      NVARCHAR(500) NOT NULL,
    DodalUserId     VARCHAR(20)   NOT NULL,
    DodanyKiedy     DATETIME2     NOT NULL DEFAULT SYSDATETIME(),
    Opis            NVARCHAR(500) NULL,
    CONSTRAINT FK_KontraktyZal_Kontrakt FOREIGN KEY (KontraktId) REFERENCES dbo.Kontrakty(Id) ON DELETE CASCADE,
    CONSTRAINT CK_KontraktyZal_Typ CHECK (TypZalacznika IN ('SKAN_PODPISANY','ANEKS','OSWIADCZENIE','KORESPONDENCJA','INNE'))
);
GO
CREATE INDEX IX_KontraktyZal_Kontrakt ON dbo.KontraktyZalaczniki(KontraktId);
GO
PRINT '✅ dbo.KontraktyZalaczniki';

-- ════════════════════════════════════════════════════════════════════════════
-- 5. (POMOCNICZE) SZABLONY WORD + KONFIGURACJA ESKALACJI
-- ════════════════════════════════════════════════════════════════════════════
CREATE TABLE dbo.KontraktyTemplates (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    Nazwa           NVARCHAR(100) NOT NULL,
    TypKontraktu    VARCHAR(20)   NOT NULL,
    SciezkaSzablon  NVARCHAR(500) NOT NULL,
    Aktywny         BIT           NOT NULL DEFAULT 1,
    ZatwierdzonyPrawnie BIT       NOT NULL DEFAULT 0,
    UtworzonyKiedy  DATETIME2     NOT NULL DEFAULT SYSDATETIME(),
    Notatka         NVARCHAR(1000) NULL
);
GO
INSERT INTO dbo.KontraktyTemplates (Nazwa, TypKontraktu, SciezkaSzablon, ZatwierdzonyPrawnie, Notatka) VALUES
  ('ARIMR_3LAT_v1', 'ARIMR_3LAT', '\\192.168.0.170\Install\UmowyZakupu\_SZABLON\Umowa_ARIMR_3LAT.docx', 0, 'Pod dotację — sprawdzić z prawniczką PRZED użyciem'),
  ('ROCZNY_v1',     'ROCZNY',     '\\192.168.0.170\Install\UmowyZakupu\_SZABLON\Umowa_Roczna.docx',      0, 'Standardowy roczny'),
  ('SEZONOWY_v1',   'SEZONOWY',   '\\192.168.0.170\Install\UmowyZakupu\_SZABLON\Umowa_Sezonowa.docx',    0, 'Okres sezonu'),
  ('WIECZNY_v1',    'WIECZNY',    '\\192.168.0.170\Install\UmowyZakupu\_SZABLON\Umowa_Wieczna.docx',     0, 'Czas nieokreślony, wypowiedzenie 90 dni'),
  ('SPOT_v1',       'SPOT',       '\\192.168.0.170\Install\UmowyZakupu\_SZABLON\Umowa_Spot.docx',        0, 'Pojedyncza dostawa');
GO
PRINT '✅ dbo.KontraktyTemplates (+5 wzorców)';

CREATE TABLE dbo.KontraktyEskalacjaConfig (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    TypAlertu       VARCHAR(30)   NOT NULL,
    DniDoWygasniecia INT          NOT NULL,                  -- 90/60/30; -1 = już wygasł
    Severity        VARCHAR(10)   NOT NULL,
    DlaUserIdLista  NVARCHAR(200) NOT NULL,                  -- 'asia;ser'
    KanalEmail      BIT           NOT NULL DEFAULT 0,
    KanalPushZpsp   BIT           NOT NULL DEFAULT 1,
    Aktywny         BIT           NOT NULL DEFAULT 1
);
GO
INSERT INTO dbo.KontraktyEskalacjaConfig (TypAlertu, DniDoWygasniecia, Severity, DlaUserIdLista, KanalEmail) VALUES
  ('WYGASA_90', 90, 'INFO', 'asia',         0),
  ('WYGASA_60', 60, 'WARN', 'asia',         0),
  ('WYGASA_30', 30, 'WARN', 'asia;ser',     1),
  ('WYGASNAL',  -1, 'CRIT', 'asia;ser',     1),
  ('BRAK_SKANU',30, 'WARN', 'asia',         0);
GO
PRINT '✅ dbo.KontraktyEskalacjaConfig (UWAGA: podmień loginy asia/ser na realne z dbo.operators)';
GO

-- ════════════════════════════════════════════════════════════════════════════
-- 6. SP — następny numer kontraktu (atomowo, reset roczny)
-- ════════════════════════════════════════════════════════════════════════════
CREATE OR ALTER PROCEDURE dbo.sp_KontraktyNastepnyNumer
    @Rok SMALLINT,
    @NumerOut VARCHAR(20) OUTPUT,
    @LpOut INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRANSACTION;
        DECLARE @lp INT = ISNULL((SELECT MAX(LpRoku) FROM dbo.Kontrakty WITH (TABLOCKX) WHERE Rok = @Rok), 0) + 1;
        SET @LpOut = @lp;
        SET @NumerOut = CAST(@lp AS VARCHAR(10)) + '/' + RIGHT(CAST(@Rok AS VARCHAR(4)), 2);
    COMMIT TRANSACTION;
END;
GO
PRINT '✅ dbo.sp_KontraktyNastepnyNumer';
GO

-- ════════════════════════════════════════════════════════════════════════════
-- 7. VIEW — płaska lista do grida (nagłówek + AKTUALNA wersja)
-- ════════════════════════════════════════════════════════════════════════════
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
    k.UtworzylUserId, k.UtworzylKiedy
FROM dbo.Kontrakty k
LEFT JOIN dbo.KontraktyWersje w ON w.KontraktId = k.Id AND w.IsAktualna = 1
LEFT JOIN dbo.Dostawcy d ON d.ID = k.DostawcaId;
GO
PRINT '✅ dbo.v_KontraktyAktualne';
GO

-- ════════════════════════════════════════════════════════════════════════════
-- 8. VIEW — ARiMR compliance (% surowca pod aktywnym 3-letnim kontraktem, 12 mies.)
--    POPRAWKA: link FarmerCalc.CustomerGID = Dostawcy.ID = Kontrakty.DostawcaId.
--    Waga: NettoFarmWeight z fallbackiem (Full-Empty) — wzorzec z AvilogDataService.
-- ════════════════════════════════════════════════════════════════════════════
CREATE OR ALTER VIEW dbo.v_ArimrCompliance AS
WITH Okres AS (
    SELECT DATEADD(MONTH, -12, CAST(GETDATE() AS DATE)) AS Od, CAST(GETDATE() AS DATE) AS Do
),
Calosc AS (
    SELECT
        ISNULL(SUM(ISNULL(fc.NettoFarmWeight, ISNULL(fc.FullFarmWeight,0) - ISNULL(fc.EmptyFarmWeight,0))), 0) AS WagaKg,
        COUNT(DISTINCT LTRIM(RTRIM(fc.CustomerGID))) AS Hodowcow
    FROM dbo.FarmerCalc fc CROSS JOIN Okres o
    WHERE fc.CalcDate BETWEEN o.Od AND o.Do
),
Arimr AS (
    SELECT
        ISNULL(SUM(ISNULL(fc.NettoFarmWeight, ISNULL(fc.FullFarmWeight,0) - ISNULL(fc.EmptyFarmWeight,0))), 0) AS WagaKg,
        COUNT(DISTINCT LTRIM(RTRIM(fc.CustomerGID))) AS Hodowcow
    FROM dbo.FarmerCalc fc CROSS JOIN Okres o
    WHERE fc.CalcDate BETWEEN o.Od AND o.Do
      AND EXISTS (
        SELECT 1
        FROM dbo.Kontrakty k
        JOIN dbo.KontraktyWersje w ON w.KontraktId = k.Id
        WHERE k.DostawcaId = LTRIM(RTRIM(fc.CustomerGID))
          AND k.LiczySieDoArimr = 1
          AND w.Status IN ('ACTIVE','EXPIRING','SIGNED')
          AND w.ObowiazujeOd <= fc.CalcDate
          AND (w.ObowiazujeDo IS NULL OR w.ObowiazujeDo >= fc.CalcDate)
      )
)
SELECT
    c.WagaKg AS SurowiecCaloscKg,
    a.WagaKg AS SurowiecArimrKg,
    c.Hodowcow AS HodowcowOgolem,
    a.Hodowcow AS HodowcowArimr,
    CAST(CASE WHEN c.WagaKg = 0 THEN 0 ELSE a.WagaKg * 100.0 / c.WagaKg END AS DECIMAL(5,2)) AS ProcentArimr,
    CASE
        WHEN c.WagaKg = 0 THEN 'BRAK_DANYCH'
        WHEN a.WagaKg * 100.0 / c.WagaKg >= 50 THEN 'OK'
        WHEN a.WagaKg * 100.0 / c.WagaKg >= 45 THEN 'WARN'
        ELSE 'CRIT'
    END AS Status,
    GETDATE() AS WyliczonoKiedy
FROM Calosc c CROSS JOIN Arimr a;
GO
PRINT '✅ dbo.v_ArimrCompliance';
GO

-- ════════════════════════════════════════════════════════════════════════════
-- 9. VIEW — hodowcy Z DOSTAWAMI (12 mies.) BEZ aktywnego kontraktu
--    Zasila kafelek dashboardu "Hodowcy bez kontraktu" (są dostawy, brak umowy).
-- ════════════════════════════════════════════════════════════════════════════
CREATE OR ALTER VIEW dbo.v_HodowcyBezKontraktu AS
WITH Okres AS (
    SELECT DATEADD(MONTH, -12, CAST(GETDATE() AS DATE)) AS Od, CAST(GETDATE() AS DATE) AS Do
),
Dostawy AS (
    SELECT
        LTRIM(RTRIM(fc.CustomerGID)) AS DostawcaId,
        COUNT(*) AS LiczbaDostaw,
        SUM(ISNULL(fc.NettoFarmWeight, ISNULL(fc.FullFarmWeight,0) - ISNULL(fc.EmptyFarmWeight,0))) AS WagaKg12m,
        MAX(fc.CalcDate) AS OstatniaDostawa
    FROM dbo.FarmerCalc fc CROSS JOIN Okres o
    WHERE fc.CalcDate BETWEEN o.Od AND o.Do AND fc.CustomerGID IS NOT NULL
    GROUP BY LTRIM(RTRIM(fc.CustomerGID))
)
SELECT
    dst.DostawcaId,
    ISNULL(d.Name, '(brak nazwy)') AS Hodowca,
    dst.LiczbaDostaw, dst.WagaKg12m, dst.OstatniaDostawa
FROM Dostawy dst
LEFT JOIN dbo.Dostawcy d ON d.ID = dst.DostawcaId
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.Kontrakty k
    JOIN dbo.KontraktyWersje w ON w.KontraktId = k.Id AND w.IsAktualna = 1
    WHERE k.DostawcaId = dst.DostawcaId
      AND w.Status IN ('ACTIVE','EXPIRING','SIGNED')
);
GO
PRINT '✅ dbo.v_HodowcyBezKontraktu';

-- ════════════════════════════════════════════════════════════════════════════
-- 10. SMOKE TEST
-- ════════════════════════════════════════════════════════════════════════════
PRINT '';
PRINT '════════════════════════════════════════════════════';
SELECT 'Tabela ' + TABLE_NAME AS Obiekt FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME LIKE 'Kontrakty%' ORDER BY TABLE_NAME;
SELECT 'View ' + TABLE_NAME AS Obiekt FROM INFORMATION_SCHEMA.VIEWS WHERE TABLE_NAME IN ('v_KontraktyAktualne','v_ArimrCompliance','v_HodowcyBezKontraktu');
DECLARE @num VARCHAR(20), @lp INT;
EXEC dbo.sp_KontraktyNastepnyNumer @Rok = 2027, @NumerOut = @num OUTPUT, @LpOut = @lp OUTPUT;
PRINT 'sp_KontraktyNastepnyNumer(2027) → ' + @num;
PRINT '════════════════════════════════════════════════════';
PRINT '✅ SCHEMA KONTRAKTY v2 GOTOWA';
PRINT 'NASTĘPNE: serwisy C# (Kontrakty/Services/), okna WPF, generator Word (OpenXML).';
