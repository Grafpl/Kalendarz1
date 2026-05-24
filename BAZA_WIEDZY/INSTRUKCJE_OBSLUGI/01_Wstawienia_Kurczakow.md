# Instrukcja: Okno "Wstawienia Kurczaków" — cykl po cyklu (deep)

> **Dla kogo**: każdy kto rejestruje cykl wstawienia (Justyna, Maja, Sergiusz).
> **Co robi okno**: rejestrujesz cykl (jeden wsad piskląt u jednego hodowcy) + planujesz dostawy do uboju + opcjonalnie serię kilku wstawień + opcjonalnie podgląd kalendarza pojemności.
> **Plik kodu**: `Zywiec/WstawieniaKurczaka/WstawienieWindow.xaml(.cs)` (~2810 linii).
> **Otwierane z**: listy wstawień (`02_Lista_Wstawien.md`) lub innych okien (np. Kalendarz dostaw, Karta hodowcy).

---

## 1. Po co istnieje to okno (jeszcze prościej niż wcześniej)

Wyobraź sobie hodowcę Wojtka:
- **24 maja** wstawia do hali 22 000 piskląt → to jest **cykl wstawienia**.
- **27 czerwca** (34 dni później) odbierzemy od niego pierwsze ~10 560 ptaków (lżejsze).
- **5 lipca** (42 dni) odbierzemy resztę ~10 700 (cięższe).

W tym oknie **wpisujesz to wszystko jednym razem**:
- jedna data wstawienia,
- jedna liczba piskląt,
- 1-N dostaw zwrotnych (zwykle 2-3) z datami i ilościami.

System automatycznie liczy upadki (3%), sumuje dostawy, sprawdza czy się zgadza, wypełnia daty od daty wstawienia + doby.

---

## 2. Trzy tryby otwarcia okna (różne pre-fille)

### Tryb A: Nowe wstawienie od zera
- Otwierane: **Ctrl+N** w liście wstawień lub przyciskiem **➕ Dodaj**.
- Wstępne dostawy: **2 wiersze** automatycznie:
  - #1: doba 35, waga 2.1 kg, szt/poj 20
  - #2: doba 42, waga 2.8 kg, szt/poj 16
- Data: **dzisiaj**.
- Dostawca: pusty (wybierz z dropdown).

### Tryb B: Nowe z pre-fillem dostawcy
- Otwierane gdy rodzic (np. Karta Hodowcy) **przekazał `Dostawca`**.
- Dropdown automatycznie zaznacza tego dostawcę.
- Reszta jak w trybie A.

### Tryb C: Nowe wstawienie kopiując z poprzedniego cyklu
- Otwierane z menu PPM listy wstawień → **"Nowe wstawienie (kopiuj dane)"**.
- Rodzic przekazuje `DaneOstatniegoDostarczonego` = lista dostaw ostatniego cyklu tego hodowcy.
- System tworzy **dokładnie te same dostawy** (doba, waga, szt/poj, mnożnik).
- Dialog pyta: "Skopiować wszystko / tylko podstawowe?"
- **Po co**: hodowca zwykle robi to samo co miesiąc temu → oszczędność 5 minut.

### Tryb D: Modyfikacja (edycja istniejącego)
- Otwierane: **PPM → Edytuj** lub **Enter** na zaznaczonym wstawieniu.
- Rodzic przekazuje: `LpWstawienia`, `Modyfikacja=true`, `Dostawca`, `DataWstawienia`, `SztWstawienia`.
- Tryb formularza zmienia się na **"Modyfikacja"** (pomarańczowy badge w nagłówku).
- **Seria jest zablokowana** (`chkSeria.IsEnabled = false`) — w modyfikacji edytujesz tylko jedno wstawienie.
- Wczytuje istniejące dostawy z bazy, odtwarza pełną historię.
- Po zapisie używa **smart synchronizacji** UPDATE/INSERT/DELETE diff (zachowuje powiązania w innych tabelach).

---

## 3. Anatomia okna — 3 kolumny + 4 sekcje

```
┌──────────────────────────────────────────────────────────────────────────────┐
│ NAGŁÓWEK: 🐔 Wstawienia | Tryb: Nowe/Modyfikacja | 👨‍🌾 [Dostawca ▼]          │
│           ☑ 📅 Kalendarz ☑ ❓ Pomoc ☑ 📋 Seria ☑ 🚚 Dostawy                  │
├────────────────────────────────────┬───────────────────┬─────────────────────┤
│ LEWA — główna zawartość             │ ŚRODKOWA          │ PRAWA               │
│                                     │ (kalendarz)       │ (pomoc)             │
│ ┌─ JEDNO WSTAWIENIE ─────────────┐ │                   │                     │
│ │ 📅 Data  🐣 Sztuki  📊 Po upad.│ │ Mini-kalendarz    │ 💡 Formuły          │
│ │          📈 Suma   ✅ Różnica  │ │ tygodni z         │ ⚡ Funkcje          │
│ └────────────────────────────────┘ │ dostawami         │ ⚠️ Ważne            │
│                                     │                   │                     │
│ ┌─ SERIA WSTAWIEŃ (opcjonalna) ──┐ │ Nawigacja:        │                     │
│ │ #2: data + sztuki + 🗑️         │ │ ◀ tyg.4-5 ▶       │                     │
│ │ #3: data + sztuki + 🗑️         │ │                   │                     │
│ │ ➕ Dodaj  📋 Wklej wszystkie  │ │ Wykres pojemności │                     │
│ └────────────────────────────────┘ │ %/dzień           │                     │
│                                     │                   │                     │
│ ┌─ SZABLON DOSTAW ───────────────┐ │ Status na dół:    │                     │
│ │ # Doba Data Dni Waga Szt/poj   │ │ Tygodni: 5        │                     │
│ │   Mnoż Sztuki Auta AutoWyl Akc │ │ Łącznie: 230k szt │                     │
│ │ ➕ Dostawa  📋 Wklej różnicę   │ │                   │                     │
│ └────────────────────────────────┘ │                   │                     │
│                                     │                   │                     │
│ ┌─ NOTATKI + PRZYCISKI ──────────┐ │                   │                     │
│ │ ❌ Anuluj  📝 [Notatka]  💾 Zap│ │                   │                     │
│ └────────────────────────────────┘ │                   │                     │
└────────────────────────────────────┴───────────────────┴─────────────────────┘
```

