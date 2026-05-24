# Instrukcja: Lista Wstawień (WidokWstawienia) — deep

> **Dla kogo**: Justyna, Maja (codziennie), Sergiusz (przegląd).
> **Co robi okno**: pokazuje **WSZYSTKIE cykle wstawień** + zarządza **przypomnieniami** dzwonienia do hodowców + **historią kontaktów** z nimi + **statystykami pracowników**.
> **Plik kodu**: `Zywiec/WstawieniaKurczaka/WidokWstawienia.xaml(.cs)` (~5300 linii — z dialogami).
> **Otwierane z**: głównego menu ZPSP → kafelka **🐔 Wstawienia Kurczaków**.

---

## 1. Po co istnieje

Codzienny pulpit Mai/Justyny:
- "Kogo dziś dzwonić?" → środkowy panel **Przypomnienia**.
- "Co ostatnio powiedziała Krystyna do Wojtka?" → prawy panel **Historia kontaktów**.
- "Wszystkie aktywne wstawienia?" → lewy panel **Lista wstawień**.

---

## 2. Anatomia — 3 panele + nagłówek

```
┌───────────────────────────────────────────────────────────────────────────────┐
│ 📋 Panel Główny Wstawień             [📊 Statystyki] [❓ Instrukcja]         │
├──────────────────────────────────────┬────────────────────┬───────────────────┤
│ 📋 LISTA WSTAWIEŃ (2.1x szerokość)   │ ⏰ PRZYPOMNIENIA  │ 📜 HISTORIA       │
│                                       │  (1.3x)            │  KONTAKTÓW (1.2x) │
│ [➕ Dodaj]                            │                    │                   │
│ 🔍 [Szukaj]  [📅 Tylko przyszłe]      │  🔔 12             │                   │
│ 📆 Od [...] Do [...]                  │                    │                   │
│                                       │                    │                   │
│ ┌─ DataGrid (10 kolumn) ──────────┐  │ ┌─ DataGrid 7 ──┐ │ ┌─ DataGrid 5 ─┐ │
│ │ 📞 LP Hodowca Data Ilość        │  │ │ LP Data Hod.  │ │ │ Hodowca Kto  │ │
│ │ Typ Cena Kto Utworzono Potw.    │  │ │ Ilość Tel 📝Za│ │ │ Nast Notatka │ │
│ │ Kolory wierszy: 🟢🟡⚪          │  │ │ 🔴 aktyw./    │ │ │ Kiedy        │ │
│ │ Czerwone tło dla zagroż. SLA    │  │ │ ⚪ oczekuj.   │ │ │ Filtry: 90d  │ │
│ └─────────────────────────────────┘  │ └───────────────┘ │ └──────────────┘ │
└──────────────────────────────────────┴────────────────────┴───────────────────┘
```

Okno **WindowState=Maximized** (pełen ekran), gradient szare tło.

---

## 3. Lewy panel — Lista wstawień

### Inicjalizacja (asynchroniczna)

Po Loaded okno wywołuje `InitializeData()` (priorytet ContextIdle):
1. **PreloadDeliveryCache()** — cache wszystkich dostaw z `HarmonogramDostaw` JOIN `WstawieniaKurczakow` (60-sekundowy TTL).
2. **LoadWstawienia()** — SELECT **TOP 100** wstawień (bez filtru daty).
3. **LoadPrzypomnienia()** — z widoku `v_WstawieniaDoKontaktu` + lista oczekujących (snoozed).
4. **LoadHistoria()** — TOP **500 wpisów** z **ostatnich 90 dni** z `ContactHistory`.

> 💡 Domyślnie pokazujemy **ostatnie 100** wstawień (wydajność). Wpisanie czegoś w filtr → ładuje **wszystkie pasujące**.

### Kolumny DataGrid (10 kolumn)

