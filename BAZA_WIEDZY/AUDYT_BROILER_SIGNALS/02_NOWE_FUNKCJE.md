# 02. Nowe funkcje do ZPSP — 12 obszarów

> Każda funkcja: **co**, **model danych** (SQL/tabele), **gdzie wpina się w UI** (WPF), **integracje**, **KPI/raport**, **szacunek finansowy** (z założeniami), **BRC v9**.
> Stack zostaje: C# / .NET 8 / WPF / SQL Server. Nazwy tabel: prefix `BS_` (Broiler Signals).
> Wszystkie kwoty PLN przy założeniu **70 000 ptaków/dzień × 250 dni roboczych = 17,5 mln ptaków/rok, średnia waga karkasu ~2,0 kg = 35 000 t/rok, średnia cena tuszki ~12 zł/kg**.

---

## NF01 — Scorecard hodowcy: FPD + hock burn + skin lesions

### Problem (książka, s. 39-44, 113)
- **Footpad dermatitis** ma formułę regulacyjną: `(score1 × 0.5 + score2 × 2) / total × 100`. Progi: <80 OK, 80-120 warning, >120 wymóg redukcji stocking density.
- **Hock burn** wpływa na value of legs (gorsze nogi = niższa cena rynkowa).
- **Skin lesions/scratches** = zazwyczaj thinning lub stress 3 tyg., łatwe do attribution.
- Książka wprost: "average scores of several flocks over an entire calendar year are taken into account" (s. 40).

### Co robię w ZPSP
- Dodaje moduł `BS_FPD_Scoring` w `KontrolaPM/` (nowy folder lub jako tab w istniejącym `Partie/`).
- Operator weterynaryjny przy zawieszaniu/pre-PM loguje co 30 min losową próbkę 50 ptaków: liczba klasy 0/1/2 FPD, liczba hock burn (Y/N), liczba scratches > 3cm.
- Na koniec partii: automatyczne wyliczenie scoru.

### Model danych
```sql
CREATE TABLE BS_FlockScoring (
    Id              INT IDENTITY PRIMARY KEY,
    PartiaId        INT NOT NULL,           -- FK listapartii
    SampleTs        DATETIME NOT NULL,
    SampleSize      INT NOT NULL,           -- ile ptaków oceniono
    FPD_Score0      INT NOT NULL,
    FPD_Score1      INT NOT NULL,
    FPD_Score2      INT NOT NULL,
    HockBurn_Count  INT NOT NULL,
    Scratches_Count INT NOT NULL,           -- > 3cm
    OperatorId      INT NOT NULL,
    Foto_BlobId     UNIQUEIDENTIFIER NULL,
    Uwagi           NVARCHAR(500) NULL
);

CREATE TABLE BS_HodowcaScorecard (   -- snapshot na koniec partii
    Id              INT IDENTITY PRIMARY KEY,
    HodowcaId       INT NOT NULL,
    PartiaId        INT NOT NULL,
    DataObliczenia  DATETIME NOT NULL,
    FPD_Index       DECIMAL(6,2),           -- (score1*0.5 + score2*2)/total*100
    HockBurn_Pct    DECIMAL(5,2),
    Scratches_Pct   DECIMAL(5,2),
    DOA_Pct         DECIMAL(5,3),           -- z NF03
    CatchInjury_Pct DECIMAL(5,3),           -- z NF03
    AntybioCleanFlag BIT NOT NULL DEFAULT 0,-- z NF02
    Punktacja       INT NOT NULL,           -- 0-100 syntetyczny score
    Notyfikacja     NVARCHAR(200) NULL      -- "FPD >120 → reduce stocking next round"
);
CREATE INDEX IX_BS_HodowcaScorecard_Hodowca ON BS_HodowcaScorecard(HodowcaId, DataObliczenia DESC);
```

### UI w WPF
- Nowa zakładka w `Hodowcy/Views/HodowcaProfileWindow` → "Scorecard 12-miesięczny".
- Wykres LiveCharts: 4 linie (FPD, HockBurn, Scratches, DOA) — kolory ze CLAUDE.md (czerwony >120 / żółty 80-120 / zielony <80).
- Aplet inspektorski `BS_FPDLogger.xaml` — duży tablet-friendly UI: 3 przyciski klasy 0/1/2 + licznik + szybkie zdjęcie z webcam (Hikvision DeviceManager już mam w `CentrumNagranAI/`).
- Eksport PDF "Hodowca Scorecard 12m" → wysyłka do hodowcy 1× miesiąc (Gmail MCP).

### Integracje
- LibraNet `listapartii` (PartiaId, HodowcaId).
- Hikvision CCTV API (już mam w CNA — fotodokumentacja).
- ARiMR `AnimNo` (z `Hodowcy/`).

### KPI
- **FPD Index** miesięczny per hodowca (norma <80).
- **% partii > 120** w roku (cel: <5%).
- **Wpływ na cenę żywca** — propozycja: -2% za FPD 80-120, -5% za >120, +2% za <40 (premia).

### Szacunek finansowy
- Założenia: 5% partii ma FPD >120 → bez systemu hodowca dostaje pełną cenę. Z systemem -5% × ~250 zł/szt × 7000 szt/partia × 5% partii × 140 partii/rok ≈ **600 000 zł/rok presji jakościowej** (przeniesienie ryzyka na hodowcę, redukcja rejection downstream).
- Redukcja rejection PM o 0.2 pkt% (z 1.0% → 0.8%): 17,5 mln szt × 0.002 × 2 kg × 12 zł = **840 000 zł/rok**.

### BRC v9
- **Sekcja 5.1** Product design — dokumentacja parametrów surowca.
- **Sekcja 3.4** Internal audits — scoring jako audit trail.

---

## NF02 — Rejestr antybiotyków i okresów karencji (MRL)

### Problem (książka, s. 31-33, 49)
- MRL (Maximum Residue Limit) — monthly testing przez regulatora.
- Withdrawal period MUSI być spełniony przed ubojem.
- Książka: trzy poziomy antybiotyków (1st/2nd/3rd choice), 3rd zakazany jako routine.
- ESBLs i resistance — pressure na redukcję.

