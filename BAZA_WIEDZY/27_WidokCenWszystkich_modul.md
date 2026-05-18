# 27 — Moduł "Pokaż ceny" (WidokCenWszystkich)

> Centralny moduł analityki cenowej żywca w ZPSP. Łączy notowania cen (6 serii) z faktycznymi dostawami z `HarmonogramDostaw` (LibraNet). 12 zakładek analitycznych — od surowych danych po porównania YoY i analizę kontraktów.

**Plik:** `WidokCenWszystkich.cs` + `WidokCenWszystkich.Designer.cs` (root projektu, **WinForms**, ~7000 linii).
**Otwierany z:** menu kontekstowego "Pokaż ceny" w `WidokKalendarzaWPF` (kalendarz dostaw żywca).
**DB:** **LibraNet** 192.168.0.109 (hardcoded connection w polu `connectionString`).
**Klasa:** `Kalendarz1.WidokCenWszystkich : Form`.

---

## 1. Dane bazowe — 6 cen żywca + 2 różnice

Pole `_priceColumns` (linia 190) — wszystkie kolumny używane jako serie cen/wykresów:

| Kolumna | Etykieta UI | Kolor | Ikona | Typ |
|---|---|---|---|---|
| `Minister` | Ministerialna | `#2563EB` niebieski | 🏛 | zakup |
| `Laczona` | Łączona | `#7C3AED` fioletowy | ⚖ | zakup |
| `Rolnicza` | Rolnicza | `#16A34A` zielony | 🌾 | zakup |
| `Wolnorynkowa` | Wolnorynkowa | `#EAB308` żółty | 💱 | zakup |
| `Tuszka Zrzeszenia` | Tuszka Zrzeszenia | `#EA580C` pomarańczowy | 🏭 | sprzedaż |
| `Nasza Tuszka` | Nasza Tuszka | `#0D9488` teal | 🍗 | sprzedaż |

Pole `_diffColumns`:
- `Rolnicza-Wolny` (różnica Rolnicza − Wolnorynkowa)
- `Różnica Tuszek` (Tuszka Zrzeszenia − Nasza Tuszka)

**Wolnorynkowa — imputacja:** Jeżeli brak wartości w danym dniu, interpolowana liniowo między najbliższymi znanymi datami. W wykresach imputowane punkty zaznaczone niebieskimi rombami.

**Klasyfikacja semantyczna kolumn (zakup vs sprzedaż)** — kolory min/max odwracane:
- **Zakup** (Minister, Laczona, Rolnicza, Wolnorynkowa, Tuszka Zrzeszenia): **niski = zielony (dobry)**, wysoki = czerwony (zły)
- **Sprzedaż** (Nasza Tuszka, Tuszka Zrzeszenia): **niski = czerwony (zły)**, wysoki = zielony (dobry)
- Hashset `_salesColumns = {"Nasza Tuszka", "Tuszka Zrzeszenia"}` decyduje o kierunku skali

---

## 2. Lista zakładek (11)

| # | Tab | Co | Główna metoda |
|---|---|---|---|
| 1 | 📋 Dane | DataGridView z surowymi danymi (heat-mapa per kolumna) + footer agregatów | `CreateDataTab` |
| 2 | 📈 Wykres Zakupowy | Linie cen zakupowych w czasie | `CreatePurchaseChartTab` |
| 3 | 💰 Wykres Sprzedażowy | Linie cen sprzedażowych w czasie | `CreateSalesChartTab` |
| 4 | 🎯 Wykres Przebitka | 3 serie marży: Zrzeszenie−Rolnicza, Nasza Tuszka−Wolnorynkowa, Nasza Tuszka−Średnia | `CreateMarginChartTab` |
| 5 | 📈 Wykres Łączony | Overlay z dwiema osiami Y (zakup lewa, sprzedaż prawa) | `CreateOverlayChartTab` |
| 6 | 📆 Sezonowość | Heatmap roczny (rok × miesiąc) | `CreateSezonowoscTab` |
| 7 | 🗓 Tygodnie | Analiza dni tygodnia per wybrana cena + hero cards | `CreateTygodnieTab` |
| 8 | 📈 YoY | Porównanie rok-do-roku, **4 tryby agregacji (Dzień/Tydzień/Miesiąc/Kwartał)**, **2 przebitki w selektorze** | `CreateYoYTab` |
| 9 | 📋 Kontrakty | **Kontrakt vs Wolny rynek** — KPI + dual chart + tabela szczegółów per TypCeny + dialog dostaw (DBLCLICK) | `CreateKontraktyTab` |
| 10 | 🌾 Pasze | Dual chart: ceny pasz + ceny żywca z korelacją (cykl 35-42 dni) | `CreatePaszeTab` |
| 11 | 📦 Klienci Top-N | Top klienci z bazy HANDEL (Sage) — wartość, ilość, średnia cena | `CreateKlienciTab` |

