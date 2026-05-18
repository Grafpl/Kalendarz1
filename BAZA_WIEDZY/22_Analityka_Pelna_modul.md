# 22 — Moduł "Analityka Pełna" (AnalitykaPelnaWindow)

> Dokumentacja techniczna modułu Analityka Pełna w ZPSP. Cross-DB (HANDEL + LibraNet), 4 widoki + dialog drill-down, wspólny FiltryPasek.

## 1. Lokalizacja w repo

```
AnalitykaPelna/
├── AnalitykaPelnaWindow.xaml(.cs)        # główne okno (TabControl + FiltryPasek w split-header)
├── Controls/
│   ├── FiltryPasek.xaml(.cs)             # wspólny pasek filtrów (kompakt + rozwijany + presety dropdown)
│   ├── KpiKafel.xaml(.cs)                # mała karta KPI używana w widokach
│   └── LoadingOverlay.xaml(.cs)          # overlay "Ładowanie..."
├── Views/
│   ├── WidokPlan.xaml(.cs)               # tab "Plan" — prognoza 8 tygodni
│   ├── WidokRealizacja.xaml(.cs)         # tab "Realizacja" — In0E LIVE
│   ├── WidokBilans.xaml(.cs)             # tab "Bilans" — produkcja vs sprzedaż
│   └── WidokWydajnosc.xaml(.cs)          # tab "Wydajność" — 6 sub-tabów (Bilans materiałowy, Hodowcy, Trend, Klasy, ...)
├── Windows/
│   └── SzczegolyKlasyDialog.xaml(.cs)    # drill-down dialog (double-click klasy w WidokWydajnosc)
├── Services/
│   ├── AnalitykaConfig.cs                # connection strings + ustawienia stałe
│   ├── AnalitykaSettings.cs              # persystencja preferencji w pliku JSON
│   ├── PrognozaService.cs                # tab Plan
│   ├── BilansService.cs                  # tab Bilans
│   ├── RealizacjaService.cs              # tab Realizacja (In0E LIVE)
│   ├── WydajnoscService.cs               # tab Wydajność (Bilans materiałowy + uzyski + klasy)
│   ├── CsvExporter.cs                    # eksport CSV (reflection-based)
│   ├── Konwertery.cs                     # IValueConverter-y używane w XAML
│   └── SqlSafe.cs                        # safe readers (NULL handling, daty/godziny LibraNet)
└── Models/
    ├── FiltryAnaliz.cs                   # wspólny DTO filtrów + combo items
    ├── PrognozaModels.cs
    ├── BilansModels.cs
    ├── RealizacjaModels.cs               # WazenieRekord, RankingOperatora, RankingPartii, Heatmapa, ...
    ├── WydajnoscModels.cs                # WydajnoscDzien, WydajnoscHodowca, WydajnoscKlasa, ...
    └── UzyskiHodowcyModels.cs            # OkresAgregacji + OkresHelper (klucze tygodni ISO, miesięcy, kwartałów)
```

## 2. Mapa baz danych — co skąd

| Baza | Server | Co służy w module |
|---|---|---|
| **HANDEL** | 192.168.0.112 | Sage Symfonia: HM.MG/MZ/TW/DK/DP (przyjęcia, wydania, sprzedaż), STContractors, ContractorClassification (handlowiec) |
| **LibraNet** | 192.168.0.109 | In0E (ważenia LIVE), PartiaDostawca (partia → hodowca), Article (kartoteka) |
| ~~UNISYSTEM~~ | 192.168.0.23\\SQLEXPRESS | **Nie używane przez Analitykę Pełną** (tylko HR/RCP) |
| ~~ZPSP~~ | 192.168.0.109 | **Nie używane przez Analitykę Pełną** |

Connection strings: `AnalitykaConfig.cs` (defaults) + override z `appsettings.json`.

## 3. Klasy drobiowe — KLUCZOWA SEMANTYKA

`In0E.QntInCont` to **klasa wagowa drobiu**, nie ilość. Mapuje się tak:

| QntInCont | Grupa | Średnia waga sztuki | Liczba szt./paleta |
|---|---|---|---|
| 1–3 | **Test / anomalia** — odrzucamy w SQL | n/a | n/a |
| 4–7 | **🍗 Duży kurczak** | ~14–16 kg/szt | ~36/paleta |
| 8–12 | **🐥 Mały kurczak** | ~5–7 kg/szt | ~36/paleta |
| >12 | Anomalia — odrzucamy | n/a | n/a |

**Standardowy filtr SQL klas drobiowych:**
```sql
WHERE e.QntInCont IS NOT NULL
  AND e.QntInCont BETWEEN 4 AND 12
  AND e.ActWeight > 0
```