### Co robię w ZPSP
- W `Hodowcy/` nowy formularz "Karta zdrowia stada" wypełniany przez weterynarza farmy + e-podpis.
- W `Partie/` system blokuje status `APPROVED` jeżeli karencja niespełniona (alert czerwony przy `VET_CHECK`).
- W `MarketIntelligence/` (Briefing) dashboard: ile partii ma "antybio-clean" stamp.

### Model danych
```sql
CREATE TABLE BS_Antybiotyk (
    Id              INT IDENTITY PRIMARY KEY,
    Nazwa           NVARCHAR(100) NOT NULL,
    Substancja      NVARCHAR(100),
    KategoriaWHO    NVARCHAR(20),           -- 1st/2nd/3rd choice
    WithdrawalDays  INT NOT NULL,           -- domyślne dni karencji
    MRL_mg_kg       DECIMAL(8,4) NULL,
    EUMaxDosage     DECIMAL(8,3) NULL
);

CREATE TABLE BS_FarmTreatment (
    Id              INT IDENTITY PRIMARY KEY,
    HodowcaId       INT NOT NULL,
    PartiaId        INT NULL,               -- może być NULL jeśli przed wiekiem partii
    AntybiotykId    INT NOT NULL,
    DataPodania     DATE NOT NULL,
    DataKonca       DATE NOT NULL,          -- ostatni dzień podania
    Dawka           NVARCHAR(50),
    Powod           NVARCHAR(200),
    VetSignature    NVARCHAR(200),          -- nazwisko weterynarza farmy
    Skan_BlobId     UNIQUEIDENTIFIER NULL,  -- skan recepty
    DataMozliwegoUboju AS DATEADD(DAY, (SELECT WithdrawalDays FROM BS_Antybiotyk WHERE Id = AntybiotykId), DataKonca) PERSISTED
);

CREATE TABLE BS_ResidueTest (        -- testy oficjalne przez regulatora
    Id              INT IDENTITY PRIMARY KEY,
    PartiaId        INT NOT NULL,
    Lab             NVARCHAR(100),
    DataPobrania    DATE NOT NULL,
    Wynik           NVARCHAR(20),           -- OK / DETECTED / EXCEEDED
    Substancja      NVARCHAR(100) NULL,
    Wartosc_mg_kg   DECIMAL(8,4) NULL,
    DokumentId      UNIQUEIDENTIFIER NULL
);
```

### UI w WPF
- `Hodowcy/Views/FarmHealthRecord.xaml` — tablica leczeń, kalendarz z czerwonymi blockami "uboju zabronionego do DD-MM".
- W `Partie/Views/NowaPartiaDialog.xaml` — sprawdzenie: jeśli `Hodowca.AntybioBlockTo > Partia.DataPlanowana` → blok z czerwonym alertem.
- Dashboard `MarketIntelligence/Briefing` — kafelek "Antybio-free flocks last 90d: X%".

### Integracje
- IRZplus — możliwy bidirectional (ARiMR ma swój rejestr leczeń, ale eksport może być uboższy).
- Email do weterynarza farmy (Gmail MCP) jako reminder pre-slaughter check.

### KPI
- **% partii z antybio-treatment w ostatnich 30 dniach** (cel: <30%, w UK norma ~25%).
- **Compliance withdrawal period** — 100% (każda blokada to zapis w audit log).
- **Residue test pozytywne** — 0 oczekiwane.

### Szacunek finansowy
- Pojedynczy incident niespełnionej karencji = rejection całej partii + recall + kara ARiMR/IW ≈ 100-500 tys. zł. Pokazanie BRC auditorowi pełnego rejestru → mniejsze ryzyko utraty certyfikatu (wartość certyfikatu = pełen dostęp do retail UE).
- Pomysł na premium marketing "antybio-free dla wybranych klientów" — nawet +5% cena dla 10% wolumenu = **210 000 zł/rok** (przy 35k t × 10% × 5% × 12 zł).

### BRC v9
- **Sekcja 5.4** Product release — dokumentacja withdrawal compliance.
- **IFS v8 sekcja 4** — supplier control.

---

## NF03 — Transport welfare: DOA + heat stress + 9-point welfare index

### Problem (książka, s. 73-77)
- Każde +15 min czasu transportu = **+6% DOA**, każde +15 min waiting na slaughterhouse = +3% DOA (s. 73).
- Limit DOA: **0.5% max, średnia ~0.2%**.
- **Welfare index** 9 punktów: mortality, fractures, trapped body parts, supine, haematomas, splayed legs, crowding, thermal stress, rejections.
- Heat stress: górny limit **25°C / 70% RH**, journey > 4h wymaga ventilation 0.3-1.0 m/s.

### Co robię w ZPSP
- Doposażam pojazdy w czujnik temp+RH (Bluetooth/4G) — dane idą do `TransportPL`.
- Przy odbiorze na rampie: tablet w ręku weterynarza wprowadza DOA + 9-pkt welfare za 2-min próbki × 2.
- Wyliczanie welfare index, scoring kierowcy, scoring catch team, scoring hodowcy.

### Model danych
```sql
CREATE TABLE BS_TransportClimat (
    Id              INT IDENTITY PRIMARY KEY,
    KursId          INT NOT NULL,           -- FK TransportPL.Kurs
    PomiarTs        DATETIME NOT NULL,
    Temperatura     DECIMAL(4,1),
    Wilgotnosc      DECIMAL(4,1),
    Pozycja         NVARCHAR(20),           -- "FRONT_TOP", "CENTER", "REAR_BOTTOM"
    AmbientTemp     DECIMAL(4,1) NULL,      -- z OpenWeatherAPI po GPS
    StatusFlag      NVARCHAR(20)            -- "OK","HOTSPOT","COLD_RISK"
);
CREATE INDEX IX_BS_TransportClimat_Kurs ON BS_TransportClimat(KursId, PomiarTs);

CREATE TABLE BS_RampInspection (
    Id              INT IDENTITY PRIMARY KEY,
    KursId          INT NOT NULL,
    PartiaId        INT NOT NULL,
    TsArrival       DATETIME NOT NULL,
    TsSlaughterStart DATETIME NULL,
    WaitingMinutes  AS DATEDIFF(MINUTE, TsArrival, TsSlaughterStart) PERSISTED,
    TotalBirds      INT NOT NULL,
    DOA_Count       INT NOT NULL,
    DOA_Pct         AS CAST(DOA_Count AS DECIMAL(10,3))/NULLIF(TotalBirds,0)*100 PERSISTED,
    Fractures_Count INT,
    Trapped_Count   INT,
    Supine_Count    INT,
    Haematomas_Count INT,
    SplayedLegs_Count INT,
    Crowding_Score  TINYINT,                -- 0-3
    ThermalStress_Score TINYINT,            -- 0-3
    RejectionsAtRamp_Count INT,
    WelfareIndex    AS (...)                -- formuła w widoku
    FotoUrls        NVARCHAR(MAX) NULL      -- JSON z linkami do Hikvision
);
CREATE INDEX IX_BS_Ramp_Partia ON BS_RampInspection(PartiaId);
```