> Zakładki `📊 Statystyki`, `🔔 Alerty`, `📊 Prognoza` zostały usunięte z UI (kod `CreateStatsTab`, `CreateAlertyTab`, `CreatePrognozaTab` zostawiony — może wrócić w przyszłości).

---

## 3. Górny pasek wspólny (filtry)

Wiersz pod tytułem okna:
- **DateRange** (`mainDateFrom` / `mainDateTo`) — zakres dat
- **Presety** (przyciski) — Dziś, 7 dni, 1 mies, 3 mies, 6 mies, 12 mies, 24 mies, YTD, Cały okres
- **Weekendy** (checkbox) — pokaż/ukryj soboty + niedziele
- **KPI** (checkbox) — pokaż/ukryj pasek KPI nad zakładkami
- **Wyszukiwarka** — debounce 300 ms, filtruje grid w zakładce Dane

> Checkbox `"↔ Porównaj okresy"` został **usunięty** z UI w 2026-05-11. Pole `chkCompareToPrevious` pozostało jako null-safe (logika porównawcza w `UpdateStatistics` wykryje że jest null i pominie blok prev).

---

## 4. Zakładka YoY — agregacja okresów

**Field:** `_yoyAggMode` (`AggregationMode` enum: `Day`, `Week`, `Month`, `Quarter`, `Year`)
**Domyślnie:** `Month` (miesięcznie — najczystsza porównywalność rok-do-roku).

**Selektor `yoyColumnSelector`** zawiera 9 opcji:
- 7 kolumn raw (Nasza Tuszka, Wolnorynkowa, Rolnicza, Minister, Laczona, Tuszka Zrzeszenia, Śr. Wszystkich Dostaw)
- 2 przebitki (liczone per wiersz):
  - **`Przebitka: Zrzeszenie − Rolnicza`** → `Tuszka Zrzeszenia − Rolnicza`
  - **`Przebitka: Nasza Tuszka − Wolnorynkowa`** → `Nasza Tuszka − Wolnorynkowa`

**Logika agregacji** (`YoYChart_Paint`):
| Tryb | `maxPeriod` | `periodOf(d)` | Etykiety osi X |
|---|---|---|---|
| Day | 366 | `d.DayOfYear` | Sty/Lut/.../Gru (na pierwszym dniu miesiąca) |
| Week | 53 | `ISOWeek.GetWeekOfYear(d)` | T01, T05, ..., T52 |
| Month | 12 | `d.Month` | Sty, Lut, ..., Gru |
| Quarter | 4 | `(d.Month-1)/3+1` | Q1 (Sty‑Mar), Q2 (Kwi‑Cze), Q3 (Lip‑Wrz), Q4 (Paź‑Gru) |

Wartości w okresie agregowane jako **średnia arytmetyczna** wszystkich notowań danego roku w danym okresie.

---

## 5. Zakładka Kontrakty — definicje i SQL

### 5.1 Definicja Kontrakt vs Wolny rynek

```csharp
CASE WHEN LOWER(TypCeny) IN ('wolnyrynek', 'wolnorynkowa')
     THEN 'Wolny' ELSE 'Kontrakt' END AS Kategoria
```

- **Kontrakt** = `TypCeny ∈ {rolnicza, ministerialna, łączona, …wszystkie inne}` — hodowca z umową (Sergiusz dostarcza paszę, odbiera po umówionej cenie)
- **Wolny rynek** = `TypCeny ∈ {wolnyrynek, wolnorynkowa}` — hodowca bez umowy, cena bieżąca

### 5.2 Źródło danych — `LibraNet.dbo.HarmonogramDostaw`

**Pełny SQL** (linie ~4605 w `LoadKontraktyDataAsync`):

