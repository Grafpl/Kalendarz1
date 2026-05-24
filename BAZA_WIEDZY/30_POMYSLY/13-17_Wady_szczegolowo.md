# Punkty 13-17 — Detekcja konkretnych wad mięsa

## 13. White Striping / Wooden Breast / Spaghetti Meat detector

### Co to jest (z PDF Broiler str. 138-145)
**White Striping (WS)**: białe pasy między włóknami mięśniowymi fileta
**Wooden Breast (WB)**: twardy filet, miejscami "drewniany"
**Spaghetti Meat (SM)**: włóknista struktura fileta

Wszystkie 3 to **muscle disorders** szybko-rosnących broilerów. Genetyka + żywienie.

### Wpływ na biznes
| Wada | Drip loss | Cooking loss | Cena fileta |
|---|---|---|---|
| Normal | 1.01% | 21.5% | 100% |
| WS lekki | 1.08% | 23.2% | 95% |
| WS ciężki | 0.90% | 26.9% | 85% |
| WB ciężki | 1.15% | 30.9% | 70% (→ przerób) |
| WS+WB | 1.05% | 31.8% | 60% (→ przerób) |

### Implementacja
**Hardware**: kamera 4K nad linią cięcia (~3000 zł, np. Hikvision DS-2CD2T46G2)

