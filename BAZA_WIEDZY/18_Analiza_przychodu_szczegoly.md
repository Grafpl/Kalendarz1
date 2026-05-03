# 18 — Analiza Przychodu Produkcji (szczegóły techniczne)

**Źródło:** `BAZA_DANYCH_ANALIZA_PRZYCHODU.md` (Sergiusz + Claude, 2026-05-03), oparte na analizie SQL-i z `AnalizaPrzychoduProdukcji/Services/PrzychodService.cs`.

**Po co osobny plik:** Moduł "Analiza Przychodu Produkcji" pracuje na bardzo specyficznym zestawie tabel (`In0E`, `Article`, `PartiaDostawca`) i ma niuanse, których nie warto mieszać z ogólną wiedzą. Jeśli pracujesz nad tym modułem — czytaj ten plik. Jeśli nad innym modułem — wystarczy `13_Bazy_danych.md`.

---

## 1. Architektura — dwie bazy, dwa światy

| Baza | Serwer | Co zawiera | Czyje |
|---|---|---|---|
| **`LibraNet`** | **192.168.0.109** | **Przychód produkcji** — ważenia, partie, hodowcy, klasy, operatorzy | „Nasze" (Sergiusz, ZPSP, Pronova) |
| **`HM`** (Symfonia Handel) | **192.168.0.112** | **Sprzedaż / handel** — faktury, kontrahenci, zamówienia | Zewn. ERP |

**Workflow:**
1. Kurczak żywy → przyjęcie + ważenie + paletyzacja w **LibraNet (109)**
2. Kurczak idzie na produkcję, robione są tuszki / porcje
3. Na koniec dnia kierownik liczy łączną produkcję
4. **Przychód towaru** wprowadzany do **Symfonia Handel (112)**
5. Z Symfonii idzie sprzedaż klientom (faktury, WZ)

> 🟢 **Moduł "Analiza Przychodu Produkcji" pracuje WYŁĄCZNIE na LibraNet (109).** Nie czyta sprzedaży z 112.

---

## 2. Słownik biznesowy

| Termin | Definicja |
|---|---|
| **Kurczak surowiec** | Kurczak żywy (przed ubojem) |
| **Tuszka** | Mięso po uboju — cała tusza (bez podziału na elementy) |
| **Kurczak Klasy A** | Tuszka bez wad (klasa **jakościowa**, nie wielkościowa). W bazie: `Article.ID = '40'` |
| **Klasa wielkości** | Numer **5-12** określający rozmiar tuszki (mniejszy numer = większy ptak). Pole DB: `In0E.QntInCont` |
| **Hodowca** | Zewnętrzny dostawca surowca. **Wszyscy hodowcy są zewnętrzni — firma NIE ma własnych ferm** |
| **Partia** | Jeden transport = jedna partia. Numer = `CustomerID (3 cyfry)` + `Partia (8 cyfr)` |
| **Mix partii** | Mięso z 2 transportów się łączy → tworzona NOWA partia |
| **Paleta** | Drewniana paleta. Tara stała |
| **Pojemnik E2** | Plastikowy pojemnik 15 kg netto. Tara stała |
| **Operator wagowy** | Pracownik fizycznie ważący — `OperatorID` (stałe ID) + `Wagowy` (imię, denormalizowane, zmienne historycznie) |
| **Storno** | Anulacja ważenia → `ActWeight < 0` |
| **Weight (standard)** | Waga deklarowana / nominalna z kartoteki towaru |
| **ActWeight (rzeczywista)** | Faktyczna waga po zważeniu (już po odjęciu tary) |
| **Dokładamy** | `ActWeight > Weight + tolerancja` — strata firmy |
| **Niedowaga** | `ActWeight < Weight - tolerancja` — ryzyko reklamacji |

---

## 3. Workflow ważenia (jak rodzą się rekordy w `In0E`)