```sql
SELECT
    Lp AS Lp,
    DataOdbioru AS Data,
    ISNULL(TypCeny, '(brak)') AS TypCeny,
    CASE WHEN LOWER(TypCeny) IN ('wolnyrynek','wolnorynkowa')
         THEN 'Wolny' ELSE 'Kontrakt' END AS Kategoria,
    ISNULL(Dostawca, '') AS Dostawca,
    ISNULL(DostawcaID, 0) AS DostawcaID,
    ISNULL(TypUmowy, '') AS TypUmowy,
    ISNULL(Auta, 0) AS Auta,
    CAST(Cena AS DECIMAL(10,4)) AS Cena,
    CAST(ISNULL(SztukiDek, 0) AS DECIMAL(18,2)) AS Sztuki,
    CAST(ISNULL(WagaDek, 0) AS DECIMAL(18,4)) AS WagaSrednia,
    CAST(ISNULL(SztukiDek, 0) * ISNULL(WagaDek, 0) AS DECIMAL(18,2)) AS WolumenKg,
    ISNULL(UWAGI, '') AS Uwagi
FROM [LibraNet].[dbo].[HarmonogramDostaw]
WHERE Bufor = 'Potwierdzony'
    AND DataOdbioru >= @from AND DataOdbioru <= @to
    AND Cena IS NOT NULL AND Cena > 0
    AND SztukiDek IS NOT NULL AND SztukiDek > 0
    AND TypCeny IS NOT NULL
ORDER BY DataOdbioru
```

**Filtry:**
- `Bufor = 'Potwierdzony'` — tylko zatwierdzone dostawy (pomija plany / wstępne)
- `Cena > 0` — pomija dostawy bez ceny
- `SztukiDek > 0` — pomija puste rekordy
- `TypCeny IS NOT NULL` — pomija sieroty bez klasyfikacji

**Agregacja** robiona po stronie .NET (LINQ) — dla Day/Week/Month/Quarter, helper `GetPeriodKey(DateTime, AggregationMode)`.

### 5.3 Wymiary obliczane

- **`WolumenKg`** = `SztukiDek × WagaDek` (kg żywca per rekord)
- **Średnia ważona cena** = `Σ(Cena × Sztuki) / Σ(Sztuki)` (ważona liczbą sztuk, nie kg!)
- **Udział wolumenowy %** = `kontrakt_kg / (kontrakt_kg + wolny_kg) × 100`

### 5.4 Layout zakładki Kontrakty

```
┌── Compact controls (46 px, full width) ─────────────────────────┐
│  📋 Kontrakty  🟢 Kontrakt 🔵 Wolny  [D][W][M][Q]  status   ?   │
├── Charts (fill) ─────────────────── ┬── Right col (360 px) ─────┤
│                                      │  🤝 Kontrakt (KPI 1)     │
│  💰 Średnia ważona cena (line)       │  🏪 Wolny rynek (KPI 2)  │
│      kontrakt vs wolny w czasie      │  💰 Średnia cena (KPI 3) │
│                                      │  📊 Stosunek K/W (KPI 4) │
│  ⚖ Udział wolumenowy 100% stacked    │  ────────────────────    │
│      kontrakt + wolny = 100%/okres   │  📋 Szczegóły per TypCeny│
│                                      │     (mini-fit table)     │
└──────────────────────────────────────┴──────────────────────────┘
```

**4 KPI cards stacked vertically (304×68 px każda):**
1. **🤝 Kontrakt** — kg + % wolumenu + liczba dostaw (zielony)
2. **🏪 Wolny rynek** — kg + % wolumenu + liczba dostaw (niebieski)
3. **💰 Średnia cena** — K x.xx / W y.yy zł/kg + spread (pomarańczowy)
4. **📊 Stosunek K/W** — % / % + łącznie kg + dostaw (fioletowy)

**Wykres** (`KontraktyChart_Paint`) — 2 sekcje pionowe:
- **Górna (52% wys)** — line chart średniej ważonej ceny per okres, kontrakt zielony, wolny niebieski, markery na każdym punkcie, etykiety wartości dla okresów rzadkich (M/Q)
- **Dolna (42% wys)** — 100% stacked bar wolumenu per okres (zielony dół = kontrakt, niebieski góra = wolny), % wewnątrz słupków gdy szerokość ≥ 28 px
- **Bez linii celu 50%** (usunięta na życzenie usera 2026-05-11)
- Etykiety osi X dopasowane do trybu: `dd.MM` / `T##/yy` / `MMM yy` / `Q# yy`, auto-decimowane gdy okresów > 20

### 5.5 Tabela szczegółów (mini-fit, 6 kolumn)

`_kontraktyDetailGrid` — wąska (kolumna 360 px), wysokość = `header + N × 22 px` (max ~12 wierszy, dalej scroll).