### UI w WPF
- Nowy widok `MapaFloty/WidokTransportCCP.xaml` — heatmap czasu transport vs DOA per kurs.
- Tablet view `Transport/Views/RampInspectionTablet.xaml` — duże przyciski +/-, kolory zgodnie z 9-pkt welfare index.
- W `Flota/` scoring kierowcy: TOP 3 / BOTTOM 3 wg DOA-per-kurs.
- W `Hodowcy/` scoring per partia: average waiting time + DOA before slaughter (cel: <2h waiting).

### Integracje
- Czujniki temp+RH w naczepach (jeden vendor: SensorPush ~150 zł/szt, lub własny ESP32 z LTE shield ~300 zł).
- WebFleet GPS już mam — łączenie po `KursId` + timestamp.
- Hikvision NVR (mam) — auto-kadry na rampie przy DOA event.

### KPI
- **DOA średni miesięczny** (cel: <0.2%).
- **Welfare Index per kurs** (cel: >85/100).
- **Czas catch-to-stun** średni (cel: <4h, alert: >6h, blok: >12h).
- **% kursów z heat stress alert** (cel: <2%).

### Szacunek finansowy
- Bez systemu: 0.2% DOA = 17,5M × 0.002 × 2 kg × 12 zł = **840 000 zł/rok wartość DOA**. Redukcja do 0.15% (-25%) = **210 000 zł/rok oszczędności**.
- Kara za welfare violation EU 1/2005: do 50 000 € jednorazowo. Audit BRC/IFS — welfare to wymóg.
- Wartość: 6 czujników × 12 pojazdów × 150 zł = **10 800 zł CAPEX** (zwrot < 1 miesiąc).

### BRC v9
- **Sekcja 4.2** Animal welfare/PRP (Pre-Requisite Programmes).
- **IFS v8 sekcja 5** — environmental control.

---

## NF04 — Stunning CCP monitor (water bath + CAS)

### Problem (książka, s. 84-92)
- EU Reg 1099/2009: minimum 4s stunning, parametry konkretne:
  - <200 Hz → ≥100 mA
  - 200-400 Hz → ≥150 mA
  - 400-1500 Hz → ≥200 mA
- "Purple broiler after scalder" = krytyczny sygnał (alive into scalding).
- Red wing tips = sygnał blood flapping/stunning issue.
- CAS: temperatura gazu blisko body temp, CO2 gradient 2-step.

### Co robię w ZPSP
- Nowy moduł `Stunning/` — sensors data ingestion z PLC linii ubojowej.
- Real-time dashboard "Stunning Bay" — bieżąca V/Hz/mA + alerty.
- Codzienny snapshot do `BS_StunningLog` agregowany per shift.
- Foto-pre-check po scalder via Hikvision (kamera nad linią po parzelniku) — Claude AI VLM (mam w CNA) wykrywa purple birds.

### Model danych
```sql
CREATE TABLE BS_StunningSession (
    Id              INT IDENTITY PRIMARY KEY,
    LiniaId         INT NOT NULL,
    StartTs         DATETIME NOT NULL,
    EndTs           DATETIME NULL,
    Metoda          NVARCHAR(20) NOT NULL,  -- 'WATER_BATH' / 'CAS_CO2' / 'CAS_ARGON'
    PartiaId        INT NULL,               -- jeśli partia segregowana
    OperatorId      INT NOT NULL
);

CREATE TABLE BS_StunningParam (
    Id              INT IDENTITY PRIMARY KEY,
    SessionId       INT NOT NULL,
    Ts              DATETIME NOT NULL,
    -- Water bath
    Voltage_V       DECIMAL(6,1) NULL,
    Frequency_Hz    INT NULL,
    Current_mA      INT NULL,
    DurationSec     DECIMAL(4,1) NULL,
    -- CAS
    CO2_Pct_Step1   DECIMAL(4,1) NULL,
    CO2_Pct_Step2   DECIMAL(4,1) NULL,
    GasTemp_C       DECIMAL(4,1) NULL,
    -- Compliance
    EUCompliantFlag BIT,                    -- vs EC1099 thresholds
    AlertMsg        NVARCHAR(200) NULL
);
CREATE INDEX IX_BS_StunningParam_Session ON BS_StunningParam(SessionId, Ts);

CREATE TABLE BS_StunningQuality (    -- z post-scalder camera + VLM
    Id              INT IDENTITY PRIMARY KEY,
    SessionId       INT NOT NULL,
    Ts              DATETIME NOT NULL,
    SampleSize      INT NOT NULL,
    PurpleBirds_Cnt INT,                    -- ALIVE INTO SCALDER — krytyczne
    RedWingTips_Cnt INT,
    PoorBleeding_Cnt INT,
    HaematomasShoulder_Cnt INT,
    VLM_Confidence  DECIMAL(4,3),           -- 0-1 z Claude VLM
    Fotos_Json      NVARCHAR(MAX)
);
```

### UI w WPF
- `Stunning/Views/StunningBayDashboard.xaml` — live KPI + alert czerwony "PURPLE BIRD DETECTED" → SMS do supervisor.
- Trend: ostatnie 24h V/Hz/mA + waterbath water level.
- Raport miesięczny: % compliance EU 1099/2009 (cel: 100%).

