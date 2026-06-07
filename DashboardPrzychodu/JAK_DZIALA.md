# Przychod Zywca LIVE — jak to działa

> **Cel dokumentu:** żebyś za 30 sekund wiedział **skąd bierze się każda liczba** na ekranie i **gdzie szukać** jak coś jest źle.
>
> Czytaj od góry do dołu — kolejność = od ogólnego do szczegółowego.

---

## 1. Co dashboard robi w jednym zdaniu

Co 30 sekund pobiera z **LibraNet** (waga portierska + harmonogram hodowców) i **HANDEL** (Symfonia, ubój) dane na wybrany dzień, łączy je w pamięci, **i pokazuje 3 widoki**: KPI u góry, listę dostaw w środku, sidebar po lewej.

---

## 2. Skąd biorą się dane (3 źródła)

```
   LIBRANET (192.168.0.109)                 HANDEL (192.168.0.112)
   ┌──────────────────────┐                ┌──────────────────────┐
   │ HarmonogramDostaw    │ ──── PLAN ──→  │     (nie używamy)    │
   │ ile hodowca obiecał  │                │                      │
   ├──────────────────────┤                │ HM.MG + HM.MZ + TW   │
   │ FarmerCalc           │ ── RZECZ ──→   │ sPWU = faktyczny     │
   │ portier waga rampa   │                │ uboj klasy A/B       │
   ├──────────────────────┤                └──────────────────────┘
   │ Dostawcy             │ → NAZWY ────────────┐
   │ słownik hodowców     │                    │
   ├──────────────────────┤                    │
   │ FarmerCalcChangeLog  │ ── HISTORIA ───────┤
   │ kto zmienił dekl.    │                    │
   └──────────────────────┘                    │
                                               ↓
                                       DASHBOARD LIVE
```

### Mapowanie pojęć branżowych → tabel w bazie

| Co Asia / Sergiusz mówi  | Co jest w bazie                                  | Tabela                |
|--------------------------|--------------------------------------------------|-----------------------|
| "Hodowca obiecał 6000 szt" | `SztukiDek` (varchar!)                         | `HarmonogramDostaw`   |
| "Hodowca obiecał wagę 2.4 kg/szt" | `WagaDek` (varchar!)                    | `HarmonogramDostaw`   |
| "Ile aut hodowca wyśle"  | `Auta` (varchar!)                                | `HarmonogramDostaw`   |
| "Brutto + Tara"          | `FullWeight`, `EmptyWeight`                      | `FarmerCalc`          |
| "Netto = co kupiliśmy"   | `NettoWeight` = `FullWeight - EmptyWeight`       | `FarmerCalc`          |
| "Sztuki licznika"        | `LumQnt`                                         | `FarmerCalc`          |
| "Padłe / konfiskaty"     | `DeclI2` / `DeclI3+I4+I5`                        | `FarmerCalc`          |
| "Hodowca podał wagę"     | `NettoFarmWeight` (z deklaracji)                 | `FarmerCalc`          |

### Gotcha #1: varcharowe pola w harmonogramie

`SztukiDek`, `WagaDek`, `Auta`, `SztSzuflada` są **VARCHAR** (tak, w głupocie). Dlatego w SQL używamy:
```sql
TRY_CAST(hd.SztukiDek AS INT)
ISNULL(TRY_CAST(hd.WagaDek AS DECIMAL(10,3)), 0)
```
**Jeśli ktoś wpisał coś dziwnego do harmonogramu** (np. "5000 + 500" zamiast "5500"), `TRY_CAST` zwraca `NULL` → plan dla tego harmonogramu = 0 kg. **Możliwy bug.**

---

## 3. Cykl życia danych (co się dzieje co 30 sek)