1. **Kierownik produkcji** wprowadza zaplanowane partie — **maks. 16 partii dziennie**
2. Auto z hodowcą podjeżdża, surowiec **przyjęty** → `PartiaDostawca` dostaje rekord (`CustomerID` + `CustomerName`)
3. Kurczak idzie na linię, ubijany, robione tuszki
4. **Tuszki ważone na wadze paletowej / pojemnikowej:**
   - Operator skanuje / wybiera **partię** (z max 16 dostępnych)
   - Operator zaznacza w programie wagowym czy waży **paletę czy pojemnik E2** (decyduje która tara)
   - Waga waży **netto** (program wagowy odejmuje tarę)
   - **Program wagowy** wstawia rekord do `dbo.In0E`
5. Rekord ma: `Data`, `Godzina`, `OperatorID`, `Wagowy`, `ArticleID`, `ActWeight`, `Weight`, `Tara`, `P1`, `QntInCont`, `TermID`
6. Na koniec dnia kierownik sumuje produkcję → wpisuje **przychód** do Symfonii (112)

> ⚠ **Operator zaznacza pojemnik/paleta w PROGRAMIE WAGOWYM** (zewn.) — nie w ZPSP. Do `In0E` trafia waga netto.

---

## 4. Tabele wykorzystywane

| Tabela | Rola | Tryb |
|---|---|---|
| `dbo.In0E` | **Rdzeń modułu** — każdy rekord = jedno fizyczne ważenie | tylko READ |
| `dbo.Article` | Słownik towarów | tylko READ |
| `dbo.PartiaDostawca` | Mapowanie partia → hodowca | tylko READ |
| `dbo.Out1A` | **NIE UŻYWAMY** (patrz §9) | — |

---

## 5. `dbo.In0E` — ważenia przychodu

**Najważniejsza tabela.** Każdy wiersz = jedno fizyczne ważenie. Zapisywana przez **program wagowy** (zewn.) — my tylko READ.

### Kolumny

| Kolumna | Typ | Opis | Quirki |
|---|---|---|---|
| `ArticleID` | varchar | Klucz do `Article.ID` (`'40'` = Kurczak A) | — |
| `ArticleName` | varchar | Nazwa towaru w momencie ważenia (denormalizacja) | „zamrożona w czasie" |
| `JM` | varchar | Jednostka (zawsze `kg`) | — |
| `TermID` | int | ID terminala / wagi | TODO: mapowanie ID → fizyczna waga |
| `TermType` | varchar | Nazwa typu terminala (np. „Linia 1", „Paletyzator") | TODO |
| `Weight` | decimal | Waga **standardowa** z kartoteki | — |
| `ActWeight` | decimal | Waga **rzeczywista netto** (po odjęciu tary) | **Ujemna = storno** |
| `Quantity` | numeric | Ilość sztuk (rzadko != 1) | — |
| `Direction` | varchar | Kierunek dokumentu | TODO |
| `Data` | date / varchar | Data ważenia | TODO: zbadać dokładny typ |
| `Godzina` | varchar | Godzina ważenia (`HH:mm:ss` jako TEKST) | Filtrowanie: `TRY_CAST(LEFT(Godzina,2) AS INT)` |
| `OperatorID` | varchar | **Stałe ID** operatora — historyczny klucz | — |
| `Wagowy` | varchar | Imię i nazwisko — **może się zmieniać historycznie** | — |
| `Tara` | decimal | Waga palety/pojemnika odjęta z brutto. **Stała na typ opakowania** | — |
| `Price` | decimal | Cena jednostkowa | Rzadko używana |
| `P1` | varchar(15) | **Numer partii** (8 cyfr) — klucz do `PartiaDostawca.Partia` | Patrz §7 |
| `P2` | varchar(15) | Druga partia. **W 99% `P2 = P1`.** Sergiusz nie wie po co była | — |
| `QntInCont` | int | **Klasa wielkościowa** (5-12 dla kurczaka, 0 = brak/mix) | Patrz §8 |