| Kolumna | FillWeight | Format |
|---|---|---|
| TypCeny (oryginalny) | 28 | tekst |
| Kat. (🤝 K / 🏪 W) | 16 | tekst, kolor wg kategorii |
| Kg | 18 | N0 |
| % | 10 | N1 |
| Śr. cena | 16 | N2 |
| Dost. (liczba dostaw) | 12 | N0 |

**Tooltip** w kolumnie TypCeny — Min/Max cena.

### 5.6 Dialog szczegółów dostaw (DBLCLICK)

Podwójne kliknięcie wiersza → `ShowKontraktyDeliveriesDialog(typCeny)`:

```
┌── Search (44 px, col 0) ────────┬── Info panel (320 px, RowSpan 2) ┐
│  🔍 [filtr…]      (X z Y)       │  🤝 TypCeny                       │
├── Grid (fill, col 0) ───────────┤  Kategoria                        │
│  12 kolumn:                      │                                   │
│    Lp, Data, Dostawca, ID,       │  📊 Statystyki łącznie            │
│    Auta, Sztuki, Waga, Kg,       │    Dostawy / Dostawców / kg /     │
│    Cena, Wartość, TypUmowy,      │    Sztuki / Wartość / Śr. ważona  │
│    Uwagi                         │    Min/Max / Zakres dat            │
│                                  │                                    │
│  Wiersz SUMA (przefiltrowany)    │  🔎 Filtr (live update)            │
│                                  │  [💾 Eksport CSV (anchor bottom)]  │
└──────────────────────────────────┴────────────────────────────────────┘
```

**Filtrowanie** (TextChanged, instant): Dostawca / DostawcaID / TypUmowy / Uwagi / Lp (case-insensitive contains).
**CSV** — UTF-8 BOM, separator `;`, format `Dostawy_{TypCeny}_{yyyyMMdd}.csv`. Średniki i CR/LF w polach text zamieniane na `,` / spację. Eksportowane są **tylko przefiltrowane** dostawy.

---

## 6. Kolumny `HarmonogramDostaw` używane przez moduł

> Tabela `LibraNet.dbo.HarmonogramDostaw` — plan dostaw żywca od hodowców. Kolumny wykorzystywane przez moduł Kontrakty + zaobserwowane w innych miejscach kodu.

| Kolumna | Typ | Co | Uwagi |
|---|---|---|---|
| `Lp` | int | Klucz wiersza | Unikalny per dostawa |
| `LpW` | int | Numer wstawienia | FK do wstawienia kurczaków |
| `Dostawca` | varchar | **Nazwa hodowcy** (denormalizowana) | Patrz indeks `IX_HarmonogramDostaw_Bufor_DataOdbioru` |
| `DostawcaID` | int | ID hodowcy | FK do tabeli hodowców (Pozyskiwanie_Hodowcy?) |
| `DataOdbioru` | datetime | Data odbioru żywca | Główny filtr czasowy |
| `Bufor` | varchar | Status dostawy | `'Potwierdzony'` = zatwierdzona; inne wartości to plan/wstępne |
| `TypCeny` | varchar | **Klasyfikacja ceny** | Wartości obserwowane: `wolnyrynek`, `wolnorynkowa`, `rolnicza`, `ministerialna`, `łączona` (case-insensitive!) |
| `TypUmowy` | varchar | Typ umowy z hodowcą | Wolny tekst |
| `Cena` | decimal | Cena za sztukę lub kg | (Sergiusz: per kg w kontrakcie, sprawdzić) |
| `SztukiDek` | decimal | Sztuki deklarowane | Używane do średniej ważonej ceny |
| `WagaDek` | decimal | Waga średnia per sztuka (kg) | `SztukiDek × WagaDek = WolumenKg` |
| `Auta` | int | Liczba aut/transportów | Patrz `Zywiec/Kalendarz/Services/InlineEditValidator.cs` — walidacja ≥ 0 |
| `KmK`, `KmH` | int | Klatki (komenda + henhouse?) | Patrz `WstawienieWindow` |
| `SztSzuflada` | int | Sztuki w szufladzie | Logistyka |
| `UWAGI` | varchar | Uwagi tekstowe | |
| `DataUtw` | datetime | Data utworzenia rekordu | Audit |
| `KtoStwo` | varchar | Kto utworzył (UserID) | Audit |
| `PotwWaga` | decimal | Potwierdzona waga (po odbiorze) | Wypełniana po ważeniu |
| `PotwSztuki` | int | Potwierdzona liczba sztuk | |

