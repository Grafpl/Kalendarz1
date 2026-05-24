# Instrukcja: Lista Partii V2 (moduł produkcyjny) — deep

> **Dla kogo**: Marcin (kierownik produkcji), brygadziści, operatorzy QC, Sergiusz.
> **Co robi**: zarządzasz **partiami uboju** — od wjazdu na rampę, przez patroszenie i QC, do zamknięcia partii. Pulpit dnia + pełna lista historyczna + 6-zakładkowy master-detail.
> **Pliki kodu**: `Partie/Windows/ListaPartiiWindow.xaml`, `Partie/Views/ProdukcjaDzisWidok.xaml`, `Partie/Views/WidokPartie.xaml`, `Partie/Services/PartiaService.cs` (~1600 linii), `Partie/Models/PartiaModels.cs`.
> **Otwierane z**: menu ZPSP → kafelka **🐔 Lista Partii** (permission `accessMap[58]`).

---

## 1. Czym jest "partia" (najprościej)

**Partia** = jeden dzień uboju, jednego hodowcy.

> Wojtek wstawił 22 000 piskląt 24.05 (cykl, instr. 01). 27.06 jego pierwsza dostawa trafia na rampę → powstaje **partia #5891**. Cały dzień produkcji tej dostawy = jedna partia.

Łańcuch: **cykl** (01-02) → **dostawa** (kalendarz, 05) → **partia** (ten plik) → rozbiór → sprzedaż.

---

## 2. Architektura — okno + 2 widoki

`ListaPartiiWindow` to **Window** (nie UserControl), 1500×900, Maximized. Zawiera **TabControl** z 2 zakładkami:

```
┌─────────────────────────────────────────────────────────────┐
│ [ 🏭 Produkcja Dzis ]  [ 📋 Lista Partii ]                 │
└─────────────────────────────────────────────────────────────┘
```

| Zakładka | Typ | Dla kogo | Po co |
|---|---|---|---|
| **🏭 Produkcja Dzis** | UserControl `ProdukcjaDzisWidok` | brygadzista | TU I TERAZ — co na linii |
| **📋 Lista Partii** | UserControl `WidokPartie` | analityk, Sergiusz | HISTORIA — wszystko + filtry |

Przełączanie:
- TabControl SelectionChanged.
- W WidokPartie przycisk **"Dashboard"** otwiera ProdukcjaDzis w **nowym oknie**.
- W ProdukcjaDzis przycisk **"Lista partii"** przełącza zakładkę.

---

## 3. Widok "🏭 Produkcja Dzis"

### Auto-refresh

`DispatcherTimer` co **30 sekund** → `LoadDataAsync(silent: true)`. Dane odświeżają się same, bez migania.

`LoadDataAsync` (silent lub z overlay):
1. `Task.WhenAll`: GetPartieDzisAsync + GetDzisHarmonogramAsync + GetNormyAsync + GetHourlyProductionBulkAsync.
2. `RunAutoStatusDetectionAsync` (auto-przejścia statusów).
3. `GetAlerts` (5 typów alertów).
4. `AssignSparklines`.
5. Update kart, statystyk, harmonogramu, alertów.

### 6 kafelków statystyk

| Kafelek | Formuła |
|---|---|
| **Partii dzis** | `_dzisPartie.Count` |
| **Otwartych** 🟢 | `Count(p => p.IsActive)` — status nie CLOSED/CLOSED_INCOMPLETE/REJECTED |
| **Wydano kg** 🔵 | `Sum(p => p.WydanoKg)` format N0 |
| **Sr. wydajnosc** 🟣 | `Average(p.WydajnoscProc)` gdzie != null, format N1 |
| **Sr. temp rampa** 🟠 | `Average(p.TempRampa)` gdzie != null |
| **Plan dostaw** 🟡 | `_harmonogram.Count` |

### Aktywne partie (karty)

