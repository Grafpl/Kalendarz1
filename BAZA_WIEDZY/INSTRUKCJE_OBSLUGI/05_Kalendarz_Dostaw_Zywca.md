# Instrukcja: Kalendarz dostaw żywca — deep

> **Dla kogo**: Justyna (planowanie), Maja (kontrola), Sergiusz (przegląd).
> **Co robi**: **tygodniowy planner dostaw żywca** z live-data (odświeżanie co 15s), edycją inline, drag&drop, 5 tabsami sidebar, notatkami z @mentions, trybem symulacji, 20+ skrótami klawiszowymi.
> **Plik kodu**: `Zywiec/Kalendarz/WidokKalendarzaWPF.xaml(.cs)`, `Services/HodowcyCacheManager.cs`.
> **Otwierane z**: menu ZPSP → **📅 Kalendarz dostaw**.

> ⚠ To **najbardziej rozbudowane okno w całym ZPSP** — production-grade z enterprise features. Ta instrukcja pokrywa wszystko.

---

## 1. Po co — w jednym zdaniu

> "Jutro Wojtek 6:00, środa Mazur 9:00, piątek Stachura 14:00 — zobacz cały tydzień na jednym ekranie, edytuj jednym dwuklikiem, widz na żywo co zmieniają inni."

---

## 2. Inicjalizacja (co się dzieje przy otwarciu)

Window_Loaded (asynchroniczny):
1. **Identyfikacja użytkownika** (UserName z parametru lub bazy).
2. **AuditLogService** — śledzenie zmian.
3. **Preferencje użytkownika** (KalendarzUserPreferences — zapamiętane filtry, kolumny).
4. **LoadAllDataAsync** — równoległy ładunek:
   - Dostawy bieżącego + następnego tygodnia.
   - Ceny + ceny dodatkowe.
   - Partie (wstawienia).
   - Pojemność tuszek.
   - Ostatnie notatki (7 dni).
   - Ranking hodowców (TOP 10).
5. **InitLiveAuditAsync** — polling co **15 sekund** do bazy (MAX(DataMod)).
6. **ChangeNotificationPopup** — subskrypcja popupów powiadomień.
7. **TryShowSurveyIfInWindow** — ankieta (okna 14:30-15:00 i 20:16-20:46).

Data startowa: **dzisiaj**. Tabela grupowana po dniach (Pn→Nd).

---

## 3. Layout

```
┌───────────────────────────────────────────────────────────────────────────────┐
│ [📅mini] [◀ Poprz.] [Dziś ✨] [Następ. ▶] [mini-mapa 9 tyg.] tyg.26 [🔽Filtry]│
├──────────────────┬─────────────────────────────────────┬───────────────────────┤
│ SIDEBAR (5 tabs)  │ BIEŻĄCY TYDZIEŃ (Pn-Nd)            │ NASTĘPNY TYDZIEŃ      │
│ - Dostawy         │                                    │ (toggle)              │
│ - Partie          │ Group by day:                      │                       │
│ - Wstawienia      │ [PONIEDZIAŁEK] header (sumy)        │ taka sama tabela      │
│ - Ranking         │   Wojtek | 2 | 10560 | 2.1 | ...   │                       │
│ - Notatki         │   Mazur  | 1 | 6200  | 2.0 | ...   │                       │
│                   │ [separator]                        │                       │
│                   │ [WTOREK] header (sumy)             │                       │
│ Kartoteka + szybki│   ...                              │                       │
│ edytor notatek    │                                    │                       │
└──────────────────┴─────────────────────────────────────┴───────────────────────┘
🟢 brdLiveWatch (miga co 15s)  ⏱ countdown 10 min do auto-refresh
```

---

## 4. Nawigacja

### Mini-kalendarz (`calendarMain`)

Klik na dowolny dzień → `NawigujDoDaty(data)` → async load tygodnia (z animacją slide).

### Przyciski tygodnia

- **◀ Poprz.** — -7 dni (animacja slide w prawo).
- **Dziś ✨** — skok do bieżącego tygodnia (świeci animacją).
- **Następ. ▶** — +7 dni (animacja slide w lewo).

### Mini-mapa tygodni (9 przycisków)

