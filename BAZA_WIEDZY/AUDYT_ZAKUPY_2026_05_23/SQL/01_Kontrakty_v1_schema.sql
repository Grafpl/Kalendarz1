-- ════════════════════════════════════════════════════════════════════════════
-- KONTRAKTY HODOWCÓW v1 — SCHEMA SQL
-- Część 4 audytu (2026-05-23)
-- Target: LibraNet (192.168.0.109)
-- Uruchom JEDNORAZOWO po przegraniu konfiguracji z appsettings.json
-- ════════════════════════════════════════════════════════════════════════════

USE LibraNet;
GO

-- Wstępne sprawdzenie: czy tabele już istnieją (jeśli tak — abort)
IF OBJECT_ID('dbo.Kontrakty', 'U') IS NOT NULL
BEGIN
    PRINT 'BŁĄD: Tabela dbo.Kontrakty już istnieje. Skrypt nie uruchomi się.';
    PRINT 'Jeśli chcesz przeglądnąć schema, sprawdź obecne tabele:';
    PRINT '  SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME LIKE ''Kontrakty%''';
    RETURN;
END
GO

-- ════════════════════════════════════════════════════════════════════════════
-- 1. GŁÓWNA TABELA — KONTRAKTY
-- ════════════════════════════════════════════════════════════════════════════

CREATE TABLE dbo.Kontrakty (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    NumerKontraktu  VARCHAR(20)  NOT NULL,
    Rok             SMALLINT     NOT NULL,
    LpRoku          INT          NOT NULL,
    DostawcaId      INT          NOT NULL,
    TypKontraktu    VARCHAR(20)  NOT NULL,
    Status          VARCHAR(20)  NOT NULL DEFAULT 'DRAFT',
    DataPodpisania  DATE         NULL,
    DataObowiazujeOd DATE        NOT NULL,
    DataObowiazujeDo DATE        NULL,
    OkresWypowiedzenia INT       NOT NULL DEFAULT 90,

    -- Warunki handlowe
    ProcentUbytku   DECIMAL(5,2) NOT NULL,
    TypCeny         VARCHAR(30)  NOT NULL,
    Cena            DECIMAL(8,4) NULL,
    TerminPlatnosciDni INT       NOT NULL DEFAULT 21,
    RozliczanaWaga  VARCHAR(20)  NOT NULL DEFAULT 'NETTO_HODOWCY',
    MinimalnaIlosc  INT          NULL,

    -- Identyfikatory hodowcy (snapshot z dnia podpisania — uchroni przed mutacjami DOSTAWCY)
    NipSnapshot     VARCHAR(15)  NULL,
    NrGospodarstwaSnapshot VARCHAR(20) NULL,
    NazwaHodowcySnapshot NVARCHAR(200) NULL,
    AdresSnapshot   NVARCHAR(300) NULL,

    -- ARiMR + sp. z o.o.
    LiczySieDoArimr BIT          NOT NULL DEFAULT 0,
    PartiaPiorkowscy VARCHAR(50) NULL,

    -- Audyt
    UtworzylUserId  VARCHAR(20)  NOT NULL,
    UtworzylKiedy   DATETIME2    NOT NULL DEFAULT GETDATE(),
    EdytowalUserId  VARCHAR(20)  NULL,
    EdytowalKiedy   DATETIME2    NULL,
    PowodWypowiedzenia NVARCHAR(500) NULL,

    -- Pliki
    SciezkaWord     NVARCHAR(500) NULL,
    SciezkaPdfSkan  NVARCHAR(500) NULL,

    CONSTRAINT FK_Kontrakty_Dostawcy FOREIGN KEY (DostawcaId) REFERENCES dbo.DOSTAWCY(ID),
    CONSTRAINT UQ_Kontrakty_Numer UNIQUE (NumerKontraktu),
    CONSTRAINT CK_Kontrakty_Status CHECK (Status IN ('DRAFT','PRINTED','SENT','SIGNED','ACTIVE','EXPIRING','EXPIRED','TERMINATED')),
    CONSTRAINT CK_Kontrakty_TypCeny CHECK (TypCeny IN ('wolnorynkowa','rolnicza','ministerialna','laczona')),
    CONSTRAINT CK_Kontrakty_TypKontraktu CHECK (TypKontraktu IN ('ARIMR_3LAT','ROCZNY','WIECZNY','SPOT')),
    CONSTRAINT CK_Kontrakty_Daty CHECK (DataObowiazujeDo IS NULL OR DataObowiazujeOd <= DataObowiazujeDo),
    CONSTRAINT CK_Kontrakty_Ubytku CHECK (ProcentUbytku >= 0 AND ProcentUbytku <= 20),
    CONSTRAINT CK_Kontrakty_Termin CHECK (TerminPlatnosciDni > 0 AND TerminPlatnosciDni <= 90)
);
GO