- Źródło: `GetPartieDzisAsync()` = `GetPartieAsync(dzis, dzis)`.
- Filtr: partie z dzisiejszą datą + `IsActive`.
- Sort: `OrderByDescending(p.CreateGodzina)`.

Każda karta zawiera:
- Numer partii + badge statusu (kolor).
- Dostawca (CustomerName).
- Metryki: szt. deklarowane, Wydano kg, Na stanie kg, QC badge ✓/✗.
- **Sparkline** (mikro-wykres wydajności godzinowej).
- Godzina utworzenia + Wydajność % + przycisk **"Zmien status"** (jeśli możliwy następny stan).

### Zamknięte dzis (osobna sekcja)

- Filtr: `Where(p => !p.IsActive)` — CLOSED/CLOSED_INCOMPLETE/REJECTED.
- Sort: `OrderByDescending(p.CloseGodzina)`.
- Tło: #F0F0F0 lub #FFF8E1 (opacity 0.8) wg statusu.

### Sidebar Harmonogram (prawy)

- Źródło: `GetDzisHarmonogramAsync()` — `HarmonogramDostaw` gdzie `DataOdbioru = dzis`.
- Każda pozycja: Lp, Dostawca, Szt, Waga, Typ ceny, Cena.
- Flaga **MaPartie** (EXISTS check) — czy już istnieje przypisana partia.
- **Klik gdy !MaPartie** → otwiera `NowaPartiaDialog(HarmonogramItem)` z pre-fillem.
- **Klik gdy MaPartie** → info MessageBox.

### Alerty (5 typów — `GetAlerts`)

| # | Alert | Warunek | Poziom |
|---|---|---|---|
| 1 | Otwarcie > 3h bez wazeń | IsActive && WydanoKg==0 && (now-CreateTime)>3h | WARNING |
| 2 | Temp poza normą | TempRampa.HasValue && !IsInNorm | ERROR |
| 3 | Brak świadectwa wet. | string.IsNullOrEmpty(VetNo) && NettoSkup > 0 | WARNING |
| 4 | Klasa B > norma | KlasaBProc.HasValue && !IsInNorm | WARNING |

Sort: descending po severity (ERROR=2, WARNING=1). Panel widoczny tylko gdy są alerty.

### Detail Flyout (po kliknięciu karty)

Kliknięcie karty (nie przycisku) → overlay z dynamicznie budowanym StackPanel:
- **Informacje**: Dostawca, Dział, Otwarcie, Otworzył, Świad. wet.
- **Metryki**: Szt deklarowane, Netto skup, Wydano, Przyjeto, Na stanie, Wydajność %.
- **Kontrola jakości**: QC badge, Klasa B, Temp rampa (kolor ERROR jeśli poza normą), Wady, Zdjęcia, Padłe.
- **Produkcja (godz.)**: większy sparkline.
- **Akcje**: jeśli IsActive — ">> NextStatus" + "Zamknij"; jeśli !IsActive — "Otwórz ponownie".
- Zamknięcie: X lub **Escape**.

### Kolor statusu na badge (`ToRowBackgroundHex`)

| Status | Tło badge |
|---|---|
| IN_PRODUCTION | #E8F4FD (niebieski) |
| CLOSED | #F0F0F0 (szary) |
| CLOSED_INCOMPLETE | #FFF8E1 (żółty) |
| REJECTED | #FFEBEE (czerwony) |
| PLANNED | #F5F5F5 |
| AT_RAMP | #FFF3E0 |
| APPROVED | #E8F5E9 (zielony) |

---

## 4. Widok "📋 Lista Partii"

### Filtry

| Filtr | Domyślnie | Działanie |
|---|---|---|
| **Od** (dpOd) | dziś -7 dni | data utworzenia >= |
| **Do** (dpDo) | dziś | data utworzenia <= |
| **Dzial** (CmbDzial) | Wszystkie | 1A / 0E / 0K (z item.Tag) |
| **Status** (CmbStatus) | Wszystkie | Hybryda: legacy (Otwarte/Zamknięte) + V2 ("V2:PLANNED" itp.) |
| **Szukaj** (TxtSzukaj) | — | `Partia LIKE OR CustomerName LIKE OR CustomerID LIKE` (Enter) |