| # | Kolumna | Binding | Szer. | Co |
|---|---|---|---|---|
| 1 | **📞 (świeżość)** | OstatniKontakt | 24px | Kropka kolorowa (Ellipse) |
| 2 | **LP** | LP | 48px | ID wstawienia |
| 3 | **Hodowca** | Dostawca | 110px | Nazwa |
| 4 | **Data** | Data | 100px | `yyyy-MM-dd ddd` (dzień tygodnia) |
| 5 | **Ilość** | IloscWstawienia | 65px | Format `# ##0` (separatory tysięcy) |
| 6 | **Typ** | TypUmowy | 70px | Wolny / Kontrakt |
| 7 | **Cena** | TypCeny | 85px | Border kolorowy + tekst |
| 8 | **Kto** | KtoStwo | 90px | Avatar + skrót imienia |
| 9 | **Utworzono** | DataUtw | * | `yyyy-MM-dd HH:mm:ss` |
| 10 | **Potw. kto** | KtoConfName | 90px | Avatar + imię (visible tylko jeśli potwierdzone) |

### Kropka świeżości (kolumna 1)

`KontaktFreshnessConverter` przekształca datę ostatniego kontaktu → kolor:
- 🟢 **Zielony** (`#2ECC71`) — kontakt ≤ 7 dni temu.
- 🟡 **Żółty** (`#F1C40F`) — kontakt 7-14 dni temu.
- 🔴 **Czerwony** (`#E74C3C`) — kontakt > 14 dni temu.
- ⚪ **Szary** (`#BDC3C7`) — nigdy nie było kontaktu.

Tooltip: "Ostatni kontakt: DD.MM.YYYY (X dni temu)" lub "Brak kontaktu".

### Kolory wierszy (cały wiersz)

`ApplySupplierGroupingColors` + `LoadingRow` handler:
- 🟢 **Zielony** (RGB 180,240,180) — `DeliveryStatus.AllPast` (wszystkie dostawy już za nami).
- 🟡 **Żółty** (RGB 255,245,170) — `DeliveryStatus.Ongoing` (część przeszłych, część przyszłych).
- ⚪ **Szary** (RGB 220,220,220) — `DeliveryStatus.AllFuture` (wszystkie dostawy w przyszłości).
- ⚪ **Białe** — brak dostaw.

### Kolor typu ceny (kolumna 7)

`TypCenyToColorConverter` mapuje:
- **łączona** → fiolet
- **rolnicza** → zielony
- **wolnyrynek** → żółty
- **ministerialna** → niebieski

### Avatary (kolumna Kto)

`UserAvatarManager`:
- **Static cache** `_avatarBrushCache` (Dictionary<string, ImageBrush>) shared.
- `HasAvatar(userId)` sprawdza obecność, `GetAvatarRounded(userId, 40)` zwraca Image.
- Fallback: Ellipse z inicjałami (np. "JK") jeśli brak avatara.
- Ładowanie **asynchroniczne** (Dispatcher.BeginInvoke) — unika problemu recyklingu wierszy DataGrid.

---

## 4. Filtry — 4 niezależne

### 1. 🔍 Szukaj (TextBox)

- **Wpisany tekst**: `_wstawieniaShowAll = true` → przeładuje **wszystkie** wstawienia (ignoruje TOP 100).
- **Pusty**: wraca do TOP 100.
- Filtr LIKE na **Dostawca** (case-insensitive).

### 2. 📅 ☑ Tylko przyszłe (CheckBox)

- Zaznaczony → wykonuje `GetUniqueSuppliersWithFutureDeliveries()`:
  ```sql
  SELECT MAX(DataWstawienia), MAX(LP), Dostawca 
  FROM WstawieniaKurczakow 
  WHERE EXISTS (SELECT 1 FROM HarmonogramDostaw WHERE LpW = LP AND DataOdbioru > NOW()) 
  GROUP BY Dostawca
  ```
- Pokaże **każdego hodowcę tylko raz** (jego najnowsze wstawienie z przyszłymi dostawami).
- Cache `_uniqueSuppliersFutureCache` invalid przy każdym refresh.