**Szerokości okna**:
- Domyślna: **980 px** (tylko lewa kolumna).
- Z Kalendarzem: rozszerza do **1400 px** (lewa + środkowa).
- Z Pomocą bez Kalendarza: **1300 px**.
- Z Kalendarzem + Pomocą: pełna szerokość, środkowa 420px, prawa 300px.

---

## 4. Górny pasek — 4 checkboxy

| Checkbox | Co włącza | Domyślnie |
|---|---|---|
| **☑ 📅 Kalendarz** | Środkowa kolumna z mini-kalendarzem tygodni dostaw | ☐ wyłączone |
| **☑ ❓ Pomoc** | Prawa kolumna z 3 kartami: Formuły / Funkcje / Ważne | ☐ wyłączone |
| **☑ 📋 Seria** | Sekcja "Kolejne wstawienia (od #2)" w lewej kolumnie | ☐ wyłączone |
| **☑ 🚚 Dostawy** | Sekcja "Szablon Dostaw" w lewej kolumnie | ☑ włączone |

> 💡 Możesz wyłączyć **🚚 Dostawy** jeśli na razie tylko wpisujesz wstawienie bez planowania dostaw.

---

## 5. Sekcja "JEDNO WSTAWIENIE" (5 pól)

```
┌─────────────────────────────────────────────────────────────────┐
│ 📅 Data        🐣 Sztuki    📊 Po 3%upad.  📈 Suma   ✅ Różnica │
│ [25.05.2026]  [22000]      [21340]        [0]       [21340]    │
└─────────────────────────────────────────────────────────────────┘
```

### Co dokładnie liczy się sam

| Pole | Co | Edytowalne? |
|---|---|---|
| **📅 Data** | Data wstawienia piskląt | ✅ TAK (DatePicker) |
| **🐣 Sztuki** | Liczba piskląt (Twoja wartość) | ✅ TAK |
| **📊 Po 3% upadku** | `Sztuki × 0.97` (norma śmiertelności) | ❌ Auto (read-only) |
| **📈 Suma** | Suma sztuk we wszystkich planowanych dostawach | ❌ Auto |
| **✅ Różnica** | `Po upadku - Suma` — ile zostało do zaplanowania | ❌ Auto |

### Co dzieje się gdy zmieniasz pola

- **Zmiana Daty Wstawienia** → wszystkie dostawy przesuwają się **automatycznie** o ten sam offset (Doba zostaje stała, data dostawy = data_wstawienia + doba).
- **Zmiana Sztuk** → "Po upadku" przelicza się natychmiast (× 0.97), Różnica też.
- **Wpisanie sztuk w dostawie** → Suma się aktualizuje, Różnica też.

### Reguła "Różnicy = 0"

Cel: po wpisaniu wszystkich dostaw **Różnica powinna być ≈ 0**. To znaczy "zaplanowałeś wszystko".

- Różnica **dodatnia** = brakuje dostaw (zostało np. 500 szt do zaplanowania).
- Różnica **ujemna** = za dużo dostaw (np. -300 szt — wpisałeś więcej niż masz).
- W praktyce **±500 szt OK** — kurczaki to żywy materiał.

> 💡 Pole **Różnica** podświetla się **pomarańczowo** gdy różne od 0 — to sygnał ostrzegawczy.

---

## 6. Sekcja "SZABLON DOSTAW" — 11 kolumn w wierszu (NAJWAŻNIEJSZA)

Każdy wiersz to **jedna dostawa** (jeden odbiór od hodowcy).

```
┌─────────────────────────────────────────────────────────────────────────────┐
│ # | Doba | Data       | Dni | Waga | Szt/poj | Mnóż | Sztuki | Auta | AutoWyl │ Akcje │
│ 1 | 35   | 28.06.2026 | 35  | 2.1  | 20      | 2    | 10560  | 2    | 2.00   │ 📋🗑️  │
│ 2 | 42   | 05.07.2026 | 42  | 2.8  | 16      | 3    | 12672  | 3    | 3.00   │ 📋🗑️  │
└─────────────────────────────────────────────────────────────────────────────┘
```

### Co znaczy każda kolumna