## 4. Realny zakres wagi palety (`In0E.ActWeight`)

- **Realny zakres**: **500–600 kg** (36 sztuk × 14–16 kg dla Dużego, lub 36 × 5–7 kg dla Małego)
- **Anomalie poza zakresem** (filtrowane na wykresach):
  - `< 100 kg` — anulacje (negatywne wagi, zwroty), paleta pół-pełna
  - `100–500 kg` — paleta niepełna, błąd ważenia
  - `> 600 kg` — przeważona, błąd
- **`ActWeight < 0`** — to ANULACJE (`RealizacjaService.BudujRankingOperatorow` liczy je jako `LiczbaAnulacji`)

W histogramie `SzczegolyKlasyDialog` filtrujemy do 500–600 kg z dynamicznym binem co 10 kg (oś X dopasowuje się do faktycznych danych — jeśli wszystko w 540–558, pokażemy tylko biny 540 i 550). Anomalie ukryte, ale ich liczba widoczna w tytule osi X: `Waga palety (kg, 540–560) • 23 ważeń poza zakresem (ukryte)`.

## 5. Schemat In0E (LibraNet) — używane kolumny

```sql
-- LibraNet.dbo.In0E — pojedyncze ważenie palety (LIVE z wagi)
ArticleID    varchar      -- ID towaru (Article.ID)
ArticleName  varchar      -- nazwa towaru
TermID       int          -- terminal wagowy
TermType     varchar      -- nazwa terminalu
Weight       decimal      -- norma (kg) — co paleta POWINNA ważyć
ActWeight    decimal      -- waga rzeczywista (kg) — co paleta WAŻY
Tara         decimal      -- waga tary
Data         date         -- data ważenia (CHECK: SQL 2008 R2 — brak TRY_CONVERT)
Godzina      varchar      -- godzina jako string "HH:mm:ss"
OperatorID   varchar      -- ID operatora
Wagowy       varchar      -- nazwa operatora (display)
P1           varchar      -- klucz partii → JOIN z PartiaDostawca.Partia
QntInCont    int          -- KLASA WAGOWA (4-12) — patrz §3
```

**JOIN z PartiaDostawca** (mapowanie partia → hodowca):
```sql
LEFT JOIN dbo.PartiaDostawca pd ON e.P1 = pd.Partia
-- pd.CustomerID, pd.CustomerName — hodowca
```

## 6. Schemat HANDEL (Symfonia) — używane tabele

| Tabela | Co | Klucze |
|---|---|---|
| `HM.MG` | Pozycje magazynu (dokumenty) | `id`, `kod` (numer dokumentu), `seria`, `data`, `anulowany` |
| `HM.MZ` | Linie pozycji magazynu | `super` → `HM.MG.id`, `idtw`, `ilosc`, `data` |
| `HM.TW` | Towary | `id`, `kod`, `nazwa`, `katalog` |
| `HM.DK` | Dokumenty kontrahentów (sprzedaż) | `id`, `kod`, `khid`, `data`, `anulowany` |
| `HM.DP` | Pozycje dokumentu | `super` → `HM.DK.id`, `idtw`, `ilosc`, `cena` |
| `SSCommon.STContractors` | Kontrahenci | `id`, `shortcut` |
| `SSCommon.ContractorClassification` | Klasyfikacje (handlowiec) | `ElementId` → STContractors.id, `CDim_Handlowiec_Val` |

**Kluczowe serie dokumentów Symfonii:**

| Seria | Co znaczy | Łańcuch produkcyjny |
|---|---|---|
| `sPZ` | Przyjęcie zewnętrzne (kupno żywca, paszy) | 1. Żywiec wjeżdża, kat. 65882 |
| `sRWU` | Rozchód wewnętrzny — Ubój | 2. Żywiec do uboju |
| `sPWU` | Przychód wewnętrzny — Ubój | 3. Tuszka A, B, podroby (kat. 65554) |
| `RWP` | Rozchód wewnętrzny — Produkcja | 4. Tuszka B do krojenia |
| `PWP` | Przychód wewnętrzny — Produkcja | 5. Filet, skrzydło, korpus |
| `sWZ` | Wydanie zewnętrzne | 6. Sprzedaż klientowi |
| `FVS` | Faktura sprzedaży | równolegle z sWZ |

**Magazyny (kat.):** 65554 świeże po uboju, 65556 wydania, 65547 paczkowane, 65562 mrożonki, 65883 pasze.

## 7. Konwencje SQL — gotchas

