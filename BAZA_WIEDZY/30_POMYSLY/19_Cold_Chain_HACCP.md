# 19. ⭐ Cold Chain Compliance Audit (HACCP) — PEŁNY PORADNIK

## Dlaczego to PRIORYTET #1 dla Ciebie

### Audyt branżowy 2026-05-11 stwierdza:
> "BRC v9 sekcja 3: **62% braków**. CCP **0/10 elektronicznie monitorowane**."

**To znaczy**: na 10 punktów krytycznych (Critical Control Points) **żaden** nie ma ciągłego elektronicznego monitoringu. **Wszystko jest na papierze**. Przy audycie BRC v9 to znaczy:
- **Żółta kartka** = poprawki w 28 dni
- **Czerwona kartka** = utrata certyfikatu = **utrata eksportu**

Wasz eksport to ~60-100M PLN/rok. **Bez BRC nie sprzedacie do Lidla, Tesco, Auchana, niemieckich sieci.**

### Co BRC v9 mówi konkretnie (parafraza sek. 4.7, 4.8)
1. Każdy CCP musi być **mierzony ciągle** lub **częstotliwie** (na audycie zapytają jak często)
2. Każde przekroczenie limitu musi być **zarejestrowane**
3. Każdy korekt musi mieć **podpisane** działanie naprawcze
4. Dane muszą być **dostępne** dla auditora w ciągu kilku minut
5. **Brak dokumentacji = niezgodność krytyczna**

---

## CO TO SĄ TWOJE 10 CCP (Critical Control Points)

Wg standardowej analizy HACCP dla ubojni drobiu:

| # | CCP | Limit | Co się dzieje przy przekroczeniu |
|---|---|---|---|
| 1 | **Temp wody w parzelniku** | 50-62°C (zal. od typu) | Niedoskubane (za niska) lub ciemne (za wysoka) |
| 2 | **Temp wody w spin chillerze** (jeśli masz) | <4°C | Bakterie rosną |
| 3 | **Czas chłodzenia tuszki do <4°C** | <6h | Salmonella/Campylobacter mnoży się |
| 4 | **Temp w chłodni stor.** | 0-4°C ciągle | Cold chain break |
| 5 | **Temp w mroźni** | <-18°C | Jakość mięsa spada |
| 6 | **Temp transport ekspedycyjny** | <4°C | Cold chain break |
| 7 | **pH wody (myjka, parzelnik)** | 6.5-8 | Bakterie / korozja |
| 8 | **Stęż. chloru w wodzie** | 20-50 ppm | Skuteczność dezynfekcji |
| 9 | **Czystość powierzchni** (swab test) | <10 CFU/cm² | Higiena |
| 10 | **Temp rdzenia tuszki przed pakowaniem** | <4°C | Cold chain break |

**Z tej listy**: 1, 2, 3, 4, 5, 6, 10 → **temperatury** (możesz objąć JEDNYM systemem)
7, 8 → osobny moduł (pH meter + Cl tester)
9 → swab test (lab, manualnie)

---

## ARCHITEKTURA SYSTEMU

### Hardware
**Wykorzystaj ekosystem z #9 i #18** + dorzuć:

