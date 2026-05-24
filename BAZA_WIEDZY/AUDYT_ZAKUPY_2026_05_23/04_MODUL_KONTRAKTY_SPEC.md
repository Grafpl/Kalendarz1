# Część 4 — Pełna spec modułu Kontrakty Hodowców

**Cel:** zbudować w ZPSP **rejestr kontraktów z hodowcami** spełniający wymogi ARiMR (dotacja IX.2026, do 10M PLN, 3-letnie umowy na ≥50% surowca) + transformację w sp. z o.o. (deadline 01.08.2026 — wszystkie umowy odnowić na nowy podmiot).

**Strażnik modułu:** Asia (zgodnie z Częścią 3).
**Stack:** istniejący ZPSP (WPF .NET 8.0, code-behind, SQL Server LibraNet, OpenXML 3.4.1).

---

## 0. Streszczenie wykonawcze

| Element | Decyzja |
|---|---|
| **Nowy moduł czy rozbudowa istniejącego?** | **Nowy folder `Kontrakty/`** — istniejący `SprawdzalkaUmowWindow` bazuje na `HarmonogramDostaw` (proxy "umowa per dostawa") i służy innemu celowi. Zostaje jako legacy. |
| **Generator Word** | **OpenXML SDK 3.4.1** (już w `.csproj`, nie trzeba nowych pakietów) |
| **Numeracja** | `N/RR` (np. `1/27` = pierwszy kontrakt w 2027, reset 1. stycznia) |
| **Statusy** | 7-state lifecycle: `DRAFT → PRINTED → SENT → SIGNED → ACTIVE → EXPIRING → EXPIRED/TERMINATED` |
| **Przypominajki** | Windows Scheduled Task (nocny job) + alerty w Centrum Asi |
| **Storage skanów PDF** | folder sieciowy `\\192.168.0.170\Install\UmowyZakupu\` + ścieżka w bazie |
| **ARiMR compliance** | live metryka `% surowca pod 3-letnim kontraktem`, próg alarmu 50% |
| **Effort wdrożenia** | **2-3 tygodnie pracy Sera** (3 fazy — patrz sekcja 11) |

---

## 1. Decyzje architektoniczne

### 1.1 Nowy moduł vs rozbudowa
- `SprawdzalkaUmowWindow` (`WPF/SprawdzalkaUmowWindow.xaml.cs`, 939 linii) bazuje na **`HarmonogramDostawRepository`** — kolumna "umowa" to flaga statusu (`UTWORZONA/WYSŁANA/OTRZYMANA`) na **dostawie**, nie na kontrakcie.
- Czyli: 1 hodowca z 50 dostawami w roku = 50 checkboxów. **To nie jest rejestr kontraktów** — to lista dostaw + status dokumentu per dostawa.
- **Rozwiązanie:** zostawiamy `SprawdzalkaUmowWindow` (Magda już to zna z #6), **dodajemy nowy moduł** `Kontrakty/` jako **single source of truth dla umów** (1 hodowca = N kontraktów w czasie, kontrakt ma datę od-do).
- Stara funkcja pozostaje "view per dostawa", nowa "view per kontrakt".

### 1.2 Biblioteka Word
- `DocumentFormat.OpenXml 3.4.1` **już w `Kalendarz1.csproj` linia 78**.
- ✅ Wystarczające: pattern "load template + replace bookmarks" + zapis jako nowy `.docx`.
- ❌ Nie używamy: DocX (Xceed — komercyjna), nie używamy Aspose (drogie), nie szablonu DevExpress (dział XAML, nie Word).

### 1.3 Permissions
- Nowy `accessMap[N] = "KontraktyHodowcow"` (numer kolejny, sprawdzić w `Menu.cs:1300+`).
- Przyznać: **Asia, Ser, Justyna (asysta)**. Nie Magda na początku — tworzy z Asią obok.

### 1.4 Lokalizacja w menu
- Kategoria **ZAOPATRZENIE I ZAKUPY**, **drugi kafelek od góry** (po Bazie Hodowców), kolor `#5C8A3A` (ciemny zielony).
- Tytuł: **"📜 Kontrakty Hodowców"**, ShortTitle: "Kontrakty".

---

## 2. SQL schema (LibraNet)

### 2.1 Tabele główne