### Integracje
- PLC linii ubojowej — Modbus TCP / OPC UA. Wymaga uzgodnienia z dostawcą linii (Marel/Foodmate/Meyn).
- Hikvision NVR + Claude Sonnet 4.6 VLM (już mam pipeline w `CentrumNagranAI/`).

### KPI
- **% Purple birds** (cel: 0, alert: >0).
- **% Compliance Hz/mA** (cel: 100% pomiarów).
- **Red wing tips %** (cel: <2%).

### Szacunek finansowy
- BRC v9 sek. 4 (Process Control) — bez tego prawdopodobnie auditor wystawi major NC. Wartość: utrzymanie BRC = dostęp do ~30% obrotu (klienci wymagający).
- Redukcja wing/breast haematomas o 3% (z 5% → 2%) = 17,5M × 0.03 × 0.4 kg/wing × 12 zł = **2 520 000 zł/rok** (wartość trimmed wings → rejection lub downgrade).

### BRC v9
- **Sekcja 4.2** (process control), **4.3** (CCP monitoring) — **kluczowy gap**.

---

## NF05 — Scalding + Plucking monitor (temperatura + finger wear)

### Problem (książka, s. 99-107)
- Scalding temp precyzja **0.1-0.2°C** — różnica kilku stopni = inny rezultat.
- Low-temp (~52°C) zachowuje epidermę, high-temp (~58°C) ją usuwa (do frozen meat).
- Plucking finger wear: ~20 dni żywotności, wymiana **1 finger/1000 ptaków**, ~200/dzień przy 200k.
- Skin ruptures >3cm = trim, ruptures > 50% wartości carcass.

### Co robię w ZPSP
- Czujnik temp w parzelniku (już zwykle jest, ale wgrywamy do bazy).
- Skaner finger replacement log — operator codziennie skanuje QR plucker stations + wprowadza liczbę wymienionych.
- Camera-based: skin rupture count na linii post-plucking (Claude VLM).

### Model danych
```sql
CREATE TABLE BS_ScaldingLog (
    Id              INT IDENTITY PRIMARY KEY,
    SesjaId         INT NOT NULL,            -- powiązany ze StunningSession
    Ts              DATETIME NOT NULL,
    TankNr          INT NOT NULL,
    TempC           DECIMAL(4,2) NOT NULL,
    SetpointC       DECIMAL(4,2) NOT NULL,
    DeviationC      AS (TempC - SetpointC) PERSISTED,
    AlertFlag       BIT
);
CREATE INDEX IX_BS_Scalding_Sesja ON BS_ScaldingLog(SesjaId, Ts);

CREATE TABLE BS_PluckerMaintenance (
    Id              INT IDENTITY PRIMARY KEY,
    DataObslugi     DATE NOT NULL,
    StationNr       INT NOT NULL,
    FingersReplaced INT NOT NULL,
    TotalFingers    INT NOT NULL,
    OperatorId      INT NOT NULL,
    PoorPluckingObs NVARCHAR(200) NULL
);

CREATE TABLE BS_PluckingQuality (
    Id              INT IDENTITY PRIMARY KEY,
    SesjaId         INT NOT NULL,
    Ts              DATETIME NOT NULL,
    SampleSize      INT NOT NULL,
    SkinRuptures_Small INT,                  -- <3cm
    SkinRuptures_Large INT,                  -- >3cm → trim
    FeathersRemaining_Cnt INT,
    FaecalContamination_Cnt INT,
    VLM_Confidence  DECIMAL(4,3)
);
```

### UI w WPF
- Tab w `Stunning/` (lub osobne `Patroszenie/`) — wykres temp parzelnika ostatnich 8h, alert deviation >1°C.
- `BS_PluckerLogger.xaml` (tablet) — checklist 4 stations × 2× dziennie.

### KPI
- **Średnia deviation scalder od setpoint** (cel: <0.3°C).
- **Skin rupture rate** (cel: <2%).
- **Plucker finger replacement rate** (cel: 200/dzień przy 70k = ~70 fingers).
- **Pełne wymiany pluckera per 20 dni** — alert jeśli pominięte.

### Szacunek finansowy
- Bez monitoringu: scalder za gorący = scalded meat (rejection) ~0.5% partii × 7000 szt × 2 kg × 12 zł = 84 000 zł/incident. 2 incydenty/rok → **168 000 zł/rok**.
- Skin rupture redukcja z 5% → 3%: 17,5M × 0.02 × 0.5 kg trim × 12 zł = **2 100 000 zł/rok**.

### BRC v9
- **Sekcja 4.6** Equipment maintenance.
- **Sekcja 4.10** Critical control points.

---

## NF06 — PM defects digital inspection (tablet w rękach weterynarza)

### Problem (książka, s. 113-117, 122-151)
- Inspektor ma ~0.5s na 1 ptak (7000/h per platform).
- Top 3 powody odrzucenia: **polyserositis**, flock-linked inflammations, **ascites**.
- Target rejection rate: <0.5% (EU avg 1.0%).
- Kategoryzacja: complete reject vs partial (trim) — różne dla różnych wad.
- ~14 wad strukturalnych: ascites, polyserositis, hepatitis, cellulitis, cachexia, fractures, dislocations, haematomas extensive, faecal/bile contamination, abnormal colour, BCO/femur necrosis, tibial necrosis, kinky back, BBS, TD, wooden breast, white striping, spaghetti meat, GMD, DMP.

### Co robię w ZPSP
- Tablet `Patroszenie/PMInspectionTablet.xaml` — 14 dużych przycisków per wada + zdjęcie + complete/partial.
- Auto-attribution do `PartiaId` (timestamp + shackle counter).
- Daily report: top 3 wady per dzień + per hodowca.
- Trigger feedback do `Hodowcy` — jego scorecard.