### Wzorce użycia

**Storno / anulacja:**
```sql
ActWeight < 0   -- ważenie cofnięte przez operatora
ActWeight > 0   -- normalne ważenie
ActWeight = 0   -- śmieć / przerwane (rzadkość)
```

**Odchylenie wagowe (KPI):**
```
Roznica       = ActWeight - Weight                       [kg]
RoznicaProc   = (ActWeight - Weight) / Weight * 100      [%]
Dokladamy     = Roznica > +tolerancja                    (strata firmy)
Niedowaga     = Roznica < -tolerancja                    (ryzyko reklamacji)
```

> ⚠ **Tolerancja `0.05 kg` (50 g) w obecnym kodzie to wartość ARBITRALNA** dodana przez Claude. Sergiusz potwierdza: *"Są tolerancje różne na towar. Możemy znaleźć za pomocą SELECT."* — patrz TODO #4.

**Klasy:**
- `QntInCont` ∈ {5, 6, 7, 8, 9, 10, 11, 12} **tylko dla `ArticleID = '40'`** (Kurczak A)
- Dla innych towarów `QntInCont = 0` (irrelewantne)

---

## 6. `dbo.Article` — słownik towarów

**Klucz pojedynczy:** `ID` (varchar).

| Kolumna | Opis |
|---|---|
| `ID` | Klucz (`'40'` = Kurczak A) |
| `Name` | Pełna nazwa |
| `ShortName` | Skrót (np. „Filet B/S", „K. A 1500+") |

### Specjalne ID

| ID | Znaczenie |
|---|---|
| **`'40'`** | **Kurczak Klasy A** — surowiec / tuszka bez wad. **Jedyny artykuł z aktywną klasyfikacją wielkości** (`QntInCont`) |
| inne | Produkty końcowe (filet, korpus, ćwiartki, podroby) |

> **TODO:** Sprawdzić czy `Article` ma kolumny `MinWeight`, `MaxWeight`, `Tolerance`, `WeightStandard` — to klucz do liczenia tolerancji per towar.

---

## 7. `dbo.PartiaDostawca` — partia ↔ hodowca + DEKODER

### Kolumny

| Kolumna | Typ | Opis |
|---|---|---|
| `guid` | uniqueidentifier | UUID (PK techniczny) |
| **`Partia`** | varchar(15) | **Numer partii (8 cyfr)** |
| **`CustomerID`** | varchar(3) | **ID hodowcy (3 cyfry)** |
| `CustomerName` | varchar | Imię i nazwisko hodowcy |
| `CreateData` | date | Data utworzenia (= data przyjęcia) |
| `CreateGodzina` | varchar | Godzina utworzenia |
| `ModificationData` | date | Modyfikacja (puste = nie modyfikowano) |

### 🔑 DEKODER NUMERU PARTII (poprawiony)

**Pełna partia w nomenklaturze firmowej:**

```
[CustomerID] + [Partia]  =  [3 cyfry hodowcy] + [8 cyfr partii]
```

**Kolumna `Partia` (8 cyfr) rozkłada się na 3 segmenty:**

```
26  119  001
RR  DDD  AAA
└── rok (2 cyfry, ostatnie 2 cyfry roku)
    └── dzień w roku (3 cyfry, 001-366)
        └── numer auta od tego hodowcy w tym dniu (3 cyfry)
```

### Przykład

Z rzeczywistych danych:

| guid | CustomerID | Partia | CustomerName | CreateData |
|---|---|---|---|---|
| 65D931CD… | **390** | **26119004** | Szymczak Dariusz | 2026-04-29 |

Rozkład:
- **CustomerID 390** = hodowca Szymczak Dariusz
- **Partia 26119004:**
  - **`26`** = rok 20**26**
  - **`119`** = **119. dzień roku 2026** = **29 kwietnia** ✓
  - **`004`** = **4. auto** od tego hodowcy w tym dniu