```sql
-- ════════════════════════════════════════════════════════════════════
-- KONTRAKTY HODOWCÓW v1 (2026-05-26, Część 4 audytu)
-- ════════════════════════════════════════════════════════════════════

CREATE TABLE dbo.Kontrakty (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    NumerKontraktu  VARCHAR(20)  NOT NULL,                 -- "1/27", "2/27" — UNIKALNY w roku
    Rok             SMALLINT     NOT NULL,                 -- 2027 (dla łatwej numeracji)
    LpRoku          INT          NOT NULL,                 -- 1, 2, 3... (kolejny w roku)
    DostawcaId      INT          NOT NULL,                 -- FK → DOSTAWCY.ID
    TypKontraktu    VARCHAR(20)  NOT NULL,                 -- 'ARIMR_3LAT' / 'ROCZNY' / 'WIECZNY' / 'SPOT'
    Status          VARCHAR(20)  NOT NULL DEFAULT 'DRAFT', -- DRAFT|PRINTED|SENT|SIGNED|ACTIVE|EXPIRING|EXPIRED|TERMINATED
    DataPodpisania  DATE         NULL,
    DataObowiazujeOd DATE        NOT NULL,
    DataObowiazujeDo DATE        NULL,                     -- NULL = wieczny / na czas nieokreślony
    OkresWypowiedzenia INT       NOT NULL DEFAULT 90,      -- dni wypowiedzenia (typowo 90)
    
    -- Warunki handlowe
    ProcentUbytku   DECIMAL(5,2) NOT NULL,                 -- 3.00 = 3%
    TypCeny         VARCHAR(30)  NOT NULL,                 -- 'wolnorynkowa' / 'rolnicza' / 'ministerialna' / 'lączona'
    Cena            DECIMAL(8,4) NULL,                     -- zł/kg (NULL = wg cennika dnia)
    TerminPlatnosciDni INT       NOT NULL DEFAULT 21,
    RozliczanaWaga  VARCHAR(20)  NOT NULL DEFAULT 'NETTO_HODOWCY', -- 'NETTO_HODOWCY' / 'NETTO_UBOJNI'
    MinimalnaIlosc  INT          NULL,                     -- min sztuk/cykl
    
    -- Identyfikatory hodowcy (snapshot z dnia podpisania)
    NipSnapshot     VARCHAR(15)  NULL,
    NrGospodarstwaSnapshot VARCHAR(20) NULL,
    NazwaHodowcySnapshot NVARCHAR(200) NULL,
    AdresSnapshot   NVARCHAR(300) NULL,
    
    -- ARiMR compliance
    LiczySieDoArimr BIT          NOT NULL DEFAULT 0,       -- czy ten kontrakt liczy się do 50% pod dotację
    PartiaPiorkowscy VARCHAR(50) NULL,                     -- 'PIORKOWSCY' / 'PIORKOWSCY_SPZOO' (po transformacji 01.08.2026)
    
    -- Audyt
    UtworzylUserId  VARCHAR(20)  NOT NULL,
    UtworzylKiedy   DATETIME2    NOT NULL DEFAULT GETDATE(),
    EdytowalUserId  VARCHAR(20)  NULL,
    EdytowalKiedy   DATETIME2    NULL,
    PowodWypowiedzenia NVARCHAR(500) NULL,
    
    -- Pliki
    SciezkaWord     NVARCHAR(500) NULL,                    -- \\server\...\Umowa_KOWALSKI_2027-01-15.docx
    SciezkaPdfSkan  NVARCHAR(500) NULL,                    -- \\server\...\Umowa_KOWALSKI_2027-01-15_signed.pdf

    CONSTRAINT FK_Kontrakty_Dostawcy FOREIGN KEY (DostawcaId) REFERENCES dbo.DOSTAWCY(ID),
    CONSTRAINT UQ_Kontrakty_Numer UNIQUE (NumerKontraktu),
    CONSTRAINT CK_Kontrakty_Status CHECK (Status IN ('DRAFT','PRINTED','SENT','SIGNED','ACTIVE','EXPIRING','EXPIRED','TERMINATED')),
    CONSTRAINT CK_Kontrakty_TypCeny CHECK (TypCeny IN ('wolnorynkowa','rolnicza','ministerialna','laczona'))
);
GO

CREATE INDEX IX_Kontrakty_Dostawca ON dbo.Kontrakty(DostawcaId, Status);
CREATE INDEX IX_Kontrakty_Daty ON dbo.Kontrakty(DataObowiazujeDo, Status) INCLUDE (DostawcaId);
CREATE INDEX IX_Kontrakty_Arimr ON dbo.Kontrakty(LiczySieDoArimr, Status, DataObowiazujeOd, DataObowiazujeDo);
GO
```

### 2.2 Załączniki PDF (skany, aneksy, dokumenty)

```sql
CREATE TABLE dbo.KontraktyZalaczniki (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    KontraktId      INT          NOT NULL,
    TypZalacznika   VARCHAR(30)  NOT NULL,                 -- 'SKAN_PODPISANY' / 'ANEKS' / 'OSWIADCZENIE' / 'KORESPONDENCJA'
    NazwaPliku      NVARCHAR(200) NOT NULL,
    SciezkaUnc      NVARCHAR(500) NOT NULL,
    DodalUserId     VARCHAR(20)  NOT NULL,
    DodanyKiedy     DATETIME2    NOT NULL DEFAULT GETDATE(),
    Opis            NVARCHAR(500) NULL,
    CONSTRAINT FK_KontraktyZal_Kontrakty FOREIGN KEY (KontraktId) REFERENCES dbo.Kontrakty(Id) ON DELETE CASCADE
);
GO
CREATE INDEX IX_KontraktyZal_Kontrakt ON dbo.KontraktyZalaczniki(KontraktId);
GO
```

### 2.3 Audit log + alerty