### Model danych
```sql
CREATE TABLE BS_PM_DefectDict (        -- słownik wad
    Id              INT IDENTITY PRIMARY KEY,
    Kod             NVARCHAR(20) UNIQUE,    -- 'ASCITES','POLYSER','HEPAT','CELLUL','CACHEX','FRACT','HAEM_EXT','BCO_FEMUR','BCO_TIBIA','KINKY','BBS','TD','WB','WS','SPAG','GMD','DMP','FAECAL','BILE','OTHER'
    NazwaPL         NVARCHAR(100),
    NazwaEN         NVARCHAR(100),
    DomyslnaAkcja   NVARCHAR(20),           -- 'COMPLETE_REJECT' / 'PARTIAL_TRIM' / 'DOWNGRADE'
    BRCv9Section    NVARCHAR(20),
    Opis            NVARCHAR(MAX)
);

CREATE TABLE BS_PM_Defect (            -- per ptak (lub batch jeśli za szybkie)
    Id              BIGINT IDENTITY PRIMARY KEY,
    SesjaId         INT NOT NULL,
    PartiaId        INT NOT NULL,
    Ts              DATETIME NOT NULL,
    InspectorId     INT NOT NULL,
    PlatformNr      TINYINT,                -- 1 lub 2
    DefectKod       NVARCHAR(20) NOT NULL,  -- FK BS_PM_DefectDict
    Akcja           NVARCHAR(20),           -- COMPLETE/PARTIAL/DOWNGRADE
    ShackleCount    INT NULL,               -- jeśli mamy z PLC
    Foto_BlobId     UNIQUEIDENTIFIER NULL,
    Uwagi           NVARCHAR(200) NULL
);
CREATE INDEX IX_BS_PM_Partia ON BS_PM_Defect(PartiaId, DefectKod);
CREATE INDEX IX_BS_PM_Inspector_Ts ON BS_PM_Defect(InspectorId, Ts);

CREATE TABLE BS_PM_DailySummary (      -- agregat
    Id              INT IDENTITY PRIMARY KEY,
    Data            DATE NOT NULL,
    PartiaId        INT NOT NULL,
    HodowcaId       INT NOT NULL,
    TotalBirds      INT NOT NULL,
    Rejected_Complete INT NOT NULL,
    Rejected_Partial INT NOT NULL,
    Rejection_Pct   AS (Rejected_Complete + Rejected_Partial * 0.3) * 100.0/NULLIF(TotalBirds,0) PERSISTED,
    Top1_Defect     NVARCHAR(20),
    Top1_Count      INT,
    Top2_Defect     NVARCHAR(20),
    Top2_Count      INT,
    Top3_Defect     NVARCHAR(20),
    Top3_Count      INT,
    Polyser_Cnt     INT, Ascites_Cnt INT, Hepat_Cnt INT, Cellul_Cnt INT,
    WB_Cnt INT, WS_Cnt INT, BCO_Cnt INT  -- ważne osobno do scorecardów
);
```

### UI w WPF
- `Patroszenie/PMInspectionTablet.xaml` — kafelkowy UI, każda wada = duży kafelek z ikoną + counter, jeden klik = +1.
- Live overlay: aktualny rejection rate (cel: <0.5%).
- `Patroszenie/PMDashboard.xaml` — wykres top 3 wady ostatnie 30 dni, breakdown per hodowca.
- Integracja z Hodowca scorecard (NF01).

### Integracje
- PLC linii (Marel/Foodmate) — shackle counter & speed.
- Hikvision NVR — foto auto na klik wady.
- Reklamacje → przy reklamacji klienta auto-link do BS_PM_Defect ostatnich N dni (closed loop).

### KPI
- **Rejection rate** (cel: <0.5%).
- **Top 3 defects** trend (sygnał: nagły wzrost polyser → problem fermy).
- **% partii pre-Salm/logistic-slaught** vs target.

### Szacunek finansowy
- Bez systemu: brak attribution → reklamacje "wsadem" → 50 tys. zł/incident hodowca + 200 tys. zł od klienta.
- Z systemem: redukcja rejection o 0.3 pkt% (z 1.0% → 0.7%) = 17,5M × 0.003 × 2 kg × 12 zł = **1 260 000 zł/rok**.
- Plus closed loop feedback hodowcy → poprawa kolejnych partii.

### BRC v9
- **Sekcja 4.10** CCP monitoring.
- **Sekcja 6.3** Inspection records — auditor chce widzieć każdy odrzut z powodem.

---

## NF07 — Chilling curve CCP monitor

### Problem (książka, s. 152-156)
- EU 92-116: temp core **<4°C** w **7h** po killing (best practice: <6h).
- Krzywa: halve temperature each quarter of cooling period (rule of thumb).
- Air chilling (EU) vs spin (US/BR) — różne setpoints.
- 30-min shock chill (max cold air) + 120-min maturation.

### Co robię w ZPSP
- Rozszerzam `DEEP_DIVE_19_Cold_Chain.md` o realne wdrożenie.
- Sensors w komorach chłodniczych + na linii (probe insertable do breast core na sample 1×/h).
- Krzywa real-time, alert jeśli temp >4°C w 6h.

### Model danych
```sql
CREATE TABLE BS_ChillSession (
    Id              INT IDENTITY PRIMARY KEY,
    LiniaId         INT NOT NULL,
    StartTs         DATETIME NOT NULL,
    EndTs           DATETIME NULL,
    Metoda          NVARCHAR(20),           -- AIR / SPIN / SPRAY
    PartiaId        INT NULL
);

CREATE TABLE BS_ChillTempLog (
    Id              BIGINT IDENTITY PRIMARY KEY,
    SessionId       INT NOT NULL,
    Ts              DATETIME NOT NULL,
    AmbientTempC    DECIMAL(4,1),
    CoreTempC       DECIMAL(4,1) NULL,      -- z probe insertable
    AirFlowMs       DECIMAL(4,2) NULL,
    Humidity        DECIMAL(4,1) NULL,
    Position        NVARCHAR(20)            -- 'IN','MID','OUT','PROBE'
);
CREATE INDEX IX_BS_ChillTemp_Session ON BS_ChillTempLog(SessionId, Ts);

CREATE TABLE BS_ChillCompliance (
    Id              INT IDENTITY PRIMARY KEY,
    SessionId       INT NOT NULL,
    PartiaId        INT,
    StartCoreTempC  DECIMAL(4,1),
    Time_to_4C_Min  INT,                    -- minuty od startu do <4°C
    EUCompliant     AS CAST(CASE WHEN Time_to_4C_Min <= 360 THEN 1 ELSE 0 END AS BIT) PERSISTED,
    AvgCurveScore   DECIMAL(4,2),           -- 0-1 jak blisko ideal
    Notes           NVARCHAR(500)
);

CREATE TABLE BS_DripLoss (
    Id              INT IDENTITY PRIMARY KEY,
    PartiaId        INT NOT NULL,
    DataPomiaru     DATE NOT NULL,
    SampleType      NVARCHAR(50),           -- 'WHOLE_CARCASS','BREAST_FILLET','LEG_FILLET'
    SampleWeight_g  DECIMAL(8,2),
    DripWeight_g    DECIMAL(8,2),
    DripPct         AS (DripWeight_g / NULLIF(SampleWeight_g,0) * 100) PERSISTED,
    AmbientTempC    DECIMAL(4,1)
);
```

