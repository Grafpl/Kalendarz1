# OKNA PRODUKCYJNE ZPSP — DOKŁADNA ANALIZA + PLAN PRZERÓBKI

> **Cel:** konkretna pomoc Sergiuszowi z oknami programu produkcyjnego ZPSP. Po dokładnej analizie kodu — co poprawić, scalić, usunąć, dodać.
>
> **Stan:** maj 2026, 71 okien w `Menu.cs`, kluczowe okna produkcyjne mają 475-2293 linii xaml.cs (chaos).
>
> **Format:** każde okno ma konkretny plan działania z liczbą godzin pracy.

---

## SPIS TREŚCI

1. [TL;DR — co teraz robić](#1-tldr)
2. [Mapa 11 kluczowych okien — priorytety](#2-mapa)
3. [Szczegółowa analiza per okno](#3-okna)
   - 3.1 [DashboardPrzychoduWindow (2293 linii ⚠️)](#31-dashboardprzychodu)
   - 3.2 [AnalizaTygodniowa (1020 linii)](#32-analizatygodniowa)
   - 3.3 [WidokPartie (772 linii)](#33-widokpartie)
   - 3.4 [ProdukcjaDzisWidok (475 linii)](#34-produkcjadzis)
   - 3.5 [ProdukcjaPanel (4 taby)](#35-produkcjapanel)
   - 3.6 [Reklamacje (6 okien)](#36-reklamacje)
   - 3.7 [MagazynPanel + LiczenieStanu](#37-magazyn)
   - 3.8 [PokazKrojenieMrozenie (kalkulator)](#38-krojenie)
   - 3.9 [Mroźnia (BRAK dedykowanego okna!)](#39-mroznia)
4. [3 największe duplikaty + jak scalić](#4-duplikaty)
5. [3 największe luki + co dodać](#5-luki)
6. [Plan migracji — od 71 okien do 45](#6-migracja)
7. [TYDZIEŃ 1 — co konkretnie zrobić](#7-tydzien-1)
8. [Mockupy 4 najważniejszych nowych okien](#8-mockupy)

---

## 1. TL;DR — co teraz robić

**Top 5 ruchów (od największego efektu):**

1. **🔥 ROZBIJ DashboardPrzychoduWindow.xaml.cs (2293 linii!)** — to bomba czasowa. Każda zmiana = ryzyko. Trzeba podzielić na ViewModel + Helpers + Window. **3-4 dni pracy**.

2. **⚡ SCALIĆ ProdukcjaDzisWidok + WidokPartie** w jeden `ListaPartiiWindow` z 2 tabami (Live / Historia). **5h pracy.**

3. **⚡ SCALIĆ AnalizaTygodniowa + DashboardPrzychodu** w jeden `DashboardAnalityka` z 3 tabami (Bilans / Przychody / Prognozy). **2 dni pracy.**

4. **⚡ DODAĆ "Zgłoś reklamację" w WidokPartie** (klik na partię → otwórz FormReklamacjaWindow z partie pre-selected). **2h pracy.**

5. **🆕 NOWE OKNO: MrozniaDashboard** — temperatury 3 komór + stany + alerty. Dziś brakuje. **6-8h pracy.**

**Czego NIE robić:**
- Nie dotykaj `LiczenieStanuWindow` — działa, MVVM, czysty
- Nie dotykaj `StatystykiReklamacjiWindow`, `UzupelnijReklamacjeWindow` — czyste, działają

---

## 2. MAPA 11 KLUCZOWYCH OKIEN — priorytety

| # | Okno | Pliki | Linii | Stan | Priorytet | Plan |
|---|---|---|---|---|---|---|
| 1 | **DashboardPrzychoduWindow** | `DashboardPrzychodu/Views/` | **2293** | 🔴 KRYTYCZNY | 🔥 NATYCHMIAST | Rozbicie na MVVM |
| 2 | **AnalizaTygodniowaWindow** | `AnalizaTygodniowa/` | 1020 | ⚠️ Średni | 📋 6 mies | Refaktor + scalenie z #1 |
| 3 | **WidokPartie** | `Partie/Views/` | 772 | ⚠️ Średni | ⚡ 3 mies | Scalenie z #4 |
| 4 | **ProdukcjaDzisWidok** | `Partie/Views/` | 475 | ⚠️ Działa | ⚡ 3 mies | Scalenie z #3 |
| 5 | **ProdukcjaPanel** | `WPF/` (osobne 4 taby) | ~ | ⚠️ Działa | 📋 6 mies | Wchłonąć do "Hala LIVE" |
| 6 | **FormPanelReklamacjiWindow** | `Reklamacje/` | ~500 | ⚠️ Działa | ⚡ 3 mies | + alert widget + mobile UI |
| 7 | **FormReklamacjaWindow** | `Reklamacje/` | ~300 | ⚠️ Działa | ⚡ 3 mies | + integracja z Partie |
| 8 | **FormRozpatrzenieWindow** | `Reklamacje/` | ~200 | ⚠️ Działa | ⚡ 3 mies | + email + auto-KA |
| 9 | **MagazynPanel** | `Magazyn/Panel/` | ~200 | ⚠️ Działa | 📋 6 mies | Config refactor |
| 10 | **LiczenieStanuWindow** | `MagazynLiczenie/` | ~100 | ✅ OK | 💤 12+ mies | Scan support tylko |
| 11 | **MrozniaDashboard** | **BRAK!** | 0 | 🆕 NOWE | ⚡ 3 mies | Stworzyć od zera |

**Łącznie linii xaml.cs:** ~5 800 (z czego DashboardPrzychodu = 40%)

---

## 3. SZCZEGÓŁOWA ANALIZA PER OKNO

### 3.1 DashboardPrzychoduWindow (2293 linii ⚠️) {#31-dashboardprzychodu}

**Lokalizacja:** `DashboardPrzychodu/Views/DashboardPrzychoduWindow.xaml(.cs)` + Services

#### Co robi
Główny dashboard przychodów (sprzedaż): KPI, wykresy, top produkty/odbiorcy/handlowcy. Filtry: data, dział, handlowiec, produkt. Auto-refresh.

#### KRYTYCZNE PROBLEMY
- **2293 LINII XAML.CS** — rekord szkoły
- **Brak MVVM** — całość w code-behind
- **Hardcoded SQL'e w Services** (prawdopodobnie)
- **Każda zmiana = ryzyko** błędu
- Pewnie **DUPLIKAT** z AnalizaTygodniowaWindow (oba liczą przychody)

#### Plan przeróbki (kolejność)

**ETAP 1: Podział kodu (3 dni)**
1. Stwórz `DashboardPrzychodu/ViewModels/DashboardPrzychoduViewModel.cs`
2. Przenieś WSZYSTKIE properties (z code-behind) → ViewModel jako INPC
3. Przenieś WSZYSTKIE komendy (button clicks) → `ICommand` w ViewModel
4. Code-behind = max 50 linii (DataContext = new ViewModel())

**ETAP 2: Helpers (1 dzień)**
1. `DashboardPrzychodu/Helpers/PrzychodCalculator.cs` — kalkulacje (KPI, top, marża)
2. `DashboardPrzychodu/Helpers/ChartBuilder.cs` — budowanie wykresów

**ETAP 3: SCALENIE z AnalizaTygodniowa (2 dni)**
- Stwórz `DashboardAnalityka/DashboardAnalitykaWindow.xaml`
- 3 taby: Bilans (z AnalizaTygodniowa) / Przychody (z DashboardPrzychodu) / Prognozy
- Wspólne filtry data + dział + handlowiec na headerze
- Wspólny ViewModel z `INotifyPropertyChanged` na poziomie okna

**Razem czas:** **6 dni roboczych** (1 dev pełnoetatowo)

#### Mockup nowego okna
```
┌──────────────────────────────────────────────────────────────────┐
│ 📊 DASHBOARD ANALITYKA — 02.05.2026                              │
│                                                                   │
│ Filtry: [Data: 01.04 - 02.05 ▼] [Dział: Wszyst ▼] [Handlowiec ▼] │
│                                                                   │
│ ┌─[Bilans Produkcja vs Sprzedaż]──[Przychody i Marża]──[Prognozy]┐│
│ │                                                                ││
│ │ 📊 KPI:                                                       ││
│ │ ┌────────┬────────┬────────┬────────┬────────┐              ││
│ │ │Produkcja│Sprzedaż│% rotacji│Wariancja│MAPE  │              ││
│ │ │ 850 t  │ 770 t  │  90.6%  │  +80 t │ 12.3%│              ││
│ │ └────────┴────────┴────────┴────────┴────────┘              ││
│ │                                                                ││
│ │ 📈 [Wykres: kolumny prod+sprzed + linia prognozy + opc YoY] ││
│ │                                                                ││
│ │ 📋 Bilans dzienny (z anomaliami):                             ││
│ │ Data       Produkcja  Sprzedaż  Wariancja  MAPE  Anomalia    ││
│ │ 02.05.2026 156 t      142 t     +14 t      8%    -           ││
│ │ 01.05.2026 148 t      156 t     -8 t       3%    -           ││
│ │ 30.04.2026 180 t      120 t     +60 t      45%   ⚡           ││
│ │ ...                                                            ││
│ │                                                                ││
│ │ 🏆 Top-N: [Odbiorcy] [Handlowcy] [Produkty]                  ││
│ │                                                                ││
│ │ 🌡️ Heatmapa (towar × dzień):                                 ││
│ │ [grid HeatSeries z LiveCharts]                                ││
│ └────────────────────────────────────────────────────────────────┘│
└──────────────────────────────────────────────────────────────────┘
```

---

### 3.2 AnalizaTygodniowaWindow (1020 linii) {#32-analizatygodniowa}

**Lokalizacja:** `AnalizaTygodniowa/AnalizaTygodniowaWindow.xaml(.cs)`

#### Co robi
Bilans produkcji vs sprzedaży. KPI (5 kart), wykresy, heatmapa, Top-N, anomalie statystyczne 2σ, MAPE, YoY comparison.

#### Problemy
- 1020 linii xaml.cs (sporo, ale nie 2293)
- Częściowo MVVM (PropertyChanged)
- **Hardcoded CONN string** na klasie
- **Anomalia detection mieszane typy** (decimal sigma + Math.Sqrt = double)
- **Heatmap** może się zacinać przy 365 dni × 50 produktów
- **Auto-refresh nie wyłącza się po 18:00** — query'wanie pustej bazy całą noc
- **Prognoza skąd?** — nie wiadomo czy z bazy czy hardcode

#### Plan przeróbki

**ETAP 1: Refaktor (4-5h)**
1. Wynieś hardcoded CONN do `App.config`
2. `WyznaczAnomalie()` → `AnalitykaBilansuHelper.cs`
3. Anomalia formula — sprawdź MAPE wzór

**ETAP 2: SCALENIE (z DashboardPrzychodu — patrz 3.1)**
- Tab "Bilans" w nowym `DashboardAnalitykaWindow`

**ETAP 3: UI fixy (3h)**
1. Heatmapa toggle "Show all / Top-15"
2. Auto-refresh disable po 18:00 (jeśli ostatnio interakcja >2h temu)
3. Drilldown na Top-N (klik na handlowca → filtr w głównej tabeli)

**Razem:** **2 dni roboczych**

---

### 3.3 WidokPartie.xaml (772 linii) {#33-widokpartie}

**Lokalizacja:** `Partie/Views/WidokPartie.xaml(.cs)`

#### Co robi
Grid z historią partii (DevExpress GridControl + 6 detail tabów: Wazenia, Produkty, QC, Skup, HACCP, Timeline). Filtry: data, dział, status. Lazy-load detail tabów.

#### Problemy
- 772 linii xaml.cs
- **6 cachów słownikowych** (zaproszenie do bug'ów)
- **Detail tabów ładuje się 1-2s** (brak indeksów w bazie?)
- **Anna nie widzi partii bez filtrów** — domyślne 7 dni
- **QC tab renderuje ręcznie w code-behind** — zacina się przy 100 pomiarów
- **Brak filtra "Tylko otwarte partie"**
- **Brak preset-buttona "Dzisiaj"**
- **Brak eksportu do PDF** (są tylko CSV)
- **Brak alertu na zmianę statusu**
- **Auto-refresh duplikat** logiki z ProdukcjaDzisWidok

#### Plan przeróbki

**ETAP 1: Quick wins (1 dzień)**
1. Preset button **"Dzisiaj"** w toolbar (1h)
2. Filter checkbox **"Tylko otwarte partie"** (30 min)
3. Timeline tab **`ORDER BY CreatedAtUTC DESC`** (15 min)
4. Cache invalidation na timestamp 5 min (1h)

**ETAP 2: SCALENIE z ProdukcjaDzisWidok (5h)** — patrz 3.4

**ETAP 3: PDF export (4h)**
- Biblioteka: PdfSharp lub QuestPDF
- Zawartość: nagłówek partii + QC tabela + Timeline + QR-kod (ZXing.Net)

**ETAP 4: QC tab refactor (3h)**
- Z ręcznej budowy Grid'a → ItemsControl + DataTemplate
- Lazy-load wciąż OK

**Razem:** **3 dni roboczych**

---

### 3.4 ProdukcjaDzisWidok.xaml (475 linii) {#34-produkcjadzis}

**Lokalizacja:** `Partie/Views/ProdukcjaDzisWidok.xaml(.cs)` (UserControl)

#### Co robi
Live dashboard partii dzisiaj — karty (340px) z aktywnymi/zamkniętymi partiami. KPI (6 kart): partii, otwartych, kg, wydajność, temp rampa, harmonogram. Right panel: harmonogram dostaw + alerty.

#### Problemy
- **475 linii xaml.cs** — duże, ale OK
- **Brak quick-close button** na karcie (musi przejść przez flyout)
- **Harmonogram nie sortuje się chronologicznie** (FIFO!)
- **Alerty zbyt ogólne** (brak alertu temp rampa > 5°C)
- **Auto-refresh 30s** — za szybko, powinno być 60s
- **Hardcoded kolory** — `#1E3A5F`, `#27AE60` zamiast ResourceDictionary
- **Brak integracji z WAGO/RADWAG** (klasy A/B real-time)

#### Plan przeróbki

**ETAP 1: Quick wins (3h)**
1. Auto-refresh interval 60s zamiast 30s (5 min)
2. Harmonogram sort `OrderBy(h => h.CzasDostawy)` (15 min)
3. Quick-close button na karcie → otwórz `ZamknijPartieDialog` (1h)
4. Alert kontekstowe (red >5°C, orange >20% klB, yellow <70% wydajność) (1h)

**ETAP 2: SCALENIE z WidokPartie (5h)**
- Stwórz nowy `ListaPartiiWindow.xaml` (jeśli nie istnieje już) z `<TabControl>`
- Tab 1: `<local:ProdukcjaDzisWidok/>` (live cards)
- Tab 2: `<local:WidokPartie/>` (detail grid)
- Wspólne filtry data + dział na headerze

**ETAP 3: Integracja WAGO (po API od dostawcy)**
- Kolumna "Klasa A/B" na karcie partii (live %)
- Trend w sparkline (godzinowy)

**Razem:** **1 dzień roboczy** (bez WAGO API, +6h gdy WAGO gotowe)

#### Mockup nowego ListaPartiiWindow
```
┌──────────────────────────────────────────────────────────────┐
│ 🐔 LISTA PARTII UBOJOWYCH — 02.05.2026                       │
│                                                               │
│ Filtry: [Data: dzisiaj ▼] [Dział: ▼] [Status: ▼] [☑ Otwarte]│
│                                                               │
│ ┌─[Live (Dzisiaj)]──[Historia + Detail]────────────────────┐ │
│ │                                                            │ │
│ │ 📊 KPI:                                                   │ │
│ │ ┌────┬────┬────┬────┬────┬────┐                         │ │
│ │ │5   │3   │142t│85% │2.3°│18  │                         │ │
│ │ │partii│otw│kg │wyd │rampa│harm│                         │ │
│ │ └────┴────┴────┴────┴────┴────┘                         │ │
│ │                                                            │ │
│ │ AKTYWNE PARTIE (klik = szczegóły, X = zamknij):          │ │
│ │ ┌───────────────┐ ┌───────────────┐                     │ │
│ │ │ 26119001  [X] │ │ 26119002  [X] │                     │ │
│ │ │ Stróżewski    │ │ Przybysz B.   │                     │ │
│ │ │ 9 180 kg ✅A  │ │ 5 940 kg ✅A  │                     │ │
│ │ │ 17% B  ⚠️    │ │ 12% B  ✅    │                     │ │
│ │ │ Sparkline     │ │ Sparkline     │                     │ │
│ │ └───────────────┘ └───────────────┘                     │ │
│ │                                                            │ │
│ │ HARMONOGRAM DOSTAW (FIFO):                                │ │
│ │ 03:00 Stróżewski 2t  ✅                                   │ │
│ │ 03:30 Przybysz B 5t  ✅                                   │ │
│ │ 04:00 Łukawska 1.5t ⏳                                    │ │
│ │                                                            │ │
│ │ ⚠️ ALERTY (3):                                            │ │
│ │ 🔴 Temp rampa > 5°C                                       │ │
│ │ 🟠 Klasa B 23% (powyżej 20%)                              │ │
│ │ 🟡 Wydajność spadła do 68%                                │ │
│ └────────────────────────────────────────────────────────────┘ │
└──────────────────────────────────────────────────────────────┘
```

---

### 3.5 ProdukcjaPanel — 4 taby (osobne okno) {#35-produkcjapanel}

**Lokalizacja:** `WPF/` (folder do potwierdzenia)

#### Co robi
4 taby: Zamówienia / Plan dnia / Statystyki / Historia realizacji.

#### Problemy
- **Brak LIVE % klasy A vs B** (z alarmem <75%)
- **Brak log wstrzymań linii**
- **Brak synchronizacji z In0E** (live kg/h per terminal)
- **Tylko historyczne dane** (2h opóźnienie)

#### Plan przeróbki — WCHŁONĄĆ do "Hala LIVE"

**Plan z poprzednich audytów:** **scalić ProdukcjaPanel + DashboardPrzychodu w 1 okno "Hala LIVE"** z tabami:
- Tab 1: Tempo (kg/h per terminal/operator) — z `In0E`
- Tab 2: Zmiany A vs B (5:00-13:30 vs 14:00-21:00)
- Tab 3: Klasa A vs B (LIVE %, alarm <75%) — Z DashboardPrzychodu
- Tab 4: Uzyski (sPWU vs RWP, magazyn 65554)
- Tab 5: Przestoje (NEW — log wstrzymań)
- Tab 6: Plan dnia (z ProdukcjaPanel TAB 2)

**Czas:** **2-3 tygodnie** (dla pełnego "Hala LIVE")

**Krótkoterminowo (2 tygodnie):**
- Dodać alert <75% Klasa A w DashboardPrzychodu (5 min!)
- Dodać przycisk "Plan dnia" w DashboardPrzychodu (otwiera ProdukcjaPanel TAB 2 w nowym oknie)

---

### 3.6 Reklamacje — 6 okien {#36-reklamacje}

**Lokalizacja:** `Reklamacje/`

#### Stan: 6 okien
1. `FormPanelReklamacjiWindow` — main list + status workflow (~500 linii)
2. `FormReklamacjaWindow` — create/edit (~300 linii)
3. `FormRozpatrzenieWindow` — resolve workflow (~200 linii)
4. `FormSzczegolyReklamacjiWindow` — drilldown
5. `StatystykiReklamacjiWindow` — analityka ✅ OK
6. `UzupelnijReklamacjeWindow` — batch completion ✅ OK

#### KLUCZOWY PROBLEM (z audytu wcześniejszego)
- **75% reklamacji = AUTO-IMPORT z Symfonii** (`SyncFakturyKorygujace()` co 5 min)
- Status: "Nowa", StatusV2: "ZGŁOSZONA", TypReklamacji: "Faktura korygująca"
- **Brak `PrzyczynaGlowna`** — automaty bez opisu
- Nikt ich nie zamyka → zawyżają statystyki

#### Plan przeróbki

**ETAP 1: Filtr auto-import (2h) — KRYTYCZNE!**
W `FormPanelReklamacjiWindow.WczytajReklamacje()`:
```sql
WHERE 1=1
  AND NOT (TypReklamacji='Faktura korygujaca' AND ZrodloZgloszenia='Symfonia')
```
**+ osobna zakładka "Korekty Symfonii"** (te 75% pseudo-reklamacji)

**ETAP 2: Alert widget (1h)**
- W główniej oknie ZPSP (TopBar): licznik **"3 reklamacje pilne"**
- Klik → otwórz FormPanelReklamacjiWindow z filtrem "Pilne"

**ETAP 3: "Zgłoś reklamację" w WidokPartie (2h)**
- Nowy button na grid'zie partii
- Klik → otwórz `FormReklamacjaWindow` z partie pre-selected

**ETAP 4: Email notyfikacje (2h)**
- Sprawdź czy `ReklamacjeEmailService.cs` istnieje (z audytu — TAK)
- Test: na zmianę statusu → email do handlowca

**ETAP 5: Mobile UI dla Pani Joli (4-6h)**
- Tablet-friendly formularz (Web?, lub WPF z Touch styles)
- Pole: klient + faktura + opis (RichText) + zdjęcie (kamera)

**ETAP 6: PDF generowanie KA (2h)**
- Sprawdź czy `ReklamacjePDFGenerator.cs` istnieje
- Test: wygeneruj korektę aresz w PDF

**ETAP 7: FAQ admin panel (2h)**
- Tabela `Reklamacje_PowodyFAQ` z szablonami
- Admin może edytować

**Razem:** **~15-20h pracy** (+ mobile UI 4-6h).

---

### 3.7 MagazynPanel + LiczenieStanu {#37-magazyn}

**Lokalizacja:** `Magazyn/Panel/MagazynPanel.xaml(.cs)` + `MagazynLiczenie/Formularze/LiczenieStanuWindow.xaml(.cs)`

#### MagazynPanel
**Stan:** ⚠️ działa, średni stan kodu.

**Problemy:**
- **Trzy connection strings hardcoded** (zamiast appsettings.json)
- **Dictionary ikonek produktów w memory** — eager load (zła praktyka)

**Plan:**
1. Config strings → `appsettings.json` (1h)
2. Lazy-load ikonek (1.5h)
3. PDF export historii wydań (2h)

**Razem:** **5h**

#### LiczenieStanuWindow
**Stan:** ✅ DZIAŁA DOBRZE — MVVM, czysty kod, touch-friendly keypad.

**Plan (opcjonalnie):**
1. Scan support (barcode) (2h)
2. Validation "12 z 150 nie liczono" (30 min)
3. Batch "Set all to 0" (1h)

**Razem (opcjonalne):** **3.5h**

---

### 3.8 PokazKrojenieMrozenie (kalkulator) {#38-krojenie}

**Lokalizacja:** `WPF/PokazKrojenieMrozenie.cs` (WinForms!)

#### Co robi
**KALKULATOR DECYZJI 13:00** — krojenie / mrożenie / sprzedaż tuszki. **3 scenariusze** liczone na podstawie cen rynkowych. **HARDKODOWANE WSPÓŁCZYNNIKI UZYSKU** (29.5% Filet, 33.4% Ćwiartka, 22.7% Korpus).

#### Status
**✅ DZIAŁA** — to jest **gem** ZPSP, używany codziennie 13:00.

#### Plan ulepszeń

**ETAP 1: Wynieś współczynniki do bazy (2h)**
- Tabela `WspolczynnikiUzysku(IdProduktu, ProcUzysku, OdDaty)`
- Edytowalne w admin panelu (bo sezonowo się zmieniają)

**ETAP 2: Auto-aktualizacja cen elementów (3h)**
- Z modułu sprzedaży (HM.DP średnia z ostatnich 7 dni)
- Zamiast ręcznego wpisywania w textboxach

**ETAP 3: Symulacje what-if (4h)**
- Slider "Cena tuszki -X%" → re-calc
- Slider "Cena fileta +X%" → re-calc
- Pomocne dla decyzji "co jeśli rynek spadnie"

**ETAP 4: Historia decyzji (2h)**
- Każda decyzja z 13:00 zapisana w bazie
- Po miesiącu: porównanie "co wybraliśmy" vs "co byłoby najlepsze"

**ETAP 5: Migracja na WPF (2 dni — opcjonalna)**
- Z WinForms na WPF (jak reszta ZPSP)
- Tylko jeśli czas pozwala

**Razem (bez migracji WPF):** **11h pracy**

---

### 3.9 MROŹNIA — BRAK dedykowanego okna! {#39-mroznia}

#### Status: 🆕 OKNO BRAKUJE

Mroźnia ma:
- 3 komory + szokówka (-30/-40°C) + chłodnia
- ~280 ton mrożonego towaru
- Inwentaryzacja tygodniowa (papier!)
- Decyzja 13:00 (PokazKrojenieMrozenie kalkuluje, ale **dane mroźni nigdzie nie są LIVE pokazane**)

#### Plan: stwórz `MrozniaDashboard.xaml` (8h)

**Ekran:**
```
┌──────────────────────────────────────────────────────┐
│ ❄️ MROŹNIA DASHBOARD — 02.05.2026                   │
│                                                       │
│ TEMPERATURY (auto z czujników IoT lub manual):       │
│ ┌────────────┬────────────┬────────────┬───────────┐│
│ │Komora 1    │Komora 2    │Komora 3    │Szokówka   ││
│ │-19.3°C ✅  │-19.1°C ✅ │-18.7°C ⚠️ │-32.4°C ✅ ││
│ │(cel -18-20)│            │            │            ││
│ └────────────┴────────────┴────────────┴───────────┘│
│                                                       │
│ STAN PARTII per komora:                              │
│ ┌─────────────────────────────────────────────────┐ │
│ │ KOMORA 1 (świeże mrożenie, 0-30 dni):           │ │
│ │   78 partii, 32 t                               │ │
│ │ KOMORA 2 (30-90 dni):                            │ │
│ │   142 partii, 58 t                              │ │
│ │ KOMORA 3 (90-180 dni, EKSPORT):                 │ │
│ │   89 partii, 42 t                               │ │
│ │ ⚠️ STARE (>180 dni):                            │ │
│ │   23 partii, 11 t — DECYZJA Sergiusz            │ │
│ └─────────────────────────────────────────────────┘ │
│                                                       │
│ DZIŚ:                                                │
│   📥 Do mrożenia (z 13:00): 800 kg Filet, 200 kg... │
│   📤 Do wydania: 1 200 kg Filet (eksport Ania)      │
│                                                       │
│ ⚠️ ALERTY:                                          │
│   • Partia 25034 leży 270 dni — SPRAWDŹ            │
│   • Komora 3 temp blisko granicy                    │
│                                                       │
│ INWENTARYZACJA:                                      │
│   Ostatnia: 28.04.2026                              │
│   Następna: 05.05.2026                              │
│   [Rozpocznij inwentaryzację cyfrową]               │
└──────────────────────────────────────────────────────┘
```

**Funkcjonalność:**
- Auto-refresh co 60s (temperatury z czujników)
- Drilldown: klik komora → lista partii w niej
- Inwentaryzacja cyfrowa (tablet w mroźni)
- Alert wieku: >180 dni czerwone, >270 dni alarm

**Tabele:**
```sql
CREATE TABLE TemperaturyMroznia (
    Id INT IDENTITY PRIMARY KEY,
    DataCzas DATETIME2 NOT NULL,
    Komora INT,           -- 1, 2, 3, szokówka
    Temperatura DECIMAL(5,2),
    INDEX IX_Komora (Komora, DataCzas DESC)
);

CREATE TABLE InwentaryzacjaMroznia (
    Id INT IDENTITY PRIMARY KEY,
    DataAudytu DATE,
    Komora INT,
    PartiaID INT,
    StatusFizyczny NVARCHAR(20),  -- 'jest', 'brak', 'inny stan'
    OsobaAudytujaca NVARCHAR(100),
    Notatki NVARCHAR(500)
);
```

**Czas:** **8h** (jeśli czujniki IoT już są — sprawdź; bez = manual wpisywanie temp).

---

## 4. 3 NAJWIĘKSZE DUPLIKATY {#4-duplikaty}

### Duplikat 1: ProdukcjaDzisWidok + WidokPartie
**Oba pokazują partie**, jedno "live dzisiaj", drugie "historia za X dni".

**Akcja:** SCALIĆ w `ListaPartiiWindow.xaml` z 2 tabami (Live / Historia).
**Czas:** 5h
**Korzyść:** Anna nie skacze między oknami; wspólne filtry.

### Duplikat 2: AnalizaTygodniowa + DashboardPrzychodu
**Oba liczą produkcję/sprzedaż, KPI, wykresy**.

**Akcja:** SCALIĆ w `DashboardAnalitykaWindow.xaml` z 3 tabami (Bilans / Przychody / Prognozy).
**Czas:** 2 dni
**Korzyść:** Sergiusz/Justyna jeden punkt zamiast 2; mniej duplikatu kodu.

### Duplikat 3: FormReklamacja vs WidokPartie (implicit)
**Reklamacje są związane z Partie**, ale można zgłosić tylko z FormReklamacja.

**Akcja:** Dodać button "Zgłoś reklamację" w `WidokPartie` (→ FormReklamacjaWindow z partie pre-selected).
**Czas:** 2h
**Korzyść:** Justyna zgłasza w 1 klik zamiast 5.

---

## 5. 3 NAJWIĘKSZE LUKI {#5-luki}

### Luka 1: LIVE INTEGRACJA WAGO + RADWAG
**Brakuje real-time danych** z klasyfikacji A/B (WAGO) i wag (RADWAG). Dziś dane są **2h stare**.

**Akcja:**
1. Sergiusz kontaktuje dostawców o API (Tydzień 1)
2. Po API → 2 service'y w ZPSP (`WagoIntegrationService`, `RadwagIntegrationService`)
3. Pull co 60s, zapis do `WagoEvent` + `RadwagEvent`

**Korzyść:** % klasy A/B per hodowca real-time + wydajność per pracownik.

### Luka 2: MOBILE UI DLA PANI JOLI
**Pani Jola wpisuje reklamacje i zamówienia z papieru** (karteczki!) — 60% wolumenu firmy zagrożone bo nie wpisuje do ZPSP.

**Akcja:**
1. Tablet 10" + uproszczony web/WPF UI
2. 3 życzenia Joli:
   - Prostszy sposób wprowadzania danych
   - Łatwy dostęp do informacji
   - Lepsza organizacja (mniej przytłaczające)
3. Touch-friendly + voice-to-text (skoro Sergiusz lubi)

**Korzyść:** koniec karteczek, audit trail, sukcesja do młodszych handlowców.

### Luka 3: MROŹNIA DASHBOARD
Brakuje **dedykowanego okna** monitorującego temperatury, stany, alerty mroźni.

**Akcja:** stwórz `MrozniaDashboard.xaml` (sekcja 3.9).
**Korzyść:** Justyna widzi mroźnię w jednym miejscu; alarm gdy temp blisko granicy; FIFO pilnowane.

---

## 6. PLAN MIGRACJI — od 71 okien do ~45 {#6-migracja}

### Stan obecny: 71 okien w `Menu.cs`

### Po konsolidacji: ~45 okien (-37%)

**Konsolidacje:**

| Stare (znika) | Nowe (zostaje) | Zysk |
|---|---|---|
| ProdukcjaDzisWidok + WidokPartie | `ListaPartiiWindow` (2 taby) | -1 okno |
| AnalizaTygodniowa + DashboardPrzychodu | `DashboardAnalitykaWindow` (3 taby) | -1 okno |
| ProdukcjaPanel + AnalizaPrzychodu + AnalizaWydajnosci | `HalaLiveWindow` (6 tabów) | -2 okna |
| Komunikator + Centrum Spotkań (już wstrzymane) | usunięte | -2 okna |
| Stare WinForms: WidokAvilog, PokazCeneTuszki, AnkietaPotwierdzoneForm | usunięte | -3 okna |

**Nowe okna:**
| Nowe | Cel |
|---|---|
| `MrozniaDashboard` | Temperatury + stany + alerty mroźni |
| `CockpitWlasciciela` (rozbudowa PulpitZarzadu) | Sergiusz codziennie rano |
| `HalaLiveWindow` | Justyna + Łukasz LIVE monitoring |
| `MobileUI_Reklamacje` | Pani Jola tablet |

**Razem:** -9 okien starych, +4 okien nowych = **netto -5 okien (66 zamiast 71)**.

Po dalszej rewolucji (12 mies): celujemy ~45 okien.

---

## 7. TYDZIEŃ 1 — co konkretnie zrobić {#7-tydzien-1}

**Założenie:** Sergiusz ma 5h dziennie na ZPSP × 5 dni = **25h na tydzień**.

### Poniedziałek (5h)
- ✅ Filtr auto-import w Reklamacjach (2h) — sygnał spada z 247 → 60 reklamacji
- ✅ Alert widget reklamacji w głównym oknie (1h)
- ✅ Quick-close button na karcie ProdukcjaDzisWidok (1h)
- ✅ Harmonogram FIFO sort w ProdukcjaDzisWidok (15 min)
- ✅ Auto-refresh 60s zamiast 30s w ProdukcjaDzisWidok (5 min)
- 📩 Wysłać maile do **WAGO + RADWAG** o API (40 min)

### Wtorek (5h)
- ✅ Backup ZPSP codzienny (skrypt PowerShell + Azure/OVH) (3h)
- ✅ "Zgłoś reklamację" button w WidokPartie (2h)

### Środa (5h)
- ✅ Preset "Dzisiaj" + filter "Otwarte partie" w WidokPartie (1.5h)
- ✅ Cache invalidation 5 min w WidokPartie (1h)
- ✅ Timeline tab `ORDER BY ... DESC` (15 min)
- ✅ Alert <75% Klasa A w DashboardPrzychodu (5 min)
- ✅ Stałe parametry kosztów krojenia/mrożenia → tabela bazy (2h)

### Czwartek (5h)
- 🔥 **Refaktor DashboardPrzychoduWindow ETAP 1**: stwórz ViewModel, przenoś properties (5h pracy = 1/3 zadania)

### Piątek (5h)
- 🔥 **Refaktor DashboardPrzychoduWindow ETAP 1 c.d.**: przenoś commands (5h = 2/3)

**Sumarycznie po tygodniu:**
- Reklamacje uporządkowane (filtr + alert + integracja Partie)
- Backup ZPSP zabezpieczony
- ProdukcjaDzisWidok ulepszone (4 quick-wins)
- WidokPartie ulepszone (2 quick-wins)
- DashboardPrzychodu w 2/3 zrobione (ETAP 1 MVVM)
- Email do dostawców WAGO+RADWAG wysłany

**Następny tydzień** (jeśli chcemy):
- Dokończenie DashboardPrzychodu ETAP 1
- ETAP 2 (Helpers) i ETAP 3 (scalenie z AnalizaTygodniowa)

---

## 8. MOCKUPY 4 NAJWAŻNIEJSZYCH NOWYCH OKIEN {#8-mockupy}

### Mockup 1: ListaPartiiWindow (po scaleniu)
*(Patrz sekcja 3.4)*

### Mockup 2: DashboardAnalitykaWindow (po scaleniu)
*(Patrz sekcja 3.1)*

### Mockup 3: MrozniaDashboard (nowe)
*(Patrz sekcja 3.9)*

### Mockup 4: HalaLiveWindow (super-konsolidacja)

```
┌──────────────────────────────────────────────────────────────────┐
│ 🏭 HALA LIVE — 02.05.2026 11:32 LIVE                              │
│                                                                   │
│ ┌─[Tempo]─[Zmiany]─[Klasa A/B]─[Uzyski]─[Przestoje]─[Plan dnia]─┐│
│ │                                                                ││
│ │ TEMPO LINII (z In0E + WAGO):                                  ││
│ │ ┌──────────────────┬──────────────────┬─────────────────────┐ ││
│ │ │ Sztuk od startu  │ Tempo bieżące    │ Cel na zmianę       │ ││
│ │ │ 41 230 / 70 000  │ 7 240 szt/h      │ 70 000 szt          │ ││
│ │ │ 58.9%            │ ✅ powyżej celu  │ Zmiana A: 13:30     │ ││
│ │ └──────────────────┴──────────────────┴─────────────────────┘ ││
│ │                                                                ││
│ │ Per terminal (z In0E):                                        ││
│ │   T01 Stróżewski: 1 850 szt/h  ✅                            ││
│ │   T02 Łukawska:   1 720 szt/h  ✅                            ││
│ │   T03 Przybysz:   1 950 szt/h  ✅                            ││
│ │   T04 Kępa:       1 720 szt/h  ✅                            ││
│ │                                                                ││
│ │ Mini-wykres ostatnich 60 min: [chart sparkline]               ││
│ │                                                                ││
│ │ ⚠️ ALERTY:                                                    ││
│ │   • Linia stała 5 min (10:42-10:47) — awaria nożna           ││
│ │   • Klasa B osiągnęła 18% (cel max 20%)                       ││
│ │                                                                ││
│ │ KLASA A vs B (LIVE):                                          ││
│ │   A: 82.1% ✅ (cel 80%)                                       ││
│ │   B: 17.9% ⚠️ (cel <20%)                                     ││
│ │   Per hodowca:                                                ││
│ │     Stróżewski: 78% A  (-2% od celu)                          ││
│ │     Przybysz B: 88% A  ✅                                     ││
│ │     ...                                                        ││
│ └────────────────────────────────────────────────────────────────┘│
└──────────────────────────────────────────────────────────────────┘
```

---

# 🎯 PRZESŁANIE FINALNE

**Sergiuszu, oto co masz:**

1. **3 dokumenty operacyjne** w `Dokumenty ogólnikowe/`:
   - `FIRMA_PEŁEN_OBRAZ.md` (~80 KB) — kompletny obraz firmy
   - `PRODUKCJA_MAGAZYN_JAKOSC_PROGRAM.md` (~50 KB) — plan programistyczny 3 działów
   - **`OKNA_PRODUKCYJNE_PLAN.md`** (TEN) — dokładna analiza okien + plan przeróbki

2. **Plan na 1 tydzień** (sekcja 7) — 25h pracy, konkretne zadania

3. **Plan migracji** 71 → 45 okien (sekcja 6)

4. **Mockupy 4 nowych okien** (sekcja 8)

**Daj sygnał:**
- "Zaczynamy poniedziałek od X" — i kodujemy
- "Najpierw chcę pogłębić Y" — i analizujemy dalej
- "Pomóż mi z Z" — i robię konkret

**Jestem gotów programować.** Każdy z 5 priorytetów z TL;DR ma konkretny plan godzinowy. Wybierz który zaczynamy. 🚀