```sql
CREATE TABLE dbo.KontraktyAudit (
    Id              BIGINT IDENTITY(1,1) PRIMARY KEY,
    KontraktId      INT          NOT NULL,
    UserId          VARCHAR(20)  NOT NULL,
    Akcja           VARCHAR(50)  NOT NULL,                 -- 'CREATED','STATUS_CHANGED','EDITED','TERMINATED','ARCHIVED','SCAN_ADDED'
    PoleZmienione   VARCHAR(50)  NULL,                     -- np. 'Status', 'Cena', 'DataObowiazujeDo'
    StaraWartosc    NVARCHAR(500) NULL,
    NowaWartosc     NVARCHAR(500) NULL,
    Kiedy           DATETIME2    NOT NULL DEFAULT GETDATE(),
    CONSTRAINT FK_KontraktyAudit_Kontrakty FOREIGN KEY (KontraktId) REFERENCES dbo.Kontrakty(Id) ON DELETE CASCADE
);
GO
CREATE INDEX IX_KontraktyAudit_Kontrakt ON dbo.KontraktyAudit(KontraktId, Kiedy DESC);
GO

CREATE TABLE dbo.KontraktyAlerty (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    KontraktId      INT          NOT NULL,
    TypAlertu       VARCHAR(30)  NOT NULL,                 -- 'WYGASA_3M','WYGASA_1M','WYGASA_7D','WYGASNAL','BRAK_SKANU','ARIMR_NIESPEŁNIONE'
    DataWygenerowania DATETIME2  NOT NULL DEFAULT GETDATE(),
    Severity        VARCHAR(10)  NOT NULL,                 -- 'INFO','WARN','CRIT'
    DlaUserId       VARCHAR(20)  NOT NULL,                 -- komu skierowany (typowo Asia)
    Przeczytany     BIT          NOT NULL DEFAULT 0,
    PrzeczytanyKiedy DATETIME2   NULL,
    PrzeczytanyKto  VARCHAR(20)  NULL,
    Wiadomosc       NVARCHAR(500) NOT NULL,
    CONSTRAINT FK_KontraktyAlerty_Kontrakty FOREIGN KEY (KontraktId) REFERENCES dbo.Kontrakty(Id) ON DELETE CASCADE
);
GO
CREATE INDEX IX_KontraktyAlerty_NieprzeczytaneUser ON dbo.KontraktyAlerty(DlaUserId, Przeczytany, Severity) WHERE Przeczytany = 0;
GO
```

### 2.4 Słownik szablonów Word

```sql
CREATE TABLE dbo.KontraktyTemplates (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    Nazwa           NVARCHAR(100) NOT NULL,                -- 'ARIMR_3LAT_v1', 'ROCZNY_v3', 'SPOT_v1'
    TypKontraktu    VARCHAR(20)  NOT NULL,                 -- musi pasować do Kontrakty.TypKontraktu
    SciezkaSzablon  NVARCHAR(500) NOT NULL,                -- \\server\...\_SZABLON\Umowa_ARIMR_3LAT.docx
    Aktywny         BIT          NOT NULL DEFAULT 1,
    PodpisaneZSer   BIT          NOT NULL DEFAULT 0,       -- czy Ser/prawniczka zatwierdziła
    UtworzonyKiedy  DATETIME2    NOT NULL DEFAULT GETDATE(),
    Notatka         NVARCHAR(1000) NULL
);
GO
INSERT INTO dbo.KontraktyTemplates (Nazwa, TypKontraktu, SciezkaSzablon, PodpisaneZSer, Notatka) VALUES
  ('ARIMR_3LAT_v1', 'ARIMR_3LAT', '\\192.168.0.170\Install\UmowyZakupu\_SZABLON\Umowa_ARIMR_3LAT.docx', 0, 'Pod dotację 2027 — sprawdzić z prawniczką'),
  ('WIECZNY_v1',    'WIECZNY',    '\\192.168.0.170\Install\UmowyZakupu\_SZABLON\Umowa_Wieczna.docx',     0, 'Czas nieokreślony, wypowiedzenie 90 dni'),
  ('SPOT_v1',       'SPOT',       '\\192.168.0.170\Install\UmowyZakupu\_SZABLON\Umowa_Spot.docx',        0, 'Pojedyncza dostawa, bez czasu obowiązywania');
GO
```

### 2.5 Konfiguracja eskalacji (per typ alertu)

```sql
CREATE TABLE dbo.KontraktyEskalacjaConfig (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    TypAlertu       VARCHAR(30)  NOT NULL,
    DniDoWygasniecia INT         NOT NULL,                 -- ile dni przed/po (-7 = 7 dni po wygaśnięciu)
    Severity        VARCHAR(10)  NOT NULL,
    DlaUserIdLista  NVARCHAR(200) NOT NULL,                -- 'asia;ser' albo 'asia;tereska;magda;ser'
    KanalEmail      BIT          NOT NULL DEFAULT 0,
    KanalPushZpsp   BIT          NOT NULL DEFAULT 1,
    BlokujLogowanie BIT          NOT NULL DEFAULT 0,       -- true tylko dla CRIT po deadline
    Aktywny         BIT          NOT NULL DEFAULT 1
);
GO
INSERT INTO dbo.KontraktyEskalacjaConfig (TypAlertu, DniDoWygasniecia, Severity, DlaUserIdLista, KanalEmail, KanalPushZpsp, BlokujLogowanie) VALUES
  ('WYGASA_3M', 90, 'INFO', 'asia',                       0, 1, 0),
  ('WYGASA_1M', 30, 'WARN', 'asia;ser',                   1, 1, 0),
  ('WYGASA_7D',  7, 'WARN', 'asia;ser;tereska;magda',     1, 1, 0),
  ('WYGASNAL',  -1, 'CRIT', 'asia;ser;tereska;magda',     1, 1, 1);
GO
```

---

## 3. Numeracja kontraktów

**Formuła:** `{LpRoku}/{Rok2cyfry}` — np. `1/27`, `2/27`, `47/27`, reset 1. stycznia.

**Implementacja:** stored procedure (atomowość pod kontrolą):

```sql
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
```

**Wywołanie z C#:** `KontraktyService.GenerateNextNumberAsync()` zwraca `(numer, lp)`.

---

## 4. Generator Word (OpenXML SDK)

### 4.1 Pattern „template + bookmarks"

Szablony Word zawierają **bookmarki** (zakładki Worda, `Wstaw → Zakładka`):

