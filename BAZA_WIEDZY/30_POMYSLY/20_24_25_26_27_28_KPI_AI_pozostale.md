# Punkty 20, 24-28 — KPI Cockpit, AI assistance, audyt

---

## 20. Drip Loss Tracker

### Co to jest
**Drip loss** = ile wody/krwi wycieka z tuszki podczas chłodzenia. Norma 1-2%.

Z PDF Broiler: drip loss = wskaźnik jakości mięsa. Wysoki = WB, niska jakość żywca, źle wykrwawiona, zbyt szybkie chłodzenie.

### Implementacja
**Workflow**:
1. Co tydzień losowy sampling 10 tuszek
2. Waga przed chłodzeniem
3. Waga po 24h (po chłodzeniu)
4. `Drip loss % = (waga_przed - waga_po) / waga_przed × 100`

### Database
```sql
CREATE TABLE DripLossSample (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    SampleDateTime DATETIME NOT NULL,
    PartiaId INT NOT NULL,
    HodowcaId INT NULL,
    WagaPrzedChlodzeniem DECIMAL(6,2) NOT NULL,
    WagaPoChlodzeniu DECIMAL(6,2) NULL,
    DripLossProc AS ((WagaPrzedChlodzeniem - WagaPoChlodzeniu) / WagaPrzedChlodzeniem * 100),
    Operator NVARCHAR(50) NULL,
    Notatki NVARCHAR(500) NULL
);
```