| # | Kolumna | Co | Edytowalne | Auto-przelicz |
|---|---|---|---|---|
| 1 | **#** | Numer dostawy (1, 2, 3...) | ❌ | — |
| 2 | **Doba** | Doba uboju (35 = 35 dni po wstawieniu) | ✅ | Zmienia Datę i Dni |
| 3 | **Data** | Data odbioru (DatePicker) | ✅ | Zmienia Dni i Dobę |
| 4 | **Dni** | Dni między wstawieniem a dostawą | ❌ | Auto sync z Doba |
| 5 | **Waga** | Średnia waga ptaka (kg) | ✅ | Nie przelicza nic |
| 6 | **Szt/poj** | Sztuk w jednym pojemniku E2 | ✅ | Zmienia Auta wyliczone |
| 7 | **Mnóż** (zielony) | Mnożnik = ile naczepek | ✅ | Liczy Sztuki + Auta |
| 8 | **Sztuki** | Łączna liczba w tej dostawie | ✅ | Auto z Mnoż lub edytujesz ręcznie |
| 9 | **Auta** (zielony) | Liczba aut/naczepek (ręczna) | ✅ | Sync z Mnoż |
| 10 | **AutoWyl** | Auta wyliczone z Sztuk (read-only) | ❌ | `Sztuki / (Szt/poj × 264)` |
| 11 | **Akcje** | 📋 Wklej różnicę / 🗑️ Usuń | — | — |

### Magiczna stała: **264**

To **liczba pojemników E2 w jednej naczepce**. Tak są skonfigurowane Twoje naczepy:
- 36 skrzynek E2 na paletę × 33 palety/naczepka = **1 188 skrzynek**? 
- W rzeczywistości używamy **264 skrzynek = 1 warstwa** (oryginalna kalkulacja zakładu).
- Formuła: **Sztuki = Mnoż × Szt/poj × 264**.

> Przykład: 2 naczepki × 20 szt/poj × 264 = **10 560 sztuk**.

### Auto-kopiowanie z poprzedniej dostawy

Gdy klikasz **➕ Dostawa** żeby dodać nowy wiersz, system **automatycznie kopiuje** z ostatniej dostawy:
- **Waga** (np. 2.1 z poprzedniego wiersza).
- **Szt/poj** (np. 20).

Dlaczego: kolejne dostawy zwykle są podobne wagowo, tylko zmienia się doba i ilość.

> Jeśli nie chcesz kopiowania, możesz przekazać explicite parametry (z poziomu kodu).

### Synchronizacja **Doba ↔ Data**

Doba i Data dostawy są **wzajemnie synchronizowane** — zmiana jednego automatycznie aktualizuje drugie.

- Wpisujesz **Doba = 35** → Data ustawia się na `DataWstawienia + 35 dni`.
- Zmieniasz **DatePicker** na 30.06 → Doba ustawia się na `(30.06 - DataWstawienia)`.

System wymusza spójność — niemożliwe jest mieć Dobę "42" a Datę za 2 tygodnie.

### Walidacja: dostawa nie może być przed wstawieniem

Jeśli ustawisz datę dostawy **wcześniejszą** niż data wstawienia → **błąd przy zapisie**:
> "Data odbioru w dostawie #X jest wcześniejsza niż data wstawienia."

---

## 7. Kolory pól (kod wizualny)

| Kolor pola | Znaczenie | Przykłady |
|---|---|---|
| 🟢 **Zielony (jasny)** | Główne pole na które masz wpływ — wpisuj tu | Sztuki wstawienia, Mnóż w dostawie |
| ⚪ **Białe** | Edytowalne, standardowe | Waga, Szt/poj, Doba |
| 🔘 **Szare** | Read-only (auto-wyliczone) | Po upadku, AutoWyl, Dni |
| 🟡 **Pomarańczowy** | Wartość pochodna, sygnał ostrzegawczy | Różnica gdy ≠ 0 |
| 🟢 **Zielony (mocny)** | Suma (auto) | Suma sztuk |
| 🔴 **Czerwony** | Akcja destruktywna | 🗑️ Usuń |
| 🔵 **Niebieski** | Akcja pomocnicza | 📋 Wklej, ➕ Dodaj |
| 🟢 **Zielony (przycisk)** | Akcja główna | 💾 Zapisz |

---

## 8. Sekcja "SERIA WSTAWIEŃ" (opcjonalna)

> Po co: ten sam hodowca wstawia w **kilku halach po sobie** w jednym tygodniu (np. 25.05, 28.05, 31.05). Zamiast tworzyć 3 cykle ręcznie, robisz wszystko w 1 oknie.

### Jak włączyć

1. **Najpierw wypełnij wstawienie #1** (data + sztuki) — bez tego system nie pozwoli.
2. Zaznacz **☑ 📋 Seria** w nagłówku.
3. Otwiera się sekcja **"Kolejne wstawienia (od #2)"**.
4. Klikasz **➕ Dodaj** → wiersz #2 (DatePicker + Sztuki + 🗑️).
5. Powtarzasz dla #3, #4...

### Przycisk "📋 Wklej wszystkie"