| Bookmark name | Co podstawia |
|---|---|
| `bm_NumerKontraktu` | `1/27` |
| `bm_DataPodpisania` | `26 maja 2027` (po polsku) |
| `bm_NazwaHodowcy` | `Jan Kowalski` |
| `bm_AdresHodowcy` | `ul. Wiejska 12, 12-345 Wieś` |
| `bm_Nip` | `1234567890` |
| `bm_NrGospodarstwa` | `PL12345678` |
| `bm_ProcentUbytku` | `3,00 %` |
| `bm_TypCeny` | `wolnorynkowa` |
| `bm_Cena` | `7,50 zł/kg netto` |
| `bm_TerminPlatnosci` | `21 dni` |
| `bm_DataOd` | `1 czerwca 2027` |
| `bm_DataDo` | `31 maja 2030` (lub `na czas nieokreślony`) |
| `bm_OkresWypowiedzenia` | `90 dni` |
| `bm_NazwaPiorkowscy` | `Piórkowscy sp. z o.o.` (po 01.08.2026) lub `Piórkowscy s.c.` (przed) |

### 4.2 Service `WordTemplateService`

```csharp
// Kontrakty/Services/WordTemplateService.cs
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace Kalendarz1.Kontrakty.Services
{
    public class WordTemplateService
    {
        public string GenerateContract(string templatePath, string outputPath, Dictionary<string, string> values)
        {
            File.Copy(templatePath, outputPath, overwrite: true);
            using var doc = WordprocessingDocument.Open(outputPath, isEditable: true);
            var body = doc.MainDocumentPart!.Document.Body!;

            foreach (var bm in body.Descendants<BookmarkStart>().ToList())
            {
                if (!values.TryGetValue(bm.Name!, out var newText)) continue;
                // Wstaw Run z tekstem po bookmarku
                var run = new Run(new Text(newText) { Space = SpaceProcessingModeValues.Preserve });
                bm.Parent!.InsertAfter(run, bm);
            }

            doc.MainDocumentPart.Document.Save();
            return outputPath;
        }
    }
}
```

### 4.3 Flow generacji w UI

1. Asia w `KontraktyEditorWindow` wypełnia pola (dostawca, typ, daty, % ubytku, cena...).
2. Klik **„📄 Generuj Word"** → C# kompiluje `Dictionary<string,string>` z pól → `WordTemplateService.GenerateContract(...)`.
3. Plik zapisuje się do `\\192.168.0.170\Install\UmowyZakupu\{Rok}\Umowa_{Nazwisko}_{NumerKontraktu}.docx`.
4. `Kontrakty.SciezkaWord` zapisuje pełną ścieżkę.
5. Word otwiera się automatycznie (Process.Start) — Asia widzi co wyszło, koryguje niestandardowe zapisy bezpośrednio w Wordzie.
6. Po edycji Asia ręcznie zapisuje Word (Ctrl+S — Word nadal pisze pod tę samą ścieżkę).
7. Drukuje, podpisuje Ser, wysyła hodowcy.
8. Status w ZPSP zmienia się: `DRAFT → PRINTED` (przy generacji) → `SENT` (Asia ręcznie po wysłaniu) → `SIGNED` (po odesłaniu skanu).

---

## 5. Powiązania z istniejącym ZPSP

### 5.1 Wstawienia + Kontrakty

W `WidokWstawienia` / `Kalendarz dostaw` — **dodać tooltip / kolor** "pod kontraktem" / "spot":

```sql
-- SQL helper: hodowca + aktywny kontrakt dla danej daty
SELECT k.*
FROM dbo.Kontrakty k
WHERE k.DostawcaId = @dostawcaId
  AND k.Status IN ('ACTIVE','EXPIRING','SIGNED')
  AND k.DataObowiazujeOd <= @data
  AND (k.DataObowiazujeDo IS NULL OR k.DataObowiazujeDo >= @data)
ORDER BY k.LiczySieDoArimr DESC, k.DataPodpisania DESC;
```

W kalendarzu dostaw: komórka **niebieska** = pod 3-letnim ARiMR, **zielona** = pod kontraktem rocznym/wiecznym, **szara** = spot (brak kontraktu).

### 5.2 SprawdzalkaUmowWindow (legacy)

Zostaje **bez zmian** — pozostaje "lista statusów dokumentów per dostawa" (Magda używa do bieżącej operacyjki).

Dodajemy **link**: w `SprawdzalkaUmowWindow` w wierszu dostawy → przycisk "📜 Pokaż kontrakt" → otwiera `KontraktyDetailsWindow` jeśli istnieje aktywny.

### 5.3 Centrum Asi (Część 3)

Sekcja **Terminy** + **ARiMR Compliance** w Centrum Asi pochodzi bezpośrednio z `dbo.Kontrakty` + `dbo.KontraktyAlerty`. Service `CentrumAsiService` (z Części 3) używa już tych tabel.

### 5.4 DOSTAWCY (LibraNet)

Nie dotykamy struktury `DOSTAWCY` (ryzyko — Sage Symfonia używa). Foreign key `Kontrakty.DostawcaId → DOSTAWCY.ID` wystarczy.

---

## 6. Dashboard ARiMR Compliance

### 6.1 Metryka „% surowca pod 3-letnim kontraktem"

**Definicja:** waga żywca **w okresie X** (typowo ostatnie 12 miesięcy) **od hodowców mających aktywny kontrakt 3-letni ARiMR**, podzielona przez **całość surowca w tym samym okresie**.