### 3-4. 📆 DatePicker Od / Do

- Filtr: `Data >= datePickerOd AND Data <= datePickerDo`.
- Łączone z innymi filtrami (AND).
- Pole pojedyncze: tylko Od lub tylko Do działa.

### Wszystkie filtry łączą się AND

W `ApplyFilters()`:
```
LIKE(Dostawca, search) 
  AND Data >= Od 
  AND Data <= Do 
  AND Dostawca IN (UniqueFuture)
```

---

## 5. Menu kontekstowe wstawień (PPM)

```
┌────────────────────────────────────────┐
│ ✏️  Edytuj wstawienie                  │
│ ➕  Nowe wstawienie (kopiuj dane)      │
│ 📅  Zmień datę wstawienia              │
│ 💰  Zmień typ ceny                     │
│ ──────────────────────────────────── │
│ ✅  Potwierdzenie wstawienia            │
│ ↩️  Cofnij potwierdzenie               │
│ ──────────────────────────────────── │
│ 🗑️  Usuń wstawienie                    │
└────────────────────────────────────────┘
```

### 1. ✏️ Edytuj wstawienie

- Otwiera `WstawienieWindow` w trybie **Modyfikacja** (instr. 01).
- SELECT 1 wiersz: Lp, Dostawca, DataWstawienia, IloscWstawienia.
- Po zapisie odświeża listę.

### 2. ➕ Nowe wstawienie (kopiuj dane)

- Wywołuje `PobierzDaneOstatniegoDostarczonego(dostawca)`:
  ```sql
  SELECT TOP 1 ... FROM HarmonogramDostaw + WstawieniaKurczakow 
  WHERE Dostawca = X AND wszystkie dostawy < dziś 
  ORDER BY DataOdbioru DESC
  ```
- Jeśli brak ostatnich zrealizowanych → bierze **TOP 1 najwcześniejszą**.
- Otwiera `OknoKopiowaniaDanychDialog` z 2 przyciskami:
  - **TAK, SKOPIUJ** → przekazuje pełne dane do nowego okna (tryb C, instr. 01).
  - **TYLKO PODSTAWOWE** → przekazuje tylko Dostawca (tryb B).

### 3. 📅 Zmień datę wstawienia

- Otwiera `OknoZmianyDatyWstawieniaDialog` z DatePicker.
- Liczy `(nowaData - staraData).Days` = offset.
- Pyta: **"Czy przesunąć też dostawy o tę samą liczbę dni?"**
  - **TAK** → `UPDATE HarmonogramDostaw SET DataOdbioru = DATEADD(DAY, @RoznicaDni, DataOdbioru) WHERE LpW = @LP`
  - **NIE** → tylko `UPDATE WstawieniaKurczakow SET DataWstawienia`
- Cache dostaw invalidated.

### 4. 💰 Zmień typ ceny

- Otwiera `WybierzTypCenyDialog` — 4 duże przyciski (łączona / rolnicza / wolnyrynek / ministerialna).
- Transakcja:
  ```sql
  UPDATE WstawieniaKurczakow SET TypCeny = @TC
  UPDATE HarmonogramDostaw SET typCeny = @TC WHERE LpW = @LP
  ```
- Refresh kolumny "Cena" we wszystkich wierszach.

### 5. ✅ Potwierdzenie wstawienia

- Wywołuje `PotwierdzWstawienie(lp)`:
  ```sql
  UPDATE WstawieniaKurczakow 
  SET isConf = 1, DataConf = GETDATE(), KtoConf = @UserID 
  WHERE Lp = @LP
  ```
- Jeśli **już potwierdzone** → pyta o aktualizację (zmiana osoby potwierdzającej).
- Pojawia się avatar w kolumnie "Potw. kto".

### 6. ↩️ Cofnij potwierdzenie

- Sprawdza `isConf = true`, pyta o potwierdzenie:
  > "Cofnąć potwierdzenie wstawienia LP=X?"