```
1. Tick timera 30s   →   PrzychodService.GetAllAsync(data)
2. Otwórz połączenie LibraNet
3. Wykonaj 1 query (4 result-sety) z pliku PrzychodLiveAll.sql
   ↓
4. Result-set 1: DOSTAWY    → lista per auto (DataGrid)
5. Result-set 2: PODSUMOWANIE → 1 wiersz KPI dnia
6. Result-set 3: PROGNOZA   → alert redukcji zamówień
7. Result-set 4: POSTĘPY    → karty hodowców (sidebar)
   ↓
8. Równolegle: GetFaktycznyPrzychodAsync (HANDEL sPWU) — klasy A/B
9. Równolegle: GetHistoriaZmianAsync — historia zmian deklaracji
10. Dispatcher.InvokeAsync → wypełnij UI w wątku UI
11. Zachowaj selekcję + scroll DataGrid
12. Czekaj 30 sek → goto 1
```

**Plik SQL** który generuje wszystko: `DashboardPrzychodu/SQL/PrzychodLiveAll.sql`. Możesz go otworzyć w SSMS, podstawić `@Data = '2026-06-06'` i zobaczyć każdy result-set osobno.

---

## 4. KPI Strip u góry (7 kafelków)

| Kafelek          | Wartość                       | Wzór                                                | Plik / linia                          |
|------------------|-------------------------------|-----------------------------------------------------|---------------------------------------|
| **PLAN**         | `txtKgPlan`                   | `SUM(SztukiDek × WagaDek)` z unikalnych harmonogramów | SQL: result-set 2, `KgPlanSuma`     |
| **ZWAŻONE**      | `txtKgZwazone`                | `SUM(NettoWeight)` gdzie `FullWeight>0 AND EmptyWeight>0` | SQL: `KgZwazoneSuma`              |
| **POZOSTAŁO**    | `txtKgPozostalo`              | `Plan - Zwazone` (computed w modelu)                | `PodsumowanieDnia.KgPozostalo`       |
| **ODCHYLENIE**   | `txtOdchylenie`               | `KgZwazoneSuma - KgPlanDoZwazonych`                 | SQL: `OdchylenieKgSuma`              |
| **TUSZKI**       | `txtPrognozaTuszek`           | `KgZwazoneSuma × 0.78` (hardcoded!)                 | `PodsumowanieDnia.PrognozaTuszekKg`  |
| **ETA**          | `txtEta`                      | `teraz + (pozostalo / tempo)`                       | `PodsumowanieDnia.EtaZakonczenia`    |
| **REALIZACJA**   | `txtRealizacja`               | `KgZwazoneSuma / KgPlanSuma × 100`                  | `PodsumowanieDnia.ProcentRealizacjiKg` |

### ⚠️ Gotcha #2: ODCHYLENIE nie jest "Zwazono - Plan dnia"

`OdchylenieKgSuma` = `Zważono - KgPlanDoZwazonych` (czyli **plan tylko dla tych aut, które już zważono**), **nie minus pełen plan dnia**.

Przykład:
- Plan dnia: 70 000 kg
- Zważono: 50 000 kg (przyjechało 40 z 52 aut)
- Plan dla tych 40 aut: 49 200 kg
- **Odchylenie: +800 kg** (zważono 800 kg więcej niż plan dla tych 40 aut)

Jeśli liczyłbyś jak `50 000 - 70 000 = -20 000 kg`, mylił byś się o cały dzień. **To celowo tak.**

### ⚠️ Gotcha #3: TUSZKI = 0.78 hardcoded

W `PodsumowanieDnia.cs:14`:
```csharp
private const decimal WspolczynnikTuszek = 0.78m;
```
Realna wydajność zależy od rasy/hodowcy/sezonu (typowo 75-82%). To **realny bug biznesowy** który był wskazany w pierwszym audycie — ale jeszcze nie naprawiony.

### ⚠️ Gotcha #4: KLASA A/B = 80%/20% hardcoded