### LibraNet (SQL Server 2008 R2)
- **Brak `TRY_CONVERT`** — używać `CAST` + walidację po stronie .NET (`SqlSafe.ParseDate`).
- **Brak `LAG`/`LEAD`** — okienkowe funkcje są ograniczone.
- Daty jako parameter zawsze jako **string** w formacie `yyyy-MM-dd`:
  ```csharp
  cmd.Parameters.AddWithValue("@DataOd", f.DataOd.ToString("yyyy-MM-dd"));
  ```
- `Godzina` w `In0E` to **varchar** nie time. Filtrowanie godziny:
  ```sql
  TRY_CAST(LEFT(e.Godzina,2) AS INT) >= @GodzOd
  ```
  (TRY_CAST jest, TRY_CONVERT nie — różnica subtelna).

### HANDEL
- `MZ.data` to `datetime` — zawsze `CAST(MZ.data AS DATE)` przy `BETWEEN`.
- `MG.anulowany = 0` zawsze w WHERE (bez tego dostaniemy zanulowane dokumenty).
- Daty bezpośrednio jako `DateTime` parameter:
  ```csharp
  cmd.Parameters.AddWithValue("@DataOd", filtry.DataOd.Date);
  ```

### Cross-DB queries
- Każdy service ma osobne `SqlConnection` per baza (`_connHandel`, `_connLibra`).
- **Nie ma cross-DB JOIN-ów** — łączenie danych odbywa się po stronie .NET (LINQ in-memory).
- `CommandTimeout = 60` (s) jest ustawiony jawnie — bez tego niektóre zagregowane query timeoutowały.

## 8. FiltryPasek — API i tryb

`Controls/FiltryPasek.xaml.cs`

**Tryby zakładki** (ukrywa/pokazuje pola opcjonalne):
```csharp
public enum TrybZakladki { Plan, Realizacja, Bilans, Wydajnosc }
filtryPasek.UstawTryb(TrybZakladki.Wydajnosc);
```

**Eventy:**
- `FiltryZastosowane(EventArgs<FiltryAnaliz>)` — klik "✓ Zastosuj" lub F5
- `LiveKlik` — klik "● LIVE" (toggle auto-refresh co 60s)
- `EksportKlik` — klik "📥 Eksport" (Ctrl+E)
- `ZamknijKlik` — klik "✕ Zamknij" (Esc)

**Layout (po refactorze 2026-05):**
- **Wiersz kompakt** (zawsze widoczny w split-header obok tabów):
  - 📅 Od – 📅 Do
  - **⏱ Presety ▾** (dropdown z 8 presetami: Dziś / Wczoraj / 7 dni / 30 dni / Tydzień / Miesiąc / Poprz. mies. / 8 tyg.)
  - 📦 Towar (ComboBox)
  - ✓ Zastosuj (F5), ✕ Wyczyść
  - **▼ Więcej filtrów** (toggle do panelu zaawansowanego)
  - ● LIVE, 📥 Eksport, ✕ Zamknij
- **Panel zaawansowany** (collapsed by default, rozwija ▼ Więcej):
  - 🐔 Hodowca, 👤 Operator, ⚖ Klasa, 🗓 Tyg. prognozy

**Osadzenie obok tabów:** TabControl ma custom `ControlTemplate` (`TabControlSplitHeaderStyle` w `AnalitykaPelnaWindow.xaml`), gdzie nagłówek jest 2-kolumnowy: TabPanel po lewej, ContentPresenter z `TemplateBinding Tag` po prawej. FiltryPasek jest osadzony jako `TabControl.Tag`.

## 9. Konwencje UI

### Kolory (klasy drobiowe)
| Element | Hex | Użycie |
|---|---|---|
| Duży kurczak primary | `#2563EB` | tło Σ Duży, KPI border, hover wierszy |
| Duży kurczak dark | `#1E40AF` `#1E3A8A` | gradient stops |
| Mały kurczak primary | `#F97316` `#FB923C` | tło Σ Mały, KPI border |
| Mały kurczak dark | `#9A3412` `#7C2D12` | gradient stops |
| Razem (suma) | `#7C3AED` | tło Σ Razem, akcje primary |
| Razem dark | `#5B21B6` `#4C1D95` | gradient stops |

### LiveCharts.Wpf 0.9.7 — gotchas
- **NaN w wartościach** powoduje crash → zawsze `0.0` zamiast NaN, formatuj label z guardem:
  ```csharp
  LabelPoint = p => p.Y > 0 ? p.Y.ToString("N0") + " kg" : ""
  ```