```sql
CREATE OR ALTER VIEW dbo.v_ArimrCompliance AS
WITH OkresOstatnie12M AS (
    SELECT DATEADD(MONTH, -12, CAST(GETDATE() AS DATE)) AS Od, CAST(GETDATE() AS DATE) AS Do
),
SurowiecCalosc AS (
    SELECT SUM(NettoFarmWeight) AS WagaKg, COUNT(DISTINCT Dostawca) AS LiczbaHodowcow
    FROM dbo.FarmerCalc, OkresOstatnie12M o
    WHERE CalcDate BETWEEN o.Od AND o.Do
),
SurowiecArimr AS (
    SELECT SUM(fc.NettoFarmWeight) AS WagaKg
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
    CAST(sa.WagaKg AS DECIMAL(18,2)) / NULLIF(sc.WagaKg, 0) * 100 AS ProcentArimr,
    CASE
        WHEN CAST(sa.WagaKg AS DECIMAL(18,2)) / NULLIF(sc.WagaKg, 0) * 100 >= 50 THEN 'OK'
        WHEN CAST(sa.WagaKg AS DECIMAL(18,2)) / NULLIF(sc.WagaKg, 0) * 100 >= 45 THEN 'WARN'
        ELSE 'CRIT'
    END AS Status
FROM SurowiecCalosc sc, SurowiecArimr sa;
GO
```

### 6.2 Mockup widoku Dashboard ARiMR (osobne okno)

```
┌══════════════════════════════════════════════════════════════════════╗
║  🎯 ARiMR COMPLIANCE — kontrakty 3-letnie                            ║
║  Stan na 26.05.2026 09:14 (ostatnie 12 miesięcy)                    ║
╠══════════════════════════════════════════════════════════════════════╣
║                                                                        ║
║                                                                        ║
║       ██████████████████████░░░░░░░░░░░░  67.4% ✅                   ║
║       0%                                       50%               100%║
║                                                                        ║
║   Wymagane minimum: 50%                                               ║
║   Aktualnie:        67.4%                                             ║
║   Margines:         +17.4 pp                                          ║
║   Status:           ✅ OK                                              ║
║                                                                        ║
║  ────────────────────────────────────────────────────────────────    ║
║                                                                        ║
║  📊 SZCZEGÓŁY                                                         ║
║                                                                        ║
║  Surowiec ogółem (ostatnie 12 mies.):     12 845 320 kg              ║
║  Surowiec pod ARiMR (3-letni):             8 657 510 kg              ║
║  Hodowców ogółem:                          137                        ║
║  Hodowców pod 3-letnim:                    42  (30.7%)                ║
║                                                                        ║
║  ────────────────────────────────────────────────────────────────    ║
║                                                                        ║
║  🟡 HODOWCY DO ZAKONTRAKTOWANIA (high value, niski wysiłek)          ║
║                                                                        ║
║  Hodowca         Dostaw  Kg/12m       Komentarz                       ║
║  ─────────────  ──────  ──────────  ──────────────────────────       ║
║  ABRAMOWICZ        12   480 000     stabilny, brak skarg              ║
║  CHOJNACKI         10   395 000     stabilny, brak skarg              ║
║  DĄBROWSKI          8   312 000     stabilny, brak skarg              ║
║  EJDYS              6   234 000     w ostatnich miesiącach +20%       ║
║  ...                                                                   ║
║                                            [Generuj propozycje umów]  ║
║                                                                        ║
║  ────────────────────────────────────────────────────────────────    ║
║                                                                        ║
║  ⚠️ KONTRAKTY KOŃCZĄCE SIĘ W NAJBLIŻSZYCH 6 MIESIĄCACH               ║
║                                                                        ║
║  Hodowca         Numer    Wygasa       Status      Akcja              ║
║  ─────────────  ───────  ──────────  ──────────  ──────────         ║
║  KOWALSKI       1/24     15.07.2026  EXPIRING    Dzwoń Tereska/Magda║
║  NOWAK BIS      7/24     03.08.2026  EXPIRING    Asia: szkic         ║
║  JANKOWSKI      12/24    20.10.2026  ACTIVE      OK                  ║
║  ...                                                                   ║
║                                                                        ║
║  [📤 Export PDF dla audytu]    [⚙ Konfiguracja eskalacji]            ║
╚══════════════════════════════════════════════════════════════════════╝
```

### 6.3 Eksport PDF dla audytu ARiMR

Snapshot dashboardu + lista wszystkich aktywnych kontraktów 3-letnich → PDF (iTextSharp, już w projekcie). Asia drukuje, dołącza do dokumentacji audytu.

---

## 7. Logika przypominajek + eskalacji

### 7.1 Nocny job (Windows Scheduled Task)

```bash
# Task Scheduler — codziennie o 02:00
Kalendarz1.exe --kontrakty-check
```

Flag `--kontrakty-check` w `App.xaml.cs`:

```csharp
// pseudokod
if (args.Contains("--kontrakty-check")) {
    var svc = new KontraktyAlertService();
    await svc.GenerujAlertyAsync();
    return; // bez UI
}
```

### 7.2 Service `KontraktyAlertService.GenerujAlertyAsync`