```csharp
private const decimal WspolczynnikKlasaA = 0.80m;
private const decimal WspolczynnikKlasaB = 0.20m;
```
Realny stosunek wychodzi z `sPWU` w HANDEL — sidebar tuszek pokazuje go w wierszu "Fakt", ale prognozy `PrognozaKlasaAKg/BKg` używają nadal 80/20.

---

## 5. Sidebar po lewej (od góry)

### 📦 Harmonogramy (karty hodowców)
- Źródło: result-set 4 (`Postepy`), tabela `HarmonogramDostaw + FarmerCalc`
- Pokazują: nazwę hodowcy, plan kg, rzecz kg, % realizacji, średnią wagę plan → rzecz, postep `X/Y aut`
- Kolor karty = deterministyczny hash nazwy hodowcy (`DashboardBrushes.DeterministicHash`) → ten sam Jan zawsze ma ten sam kolor
- Aktywne karty (nowe ważenie od poprzedniego refresh) mają pulsujący border

### 🍗 Tuszki — prognoza
- `txtTuszkiPlanSidebar` = `KgPlanSuma × 0.78`
- `txtTuszkiRzeczSidebar` = `KgZwazoneSuma × 0.78`
- Klasa A/B 3 wartości: **Plan** (z 80/20), **Rzecz** (prognoza z dnia), **Fakt** (z Symfonii sPWU)

### ⚡ Akcje (expander) — `BtnExportExcel/Print/Help/Diagnose`

### 📝 Historia deklaracji (TOP 100 dnia)
- Źródło: `dbo.FarmerCalcChangeLog` filtrowane do `Szt.Dek`, `Waga Brutto Hodowca`, `Waga Tara Hodowca`
- Format: `HH:mm  [pole]  Hodowca  old → new`
- Tooltip: pełen kontekst + kto zmienił

---

## 6. DataGrid środkowy (11 kolumn)

| Kolumna       | Co pokazuje                                          | Skąd (model `DostawaItem`)                    |
|---------------|------------------------------------------------------|-----------------------------------------------|
| **LP**        | Numer kursu (`CarLp` z FarmerCalc)                   | `NrKursu`                                     |
| **HODOWCA**   | Nazwa z `Dostawcy.Name` lub fallback z harmonogramu  | `Hodowca`                                     |
| **PLAN**      | Plan kg **na to konkretne auto**                     | `KgPlanNaAuto` (computed)                     |
| **RZECZ**     | Netto z wagi portierskiej                            | `KgRzeczywiste = NettoWeight`                 |
| **ODCH.PLAN** | Netto − plan na auto                                 | `OdchylenieVsPlanAutoDisplay`                 |
| **POZOST.**   | Ile kg zostało do zważenia z harmonogramu hodowcy    | `KgPozostalo` (tylko na ostatnim wierszu hodowcy) |
| **POST.**     | `AutaZwazone / AutaOgolem` + progressbar             | `PostepDisplay`                               |
| **W.DEK**     | Średnia waga deklarowana z harmonogramu              | `SredniaWagaPlanCalc`                         |
| **W.RZ**      | Średnia waga rzeczywista = `Netto / SztRzecz`        | `SredniaWagaRzeczywistaCalc`                  |
| **TUSZKI**    | Netto × 0.78 (tylko dla zważonych)                   | `TuszkiRzeczywisteKg`                         |
| **GODZ**      | Godzina przyjazdu                                    | `PrzyjazdDisplay`                             |

### ⚠️ Gotcha #5: PLAN per auto zależy od trybu Stare/Nowe

**Tryb "Stare":**
```
PlanNaAuto = PlanKgLacznie / AutaPlanowane   (równo)
```
Czyli jeśli harmonogram = 18 000 kg / 3 auta → każde auto dostaje 6000 kg.

**Tryb "Nowe":**
```
PlanNaAuto[i] = SztukiExcel[i] × WagaDek      (per auto)
PlanNaAuto[ostatnie] = PlanKgLacznie - suma poprzednich  (reszta)
```
Wymaga że `SztukiExcel` (z AVILOG) są wpisane w FarmerCalc. Jeśli nie są → plan = 0 → odchylenie wygląda groteskowo.

