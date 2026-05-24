# 30. ⭐ ML Forecast yield per hodowca — PEŁNY PORADNIK

## Co to jest
**Machine Learning** model który **przewiduje yield (uzysk mięsa) w PROCENTACH** dla nowej partii zanim ją kupisz, na podstawie:
- Cech hodowcy (historia, lokalizacja, ras stosowanych)
- Cech partii (waga avg, wiek, sezon, pogoda)
- Cech transportu (czas trasy, temperatura, lairage)

## Wartość biznesowa — szczegółowo

### Scenariusz przed wdrożeniem
- Kupujesz od Kowalskiego za 5.20 zł/kg żywca
- Po uboju yield: 56% (norma 60%)
- "Zła partia" — pretensje, ale za późno
- Strata: różnica 4% × 18 ton × 12 zł = **8 640 zł na partii**

### Scenariusz po wdrożeniu
- Zamierzasz kupić partię od Kowalskiego (4500 sztuk, waga avg 2.6 kg)
- System ML przewiduje: yield 56% ± 2% (na podstawie ostatnich 30 podobnych partii)
- Negocjujesz: "Skoro będzie 56% yield, mogę dać max 5.05 zł/kg żywca"
- Kowalski się zgadza (i tak by przegrał) lub odmawia (kupisz tańszą partię od kogoś)
- Oszczędność: 0.15 zł × 18 ton × 1000 = **2 700 zł/partia**

### Roczna wartość
- 30 hodowców × ~100 partii × 2 700 zł średnio = **8.1M PLN/rok teoretycznie**
- Realnie (po negocjacjach, części odmów): **1-3M PLN/rok**

### Plus
- **Lepszy mix** dostawców — wiesz którzy są wartościowi
- **Premia za dobry yield** dla wzorowych hodowców (motywacja systemowa)
- **Plan produkcji** bardziej precyzyjny

---

## DANE WEJŚCIOWE — co modelujemy

### Cechy hodowcy (slowly changing)
- `hodowca_id` — ID
- `lokalizacja_woj` — województwo (klimat lokalny)
- `lokalizacja_dystanst_km` — odległość od ubojni
- `liczba_partii_historycznie` — doświadczenie
- `yield_avg_3mies` — średnia historyczna 3 mies
- `yield_avg_12mies` — średnia historyczna 12 mies
- `procent_klasy_b_3mies` — historia jakości
- `pozytywy_mikro_12mies` — historia higieny

### Cechy partii (per delivery)
- `liczba_sztuk`
- `waga_avg_szt` — średnia waga
- `wiek_dni` — wiek bawełnia
- `rasa` (Ross 308, Cobb 500, ...)
- `sezon` — pora roku (lato/zima inne yield)
- `miesiac` — bardziej granularne
- `dzien_tygodnia` — niektóre dni są gorsze (poniedziałek po weekendzie)

### Cechy transportu
- `czas_transportu_h`
- `lairage_h`
- `temp_max_transport_C`
- `wilgotnosc_transport_proc`
- `hsi_max` — z #3
- `doa_proc` — z #2

### Cechy procesowe (znane w momencie uboju)
- `parzelnik_temp_avg`
- `chilling_czas_h`
- `incydenty_ccp_dzien` — z #19

### Output (Y)
- `yield_proc` — finalne %% yield (output ML)

---

## ALGORYTM — wybór

### Opcja 1: Gradient Boosting (zalecane) ⭐
- **XGBoost** lub **LightGBM** lub **CatBoost**
- Świetny dla tabelarycznych danych
- Łatwy interpret feature importance
- Sprawdza się od 100+ rekordów

### Opcja 2: Random Forest
- Solidny baseline
- Mniej tuningu niż XGBoost
- Mniej dokładny dla małych nieliniowości

### Opcja 3: Neural Network (TensorFlow/PyTorch)
- Przesadnie skomplikowany dla 1000-10000 rekordów
- Lepiej zostawić na później

### Opcja 4: Linear Regression + feature engineering
- Najprostszy
- Łatwy do wytłumaczenia (każdy współczynnik = wpływ cechy)
- Nie złowi nieliniowości

