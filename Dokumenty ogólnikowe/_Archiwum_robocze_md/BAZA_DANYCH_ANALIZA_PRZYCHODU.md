# Dokumentacja bazy danych — Analiza Przychodu Produkcji

> **Dla kogo:** Sergiusz Piórkowski (właściciel/programista) — przyszłe ja, plus każdy
> kto przejmie temat. Dokument bazuje na analizie SQL-i z `PrzychodService.cs` oraz na
> wiedzy biznesowej zebranej w sesji **2026-05-03**.
>
> **Po co:** żeby za 2 lata wiedzieć **co**, **gdzie** i **dlaczego** —
> bez konieczności rekonstruowania wiedzy od zera.

---

## SPIS TREŚCI

1. [Architektura — dwie bazy, dwa światy](#1-architektura)
2. [Słownik biznesowy](#2-slownik-biznesowy)
3. [Workflow produkcji (od kurczaka do faktury)](#3-workflow-produkcji)
4. [Konfiguracja połączenia (LibraNet 109)](#4-konfiguracja-polaczenia)
5. [Tabele wykorzystywane przez moduł](#5-tabele-wykorzystywane)
6. [`dbo.In0E` — ważenia przychodu](#6-in0e)
7. [`dbo.Article` — słownik towarów](#7-article)
8. [`dbo.PartiaDostawca` — partia → hodowca + DEKODER PARTII](#8-partia-dostawca)
9. [`dbo.Out1A` — sprzedaż (NIE UŻYWAMY w tym module)](#9-out1a)
10. [Schemat relacji (konceptualny)](#10-schemat-relacji)
11. [Klasy kurczaka — dedykowana sekcja](#11-klasy-kurczaka)
12. [Operatorzy](#12-operatorzy)
13. [Zmiany dzienna / nocna](#13-zmiany)
14. [Odchylenia wagowe i tolerancje](#14-odchylenia)
15. [Storno](#15-storno)
16. [Quirki i pułapki w danych](#16-quirki)
17. [Tabele powiązane (nieużywane przez moduł)](#17-tabele-powiazane)
18. [Inne bazy w ekosystemie firmy](#18-inne-bazy)
19. [Zapytania referencyjne](#19-zapytania-referencyjne)
20. [Indeksy i wydajność](#20-indeksy)
21. [TODO — co jeszcze trzeba zbadać](#21-todo)
22. [Linki do kodu](#22-linki-do-kodu)
23. [Historia zmian](#23-historia-zmian)

---

<a id="1-architektura"></a>
## 1. Architektura — dwie bazy, dwa światy

W firmie żyją **dwie odrębne bazy** o całkowicie różnym przeznaczeniu:

| Baza | Serwer | Co zawiera | Czyje to |
|---|---|---|---|
| **`LibraNet`** | `192.168.0.109` | **Przychód produkcji** — ważenia, partie, hodowcy, klasy, operatorzy | „Nasze" (Sergiusz, ZPSP, Pronova) |
| **`HM`** (Symfonia Handel) | `192.168.0.112` (zwany dalej **„112"**) | **Sprzedaż / handel** — faktury, kontrahenci, zamówienia, magazyn księgowy | Symfonia Handel (zewnętrzny ERP) |

**Kluczowy podział pracy:**

1. Kurczak żywy → **przyjęcie** + **ważenie** + **paletyzacja** w LibraNet (109)
2. Kurczak idzie na produkcję, robi się tuszki / porcje
3. Kierownik na koniec dnia liczy **ile zostało wyprodukowane**
4. **Przychód towaru** zostaje wprowadzony do **Symfonia Handel** (112)
5. Z Symfonii idzie sprzedaż klientom (faktury, WZ)

> 🟢 **Moduł "Analiza Przychodu Produkcji" pracuje WYŁĄCZNIE na LibraNet (109).**
> Nie czyta sprzedaży z 112. Tabela `Out1A` w LibraNet jest historyczną pozostałością —
> patrz [§9](#9-out1a).

---

<a id="2-slownik-biznesowy"></a>
## 2. Słownik biznesowy

Te pojęcia wracają wszędzie w danych — warto mieć je w jednym miejscu.

| Termin | Definicja |
|---|---|
| **Kurczak surowiec** | Kurczak **żywy** — przyjęty od hodowcy, jeszcze przed ubojem |
| **Tuszka** | Mięso z kurczaka po uboju — **cała tusza** (bez podziału na elementy) |
| **Kurczak Klasy A** | **Tuszka bez żadnych wad** (klasa jakościowa A, nie wielkościowa). W bazie reprezentowany jako `Article.ID = '40'` |
| **Klasa (wielkość)** | Numer 5–12 określający **rozmiar tuszki** (mniejszy numer = większy ptak). Patrz [§11](#11-klasy-kurczaka). Pole DB: `In0E.QntInCont` |
| **Hodowca** | Zewnętrzny dostawca surowca. **Wszyscy hodowcy są zewnętrzni — firma nie ma własnych ferm.** |
| **Partia** | Jeden transport / jedna dostawa surowca = jedna partia. Numer to konkatenacja `CustomerID` + `Partia` (patrz [§8](#8-partia-dostawca) — DEKODER) |
| **Mix partii** | Kiedy mięso z dwóch transportów się łączy → **tworzona jest nowa partia** (ten sam numer auta, ale inny ID hodowcy z przodu) |
| **Paleta** | Drewniana paleta na której układa się tuszki. Tara stała. |
| **Pojemnik E2** | Plastikowy pojemnik na 15 kg netto tuszki. Tara stała. |
| **Operator wagowy** | Pracownik fizycznie ważący — w danych jako `OperatorID` (stałe ID) i `Wagowy` (imię i nazwisko, denormalizowane) |
| **Storno** | Anulacja ważenia — w bazie jako `ActWeight < 0` |
| **Weight (standard)** | Waga deklarowana / nominalna z kartoteki towaru |
| **ActWeight (rzeczywista)** | Faktyczna waga pokazana przez wagę po ważeniu |
| **Dokładamy** | `ActWeight > Weight` — oddajemy klientowi nadmiar za darmo (strata firmy) |
| **Niedowaga** | `ActWeight < Weight` — klient dostaje za mało (ryzyko reklamacji) |

---

<a id="3-workflow-produkcji"></a>
## 3. Workflow produkcji — co generuje wpisy do In0E

Punkt po punkcie, jak rodzą się rekordy w bazie:

1. **Kierownik produkcji** wprowadza zaplanowane partie do systemu — **maks. 16 partii dostępnych w jednym dniu**
2. Auto z hodowcą podjeżdża, surowiec zostaje **przyjęty** — w `PartiaDostawca` powstaje rekord z numerem partii i hodowcą (`CustomerID` + `CustomerName`)
3. Kurczak idzie na linię, jest ubijany, robione są tuszki
4. **Tuszki ważone na wadze paletowej / pojemnikowej** — operator:
   - skanuje / wybiera **partię** (z max 16 dostępnych w danym dniu)
   - waga waży **netto** (sama tara palety / pojemnika E2 jest odejmowana)
   - **w programie wagowym** operator zaznacza, czy ważył paletę czy pojemnik (to decyduje która tara)
   - waga sama wstawia rekord do `dbo.In0E`
5. Powstaje rekord z: `Data`, `Godzina`, `OperatorID`, `Wagowy`, `ArticleID`, `ActWeight`, `Weight`, `Tara`, `P1`, `QntInCont` (klasa wielkości), `TermID`
6. Na koniec dnia kierownik liczy łączną produkcję i wprowadza **przychód towaru** do Symfonii Handel (na 112) — to już poza zakresem tego modułu

> ⚠ **Operator zaznaczanie pojemnik/paleta dzieje się W PROGRAMIE WAGOWYM**, nie w naszym
> module. Do bazy `In0E` trafia już sama waga **netto**. W kolumnie `Tara` jest odjęta wartość.

---

<a id="4-konfiguracja-polaczenia"></a>
## 4. Konfiguracja połączenia (LibraNet 109)

| Parametr | Wartość |
|---|---|
| Serwer | `192.168.0.109` |
| Baza danych | `LibraNet` |
| Użytkownik | `pronova` |
| Hasło | `pronova` |
| TrustServerCertificate | `True` |

Connection string zaszyty w `AnalizaPrzychoduWindow.xaml.cs`:

```
Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True
```

> ⚠ Użytkownik `pronova` **NIE MA uprawnień CREATE DATABASE** na 109 — dlatego wszystkie
> nowe tabele dodajemy do istniejącej bazy LibraNet, nie tworzymy osobnych.

---

<a id="5-tabele-wykorzystywane"></a>
## 5. Tabele wykorzystywane przez moduł

| Tabela | Rola w module | Czytamy / Piszemy |
|---|---|---|
| `dbo.In0E` | **Rdzeń modułu** — każdy rekord = jedno fizyczne ważenie | tylko READ |
| `dbo.Article` | Słownik towarów (ID, nazwa, skrót) | tylko READ |
| `dbo.PartiaDostawca` | Mapowanie: partia → hodowca | tylko READ |
| `dbo.Out1A` | **Nie używamy** — patrz [§9](#9-out1a) | — |

---

<a id="6-in0e"></a>
## 6. `dbo.In0E` — ważenia przychodu produkcji

**Najważniejsza tabela modułu.** Każdy wiersz = jedno fizyczne ważenie tuszki / palety
/ pojemnika na linii produkcyjnej. Zapis robi **program wagowy** (nie my — my tylko czytamy).

### 6.1 Kolumny używane przez moduł

| Kolumna | Typ | Opis | Quirki |
|---|---|---|---|
| `ArticleID` | varchar | Klucz do `Article.ID` (np. `'40'` = Kurczak Klasy A) | — |
| `ArticleName` | varchar | Nazwa towaru w momencie ważenia (denormalizacja — może być nieaktualna jeśli ktoś zmienił `Article.Name`) | „zamrożona w czasie" |
| `JM` | varchar | Jednostka miary (zawsze `kg`) | — |
| `TermID` | int | ID terminala / wagi | — |
| `TermType` | varchar | Nazwa / typ terminala (np. „Linia 1", „Paletyzator") | **TODO:** wymień co jest co — patrz [§21](#21-todo) |
| `Weight` | decimal | **Waga standardowa** z kartoteki towaru | — |
| `ActWeight` | decimal | **Waga rzeczywista netto** (już po odjęciu tary palety/pojemnika) | **Ujemna = storno** |
| `Quantity` | numeric | Ilość sztuk (rzadko != 1) | — |
| `Direction` | varchar | Kierunek dokumentu | **Nie używamy.** Trzeba zbadać czy są inne wartości niż IN |
| `Data` | date / varchar | Data ważenia | **TODO:** zbadać dokładny typ |
| `Godzina` | varchar | Godzina ważenia (`HH:mm:ss` jako TEKST, nie `time`) | Filtrowanie wymaga `TRY_CAST(LEFT(Godzina,2) AS INT)` |
| `OperatorID` | varchar | **Stałe ID** operatora — nigdy się nie zmienia | — |
| `Wagowy` | varchar | Imię i nazwisko operatora — **może się zmienić** historycznie (np. zmiana nazwiska / literówka). Nowe ważenia mają nową wartość, stare zachowują starą | — |
| `Tara` | decimal | Waga palety/pojemnika E2 odjęta z brutto. **Stała na typ opakowania** (paleta = jedna wartość, pojemnik E2 = inna) | — |
| `Price` | decimal | Cena jednostkowa | Rzadko używana w przychodzie |
| `P1` | varchar(15) | **Numer partii produkcyjnej / surowca** — klucz do `PartiaDostawca.Partia` | Patrz [§8](#8-partia-dostawca) |
| `P2` | varchar(15) | Druga partia. **W 99% `P2 = P1`.** Sergiusz nie wie po co historycznie była — moduł NIE czyta P2 | — |
| `QntInCont` | int | **Wielkość / klasa palety** (5–12 dla kurczaka) | Patrz [§11](#11-klasy-kurczaka) |

### 6.2 Workflow zapisu

Operator **NIE wpisuje danych ręcznie do bazy**. Procedura:
1. Operator skanuje / wybiera **partię** spośród maks. 16 dostępnych dziś (kierownik wprowadził)
2. Operator zaznacza w programie wagowym czy waży **paletę czy pojemnik E2**
3. Waga waży, program wagowy odejmuje tarę i zapisuje rekord

> Skoro to robi program zewnętrzny, **nie kontrolujemy formatu zapisów**. Stąd quirki:
> ujemne `ActWeight` (storno), pusty `P1`, `Klasa = 0` dla nie-kurczaków, itp.

### 6.3 Kluczowe wzorce użycia

**Storno / anulacja:**
```sql
ActWeight < 0   -- ważenie cofnięte przez operatora (anulacja)
ActWeight > 0   -- normalne ważenie
ActWeight = 0   -- śmieć / zaczęte i przerwane (rzadkość)
```

**Odchylenie wagowe (KPI biznesowy):**
```
Roznica         = ActWeight - Weight
RoznicaProc     = (ActWeight - Weight) / Weight * 100
Dokladamy       = Roznica > +0.05 kg   (dajemy klientowi za darmo — STRATA firmy)
Niedowaga       = Roznica < -0.05 kg   (klient dostaje za mało — RYZYKO reklamacji)
```

> ⚠ Tolerancja `0.05 kg` (50 g) w kodzie to **moja wartość arbitralna**.
> **W rzeczywistości tolerancje są różne dla różnych towarów** — patrz [§14](#14-odchylenia)
> i TODO w [§21](#21-todo).

**Klasy kurczaka:**
- `QntInCont` przyjmuje wartości 5–12 **tylko dla `ArticleID = '40'`**
- Dla innych towarów `QntInCont` jest najczęściej `0` (irrelewantne)

---

<a id="7-article"></a>
## 7. `dbo.Article` — słownik towarów

**Rola:** kanoniczna lista produktów. Używana, żeby pobrać pełną nazwę / skrót,
gdy `In0E.ArticleName` może być zdezaktualizowane.

### 7.1 Kolumny

| Kolumna | Typ | Opis |
|---|---|---|
| `ID` | varchar | Klucz główny artykułu (np. `'40'` = Kurczak A) |
| `Name` | varchar | Pełna nazwa towaru |
| `ShortName` | varchar | Skrót (np. „Filet B/S", „K. A 1500+") |

### 7.2 Wykorzystanie w module

```sql
SELECT ID, Name, ShortName
FROM dbo.Article
WHERE ID IS NOT NULL AND ID <> ''
  AND Name IS NOT NULL AND Name <> ''
ORDER BY Name
```

**W UI** combo „Towar" wyświetla `"ShortName - Name"`, ranking towarów używa `Name`.

### 7.3 Znane wartości specjalne

| ID | Znaczenie |
|---|---|
| `'40'` | **Kurczak Klasy A** — surowiec / tuszka bez wad. Jedyny artykuł z aktywną klasyfikacją wielkości (`QntInCont`) |
| inne | Produkty końcowe, porcjowane (filet, korpus, ćwiartki, podroby itd.) |

> **TODO:** zbadać czy Article ma kolumny `MinWeight`, `MaxWeight`, `Tolerance`,
> `WeightStandard` — to klucz do liczenia tolerancji per towar. Patrz [§14](#14-odchylenia).

---

<a id="8-partia-dostawca"></a>
## 8. `dbo.PartiaDostawca` — partia → hodowca + DEKODER PARTII

**Rola:** każda partia surowca (`In0E.P1`) ma przypisanego hodowcę. Tu trzymamy to powiązanie.

### 8.1 Kolumny (rzeczywista struktura)

```sql
SELECT TOP 10
    [guid], [Partia], [CustomerID], [CustomerName],
    [CreateData], [CreateGodzina],
    [ModificationData], [ModificationGodzina]
FROM [LibraNet].[dbo].[PartiaDostawca]
ORDER BY CreateData DESC
```

| Kolumna | Typ | Opis |
|---|---|---|
| `guid` | uniqueidentifier | UUID rekordu (PK techniczny) |
| `Partia` | varchar(15) | **Numer partii** (8 cyfr — patrz dekoder niżej) |
| `CustomerID` | varchar(3) | **ID hodowcy** (3 cyfry) |
| `CustomerName` | varchar | Imię i nazwisko hodowcy (np. „Sujecka Barbara") |
| `CreateData` | date | Data utworzenia rekordu (= data przyjęcia) |
| `CreateGodzina` | varchar | Godzina utworzenia |
| `ModificationData` | date | Data modyfikacji (puste = nie modyfikowano) |
| `ModificationGodzina` | varchar | Godzina modyfikacji |

### 8.2 🔑 DEKODER NUMERU PARTII

**Pełna partia w nomenklaturze firmowej** to konkatenacja:

```
[CustomerID] + [Partia]  =  [3 cyfry hodowcy] + [8 cyfr partii]
```

**Kolumna `Partia` rozkłada się na 3 segmenty:**

```
26  119  001
RR   DDD   AAA
└── rok (2 cyfry)
    └── dzień w roku (3 cyfry, 001–366)
        └── numer auta (3 cyfry, kolejny w danym dniu)
```

### 8.3 Przykład pełnego dekodowania

Z rzeczywistych danych:

| guid (skrót) | CustomerID | Partia | CustomerName | CreateData |
|---|---|---|---|---|
| 65D931CD… | **390** | **26119004** | Szymczak Dariusz | 2026-04-29 |

Rozkład:
- **CustomerID = 390** → hodowca o ID 390 = **Szymczak Dariusz**
- **Partia = 26119004**
  - **`26`** = rok 20**26**
  - **`119`** = **119. dzień roku 2026** = **29 kwietnia** ✓ (zgadza się z `CreateData`)
  - **`004`** = **4. auto** od tego hodowcy w tym dniu

**Pełna nomenklatura firmowa:** `390-26119004` lub `39026119004` (kontekstowo)

### 8.4 Konsekwencje formatu

- **Kolumna `In0E.P1` przechowuje samo `Partia` (8 cyfr)** — bez `CustomerID`. Aby ustalić
  hodowcę trzeba zrobić JOIN z `PartiaDostawca`.
- **Ten sam `Partia` może wystąpić u różnych hodowców** w tym samym dniu (różne ID, ten sam
  numer auta) — dlatego JOIN MUSI być po `Partia` + `CustomerID`, nie tylko po `Partia`.
  Faktycznie jednak `In0E.P1` jest unikalny w bazie (każda partia = jeden hodowca).

### 8.5 Mix partii — co się dzieje

> Kiedy mięso z dwóch transportów się **miesza** w produkcji, tworzona jest **nowa partia**.
> Numer auta zostaje ten sam (np. `001`), ale **z przodu jest inny ID hodowcy** —
> co tworzy nową, oddzielną partię w `PartiaDostawca`.

### 8.6 Hodowcy zewnętrzni

> **Wszyscy hodowcy są zewnętrzni** — firma **nie ma własnych ferm**.
> Nie ma rozróżnienia „własny vs zewnętrzny" w danych.

### 8.7 Ten sam dostawca pod różnymi `CustomerID`

> ⚠ Tak — to się zdarza. Np. ferma + brat tego samego hodowcy mogą mieć osobne `CustomerID`,
> ale realnie to ta sama działalność. Przy raportowaniu do dyrektora warto agregować po
> `CustomerName` z normalizacją (lower-case, trim).

### 8.8 Zapytania referencyjne

**Filtr po dostawcy** — subquery (One-to-One):
```sql
WHERE In0E.P1 IN (
    SELECT pd.Partia
    FROM dbo.PartiaDostawca pd
    WHERE pd.CustomerID = @Dostawca OR pd.CustomerName = @Dostawca
)
```

**Mapa partia→dostawca dla okresu** (cachowana w pamięci):
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

<a id="9-out1a"></a>
## 9. `dbo.Out1A` — sprzedaż (NIE UŻYWAMY w tym module)

> ⚠ **WAŻNE:** Tabela `Out1A` istnieje w LibraNet, ale Sergiusz **nie tworzył jej i nie wie do czego
> dokładnie służy**. **Sprzedaż firmy jest w Symfonia Handel na serwerze 112**, nie tu.
>
> Service `LoadSalesAsync` w kodzie został napisany historycznie, ale **zakładka Sprzedaż
> została usunięta z UI na życzenie użytkownika**. Service jest gotowy gdyby kiedyś okazało się
> przydatny, ale **nie polegaj na tych danych** dopóki nie zostanie zbadane co tu siedzi.

### Kolumny widoczne w kodzie (do sprawdzenia)

| Kolumna | Co prawdopodobnie znaczy | Status |
|---|---|---|
| `ArticleID`, `ArticleName` | Towar | Standard |
| `CustomerID` | Klient — ale jakiej bazy? Nie wiemy | **TODO** |
| `Data`, `Godzina` | Data wydania | Standard |
| `Weight`, `ActWeight`, `Price` | Wagi i cena | Prawdopodobnie OK |
| `P1` | Partia produktu (out) | **TODO** |
| `Related_IN` | Prawdopodobnie partia surowca (link do `In0E.P1`) | **TODO** zweryfikować |
| `DocNo`, `OrderNo` | Numer dokumentu / zamówienia | **TODO** czy każde Out1A ma OrderNo |

### Co warto zbadać (TODO)

- Kto i kiedy zapisuje do tej tabeli?
- Czy to jest spójne z Symfonia Handel na 112, czy żyje własnym życiem?
- Czy `Related_IN` rzeczywiście wskazuje na `In0E.P1` w 100% przypadków?
- Czy to jest jakaś replikacja / kopia / archiwum?

---

<a id="10-schemat-relacji"></a>
## 10. Schemat relacji (konceptualny)

```
                ┌────────────────────────┐
                │      dbo.Article       │
                │  ID (PK)               │
                │  Name, ShortName       │
                └───────────┬────────────┘
                            │ ArticleID
                            │
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

`Out1A` celowo pominięty — patrz [§9](#9-out1a).

---

<a id="11-klasy-kurczaka"></a>
## 11. Klasy kurczaka — dedykowana sekcja

### 11.1 Co to jest klasa wielkościowa

> **Klasa = ile sztuk tuszek mieści się w pojemniku E2 (15 kg netto).**
>
> **Mniejszy numer = większy ptak.**

Wzór: `średnia waga tuszki ≈ 15 kg / numer_klasy`

| Klasa | Sztuk w pojemniku (15 kg) | Średnia waga tuszki (≈) | Komentarz |
|---|---|---|---|
| **5** | 5 szt | ≈ 3,0 kg | Bardzo duży kurczak |
| **6** | 6 szt | ≈ 2,5 kg | **Idealna klasa** — duży, ale nie za bardzo |
| **7** | 7 szt | ≈ 2,1 kg | **Idealna klasa** |
| **8** | 8 szt | ≈ 1,9 kg | Średni |
| **9** | 9 szt | ≈ 1,7 kg | Średni |
| **10** | 10 szt | ≈ 1,5 kg | Mniejszy |
| **11** | 11 szt | ≈ 1,4 kg | Mały |
| **12** | 12 szt | ≈ 1,25 kg | Najmniejszy w użyciu |

### 11.2 Idealna klasa

> **Klasa 6–7 jest preferowana** w produkcji.

Brak normy minimalnej / maksymalnej w bazie — „co przyjdzie z hodowcy, to przyjdzie".
**Statystyki klas są więc deskryptywne, nie preskryptywne.**

### 11.3 Klasa 0 — co to znaczy

> `QntInCont = 0` to **najczęściej zapomnienie wpisania klasy przez operatora** lub **mix
> klas w pojemniku** (więcej niż jeden rozmiar tuszki).

W modułach analitycznych klasę 0 traktujemy jako odrębną kategorię „brak klasy" /
„mix" — nie usuwamy z analiz, ale flagujemy jako anomalię operatora.

### 11.4 Pole DB

- Kolumna: `In0E.QntInCont`
- Typ: `int`
- Wartości w użyciu: **5–12** (i `0` jako "brak/mix")
- **Tylko dla `ArticleID = '40'`** (Kurczak A) — dla innych towarów ignorujemy

---

<a id="12-operatorzy"></a>
## 12. Operatorzy

### 12.1 Aktywni operatorzy

> **Aktywnych jednocześnie na wagach jest 3–5 osób.**
>
> Lista wszystkich w bazie (kiedykolwiek aktywnych) jest dłuższa — moduł filtruje po
> aktywności w ostatnich 90 dniach.

### 12.2 Stabilność identyfikatora

| Pole | Stabilność |
|---|---|
| `OperatorID` | **STAŁE** — nigdy się nie zmienia, klucz historyczny |
| `Wagowy` | **MOŻE SIĘ ZMIENIĆ** historycznie (np. ktoś zmieni nazwisko, literówkę poprawi). Nowe ważenia będą miały nową wartość, **stare zachowają starą** |

> 💡 **Konsekwencja:** Jeśli grupujesz ranking po imieniu (`Wagowy`), możesz mieć dwa
> wpisy dla tej samej osoby gdy ktoś poprawił literówkę. **Grupuj po `OperatorID`.**

### 12.3 Klasyfikacja Paletujący / Porcjujący

Sergiusz: *„ArticleID=40 zawsze będzie robiony na wadze paletowej tylko i wyłącznie."*

Stąd heurystyka w module:
```csharp
Paletuje = g.Count(r => r.ArticleID == "40") > g.Count() / 2
```

| Typ | Definicja | W praktyce |
|---|---|---|
| **Paletujący** | >50% ważeń to ArticleID=40 (Kurczak A) | Pracownik na wadze paletowej |
| **Porcjujący** | ≤50% ważeń to ArticleID=40 | Pracownik na wadze pojemnikowej (filet, korpus, itd.) |

> ⚠ Ta heurystyka jest **probabilistyczna**. Czy dany operator w 100% przypadków
> obsługuje tylko jedną wagę — **Sergiusz nie wie na pewno**. W razie wątpliwości
> [zobacz TODO](#21-todo) — można policzyć rozkład SQL-em.

### 12.4 Zapytanie referencyjne

```sql
SELECT DISTINCT OperatorID, Wagowy
FROM dbo.In0E
WHERE OperatorID IS NOT NULL
  AND Wagowy IS NOT NULL AND Wagowy <> ''
  AND Data >= CONVERT(varchar(10), DATEADD(DAY, -90, GETDATE()), 120)
ORDER BY Wagowy
```

---

<a id="13-zmiany"></a>
## 13. Zmiany dzienna / nocna

### 13.1 Granice używane w module

| Zmiana | Godziny | Stała w kodzie |
|---|---|---|
| **Dzienna** | 5:00 – 21:00 | `DAY_SHIFT_START = 5` |
| **Nocna** | 21:00 – 5:00 | `NIGHT_SHIFT_START = 21` |

### 13.2 Status: do weryfikacji

> ❓ **Sergiusz nie pamięta czy te granice są twarde (stałe), czy są wyjątki**
> (krótszy piątek, weekend, dni przedświąteczne).
>
> **Można zweryfikować SQL-em** — patrz [§21 TODO #1](#21-todo).

---

<a id="14-odchylenia"></a>
## 14. Odchylenia wagowe i tolerancje

### 14.1 Definicje

```
Roznica       = ActWeight - Weight                        [kg]
RoznicaProc   = (ActWeight - Weight) / Weight * 100       [%]
Dokladamy     = Roznica > +tolerancja                     [bool]
Niedowaga     = Roznica < -tolerancja                     [bool]
```

### 14.2 Tolerancja w obecnym kodzie

W kodzie (`PrzychodModels.cs`):
```csharp
public bool Dokladamy => Roznica > 0.05m;     // tolerancja 50 g
public bool Niedowaga => Roznica < -0.05m;
```

> ⚠ **TO JEST WARTOŚĆ ARBITRALNA** dodana przez Claude. Sergiusz potwierdził:
> **„Są tolerancje różne na towar. Możemy znaleźć za pomocą SELECT."**

### 14.3 TODO — znaleźć tolerancje per towar

Trzeba zbadać tabelę `Article` — może mieć kolumny w stylu:
- `MinWeight`, `MaxWeight`
- `Tolerance`, `WeightStandard`
- `WeightMin`, `WeightMax`
- albo osobną tabelę `ArticleTolerance`

**Sugerowany SQL eksploracyjny:**
```sql
-- Wszystkie kolumny Article — zobacz co jest dostępne
SELECT TOP 5 *
FROM [LibraNet].[INFORMATION_SCHEMA].[COLUMNS]
WHERE TABLE_NAME = 'Article';

-- Albo SELECT TOP 5 * FROM Article — wszystkie kolumny dla 1 wiersza
SELECT TOP 5 *
FROM [LibraNet].[dbo].[Article]
WHERE ID = '40';

-- Szukanie kolumn związanych z tolerancją po nazwie
SELECT TABLE_NAME, COLUMN_NAME, DATA_TYPE
FROM [LibraNet].[INFORMATION_SCHEMA].[COLUMNS]
WHERE COLUMN_NAME LIKE '%Toler%'
   OR COLUMN_NAME LIKE '%MinWeight%'
   OR COLUMN_NAME LIKE '%MaxWeight%'
   OR COLUMN_NAME LIKE '%WeightStd%';
```

Po znalezieniu należy:
1. Zaktualizować `PrzychodRecord.Dokladamy` / `Niedowaga` żeby brały tolerancję z `Article`
2. Wpisać w tym dokumencie listę towarów z ich tolerancjami

### 14.4 Skutki biznesowe

| Sytuacja | Strona | Komentarz Sergiusza |
|---|---|---|
| Dokładamy (rzeczywista > standard) | **Strata firmy** — oddajemy klientowi za darmo | „Nie wiem czy ktoś rozlicza operatora gdy systematycznie dokłada" |
| Niedowaga (rzeczywista < standard) | **Ryzyko klienta** — może być reklamacja | „Nie wiem jak często sprawdzamy" |

> **Średni koszt dokładania w skali miesiąca: brak danych.**
> Do wyliczenia kosztu w PLN potrzeba ceny — patrz [§21 TODO](#21-todo).

---

<a id="15-storno"></a>
## 15. Storno

### 15.1 Definicja

> **Storno = ważenie cofnięte przez operatora** = `ActWeight < 0`.

### 15.2 Konsekwencje przy agregacji

| Zliczanie | Jak liczyć | Powód |
|---|---|---|
| **Suma kg** | `SUM(ActWeight)` | Dodatnie i ujemne się kompensują (storno cofa pierwotne ważenie) |
| **Liczba ważeń** | `COUNT(ActWeight > 0)` | Inaczej zliczasz podwójnie |
| **Liczba anulacji** | `COUNT(ActWeight < 0)` | Sygnał jakości operatora |

### 15.3 Co znaczy dużo storno

- Wysoki % storno u jednego operatora → możliwa nieuwaga / pomyłki / problem z wagą
- >5% storno na partii → potencjalna anomalia (np. zła klasyfikacja)
- Storno po godzinach pracy → podejrzane (ktoś manipuluje wieczorem?)

---

<a id="16-quirki"></a>
## 16. Quirki i pułapki w danych

### 16.1 P1 = P2
W ~99% rekordów `P2 = P1`. Sergiusz **nie wie do czego historycznie służyło `P2`**.
Moduł NIE czyta P2.

### 16.2 Zombie partia z 2014
W bazie istnieją rekordy z partią z 2014 roku, które „zaśmiecają" zestawienia.
Stała `MinPartiaCreateData = "2024-01-01"` (możliwa migracja danych w styczniu 2024).
W praktyce nie jest stosowana jako WHERE w kwerendach — wszystkie zapytania mają
zakres dat z DatePicker.

### 16.3 `Godzina` jako tekst
`Godzina` jest stringiem `HH:mm:ss`, nie kolumną typu `time`. Filtrowanie godzinowe
wymaga:
```sql
TRY_CAST(LEFT(Godzina,2) AS INT) >= @GodzOd
TRY_CAST(LEFT(Godzina,2) AS INT) <= @GodzDo
```

### 16.4 `IsClose` to martwe pole
W tabelach partii istnieje historyczna flaga `IsClose` — **NIE JEST DZIŚ AKTUALIZOWANA**.
Status partii śledzimy w nowej tabeli `PartiaStatus` (moduł Partie V2).

### 16.5 ArticleName w In0E zamrożone w czasie
`In0E.ArticleName` to **kopia w momencie ważenia**. Jeśli ktoś zmieni nazwę w
`Article`, historyczne ważenia nadal będą miały starą.

```csharp
// Dlatego ranking nadpisuje:
if (_articleDict.TryGetValue(g.Key, out var info))
    articleName = info.Name;
```

### 16.6 `(brak partii)` w danych
Pojedyncze rekordy w `In0E` mają puste / `NULL` `P1`. Sergiusz: **„Prawdopodobnie błąd."**
Moduł grupuje takie rekordy pod etykietą `(brak partii)`.

### 16.7 Operator może być w obu kategoriach
Choć Sergiusz uważa, że ArticleID=40 zawsze leci na wadze paletowej, **na poziomie
danych** może się zdarzyć, że operator ma rekordy obu typów. Heurystyka >50%
rozstrzyga dwuznaczność.

### 16.8 Ten sam dostawca pod różnymi `CustomerID`
Patrz [§8.7](#8-partia-dostawca). Trzeba uważać przy raportowaniu agregowanym.

---

<a id="17-tabele-powiazane"></a>
## 17. Tabele powiązane (nieużywane przez moduł)

Te tabele są w LibraNet i powiązane z partiami / produkcją, ale moduł
„Analiza Przychodu" ich **nie czyta**. Wymienione tu tylko dla kontekstu —
korzystają z nich inne moduły (Partie V2, Hodowcy, Transport).

| Tabela | Zawartość | Moduł |
|---|---|---|
| `dbo.listapartii` | Lista partii (dane planistyczne, statusy V2). `Partia` (varchar 15) — łączy się z `In0E.P1`. Kolumny: `StatusV2`, `HarmonogramLp`, `IsClose` (martwa), `CreateData` | Partie V2 |
| `dbo.PartiaStatus` | Historia statusów partii (10-stanowy lifecycle: PLANNED → IN_TRANSIT → AT_RAMP → VET_CHECK → APPROVED → IN_PRODUCTION → PROD_DONE → CLOSED → CLOSED_INCOMPLETE → REJECTED) | Partie V2 |
| `dbo.PartiaAuditLog` | Audyt zmian na partiach | Partie V2 |
| `dbo.HarmonogramDostaw` | Plan przyjęć surowca (kierowcy, hodowcy, godziny) | Transport / Partie V2 |
| `dbo.Pozyskiwanie_Hodowcy` | CRM dla hodowców (1874 rekordów importowanych z Excela) | Pozyskiwanie Hodowców |
| `dbo.Pozyskiwanie_Aktywnosci` | Kontakty z hodowcami (CRM-like) | Pozyskiwanie Hodowców |
| `dbo.QC_Normy` | Konfigurowalne normy jakościowe partii | Partie V2 |
| `dbo.Haccp` | Pomiary HACCP (temperatura, pH, itd.) | Partie V2 |
| `dbo.FarmerCalc` | Rozliczenia z hodowcami | (zewn.) |
| `dbo.DriverDetails` | Szczegóły kierowców (1:1 z `Driver`) | Flota |
| `dbo.VehicleDetails` | Szczegóły pojazdów (1:1 z `CarTrailer`) | Flota |
| `dbo.DriverVehicleAssignment` | Przypisania kierowca↔pojazd | Flota |
| `dbo.Article` (rozszerzenia) | `ArticleAuditLog`, `ArticleFavorites` | Kartoteka Towarów |

---

<a id="18-inne-bazy"></a>
## 18. Inne bazy w ekosystemie firmy

| Baza | Serwer | Co zawiera | Kto używa |
|---|---|---|---|
| **`UNISYSTEM`** | 192.168.0.23\SQLEXPRESS | UNICARD RCP — rejestracja czasu pracy | Kontrola Godzin, Wnioski Urlopowe |
| **`ZPSP`** | 192.168.0.23\SQLEXPRESS | Niestandardowe tabele HR (HR_*) | Kontrola Godzin |
| **`LibraNet`** | **192.168.0.109** | **Przychód, partie, hodowcy, flota, transport, towary** | **Ten moduł** + 5 innych |
| **`HM`** (Symfonia Handel) | **192.168.0.112** | **Sprzedaż, faktury, zamówienia, kontrahenci** | Symfonia Handel (zewn. ERP) |
| `Handel` | (separate) | Kontrahenci (klienci, dostawcy) | Faktury, Transport |
| `TransportPL` | (separate) | Główne dane transportu (kierowcy, pojazdy, kursy, ładunki) | Transport |

> 💡 **Pamiętaj:** **109 = NASZE / produkcja / przychód**, **112 = Symfonia / sprzedaż**.

---

<a id="19-zapytania-referencyjne"></a>
## 19. Zapytania referencyjne (skopiowane z `PrzychodService.cs`)

### 19.1 Główne ważenia (filtry dynamiczne)

```sql
SELECT
    e.ArticleID, e.ArticleName, e.JM, e.TermID, e.TermType,
    e.Weight, e.Quantity, e.Direction,
    e.Data, e.Godzina, e.OperatorID, e.Wagowy,
    e.Tara, e.Price, e.P1, e.P2, e.ActWeight, e.QntInCont
FROM dbo.In0E e
WHERE e.Data >= @DataOd AND e.Data <= @DataDo
  AND ISNULL(e.ArticleName,'') <> ''
  -- + opcjonalne: ArticleID, OperatorID, TermID, P1, QntInCont, Godzina, Dostawca
ORDER BY e.Data, e.Godzina
```

### 19.2 Słownik towarów

```sql
SELECT ID, Name, ShortName
FROM dbo.Article
WHERE ID IS NOT NULL AND ID <> ''
  AND Name IS NOT NULL AND Name <> ''
ORDER BY Name
```

### 19.3 Lista operatorów (ostatnie 90 dni)

```sql
SELECT DISTINCT OperatorID, Wagowy
FROM dbo.In0E
WHERE OperatorID IS NOT NULL
  AND Wagowy IS NOT NULL AND Wagowy <> ''
  AND Data >= CONVERT(varchar(10), DATEADD(DAY, -90, GETDATE()), 120)
ORDER BY Wagowy
```

### 19.4 Lista terminali

```sql
SELECT DISTINCT TermID, TermType
FROM dbo.In0E
WHERE TermID IS NOT NULL
ORDER BY TermID
```

### 19.5 TOP 200 partii (aktywnych w ostatnich 60 dniach)

```sql
SELECT DISTINCT TOP 200 P1
FROM dbo.In0E
WHERE P1 IS NOT NULL AND P1 <> ''
  AND Data >= CONVERT(varchar(10), DATEADD(DAY, -60, GETDATE()), 120)
ORDER BY P1 DESC
```

### 19.6 Lista hodowców (z aktywnych partii)

```sql
SELECT DISTINCT pd.CustomerID, pd.CustomerName
FROM dbo.PartiaDostawca pd
WHERE pd.CustomerName IS NOT NULL AND pd.CustomerName <> ''
  AND pd.Partia IN (
      SELECT DISTINCT P1
      FROM dbo.In0E
      WHERE P1 IS NOT NULL AND P1 <> ''
        AND Data >= CONVERT(varchar(10), DATEADD(DAY, -90, GETDATE()), 120)
  )
ORDER BY pd.CustomerName
```

### 19.7 Mapa partia → hodowca dla zadanego okresu

```sql
SELECT pd.Partia, pd.CustomerID, pd.CustomerName
FROM dbo.PartiaDostawca pd
WHERE pd.Partia IN (
    SELECT DISTINCT P1
    FROM dbo.In0E
    WHERE P1 IS NOT NULL AND P1 <> ''
      AND Data >= @DataOd AND Data <= @DataDo
)
```

### 19.8 Dekoder partii — jednoliniowiec

```sql
-- Dla rekordu In0E pokaż partię w czytelnej formie
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

---

<a id="20-indeksy"></a>
## 20. Indeksy i wydajność

W tej chwili moduł nie używa żadnych specjalnych hintów. Dla zakresów dat
> 30 dni zapytania działają szybko dzięki:
- Filtr na `Data` (powinien istnieć indeks na `In0E.Data`)
- TOP 200 + DISTINCT na partiach
- Subquery na partii zamiast JOIN-a (mniej pracy SQL gdy filtr po dostawcy pusty)

**Sugerowane indeksy (jeśli wystąpią problemy):**
```sql
CREATE INDEX IX_In0E_Data_ArticleID    ON dbo.In0E (Data, ArticleID);
CREATE INDEX IX_In0E_P1                ON dbo.In0E (P1);
CREATE INDEX IX_In0E_Data_OperatorID   ON dbo.In0E (Data, OperatorID);
CREATE INDEX IX_PartiaDostawca_Partia  ON dbo.PartiaDostawca (Partia);
```

> **TODO:** zweryfikować jakie indeksy faktycznie są — patrz [§21 TODO #5](#21-todo).

---

<a id="21-todo"></a>
## 21. TODO — co jeszcze trzeba zbadać

Lista konkretnych pytań, które pozostały bez odpowiedzi. Przy każdym sugerowany
SQL eksploracyjny.

### TODO #1 — Co to jest `TermType` i jakie są terminale?

```sql
-- Zobacz jakie typy + ile ważeń każdy ma w ostatnim miesiącu
SELECT
    TermID, TermType,
    COUNT(*)                           AS liczba_wazen,
    SUM(CASE WHEN ActWeight > 0 THEN ActWeight ELSE 0 END) AS suma_kg,
    MIN(Data) AS pierwsza, MAX(Data)   AS ostatnia
FROM dbo.In0E
WHERE Data >= CONVERT(varchar(10), DATEADD(DAY, -30, GETDATE()), 120)
GROUP BY TermID, TermType
ORDER BY suma_kg DESC;
```

**Co chcemy wiedzieć:** mapowanie `TermID → fizyczne stanowisko` (paletyzator, linia 1, linia 2…).

### TODO #2 — Co znaczy `Direction` i jakie ma wartości?

```sql
SELECT Direction, COUNT(*) AS liczba
FROM dbo.In0E
WHERE Data >= CONVERT(varchar(10), DATEADD(DAY, -90, GETDATE()), 120)
GROUP BY Direction
ORDER BY liczba DESC;
```

### TODO #3 — Czy granice zmian (5–21 / 21–5) są twarde?

```sql
-- Histogram ważeń po godzinie — które godziny są martwe / aktywne
SELECT
    LEFT(Godzina, 2) AS hour,
    COUNT(*)         AS liczba_wazen,
    DATENAME(WEEKDAY, Data) AS dzien_tyg
FROM dbo.In0E
WHERE Data >= CONVERT(varchar(10), DATEADD(DAY, -30, GETDATE()), 120)
  AND ActWeight > 0
GROUP BY LEFT(Godzina, 2), DATENAME(WEEKDAY, Data)
ORDER BY dzien_tyg, hour;
```

**Co szukamy:** czy weekendy / piątek / dni przedświąteczne mają inne aktywne godziny.

### TODO #4 — Tolerancje wagowe per towar

```sql
-- 1) Pełna struktura Article
SELECT TOP 1 * FROM dbo.Article WHERE ID = '40';

-- 2) Szukanie kolumn związanych z tolerancją
SELECT TABLE_NAME, COLUMN_NAME, DATA_TYPE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE COLUMN_NAME LIKE '%Toler%'
   OR COLUMN_NAME LIKE '%MinW%'
   OR COLUMN_NAME LIKE '%MaxW%'
   OR COLUMN_NAME LIKE '%Standard%';

-- 3) Empiryczna tolerancja per towar (mediana abs(Roznica) z ostatniego miesiąca)
SELECT
    e.ArticleID, e.ArticleName,
    AVG(ABS(e.ActWeight - e.Weight))                AS sr_odchylenie,
    STDEV(e.ActWeight - e.Weight)                   AS odch_std,
    MAX(ABS(e.ActWeight - e.Weight))                AS max_odchylenie
FROM dbo.In0E e
WHERE e.Data >= CONVERT(varchar(10), DATEADD(DAY, -30, GETDATE()), 120)
  AND e.ActWeight > 0 AND e.Weight > 0
GROUP BY e.ArticleID, e.ArticleName
ORDER BY sr_odchylenie DESC;
```

### TODO #5 — Indeksy faktycznie istniejące w LibraNet

```sql
SELECT
    i.name              AS index_name,
    OBJECT_NAME(i.object_id) AS table_name,
    STRING_AGG(c.name, ', ') WITHIN GROUP (ORDER BY ic.key_ordinal) AS columns
FROM sys.indexes i
JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
JOIN sys.columns c        ON c.object_id = ic.object_id AND c.column_id = ic.column_id
WHERE OBJECT_NAME(i.object_id) IN ('In0E', 'Out1A', 'Article', 'PartiaDostawca')
GROUP BY i.name, i.object_id
ORDER BY table_name, index_name;
```

### TODO #6 — Co tak naprawdę jest w `Out1A`?

```sql
-- Kto zapisuje (audit jeśli jest)
SELECT TOP 5 * FROM dbo.Out1A ORDER BY Data DESC;

-- Czy Related_IN zawsze wskazuje na istniejący In0E.P1?
SELECT
    COUNT(*)                                              AS total_out,
    SUM(CASE WHEN Related_IN IS NULL THEN 1 ELSE 0 END)   AS bez_partii_in,
    SUM(CASE WHEN o.Related_IN IS NOT NULL
              AND e.P1 IS NULL THEN 1 ELSE 0 END)         AS partia_in_nie_istnieje
FROM dbo.Out1A o
LEFT JOIN (SELECT DISTINCT P1 FROM dbo.In0E) e ON e.P1 = o.Related_IN
WHERE o.Data >= CONVERT(varchar(10), DATEADD(DAY, -30, GETDATE()), 120);
```

### TODO #7 — Triggery / procedury na In0E

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

### TODO #8 — Wielkość bazy

```sql
SELECT
    OBJECT_NAME(p.object_id)            AS table_name,
    SUM(p.rows)                         AS row_count,
    SUM(a.total_pages) * 8 / 1024.0     AS total_mb,
    SUM(a.used_pages) * 8 / 1024.0      AS used_mb
FROM sys.partitions p
JOIN sys.allocation_units a ON a.container_id = p.partition_id
WHERE OBJECT_NAME(p.object_id) IN ('In0E', 'Out1A', 'Article', 'PartiaDostawca')
  AND p.index_id IN (0, 1)
GROUP BY p.object_id
ORDER BY total_mb DESC;
```

### TODO #9 — Operator zawsze na jednej wadze?

```sql
-- Dla każdego operatora pokaż rozkład typu ważeń
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

**Sukces = każdy operator ma 100% albo 0% w paletach, a nie mieszankę.**

### TODO #10 — Średnia cena dla liczenia "kosztu odchylenia" w PLN

Trzeba wziąć z `Out1A` (jeśli żyje) lub z Symfonia 112 (sprzedaż).
**Wymaga połączenia do 112** — zaplanować osobno.

---

<a id="22-linki-do-kodu"></a>
## 22. Linki do kodu

| Plik | Co zawiera |
|---|---|
| `AnalizaPrzychoduProdukcji/Services/PrzychodService.cs` | Wszystkie zapytania SQL (jedyne źródło prawdy) |
| `AnalizaPrzychoduProdukcji/Models/PrzychodModels.cs` | Modele DTO odpowiadające kolumnom |
| `AnalizaPrzychoduProdukcji/AnalizaPrzychoduWindow.xaml.cs` | Logika UI, agregacje w pamięci, drill-down, LIVE, skróty |
| `AnalizaPrzychoduProdukcji/AnalizaPrzychoduWindow.xaml` | Wygląd okna (6 zakładek, 5 kart KPI, Health Strip) |
| `AnalizaPrzychoduProdukcji/ViewModels/AnalizaPrzychoduViewModel.cs` | Bindings dla LiveCharts (etykiety, wartości, formattery) |

---

<a id="23-historia-zmian"></a>
## 23. Historia zmian

| Data | Autor | Zmiana |
|---|---|---|
| 2026-05-03 | Claude (Sergiusz) | Pierwsza wersja — analiza SELECT-ów + struktura `PrzychodService.cs` |
| 2026-05-03 | Claude + Sergiusz Q&A | **Wersja rozbudowana**: dekoder partii (CustomerID + Partia → hodowca/rok/dzień/auto), słownik biznesowy, klasy kurczaka 5–12 (idealna 6–7), workflow produkcji, podział 109 LibraNet (przychód) vs 112 Symfonia (sprzedaż), Out1A → "nie używamy", lista 10 TODO z eksploracyjnymi SQL |