CREATE INDEX IX_Kontrakty_Dostawca ON dbo.Kontrakty(DostawcaId, Status);
CREATE INDEX IX_Kontrakty_Daty ON dbo.Kontrakty(DataObowiazujeDo, Status) INCLUDE (DostawcaId);
CREATE INDEX IX_Kontrakty_Arimr ON dbo.Kontrakty(LiczySieDoArimr, Status, DataObowiazujeOd, DataObowiazujeDo);
CREATE INDEX IX_Kontrakty_Rok ON dbo.Kontrakty(Rok, LpRoku);
GO

PRINT '✅ Tabela dbo.Kontrakty utworzona';

-- ════════════════════════════════════════════════════════════════════════════
-- 2. ZAŁĄCZNIKI PDF (skany, aneksy)
-- ════════════════════════════════════════════════════════════════════════════

CREATE TABLE dbo.KontraktyZalaczniki (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    KontraktId      INT          NOT NULL,
    TypZalacznika   VARCHAR(30)  NOT NULL,
    NazwaPliku      NVARCHAR(200) NOT NULL,
    SciezkaUnc      NVARCHAR(500) NOT NULL,
    DodalUserId     VARCHAR(20)  NOT NULL,
    DodanyKiedy     DATETIME2    NOT NULL DEFAULT GETDATE(),
    Opis            NVARCHAR(500) NULL,
    CONSTRAINT FK_KontraktyZal_Kontrakty FOREIGN KEY (KontraktId) REFERENCES dbo.Kontrakty(Id) ON DELETE CASCADE,
    CONSTRAINT CK_KontraktyZal_Typ CHECK (TypZalacznika IN ('SKAN_PODPISANY','ANEKS','OSWIADCZENIE','KORESPONDENCJA','INNE'))
);
GO
CREATE INDEX IX_KontraktyZal_Kontrakt ON dbo.KontraktyZalaczniki(KontraktId);
GO
PRINT '✅ Tabela dbo.KontraktyZalaczniki utworzona';

-- ════════════════════════════════════════════════════════════════════════════
-- 3. AUDIT LOG
-- ════════════════════════════════════════════════════════════════════════════

CREATE TABLE dbo.KontraktyAudit (
    Id              BIGINT IDENTITY(1,1) PRIMARY KEY,
    KontraktId      INT          NOT NULL,
    UserId          VARCHAR(20)  NOT NULL,
    Akcja           VARCHAR(50)  NOT NULL,
    PoleZmienione   VARCHAR(50)  NULL,
    StaraWartosc    NVARCHAR(500) NULL,
    NowaWartosc     NVARCHAR(500) NULL,
    Kiedy           DATETIME2    NOT NULL DEFAULT GETDATE(),
    CONSTRAINT FK_KontraktyAudit_Kontrakty FOREIGN KEY (KontraktId) REFERENCES dbo.Kontrakty(Id) ON DELETE CASCADE
);
GO
CREATE INDEX IX_KontraktyAudit_Kontrakt ON dbo.KontraktyAudit(KontraktId, Kiedy DESC);
GO
PRINT '✅ Tabela dbo.KontraktyAudit utworzona';