- `UPDATE ... SET isConf = 0, DataConf = NULL, KtoConf = NULL`

### 7. 🗑️ Usuń wstawienie

- Pyta: **"Czy usunąć wstawienie LP=X (Dostawca: Y)?"**
- Kaskadowe DELETE:
  1. `DELETE FROM HarmonogramDostaw WHERE LpW = @LP`
  2. `DELETE FROM WstawieniaKurczakow WHERE Lp = @LP`
- **NIE usuwa** `ContactHistory` (historia kontaktów zostaje).

---

## 6. Środkowy panel — ⏰ Przypomnienia

### Skąd się biorą

System **automatycznie** generuje przypomnienia z widoku `v_WstawieniaDoKontaktu` (logika: pierwsza dostawa <= dziś + 3 dni → przypomnienie aktywne).

Dwie kategorie:
- **Aktywne** (`Oczekuje = 0`): czerwone tło (RGB 255,235,238), czerwona gruba czcionka — **dzwonić TERAZ**.
- **Oczekujące** (`Oczekuje = 1`, snoozed): szare tło (RGB 236,240,241), czarna czcionka.

ToolTip: `OstatNotatka` (np. "Nie odebrał 3x", "Na wakacjach do 15.06").

### Kolumny

| # | Kolumna | Binding | Szer. | Co |
|---|---|---|---|---|
| 1 | **LP** | LP | 46px | Numer wstawienia |
| 2 | **Data** | Data | 66px | `MM-dd ddd` |
| 3 | **Hodowca** | Dostawca | 95px | Imię i nazwisko |
| 4 | **Ilość** | Ilosc | 64px | Sztuk w cyklu |
| 5 | **Tel** | Telefon | 96px | `PhoneFormatConverter` (`+48...` → `+48 xxx xxx xxx`) |
| 6 | **📝** | IleProb | 40px | `IleProbConverter` ("3 not.") |
| 7 | **Za** | ZaDni | 46px | `ZaDniConverter` ("15 dni") |

### Skąd numer telefonu

Phone1 z tabeli `Dostawcy`:
```sql
SELECT d.Phone1 FROM dbo.Dostawcy d WHERE d.ShortName = v.Dostawca
```
Fallback NULL → wyświetla "-".

### Menu kontekstowe Przypomnień (PPM)

```
┌──────────────────────────────────────────┐
│ 📵  Nie odebrał (+3 dni)                 │
│ 📞  3 próby telefonu (+1 miesiąc)        │
│ ───────────────────────────────────── │
│ 🕐  Odłożenie na dłużej…                 │
│ ➕  Dodanie numeru hodowcy               │
│ ───────────────────────────────────── │
│ 📞  Zadzwoń (Łącze z telefonem)          │
│ ───────────────────────────────────── │
│ 📱  Wyślij SMS (schowek)                 │
│ 📲  Wyślij SMS (Łącze z telefonem)       │
│ 📋  Kopiuj numer telefonu                │
└──────────────────────────────────────────┘
```

### Co dokładnie robi każda opcja

#### 1. 📵 Nie odebrał (+3 dni)

`AddContactHistory(lp, dostawca, DateTime.Today.AddDays(3), "Brak kontaktu")`. Przypomnienie znika, wraca za 3 dni.

#### 2. 📞 3 próby telefonu (+1 miesiąc)

`AddContactHistory(lp, dostawca, DateTime.Today.AddMonths(1), "3 próby telefonu - nadal brak kontaktu")`.

#### 3. 🕐 Odłożenie na dłużej…

Otwiera `OknoOdlozeniaDialog`:
- **TextBox**: liczba miesięcy (0-60, auto-update DatePicker).
- **DatePicker**: konkretna data.
- **TextBox**: notatka (opcjonalna).
- Po zapisie → INSERT do ContactHistory.

#### 4. ➕ Dodanie numeru hodowcy