**Indeks:** `IX_HarmonogramDostaw_Bufor_DataOdbioru` na (Bufor, DataOdbioru) z include Dostawca — przyspiesza zapytania filtrujące po `Bufor='Potwierdzony' AND DataOdbioru BETWEEN`.

**Powiązania:**
- `listapartii.HarmonogramLp = HarmonogramDostaw.Lp` (V2, partia ↔ harmonogram)
- Insert SQL w `WstawienieWindow.xaml.cs` linia ~2653 — pełna lista kolumn przy `INSERT`

---

## 7. Klasa pomocnicza `GetPeriodKey`

```csharp
private (DateTime SortKey, string Label) GetPeriodKey(DateTime d, AggregationMode mode)
{
    switch (mode)
    {
        case AggregationMode.Day:
            return (d.Date, d.ToString("dd.MM"));
        case AggregationMode.Week:
            int delta = ((int)d.DayOfWeek - 1 + 7) % 7;
            DateTime mon = d.Date.AddDays(-delta);
            int wk = System.Globalization.ISOWeek.GetWeekOfYear(d);
            return (mon, $"T{wk:00}/{d:yy}");
        case AggregationMode.Month:
            return (new DateTime(d.Year, d.Month, 1),
                    d.ToString("MMM yy", new CultureInfo("pl-PL")));
        case AggregationMode.Quarter:
            int q = (d.Month - 1) / 3 + 1;
            DateTime start = new DateTime(d.Year, (q - 1) * 3 + 1, 1);
            return (start, $"Q{q} {d:yy}");
        default:
            return (d.Date, d.ToString("dd.MM"));
    }
}
```

**Zasada:** `SortKey` = początek okresu (poniedziałek tygodnia, 1. dnia miesiąca/kwartału). `Label` = etykieta dla osi X.

**Tydzień:** ISO 8601 (poniedziałek-niedziela, tydzień 1 zawiera 4 stycznia).

---

## 8. Stan zakładek po refactorze 2026-05-11

✅ **Aktywne:** Dane, Wykres Zakupowy, Sprzedażowy, Przebitka, Łączony, Sezonowość, Tygodnie, YoY, Kontrakty, Pasze, Klienci Top-N
❌ **Usunięte z UI:** Statystyki (kod `CreateStatsTab` + `CreateStatsCard` zostawiony w pliku), Alerty, Prognoza
🔧 **Usunięty checkbox:** `chkCompareToPrevious` ("Porównaj okresy") — z górnego paska. Pole pozostało jako null-safe.

---

## 9. Konwencje wizualne (mojowe dla tego modułu)

- **Kolory zakup vs sprzedaż** — heat-mapa odwrócona, klasyfikacja przez `_salesColumns` hashset
- **Typografia:** Segoe UI / Segoe UI Semibold; emoji w **Segoe UI Emoji Regular** (NIE Bold — Segoe UI Emoji nie ma wariantu Bold, fallback gubi color glyphs)
- **Karty (KPI / hero):** zaokrąglone rogi r=10 px, accent bar 4 px po lewej w kolorze kategorii
- **Linia bieżącego roku w YoY:** grubsza (3 px) niż lata historyczne (2 px), starsze niż -1 — dashed

---

## 10. Powiązane pliki / referencje

| Plik | Po co |
|---|---|
| `WidokKalendarzaWPF.xaml.cs` | Otwiera "Pokaż ceny" z menu kontekstowego |
| `Zywiec/Kalendarz/Services/InlineEditValidator.cs` | Walidacja kolumny `Auta` w kalendarzu dostaw (≥ 0) |
| `Zywiec/WstawieniaKurczaka/WstawienieWindow.xaml.cs` ~2653 | Pełny INSERT do `HarmonogramDostaw` z listą wszystkich kolumn |
| `Dostawa.cs` ~87 | SELECT z `HarmonogramDostaw` po `LpW = @NumerWstawienia` |
| `HarmonogramDostawRepository.cs` | Repository dla `HarmonogramDostaw` |
| `13_Bazy_danych.md` | Tabela `HarmonogramDostaw` (Plan dostaw żywca od hodowców) |
| `04_Klienci_dostawcy.md` | Ceny żywca (wolny 4.00, rolnicza 4.40, ministerialna 5.23) + model kontraktowy 50/50 |
| `08_Sprzedaz_ceny.md` | Polityka cenowa, marża top-down, bufor |

---

**Stan dokumentu:** 2026-05-11 — po refactorze layoutu Kontrakty (4 KPI stacked, mini-fit table, dialog dostaw z search + info panel).