-- ════════════════════════════════════════════════════════════════════════════
-- 4. ALERTY (notyfikacje dla Asi/Sera)
-- ════════════════════════════════════════════════════════════════════════════

CREATE TABLE dbo.KontraktyAlerty (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    KontraktId      INT          NOT NULL,
    TypAlertu       VARCHAR(30)  NOT NULL,
    DataWygenerowania DATETIME2  NOT NULL DEFAULT GETDATE(),
    Severity        VARCHAR(10)  NOT NULL,
    DlaUserId       VARCHAR(20)  NOT NULL,
    Przeczytany     BIT          NOT NULL DEFAULT 0,
    PrzeczytanyKiedy DATETIME2   NULL,
    PrzeczytanyKto  VARCHAR(20)  NULL,
    Wiadomosc       NVARCHAR(500) NOT NULL,
    CONSTRAINT FK_KontraktyAlerty_Kontrakty FOREIGN KEY (KontraktId) REFERENCES dbo.Kontrakty(Id) ON DELETE CASCADE,
    CONSTRAINT CK_KontraktyAlerty_Severity CHECK (Severity IN ('INFO','WARN','CRIT'))
);
GO
CREATE INDEX IX_KontraktyAlerty_NieprzeczytaneUser ON dbo.KontraktyAlerty(DlaUserId, Przeczytany, Severity)
    WHERE Przeczytany = 0;
GO
PRINT '✅ Tabela dbo.KontraktyAlerty utworzona';

-- ════════════════════════════════════════════════════════════════════════════
-- 5. TEMPLATES (szablony Word)
-- ════════════════════════════════════════════════════════════════════════════

CREATE TABLE dbo.KontraktyTemplates (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    Nazwa           NVARCHAR(100) NOT NULL,
    TypKontraktu    VARCHAR(20)  NOT NULL,
    SciezkaSzablon  NVARCHAR(500) NOT NULL,
    Aktywny         BIT          NOT NULL DEFAULT 1,
    PodpisaneZSer   BIT          NOT NULL DEFAULT 0,
    UtworzonyKiedy  DATETIME2    NOT NULL DEFAULT GETDATE(),
    Notatka         NVARCHAR(1000) NULL
);
GO
PRINT '✅ Tabela dbo.KontraktyTemplates utworzona';

INSERT INTO dbo.KontraktyTemplates (Nazwa, TypKontraktu, SciezkaSzablon, PodpisaneZSer, Notatka) VALUES
  ('ARIMR_3LAT_v1', 'ARIMR_3LAT', '\\192.168.0.170\Install\UmowyZakupu\_SZABLON\Umowa_ARIMR_3LAT.docx', 0, 'Pod dotację 2027 — sprawdzić z prawniczką PRZED użyciem'),
  ('WIECZNY_v1',    'WIECZNY',    '\\192.168.0.170\Install\UmowyZakupu\_SZABLON\Umowa_Wieczna.docx',     0, 'Czas nieokreślony, wypowiedzenie 90 dni'),
  ('ROCZNY_v1',     'ROCZNY',     '\\192.168.0.170\Install\UmowyZakupu\_SZABLON\Umowa_Roczna.docx',      0, 'Standardowy roczny'),
  ('SPOT_v1',       'SPOT',       '\\192.168.0.170\Install\UmowyZakupu\_SZABLON\Umowa_Spot.docx',        0, 'Pojedyncza dostawa, bez czasu obowiązywania');
GO
PRINT '✅ Wzorcowe templates wstawione (cztery typy)';

-- ════════════════════════════════════════════════════════════════════════════
-- 6. KONFIGURACJA ESKALACJI
-- ════════════════════════════════════════════════════════════════════════════