```csharp
// Kontrakty/Services/KontraktyAlertService.cs
public async Task GenerujAlertyAsync()
{
    var config = await PobierzKonfiguracjeEskalacjiAsync();
    var kontrakty = await PobierzKontraktyZDatamiAsync();

    foreach (var k in kontrakty)
    {
        if (k.DataObowiazujeDo == null) continue; // wieczny, nie wygasa

        var dniDo = (k.DataObowiazujeDo.Value - DateTime.Today).Days;

        foreach (var cfg in config)
        {
            if (cfg.DniDoWygasniecia != dniDo) continue;
            if (await AlertJuzIstniejeAsync(k.Id, cfg.TypAlertu)) continue;

            foreach (var user in cfg.DlaUserIdLista.Split(';'))
            {
                await ZapiszAlertAsync(k, cfg, user);
                if (cfg.KanalEmail) await WyslijEmailAsync(k, cfg, user);
            }
        }

        // Status auto-update
        if (dniDo <= 30 && dniDo > 0 && k.Status == "ACTIVE") {
            await ZmienStatusAsync(k.Id, "EXPIRING");
        }
        if (dniDo < 0 && k.Status != "EXPIRED" && k.Status != "TERMINATED") {
            await ZmienStatusAsync(k.Id, "EXPIRED");
        }
    }
}
```

### 7.3 Reakcja na alerty w UI

- **W Centrum Asi** (Część 3) — sekcja "Terminy" pokazuje top 5 nieprzeczytanych alertów (z `KontraktyAlerty WHERE Przeczytany=0 ORDER BY Severity DESC`).
- **Notification badge** na kafelku "Kontrakty Hodowców" w głównym menu — liczba alertów `WHERE Przeczytany=0`.
- **Po kliknięciu alertu** w Centrum Asi — otwiera bezpośrednio `KontraktyDetailsWindow(kontraktId)` + automatycznie oznacza alert jako przeczytany.

### 7.4 Eskalacja `BLOKUJ_LOGOWANIE`

Dla alertów `WYGASNAL` (CRIT) — w `Menu1.xaml.cs` (login flow) po zalogowaniu Asi/Sera:

```csharp
// Pseudokod w Menu1.xaml.cs po LoginButton_Click
var krytyczne = await KontraktyAlertService.PobierzKrytyczneNieprzeczytaneAsync(App.UserID);
if (krytyczne.Count > 0)
{
    var dlg = new KrytyczneAlertyWindow(krytyczne) { Owner = this };
    if (dlg.ShowDialog() != true) return; // nie pozwól wejść do menu
}
```

---

## 8. Scenariusze użycia (7 ścieżek)

### S1. Nowa umowa z istniejącym hodowcą (typowy przypadek)

1. Asia: Menu → **📜 Kontrakty Hodowców** → przycisk **„➕ Nowy kontrakt"**.
2. Wybiera hodowcę (`cmbDostawca`), typ kontraktu (`ARiMR 3-letni`), szablon Word (`ARIMR_3LAT_v1`).
3. Wypełnia daty, % ubytku, cenę, typ ceny, termin płatności.
4. Klik **„🔢 Wygeneruj numer"** → `1/27` (lub kolejny).
5. Klik **„📄 Generuj Word"** → szablon wypełnia się bookmarkami → Word otwiera się.
6. Asia koryguje niestandardowe zapisy w Wordzie → Ctrl+S.
7. Klik **„✅ Zapisz w ZPSP"** → status `DRAFT` → zapis do `Kontrakty`.
8. Drukuje 2 egz., podpisuje Ser, wysyła hodowcy → klik **„Zmień status: SENT"** w UI.
9. Po otrzymaniu skanu → klik **„📎 Dodaj skan PDF"** → wybór pliku → wpis w `KontraktyZalaczniki` → status `SIGNED`.
10. Asia za 1-2 dni klika **„Aktywuj"** → status `ACTIVE`.

### S2. Przedłużenie kontraktu (wygasa za 3 mies.)

1. Asia widzi alert w Centrum Asi: "Kontrakt KOWALSKI wygasa 15.07.2026 — za 90 dni".
2. Klik → otwiera `KontraktyDetailsWindow(kontraktId)`.
3. Klik **„🔄 Utwórz przedłużenie"** → otwiera nowy kontrakt z polami pre-wypełnionymi z poprzedniego.
4. Asia tylko zmienia daty + ewentualnie cenę → Generuj Word → reszta jak S1.
5. Poprzedni kontrakt automatycznie `TERMINATED` w dniu rozpoczęcia nowego (alternatywnie zostaje `EXPIRED` po dacie końca).

### S3. Wypowiedzenie kontraktu (rozstanie z hodowcą)

1. Asia/Ser: `KontraktyDetailsWindow` → klik **„❌ Wypowiedz kontrakt"**.
2. Dialog: data wypowiedzenia (dziś), data zakończenia (dziś + okres wypowiedzenia z umowy, typowo 90 dni), powód.
3. Po zatwierdzeniu: status `TERMINATED`, `DataObowiazujeDo` aktualizuje się.
4. Wpis w `KontraktyAudit`. Asia generuje **wypowiedzenie Word** z osobnego szablonu.

### S4. Recall pod audyt ARiMR (kontrola)

1. Asia: Menu → **📜 Kontrakty Hodowców** → przycisk **„📤 Export ARiMR"**.
2. Dialog: zakres dat (np. od 01.01.2027 do dziś), typ kontraktu (`ARIMR_3LAT`).
3. Generuje **PDF raport** (iTextSharp): tytuł, lista wszystkich aktywnych 3-letnich kontraktów w okresie, %compliance per miesiąc, podsumowanie.
4. PDF zawiera linki do skanów (URL UNC do PDF w `KontraktyZalaczniki`).
5. Asia drukuje, podpina do dokumentacji audytu.

### S5. Transformacja w sp. z o.o. (01.08.2026 — wszystkie umowy odnowić)