**AI**: Claude VLM (reuse z #12)

**Workflow**:
1. Kamera robi zdjęcie co 5 sekund
2. AI klasyfikuje filet: NORMAL / WS_LIGHT / WS_HEAVY / WB / SM / WS+WB
3. Operator dostaje powiadomienie ekran obok: "Wykryto WB → odsuń na bok"
4. Auto-segregacja: NORMAL → premium pack; WS/WB → mrożone klasa B; ciężkie → przerób

### Wartość
- Bez systemu: 100% jako klasa A → reklamacje, -10% średnio
- Z systemem: dobra segregacja → mniej reklamacji, +5% średnia cena
- **600-800k PLN/rok** (zależy od % WB w stadach)

### Database
```sql
CREATE TABLE FiletDetection (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    DetectionDateTime DATETIME NOT NULL,
    PartiaId INT NULL,
    LineaProdukcyjna NVARCHAR(20) NULL,
    KategoriaWady NVARCHAR(20) NOT NULL,
    -- NORMAL, WS_LIGHT, WS_HEAVY, WB_LIGHT, WB_HEAVY, SM, MIX
    ConfidenceScore DECIMAL(5,2) NULL,
    ZdjeciePath NVARCHAR(500) NULL,
    KierunekSortowania NVARCHAR(30) NULL  -- PREMIUM, KLASA_B, PRZEROB
);
```

### Czas: ~60h kodu + 4000 zł hardware

---

## 14. Pop-out & Fracture Cost Calculator

### Co to jest
Tabela z kosztami wad w PLN. Wiesz że są pop-outy, ale nie wiesz **ile Cię kosztują**.

### Implementacja
**Reuse z #11 (Digital Inspection)** — dane już są. Dodaj słownik kosztów per wada:

```sql
CREATE TABLE WadaKosztSlownik (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    TypWady NVARCHAR(50) NOT NULL UNIQUE,
    SredniaStrataKgNaWade DECIMAL(5,2) NOT NULL,  -- ile kg traci typowo tuszka
    SredniaWartoscPLNNaWade DECIMAL(8,2) NOT NULL,
    UwagiOpis NVARCHAR(500) NULL
);

INSERT INTO WadaKosztSlownik VALUES
('ASCITES', 1.8, 22, 'Cała tuszka utylizacja'),
('POPOUT_SKRZYDLO', 0.4, 5, 'Usunięcie skrzydła'),
('POPOUT_UDO', 0.6, 8, 'Klasa B uda'),
('HEMATOMA_PIERS_MALA', 0.1, 1, 'Trim'),
('HEMATOMA_PIERS_DUZA', 0.8, 12, 'Cały filet'),
('WB_CIEZKI', 0.6, 8, 'Filet do przerobu, -50%'),
('CELLULITIS', 1.5, 18, 'Cała tuszka utylizacja'),
('ZLAMANIE_KOSCI', 0.3, 4, 'Usunięcie elementu');
```

### UI
Widok "Koszty wad dnia":
```
Wada               | Liczba | Strata Σ kg | Strata Σ PLN
WB ciężki          |  89    |  53 kg      | 712 zł
Hematoma duza      |  24    |  19 kg      | 288 zł
Pop-out skrzydlo   |  47    |  19 kg      | 235 zł
Ascites            |  18    |  32 kg      | 396 zł
TOTAL              |  178   |  123 kg     | 1631 zł
                                          
Trend miesięczny: 1631/dzień × 30 = 48 930 zł/miesiąc
```

### Czas: ~16h kodu

---

## 15. Ascites Watcher (puchlinianie)

### Co to jest (z PDF Broiler str. 134)
**Ascites** = płyn w jamie brzusznej, powstaje gdy serce + naczynia nie nadążają z dotlenieniem szybko rosnących broilerów.

Główna przyczyna odrzutów weterynaryjnych.

### Wartość
- Każdy ascites = strata 1.5-2 kg × 12 zł = ~20-25 zł/sztuka
- 1% partii ascites norma, 5% = problem
- Detekcja farm z wysokim ascites → 30-50k PLN/rok per hodowca

### Implementacja
**Reuse z #11** — kolumna `Ascites` już jest

Nowy raport "Hodowcy z wysokim ascites":
```sql
SELECT 
    pd.HodowcaId, h.Nazwisko,
    COUNT(*) AS LiczbaPartii,
    AVG(CAST(w.Ascites AS DECIMAL(10,2)) / w.LiczbaSztukSprawdzonych * 100) AS AvgProcAscites,
    SUM(w.Ascites) AS TotalAscites
FROM WetInspectionRecord w
JOIN PartiaDostawca pd ON pd.Partia = w.PartiaId
JOIN Pozyskiwanie_Hodowcy h ON h.Id = pd.HodowcaId
WHERE w.InspectionDateTime >= DATEADD(MONTH, -3, GETDATE())
GROUP BY pd.HodowcaId, h.Nazwisko
HAVING AVG(CAST(w.Ascites AS DECIMAL(10,2)) / w.LiczbaSztukSprawdzonych * 100) > 2.0
ORDER BY AvgProcAscites DESC;
```

### Automatyczne porady
- ascites >5% → "redukcja obsady z 22 na 18 szt/m²"
- ascites trending up → "sprawdź wentylację w kurniku"
- ascites tylko zimą → "ogrzewanie nieprawidłowe"

### Czas: ~8h kodu (jeśli #11 już wdrożone)

---

## 16. Bone fracture heatmap

### Co to jest
Heatmapa pokazująca **kiedy** i **gdzie** na linii powstają złamania kości.

### Hipoteza
Pop-outy NIE są losowe. Powstają w określonych miejscach (skubarki) i czasach (kompresor rozregulowuje się przez upał).

### Implementacja
**Reuse z #11** — dane już są

Nowy widok "Heatmap złamania":
```
Heatmapa fractures (ostatnie 30 dni)

        6:00  8:00 10:00 12:00 14:00 16:00 18:00 20:00
Pn      ░░    ▒▒   ▓▓   ▓▓   ▒▒   ▒▒   ░░   ░░
Wt      ░░    ▒▒   ▒▒   ▓▓   ▓▓▓  ▓▓   ▒▒   ░░    ← peak 14:00
Sr      ░░    ▒▒   ▒▒   ▒▒   ▒▒   ▒▒   ░░   ░░
Cz      ░░    ▒▒   ▒▒   ▒▒   ▒▒   ▒▒   ▒▒   ░░
Pt      ░░    ▒▒   ▒▒   ▒▒   ▓▓   ▓▓   ▒▒   ░░

Skala: ░ <5/h  ▒ 5-15/h  ▓ 15-25/h  ▓▓ >25/h ⚠

Hipoteza: wzrost o 14:00 → kompresor skubarki #2 przegrzewa się
Akcja: konserwacja w południe (do 12:00)
```

### Wartość
- Detekcja systemowych problemów na linii
- Mniej pop-outów = wyższy yield premium = ~150-250k PLN/rok

### Czas: ~12h kodu

---

## 17. Cellulitis & polyserositis tracker

### Co to jest (z PDF Broiler str. 134-135)
**Cellulitis** = bakteryjne zapalenie tkanki podskórnej. Często pod skrzydłem lub na pośladkach. **Wskaźnik brudnej ściółki w kurniku**.

**Polyserositis** = zapalenie błon surowiczych. Wskazuje na **kurz i E. coli** w hali.

### Wartość
- Cellulitis = strata całej tuszki (utylizacja)
- 30-100 sztuk/partia × 25 zł = 750-2500 zł/partia straty
- Detekcja farm z chronicznym problemem → 100-200k PLN/rok

### Implementacja
**Reuse z #11** — kolumna `Cellulitis` już jest

Dashboard per hodowca z trendem 12-miesięcznym + porównanie do średniej rynkowej.

Auto-email do hodowcy z cellulitis >3%:
```
Szanowny Panie Kowalski,

W ostatnich 3 miesiącach Pańskie partie miały średnio 4.2% cellulitis 
(norma rynkowa: <1%). To wskazuje na potencjalny problem z higieną:
- Brudna lub mokra ściółka
- Nadmierne zagęszczenie obsady
- Niedostateczna wentylacja

Sugerujemy:
1. Audyt ściółki — wymiana częstsza
2. Kontrola wentylacji
3. Badanie wody pitnej (E. coli)

Czy możemy umówić wizytę zootechnika?

Pozdrowienia,
Dział Jakości Piórkowscy
```

### Czas: ~16h kodu