| Punkt | Czujnik | Liczba | Koszt jednostkowy |
|---|---|---|---|
| Parzelnik (CCP #1) | PT1000 (z #9) | 4 | wliczone |
| Spin chiller (CCP #2) | PT1000 zanurzeniowy | 2 | 350 zł |
| Chłodnia (CCP #3, #4) | PT1000 (z #18) | 6 | wliczone |
| Mroźnia (CCP #5) | PT1000 niskotemp -40°C | 4 | 450 zł |
| Transport (CCP #6) | GPS+Temp tracker | 3-5 (per ciężarówka) | 1500 zł |
| Pre-packaging (CCP #10) | Sonda igłowa | 2 | 800 zł |

**Razem nowych czujników**: 8-10 sztuk + 3-5 trackerów transportowych = **~15-25 tys zł**

### Tracker transportowy (CCP #6)
- **Wialon** (system z którego korzystasz w Webfleet) ma temp moduły
- Lub **Berlinger CHT-100** (autonomiczny tracker GPS+Temp, niezależny) — 1500 zł
- Lub **Sensitech TempTale GEO** — premium, ale standard branżowy

---

## DATABASE SCHEMA

```sql
-- LibraNet
CREATE TABLE CCP_Punkt (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Kod NVARCHAR(20) NOT NULL UNIQUE,  -- 'CCP_01_PARZELNIK', 'CCP_03_CHILLING'
    Nazwa NVARCHAR(100) NOT NULL,
    TypPomiaru NVARCHAR(30) NOT NULL,  -- 'TEMP', 'PH', 'CHLOR'
    LimitDolny DECIMAL(8,2) NULL,
    LimitGorny DECIMAL(8,2) NULL,
    JednostkaPomiaru NVARCHAR(10) NOT NULL,  -- '°C', 'pH', 'ppm'
    CzestotliwoscMin INT NULL,  -- co ile minut min. pomiar
    OpisZasad NVARCHAR(500) NULL,
    Aktywny BIT NOT NULL DEFAULT 1
);

CREATE TABLE CCP_Sonda (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    PunktId INT NOT NULL FOREIGN KEY REFERENCES CCP_Punkt(Id),
    KodSondy NVARCHAR(30) NOT NULL UNIQUE,
    NumerSeryjny NVARCHAR(50) NULL,
    ProducentModel NVARCHAR(100) NULL,
    DataKalibracji DATE NULL,
    DataNastepnejKalibracji DATE NULL,
    KalibracjaCertyfikatPath NVARCHAR(300) NULL,
    Aktywna BIT NOT NULL DEFAULT 1
);

CREATE TABLE CCP_Pomiar (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    SondaId INT NOT NULL FOREIGN KEY REFERENCES CCP_Sonda(Id),
    PomiarDateTime DATETIME NOT NULL,
    Wartosc DECIMAL(8,2) NOT NULL,
    PartiaId INT NULL,
    StatusPomiaru NVARCHAR(20) NOT NULL DEFAULT 'AUTOMATYCZNY',  -- AUTOMATYCZNY, MANUALNY
    OperatorId NVARCHAR(50) NULL  -- dla pomiarów manualnych
);
CREATE INDEX IX_CCP_Pomiar_Sonda_DateTime ON CCP_Pomiar(SondaId, PomiarDateTime);
CREATE INDEX IX_CCP_Pomiar_DateTime ON CCP_Pomiar(PomiarDateTime);

CREATE TABLE CCP_Incydent (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    PunktId INT NOT NULL FOREIGN KEY REFERENCES CCP_Punkt(Id),
    SondaId INT NOT NULL FOREIGN KEY REFERENCES CCP_Sonda(Id),
    StartDateTime DATETIME NOT NULL,
    EndDateTime DATETIME NULL,
    WartoscMin DECIMAL(8,2) NULL,
    WartoscMax DECIMAL(8,2) NULL,
    WartoscAvg DECIMAL(8,2) NULL,
    LimitDolny DECIMAL(8,2) NULL,
    LimitGorny DECIMAL(8,2) NULL,
    DotkniePartie NVARCHAR(MAX) NULL,  -- CSV partii dotkniętych
    Priorytet NVARCHAR(20) NOT NULL DEFAULT 'WYSOKI',
    
    -- Korekta
    KorektaDateTime DATETIME NULL,
    KorektaPrzezId NVARCHAR(50) NULL,
    KorektaOpis NVARCHAR(2000) NULL,
    KorektaSkutecznoscOcena INT NULL,  -- 1-5
    
    -- Eskalacja
    Eskalowany BIT NOT NULL DEFAULT 0,
    EskalacjaPrzezId NVARCHAR(50) NULL,
    EskalacjaDateTime DATETIME NULL,
    EskalacjaOpis NVARCHAR(2000) NULL,
    
    StatusFinal NVARCHAR(20) NOT NULL DEFAULT 'OTWARTY'  -- OTWARTY, ZAMKNIETY, ESKALOWANY
);

CREATE TABLE CCP_Kalibracja (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    SondaId INT NOT NULL FOREIGN KEY REFERENCES CCP_Sonda(Id),
    DataKalibracji DATETIME NOT NULL,
    PrzezKogo NVARCHAR(100) NULL,
    ReferenceMethod NVARCHAR(200) NULL,
    WartoscRef DECIMAL(8,2) NULL,
    WartoscZmierzona DECIMAL(8,2) NULL,
    OdchylenieAbs DECIMAL(8,2) NULL,
    WynikiOkText NVARCHAR(500) NULL,
    CertyfikatPath NVARCHAR(300) NULL,
    NastepnaDataKalibracji DATE NULL
);

-- Tabela cold chain trips dla transportu (CCP #6)
CREATE TABLE CCP_TransportTrip (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    PojazdId INT NULL,  -- FK do Pojazd
    TrackerId NVARCHAR(50) NOT NULL,
    StartDateTime DATETIME NOT NULL,
    EndDateTime DATETIME NULL,
    KlientId INT NULL,
    PartieIds NVARCHAR(MAX) NULL,  -- CSV partii
    TempMin DECIMAL(5,2) NULL,
    TempMax DECIMAL(5,2) NULL,
    TempAvg DECIMAL(5,2) NULL,
    OdchyleniaCount INT NOT NULL DEFAULT 0,
    StatusFinal NVARCHAR(20) NULL  -- 'OK', 'NARUSZENIE'
);
```

---

## SOFTWARE — Centralny BackgroundWorker

**Plik**: `Services/CCPMonitoringService.cs`

```csharp
public class CCPMonitoringService : BackgroundService
{
    private readonly ScaldingMonitorService _scalding;
    private readonly ChillingMonitorService _chilling;
    private readonly FreezerMonitorService _freezer;
    private readonly TransportTrackerService _transport;
    private readonly INotificationService _notif;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                // Pomiar wszystkich punktów
                await ProcessScalding();    // CCP 1, 2
                await ProcessChilling();    // CCP 3, 4
                await ProcessFreezer();     // CCP 5
                await ProcessTransport();   // CCP 6
                await ProcessPrePackaging();// CCP 10

                // Detekcja incydentów
                await DetectIncidents();

                // Sprawdzenie czy kalibracje aktualne
                await CheckCalibrationDeadlines();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "CCP monitoring failure");
            }

            await Task.Delay(TimeSpan.FromMinutes(1), ct);
        }
    }

    private async Task DetectIncidents()
    {
        // Dla każdego CCP w ciągu ostatnich 5 min — sprawdź czy są wartości poza limitem
        const string sql = @"
            SELECT TOP 100 p.Id, p.SondaId, p.PomiarDateTime, p.Wartosc, p.PartiaId,
                   s.PunktId, cp.LimitDolny, cp.LimitGorny, cp.Kod
            FROM CCP_Pomiar p
            JOIN CCP_Sonda s ON s.Id = p.SondaId
            JOIN CCP_Punkt cp ON cp.Id = s.PunktId
            WHERE p.PomiarDateTime >= DATEADD(MINUTE, -5, GETDATE())
              AND ((cp.LimitDolny IS NOT NULL AND p.Wartosc < cp.LimitDolny)
                OR (cp.LimitGorny IS NOT NULL AND p.Wartosc > cp.LimitGorny))
            ORDER BY p.PomiarDateTime DESC";

        // Dla każdego z odchyleń → sprawdź czy istnieje już otwarty incydent
        // Jeśli nie → utwórz nowy + wyślij alert
        // Jeśli tak → przedłuż czas trwania
    }

    private async Task CheckCalibrationDeadlines()
    {
        const string sql = @"
            SELECT KodSondy, DataNastepnejKalibracji
            FROM CCP_Sonda
            WHERE Aktywna = 1
              AND (DataNastepnejKalibracji IS NULL 
                OR DataNastepnejKalibracji <= DATEADD(DAY, 14, GETDATE()))";
        // ... powiadom o zbliżającej się kalibracji
    }
}
```

---

## UI — Dashboard CCP

**Plik**: `Produkcja/Views/CCP_Dashboard.xaml`

```
┌─────────────────────────────────────────────────────────────┐
│  🛡 COLD CHAIN CCP DASHBOARD   2026-05-23 14:23 ⚪ LIVE      │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  STAN WSZYSTKICH CCP:                                       │
│                                                             │
│  ┌─────────────────────────────┐  ┌─────────────────────┐ │
│  │ CCP #1 PARZELNIK            │  │ CCP #2 SPIN CHILLER │ │
│  │ ━━━━━━━━━━━━━━━━━━━━━━━━━━ │  │ ━━━━━━━━━━━━━━━━━━━ │ │
│  │ 52.3°C  (norma 50-62°C)  ✓  │  │ 2.1°C  (norma <4)  ✓ │ │
│  │ Ostatni pomiar: 14:22       │  │ Ostatni: 14:22      │ │
│  └─────────────────────────────┘  └─────────────────────┘ │
│                                                             │
│  ┌─────────────────────────────┐  ┌─────────────────────┐ │
│  │ CCP #3 CHILLING TIME        │  │ CCP #4 COLD STORE   │ │
│  │ ━━━━━━━━━━━━━━━━━━━━━━━━━━ │  │ ━━━━━━━━━━━━━━━━━━━ │ │
│  │ 3.5h śr. (norma <6h)     ✓  │  │ 2.8°C  (norma 0-4) ✓ │ │
│  │ Najgorsza partia: 5.2h ⚠    │  │ Stab. od 8h     ✓   │ │
│  └─────────────────────────────┘  └─────────────────────┘ │
│                                                             │
│  ┌─────────────────────────────┐  ┌─────────────────────┐ │
│  │ CCP #5 MROŹNIA              │  │ CCP #6 TRANSPORT    │ │
│  │ ━━━━━━━━━━━━━━━━━━━━━━━━━━ │  │ ━━━━━━━━━━━━━━━━━━━ │ │
│  │ -20.5°C (norma <-18)    ✓   │  │ 3 aktywne kursy:    │ │
│  │ Drzwi otwarte: 0min od 11:00│  │ K1: 3.2°C ✓         │ │
│  │                              │  │ K2: 4.6°C ⚠ alert  │ │
│  │                              │  │ K3: 2.8°C ✓         │ │
│  └─────────────────────────────┘  └─────────────────────┘ │
│                                                             │
│  ⚠ AKTYWNE INCYDENTY (1):                                  │
│  • 14:18 CCP #6 K2 (Lidl Auchan #1247) — temp 4.6°C       │
│    czas trwania: 5 min, kierowca powiadomiony              │
│                                                             │
│  📊 KPI DZIEŃ:                                             │
│  Compliance rate: 99.2% (4 incydenty z 487 pomiarów)       │
│  Czas korekty śr.: 8 min (cel <15 min)                     │
│                                                             │
│  [📄 Raport BRC v9]  [📥 Eksport CSV]  [🔧 Kalibracje]    │
└─────────────────────────────────────────────────────────────┘
```

---

## RAPORT BRC v9 (one-click)

### Co zawiera
1. **Strona tytułowa**: zakład, okres, podpis QM
2. **Streszczenie**:
   - Liczba CCP monitorowanych
   - Compliance rate (% pomiarów w normie)
   - Liczba incydentów + status korekt
3. **Per CCP**:
   - Min/max/avg za okres
   - Wykres przebiegu czasowego
   - Lista wszystkich incydentów + korekt + skuteczność
4. **Kalibracje**:
   - Lista sond + daty kalibracji
   - Certyfikaty referencyjne (załączone)
5. **Działania korygujące**:
   - Wszystkie korekty z opisami
   - Trendy poprawy
6. **Załączniki**:
   - Procedury HACCP
   - Plan kalibracji
   - Lista personelu odpowiedzialnego

### Generacja (QuestPDF)
```csharp
public class BRCv9ReportGenerator
{
    public byte[] Generate(DateTime od, DateTime doDate)
    {
        return Document.Create(c =>
        {
            c.Page(p =>
            {
                p.Header().Element(BuildTitle);
                p.Content().Column(col =>
                {
                    col.Item().Element(BuildExecutiveSummary);
                    col.Item().PageBreak();
                    
                    foreach (var ccp in GetAllCCP())
                    {
                        col.Item().Element(c => BuildCCPSection(c, ccp, od, doDate));
                        col.Item().PageBreak();
                    }
                    
                    col.Item().Element(c => BuildCalibrationSummary(c, od, doDate));
                    col.Item().Element(c => BuildCorrectiveActions(c, od, doDate));
                });
                p.Footer().AlignCenter().Text(t => 
                {
                    t.Span("BRC v9 Compliance Report — Generated automatically by ZPSP");
                });
            });
        }).GeneratePdf();
    }
}
```

---

## WORKFLOW INCYDENTU

```
14:18 — Sonda CCP #6 (transport K2) zaraportowała 4.6°C
        ↓
        Service detektuje (>4°C, limit)
        ↓
        Tworzy rekord CCP_Incydent (status: OTWARTY)
        ↓
        Wysyła powiadomienie:
        - SMS do kierowcy K2: "Sprawdź temp ładunku!"
        - SMS do dyspozytora: "Incydent K2 4.6°C"
        - Push w app dla QM
        ↓
14:23 — Kierowca odpowiada: "wszystko OK, drzwi otwarte przy załadunku"
        ↓
        QM ocenia → potwierdza korekta:
        - Wybiera "Drzwi otwarte przy załadunku - akceptowalne"
        - Wskazuje partie dotknięte: ZADNE (krótki epizod)
        - Klika ZAMKNIJ INCYDENT
        ↓
14:25 — Status: ZAMKNIETY
        Czas trwania: 7 min
        Korekta: udokumentowana
        ↓
        W raporcie BRC: ten incydent z opisem korekty
```

---

## ALERTY I ESKALACJA

### Poziomy alertów
1. **Pre-alert** (90% limitu): info do QM
2. **Alert** (przekroczenie): SMS + push + email
3. **Escalation** (przekroczenie >15 min): kierownik produkcji
4. **Critical** (przekroczenie >30 min): dyrektor, blokada partii

### Konfigurowalne
```sql
CREATE TABLE CCP_AlertRule (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    PunktId INT NOT NULL,
    PoziomEskalacji INT NOT NULL,  -- 1, 2, 3
    CzasOdIncydentuMin INT NOT NULL,  -- po ilu min do tego poziomu
    OdbiorcyEmail NVARCHAR(MAX) NULL,
    OdbiorcySms NVARCHAR(MAX) NULL,
    AkcjaAutomatyczna NVARCHAR(100) NULL  -- 'BLOK_PARTII', 'AUDIO_ALARM'
);
```

---

## INTEGRACJA Z EXISTING ZPSP

### Powiązania
- **listapartii** — partia dotknięta incydentem (`CCP_Incydent.DotkniePartie`)
- **PartiaStatus** — auto-zmiana statusu partii (np. CLOSED_INCOMPLETE) jeśli incydent krytyczny
- **Reklamacje** — jeśli klient zgłasza problem, automat. pokazać incydenty CCP z dnia produkcji
- **Hodowcy** — w raporcie miesięcznym hodowcy pokazać % partii z incydentami CCP

### Menu
- `accessMap[68] = "CCP_Dashboard"` (nowa pozycja)
- Kategoria: PRODUKCJA I MAGAZYN
- Ikona: 🛡

---

## KALIBRACJE — system zarządzania

### Harmonogram
- **PT1000**: co 6 mies. weryfikacja, co 12 mies. pełna kalibracja
- **Spin chiller sonda**: co 3 mies. (środowisko agresywne)
- **Trackery transportowe**: co 12 mies.
- **Sondy igłowe**: co 3 mies. (mechaniczne uszkodzenia)

### Workflow kalibracji
1. System wysyła powiadomienie 14 dni przed terminem
2. QM zleca firmie zewnętrznej (np. **GUM**, **Polmetrol**)
3. Po kalibracji upload certyfikatu PDF do `CCP_Kalibracja.CertyfikatPath`
4. Aktualizacja `CCP_Sonda.DataNastepnejKalibracji`
5. Jeśli sondy nie da się skalibrować → automatic alert do dyrektora

### Koszt kalibracji
- PT1000: 80-150 zł/szt × 12 = ~1500 zł/rok
- Sondy spec. (mroźnia): 300 zł/szt × 4 = 1200 zł/rok
- Trackery: 200 zł/szt × 5 = 1000 zł/rok
- **TOTAL: ~4-5 tys zł/rok** kalibracji

---

## CZAS IMPLEMENTACJI

| Faza | Czas | Koszt |
|---|---|---|
| **FAZA 1** — Hardware setup (8 sond + tracery + szafa) | 1 tydzień | 15-25 tys zł |
| **FAZA 2** — Database + serwisy (CCP_MonitoringService) | 40h | — |
| **FAZA 3** — Dashboard + UI | 32h | — |
| **FAZA 4** — Raport BRC v9 generator (QuestPDF) | 24h | $99/rok |
| **FAZA 5** — Workflow incydentów + alerty | 20h | SMS API ~500 zł/rok |
| **FAZA 6** — Kalibracje + procedury | 16h | — |
| **FAZA 7** — Pilot 2 tygodnie + szkolenie QM | 80h pracy QM | — |
| **RAZEM** | **~3 mies. wdrożenia** | **~17-27 tys zł setup + ~6 tys/rok** |

---

## SCENARIUSZE AUDYTU BRC v9 (kiedy okaże swoją wartość)

### Scenariusz 1: Niespodziewany audyt 9:00
- Auditor BRC pyta: "Pokaż mi temp chłodni za marzec"
- **Przed systemem**: 30 minut szukania po teczkach, brak danych z 3 dni (papier zginął)
- **Z systemem**: 3 kliknięcia → PDF z wykresami za cały marzec → auditor zadowolony

### Scenariusz 2: Reklamacja Salmonella
- Klient zgłasza Salmonella w produkcie z 12.04
- **Przed systemem**: nie wiesz czy chłodnia w tym dniu była OK
- **Z systemem**: raport pokazuje: "12.04 chłodnia 1.9°C avg, 0 incydentów, czas chłodzenia 4.2h" → problem nie tu

### Scenariusz 3: Nagły incydent w nocy
- 03:00 — kompresor padł, chłodnia rośnie z 2°C
- **Przed systemem**: rano operator zauważa, partie z 6h nocy zagrożone
- **Z systemem**: o 03:15 SMS do mechanika dyżurnego → naprawa o 04:00 → tylko 1h naruszenia → tylko 1 partia do oceny zamiast 6

---

## RYZYKA

⚠️ **Sondy są drogie i kruche** — przygotuj zapasy (po 1 sztuce każdego typu)
⚠️ **Awaria sieci = brak danych** — gateway musi mieć **lokalny buffer 7 dni**
⚠️ **Personel sceptyczny** — zaprezentuj jako "chroni Was przed audytem"
⚠️ **Trackery transportowe mają baterie** — kontroluj stan (alert <20%)
⚠️ **Kalibracja zaniedbana = dane niewiarygodne** — pilnuj jak oka w głowie