### ⚠️ Gotcha #6: Ostatnie auto w grupie ma "(R)"

W kolumnie PLAN przy ostatnim niezważonym aucie pojawia się małe `(R)` w kolorze amber. To znaczy: **plan dla tego auta = reszta z harmonogramu** (czyli "ile zostało dowieźć żeby plan się zgadzał"), a nie `PlanKgLacznie / AutaPlanowane`.

To po to żeby suma planów per auto = plan z harmonogramu (inaczej ostatnie auto by miało sztywny plan i ciągle byłoby z odchyleniem).

---

## 7. Wiersz SUMA na dole DataGrid

| Komórka              | Wzór                                               |
|----------------------|----------------------------------------------------|
| `txtSumaDostawy`     | `dostawyView.Count` (po filtrze search)            |
| `txtSumaPlan`        | `SUM(d.KgPlanNaAuto)` — plan dla wszystkich dostaw |
| `txtSumaRzecz`       | `SUM(d.KgRzeczywiste)` gdzie Status=Zważony        |
| `txtSumaOdchylenie`  | `SUM(d.OdchylenieKgCalc)` (computed per wiersz)    |
| `txtSumaPozostaloKg` | `SUM(KgPozostalo)` z **unikalnych** harmonogramów (nie duplikujemy!) |

### ⚠️ Gotcha #7: SUMA Pozostało

Iterujemy po `_dostawy.GroupBy(LpDostawy).First()` — bierzemy **tylko jeden wiersz na harmonogram** (bo każdy hodowca ma X aut, ale `KgPozostalo` jest takie samo dla wszystkich aut tego samego hodowcy). Jeśli ta logika dedupingu się zepsuje → suma będzie zawyżona o ×ilość aut hodowcy.

---

## 8. Skąd biorą się obliczone wartości w modelu `DostawaItem`

| Property                          | Wzór                                                          |
|-----------------------------------|---------------------------------------------------------------|
| `Status`                          | Brutto>0 && Tara>0 → Zważony; Brutto>0 → BruttoWpisane; else Oczekuje |
| `KgPlanNaAuto`                    | NowyPlanKg ?? (CzyOstatnieAuto ? KgPozostalo : PlanKgLacznie/AutaPlanowane) |
| `CzyOstatnieAuto`                 | AutaCzekajacych ≤ 1 AND Status=Oczekuje                       |
| `SredniaWagaRzeczywistaCalc`      | Netto/Szt jeśli oba >0, else SredniaWagaRzeczywista           |
| `WagaTuszkiKg`                    | SrWagaRzecz × 0.78                                            |
| `SztukWPojemniku`                 | 15 kg / WagaTuszkiKg (klasa = rozmiar kurczaka)               |
| `OdchylenieVsPlanAutoKg`          | Z SQL: Netto - PlanKgLacznie/AutaPlanowane                    |
| `OdchylenieVsDeklHodowcaKg`       | Z SQL: Netto - NettoFarmWeight (lub WagaDek)                  |
| `OdchylenieKg` (legacy alias)     | OdchylenieVsPlanAutoKg ?? OdchylenieVsDeklHodowcaKg           |
| `OdchylenieProcCalc`              | (KgRzecz - planRef) / planRef × 100                           |
| `Poziom`                          | OK ≤ ±2%, Uwaga ≤ ±5%, Problem > 5%; dodatnia zawsze OK       |

---

## 9. Dwa rodzaje odchylenia — kluczowe rozróżnienie

Dashboard ma **dwie różne metryki odchylenia** dla każdej dostawy. To celowe.

### A. `OdchylenieVsPlanAuto` — "vs plan dispatcher'a"
- **Co mówi:** "Czy auto przywiozło dokładnie tyle, ile dispatcher zaplanował"
- **Wzór:** `Netto − (PlanKgLacznie / AutaPlanowane)`
- **Zastosowanie:** kontrola realizacji harmonogramu, czy auta są "porównywalne wielkościowo"
- **Bug objawia się gdy:** AutaPlanowane = 0 lub PlanKgLacznie = 0 → odchylenie = NULL