**Pełna nomenklatura:** `390-26119004` lub `39026119004` (kontekstowo).

### Konsekwencje

- `In0E.P1` przechowuje **TYLKO `Partia`** (8 cyfr) — bez `CustomerID`. JOIN potrzebny.
- W teorii ten sam `Partia` (8 cyfr) może wystąpić u różnych hodowców. **Realnie** `In0E.P1` jest unikalne (każda partia = jeden hodowca).
- **Mix partii:** gdy mięso z 2 transportów się miesza → **nowa partia**. Numer auta zostaje (`001`), ale ID hodowcy z przodu jest inne — tworzy nowy rekord w `PartiaDostawca`.
- **Ten sam dostawca pod różnymi `CustomerID`** — np. ferma + brat. Realnie ta sama działalność. Przy raportowaniu warto agregować po `CustomerName` z normalizacją.

---

## 8. Klasy wielkościowe (5-12)

> **Klasa = liczba sztuk tuszek mieszczących się w pojemniku E2 (15 kg netto).**
> **Mniejszy numer = większy ptak.**

Wzór: `średnia waga tuszki ≈ 15 kg / numer_klasy`

| Klasa | Sztuk w 15kg | Średnia waga tuszki | Komentarz |
|---|---|---|---|
| **5** | 5 szt | ≈ 3.0 kg | Bardzo duży |
| **6** | 6 szt | ≈ 2.5 kg | **Idealna klasa** |
| **7** | 7 szt | ≈ 2.14 kg | **Idealna klasa** |
| **8** | 8 szt | ≈ 1.875 kg | Średni |
| **9** | 9 szt | ≈ 1.67 kg | Średni |
| **10** | 10 szt | ≈ 1.5 kg | Mniejszy |
| **11** | 11 szt | ≈ 1.36 kg | Mały |
| **12** | 12 szt | ≈ 1.25 kg | Najmniejszy w użyciu |

**Klasa preferowana:** 6-7.

**Klasa 0:**
- `QntInCont = 0` to **najczęściej zapomnienie wpisania klasy przez operatora** lub **mix klas w pojemniku** (różne rozmiary)
- W analizach: traktować jako odrębną kategorię „brak/mix" — **nie usuwać**, ale flagować jako anomalię operatora

**Brak normy:** *"Co przyjdzie z hodowcy, to przyjdzie"* — statystyki klas są **deskryptywne, nie preskryptywne**.

---

## 9. `dbo.Out1A` — sprzedaż (NIE UŻYWAMY)

> ⚠ **Tabela istnieje w LibraNet, ale Sergiusz nie tworzył jej i nie wie do czego dokładnie służy.**
> **Sprzedaż firmy jest w Symfonia Handel na 112**, nie tu.
> Service `LoadSalesAsync` w kodzie napisany historycznie, ale **zakładka Sprzedaż usunięta z UI** na życzenie usera. Nie polegać na tych danych.

### Kolumny widoczne (do sprawdzenia)

| Kolumna | Prawdopodobnie | Status |
|---|---|---|
| `ArticleID`, `ArticleName` | Towar | Standard |
| `CustomerID` | Klient — ale jakiej bazy? | TODO |
| `Data`, `Godzina` | Data wydania | Standard |
| `Weight`, `ActWeight`, `Price` | Wagi i cena | Prawdopodobnie OK |
| `P1` | Partia produktu | TODO |
| `Related_IN` | Prawdopodobnie partia surowca (`In0E.P1`) | TODO zweryfikować |
| `DocNo`, `OrderNo` | Numer dokumentu / zamówienia | TODO |

---

## 10. Operatorzy

### Aktywni
- **3-5 osób jednocześnie** na wagach
- Lista wszystkich (kiedykolwiek aktywnych) jest dłuższa — moduł filtruje po aktywności w ostatnich 90 dniach

### Stabilność identyfikatora