Kopiuje liczbę sztuk z **głównego pola (#1)** do **wszystkich wierszy serii**.

> Po co: hodowca robi tę samą partię w 3 halach — wpisz 22 000 raz w głównym, kliknij "Wklej wszystkie" → wszystkie serie dostają po 22 000.

### Walidacja przed włączeniem serii

Gdy klikasz **☑ Seria** bez wypełnionego pola sztuk/daty → **alert**:
> "Najpierw wypełnij datę i ilość sztuk pierwszego wstawienia!"

### Numeracja

Seria zaczyna się od **#2** (bo #1 jest w głównym panelu). Po usunięciu wpisu, numeracja **odświeża się** (zostaje ciągła #2, #3, #4...).

### Modyfikacja blokuje serię

W trybie **Modyfikacja** (edycja istniejącego wstawienia) seria jest **wyłączona** — `chkSeria.IsEnabled = false`. Edytujesz tylko **jedno** wstawienie. Jeśli musisz dodać kolejne hodowcy → zamknij okno i otwórz nowe.

### Każde wstawienie serii = pełna kopia dostaw

Po zapisaniu serii, **każde** wstawienie (#1, #2, #3...) dostaje **te same dostawy** ze "Szablonu Dostaw". Czyli jeśli wpiszesz 2 dostawy w szablonie i 3 wstawienia w serii → powstaje **3 cykle × 2 dostawy = 6 wpisów** w `HarmonogramDostaw`.

---

## 9. Wszystkie przyciski "Wklej" (po co który)

### W sekcji JEDNO WSTAWIENIE — brak przycisków

### W sekcji SERIA — **📋 Wklej wszystkie**

Kopiuje **sztuki z #1** do wszystkich wierszy serii.
Walidacja: muszą być wpisane sztuki w głównym polu.

### W sekcji DOSTAWY — **📋 Wklej różnicę** (nad listą)

Kopiuje **wartość pola "Różnica"** (z sekcji JEDNO WSTAWIENIE) do **wszystkich dostaw** w kolumnie "Sztuki".

> Po co: zaplanowałeś 22 000 piskląt, masz 21 340 po upadku, wpisałeś dostawę #1 = 10 560 sztuk → Różnica pokazuje **10 780 zostało**. Klikasz "Wklej różnicę" → reszta dostaw dostaje 10 780.

> ⚠ **Uwaga**: wkleja do **wszystkich** dostaw bez Sztuk. Jeśli zostały **dwie puste** dostawy — obie dostaną tę samą wartość (co zwykle za dużo). W praktyce użyj gdy została **jedna pusta**.

### W każdej dostawie — **📋 Akcje per wiersz**

Zazwyczaj nie ma pojedynczego "wklej do tego wiersza" — używaj globalnego "Wklej różnicę" lub edytuj ręcznie.

---

## 10. Pre-fill dostawcy (dropdown)

Dropdown **👨‍🌾 Dostawca** w nagłówku:
- Lista pobierana z tabeli `dbo.DOSTAWCY` (SELECT DISTINCT Name).
- **Cache na 30 minut** — szybkie ładowanie nawet przy 2000+ dostawców.
- Cache się odświeża automatycznie albo wymuszony przez F5 / nowe okno.

### Co dzieje się gdy wybierzesz dostawcę

System **automatycznie pobiera dane** tego dostawcy (z `dbo.Dostawcy`):
- Address, PostalCode, City, Distance (KM), Phone1, Email.

Te dane są używane:
- W zapisie do `HarmonogramDostaw` (kolumna `KmK`, `KmH`).
- Nie są pokazywane wprost w oknie (panel `panelDaneDostawcy` jest ukryty), ale logicznie wpływają na to co trafia do bazy.

---

## 11. Środkowa kolumna — Mini-Kalendarz dostaw (☑ 📅 Kalendarz)

> Po co: zanim zapiszesz, widzisz **czy nie wkładasz dostawy do przepełnionego dnia**.

### Co tam jest

```
┌─────────────────────────────────────┐
│ 📅 Podgląd tygodni dostaw           │
│ ◀ tyg. 26-27 ▶                       │
├─────────────────────────────────────┤
│ Tydzień 26 (22-28.06)               │
│ [Wojtek: 2 auta, 10 560 szt]        │
│ [Mazur: 1 auto, 6 200 szt]          │
│                                     │
│ Tydzień 27 (29.06-05.07)            │
│ [Wojtek: 3 auta, 12 672 szt] PLAN   │
│ [Stachura: 2 auta, 8 100 szt]       │
├─────────────────────────────────────┤
│ Tygodni z dostawami: 5              │
│ Łącznie: 230 540 szt.               │
└─────────────────────────────────────┘
```

### Skąd dane

System pobiera **wszystkie dostawy z bazy** z ostatnich **6 miesięcy i przyszłych 6 miesięcy** (poza statusami "Anulowany" i "Sprzedany").

### Co pokazują kolory dnia

| Kolor dnia | % pojemności | Co znaczy |
|---|---|---|
| 🟢 Zielony | <50% | OK, spokojnie |
| 🟡 Żółty/pomarańczowy | 50-80% | Średnio, można dodać |
| 🟠 Czerwony | 80-100% | Pełno, ostrożnie |
| 🔴 Czerwony + **PULSE** | ≥100% | **PRZEKROCZENIE!** |
| 🟣 Fioletowy + PULSE | Twoja planowana dostawa | Z aktualnego formularza |
| 🔵 Niebieski + PULSE | Dzisiaj | Bieżący dzień |

> Norma pojemności: **80 000 szt/dzień max** (`MAX_DAILY_CAPACITY = 80000`). Tydzień = 560 000 szt.

### Twoje planowane dostawy widoczne na fioletowo

Gdy wpiszesz datę dostawy w "Szablon Dostaw", system **w 300ms** po ostatniej zmianie:
1. Wczytuje świeże dane z bazy.
2. Nakłada **Twoje planowane** dostawy (Status="Planowany") na ten widok.
3. Renderuje wszystko z pulse animacjami.

Jest **debounce** (opóźnienie) **300ms** żeby kalendarz nie re-renderował się na każde naciśnięcie klawisza.

### Nawigacja tygodni

- **◀ Poprz. tydzień** / **▶ Następ.** — przesuwasz widok.
- **Pulse animacja** dla bieżącego tygodnia (pomarańczowe halo).
- **Skrót**: brak — używaj myszki.

### Wskaźnik tygodnia

Numer tygodnia liczony **ISO 8601** (poniedziałek = pierwszy dzień, FirstFourDayWeek). Polski standard.

---

## 12. Prawa kolumna — Pomoc (☑ ❓ Pomoc)

Tylko **3 statyczne karty**:
- 💡 **Formuły** — wzory matematyczne (mnoż × szt/poj × 264 itp.)
- ⚡ **Funkcje** — co robi który przycisk
- ⚠️ **Ważne** — typowe pułapki

Nie ma interakcji — to tylko wbudowana ściąga. Otwarcie poszerza okno o 300 px.

---

## 13. Notatki — jedno pole, wiele zapisów

Pole **📝 Notatka** w dolnym pasku.

Po zapisie wstawienia, notatka zapisuje się do tabeli `Notatki` **per każda dostawa** (czyli dla 2 dostaw powstają **2 wpisy** z tą samą treścią).

> Klucz: `IndeksID = Lp z HarmonogramDostaw`, `TypID` = stała dla wstawień, `KtoStworzyl = UserID`, `DataUtworzenia = now`.

W modyfikacji notatka jest wczytywana z tabeli `WstawieniaKurczakow.Uwagi` (czyli **z innej tabeli niż się zapisuje**) — historyczna asymetria, ale działa.

---

## 14. ZAPIS — krok po kroku (BtnZapisz_Click)

### Faza 1: Walidacje

System sprawdza:
1. ✅ Wybrany dostawca?
2. ✅ Jeśli **seria**: liczba serii > 0?
3. ✅ Jeśli **nie seria**: data wstawienia + sztuki wpisane?
4. ✅ Liczba dostaw > 0?
5. ✅ Każda dostawa: data + sztuki > 0?
6. ✅ Każda data dostawy ≥ data wstawienia?

Jeśli któraś walidacja fail → **MessageBox** z opisem błędu → zapis przerwany.

### Faza 2: Sprawdzenie podobnego wstawienia (tylko dla NOWYCH)

System szuka w bazie wstawień u **tego samego dostawcy** w **±1 dzień od daty wstawienia**. Jeśli znajdzie → **ostrzeżenie**:
> "⚠ Znaleziono podobne wstawienie: Lp=X, Data=Y, Sztuki=Z. Czy na pewno chcesz utworzyć nowe?"

- **Tak** → kontynuuj zapis.
- **Nie** → zapis przerwany (możesz zamknąć okno).

### Faza 3: Decyzja o typie kontraktu (3 dialogi)

System pyta o **typ umowy** → **typ ceny** → **bufor**. To **3 kolejne dialogi** modalne.

#### Dialog 1: "Wybierz typ hodowcy" (tylko dla NOWEGO lub gdy edytujesz i chcesz zmienić)

2 duże przyciski:
```
┌─────────────────────────┬─────────────────────────┐
│   💰 Wolnyrynek         │   📝 Kontrakt           │
│   (żółty)               │   (niebieski)           │
└─────────────────────────┴─────────────────────────┘
```

#### Dialog 2a: Jeśli "Wolnyrynek" → "Czy hodowca jest wierny?"

```
┌─────────────────────────┬─────────────────────────┐
│   ⭐ Tak (wierny)        │   Nie                   │
└─────────────────────────┴─────────────────────────┘
```

- **Tak** → typ umowy: **`W.Wolnyrynek`**, bufor: **"B.Wolny."**
- **Nie** → typ umowy: **`Wolnyrynek`**, bufor: **"Do wykupienia"**

#### Dialog 2b: Jeśli "Kontrakt" → "Wybierz typ ceny"

4 przyciski:
```
┌─────────────┬─────────────┬─────────────┬─────────────┐
│ 🔗 Łączona  │ 🌾 Rolnicza │ 💰 Wolny    │ 🏛️ Minister.│
│ (fiolet)    │ (zielony)   │ (żółty)     │ (niebieski) │
└─────────────┴─────────────┴─────────────┴─────────────┘
```

Wybrana wartość trafia do `typCeny`. Typ umowy: **`Kontrakt`**, bufor: **"B.Kontr."**

#### Modyfikacja — podgląd istniejącego typu

W trybie **Modyfikacja**, jeśli wstawienie ma już typ → najpierw dialog:
```
┌────────────────────────────────────────┐
│  Aktualny typ:                         │
│  📝 Kontrakt 💰 Wolnyrynek             │
│  Bufor: B.Kontr.                       │
│                                        │
│  [🔒 Zachowaj]  [✏️ Zmień]  [Anuluj]  │
└────────────────────────────────────────┘
```

- **Zachowaj** → typ niezmieniony.
- **Zmień** → przechodzi do dialogu wyboru typu (jak nowe).
- **Anuluj** → cały zapis przerwany.

#### Tabela mapowania (skrót)

| Typ hodowcy | + Wybór | Typ umowy | Typ ceny | Bufor |
|---|---|---|---|---|
| Wolnyrynek | Tak (wierny) | `W.Wolnyrynek` | `wolnyrynek` | `B.Wolny.` |
| Wolnyrynek | Nie | `Wolnyrynek` | `wolnyrynek` | `Do wykupienia` |
| Kontrakt | Łączona | `Kontrakt` | `łączona` | `B.Kontr.` |
| Kontrakt | Rolnicza | `Kontrakt` | `rolnicza` | `B.Kontr.` |
| Kontrakt | Wolnyrynek | `Kontrakt` | `wolnyrynek` | `B.Kontr.` |
| Kontrakt | Ministerialna | `Kontrakt` | `ministerialna` | `B.Kontr.` |

### Faza 4: Transakcja SQL (zapis)

System otwiera **transakcję** i wykonuje:

#### Dla NOWEGO bez serii

1. `NowyNumerWstawienia()` — SELECT MAX(Lp) z **lockiem** (UPDLOCK + HOLDLOCK) żeby zabezpieczyć przed race condition.
2. `INSERT INTO WstawieniaKurczakow` (Lp, Dostawca, DataWstawienia, IloscWstawienia, DataUtw, KtoStwo, Uwagi, TypUmowy, TypCeny).
3. Dla każdej dostawy: `INSERT INTO HarmonogramDostaw` (Lp, LpW=link, Dostawca, DataOdbioru, Kmk, KmH, WagaDek, SztukiDek, TypUmowy, bufor, SztSzuflada, Auta, typCeny, UWAGI, DataUtw, KtoStwo).
4. Dla każdej dostawy jeśli wpisana notatka: `INSERT INTO Notatki`.

#### Dla NOWEGO z serii

Pętla po wszystkich serii (#1 + #2 + ...):
1. Każde wstawienie dostaje swój `NowyNumerWstawienia()`.
2. `INSERT INTO WstawieniaKurczakow` dla każdego.
3. Dla każdej serii — pętla po wszystkich dostawach ze "Szablonu" → `INSERT INTO HarmonogramDostaw`.

#### Dla MODYFIKACJI

1. `UPDATE WstawieniaKurczakow SET ... WHERE Lp=...`
2. **`SynchronizujDostawy`** — smart diff:
   - Dla każdej dostawy w UI:
     - Jeśli ma `LpDostawy` (była w bazie) **i istnieje w bazie** → **UPDATE**.
     - Jeśli nie ma `LpDostawy` (nowa) → **INSERT**.
   - Po pętli: dla każdej dostawy w bazie której nie ma w UI → **DELETE**.
3. Notatki — analogicznie.

> **Dlaczego smart diff zamiast DELETE ALL + INSERT ALL**: bo `HarmonogramDostaw.Lp` ma **powiązania w innych tabelach** (`PartiaDostawca`, `QC`, transport...). DELETE złamałby te linki.

### Faza 5: Commit + zwrot do rodzica

- `COMMIT TRANSACTION` — wszystko zapisane.
- `_zapisanoSukces = true`.
- `_zapiszInfoAuta = (typUmowy != "Wolnyrynek")` — dla zwykłego Wolnyrynek **NIE zapisuje aut** (bo to wstępne dane, wolny rynek się zmieni).
- `_zapiszIloscNotatek = liczba zapisanych notatek`.
- `DialogResult = true`.
- `Close()`.

Rodzic (lista wstawień) odczytuje te właściwości i pokazuje **toast** "✅ Zapisano" w prawym dolnym rogu.

---

## 15. Anulowanie

Przycisk **❌ Anuluj** lub **Esc** → zamyka okno bez zapisu. **Nie ma pytania** "czy anulować niezapisane zmiany" — po prostu zamyka.

> ⚠ Jeśli wpisałeś dużo danych, zamknięcie = strata. Pamiętaj.

---

## 16. Wszystkie skróty klawiszowe

Okno **nie ma globalnych hot-keys** w stylu Ctrl+S. Działa tylko:
- **Esc** — Anuluj (jeśli focus jest na przycisku Anuluj).
- **Tab** — przewija fokus między polami (standard WPF).
- **Enter** w polu — zazwyczaj przejście do następnego (nie zawsze).

> 💡 Zapis tylko myszką → kliknij **💾 Zapisz**.

---

## 17. Klasy pomocnicze w kodzie (dla deweloperów)

| Klasa | Co reprezentuje |
|---|---|
| `DostawaRow` | Jedna dostawa: TextBox-y, DatePicker, ID, optional LpDostawy |
| `SeriaWstawienRow` | Jedno dodatkowe wstawienie #2+ w serii |
| `HarmonogramDostaw` | Wczytana z bazy dostawa (przy modyfikacji) |
| `DaneOstatniegoDostarczonego` | Lista dostaw do skopiowania przy "Nowe (kopiuj)" |
| `DaneDostawy` | Jeden wpis ze skopiowanej historii |
| `MiniWeekData` | Tydzień w kalendarzu (start, end, sumy, dni) |
| `MiniDeliveryItem` | Jedna dostawa w kalendarzu (z kolorem statusu) |

---

## 18. Typowy dzień Justyny — krok po kroku

```
09:30  Justyna dzwoni do Wojtka. Umawia wstawienie 26.05, 25 000 piskląt.
       
09:35  Otwiera ZPSP → "Lista Wstawień" → Ctrl+N → otwiera okno "Wstawienia Kurczaków"
       (Tryb A: Nowe).
       
09:36  Dropdown 👨‍🌾 → wybiera "Wojtek Nowak".
       System auto-pobiera adres, KM, telefon (niewidoczne, ale używane).
       
09:37  📅 Data: 26.05.2026 (zostawia jutrzejszą).
       🐣 Sztuki: 25 000.
       Po upadku auto: 24 250. Suma: 0. Różnica: 24 250.
       
09:38  Klika ☑ Kalendarz — pojawia się środkowa kolumna.
       Widzi że tydzień 26 ma już 80% pojemności (czerwony!).
       Decyduje: tylko 1 dostawa na wtorek (mniej obciążenia).
       
09:40  Szablon dostaw — domyślnie #1 (doba 35) i #2 (doba 42).
       
09:41  W #1: zmienia Mnoż na 3 → system liczy:
       Sztuki = 3 × 20 × 264 = 15 840.
       Auta = 3 (sync).
       AutoWyl = 3.00.
       
09:42  W #2: zmienia Mnoż na 1.6 → Sztuki = 1.6 × 16 × 264 = 6 758.
       Auta = 1.6 → 2 (zaokrąglone w formularzu zewnętrznym).
       
09:43  Sprawdza:
       Suma = 15 840 + 6 758 = 22 598.
       Po upadku = 24 250.
       Różnica = 24 250 - 22 598 = 1 652 (dodatnia, brakuje 1 652 szt).
       
09:44  Justyna nie chce dodawać 3. dostawy. Decyduje: zwiększyć mnożnik #2 z 1.6 na 1.8.
       Sztuki #2 = 1.8 × 16 × 264 = 7 603.
       Suma = 15 840 + 7 603 = 23 443.
       Różnica = 24 250 - 23 443 = 807 (dodatnia, ale OK).
       
09:45  📝 Notatka: "Wojtek prosi o odbiór po 9:00, Marek pref."
       
09:46  Klika 💾 Zapisz.
       
09:47  Dialog 1: "Typ hodowcy?" → klika "Kontrakt" (Wojtek ma stały kontrakt).
       
09:47  Dialog 2: "Typ ceny?" → klika "Łączona" (standardowa dla Wojtka).
       
09:48  System zapisuje, okno zamyka się, lista pokazuje toast:
       "✅ Zapisano • 2 dostawy • 1 notatka"
       
09:50  Justyna patrzy na kalendarz dostaw — tydzień 31 (26.07) ma dostawę #2 Wojtka.
       Wszystko OK.
```

---

## 19. Co dzieje się **automatycznie** (15 ukrytych mechanizmów)

> Te rzeczy okno robi **samo** — nie musisz o nich myśleć, ale warto wiedzieć że istnieją:

1. **Auto-kopiowanie wagi/szt z poprzedniej dostawy** przy dodawaniu nowej.
2. **Sync Doba ↔ Data** — zmiana jednego aktualizuje drugie.
3. **Mnoż ↔ Sztuki ↔ AutoWyl** — automatyczne przeliczanie po obu stronach.
4. **3% upadku** = `Sztuki × 0.97` — zaraz po wpisaniu sztuk.
5. **Suma + Różnica** — natychmiast po każdej zmianie sztuk w dostawie.
6. **Kopiowanie dat dostaw** przy zmianie daty wstawienia (Doba zostaje stała).
7. **Pre-fill 2 domyślnych dostaw** (35d/2.1kg i 42d/2.8kg) dla nowych cykli.
8. **Mini-kalendarz auto-refresh** 300ms po zmianach (debounce).
9. **Cache dostawców** 30 min (szybkie ładowanie dropdown).
10. **UPDLOCK + HOLDLOCK** na nowym numerze wstawienia (zabezpieczenie przed race).
11. **Smart diff przy modyfikacji** (UPDATE/INSERT/DELETE zamiast nuke + recreate).
12. **isLoading guard** — eventy są wstrzymane podczas ładowania danych (żeby kalkulacje nie wykonały się na pół-załadowanych danych).
13. **Parse liczb obu kultur** — akceptuje "10,5" i "10.5".
14. **Tolerancja spacji w sztuki** — "13 200" działa tak samo jak "13200".
15. **Pulse animacje** w kalendarzu — bieżący tydzień, dziś, planowane dostawy, overload.

---

## 20. Typowe problemy i jak je rozwiązać

### "Próbuję włączyć Serię ale nie pozwala"
**Powód**: jesteś w trybie **Modyfikacja**.
**Rozwiązanie**: Seria działa tylko dla **Nowych**. Zamknij okno, kliknij ➕ Dodaj na liście.

### "Sztuki w dostawie zmieniam, ale Suma nie aktualizuje się"
**Powód**: prawdopodobnie wpisujesz w **AutoWyl** (read-only) zamiast w **Sztuki**.
**Rozwiązanie**: pole zielone (HighlightBox) = Mnoż / Auta. Pole jaśniejsze ale aktywne = Sztuki. Sprawdź gdzie klikasz.

### "Mnoż się nie kopiuje do Aut"
**Powód**: prawdopodobnie pomyłka — Mnoż jest **zielony** (highlight), Auta ręczne też **zielone**. Wpisuj w Mnoż.

### "Auta wyliczone pokazują 2.50 ale Auta ręczne 3"
**Powód**: AutoWyl to **dokładna** liczba aut (czyste dzielenie). Auta ręczne to **zaokrąglona** liczba (zwykle w górę).
**Rozwiązanie**: zostaw — w bazie zapisze się **Auta ręczne** (3), AutoWyl to tylko informacyjne.

### "Wpisuję datę dostawy 20.05, a Doba ustawia się na -5"
**Powód**: data dostawy jest **wcześniejsza** niż data wstawienia (25.05).
**Rozwiązanie**: zwykle musisz zmienić datę dostawy na późniejszą, ALBO datę wstawienia na wcześniejszą. System nie pozwoli zapisać.

### "Zapisałem dla Wolnyrynek i Auta zniknęły"
**Powód**: TAK MA BYĆ. Dla zwykłego Wolnyrynek (nie wierny) auta **nie są zapisywane** do `HarmonogramDostaw`.
**Rozwiązanie**: nic. Jeśli chcesz mieć auta zapisane → zmień typ na **Wierny** lub **Kontrakt**.

### "Pomyłkowo wybrałem Kontrakt zamiast Wolnyrynek przy zapisie"
**Powód**: kliknąłeś w pierwszym dialogu zły przycisk.
**Rozwiązanie**: 
- **W nowym oknie**: kliknij Anuluj w dowolnym kolejnym dialogu → zapis przerwany, możesz ponowić.
- **Po zapisie**: otwórz w trybie Modyfikacja → przy zapisie wybierz **"Zmień"** w podglądzie aktualnego typu.

### "Kalendarz dostaw pokazuje tydzień z 110% — czy mogę zapisać?"
**Powód**: ten tydzień jest już przepełniony.
**Rozwiązanie**: system **NIE blokuje** zapisu. Możesz zapisać. Ale negocjuj potem z innymi hodowcami przesunięcie. Praktyka: nie przekraczaj 85%/tydzień.

### "Cache hodowców działa dziwnie — nie widzę nowego dostawcy w dropdown"
**Powód**: cache na 30 minut — może świeży hodowca nie jest jeszcze załadowany.
**Rozwiązanie**: zamknij i otwórz okno (lub F5 w innym module wymusi refresh).

---

## 21. Diagram cyklu — od wstawienia do uboju

```
DATA WSTAWIENIA (0)         DATA DOSTAWY #1 (~35d)     DATA DOSTAWY #2 (~42d)
       │                            │                            │
       │ wpisujesz w tym oknie      │ system planuje             │ system planuje
       │ → INSERT WstawieniaKurczakow│ → INSERT HarmonogramDostaw│ → INSERT HarmonogramDostaw
       │                            │                            │
       │                            │                            │
       │       ~35 dni              │        ~7 dni             │
       │       hodowca rośnie       │        (różnica między dostawami)
       │       pisklęta             │                            │
       │                            │                            │
       ↓                            ↓                            ↓
  📅 24.05.2026             📅 27.06.2026                  📅 05.07.2026
  Wojtek wstawia            Marek wiezie 10 560 szt         Marek wiezie 12 672 szt
  22 000 piskląt            do zakładu                       do zakładu
                            → tworzy się Partia #5891        → tworzy się Partia #5892
                            (instr. 03 Lista Partii)         (instr. 03 Lista Partii)
                            
  WSTAWIENIE = ZAMÓWIENIE   DOSTAWY = REALIZACJE
  (zaplanowane)             (po fakcie powstają partie ubojowe)
```

---

## 22. Co dalej

- **Wszystkie cykle** lista (skąd otwierasz to okno) → instr. `02_Lista_Wstawien.md`.
- **Partia uboju** (powstaje po dostawie) → instr. `03_Lista_Partii.md`.
- **Karta hodowcy 360°** (analiza historyczna) → instr. `06_Baza_Hodowcow.md`.
- **Kalendarz dostaw** (widok wszystkich dostaw w tygodniu) → instr. `05_Kalendarz_Dostaw_Zywca.md`.
- **Umowy zakupu** (formalna umowa z hodowcą) → instr. `07_Umowy_i_Dokumenty.md`.

---

## 23. Reguły kciuka (do zapamiętania)

1. **Różnica ≈ 0** = wszystko zaplanowane. Tolerujemy ±500 szt.
2. **3% upadku** = norma branżowa. Hodowcy lepsi 1.5%, słabsi 4-5% — system zakłada średnio.
3. **264 = pojemniki E2 w naczepce**. Stała Twojego zakładu.
4. **Kalendarz tygodnia 80k szt/dzień** = limit miękki. Nie przekraczaj 85%.
5. **Seria** tylko dla nowych, jeden hodowca, jeden okres.
6. **Modyfikacja** robi smart diff — bezpieczne.
7. **Wolnyrynek (nie wierny) NIE zapisuje aut**. Świadoma decyzja.
8. **Cache dostawców 30 min**. F5 = refresh.

> ⚠ Jeśli widzisz w oknie coś czego ta instrukcja nie opisuje (nowy guzik, dziwne zachowanie) — zgłoś, zaktualizujemy.