Pokazuje **9 tygodni naraz**:
- Wybrany tydzień: niebieski (#3B82F6).
- Bieżący tydzień: zielony (#10B981).
- Bieżący + wybrany: zielony z niebieską ramką.
- Scrollowanie: ◀ ▶ (zmienia offset).

### Numer tygodnia

ISO 8601 (poniedziałek = pierwszy dzień).

---

## 5. Tabela tygodnia — 9 kolumn + 2 toggle

| # | Nagłówek | Binding | Edycja | Co |
|---|---|---|---|---|
| 1 | **📅 Tydzień** | DostawcaDisplay | view | Nazwa hodowcy |
| 2 | **🚛** | AutaDisplay | dwuklik inline | Liczba aut |
| 3 | **🐔 Szt** | SztukiDekDisplay | dwuklik inline | Sztuki (białe tło jeśli nie potwierdzone) |
| 4 | **⚖️ Waga** | WagaDekDisplay | dwuklik inline | Waga (białe tło jeśli nie potwierdzona) |
| 5 | **📉 Ub** | RoznicaDniDisplay | read-only | Różnica dni, **CZERWONE** jeśli wstawienie nie potwierdzone |
| 6 | **📊 Typ** | TypCenyDisplay | dwuklik dropdown | Typ ceny (kolorowe tła) |
| 7 | **💰 Cena** | CenaDisplay | dwuklik inline | Cena jednostkowa |
| 8 | **🚗 KM** | KmDisplay | read-only | Kilometry transportu |
| 9 | **📝 Uwagi** | Uwagi | dwuklik textarea | Notatki + avatar autora |

Toggle (z filtra): checkboxy potwierdzeń (PotwWaga, PotwSztuki, IsWstawienieConfirmed).

### Grupowanie po dniach

Każdy dzień ma **nagłówek** (IsHeaderRow) z sumami:
- Suma aut, suma sztuk, średnia waga, średnia cena, liczba anulowanych/sprzedanych.
- Między dniami: separator (pusty wiersz).
- `RecalculateDayHeader()` przelicza sumy gdy zmieni się wiersz.

---

## 6. Kolory wierszy — 6 statusów

| Status | HEX | Kolor | Warunek |
|---|---|---|---|
| **Potwierdzony** (1) | #C8E6C9 | jasnozielony | Status==1, waga+sztuki potwierdzone |
| **Do wykupienia** (2) | #F5F5F5 | szary | Status==2 (domyślny) |
| **Anulowany** (3) | #FFCDD2 | jasnoczerwony | Status==3 |
| **Sprzedany** (4) | #BBDEFB | jasnoniebieski | Status==4 |
| **B.Wolny** (5) | #FFF9C4 | jasnożółty | Status==5 (broiler wolny) |
| **B.Kontr** (6) | #7E57C2 | fioletowy | Status==6 (broiler kontrakt) |

---

## 7. Dodawanie dostawy

### Sposób A: przycisk / Ctrl+N

`MenuItemNowaDostawaZDaty_Click` → otwiera okno `Dostawa` (WinForms) z datą = `_selectedDate`. Po zamknięciu: reload tabeli.

### Sposób B: PPM na nagłówku dnia

PPM na wierszu nagłówka → "➕ Nowa dostawa na ten dzień" → to samo co A.

> Pełne pola okna `Dostawa` (hodowca, auta, sztuki, waga, cena, typ, status, anomaly badges, konflikt daty) — w osobnym oknie poza zakresem tego pliku, ale logika analogiczna do edytora wstawień.

---

## 8. Edycja dostawy — 3 sposoby

### Sposób A: Dwuklik na komórkę (inline)

`DgDostawy_MouseDoubleClick` → HitTest wykrywa kolumnę:
- 🚛 Auta → `EditCellValueAsync(LP, "A")`
- 🐔 Szt → `EditCellValueAsync(LP, "Szt")`
- ⚖️ Waga → `EditCellValueAsync(LP, "Waga")`
- 📊 Typ → `EditTypCenyAsync()` (dropdown)
- 💰 Cena → `EditCenaAsync()`
- 📝 Uwagi → `EditNoteAsync(LP)` (textarea)

### Sposób B: PPM (menu kontekstowe — 20+ opcji)

```
📦 DOSTAWA
─ Operacje ─
  ➕ Nowa dostawa na ten dzień (Ctrl+N)
  📋 Duplikuj dostawę (Ctrl+D)
  📱 Kopiuj SMS o szczegółach
─ Zmiana daty ─
  ▲ Przesuń -1 dzień (-)
  ▼ Przesuń +1 dzień (+)
─ Potwierdzenia ─
  ✅ Potwierdź WAGĘ (Ctrl+W)
  ✅ Potwierdź SZTUKI (Ctrl+S)
  ❌ Cofnij potwierdzenie WAGI
  ❌ Cofnij potwierdzenie SZTUK
  🐣 Potwierdź WSTAWIENIE (orange)
  ❌ Cofnij potwierdzenie WSTAWIENIA
  ✏️ Edytuj WSTAWIENIE (blue)
─ Status ─
  ✅ Potwierdź dostawę (green)
  ❌ Anuluj dostawę (red)
─ Widoki/raporty ─
  ⚖️ Pokaż wagi (F2)
  🚛 Pokaż dostawy (F3)
  💰 Pokaż ceny (F4)
  💵 Dodaj cenę (Ctrl+P)
  🐣 Pokaż pasze/pisklęta (F5)
  🍗 Pokaż tuszkę (F6)
  📋 Pokaż avilog (F7)
  📈 Pokaż plan sprzedaży (F8)
  📜 Historia zmian dostawy (Ctrl+H)
─ Usuwanie ─
  🗑 Usuń dostawę (Del, red)
```

### Audit log

`AuditLogService` zapisuje: typ operacji (INSERT/UPDATE/DELETE), źródło (DragDrop/Bulk/Checkbox/InlineEdit/Dostawa), użytkownik, zmienione pole, stara→nowa wartość. `ShowOtherUsersChangesAsync` pokazuje live changes innych.

---

## 9. Drag & Drop (działa!)

Przesunięcie dostawy między dniami (w tygodniu lub między tygodniami):
1. PreviewMouseLeftButtonDown → zapis pozycji startu.
2. MouseMove > 5px → `_isDragging = true`, zmiana kursora.
3. PreviewMouseLeftButtonUp → **overlay potwierdzenia**:
   - "Przeniesienie dostawy" → hodowca, auta, data stara (przekreślona), data nowa (zielona).
   - [Anuluj] / [Przenieś].
4. Przenieś → SaveDate(LP, newDate) → UPDATE w bazie.

Blokady: menu kontekstowe otwarte (czeka 500ms), inline edit otwarty, tryb symulacji.

---

## 10. Filtry (ikona lejka 🔽)

| Filtr | Domyślnie |
|---|---|
| ☑ Pokaż dostawy | zaznaczone |
| ☑ Pokaż anulowane | niezaznaczone |
| ☑ Pokaż sprzedane | niezaznaczone |
| ☑ Pokaż do wykupienia | zaznaczone |
| ☑ Pokaż następny tydzień | toggle |
| ☑ Pokaż checkboxy | toggle (kolumny potwierdzeń) |

Persistencja: per-sesję + **zapisywane** w KalendarzUserPreferences (przeżywają zamknięcie).

---

## 11. Pięć tabsów sidebar

### 1. Dostawy (domyślny)

Główna tabela (dgDostawy + dgDostawyNastepny).

### 2. Partie

DataGrid `dgPartie` — lista wstawień (LpPartii, DataPartii, IloscPartii). `LoadPartieAsync()`.

### 3. Wstawienia

DataGrid `dgWstawienia` — szczegóły wstawienia dla wybranego hodowcy:
- ComboBox "Hodowca" (`cmbHodowcaWstawienia`) — autocomplete z HodowcyCacheManager.
- ComboBox "LpW" (`cmbLpWstawienia`) — wstawienia dla hodowcy.
- Pokazuje: suma sztuk, pozostałe (sztuki×0.97 - suma_dostaw).

### 4. Ranking

DataGrid `dgRanking` — TOP 10 hodowców wg liczby dostaw w okresie:
- Kolumny: Ranking, Hodowca, Liczba_dostaw, Ostatnia_dostawa.
- Selektor okresu (`dpRankingDate`).
- PPM "Pokaż historię" → filtruje główną tabelę dla tego hodowcy.

### 5. Notatki

DataGrid `dgNotatki` (dla wybranej dostawy) + `_ostatnieNotatki` (7 dni).
- Tworzenie: TextBox `txtQuickNote` — Enter wysyła.
- **@mentions**: wpisz `@` → popup `mentionsListBox` z operatorami → wstaw `@[id:Operator]`.
- **Reply**: PPM → "Odpowiedz na notatkę".
- **Badge** `btnMentionsBadge` — nieprzeczytane @mentions (polling co 60s).
- **Pulse**: notatki z ostatnich 3 dni pulsują.

---

## 12. Tryb symulacji (ukryta moc)

`_isSimulationMode` (toggle w toolbar):
- Backup danych (`_simulationBackup`).
- Zmiany lokalne (`_simulationChanges`), **NIE zapisywane do bazy**.
- Blokuje: D&D, live watch, zapis.
- Czerwona pulsująca ramka (SimulationPulseAnimation).
- "Anuluj symulację" → restore backup.

> Po co: testowanie "co jeśli przesunę 3 dostawy" bez ruszania prawdziwych danych.

---

## 13. Live data + notyfikacje

- **LiveWatchTickAsync** co 15s → sprawdza MAX(DataMod).
- **ShowOtherUsersChangesAsync** → floating popup ze zmianami innych.
- **ChangeNotificationPopup** → klik = skok do dostawy (popup w prawym dolnym rogu — feedback z memory).
- Countdown 10 min do pełnego auto-refresh.

---

## 14. Wszystkie skróty klawiszowe (20+)

| Skrót | Akcja | Skrót | Akcja |
|---|---|---|---|
| Ctrl+N | Nowa dostawa | F2 | Pokaż wagi |
| Ctrl+D | Duplikuj | F3 | Pokaż dostawy |
| Ctrl+H | Historia zmian | F4 | Pokaż ceny |
| Ctrl+P | Dodaj cenę | F5 | Pokaż pasze/pisklęta |
| Ctrl+W | Potwierdź wagę | F6 | Pokaż tuszkę |
| Ctrl+S | Potwierdź sztuki | F7 | Pokaż avilog |
| Ctrl+F | Filtr (open) | F8 | Plan sprzedaży |
| Del | Usuń dostawę | +/- | Data ±1 dzień |
| Alt+→ | Następny tydzień | Alt+← | Poprzedni tydzień |
| Alt+↑ | Dzisiaj | Enter (notatka) | Wyślij notatkę |

---

## 15. HodowcyCacheManager

- **TTL 30 minut**: `SELECT Name FROM Dostawcy WHERE Halt='0'`.
- **Auto-invalidacja**: tani `COUNT(*)` check — jeśli COUNT zmienił się, cache invalid nawet przed TTL.
- Thread-safe (ReaderWriterLockSlim).
- `Invalidate()` — wymuś refresh (po edycji hodowcy w innym module).
- `Search(text)` — szuka w cache (nie w DB).
- F5 / `BtnRefreshHodowcy_Click` → `GetAsync(forceReload: true)`.

---

## 16. Animacje (8 storyboardów)

| Animacja | Kiedy |
|---|---|
| TodayPulse | Nagłówek dzisiejszego dnia |
| WeekSlideIn/Out | Transicja tygodni |
| LpWMatchPulse | Wiersze z tym samym LpW (hover) |
| RecentNotePulse | Notatki z ostatnich 3 dni |
| SimulationPulse | Tryb symulacji (czerwona ramka) |
| CheckmarkAppear | Pojawienie checkmark |
| SuccessFlash | Flash zielony przy sukcesie |
| SlideInFromRight | Toast notifications |

---

## 17. Pojemność dnia

Limit **80 000 szt/dzień** używany do walidacji/ostrzeżeń (głównie w oknie `Dostawa` przy dodawaniu). W kalendarzu sumy dnia są w nagłówkach — możesz porównać wzrokowo.

> Pasek capacity wprost (jak w edytorze wstawień, instr. 01) jest planowany w audycie NF03.

---

## 18. Typowy dzień Justyny

```
06:30  Otwiera Kalendarz dostaw. Bieżący tydzień:
       Pn: 3 (2 zielone, 1 żółta), Wt: 4 (żółte), Śr: 2 (zielone).
06:35  Filtr "tylko do wykupienia" — 5 do potwierdzenia.
06:40  PPM Wojtek → ✅ Potwierdź dostawę (po telefonie).
06:50  Dwuklik Uwagi Mazura → "po 10:00, brama techniczna".
07:00  Sergiusz: "dodaj jutrzejszą partię Krzyśka".
       Ctrl+N → wpis. Ostrzeżenie: "wtorek 80k limit".
       Dodaje z notatką "PILNE".
07:15  Tab Ranking — Wojtek nr 1, Mazur spada.
07:20  Następny tydzień — większość żółta, notatka mentalna.
07:25  Tryb symulacji: testuje przesunięcie 3 dostaw na inny dzień.
       Wygląda OK → anuluje symulację, robi naprawdę przez D&D.
07:30  ☕
```

---

## 19. FAQ

**P: Skąd dostawy?**
O: Z `HarmonogramDostaw` (tworzone przy cyklu wstawienia, instr. 01) lub ręcznie tutaj.

**P: Dodałem dostawę, nie widzę.**
O: Sprawdź filtry. F5.

**P: Drag&drop między dniami?**
O: Tak. Przeciągnij wiersz → overlay potwierdzenia → Przenieś.

**P: Co znaczy "Ub" czerwone?**
O: Wstawienie nie jest potwierdzone (RoznicaDniDisplay czerwony).

**P: B.Wolny vs Wolnyrynek?**
O: B.Wolny = bezumowny wolny (wierny). B.Kontr = bezumowny kontraktowy.

**P: Tryb symulacji — co to?**
O: Testowanie zmian bez zapisu. Czerwona ramka. Anuluj = przywróć.

**P: @mentions?**
O: W notatce wpisz @ → wybierz operatora → dostaje powiadomienie (badge).

**P: Cache hodowców dziwny?**
O: F5 wymusza refresh (30 min TTL).

**P: WPF czy WinForms?**
O: WPF aktywny (`WidokKalendarzaWPF`). Stary `WidokKalendarza.cs` = legacy.

---

## 20. Co dalej

- **Cykl wstawienia** → `01_Wstawienia_Kurczakow.md`.
- **Lista cykli** → `02_Lista_Wstawien.md`.
- **Partia** → `03_Lista_Partii.md`.
- **Karta hodowcy** → `06_Baza_Hodowcow.md`.