| Pole | Stabilność |
|---|---|
| `OperatorID` | **STAŁE** — nigdy się nie zmienia, klucz historyczny |
| `Wagowy` | **MOŻE SIĘ ZMIENIAĆ** (literówka, zmiana nazwiska). Stare ważenia zachowują starą wartość |

> 💡 **Konsekwencja:** Grupuj po `OperatorID`, nie po `Wagowy` — bo Anna Kowalska może być w bazie jako "Anna Kowalska", "A. Kowalska", "Ania Kowalska".

### Klasyfikacja Paletujący / Porcjujący

Sergiusz: *"ArticleID=40 zawsze będzie robiony na wadze paletowej tylko i wyłącznie."*

Heurystyka w module:
```csharp
Paletuje = g.Count(r => r.ArticleID == "40") > g.Count() / 2
```

| Typ | Definicja | W praktyce |
|---|---|---|
| **Paletujący** | >50% ważeń to ArticleID=40 | Wagi paletowej |
| **Porcjujący** | ≤50% ważeń to ArticleID=40 | Wagi pojemnikowej (filet, korpus) |

> ⚠ Heurystyka **probabilistyczna**. Czy operator w 100% obsługuje tylko jedną wagę — Sergiusz nie wie. Patrz TODO #9.

---

## 11. Zmiany dzień / noc

| Zmiana | Godziny | Stała w kodzie |
|---|---|---|
| **Dzienna** | 5:00 – 21:00 | `DAY_SHIFT_START = 5` |
| **Nocna** | 21:00 – 5:00 | `NIGHT_SHIFT_START = 21` |

> ❓ **Sergiusz nie pamięta czy te granice są twarde** — czy są wyjątki (krótszy piątek, weekend, dni przedświąteczne). **Zweryfikować SQL-em** — TODO #3.

---

## 12. Quirki i pułapki

### `P1 = P2`
W 99% `P2 = P1`. Sergiusz nie wie po co historycznie było `P2`. Moduł NIE czyta P2.

### Zombie partia z 2014
W bazie istnieją rekordy z partią z 2014 roku. Stała `MinPartiaCreateData = "2024-01-01"` (możliwa migracja danych styczeń 2024). W praktyce niestosowana — wszystkie zapytania mają DatePicker.

### `Godzina` jako tekst
String `HH:mm:ss`, nie kolumna `time`. Filtrowanie:
```sql
TRY_CAST(LEFT(Godzina,2) AS INT) >= @GodzOd
```

### `IsClose` to martwe pole
Historyczna flaga `IsClose` **NIE jest aktualizowana**. Status partii w nowej tabeli `PartiaStatus` (Partie V2).

### `In0E.ArticleName` zamrożone w czasie
Kopia w momencie ważenia. Jeśli ktoś zmieni nazwę w `Article` — historyczne ważenia zachowają starą.

```csharp
// Ranking nadpisuje aktualną nazwą:
if (_articleDict.TryGetValue(g.Key, out var info))
    articleName = info.Name;
```

### `(brak partii)` w danych
Pojedyncze rekordy w `In0E` mają puste `P1`. Sergiusz: *"Prawdopodobnie błąd."* Moduł grupuje pod `(brak partii)`.

### Operator może być w obu kategoriach
Choć Sergiusz uważa, że ArticleID=40 zawsze leci na wadze paletowej, na poziomie danych operator może mieć rekordy obu typów. Heurystyka >50% rozstrzyga.

### Ten sam dostawca pod różnymi `CustomerID`
Patrz §7. Trzeba uważać przy raportowaniu agregowanym.

---

## 13. Schemat relacji (konceptualny)