### UI w WPF
- `Chlodnia/ChillingCurveWindow.xaml` — wykres real-time per partia + przewidywany czas dojścia do 4°C (regresja).
- Alert: czerwony jeśli prognoza >6h.
- Dashboard miesięczny: % partii EU-compliant + średni Time_to_4C.

### Integracje
- BACnet (HVAC chłodni). Dla Twojej **planowanej chłodni glikolowej 2,8 mln PLN** — zaprojektuj BACnet output od razu.
- Insertable probe (HACCP) — najprościej manualnie 1× per sesja przez operatora.

### KPI
- **% partii EU-compliant <4°C w 6h** (cel: 100%).
- **Drip loss** średni (cel: <1.5% breast fillet, <2% whole).
- **Średni Time_to_4C** trend miesięczny.

### Szacunek finansowy
- Drip loss redukcja z 2% → 1.2% (lepsze chillowanie) = 35 000 t × 0.8% × 12 zł = **3 360 000 zł/rok** (książka s. 174 wprost: drip loss to "consumer pays for less meat").
- BRC v9 wymaga CCP electronic monitoring — bez tego potencjalna utrata certyfikatu.
- Wartość inwestycji glikolowej (2,8M) - **z systemem monitoringu zwrot przyspieszony** o ~30%.

### BRC v9
- **Sekcja 4.10** CCP monitoring **— kluczowy gap**.
- **Sekcja 4.11** Temperature control.

---

## NF08 — Vision grading A/B/C + auto-trim flag

### Problem (książka, s. 157)
- Klasyfikacja A/B/C wg video analysis (mam już w przemyśle, działa).
- A = niezpsute, B = trim needed, C = reject.
- W `Kartoteka Towarow/` mam katalog produktów i klas wagowych, ale nie mam vision grading.

### Co robię w ZPSP
- Kamera + Claude Sonnet 4.6 VLM (lub model open-source typu YOLO) na linii grading.
- Per tuszka: zdjęcie + klasyfikacja + power dla trim.
- Statystyka A/B/C per partia → feedback do hodowcy.

### Model danych
```sql
CREATE TABLE BS_GradingScan (
    Id              BIGINT IDENTITY PRIMARY KEY,
    Ts              DATETIME NOT NULL,
    PartiaId        INT NOT NULL,
    ShackleCount    INT,
    Weight_g        INT,
    Klasa           CHAR(1),                -- 'A','B','C'
    Defekty_Json    NVARCHAR(500),          -- lista defektów wykrytych
    VLM_Confidence  DECIMAL(4,3),
    Foto_BlobId     UNIQUEIDENTIFIER NULL,
    KosztTrim_g     INT NULL                -- ile gramów do odcięcia
);
CREATE INDEX IX_BS_Grading_Partia ON BS_GradingScan(PartiaId, Klasa);
```

### UI w WPF
- Tab w `KartotekaTowarow/` — "Grading dziś" (kafelki A/B/C %, trend tygodniowy).
- Dashboard `AnalitykaPelna/`: dodatkowy widok "Klasy A/B/C per partia".

### Integracje
- Kamera Hikvision na linii grading + Claude VLM (mam pipeline).
- Lub Marel IRIS (jeśli mamy ich linię — wówczas API).

### KPI
- **% A-grade** (cel: >85%).
- **% C-grade rejected** (cel: <0.5%).
- **Trim grammage** średnia per tuszka (cel: <50g).

### Szacunek finansowy
- Lepsze sortowanie A/B → wyższa cena za A (premium klienci): 35 000 t × 5% reklamacji "B in A box" × 12 zł = **2 100 000 zł/rok** unikniętych reklamacji.

### BRC v9
- **Sekcja 5.4** Product release.

---

## NF09 — MAP + shelf life + end-to-end traceability (4h recall)

### Problem (książka, s. 173-175)
- MAP: O2 ↓, CO2/N2 ↑ → 30-60 dni shelf life (vs 2-7 dni air-permeable).
- Drip loss 1-5% w MAP vs 8-10% bez.
- Traceability: **4 godziny** do prześledzenia produktu w supermarkecie → flock → ferma.
- Anti-tamper sticker na każdej skrzynce.

### Co robię w ZPSP
- Każda paleta/skrzynka dostaje QR (już mam w wagach LibraNet — wystarczy rozszerzyć).
- W MAP packagingu: log gas mixture per partia + foto-validation.
- `BS_Traceability` jako materialized view: scan QR → klient → partia → hodowca → ferma → wszystkie wady PM + temp chłodnicze + ATB history.