**Rekomendacja: LightGBM**
- Szybki trening (<1 min na 10k rekordów)
- Dobre wyniki out-of-the-box
- Świetne SHAP values (interpretowalność)
- C# dostęp: **Microsoft.ML** lub **ML.NET** z LightGBM trainer

---

## DATABASE SCHEMA

```sql
-- LibraNet
CREATE TABLE MlYieldDataset (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    PartiaId INT NOT NULL,
    DataPobrania DATETIME NOT NULL,
    
    -- Features hodowcy
    HodowcaId INT NOT NULL,
    HodowcaWoj NVARCHAR(50) NULL,
    HodowcaDystansKm DECIMAL(8,2) NULL,
    HodowcaLiczbaPartiiHist INT NULL,
    HodowcaYieldAvg3Mies DECIMAL(5,2) NULL,
    HodowcaYieldAvg12Mies DECIMAL(5,2) NULL,
    HodowcaProcKlasyB3Mies DECIMAL(5,2) NULL,
    HodowcaPozytywyMikro12Mies INT NULL,
    
    -- Features partii
    LiczbaSztuk INT NOT NULL,
    WagaAvgSzt DECIMAL(5,2) NOT NULL,
    WiekDni INT NULL,
    Rasa NVARCHAR(50) NULL,
    Sezon NVARCHAR(20) NULL,
    Miesiac TINYINT NULL,
    DzienTygodnia TINYINT NULL,
    
    -- Features transportu
    CzasTransportuH DECIMAL(4,1) NULL,
    LairageH DECIMAL(4,1) NULL,
    TempMaxTransportC DECIMAL(5,2) NULL,
    WilgotnoscTransportProc DECIMAL(5,2) NULL,
    HsiMax DECIMAL(5,2) NULL,
    DoaProc DECIMAL(5,2) NULL,
    
    -- Features procesowe
    ParzelnikTempAvg DECIMAL(5,2) NULL,
    ChillingCzasH DECIMAL(4,1) NULL,
    IncydentyCcpDzien INT NULL,
    
    -- Target (output)
    YieldProc DECIMAL(5,2) NULL,  -- to przewidujemy
    
    -- Audit
    UzyteDoTreningu BIT NOT NULL DEFAULT 0,
    UzyteDoWalidacji BIT NOT NULL DEFAULT 0,
    UzyteDoTestu BIT NOT NULL DEFAULT 0,
    WersjaModeluPrognozy NVARCHAR(50) NULL,
    PrognozaYieldProc DECIMAL(5,2) NULL,
    PrognozaConfidence DECIMAL(5,2) NULL
);
CREATE INDEX IX_MlYield_Partia ON MlYieldDataset(PartiaId);
CREATE INDEX IX_MlYield_Hodowca_DataPobr ON MlYieldDataset(HodowcaId, DataPobrania);

CREATE TABLE MlModel (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Nazwa NVARCHAR(100) NOT NULL,
    Wersja NVARCHAR(50) NOT NULL,
    Algorytm NVARCHAR(50) NOT NULL,  -- 'LightGBM', 'XGBoost', 'RandomForest'
    DataTreningu DATETIME NOT NULL,
    LiczbaRekordowTrain INT NULL,
    LiczbaRekordowVal INT NULL,
    MaeWalidacja DECIMAL(5,2) NULL,  -- Mean Absolute Error
    RmseWalidacja DECIMAL(5,2) NULL,
    R2Walidacja DECIMAL(5,2) NULL,
    FeatureImportanceJson NVARCHAR(MAX) NULL,
    SciezkaModelu NVARCHAR(500) NULL,  -- np. C:\models\yield_lgbm_v3.zip
    Aktywny BIT NOT NULL DEFAULT 0
);

CREATE TABLE MlPredykcjaLog (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    DataPredykcji DATETIME NOT NULL,
    ModelId INT NOT NULL FOREIGN KEY REFERENCES MlModel(Id),
    HodowcaId INT NOT NULL,
    FeaturesJson NVARCHAR(MAX) NULL,
    PredictedYield DECIMAL(5,2) NULL,
    PredictedConfidence DECIMAL(5,2) NULL,
    PredictedRangeMin DECIMAL(5,2) NULL,
    PredictedRangeMax DECIMAL(5,2) NULL,
    RzeczywistyYield DECIMAL(5,2) NULL,  -- wypelnia się po fakcie
    UzytkownikIzapytania NVARCHAR(100) NULL
);
```