1. Asia: Menu → **📜 Kontrakty Hodowców** → przycisk **„🔄 Migracja sp. z o.o."**.
2. System pyta: "Czy odnowić wszystkie aktywne kontrakty na nowy podmiot?".
3. Asia zatwierdza → batch operation:
   - Dla każdego ACTIVE/EXPIRING/SIGNED kontraktu z `PartiaPiorkowscy = 'PIORKOWSCY'`:
     - Generuje nowy kontrakt z tymi samymi warunkami + `PartiaPiorkowscy = 'PIORKOWSCY_SPZOO'`.
     - Stary status `TERMINATED` z powodem "Transformacja w sp. z o.o."
4. Generuje **Word per każdy nowy** automatycznie (Asia musi tylko podpisać masowo).
5. Lista do wysłania hodowcom przez Tereska/Magda.

### S6. Hodowca dzwoni "wygasła nam umowa, i co teraz?"

1. Magda otwiera **📜 Kontrakty Hodowców** → wyszukuje hodowcę → widzi że status `EXPIRED`.
2. Klik **„🚨 Eskaluj do Asi"** → wpis do `KontraktyAlerty` `Severity=CRIT` dla Asi.
3. Asia dostaje notyfikację → podejmuje decyzję: przedłużamy (S2) lub rozstajemy (S3).

### S7. Dashboard pokazuje compliance < 50% (alarm)

1. Asia rano otwiera Centrum Asi → sekcja ARiMR Compliance czerwona "44.7% CRIT".
2. Klik → otwiera **Dashboard ARiMR** (sekcja 6).
3. Sekcja "Hodowcy do zakontraktowania" pokazuje top 10 high-value spotowych.
4. Asia klika **„Generuj propozycje umów"** → batch tworzenia szkiców `DRAFT` dla 5-10 hodowców.
5. Asia rozmawia z każdym (telefon przez Magdę/Tereskę) → zatwierdzane → status idzie `DRAFT → PRINTED → SENT` → po podpisaniu compliance rośnie.

---

## 9. UI — okna modułu

### 9.1 `KontraktyListaWindow.xaml` (główny widok)

```
┌══════════════════════════════════════════════════════════════════════════╗
║  📜 Kontrakty Hodowców                                          [_][□][X]║
╠══════════════════════════════════════════════════════════════════════════╣
║  [➕ Nowy] [🔄 Przedłuż] [📊 Dashboard ARiMR] [📤 Export ARiMR] [⚙]    ║
║                                                                            ║
║  🔍 [_____________] [📅 Aktywne ✓] [📅 Wygasające] [📅 Wygasłe]          ║
║  [Typ: Wszystkie ▼] [ARiMR ✓]                              📑 137 umów   ║
║                                                                            ║
║  ┌──────────────────────────────────────────────────────────────────┐   ║
║  │ Nr     Hodowca       Typ        Status   Od         Do      Akcje│   ║
║  │ 1/27   KOWALSKI      ARiMR 3l   ACTIVE   01.02.27   31.01.30 [...│   ║
║  │ 2/27   NOWAK BIS     Roczny     ACTIVE   15.02.27   14.02.28 [...│   ║
║  │ 3/27   JANKOWSKI     Wieczny    ACTIVE   01.03.27   ---      [...│   ║
║  │ 12/25  CYBULSKI      ARiMR 3l   EXPIRING 01.06.25   31.05.28 [...│   ║
║  │ 47/24  ABRAMOWICZ    Roczny     EXPIRED  01.10.24   30.09.25 [...│   ║
║  │ ...                                                              │   ║
║  └──────────────────────────────────────────────────────────────────┘   ║
║                                                                            ║
║  Wybierz wiersz → prawy klik: Edytuj / Generuj Word / Dodaj skan / ...  ║
╚══════════════════════════════════════════════════════════════════════════╝
```

### 9.2 `KontraktyEditorWindow.xaml`

3 sekcje (grouped):
- **Strony umowy** (Dostawca / nasz podmiot)
- **Warunki handlowe** (% ubytku, cena, termin płatności, rozliczana waga)
- **Terminy + status** (data od/do, okres wypowiedzenia, status, LiczySieDoArimr)
- Dolny pasek: `[📄 Generuj Word]` `[📎 Dodaj skan]` `[💾 Zapisz]` `[❌ Anuluj]`

### 9.3 `KontraktyDetailsWindow.xaml`

Read-only view + tabbed:
- **Podstawowe** — wszystkie pola
- **Załączniki PDF** — lista plików (skan podpisany, aneksy, korespondencja)
- **Audit log** — historia zmian (`KontraktyAudit`)
- **Alerty** — historia alertów (`KontraktyAlerty`)
- **Dostawy pod kontraktem** — `HarmonogramDostaw` w okresie `DataOd–DataDo` dla hodowcy

---

## 10. Migracja istniejących danych