### B. `OdchylenieVsDeklHodowca` — "vs to co hodowca obiecał"
- **Co mówi:** "Czy hodowca kłamie ile waży drób"
- **Wzór:** `Netto − COALESCE(NettoFarmWeight, WagaDek)`
- **Zastosowanie:** wykrywanie systematycznych kłamców (klasyczny use case branżowy)
- **Bug objawia się gdy:** ani NettoFarmWeight ani WagaDek nie ma → odchylenie = NULL

**Pierwsza metryka jest pokazywana** w kolumnie ODCH.PLAN. Druga jest w tooltipie. KPI Strip i Wiersz SUMA używają wzoru pośredniego z SQL (`OdchylenieKgSuma = KgZwazoneSuma - KgPlanDoZwazonych`).

---

## 10. Gdzie najczęściej coś idzie nie tak — checklist debug

Jeśli **liczba na ekranie wygląda dziwnie**, sprawdź po kolei:

### Krok 1: Czy w bazie są w ogóle dane na ten dzień?
```sql
SELECT COUNT(*)
FROM dbo.FarmerCalc
WHERE CalcDate = '2026-06-06' AND ISNULL(Deleted, 0) = 0;
```
Zero rekordów → dashboard pokaże same zera lub puste pola.

### Krok 2: Czy `LpDostawy` w FarmerCalc matchuje `Lp` w HarmonogramDostaw?
```sql
SELECT fc.LpDostawy, COUNT(*) AS dostaw, MAX(hd.Lp) AS harmonogramLp
FROM dbo.FarmerCalc fc
LEFT JOIN dbo.HarmonogramDostaw hd ON TRY_CAST(fc.LpDostawy AS INT) = hd.Lp
WHERE fc.CalcDate = '2026-06-06'
GROUP BY fc.LpDostawy;
```
Jeśli `harmonogramLp` = NULL dla niektórych dostaw → plan = 0 dla tych aut.

### Krok 3: Czy `SztukiDek`/`WagaDek` w harmonogramie są poprawnymi liczbami?
```sql
SELECT Lp, Dostawca, SztukiDek, WagaDek, Auta,
       TRY_CAST(SztukiDek AS INT) AS sztOK,
       TRY_CAST(WagaDek AS DECIMAL(10,3)) AS wagaOK,
       TRY_CAST(Auta AS INT) AS autaOK
FROM dbo.HarmonogramDostaw
WHERE Lp IN (SELECT DISTINCT TRY_CAST(LpDostawy AS INT) FROM dbo.FarmerCalc WHERE CalcDate = '2026-06-06');
```
Jeśli któraś kolumna `*OK` jest NULL → ktoś wpisał coś dziwnego do harmonogramu.

### Krok 4: Czy ktoś niedawno edytował dane?
```sql
SELECT TOP 20 *
FROM dbo.FarmerCalcChangeLog
WHERE CalcDate = '2026-06-06'
ORDER BY ChangedAt DESC;
```
Możesz zobaczyć kto zmienił co i kiedy.

### Krok 5: Sprawdź diagnostykę w aplikacji
W bottom bar dashboardu jest przycisk **🔧 DIAG** (red). Klika otwiera okno które:
- Testuje każdą kolumnę z FarmerCalc osobno
- Pokazuje liczbę rekordów na dzień
- Sprawdza obliczenia CASE WHEN
- Pokazuje przykładowy rekord surowy

Wynik kopiuje do schowka. Wyślij to do mnie jeśli coś dziwnego.

### Krok 6: Włącz tryb "Stare" jeśli "Nowe" miesza wartości
W górnej belce są radio: **Stare / Nowe**. Jeśli przy "Nowe" liczby są dziwne, a przy "Stare" OK → problem jest z `SztukiExcel` w FarmerCalc (puste = "Nowe" liczy 0 × WagaDek = 0).