### Model danych
```sql
CREATE TABLE BS_PackagingBatch (
    Id              INT IDENTITY PRIMARY KEY,
    PartiaId        INT NOT NULL,
    DataPakowania   DATE NOT NULL,
    TypOpakowania   NVARCHAR(50),           -- 'MAP_CO2_70','MAP_N2','VACUUM','OVERWRAP'
    O2_Pct          DECIMAL(4,1),
    CO2_Pct         DECIMAL(4,1),
    N2_Pct          DECIMAL(4,1),
    ShelfLifeDays   INT,
    ExpiryDate      AS DATEADD(DAY, ShelfLifeDays, DataPakowania) PERSISTED,
    AntiTamperUid   UNIQUEIDENTIFIER DEFAULT NEWID()
);

CREATE TABLE BS_TraceabilityScan (
    Id              BIGINT IDENTITY PRIMARY KEY,
    Ts              DATETIME NOT NULL,
    QrCode          NVARCHAR(100),
    ScanType        NVARCHAR(30),           -- 'PACK','SHIP','CUSTOMER','COMPLAINT'
    KlientId        INT NULL,
    PalletId        INT NULL,
    UserId          INT NOT NULL
);

CREATE VIEW BS_TraceabilityFull AS
SELECT 
    p.Id PartiaId, p.HodowcaId, h.NazwaFermy, p.DataStartUboju,
    pa.AntybioBlockTo,
    pmd.Rejected_Complete, pmd.Polyser_Cnt, pmd.WB_Cnt,
    cc.Time_to_4C_Min, cc.EUCompliant,
    pb.TypOpakowania, pb.ExpiryDate, pb.AntiTamperUid,
    s.Klient, s.DataDostawy
FROM listapartii p
JOIN Hodowcy h ON h.Id = p.HodowcaId
LEFT JOIN BS_FarmTreatment ft ...
LEFT JOIN BS_PM_DailySummary pmd ON pmd.PartiaId = p.Id
LEFT JOIN BS_ChillCompliance cc ON cc.PartiaId = p.Id
LEFT JOIN BS_PackagingBatch pb ON pb.PartiaId = p.Id
LEFT JOIN ... -- shipments
```

### UI w WPF
- `Reklamacje/Views/TraceabilityWindow.xaml` — skanuj QR → 1 ekran z **całą historią** tej partii (4-sekundowe lookup).
- Recall mode: skanuj QR → lista wszystkich klientów którym poszły partie ostatnich N dni.

### Integracje
- KSeF (faktury → dostawa → klient).
- LibraNet wagi (już mam QR na paletach).

### KPI
- **Czas recall** (cel: <4h od triggera).
- **Shelf life realny vs deklarowany** (cel: 95% paczek dotrwało).

### Szacunek finansowy
- Bez systemu: recall = stracenie dnia produkcji ≈ 250 000 zł + reputational damage. Z systemem: recall ograniczony do 1 partii = ~30 000 zł.
- MAP shelf life wydłużone → możliwość eksportu (Czechy, Słowacja, Niemcy klienci dystans): +10% obrót = **~30 mln zł** (dla 10% wolumenu).

### BRC v9
- **Sekcja 3.9** Traceability **— wymóg recall w 4h, audytor testuje na 2 produktach losowo**.

---

## NF10 — Salmonella + Campylobacter LIMS + logistic slaughtering

### Problem (książka, s. 50-53, 180)
- EU: fresh poultry musi być **wolne od SE i ST**.
- Overshoe sampling 21 dni pre-slaughter, wynik 24h pre-slaughter.
- Salm+ flocks → ostatnia partia dnia (logistic slaughter) + heat treatment.
- Campylobacter neck skin sampling po slaughter, monitoring trend.

### Co robię w ZPSP
- W `Hodowcy/` formularz "Output monitoring sample" 21-dni pre-slaughter.
- LIMS integration (jeśli mamy lab partnerski) lub OCR PDF wyników.
- Auto-scheduler `Partie/`: jeśli SE/ST+, partia idzie do ostatniego slotu dnia.

### Model danych
```sql
CREATE TABLE BS_PathogenSample (
    Id              INT IDENTITY PRIMARY KEY,
    HodowcaId       INT NOT NULL,
    PartiaId        INT NULL,
    DataPobrania    DATE NOT NULL,
    TypProbki       NVARCHAR(30),           -- 'OVERSHOE','CECUM','NECK_SKIN','BOOT'
    Lokalizacja     NVARCHAR(50),           -- 'FARM_HOUSE_1','SLAUGHTERHOUSE'
    Lab             NVARCHAR(100),
    DataWyniku      DATE,
    Patogen         NVARCHAR(30),           -- 'SE','ST','S_HADAR','S_INFANTIS','S_VIRCHOV','CAMPY_JEJUNI','CAMPY_COLI'
    Wynik           NVARCHAR(20),           -- 'POSITIVE','NEGATIVE','CFU_X'
    CFU_per_g       DECIMAL(10,2) NULL,
    Dokument_BlobId UNIQUEIDENTIFIER
);
CREATE INDEX IX_BS_Path_Hodowca ON BS_PathogenSample(HodowcaId, DataWyniku DESC);
CREATE INDEX IX_BS_Path_Partia ON BS_PathogenSample(PartiaId);

CREATE TABLE BS_LogisticSlaughter (
    PartiaId        INT PRIMARY KEY,
    Powod           NVARCHAR(50),           -- 'SE_POSITIVE','ST_POSITIVE','MIXED'
    SlotDnia        TINYINT,                -- 99 = ostatni
    HeatTreatRequired BIT,
    DataDecyzji     DATETIME NOT NULL,
    DecydujacyId    INT NOT NULL
);
```

### UI w WPF
- `Hodowcy/Views/PathogenHistoryWindow.xaml` — historia próbek per hodowca.
- W `Partie/Views/PlanowanieDniaWindow.xaml` — auto-uszeregowanie z SE+ na koniec.

### Integracje
- Lab partnerski (najczęściej SGS, JS Hamilton, Eurofins) — emaile z PDF wyników → OCR → DB.

### KPI
- **% SE/ST positive flocks** (cel: <1% — EU regulacja).
- **% Campy positive carcasses** (trend pomocniczy).
- **Compliance logistic slaughtering** (cel: 100%).

### Szacunek finansowy
- Pojedyncze SE+ niewykryte → recall klienta = 200-500 tys. zł.
- Compliance pre-slaughter (output monitoring) — wymóg EU, bez tego brak certyfikacji.

### BRC v9
- **Sekcja 5.6** Pathogen control **— wymóg dla mięs surowych**.

---

## NF11 — Foreign material registry (metal/plastic/glass detector log)

### Problem (książka, s. 177-179)
- Metal detector + X-ray + bone detector po pakowaniu (last step).
- Foreign material > microbial = większe ryzyko dla konsumenta.
- Colour coding utensils (blue gloves, yellow allergen-free, red waste).
- Maintenance protocol: "wymieniłeś 10 bolts, musisz oddać 10 starych".