- **`LabelsRotation = 0`** na osi X dla czytelności.
- **Krótkie etykiety** (tygodniowe → `T18\n29.04`, miesięczne → `maj\n2026`) — patrz `SkrocEtykieteOsi`.
- **`LightweightCellEditor` nie obsługuje FontWeight** (DevExpress note).
- `RowSeries.MaxRowHeight` **nie istnieje** (jest tylko `MaxColumnWidth` w `ColumnSeries`).

### Σ paski (panele podsumowań)
- Padding: 4/4/6 (kompaktowe, ~22-28px wysokości)
- Background: `LinearGradientBrush` 3-stop (ciemny → jasny → ciemny)
- Drop shadow na tekście dla głębi
- ProgressBar wyższy: 22-26px, ciemniejsze tło dla kontrastu

### DataGrid wierszy klas
- `RowHeight = 38`, `Cursor=Hand`, hover: `#DBEAFE` (Duży) / `#FED7AA` (Mały)
- `MouseDoubleClick` → otwiera `SzczegolyKlasyDialog`

### Wybór klas (Eksplorator-style)
- Klik na klasę: **wyłącznie ta klasa** (reszta odznaczona)
- Ctrl+klik: toggle pojedynczej w selekcji
- Klik bez Ctrl na inną: znowu **tylko ta nowa**
- 4 skróty grupowe: 🍗 4–7, 🐥 8–12, ∑ 4–12, ↺ Tylko startowa

## 10. SzczegolyKlasyDialog — drill-down

Otwierany przez **double-click** na wiersz klasy w `WidokWydajnosc.dgKlasyDuzy/Maly`.

**Strategia ładowania:**
- Dane ładowane **raz** (wszystkie klasy 4–12 jednym SQL-em)
- Filtr klas działa **klient-side** — toggle natychmiastowy, bez ponownego query
- `% z partii` liczone na **całej puli** (nie zmienia się przy zmianie wyboru klas)

**6 KPI:** Ważeń, Hodowców, Partii, Σ Standard kg, Σ Rzeczywista kg, Δ Dołożono kg/%

**2 wykresy:**
- Histogram wagi palety (zakres 500–600, dynamiczny X, bin 10 kg, label = `liczba (proc%)`)
- Top 10 hodowców (RowSeries, label = `kg (proc%)`)

**Tabela 12 kolumn:** Data, Godz., Hodowca, Partia, Operator, Klasa, Standard kg, Rzeczywista kg, **Δ kg** (zielony+ / czerwony−), **Δ %**, **% z partii**, Tara

## 11. SzczegolyKlasyDialog (drill-down) — flow danych

```
WidokWydajnosc.dgKlasyDuzy/Maly
        ↓ MouseDoubleClick
DgKlasy_MouseDoubleClick(klasa, _ostatnieFiltry)
        ↓ ShowDialog
SzczegolyKlasyDialog(klasa, filtry)
        ↓ Loaded
ZaladujWszystkoAsync()
        ↓ RealizacjaService.LoadWazeniaAsync(f) — KlasaKurczaka=null, klasy 4-12 filter w .NET
        ↓ raw → WazenieZSzczegolami (ProcentZPartii, RoznicaProc)
_wszystkieRaw (List<WazenieZSzczegolami>)
        ↓ Filter (klient-side)
_wazenia (Where klasa ∈ _wybraneKlasy)
        ↓
[KPI] [Histogram] [TopHodowcy] [DataGrid]
```

## 12. Rekomendacje SQL (do wdrożenia)

Patrz `BAZA_WIEDZY/SQL/REKOMENDACJE_INDEKSY_ANALITYKA.sql`:

1. **Indeks `IX_In0E_Data_QntInCont`** — przyspiesza per-klasa queries
2. **Indeks `IX_In0E_Data_ArticleID`** — przyspiesza Realizacja
3. **Indeks `IX_In0E_P1`** — przyspiesza JOIN PartiaDostawca
4. **(opcjonalnie) Tabela `AnalitykaPreferencjeUzytkownika`** — zapis ostatnich filtrów per user

## 13. Migracja z poprzednich okien

Moduł zastąpił **4 stare okna** (deprecation w FAZA 6):
- DashboardAnalityczny → tab Bilans
- PrognozaUboju → tab Plan
- AnalizaWydajnosci → tab Wydajność
- AnalizaPrzychodu → częściowo tab Bilans (z dodatkami)

Stare okna pozostają w repo, ale w menu są oznaczone jako deprecated i prowadzą do AnalitykaPelnaWindow.

---

**Ostatnia aktualizacja:** 2026-05-08 (po refactorach: KPI obok wykresu, FiltryPasek kompakt+rozwijany, presety jako dropdown, SzczegolyKlasyDialog z multi-select klas i histogramem 500–600 kg).