Otwiera `OknoDodaniaNumeruDialog`:
- 3 pola: Phone1, Phone2, Phone3.
- Pre-fill z `SELECT Phone1, Phone2, Phone3 FROM Dostawcy WHERE ShortName = X`.
- Walidacja: co najmniej 1 numer.
- Po zapisie: `UPDATE Dostawcy SET Phone1, Phone2, Phone3`.

#### 5. 📞 Zadzwoń (Łącze z telefonem)

- Próbuje `tel:` URI (ProcessStartInfo + UseShellExecute = true).
- Fallback Windows Phone Link: `ms-phone://call?phonenumber={Uri.EscapeDataString(telefon)}`.
- Catch: "Nie udało się — skopiowaliśmy numer do schowka".

#### 6. 📱 Wyślij SMS (schowek)

`PrzygotujTrescSms()` — szablon:
> "Dzien dobry, kontaktujemy sie w sprawie kolejnego wstawienia kurczakow. Wg naszych danych ostatnie wstawienie: {data}, {ilosc} szt. Prosimy o informacje czy planuje Pan kolejne wstawienie i na kiedy. Pozdrawiamy, Piorkowscy."

Klipboard → wklejasz w komunikatorze.

#### 7. 📲 Wyślij SMS (Łącze z telefonem)

`sms:` URI lub `ms-phone://sms?phonenumber={escaped}`. Otwiera aplikację SMS na komputerze (Windows Phone Link wymaga sparowany telefon Android), treść też w schowku.

#### 8. 📋 Kopiuj numer telefonu

`Clipboard.SetText(telefon)`. Wkleisz gdzie chcesz.

---

## 7. Prawy panel — 📜 Historia kontaktów

### Skąd dane

```sql
SELECT TOP 500 ... FROM ContactHistory 
WHERE CreatedAt >= DATEADD(DAY, -90, GETDATE())
ORDER BY ContactDate DESC, CreatedAt DESC, ContactID DESC
```

Każdy ruch z panelu Przypomnień (Nie odebrał / 3 próby / Odłóż / SMS) zostawia wpis.

### Kolumny

| # | Kolumna | Binding | Szer. | Co |
|---|---|---|---|---|
| 1 | **Hodowca** | Dostawca | 0.6* | Z którym dzwoniłeś |
| 2 | **Kto** | UserName | 38px | Avatar (18×18 indigo `#6366F1`) lub Ellipse fallback |
| 3 | **Nast.** | SnoozedUntil | 46px | `MM-dd` — kiedy następne przypomnienie |
| 4 | **Notatka** | Reason | 1.0* | TextWrapping, max 40px, ellipsis |
| 5 | **Kiedy** | CreatedAt | 72px | `yyyy-MM-dd` — kiedy zapis powstał |

### Podświetlenie świeżych

`DataGridHistoria_HighlightRow()`:
- **CreatedAt >= ostatnie 90 dni**: czerwone tło (RGB 255,235,238), czerwona czcionka, bold.
- **Starsze**: białe tło, szara czcionka.

### Menu kontekstowe Historii (PPM)

- **✏️ Edytuj notatkę** — otwiera `OknoEdycjiNotatkiHistoriiDialog` (TextBox z obecną notatką) → UPDATE.
- **🗑️ Usuń wpis** — pyta potwierdzenie → DELETE.

### Double-click

Otwiera `ShowDetailWindow` z `BuildHistoriaDetailContent` — szczegóły wpisu: Hodowca, User, SnoozedUntil, Reason, CreatedAt.

---

## 8. Górne przyciski

### 📊 Statystyki

`BtnStatystyki_Click` → otwiera nowe okno `StatystykiPracownikow`.

Co pokazuje (dla każdego pracownika w okresie):
- Ile **wstawień** dodał.
- Ile **kontaktów** przeprowadził (kliknięć w panelu Przypomnień).
- Ile wstawień **potwierdził**.