---

## 11. Mapa plików — gdzie czego szukać w kodzie

| Co chcesz zmienić                          | Plik                                                          |
|--------------------------------------------|---------------------------------------------------------------|
| Wartość kafelka KPI (np. dodać podtekst)   | `Views/DashboardPrzychoduWindow.xaml.cs` → `UpdateSummaryUI`  |
| Wzór odchylenia                            | `SQL/PrzychodLiveAll.sql` (result-set 1, kolumny OdchylenieVsPlanAutoKg) |
| Dodać kolumnę DataGrid                     | `Views/DashboardPrzychoduWindow.xaml` (po `W.RZ`)             |
| Logikę "Stare/Nowe" plan                   | `Views/DashboardPrzychoduWindow.Plan.cs`                      |
| Kolor hodowcy / deterministyczny hash      | `Theme/DashboardBrushes.cs`                                   |
| Konfigurację persistence                   | `Services/DashboardSettings.cs`                               |
| Wzór ETA / Pace                            | `Models/PodsumowanieDnia.cs`                                  |
| Faktyczny sPWU z Symfonii                  | `SQL/FaktycznyPrzychodSymfonia.sql`                           |
| Historia zmian (TOP 100 dnia)              | `SQL/HistoriaZmianDeklaracji.sql`                             |
| Eksport Excel / wydruk                     | `Views/DashboardPrzychoduWindow.ExportPrint.cs`               |

---

## 12. Co jest "po staremu" i może być źródłem starych bugów

| Element                                | Status                                                  |
|----------------------------------------|---------------------------------------------------------|
| `0.78` wydajność tuszek (hardcoded)    | ❌ **Realny bug biznesowy** — powinno być z avg 30 dni  |
| `0.80/0.20` klasa A/B (hardcoded)      | ❌ **Realny bug biznesowy** — powinno być z avg z sPWU  |
| `sa` z hasłem do HANDEL w `PrzychodService.cs:48` | ⚠️ Security gap (znany)                       |
| `pronova/pronova` do LibraNet          | ⚠️ Security gap (znany)                                 |
| Tryb "Nowe" wymaga ręcznego `SztukiExcel` | ⚠️ Jeśli puste → plan = 0                            |
| `PodsumowanieDnia.WorkdayStart=6:00, End=14:00` defaultowe | ⚠️ Konfigurowalne w settings ale nie w UI |

---

## 13. Format danych (na wszelki wypadek)

| Pole bazy            | Format                                       |
|----------------------|----------------------------------------------|
| `FarmerCalc.CalcDate`   | DATE (`2026-06-06`)                       |
| `FarmerCalc.Przyjazd`   | DATETIME                                  |
| `FarmerCalc.NettoWeight`, `FullWeight`, `EmptyWeight` | DECIMAL kg |
| `FarmerCalc.LumQnt`     | INT (sztuki)                              |
| `FarmerCalc.LpDostawy`  | INT (klucz do harmonogramu)               |
| `HarmonogramDostaw.Lp`  | INT (primary key)                         |
| `HarmonogramDostaw.SztukiDek` | **VARCHAR** (!!)                    |
| `HarmonogramDostaw.WagaDek` | **VARCHAR** (!!)                       |
| `HarmonogramDostaw.Auta` | **VARCHAR** (!!)                         |

---

## Jeśli widzisz coś dziwnego — daj mi:
1. Datę na której to widzisz
2. Screen ekranu (lub kopiuj wynik DIAG do schowka)
3. Co dokładnie się **nie zgadza** (np. "Plan dnia pokazuje 70t a w harmonogramie mam 80t")
4. Co spodziewasz się że powinno być

Wtedy odpalę odpowiednie zapytanie ze sekcji 10 i znajdę źródło w 5 minut.