**Sytuacja wyjściowa:** umowy w segregatorach + folder sieciowy `\\192.168.0.170\Install\UmowyZakupu\` (chaos).

**Plan migracji (Asia + Magda + Ser, 1-2 dni):**

1. **Faza M1 — inwentaryzacja papierowa.** Asia + Magda przeglądają segregatory, robią Excel: hodowca, NIP, data podpisania, data do, typ ceny, % ubytku, ścieżka do skanu.
2. **Faza M2 — bulk import.** Ser pisze `KontraktyBulkImportService` — czyta Excel → INSERT do `Kontrakty` ze statusem `ACTIVE` (zakłada że to co w segregatorze jest ważne).
3. **Faza M3 — przegląd.** Asia w `KontraktyListaWindow` weryfikuje 1-by-1, zmienia status na `EXPIRED` te które już wygasły.
4. **Faza M4 — uzupełnienie skanów.** Asia/Magda skanują brakujące papiery → wgrywają do `\\server\UmowyZakupu\` → klik "📎 Dodaj skan" w `KontraktyDetailsWindow`.

**Effort migracji: ~2 dni Asi + 4h Magdy + 4h Sera (Excel import + UI).**

---

## 11. Plan wdrożenia (3 fazy)

### Faza 1 — Fundament (5 dni roboczych Sera)

- ✅ SQL schema (tabele, indeksy, słowniki, sp_NastepnyNumer) — 1 dzień
- ✅ `KontraktyService` (CRUD, numeracja, status transitions) — 1 dzień
- ✅ `KontraktyListaWindow` + `KontraktyEditorWindow` (CRUD UI) — 2 dni
- ✅ Bulk import z Excela (migracja istniejących) — 1 dzień

**Po Fazie 1:** Asia ma rejestr 100+ kontraktów w bazie z poprawnymi statusami. Wciąż bez generatora Word ani alertów.

### Faza 2 — Generator Word + skany (4 dni)

- ✅ `WordTemplateService` (OpenXML, bookmarks) — 1 dzień
- ✅ Szablony Word `_SZABLON/Umowa_ARIMR_3LAT.docx` + 2 inne (Asia + prawniczka tworzą treść, Ser tylko bookmarki) — 1 dzień (głównie Asi/prawniczka)
- ✅ UI **„Generuj Word"** + auto-otwarcie Worda — 4h
- ✅ UI **„Dodaj skan PDF"** + upload do folderu sieciowego — 4h
- ✅ `KontraktyDetailsWindow` (3 zakładki) — 1 dzień

**Po Fazie 2:** Asia robi nowe kontrakty z 1 klika generacji Worda. Każdy ma przypisany skan w bazie + folderze.

### Faza 3 — Alerty + Dashboard ARiMR (4 dni)

- ✅ `KontraktyAlertService` (nocny job) — 1 dzień
- ✅ Windows Scheduled Task — 1h
- ✅ `KontraktyAlertyWindow` (lista nieprzeczytanych) — 4h
- ✅ Integracja z Centrum Asi (sekcja Terminy) — 4h
- ✅ Email sender (Outlook interop) — 4h
- ✅ `DashboardArimrWindow` + view `v_ArimrCompliance` — 1 dzień
- ✅ Export PDF dla audytu — 1 dzień

**Po Fazie 3:** **pełny moduł działa.** Asia widzi compliance live, dostaje alerty 90/30/7 dni przed wygasaniem, ma generator PDF na żądanie audytu.

**Razem: 13 dni roboczych Sera + ~3 dni Asi (przeglądy, weryfikacje, szablony).**

---

## 12. Co po wdrożeniu — opcjonalne rozszerzenia

| Funkcja | Effort | Wartość |
|---|---|---|
| Integracja z **KSeF** — automatyczne pobranie faktur dla kontraktu | 5 dni | ⭐⭐⭐ |
| Integracja z **IRZplus** — weryfikacja nr gospodarstwa hodowcy | 3 dni | ⭐⭐ |
| **Mobile** — Asia w terenie u prawniczki ma podgląd przez telefon | 10 dni | ⭐⭐ |
| **OCR skanów** — automatyczne wyciąganie daty/numeru z PDF | 4 dni | ⭐⭐⭐ |
| **Symfonia sync** — kontrakt w ZPSP = automatyczne ustawienia w Sage | 3 dni | ⭐⭐⭐⭐ |

---

## 📌 PODSUMOWANIE CZĘŚCI 4

| Co | Status |
|---|---|
| **Stack** | C#/.NET 8.0/WPF/SQL Server + OpenXML 3.4.1 (już w projekcie) |
| **Nowe pliki** | ~15 (services, windows, models, SQL) |
| **Nowe tabele** | 6 (`Kontrakty`, `KontraktyZalaczniki`, `KontraktyAudit`, `KontraktyAlerty`, `KontraktyTemplates`, `KontraktyEskalacjaConfig`) |
| **Effort** | **13 dni Sera + 3 dni Asi** (~3 tygodnie kalendarzowo) |
| **Deadline** | **01.08.2026** (transformacja sp. z o.o. — wszystkie kontrakty migrowane) |
| **Cel ARiMR** | **IX.2026** — 50%+ surowca pod 3-letnimi kontraktami, dashboard pokazuje compliance |

**Ścieżka krytyczna w czasie:**
- **Tydzień 1-2 (26.05 — 06.06):** Faza 1 (fundament) + start migracji z Excela
- **Tydzień 3-4 (09.06 — 20.06):** Faza 2 (Word generator + skany)
- **Tydzień 5-6 (23.06 — 04.07):** Faza 3 (alerty + dashboard)
- **Tydzień 7-8 (07.07 — 18.07):** Asia uzupełnia skany, weryfikuje 100% bazy
- **Tydzień 9-10 (21.07 — 01.08):** Transformacja sp. z o.o. — batch generacji nowych kontraktów
- **Sierpień-wrzesień:** spokojne dopinanie braków, generowanie PDF dla wniosku ARiMR

**Reguła #1:** **Asia jest właścicielką modułu od pierwszego dnia** — Ser pisze, Asia decyduje co działa.
**Reguła #2:** **Magda nie tworzy kontraktów** w pierwszych 3 miesiącach — tylko czyta i eskaluje do Asi.
**Reguła #3:** **Backup folderu skanów** (`\\192.168.0.170\Install\UmowyZakupu\`) — Edyta IT konfiguruje codzienne kopie. **Bez backupu = ryzyko utraty całej dokumentacji ARiMR.**