Dla kierownika — kto pracuje, kto stoi.

### ❓ Instrukcja

`BtnPomoc_Click` → otwiera duże okno (900x700) z **11 sekcjami**:
1. Co to jest, 2. Statystyki, 3. Lista wstawień, 4. Dostawy, 5. Przypomnienia, 6. Historia, 7. Dodawanie, 8. Edycja, 9. Zmiana daty, 10. Usuwanie, 11. Porady.

Każda sekcja: zielony nagłówek (Border #5C8A3A) + biały tekst (`AddInstrukcjaSection()`).

---

## 9. Toast notifications (po zapisie wstawienia)

Po zamknięciu okna edycji (`WstawienieWindow.ZapisanoSukces == true`) wywoływany jest `PokazToastSukces`:

```
"✅ Zapisano • {ostrzezenieAuta} {notatki}"
```

`ostrzezenieAuta`:
- "" (brak) jeśli `ZapiszInfoAuta == true`.
- "(auta nie zapisane — Wolnyrynek)" jeśli `ZapiszInfoAuta == false`.

`notatki`:
- "({ZapiszIloscNotatek} notatek)" jeśli > 0.

### Pozycja toast

`PokazToast` w prawym dolnym rogu (HorizontalAlignment.Right, VerticalAlignment.Bottom, Margin 24,24).

Kolory:
- 🟢 Zielony (RGB 46,204,113) przy sukcesie.
- 🟠 Pomarańczowy (RGB 243,156,18) przy ostrzeżeniu.

Animacje:
- Fade-in 180ms.
- Wyświetlanie 2.5s.
- Fade-out 250ms.
- Grid.RowSpan + ColumnSpan = pokrywa całe okno (Z-index 9999).
- DispatcherTimer auto-hide.

---

## 10. Skróty klawiszowe

`Window_PreviewKeyDown`:

| Skrót | Akcja |
|---|---|
| **Ctrl+N** | `BtnDodaj_Click()` — nowe wstawienie |
| **Ctrl+F** | `textBoxFilter.Focus()` + SelectAll |
| **F5** | `RefreshAll()` — refresh wszystkich 4 źródeł |
| **Delete** | `MenuUsun_Click()` (focus na DataGrid wstawień) |
| **Enter** | `MenuEdytuj_Click()` (focus na DataGrid wstawień) |
| **Escape** | Zamknij otwarty tooltip / menu |

`Window_PreviewMouseLeftButtonDown`:
- Zamyka tooltip przy kliknięciu poza nim.

---

## 11. Cache mechanizmy

### `_deliveryCache` (60 sekund)

Cache dostaw — JOIN HarmonogramDostaw + WstawieniaKurczakow.

```csharp
const int DELIVERY_CACHE_TTL_SECONDS = 60;
```

Co zawiera: `LpW, DataOdbioru, Auta, SztukiDek, WagaDek, Cena, typCeny, bufor, DataWstawienia`.

Liczy `RoznicaDni = (DataOdbioru - DataWstawienia).Days`.

> 💡 **Po co**: eliminacja **N+1 problemu** w LoadingRow — bez cache każdy wiersz wstawienia robiłby osobny SELECT dostaw.

Używany przez:
- `GetDeliveryStatus(lp)` — enum: NoDeliveries, AllPast, Ongoing, AllFuture.
- `GetDeliveryDetails(lp)` — pełna lista dostaw dla tego wstawienia.

### `_uniqueSuppliersFutureCache`

Cache unikalnych hodowców z przyszłymi dostawami (dla checkboxa "Tylko przyszłe").

### `_avatarBrushCache`

Static, shared między WidokWstawienia i innymi modułami. Avatary użytkowników.

---

## 12. Convertery WPF (jak działają kolumny)

| Converter | Co przekształca | Przykład |
|---|---|---|
| `IleProbConverter` | int → "X not." \| "" | 3 → "3 not." |
| `ZaDniConverter` | int → "1 dzień" \| "X dni" \| "" | 5 → "5 dni" |
| `KontaktFreshnessConverter` | DateTime → Brush | 5d temu → zielony |
| `KontaktFreshnessTooltipConverter` | DateTime → string | "Ostatni kontakt: 20.05 (5 dni temu)" |
| `IsConfConverter` | bool → "✓" \| "" | true → "✓" |
| `TypCenyToColorConverter` | string → Brush | "rolnicza" → zielony |
| `TypCenyToForegroundConverter` | string → White/Black | (kontrast) |
| `InitialsConverter` | "Jan Kowalski" → "JK" | |
| `PhoneFormatConverter` | "+48..." → "+48 xxx xxx xxx" | |
| `NonEmptyToVisibilityConverter` | null/empty → Collapsed | |

---

## 13. Inne ukryte funkcje

### `dataGridDoPotwierdzenia` (ukryty)

W XAML: `Visibility="Collapsed"`. Historycznie używany do listy wstawień do potwierdzenia.

- `LoadDoPotwierdzenia()` — SELECT WHERE `isConf IS NULL/0` i `DataWstawienia ±30 dni`.
- `MenuPotwierdzWstawienie_Click()` — przyspieszone potwierdzanie.

**Obecnie nieużywany** (kod zostaje na wypadek przywrócenia funkcji).

### `BuildWstawienieDetailContent()`

Generic `ShowDetailWindow` (500x450) z dynamicznie budowanym StackPanel — używany do podglądu szczegółów wpisu bez otwierania pełnego edytora.

Lista dostaw w widoku detalu (`📦 Zaplanowane dostawy`):
- **Dwuklik na dostawie** → `OtworzKalendarzDostawNaDacie()`:
  - Szuka istniejącego okna `WidokKalendarzaWPF`.
  - Jeśli istnieje → aktywuj + `NawigujDoDaty()`.
  - Jeśli nie → nowe okno z Loaded → NawigujDoDaty.

### Historia per dostawca

`LoadHistoriaForDostawca()` — specjalna wersja LoadHistoria bez 90-dniowego cutoff. Używana w widoku szczegółów hodowcy.

---

## 14. Wszystkie SQL query

### Główne wstawienia

```sql
SELECT TOP 100 
  w.Lp, w.Dostawca, w.DataWstawienia, w.IloscWstawienia,
  w.TypUmowy, w.TypCeny, w.KtoStwo, w.DataUtw,
  w.isCheck, w.isConf, w.KtoConf, w.DataConf,
  u.Nazwisko AS KtoConfName,
  (SELECT MAX(ch.ContactDate) FROM ContactHistory ch WHERE ch.LpWstawienia = w.Lp) AS OstatniKontakt
FROM WstawieniaKurczakow w
LEFT JOIN Uzytkownicy u ON u.Id = w.KtoConf
ORDER BY w.DataWstawienia DESC, w.Lp DESC
```

### Przypomnienia aktywne

```sql
SELECT DISTINCT v.LpWstawienia AS LP, ...
FROM v_WstawieniaDoKontaktu v
LEFT JOIN Dostawcy d ON d.ShortName = v.Dostawca
WHERE v.Aktywne = 1
```

### Przypomnienia oczekujące (snoozed)

```sql
SELECT ... FROM ContactHistory ch
WHERE ch.SnoozedUntil > GETDATE()
GROUP BY ch.LpWstawienia
```

### Historia

```sql
SELECT TOP 500 ContactID, Dostawca, UserName, UserID, 
  SnoozedUntil, Reason, CreatedAt
FROM ContactHistory
WHERE CreatedAt >= DATEADD(DAY, -90, GETDATE())
ORDER BY ContactDate DESC, CreatedAt DESC, ContactID DESC
```

---

## 15. Typowy poranek Justyny

```
08:00  Otwiera ZPSP → "🐔 Wstawienia Kurczaków"
       
08:01  Patrzy na środkowy panel:
       🔔 12 — 12 przypomnień do dziś
       
08:02  PPM na pierwszym (Wojtek, 26.05):
         → 📞 Zadzwoń (Łącze z telefonem)
       Wojtek odbiera, potwierdza jutrzejsze wstawienie.
       Justyna nie używa innych opcji (brak akcji = przypomnienie zostaje).
       
08:05  PPM na drugim (Marcin) → 📵 Nie odebrał (+3 dni).
       System przesuwa na 28.05.
       
08:07  PPM na trzecim (Mazur) → 📱 SMS (schowek).
       Klipboard ma gotowy tekst, Justyna wkleja w WhatsApp.
       
08:15  8 przypomnień done. Reszta to oczekujące — pomija.
       
08:20  Filtr lewy: ☑ Tylko przyszłe.
       Lista pokazuje 47 aktywnych wstawień (każdy hodowca raz).
       Sortuje po Data (kolumna 4) — najbliżsi pierwsi.
       
08:25  Sprawdza kogo trzeba zaczepić wcześniej (np. duża dostawa za 5 dni).
       
08:30  Klika 📊 Statystyki → widzi że Maja zrobiła 18 ruchów dziś, Sergiusz 3.
       
08:35  Kawa ☕
```

---

## 16. FAQ

**P: Dlaczego widzę tylko 100 wstawień, choć w bazie jest 5000?**
O: Domyślny limit TOP 100 (wydajność). Wpisz coś w filtr → ładuje wszystkie pasujące.

**P: Co znaczy zielona kropka w pierwszej kolumnie?**
O: Świeżość kontaktu z tym hodowcą. Zielona = ktoś z nim rozmawiał ≤7 dni temu.

**P: Wstawienie ma żółty wiersz — coś źle?**
O: Nie. Żółty = "część dostaw odebrana, część czeka" (cykl w trakcie). Normalne.

**P: Avatar pracownika nie pokazuje się.**
O: Pracownik nie ma ustawionego avatara w `UserAvatarManager`. Sprawdź w ustawieniach użytkownika.

**P: Czemu "Tylko przyszłe" pokazuje hodowcę dwa razy?**
O: Powinien pokazywać raz. Jeśli widzisz dwa wpisy — sprawdź czy nie są to dwa różne hodowcy o podobnych nazwiskach (kolumna Dostawca jest unique key).

**P: Co znaczy "Potwierdzenie wstawienia"?**
O: Druga osoba weryfikuje. Justyna wpisała, Sergiusz potwierdził. Audytowy ślad.

**P: Mogę dzwonić przez aplikację Windows Phone Link?**
O: Tak. PPM → 📞 Zadzwoń → wymaga sparowanego telefonu Android z Windows 11.

**P: SMS schowek vs Phone Link — co lepiej?**
O: **Schowek** = uniwersalne (działa w każdej aplikacji do komunikacji). **Phone Link** = otwiera bezpośrednio SMS na komputerze, jeśli sparowany telefon.

**P: Cache 60s — czy dane mogą być nieświeże?**
O: Tak, do 60 sekund po zmianie w bazie. F5 = wymusza refresh.

**P: Historia ma tylko 90 dni — co ze starszą?**
O: Starsza jest w bazie, ale nie pokazywana. Można wyciągnąć przez Statystyki lub bezpośrednio SQL.

**P: Czy 500 wpisów w historii to limit?**
O: Tak, TOP 500. Jeśli więcej — niektóre nie są pokazane (ale są w bazie).

---

## 17. Co dalej

- **Edytor wstawienia** (klik PPM → Edytuj) → `01_Wstawienia_Kurczakow.md`.
- **Partie ubojowe** (po dostawie) → `03_Lista_Partii.md`.
- **Kalendarz dostaw** (widok wszystkich dostaw w tygodniu) → `05_Kalendarz_Dostaw_Zywca.md`.
- **Karta hodowcy 360°** → `06_Baza_Hodowcow.md`.