> Status combo jest hybrydowy: opcje "V2:PLANNED", "V2:IN_PRODUCTION" mapują na `statusV2Filter`, a "Otwarte"/"Zamknięte" na legacy `IsClose`.

### Przyciski toolbar

| Przycisk | Akcja | Skrót |
|---|---|---|
| **+ Nowa partia** 🟢 | NowaPartiaDialog (bez harmonogramu) | Ctrl+N |
| **Zamknij** 🟠 | ZamknijPartieDialog (wymaga IsClose != 1) | — |
| **Otworz** 🟣 | OtworzPartieDialog (wymaga IsClose == 1) | — |
| **Excel** | Export do .xlsx (wszystkie partie wg filtra!) | — |
| **Odswiez** | LoadDataAsync | F5 |
| **Dostawcy** 🟣 | DostawcaComparisonWindow (ranking dostawców) | — |
| **Dashboard** | Nowe okno z ProdukcjaDzisWidok | — |

### DataGrid (21 kolumn, DevExpress)

1. Pasek koloru (6px, status) · 2. Partia · 3. Data · 4. Godz. · 5. Dostawca · 6. ID Dost. · 7. Dzial · 8. Status (badge) · 9. Szt. dekl. · 10. Netto skup kg · 11. Wydano kg · 12. Wydano szt · 13. Przyjeto kg · 14. Przyjeto szt · 15. Na stanie kg · 16. Wydajn. % · 17. Klasa B % · 18. Temp rampa · 19. QC badge · 20. Świad. wet. · 21. Zamkniecie info.

Wszystkie sortowalne i filtrowalne (AutoFilter row).

### Stats bar (dół)

```
Partii: X (otwartych: Y, zamkniętych: Z) | Dzis: A partii, B kg | 
Sr. wydajnosc: C% | Sr. klasa B: D% | Sr. temp rampa: E C
```

Liczone in-memory (bez SQL) z `GetStatsAsync`.

---

## 5. Master-detail — 6 zakładek (lazy load)

Rozwinięcie wiersza → TabControl z 6 tabami. Pierwszy ("Wazenia") ładuje się od razu, reszta **lazy** na kliknięcie. Każda partia ma cache (6 dictionaries), czyszczony przy LoadDataAsync.

### Tab 1: Wazenia

- `GetWazeniaAsync(partia)` — `UNION Out1A + In0E WHERE P1 = @Partia ORDER BY Data DESC, Godzina DESC`.
- Kolumny: ArticleID, ArticleName, ActWeight, Quantity, Data, Godzina, Wagowy, Direction, Zrodlo ("Out1A"/"In0E").
- `IsStorno = ActWeight < 0`.

### Tab 2: Produkty

- `GetProduktyAsync(partia)` — GROUP BY ArticleID z Out1A, sumy + procenty.
- Kolumny: ArticleID, ArticleName, NettoKg, SztDodatnie, IleWazen, ProcentUdzialu.

### Tab 3: QC / Jakosc

- `GetQCDataAsync(partia)` — TemperaturyMiejsca, WadyPartiiSkale (TOP 1), PodsumaPartii, Zdjecia.
- Dynamiczny StackPanel z sekcjami:
  - **Temperatury** — tabela (Miejsce, Proba1-4, Srednia) + color highlight jeśli Srednia > 4°C (czerwony).
  - **Ocena wad** — gwiazdki (★/☆) dla Skrzydła, Nogi, Oparzenia (1-5).
  - **Podsumowanie** — Klasa B (czerwony jeśli >20%), Przekarmienie, Notatka.
  - **Zdjęcia** — lista (opis + typ wady + operator).

### Tab 4: Skup

