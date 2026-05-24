# 🐔 30 pomysłów dla ZPSP — analiza "Broiler Meat Signals" (VetBooks.ir)

**Data**: 2026-05-23
**Autor**: Claude Opus 4.7 (sesja Sergiusza)
**Źródło**: `BAZA_WIEDZY/Drobiarstwo/Broiler Meat Signals (VetBooks.ir).pdf` (190+ str, rozdz. 4-9 = ubojnia)
**Cel**: dopasowanie wiedzy branżowej do istniejącej architektury ZPSP (HANDEL + LibraNet + AnalitykaPelna + CentrumNagranAI)

---

## 📋 SPIS TREŚCI

### CZĘŚĆ I — Krótki przegląd (pkt 1-8)
1. [Kalkulator wycofania paszy](#1-kalkulator-wycofania-paszy)
2. [DOA Dashboard](#2-doa-dashboard-dead-on-arrival)
3. [Heat Stress Index transportu](#3-heat-stress-index-transportu)
4. [Crop fullness predictor](#4-crop-fullness-predictor)
5. [Lairage Timer](#5-lairage-timer)
6. [Audyt jakości wykrwawienia VLM](#6-audyt-jakości-wykrwawienia-vlm)
7. [Heart fibrillation detector](#7-heart-fibrillation-detector)
8. [Klasyfikacja typu ogłuszania](#8-klasyfikacja-typu-ogłuszania)

### CZĘŚĆ II — SZCZEGÓŁOWE INSTRUKCJE
**Pliki w podfolderze `30_POMYSLY/`** (każdy pomysł w osobnym pliku z pełnym detalem):

| # | Plik szczegółowy |
|---|---|
| **INDEX + ROADMAPA** | [30_POMYSLY/00_INDEX_I_ROADMAPA.md](30_POMYSLY/00_INDEX_I_ROADMAPA.md) |
| 9 ⭐ | [09_Scalding_Monitor.md](30_POMYSLY/09_Scalding_Monitor.md) |
| 10 ⭐ | [10_Plucking_Damage_Tracker.md](30_POMYSLY/10_Plucking_Damage_Tracker.md) |
| 11 | [11_Digital_Inspection_Sheet.md](30_POMYSLY/11_Digital_Inspection_Sheet.md) |
| 12 ⭐ | [12_Forensic_Hematoma_Dating.md](30_POMYSLY/12_Forensic_Hematoma_Dating.md) |
| 13-17 | [13-17_Wady_szczegolowo.md](30_POMYSLY/13-17_Wady_szczegolowo.md) |
| 18 ⭐ | [18_Chilling_Curve_Monitor.md](30_POMYSLY/18_Chilling_Curve_Monitor.md) |
| 19 ⭐ | [19_Cold_Chain_HACCP.md](30_POMYSLY/19_Cold_Chain_HACCP.md) |
| 20, 24-28 | [20_24_25_26_27_28_KPI_AI_pozostale.md](30_POMYSLY/20_24_25_26_27_28_KPI_AI_pozostale.md) |
| 21 ⭐ | [21_Yield_Waterfall.md](30_POMYSLY/21_Yield_Waterfall.md) |
| 22 ⭐ | [22_End_to_End_Traceability.md](30_POMYSLY/22_End_to_End_Traceability.md) |
| 23 ⭐ | [23_Salmonella_Lab_Integration.md](30_POMYSLY/23_Salmonella_Lab_Integration.md) |
| 29 ⭐ | [29_RAG_AI_Chat.md](30_POMYSLY/29_RAG_AI_Chat.md) |
| 30 ⭐ | [30_ML_Forecast_Yield.md](30_POMYSLY/30_ML_Forecast_Yield.md) |

⭐ = sekcje z największą detalicznością (poprosiłeś o szczególnie szczegółowe)

### CZĘŚĆ III — PLAN WDROŻENIA + BUDŻET + ZALEŻNOŚCI
W pliku [00_INDEX_I_ROADMAPA.md](30_POMYSLY/00_INDEX_I_ROADMAPA.md):
- Ranking polecenia (Klasa S/A/B/C)
- Roadmapa 12 miesięcy
- Budżet hardware + software + Twój czas
- Drzewo zależności między pomysłami
- Scenariusz "MVP w 3 miesiące"

---

# CZĘŚĆ I — KRÓTKI PRZEGLĄD (pkt 1-8)

## 1. Kalkulator wycofania paszy

### Wartość
- Zmniejszenie zanieczyszczeń tuszek o ~30% (mniejszy crop)
- Oszczędność: ~150-200 tys PLN/rok
- Mniej mycia linii (chemia + woda)

### Wymagania bazodanowe
```sql
-- LibraNet
ALTER TABLE PartiaDostawca ADD
    PlanowanyUbojDateTime DATETIME NULL,
    CzasTransportu_h DECIMAL(4,1) NULL,
    CzasLapania_h DECIMAL(4,1) NULL DEFAULT 2.0,
    LairageOczekiwany_h DECIMAL(4,1) NULL DEFAULT 1.5,
    WycofaniePaszyDateTime DATETIME NULL,  -- wyliczone
    WycofaniePaszyPotwierdzone BIT NOT NULL DEFAULT 0,
    WycofaniePaszyPotwierdzilHodowca DATETIME NULL;
```

### Algorytm
```csharp
DateTime CalcWycofanie(DateTime ubojPlan, double trans_h, double catch_h, double lairage_h)
{
    const double FULL_WITHDRAWAL_H = 9.0; // wg PDF, dla pelletu mielonego
    var marginH = catch_h + trans_h + lairage_h;
    return ubojPlan.AddHours(-(FULL_WITHDRAWAL_H - marginH));
}
```

### Powiadomienia
- SMS przez SMSAPI.pl (~0.06 zł/SMS) 1h przed deadlinem
- Email backup
- Powiadomienie w app dla dyspozytora

### Architektura
- `Hodowcy/Services/WycofaniePaszyCalculator.cs`
- `Hodowcy/Services/PowiadomieniaPaszyService.cs`
- Widok: dodać do `ProdukcjaDzisWidok` sekcję "Wycofania na dziś"

### Czas implementacji: **8-12h**

---

## 2. DOA Dashboard (Dead On Arrival)

### Wartość
- Norma branżowa <0.2%, max 0.5%
- Każda padła sztuka = 30-40 PLN strata
- Po wdrożeniu monitoringu: redukcja DOA o 30-40%
- **Oszczędność: 400-500 tys PLN/rok**

### Bazadanowe
```sql
ALTER TABLE listapartii ADD
    LiczbaPadlych INT NULL,
    LiczbaZywych INT NULL,
    ProcentDOA AS (CASE WHEN (LiczbaPadlych + LiczbaZywych) > 0
                        THEN CAST(LiczbaPadlych AS DECIMAL(10,4)) /
                             (LiczbaPadlych + LiczbaZywych) * 100
                        ELSE NULL END);

CREATE INDEX IX_listapartii_DOA ON listapartii(ProcentDOA) WHERE ProcentDOA IS NOT NULL;
```

### UI elementy
- Karta KPI w `WidokFabryka` — "DOA dziś: 0.18% ✓" (zielony) / "0.31% ⚠" (żółty) / ">0.5% 🚨" (czerwony)
- Ranking hodowców 30-dniowy z DOA% jako kolumna
- Wykres trendu DOA dla wybranego hodowcy
- Alert push gdy DOA partii >0.5%

### Workflow przyjęcia
1. Pracownik rampy waży kontenery
2. Przelicza padłe vs żywe (lub estymacja per kontener)
3. Wpisuje w aplikacji mobilnej (Android tablet)
4. Auto-zapis do `listapartii.LiczbaPadlych`

### Wartość biznesowa per hodowca
```
Kowalski:    0.15% (200 partii) → wzorowy, premia 0.05 zł/kg
Nowak:       0.38% (87 partii)  → rozmowa, audit kurnika
Wiśniewski:  0.62% (35 partii)  → 3-miesięczny program naprawczy
```

### Czas implementacji: **6-10h**

---

## 3. Heat Stress Index transportu

### Wartość
- Lato: 200 t/dzień × 3-4 miesiące = ~24 000 t pod ryzykiem
- Redukcja DOA latem z 0.4% → 0.2% = **200k PLN/sezon**
- Lepsza jakość mięsa (mniej PSE/DFD)

### API meteo
```csharp
// Open-Meteo (DARMOWE, bez klucza)
// https://api.open-meteo.com/v1/forecast?latitude=52.4&longitude=18.5&hourly=temperature_2m,relativehumidity_2m
public async Task<WeatherForecast> GetForecastAsync(double lat, double lon, DateTime when)
{
    var url = $"https://api.open-meteo.com/v1/forecast?latitude={lat}&longitude={lon}" +
              $"&hourly=temperature_2m,relativehumidity_2m" +
              $"&start_date={when:yyyy-MM-dd}&end_date={when:yyyy-MM-dd}";
    // ... HttpClient
}
```

### Formuła HSI (Heat Stress Index dla drobiu)
```csharp
// THI = (1.8 * T + 32) - ((0.55 - 0.0055 * RH) * (1.8 * T - 26))
// gdzie T = °C, RH = % wilgotności
public static double CalculateHSI(double tempC, double humidityPct)
{
    return (1.8 * tempC + 32) - ((0.55 - 0.0055 * humidityPct) * (1.8 * tempC - 26));
}

public static string ClassifyHSI(double hsi) => hsi switch
{
    < 70 => "BEZPIECZNY",
    < 80 => "OSTROŻNIE",
    < 90 => "RYZYKO",
    _    => "KRYTYCZNY — wstrzymaj transport"
};
```

### UI mapa
- Reuse istniejącej mapy z modułu Webfleet (`MapaTransport`)
- Każdy pin partii kolorowany wg HSI
- Tooltip: "Kowalski, T=29°C, RH=72%, HSI=88, przewidywane DOA: 0.4%"

### Czas implementacji: **12-18h**

---

## 4. Crop fullness predictor

### Wartość
- Niska indywidualnie, ale **rozróżnia winę** w reklamacjach
- Argument do hodowców

### Algorytm
```csharp
public CropRisk EstimateCropFullness(DateTime feedWithdrawn, DateTime slaughter)
{
    var hoursH = (slaughter - feedWithdrawn).TotalHours;
    return hoursH switch
    {
        < 3   => CropRisk.Critical,      // wole bardzo pełne, 8-10% zanieczyszczeń
        < 5   => CropRisk.High,           // 4-6%
        < 7   => CropRisk.Medium,         // 2-3%
        < 10  => CropRisk.Optimal,        // norma
        < 14  => CropRisk.Hungry,         // głodne — jedzą ściółkę = inny problem
        _     => CropRisk.TooLong
    };
}
```

### Czas implementacji: **3-5h** (przy okazji #1)

---

## 5. Lairage Timer (oczekiwanie w ubojni)

### Wartość
- Mniej DFD mięsa (Dark, Firm, Dry)
- Mniej zwrotów od marketów
- **50-100k PLN/rok**

### Implementacja
```csharp
// Reuse istniejacego PartiaStatus history
public class LairageTimerService
{
    public async Task<TimeSpan> GetLairageDuration(int partiaId)
    {
        var atRamp = await GetStatusTimestamp(partiaId, "AT_RAMP");
        var inProd = await GetStatusTimestamp(partiaId, "IN_PRODUCTION");
        return (inProd ?? DateTime.Now) - (atRamp ?? DateTime.Now);
    }

    public AlertLevel ClassifyDuration(TimeSpan duration) => duration.TotalMinutes switch
    {
        < 60   => AlertLevel.OK,
        < 90   => AlertLevel.Warning,
        < 120  => AlertLevel.Critical,
        _      => AlertLevel.Violation  // przekroczono limit 2h z PDF
    };
}
```

### Widget w `ProdukcjaDzisWidok`
```
┌─────────────────────────────────────────────┐
│ ⏱ PARTIE NA RAMPIE                          │
├─────────────────────────────────────────────┤
│ Kowalski #1247  │ 00:45  │ 🟢 OK            │
│ Nowak #1248     │ 01:30  │ 🟡 UWAGA         │
│ Wiśniewski #49  │ 02:15  │ 🔴 PRZEKROCZONO  │
└─────────────────────────────────────────────┘
```

### Czas implementacji: **3-5h**

---

## 6. Audyt jakości wykrwawienia VLM

### Wartość
- Tuszki źle wykrwawione: czerwona skóra → zwroty
- Wczesna detekcja przed pakowaniem = ~100-200k PLN/rok

### Wykorzystanie istniejącej infrastruktury CentrumNagranAI
```csharp
public class WykrwawienieAuditService
{
    private readonly ClaudeVlmService _vlm;
    private readonly NvrService _nvr; // kamera nad linią wykrwawiania

    public async Task<BleedQualityScore> AnalyzeFrame()
    {
        var frame = await _nvr.CaptureFrameAsync("LINIA_WYKRWAWIANIA");
        var prompt = "Oceń jakość wykrwawienia tuszek na obrazie. " +
                     "Skala 1-10 (10=perfekcyjne, 1=mocno czerwona skóra). " +
                     "Zwróć JSON: {score: int, observations: string}";
        return await _vlm.AnalyzeAsync(frame, prompt);
    }
}
```

### Częstotliwość
- 1 klatka co 5 minut = ~96 klatek/zmiana
- Koszt Claude Haiku ~$0.003/klatka × 96 = **$0.29/zmiana = ~1.20 zł**
- Tygodniowo: ~36 zł
- Rocznie: ~1800 zł

### Dashboard
- Wykres jakości wykrwawienia per godzina
- Alert gdy avg score < 7 przez >30 min
- Sugestia: "Sprawdź ostrzenie noża / parametry stunning"

### Czas implementacji: **8-12h** (reuse istniejących klas)

---

## 7. Heart fibrillation detector

### Wartość niska — **POMIJAM W TYM ETAPIE**

Powód: większość zakładów (w tym Wy) docelowo idzie w CAS gas stunning gdzie problem nie występuje. Implementacja teraz to inwestycja, która będzie nieaktualna za 2-3 lata.

---

## 8. Klasyfikacja typu ogłuszania w analityce

### Wartość — **decyzja inwestycyjna** (raz na 5-10 lat)

### Dodaj enum
```csharp
public enum StunningMethod
{
    ElectricWaterBath,
    GasCAS_CO2,
    GasCAS_NobleGas,
    HeadOnlyElectric,
    Religious_None
}
```

### Pole w `listapartii`
```sql
ALTER TABLE listapartii ADD StunningMethod NVARCHAR(30) NULL;
```

### Raport rocznych statystyk
- Hematomy w piersi per metoda (% odrzutów)
- Koszt straty na metodzie vs koszt aparatury CAS
- ROI calculator dla zakupu CAS

### Czas implementacji: **2-3h** (sama analityka, decyzja biznesowa osobno)

---