```
                ┌────────────────────────┐
                │      dbo.Article       │
                │  ID (PK)               │
                │  Name, ShortName       │
                └───────────┬────────────┘
                            │ ArticleID
                ┌───────────▼──────────┐
                │      dbo.In0E        │
                │  (ważenia produkcji) │
                │                      │
                │  ArticleID           │
                │  Weight (standard)   │
                │  ActWeight (rzecz.)  │
                │  P1 ─────────────────┼──┐
                │  TermID (waga)       │  │
                │  OperatorID          │  │
                │  QntInCont (klasa)   │  │
                │  Tara                │  │
                └──────────────────────┘  │
                                          │
                                   ┌──────▼──────────────┐
                                   │ dbo.PartiaDostawca  │
                                   │  Partia (8 cyfr)    │
                                   │  CustomerID (3 c.)  │
                                   │  CustomerName       │
                                   │  CreateData/Godz.   │
                                   └─────────────────────┘
```

---

## 14. Kluczowe SQL queries (referencyjne)

### Lista hodowców z aktywnych partii (90 dni)
```sql
SELECT DISTINCT pd.CustomerID, pd.CustomerName
FROM dbo.PartiaDostawca pd
WHERE pd.CustomerName IS NOT NULL AND pd.CustomerName <> ''
  AND pd.Partia IN (
      SELECT DISTINCT P1 FROM dbo.In0E
      WHERE P1 IS NOT NULL AND P1 <> ''
        AND Data >= CONVERT(varchar(10), DATEADD(DAY, -90, GETDATE()), 120)
  )
ORDER BY pd.CustomerName
```

### Dekoder partii — jednoliniowiec
```sql
SELECT
    e.P1                                              AS partia_kod,
    pd.CustomerID + '-' + e.P1                        AS partia_pelna,
    pd.CustomerName                                    AS hodowca,
    20 * 100 + CAST(LEFT(e.P1, 2) AS INT)              AS rok_partii,
    CAST(SUBSTRING(e.P1, 3, 3) AS INT)                 AS dzien_roku,
    CAST(SUBSTRING(e.P1, 6, 3) AS INT)                 AS numer_auta,
    DATEADD(DAY, CAST(SUBSTRING(e.P1, 3, 3) AS INT) - 1,
            DATEFROMPARTS(2000 + CAST(LEFT(e.P1, 2) AS INT), 1, 1))
                                                       AS data_z_partii
FROM dbo.In0E e
LEFT JOIN dbo.PartiaDostawca pd ON pd.Partia = e.P1
WHERE e.P1 = '26119001';
```

### Lista operatorów (90 dni)
```sql
SELECT DISTINCT OperatorID, Wagowy
FROM dbo.In0E
WHERE OperatorID IS NOT NULL
  AND Wagowy IS NOT NULL AND Wagowy <> ''
  AND Data >= CONVERT(varchar(10), DATEADD(DAY, -90, GETDATE()), 120)
ORDER BY Wagowy
```

### Mapa partia → hodowca dla okresu (cachowana)
```sql
SELECT pd.Partia, pd.CustomerID, pd.CustomerName
FROM dbo.PartiaDostawca pd
WHERE pd.Partia IN (
    SELECT DISTINCT P1 FROM dbo.In0E
    WHERE P1 IS NOT NULL AND P1 <> ''
      AND Data >= @DataOd AND Data <= @DataDo
)
```

---

## 15. Sugerowane indeksy (jeśli problem z wydajnością)

```sql
CREATE INDEX IX_In0E_Data_ArticleID    ON dbo.In0E (Data, ArticleID);
CREATE INDEX IX_In0E_P1                ON dbo.In0E (P1);
CREATE INDEX IX_In0E_Data_OperatorID   ON dbo.In0E (Data, OperatorID);
CREATE INDEX IX_PartiaDostawca_Partia  ON dbo.PartiaDostawca (Partia);
```

> **TODO #5:** Zweryfikować jakie indeksy faktycznie są.

---

## 16. Pliki kodu modułu