- `GetSkupDataAsync(partia)` — TOP 1 z FarmerCalc LEFT JOIN Dostawcy, Driver.
- Sekcje: Dane skupu (Dostawca, Data, Kierowca, Pojazd), Wagi (Brutto/Tara/Netto), Sztuki (Deklarowane/Padłe), Rozliczenie (Cena/Wartość), Czas (Wyjazd/Załadunek/Przyjazd HH:mm).

### Tab 5: HACCP

- `GetHaccpAsync(partia)` — `Haccp WHERE P1 = @Partia OR P2 = @Partia`.
- Kolumny: ZDzialu, Artykul, PartiaZrodlowa → NaDzial, ArtykulDocelowy, PartiaDocelowa, SumaKg, MinDate, MaxDate.
- **Traceability** — śledzenie produktów przetworzonych między działami.

### Tab 6: Timeline

- `GetTimelineAsync(partia)` — UNION: OPEN event + CLOSE event + Out1A wazenia + In0E wazenia, ORDER BY EventTime ASC.
- ItemsControl z ikonami: OPEN→🟢, CLOSE→🔴, WEIGHT→⚖, TEMP→🌡, QC→🔍, PHOTO→📷, TRANSPORT→🚚.

---

## 6. NowaPartiaDialog

### Pola

| Pole | Wymagane | Co |
|---|---|---|
| **Dzial** (cmbDzial) | ✅ | 1A / 0E / 0K (manual items), default 1A |
| **Dostawca** (cmbDostawca) | ✅ | `GetDostawcyAsync()` = `SELECT ID, ShortName FROM Dostawcy WHERE Halt=0` |
| **Nr partii** | ❌ Auto | system generuje |

### Z harmonogramu (zielony banner)

Jeśli otwarte przez kliknięcie HarmonogramItem:
- Banner "Z HARMONOGRAMU DOSTAW".
- Dostawca auto-select (Name.IndexOf >= 0).
- Subdetal (informacyjne): Szt deklarowane, Waga, Typ ceny, Cena — **read-only**, partia tworzy się z bieżącego stanu bazy.

### Po zapisaniu

- `CreatePartiaFromHarmonogramAsync(dirId, customerID, customerName, null, App.UserID, harmonogramLp)`.
- Generuje nrPartii: `{rok2}{dzienRoku}{nextNo:D3}` (np. 24001001).
- Status początkowy: **PLANNED** (z harmonogramu) lub auto-detect.
- INSERT: partnumbers + listapartii + PartiaDostawca + PartiaStatus + AuditLog.

---

## 7. ZamknijPartieDialog (QC checklist)

### Co widać

- Header: dostawca, data otwarcia, operator.
- Podsumowanie produkcji: Wydano (kg+szt), Przyjeto, Na stanie, Wydajność.
- **QC Checklist** (5 pozycji).
- Komentarz zamknięcia (TextBox).
- Ostrzeżenie >10 kg na stanie (jeśli dotyczy).

### 5 pozycji QC

| # | Pozycja | IsOK gdy | IsWarning gdy |
|---|---|---|---|
| 1 | **Temperatury** | MaTemperatury && w normie | MaTemperatury && poza normą |
| 2 | **Ocena wad** | MaWady (S/N/O wypełnione) | — |
| 3 | **Klasa B %** | KlasaBProc && w normie (≤15-20%) | KlasaBProc && poza normą |
| 4 | **Zdjęcia** | IloscZdjec > 0 | — |
| 5 | **Świadectwo wet.** | VetNo nie pusty | — |

### Ewaluacja kompletności

```
ok = liczba IsChecked
warnings = liczba IsWarning
qcComplete = (ok == total && warnings == 0)
```

- **qcComplete** → zielony, status **CLOSED**, "QC KOMPLETNE (5/5)".
- **ok > 0 niekompletne** → żółty, status **CLOSED_INCOMPLETE**, "QC NIEKOMPLETNE (X/5), Y ostrzeżeń".
- **ok == 0** → czerwony, status **CLOSED_INCOMPLETE**, "QC BRAK (0/5)".

### Ostrzeżenie na stanie