---

## WYMAGANIA DANYCH

### Minimum do treningu
- **300-500 rekordów** dla MVP (4-6 miesięcy danych)
- **2000-5000 rekordów** dla solidnego modelu (rok+ danych)
- **Każda kolumna** wypełniona w >70% rekordów

### Stan obecny w ZPSP
- Yield finalny — można już teraz wyliczyć z HANDEL (sPWP / sPWU per partia)
- Hodowca — masz w `PartiaDostawca`
- Waga, sztuki — masz w `listapartii`
- Transport time, lairage — **brakuje** (potrzebny #5 Lairage Timer + DOA z #2)
- Sezon, miesiąc, dzień — wyliczalne z daty

### Plan zbierania danych (parallel z innymi pomysłami)
1. **Teraz** — backfill yield retrospektywnie (z istniejących sPWP/sPWU)
2. **Po #2** (DOA) — dodaj DOA
3. **Po #5** (Lairage) — dodaj czas oczekiwania
4. **Po #19** (CCP) — dodaj incydenty
5. **Po 6-9 miesiącach** — uruchamiaj pierwszy model

---

## SQL: BACKFILL DATASETU

```sql
-- Buduj dataset retrospektywnie z historii
WITH PartieZyield AS (
    SELECT 
        lp.LP AS PartiaId,
        lp.DataUboju AS DataPobrania,
        pd.HodowcaId,
        lp.LiczbaSztuk,
        lp.WagaCalkowita / NULLIF(lp.LiczbaSztuk, 0) AS WagaAvgSzt,
        DATEDIFF(DAY, pd.DataWklucia, lp.DataUboju) AS WiekDni,
        DATEPART(MONTH, lp.DataUboju) AS Miesiac,
        DATEPART(WEEKDAY, lp.DataUboju) AS DzienTygodnia,
        CASE 
            WHEN DATEPART(MONTH, lp.DataUboju) IN (12, 1, 2) THEN 'ZIMA'
            WHEN DATEPART(MONTH, lp.DataUboju) IN (3, 4, 5) THEN 'WIOSNA'
            WHEN DATEPART(MONTH, lp.DataUboju) IN (6, 7, 8) THEN 'LATO'
            ELSE 'JESIEN'
        END AS Sezon
    FROM listapartii lp
    JOIN PartiaDostawca pd ON pd.Partia = lp.LP
    WHERE lp.DataUboju >= DATEADD(MONTH, -24, GETDATE())
),
YieldCalculation AS (
    -- Cross-DB: sPWU vs sPWP per partia
    SELECT 
        ... cross-DB query z HANDEL.HM.MG sPWU/sPWP 
        do wyliczenia yield per partia
)
INSERT INTO MlYieldDataset (PartiaId, DataPobrania, HodowcaId, ...)
SELECT 
    p.PartiaId, p.DataPobrania, p.HodowcaId, ...
    yc.YieldProc
FROM PartieZyield p
JOIN YieldCalculation yc ON yc.PartiaId = p.PartiaId
WHERE yc.YieldProc IS NOT NULL;
```

---

## TRENING MODELU — ML.NET (C#)

**Pakiet NuGet**:
```xml
<PackageReference Include="Microsoft.ML" Version="3.0.1" />
<PackageReference Include="Microsoft.ML.LightGbm" Version="3.0.1" />
```

**Plik**: `MLProgram/YieldPredictionTrainer.cs`

```csharp
using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Trainers.LightGbm;

namespace Kalendarz1.MLPrograms;

public class YieldFeatures
{
    [LoadColumn(0)] public float HodowcaYieldAvg3Mies { get; set; }
    [LoadColumn(1)] public float HodowcaYieldAvg12Mies { get; set; }
    [LoadColumn(2)] public float HodowcaProcKlasyB3Mies { get; set; }
    [LoadColumn(3)] public float WagaAvgSzt { get; set; }
    [LoadColumn(4)] public float WiekDni { get; set; }
    [LoadColumn(5)] public float Miesiac { get; set; }
    [LoadColumn(6)] public string Sezon { get; set; } = "";
    [LoadColumn(7)] public string Rasa { get; set; } = "";
    [LoadColumn(8)] public float CzasTransportuH { get; set; }
    [LoadColumn(9)] public float LairageH { get; set; }
    [LoadColumn(10)] public float TempMaxTransportC { get; set; }
    [LoadColumn(11)] public float HsiMax { get; set; }
    [LoadColumn(12)] public float DoaProc { get; set; }
    [LoadColumn(13)] public float ChillingCzasH { get; set; }
    [LoadColumn(14)] public float IncydentyCcpDzien { get; set; }
    
    [LoadColumn(15), ColumnName("Label")] public float YieldProc { get; set; }
}

public class YieldPrediction
{
    [ColumnName("Score")] public float PredictedYield { get; set; }
}

public class YieldPredictionTrainer
{
    private readonly MLContext _ml;

    public YieldPredictionTrainer()
    {
        _ml = new MLContext(seed: 42);
    }

    public ITransformer Train(string csvDatasetPath, string modelOutputPath)
    {
        // 1. Load data
        var data = _ml.Data.LoadFromTextFile<YieldFeatures>(
            csvDatasetPath, 
            hasHeader: true, 
            separatorChar: ',');

        // 2. Split: 70% train, 15% val, 15% test
        var split1 = _ml.Data.TrainTestSplit(data, testFraction: 0.3);
        var split2 = _ml.Data.TrainTestSplit(split1.TestSet, testFraction: 0.5);
        var trainSet = split1.TrainSet;
        var valSet = split2.TrainSet;
        var testSet = split2.TestSet;

        // 3. Pipeline
        var pipeline = _ml.Transforms.Categorical.OneHotEncoding("Sezon")
            .Append(_ml.Transforms.Categorical.OneHotEncoding("Rasa"))
            .Append(_ml.Transforms.Concatenate("Features",
                "HodowcaYieldAvg3Mies", "HodowcaYieldAvg12Mies", "HodowcaProcKlasyB3Mies",
                "WagaAvgSzt", "WiekDni", "Miesiac", "Sezon", "Rasa",
                "CzasTransportuH", "LairageH", "TempMaxTransportC", "HsiMax",
                "DoaProc", "ChillingCzasH", "IncydentyCcpDzien"))
            .Append(_ml.Regression.Trainers.LightGbm(
                labelColumnName: "Label",
                featureColumnName: "Features",
                numberOfLeaves: 31,
                numberOfIterations: 200,
                minimumExampleCountPerLeaf: 10));

        // 4. Train
        var model = pipeline.Fit(trainSet);

        // 5. Evaluate
        var valPredictions = model.Transform(valSet);
        var valMetrics = _ml.Regression.Evaluate(valPredictions);
        Console.WriteLine($"VAL: MAE={valMetrics.MeanAbsoluteError:F2}, RMSE={valMetrics.RootMeanSquaredError:F2}, R²={valMetrics.RSquared:F2}");

        var testPredictions = model.Transform(testSet);
        var testMetrics = _ml.Regression.Evaluate(testPredictions);
        Console.WriteLine($"TEST: MAE={testMetrics.MeanAbsoluteError:F2}, RMSE={testMetrics.RootMeanSquaredError:F2}, R²={testMetrics.RSquared:F2}");

        // 6. Save
        _ml.Model.Save(model, trainSet.Schema, modelOutputPath);

        return model;
    }

    public YieldPrediction Predict(string modelPath, YieldFeatures input)
    {
        var loadedModel = _ml.Model.Load(modelPath, out _);
        var predictionEngine = _ml.Model.CreatePredictionEngine<YieldFeatures, YieldPrediction>(loadedModel);
        return predictionEngine.Predict(input);
    }
}
```

---

## UI — Predictor w aplikacji

**Plik**: `Hodowcy/Views/YieldPredictorWindow.xaml`

```
┌────────────────────────────────────────────────────────┐
│ 🤖 PROGNOZA YIELD — DLA NOWEJ PARTII                   │
├────────────────────────────────────────────────────────┤
│                                                        │
│ HODOWCA:                                              │
│ Wybierz: [Kowalski (F-12) ▼]                          │
│                                                        │
│ Historyczne (auto):                                    │
│   • Yield avg 3 mies:  56.8%                          │
│   • Yield avg 12 mies: 58.1%                          │
│   • Klasy B avg:       4.2%                           │
│   • Pozytywy mikro:    1 (12 mies)                    │
│                                                        │
│ PLANOWANA PARTIA:                                     │
│   Liczba sztuk:     [4500]                            │
│   Waga avg szt (kg):[2.6 ]                            │
│   Wiek (dni):       [42  ]                            │
│   Rasa:             [Ross 308 ▼]                      │
│   Data uboju:       [25.05.2026 ▼]                    │
│                                                        │
│ TRANSPORT (auto z Webfleet + meteo):                  │
│   Czas trasy:       2.5 h                             │
│   Temp przewidywana:24°C (lato)                       │
│   HSI max:          78 (NIE krytyczny)                │
│                                                        │
│ ──────────────────────────────────────────────────    │
│                                                        │
│ 🎯 PROGNOZA AI:                                       │
│                                                        │
│   Predicted yield: 56.5%  ± 1.8%                      │
│   Confidence:      85%                                 │
│   Zakres:          54.7% - 58.3%                      │
│                                                        │
│   Top 3 czynniki wpływu:                              │
│   1. Hodowca historic 12mies (-1.5%)  ⚠              │
│   2. Wiek 42 dni (+0.3%)                              │
│   3. Lato +24°C (-0.6%)                               │
│                                                        │
│ 💰 KALKULACJA OPŁACALNOŚCI:                          │
│                                                        │
│   Cena oferowana hodowcy: [5.20 zł/kg]                │
│   Predicted yield:        56.5%                       │
│   Efektywny koszt mięsa:  9.20 zł/kg                  │
│                                                        │
│   Avg hodowców mix:       60% yield                   │
│   Efektywny koszt avg:    8.67 zł/kg                  │
│                                                        │
│   ⚠ Ta partia DROŻSZA niż średnia o 6.1%             │
│                                                        │
│   SUGEROWANA MAX CENA:   [5.05 zł/kg]                 │
│   Negocjuj lub odmów.                                 │
│                                                        │
└────────────────────────────────────────────────────────┘

[💾 Zapisz prognozę]  [📞 Skontaktuj hodowcę]  [✗ Odmów]
```

---

## WORKFLOW UŻYTKOWANIA

### Predykcja nowej partii
1. Hodowca dzwoni: "mam 4500 sztuk, 2.6 kg, możesz odebrać 25.05?"
2. Otwierasz YieldPredictorWindow
3. Wybierasz hodowcę → auto-wypełnione historie
4. Wpisujesz parametry partii
5. System przewiduje yield + opłacalność
6. Negocjujesz cenę
7. **Po uboju** wracasz, wpisujesz rzeczywisty yield → dane do retraining

### Retraining
- **Co miesiąc** uruchom retraining na pełnym zbiorze
- Porównaj nowy model vs stary (na test set)
- Jeśli nowy lepszy (MAE niższy) → aktywuj
- Stary archiwizuj

### Monitoring jakości
- **MAE** powinien być <2% (na pierwszy rok 3-4% akceptowalne)
- **R²** powinien być >0.5
- **Drift detection** — jeśli predykcje systematycznie różne od rzeczywistości → coś się zmieniło (nowy hodowca, nowa rasa)

---

## EXPLAINABILITY — SHAP values

Każda predykcja przychodzi z **breakdown czemu**:

```
Predicted yield: 56.5%

Wkład cech:
+ Bazowa średnia:           60.0%
- HodowcaYield_12mies (58):  -1.5%
- Lato +24°C:                -0.6%
- DOA 0.3% (norma <0.2%):    -0.8%
+ Wiek 42 dni (sweet spot):  +0.3%
+ Rasa Ross 308 (good):      +0.2%
- Lairage przewidziane 2.5h: -0.5%
─────────────────────────────────
= Końcowa prognoza:          56.5%
```

To **przejrzysta dyskusja** z hodowcą: "twoje historyczne dane ciągną cię w dół o 1.5%".

---

## STRATEGIA OD MVP DO PRODUCT

### Faza 0 — przygotowanie danych (3-6 mies)
- Wdrożenia #2 (DOA), #5 (Lairage)
- Backfill historyczny yield z HANDEL
- Zbieranie 200+ rekordów

### Faza 1 — MVP (1 mies pracy)
- Pierwszy model LightGBM
- Prosty UI (predict only)
- Akceptowalne MAE 4-5%
- Manual integracja w workflow

### Faza 2 — Polish (1 mies)
- SHAP explainability
- Auto-retraining co miesiąc
- A/B test: czy decyzje cenowe lepsze niż przed?

### Faza 3 — Production (po pół roku)
- MAE <2%
- Auto-flagowanie partii do odmowy
- Integracja z `Zamowienia` (po zatwierdzeniu prognozy → auto-przyjęcie)
- Premia za wzorowych hodowców (oparta na ML score)

### Faza 4 — Ekspansja (po roku)
- ML także dla: drip loss, klasa B, koszt operacyjny
- Cluster analysis: które hodowcy są podobni
- Recommender system: "dla tego klienta najlepsze są partie z hodowców X, Y, Z"

---

## CZAS IMPLEMENTACJI

| Etap | Czas |
|---|---|
| Tabele bazy (3 nowe) | 6h |
| ETL backfill z HANDEL+LibraNet | 24h |
| Trainer ML.NET + walidacja | 24h |
| Predictor service | 12h |
| UI Window | 20h |
| SHAP-like explainability | 16h |
| Auto-retraining job | 16h |
| Pilot 2 miesiące (zbieranie feedback) | 80h |
| **RAZEM** | **~120h pracy** |

**Plus**: 6-12 miesięcy zbierania danych przed solidnym wdrożeniem.

---

## RYZYKA

⚠️ **Mało danych na start** — pierwszy model MAE 4-5% (czyli kiepski). Trzeba poczekać 6-12 mies aż MAE <2%.
⚠️ **Hodowcy oburzeni** — "ML mówi że jestem zły!". Mitygacja: pokazuj SHAP (konkretne czynniki), nie tylko liczbę.
⚠️ **Concept drift** — rynek się zmienia, model się starzeje. Retraining co miesiąc.
⚠️ **Overfitting** — model dobrze działa na trening, źle na nowe partie. Mitygacja: dobra walidacja + simple model.
⚠️ **Edge cases** — pierwszy raz nowy hodowca, nowy ras → model nie wie. Mitygacja: confidence score, manualna ocena.
⚠️ **Wzajemna zależność z innymi pomysłami** — bez #2, #5, #19 model gorszy. Czekaj na nie.

---

## ALTERNATYWA: PROSTSZA HEURYSTYKA (na start)

Zamiast ML można zacząć od prostej formuły:

```csharp
double PredictYieldHeurystyka(int hodowcaId, ...)
{
    var avg12mies = GetHodowcaYield12Mies(hodowcaId);
    var seasonAdjust = sezon == "LATO" ? -0.8 : (sezon == "ZIMA" ? -0.3 : 0);
    var weightAdjust = (wagaAvgSzt < 2.0 || wagaAvgSzt > 3.0) ? -0.5 : 0;
    var transportAdjust = czasTransportuH > 3 ? -0.4 : 0;
    
    return avg12mies + seasonAdjust + weightAdjust + transportAdjust;
}
```

**Heurystyka:**
- MAE: ~3-4% (gorsza niż ML)
- Łatwa do wytłumaczenia
- Bez infrastruktury ML
- **5h pracy**, działa od razu

**Strategia: heurystyka → ML (gdy będzie wystarczająco danych)**.