### Statystyki
- Per hodowca (czy żywiec daje wysoki drip?)
- Per krzywa chłodzenia (czy korelacja z #18?)
- Per WB obecność (czy WB ma wyższy drip?)

### Wartość
- Drip loss 2% zamiast 1% = -1 kg/100kg = -3 zł/100kg straty
- Detekcja problemów chłodzenia = oszczędność energii + jakość

### Czas: ~16h kodu

---

## 24. Microbial Risk Score per partia

### Co to jest
Algorytm risk score (0-100) dla każdej partii — pomaga QC alokować zasoby (priorytet kontroli).

### Formuła
```csharp
public int CalcRiskScore(int partiaId)
{
    int score = 0;
    
    // Czynniki ryzyka:
    score += GetDoaPct(partiaId) > 0.3 ? 15 : 0;       // wysokie DOA
    score += GetLairageH(partiaId) > 2 ? 10 : 0;       // długie lairage
    score += GetCropFullness(partiaId) > 0.5 ? 10 : 0; // pełen crop
    score += HasIncydentCCP24h() ? 20 : 0;             // incydent CCP
    score += GetHodowcaPositiveHistory(partiaId);      // historia hodowcy
    score += GetCellulitisPct(partiaId) > 2 ? 10 : 0;  // higiena
    score += IsHotSeason() ? 5 : 0;                    // lato = wyższe ryzyko
    
    return Math.Min(score, 100);
}
```

### Workflow
- Risk >70 → dodatkowa próbka mikro
- Risk >50 → opóźnienie wysyłki 24h dla obserwacji
- Risk <30 → normalna procedura

### Wartość
- Zapobieganie 1-2 recall'om/rok = **200-500k PLN**
- Optymalizacja zasobów QC

### Integracja: [#19], [#22], [#23]

### Czas: ~20h kodu

---

## 25. KPI Cockpit (30 wskaźników)

### Co to jest
**Jedno okno = 30 KPI**. Codziennie 5 min, znasz stan firmy.

### KPI z PDF + branża
1. **DOA %** (norma <0.2%)
2. **Lairage czas avg** (norma <2h)
3. **Yield uboju %** (norma >85%)
4. **Yield krojenia %** (norma >62%)
5. **Wyd. wykrwawienia %**
6. **Plucking damage %**
7. **Ascites %** (norma <1%)
8. **Cellulitis %** (norma <1%)
9. **WB+WS+SM %** (norma <5%)
10. **Drip loss %** (norma 1-2%)
11. **Chilling time h** (norma <6h)
12. **Cold chain compliance %** (target 99.5%)
13. **Salmonella pozytywy %** (target <1%)
14. **Campylobacter pozytywy %** (target <5%)
15. **Klasa A % vs B**
16. **Reklamacje sztuk/dzień**
17. **Reklamacje wartość PLN/dzień**
18. **Recall'e w miesiącu**
19. **CCP incydenty/dzień**
20. **Kalibracje overdue**
21. **Średnia waga partii kg**
22. **Avg cena żywca zł/kg**
23. **Avg cena fileta zł/kg**
24. **Marża brutto %**
25. **Hodowcy z alertami**
26. **Klienci aktywni**
27. **Ekspedycja dziś t**
28. **Pojazdy w trasie**
29. **Wycieki energii kWh**
30. **Welfare Index**

### UI
- Grid 5×6 kafelków
- Każdy: liczba + trend (↑/→/↓) + alert badge
- Klik kafelka → drill-down do szczegółów
- Auto-refresh co 1 min

### Czas: ~80h kodu (większość to integracje z innymi modułami)

---

## 26. Welfare Index Calculator

### Co to jest
**Welfare Index** = sumaryczny score dobrostanu zwierząt (wymóg eksportowy EU + audyty Lidl/Tesco/welfare).

### Składowe (wg WelFur / WelfareQualityProject)
- DOA % (im niżej, tym lepiej)
- Footpad lesions % (kontuzje stóp)
- Hock burns % (oparzenia stawów)
- Transport conditions (czas, temp, gęstość)
- Lairage conditions
- Stunning efektywność %

### Formuła
```
Welfare = 100 - (DOA × 50 + Footpad × 5 + HockBurn × 5 + 
                 TransportPenalty + LairagePenalty + StunningPenalty)
```

### Wartość
- Premium clients płacą +2-5% za welfare-certified
- Wymóg dla niektórych krajów eksportu

### Czas: ~40h kodu

---

## 27. Rejection Trend Heatmap (per type)

### Co to jest
**Heatmapa typu Github contributions** dla każdego typu wady. Pokazuje trendy + sezonowość.

```
Typ wady: ASCITES (2026, miesięcznie)

        Sty Lut Mar Kwi Maj Cze Lip Sie Wrz Paź Lis Gru
2024    ▓   ▓   ▒   ▒   ▒   ▒   ▓   ▓   ▒   ▒   ▓   ▓
2025    ▓▓  ▓   ▒   ▒   ▒   ▒   ▓▓  ▓▓  ▒   ▒   ▓   ▓
2026    ▓   ▓   ▒   ▓   ▒   ?   ?   ?   ?   ?   ?   ?
        
Skala: ░ <0.5%  ▒ 0.5-1.5%  ▓ 1.5-3%  ▓▓ >3% ⚠

Wniosek: ascites wzrasta latem (zatkana wentylacja w kurnikach).
Akcja: program ulepszania wentylacji u top 5 hodowców.
```

### Implementacja
**Reuse z #11** dane. Konwerter heatmap kolorów + grid.

### Czas: ~16h kodu

---

## 28. Photo + AI audyt

### Co to jest
**Każdy moduł** który ma do czynienia z jakością dostaje opcję "Załącz zdjęcie + AI analizuje".

### Reuse z #12 (Forensic Hematoma)
Ten sam pipeline AI VLM. Zmieniają się tylko prompty:
- Reklamacje → analiza wady fideta
- WadyPartii → klasyfikacja 12 typów wad
- Hodowcy → audyt kurnika (zdjęcie ściółki, wentylacji)
- Transport → zdjęcie crates po dotarciu
- CCP → zdjęcie chłodni z czujnikiem
- Mappage → zdjęcie palet z lot number (OCR)

### Wartość
- Konsystentne dane (każde zdjęcie → tabularny output)
- Mniej subiektywności
- Dowody dla audytora BRC

### Czas: ~24h (głównie modyfikacja istniejących okien)