| Plik | Co zawiera |
|---|---|
| `AnalizaPrzychoduProdukcji/Services/PrzychodService.cs` | Wszystkie zapytania SQL (jedyne źródło prawdy) |
| `AnalizaPrzychoduProdukcji/Models/PrzychodModels.cs` | Modele DTO odpowiadające kolumnom |
| `AnalizaPrzychoduProdukcji/AnalizaPrzychoduWindow.xaml.cs` | Logika UI, agregacje w pamięci, drill-down, LIVE |
| `AnalizaPrzychoduProdukcji/AnalizaPrzychoduWindow.xaml` | UI (6 zakładek, 5 kart KPI, Health Strip) |
| `AnalizaPrzychoduProdukcji/ViewModels/AnalizaPrzychoduViewModel.cs` | Bindings dla LiveCharts |

---

## 17. TODO — pytania badawcze (10 zapytań SQL)

Lista pytań bez odpowiedzi z eksploracyjnymi SQL-ami. **Każdy do uruchomienia w SSMS na 192.168.0.109/LibraNet.**

### TODO #1 — Co to `TermType`? Mapowanie ID → fizyczne stanowisko

```sql
SELECT TermID, TermType,
    COUNT(*) AS liczba_wazen,
    SUM(CASE WHEN ActWeight > 0 THEN ActWeight ELSE 0 END) AS suma_kg,
    MIN(Data) AS pierwsza, MAX(Data) AS ostatnia
FROM dbo.In0E
WHERE Data >= CONVERT(varchar(10), DATEADD(DAY, -30, GETDATE()), 120)
GROUP BY TermID, TermType
ORDER BY suma_kg DESC;
```

### TODO #2 — Co znaczy `Direction`?

```sql
SELECT Direction, COUNT(*) AS liczba
FROM dbo.In0E
WHERE Data >= CONVERT(varchar(10), DATEADD(DAY, -90, GETDATE()), 120)
GROUP BY Direction
ORDER BY liczba DESC;
```

### TODO #3 — Czy granice zmian (5-21) są twarde?

```sql
SELECT
    LEFT(Godzina, 2) AS hour,
    COUNT(*) AS liczba_wazen,
    DATENAME(WEEKDAY, Data) AS dzien_tyg
FROM dbo.In0E
WHERE Data >= CONVERT(varchar(10), DATEADD(DAY, -30, GETDATE()), 120)
  AND ActWeight > 0
GROUP BY LEFT(Godzina, 2), DATENAME(WEEKDAY, Data)
ORDER BY dzien_tyg, hour;
```

### TODO #4 — Tolerancje wagowe per towar

```sql
-- 1) Pełna struktura Article
SELECT TOP 1 * FROM dbo.Article WHERE ID = '40';

-- 2) Szukanie kolumn związanych z tolerancją
SELECT TABLE_NAME, COLUMN_NAME, DATA_TYPE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE COLUMN_NAME LIKE '%Toler%'
   OR COLUMN_NAME LIKE '%MinW%'
   OR COLUMN_NAME LIKE '%MaxW%';

-- 3) Empiryczna tolerancja (mediana abs(Roznica))
SELECT
    e.ArticleID, e.ArticleName,
    AVG(ABS(e.ActWeight - e.Weight)) AS sr_odchylenie,
    STDEV(e.ActWeight - e.Weight) AS odch_std,
    MAX(ABS(e.ActWeight - e.Weight)) AS max_odchylenie
FROM dbo.In0E e
WHERE e.Data >= CONVERT(varchar(10), DATEADD(DAY, -30, GETDATE()), 120)
  AND e.ActWeight > 0 AND e.Weight > 0
GROUP BY e.ArticleID, e.ArticleName
ORDER BY sr_odchylenie DESC;
```

### TODO #5 — Faktyczne indeksy w LibraNet

```sql
SELECT
    i.name AS index_name,
    OBJECT_NAME(i.object_id) AS table_name,
    STRING_AGG(c.name, ', ') WITHIN GROUP (ORDER BY ic.key_ordinal) AS columns
FROM sys.indexes i
JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
WHERE OBJECT_NAME(i.object_id) IN ('In0E', 'Out1A', 'Article', 'PartiaDostawca')
GROUP BY i.name, i.object_id
ORDER BY table_name, index_name;
```