`NaStanieKg > 10` (dokładnie >10, nie >=10) → czerwony banner:
> "Na stanie zostało X kg produktu! Czy na pewno chcesz zamknąć?"

### Logika zamknięcia

`ClosePartiaV2Async(partia, userID, komentarz, qcComplete)`:
```sql
UPDATE listapartii SET IsClose=1, StatusV2=@Status, CloseData, CloseGodzina, CloseOperator
INSERT PartiaStatus (Status=newStatus, StatusPoprzedni='IN_PRODUCTION', Komentarz)
InsertAuditLog "Zamknieta"
```

---

## 8. OtworzPartieDialog

- Otwiera tylko partie z `IsClose == 1` (sprawdzane w WidokPartie przed dialogiem).
- **Powód obowiązkowy** (TextBox, focused) — bez niego MessageBox "Podaj powod".
- `ReopenPartieAsync`: `UPDATE listapartii SET IsClose=0, CloseData=NULL, CloseGodzina=NULL, CloseOperator=NULL`.
- StatusV2 **nie zmienia się** (zostaje CLOSED/itp.) — tylko IsClose flag.
- AuditLog: "PonownieOtwarta" + "Powod: ...".

---

## 9. 10 stanów lifecycle

```
PLANNED → IN_TRANSIT → AT_RAMP → VET_CHECK → APPROVED →
IN_PRODUCTION → PROD_DONE → CLOSED
                                  ↘ CLOSED_INCOMPLETE
                                  ↘ REJECTED
```

| Stan | Znaczenie | Auto-detect |
|---|---|---|
| **PLANNED** | Utworzona z harmonogramu/ręcznie | brak danych skupu |
| **IN_TRANSIT** | Kierowca wyjechał | (rzadko używane) |
| **AT_RAMP** | Auto na rampie | NettoSkup > 0 |
| **VET_CHECK** | Kontrola wet. | (ręcznie) |
| **APPROVED** | Wet zatwierdził | VetNo nie pusty |
| **IN_PRODUCTION** | Rozbiór, ważenia | WydanoKg > 0 |
| **PROD_DONE** | Rozbiór skończony | (ręcznie) |
| **CLOSED** | Zamknięta poprawnie (QC OK) | IsClose=1 && MaTemperatury && MaWady |
| **CLOSED_INCOMPLETE** | Zamknięta z brakami | IsClose=1 && NOT (temp+wady) |
| **REJECTED** | Odrzucona | (ręcznie) |

### DetectAutoStatus — dokładny algorytm

```
JEŚLI IsClose == 1:
    → (MaTemperatury && MaWady) ? CLOSED : CLOSED_INCOMPLETE
JEŚLI WydanoKg > 0 → IN_PRODUCTION
JEŚLI VetNo nie pusty → APPROVED
JEŚLI NettoSkup > 0 → AT_RAMP
INACZEJ → PLANNED
```

### Auto-przejście (RunAutoStatusDetectionAsync)

Dla każdej aktywnej partii: jeśli `detected != current && (int)detected > (int)current && (int)detected <= IN_PRODUCTION` → batch UPDATE. **Status zmienia się sam** gdy pojawią się dane (skup, vet, ważenia).

### Ręczne "Zmień status"

Przycisk pojawia się tylko gdy `GetNextStatus(current) != null`. Kolejność PLANNED→...→PROD_DONE. **Po PROD_DONE** trzeba kliknąć "Zamknij" (nie zwykłe "Zmień status").

---

## 10. Ukryte funkcje (15)