CREATE TABLE dbo.KontraktyEskalacjaConfig (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    TypAlertu       VARCHAR(30)  NOT NULL,
    DniDoWygasniecia INT         NOT NULL,
    Severity        VARCHAR(10)  NOT NULL,
    DlaUserIdLista  NVARCHAR(200) NOT NULL,
    KanalEmail      BIT          NOT NULL DEFAULT 0,
    KanalPushZpsp   BIT          NOT NULL DEFAULT 1,
    BlokujLogowanie BIT          NOT NULL DEFAULT 0,
    Aktywny         BIT          NOT NULL DEFAULT 1
);
GO
PRINT '✅ Tabela dbo.KontraktyEskalacjaConfig utworzona';

INSERT INTO dbo.KontraktyEskalacjaConfig (TypAlertu, DniDoWygasniecia, Severity, DlaUserIdLista, KanalEmail, KanalPushZpsp, BlokujLogowanie) VALUES
  ('WYGASA_3M', 90, 'INFO', 'asia',                       0, 1, 0),
  ('WYGASA_1M', 30, 'WARN', 'asia;ser',                   1, 1, 0),
  ('WYGASA_7D',  7, 'WARN', 'asia;ser;tereska;magda',     1, 1, 0),
  ('WYGASNAL',  -1, 'CRIT', 'asia;ser;tereska;magda',     1, 1, 1),
  ('BRAK_SKANU', 30, 'WARN', 'asia',                      0, 1, 0),
  ('ARIMR_NIESPELNIONE', 0, 'CRIT', 'asia;ser',           1, 1, 0);
GO
PRINT '✅ Wzorcowa konfiguracja eskalacji wstawiona (6 typów)';
PRINT 'UWAGA: zmień ID userów (asia, ser, ...) na właściwe loginy z dbo.operators!';

-- ════════════════════════════════════════════════════════════════════════════
-- 7. STORED PROCEDURE — generowanie kolejnego numeru
-- ════════════════════════════════════════════════════════════════════════════

CREATE OR ALTER PROCEDURE dbo.sp_KontraktyNastepnyNumer
    @Rok SMALLINT,
    @NumerOut VARCHAR(20) OUTPUT,
    @LpOut INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRANSACTION;
        DECLARE @lp INT = ISNULL(
            (SELECT MAX(LpRoku) FROM dbo.Kontrakty WITH (TABLOCKX) WHERE Rok = @Rok),
            0
        ) + 1;
        SET @LpOut = @lp;
        SET @NumerOut = CAST(@lp AS VARCHAR(10)) + '/' + RIGHT(CAST(@Rok AS VARCHAR(4)), 2);
    COMMIT TRANSACTION;
END;
GO
PRINT '✅ Procedura dbo.sp_KontraktyNastepnyNumer utworzona';

-- ════════════════════════════════════════════════════════════════════════════
-- 8. VIEW — ARiMR Compliance (live)
-- ════════════════════════════════════════════════════════════════════════════