### Co robię w ZPSP
- Rejestr każdego alarmu metal/X-ray + foto + akcja.
- Maintenance log z compliance tools-out vs tools-in.
- Wzór do BRC HACCP plan-do-check-act.

### Model danych
```sql
CREATE TABLE BS_ForeignMatAlarm (
    Id              INT IDENTITY PRIMARY KEY,
    Ts              DATETIME NOT NULL,
    Linia           NVARCHAR(50),
    Typ             NVARCHAR(20),           -- 'METAL','XRAY_BONE','XRAY_PLASTIC','XRAY_GLASS'
    Material        NVARCHAR(50),
    Foto_BlobId     UNIQUEIDENTIFIER,
    PartiaId        INT NULL,
    AkcjaPodjeta    NVARCHAR(200),
    OperatorId      INT NOT NULL
);

CREATE TABLE BS_MaintenanceTool (
    Id              INT IDENTITY PRIMARY KEY,
    ToolId          NVARCHAR(50),
    TechnikId       INT NOT NULL,
    DataWydania     DATETIME NOT NULL,
    DataZwrotu      DATETIME NULL,
    Lokalizacja     NVARCHAR(100),
    KomentaryZwrotu NVARCHAR(200)
);
```

### UI w WPF
- `Higiena/ForeignMatLog.xaml` — chronologia alarmów, drill-down per partia/linia.
- Dashboard: alarmy/miesiąc + procent investigated/closed.

### KPI
- **Alarmów false-positive** (cel: <5/zmiana).
- **Investigation closed within 24h** (cel: 100%).

### Szacunek finansowy
- BRC v9 sekcja 4.9 wymóg. Bez tego — major NC.
- Pozytywny consumer complaint metal/plastic → ~50 tys. zł + reputational.

### BRC v9
- **Sekcja 4.9** Detection and removal of foreign bodies.

---

## NF12 — BRC v9 / IFS v8 audit trail elektroniczny

### Problem (audyt branżowy, BAZA_WIEDZY/30_POMYSLY/00_INDEX)
- BRC v9 sek. 3: 62% braków (audyt branżowy 2026-05-11).
- KPI pokrycie 17% (1/30 sygnałów monitorowane elektronicznie).
- CCP 0/10 elektronicznie monitorowane.

### Co robię w ZPSP
- Moduł `Compliance/` z BRC v9 checklist (181 wymagań) + status per wymóg.
- Każda CCP z NF03-NF11 ma flagę `BRCv9Section` → automatyczne wypełnienie checklist.
- PDF eksport "BRC v9 self-assessment" co kwartał.
- IFS v8 podobnie.

### Model danych
```sql
CREATE TABLE BS_ComplianceRequirement (
    Id              INT IDENTITY PRIMARY KEY,
    Standard        NVARCHAR(20),           -- 'BRC_v9','IFS_v8','IRZplus','KSeF'
    Section         NVARCHAR(30),
    Title           NVARCHAR(500),
    Priority        NVARCHAR(20),           -- 'FUNDAMENTAL','STATEMENT_INTENT','GENERAL'
    Description     NVARCHAR(MAX),
    EvidenceSource  NVARCHAR(200)           -- np. 'BS_StunningParam','BS_ChillCompliance'
);

CREATE TABLE BS_ComplianceStatus (
    Id              INT IDENTITY PRIMARY KEY,
    RequirementId   INT NOT NULL,
    Status          NVARCHAR(30),           -- 'CONFORMING','MINOR_NC','MAJOR_NC','NOT_APPLIC'
    LastChecked     DATETIME NOT NULL,
    CheckedBy       INT NOT NULL,
    EvidenceUrls    NVARCHAR(MAX),
    GapNotes        NVARCHAR(MAX),
    PlanowanaData   DATE NULL,
    Odpowiedzialny  INT NULL
);
```

### UI w WPF
- `Compliance/BRCDashboard.xaml` — radar chart 7 sekcji BRC + drill-down per requirement.
- Każdy requirement: kliknięcie pokazuje aktualną evidence (np. dla CCP — wykres ostatnich 30 dni).

### KPI
- **% conforming BRC v9** (cel: >95%).
- **# major NC otwarte** (cel: 0).

### Szacunek finansowy
- BRC v9 utrzymanie = dostęp do retail UE = ~50% obrotu (260 mln × 50% = **130 mln zł chronionych**).
- Audyt zewnętrzny self-prep oszczędność: ~20 tys. zł/rok consulting.

### BRC v9
- **Cała norma**, ze szczególnym akcentem na sek. 3 (HACCP) i 4 (process control).

---

## Podsumowanie 12 funkcji — wartość roczna

| # | Funkcja | CAPEX szac. | Roczna wartość | Płatność zwrotu |
|---|---|---|---|---|
| NF01 | FPD Scorecard | 5k (tablet × 2) | 0.84M | <2 mies. |
| NF02 | Antybiotyki | 0 | 0.21M + ryzyko | <1 mies. |
| NF03 | Transport CCP | 11k (czujniki) | 0.21M + ryzyko | <2 mies. |
| NF04 | Stunning CCP | 30k (PLC integration) | 2.52M | <1 mies. |
| NF05 | Scalding/Plucking | 5k (tablety) | 2.27M | <1 mies. |
| NF06 | PM Defects | 15k (4 tablety) | 1.26M | <2 mies. |
| NF07 | Chilling Curve | 20k (probes, BACnet) | 3.36M | <1 mies. |
| NF08 | Vision Grading | 80k (kamery + GPU) | 2.10M | <5 mies. |
| NF09 | MAP + Traceability | 50k (gas analyzer, QR system) | ~3.0M | <2 mies. |
| NF10 | Salm/Campy LIMS | 0 (OCR-based) | ryzyko głównie | n/a |
| NF11 | Foreign Material | 0 (już mamy detektory) | ryzyko | n/a |
| NF12 | BRC Compliance Audit | 0 | 130M chronionych | n/a |
| **RAZEM** | | **~216k zł** | **~16M zł/rok + 130M chronionych** | **<1 rok** |

**Założenia**: przy 70k ptaków × 250 dni × średnie wartości jak wyżej. Realne wartości zależą od bieżących cen surowca i ekspozycji rynkowej.