1. **PartiaAuditLog** — każda operacja (Otwarta/Zamknieta/PonownieOtwarta) logowana, fire-and-forget.
2. **HarmonogramLp linkage** — partia linkowana do pozycji harmonogramu, MaPartie zakazuje duplikatów.
3. **Auto Status Detection** co 30s.
4. **Sparkline cumulative** — godzinowa produkcja przeliczona na narastającą.
5. **Detail Flyout** — overlay zamiast nowego okna.
6. **Quick Status Advance** — przycisk na karcie bez dialogu.
7. **Status History** — PartiaStatus.CreatedAtUTC pełen audit.
8. **QC Normy configurable** — QC_Normy table, IsInNorm() dynamic.
9. **Lazy detail tabs** — ładowanie na klik + cache.
10. **Excel dynamic** — eksportuje wszystkie partie wg filtra (nie tylko widoczne).
11. **Dwustronna walidacja statusu** — IsClose synchronizowany z StatusV2.
12. **Harmonogram pre-fill green banner** — informacyjne, read-only.
13. **PartiaDostawca separate table** — zmiana dostawcy bez ruszania listapartii.
14. **View-based QC** — vw_QC_Podsum, vw_QC_WadySkale.
15. **Cache clearing** — LoadDataAsync czyści wszystkie 6 cacheów.

### Tabele tworzone automatycznie (EnsureSchemaAsync)

- `StatusV2` kolumna (varchar 30) + `HarmonogramLp` kolumna w listapartii.
- `PartiaStatus` (historia statusów).
- `QC_Normy` (8 domyślnych norm: TempRampa, TempChillera, TempTunel, KlasaB, Przekarmienie, Skrzydla, Nogi, Oparzenia).
- `PartiaAuditLog`.

---

## 11. Typowy dzień Marcina

```
06:00  Otwiera → "Produkcja Dzis". 3 partie planowane, 0 otwartych.
06:15  Wojtek na rampie → wagowy wpisuje NettoSkup → status auto PLANNED → AT_RAMP ✅
06:45  Weterynarz wpisuje VetNo → status auto AT_RAMP → APPROVED ✅
07:00  Rozbiór, pierwsze ważenia → status auto APPROVED → IN_PRODUCTION ✅
W ciągu dnia:
       Co godzinę patrzy kafelki: "Wydajność 76.5%, Temp 4.2°C, Na stanie 1200 kg".
       Klik karty → Flyout → tab Wazenia (sprawdza trend).
14:00  Wojtek skończony → ręcznie IN_PRODUCTION → PROD_DONE.
15:00  Lista Partii → wybiera Wojtka → "Zamknij".
       QC: 4/5 (brak zaświadczenia wet). Zamyka jako CLOSED_INCOMPLETE 🟡.
16:00  Mazur — ta sama procedura.
```

---

## 12. FAQ

**P: Czemu status zmienił się sam?**
O: DetectAutoStatus — wagowy wpisał skup → PLANNED→AT_RAMP automatycznie.

**P: Jak otworzyć ponownie zamkniętą partię?**
O: Lista Partii → wybierz → **Otworz** 🟣 → podaj powód.

**P: Brak przycisku "Zmień status" na karcie?**
O: Nie ma naturalnego następnego stanu. PROD_DONE wymaga "Zamknij".

**P: Co znaczy "Na stanie 200 kg"?**
O: Wydano 5200, przyjęto 5000 → 200 czeka w magazynie. >10 kg przy zamykaniu = ostrzeżenie.

**P: Tab HACCP — co tam?**
O: Traceability — jeśli z partii 5891 zrobiono filety które trafiły do 5920 (inny dział), ten ślad jest tu.

**P: Excel eksportuje co?**
O: Wszystkie partie wg filtra (jeśli na ekranie 50, w filtrze 500 → eksportuje 500).

**P: Sparkline na karcie?**
O: Mikro-wykres narastającej produkcji godzinowej. W dół = problem.

---

## 13. Co dalej

- **Cykl wstawienia** (zanim partia powstała) → `01_Wstawienia_Kurczakow.md`.
- **Lista cykli** → `02_Lista_Wstawien.md`.
- **Kalendarz dostaw** → `05_Kalendarz_Dostaw_Zywca.md`.
- **Reklamacje** (po sprzedaży) → `04_Reklamacje.md`.
- **Audyt jakości BRC** → `BAZA_WIEDZY/AUDYT_BROILER_SIGNALS/`.