### TODO #6 — Co tak naprawdę jest w `Out1A`?

```sql
SELECT TOP 5 * FROM dbo.Out1A ORDER BY Data DESC;

SELECT
    COUNT(*) AS total_out,
    SUM(CASE WHEN Related_IN IS NULL THEN 1 ELSE 0 END) AS bez_partii_in,
    SUM(CASE WHEN o.Related_IN IS NOT NULL AND e.P1 IS NULL THEN 1 ELSE 0 END) AS partia_in_nie_istnieje
FROM dbo.Out1A o
LEFT JOIN (SELECT DISTINCT P1 FROM dbo.In0E) e ON e.P1 = o.Related_IN
WHERE o.Data >= CONVERT(varchar(10), DATEADD(DAY, -30, GETDATE()), 120);
```

### TODO #7 — Triggery / procedury na `In0E`

```sql
SELECT
    o.name AS obj_name, o.type_desc, o.create_date
FROM sys.sql_modules m
JOIN sys.objects o ON o.object_id = m.object_id
WHERE m.definition LIKE '%In0E%'
   OR m.definition LIKE '%Out1A%'
   OR m.definition LIKE '%PartiaDostawca%'
ORDER BY o.create_date DESC;
```

### TODO #8 — Wielkość tabel

```sql
SELECT
    OBJECT_NAME(p.object_id) AS table_name,
    SUM(p.rows) AS row_count,
    SUM(a.total_pages) * 8 / 1024.0 AS total_mb,
    SUM(a.used_pages) * 8 / 1024.0 AS used_mb
FROM sys.partitions p
JOIN sys.allocation_units a ON a.container_id = p.partition_id
WHERE OBJECT_NAME(p.object_id) IN ('In0E', 'Out1A', 'Article', 'PartiaDostawca')
  AND p.index_id IN (0, 1)
GROUP BY p.object_id
ORDER BY total_mb DESC;
```

### TODO #9 — Operator zawsze na jednej wadze?

```sql
SELECT
    OperatorID, MIN(Wagowy) AS imie,
    SUM(CASE WHEN ArticleID = '40' THEN 1 ELSE 0 END) AS wazenia_palety_A,
    SUM(CASE WHEN ArticleID <> '40' THEN 1 ELSE 0 END) AS wazenia_porcji,
    COUNT(*) AS razem
FROM dbo.In0E
WHERE Data >= CONVERT(varchar(10), DATEADD(DAY, -30, GETDATE()), 120)
  AND ActWeight > 0
  AND OperatorID IS NOT NULL
GROUP BY OperatorID
HAVING COUNT(*) > 50
ORDER BY razem DESC;
```

**Sukces:** każdy operator ma 100% albo 0% w paletach — nie mieszankę.

### TODO #10 — Średnia cena dla "kosztu odchylenia" w PLN

Trzeba wziąć z `Out1A` (jeśli żyje) lub Symfonia 112 (sprzedaż). Wymaga osobnego ETL.

---

## 18. Konfiguracja połączenia

```
Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True
```

> ⚠ User `pronova` **NIE MA uprawnień CREATE DATABASE** na 109 → wszystkie nowe tabele dodawane do istniejącej `LibraNet`, nie nowych baz.

---

## Zobacz też

- [`13_Bazy_danych.md`](13_Bazy_danych.md) — ogólny opis 4 baz w firmie
- [`06_Hala_produkcja.md`](06_Hala_produkcja.md) — workflow uboju (skąd biorą się ważenia)
- [`04_Klienci_dostawcy.md`](04_Klienci_dostawcy.md) — hodowcy (`CustomerID`/`CustomerName`)
- [`17_Slownik_skrotow.md`](17_Slownik_skrotow.md) — klasy wagowe (5-12)