CREATE OR ALTER VIEW dbo.v_ArimrCompliance AS
WITH OkresOstatnie12M AS (
    SELECT DATEADD(MONTH, -12, CAST(GETDATE() AS DATE)) AS Od, CAST(GETDATE() AS DATE) AS Do
),
SurowiecCalosc AS (
    SELECT
        ISNULL(SUM(NettoFarmWeight), 0) AS WagaKg,
        COUNT(DISTINCT Dostawca) AS LiczbaHodowcow
    FROM dbo.FarmerCalc, OkresOstatnie12M o
    WHERE CalcDate BETWEEN o.Od AND o.Do
),
SurowiecArimr AS (
    SELECT
        ISNULL(SUM(fc.NettoFarmWeight), 0) AS WagaKg,
        COUNT(DISTINCT fc.Dostawca) AS LiczbaHodowcow
    FROM dbo.FarmerCalc fc, OkresOstatnie12M o
    WHERE fc.CalcDate BETWEEN o.Od AND o.Do
      AND EXISTS (
        SELECT 1 FROM dbo.Kontrakty k
        WHERE k.DostawcaId = fc.Dostawca
          AND k.LiczySieDoArimr = 1
          AND k.Status IN ('ACTIVE','EXPIRING','SIGNED')
          AND k.DataObowiazujeOd <= fc.CalcDate
          AND (k.DataObowiazujeDo IS NULL OR k.DataObowiazujeDo >= fc.CalcDate)
      )
)
SELECT
    sc.WagaKg AS SurowiecCaloscKg,
    sa.WagaKg AS SurowiecArimrKg,
    sc.LiczbaHodowcow AS HodowcowOgolem,
    sa.LiczbaHodowcow AS HodowcowArimr,
    CAST(CASE WHEN sc.WagaKg = 0 THEN 0
              ELSE sa.WagaKg * 100.0 / sc.WagaKg END AS DECIMAL(5,2)) AS ProcentArimr,
    CASE
        WHEN sc.WagaKg = 0 THEN 'BRAK_DANYCH'
        WHEN sa.WagaKg * 100.0 / sc.WagaKg >= 50 THEN 'OK'
        WHEN sa.WagaKg * 100.0 / sc.WagaKg >= 45 THEN 'WARN'
        ELSE 'CRIT'
    END AS Status,
    GETDATE() AS WyliczonoKiedy
FROM SurowiecCalosc sc, SurowiecArimr sa;
GO
PRINT '✅ View dbo.v_ArimrCompliance utworzona';

-- ════════════════════════════════════════════════════════════════════════════
-- 9. SMOKE TEST
-- ════════════════════════════════════════════════════════════════════════════

PRINT '';
PRINT '════════════════════════════════════════════════════════════════';
PRINT 'SMOKE TEST — wszystkie tabele i obiekty';
PRINT '════════════════════════════════════════════════════════════════';

SELECT
    'Tabela ' + TABLE_NAME AS Obiekt,
    'OK' AS Status
FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_NAME LIKE 'Kontrakty%'
ORDER BY TABLE_NAME;

SELECT
    'Procedura ' + ROUTINE_NAME AS Obiekt,
    'OK' AS Status
FROM INFORMATION_SCHEMA.ROUTINES
WHERE ROUTINE_NAME LIKE 'sp_Kontrakty%';

SELECT
    'View ' + TABLE_NAME AS Obiekt,
    'OK' AS Status
FROM INFORMATION_SCHEMA.VIEWS
WHERE TABLE_NAME LIKE 'v_Arimr%';

-- Test sp_KontraktyNastepnyNumer (dry run)
DECLARE @num VARCHAR(20), @lp INT;
EXEC dbo.sp_KontraktyNastepnyNumer @Rok = 2027, @NumerOut = @num OUTPUT, @LpOut = @lp OUTPUT;
PRINT 'Test sp_KontraktyNastepnyNumer(2027) → ' + @num + ' (Lp=' + CAST(@lp AS VARCHAR) + ')';
PRINT '(uwaga: ten test ALOKUJE numer w transakcji która została cofnięta — możesz uruchomić ponownie)';

PRINT '';
PRINT '════════════════════════════════════════════════════════════════';
PRINT '✅ SCHEMA KONTRAKTY v1 GOTOWA';
PRINT '════════════════════════════════════════════════════════════════';
PRINT '';
PRINT 'NASTĘPNE KROKI:';
PRINT '1. Sprawdź czy ID userów w KontraktyEskalacjaConfig zgadzają się z dbo.operators';
PRINT '2. Utwórz folder \\192.168.0.170\Install\UmowyZakupu\_SZABLON\ z szablonami Word';
PRINT '3. Wgraj z Asią szablony do _SZABLON (po konsultacji z prawniczką)';
PRINT '4. Faza 2: serwisy C# (KontraktyService, WordTemplateService, KontraktyAlertService)';
PRINT '';
PRINT 'PEŁNA SPEC: BAZA_WIEDZY/AUDYT_ZAKUPY_2026_05_23/04_MODUL_KONTRAKTY_SPEC.md';
