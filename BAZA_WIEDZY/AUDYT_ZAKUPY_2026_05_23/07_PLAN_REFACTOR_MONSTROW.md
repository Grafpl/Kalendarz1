# Część 7 (dodatek) — Plan refactoru plików-monstrów

**Cel:** uporządkować 5 plików > 3000 linii bez ryzyka regresji produkcyjnych. **Q3-Q4 2026, nie pod Magdę.**

**Reguła nadrzędna:** **NIE refaktorujemy całości na raz.** Małe, izolowane partials + testy ręczne między zmianami.

---

## TOP 5 plików-monstrów (z Części 1 audytu)

| Plik | Linie | Stan | Pilność refactoru |
|---|---:|---|---|
| `Zywiec/WidokSpecyfikacji/WidokSpecyfikacje.xaml.cs` | **16 255** | 🔴 KRYT | Wysoka (16k linii = każda zmiana ryzyko) |
| `Zywiec/Kalendarz/WidokKalendarzaWPF.xaml.cs` | **9 631** | 🔴 KRYT | Wysoka (6 timerów, race conditions) |
| `Zywiec/WstawieniaKurczaka/WidokWstawienia.xaml.cs` | **5 333** | 🟡 ŚR | Średnia (Magda używa codziennie — ryzyko regresji wysokie) |
| `Portiernia/PanelPortiera.xaml.cs` | **4 231** | 🟡 ŚR | Średnia (PIN security issue) |
| `Hodowcy/PozyskiwanieHodowcowWindow.xaml.cs` | **3 312** | 🟢 NICE | Niska (rzadko używane, leady) |

**Łącznie: 38,7k linii w 5 plikach** = ~40% kodu UI w module zakup.

---

## Strategia ogólna — "extract small services"

**Bez przepisywania na MVVM.** Code-behind zostaje, ale ciężka logika idzie do osobnych klas service.

### Pattern dla każdego pliku:

1. **Faza A — partial classes** (1 dzień): rozbij `.xaml.cs` na 3-5 partial files po obszarach funkcjonalnych
2. **Faza B — extract services** (2-3 dni): wyciąg ciężkich metod (SQL, business logic) do osobnych `*Service.cs`
3. **Faza C — extract helpers** (1 dzień): extension methods, helpers, constants do osobnego pliku
4. **Faza D — usuń duplikaty** (1 dzień): consolidacja powtarzających się metod (np. 5× connection string)

**Razem per plik: 5-6 dni Sera**, **rozłożone na 4-6 tygodni** (po godzinie dziennie, testowane na bieżąco).

---

## Plan szczegółowy per plik

### 1. WidokSpecyfikacje.xaml.cs — **16 255 linii**

**Co tam jest (z głębokiego rzutu okiem):**
- ~3000 linii: bindings + filtry + DataGrid manipulacje
- ~4000 linii: generacja PDF (iTextSharp)
- ~2000 linii: Outlook interop (wysyłka email)
- ~2000 linii: edycja specyfikacji (formularz CRUD)
- ~1500 linii: importy i mapowania
- ~2000 linii: analizy i podsumowania (multi-tab)
- ~1500 linii: helpers, conversion, formatting
- ~250 linii: konstruktor, event handlery, lifecycle

**Plan rozbicia (12-14 dni Sera, rozłożone Q3 2026):**

#### Faza A — 5 partial files (2 dni)
```
WidokSpecyfikacje.xaml.cs        (główny, ~3000 linii — bindings + lifecycle)
WidokSpecyfikacje.Filtry.cs      (filtry + chip buttons + search, ~2000 linii)
WidokSpecyfikacje.PDF.cs         (cała generacja PDF, ~4000 linii)
WidokSpecyfikacje.Email.cs       (Outlook interop, ~2000 linii)
WidokSpecyfikacje.Edycja.cs      (CRUD formularz, ~2000 linii)
WidokSpecyfikacje.Import.cs      (mapowania, ~1500 linii)
WidokSpecyfikacje.Analizy.cs     (multi-tab, ~2000 linii)
```

#### Faza B — extract services (5-7 dni)
```
Zywiec/WidokSpecyfikacji/Services/
├── SpecyfikacjeRepository.cs      (CRUD SQL)
├── SpecyfikacjePdfGenerator.cs    (iTextSharp wrapper)
├── SpecyfikacjeEmailSender.cs     (Outlook interop wrapper)
├── SpecyfikacjeFilterService.cs   (logika filtrów)
└── SpecyfikacjeImportService.cs   (mapowania)
```

Każdy service ma jasne wejście/wyjście (DTO + Task<T>), bez referencji do UI elementów.

#### Faza C — extract converters + helpers (1 dzień)
```
Zywiec/WidokSpecyfikacji/Converters/
├── ZeroToEmptyConverter.cs       (już jest jako inline w XAML — wyciąg)
├── PercentToPixelConverter.cs    (jw.)
└── ...
```

#### Faza D — quick wins (2 dni)

- Wspólny `InfoBanner` style w App.xaml (już zaplanowane w Części 2 weekend)
- Dodanie przycisku "📋 Skopiuj z poprzedniej" (Część 2, RANK A1)
- Walidacje numeryczne (cena, % ubytku)

**Po refactorze:** zmiana funkcji = zmiana 1 service'u, nie 16k linii.

---

### 2. WidokKalendarzaWPF.xaml.cs — **9 631 linii**

**Co tam jest:**
- ~3000 linii: kalendarz miesięczny + komórki + bindings
- ~1500 linii: 6 timerów (refresh, ceny, mentions, audit, countdown, survey)
- ~1500 linii: dialog edycji dostawy
- ~1000 linii: live audit notifications
- ~1500 linii: ranking dostaw
- ~1000 linii: SMS i email
- ~131 linii: lifecycle, ctor

**Plan rozbicia (10-12 dni):**

#### Faza A — partial files (2 dni)
```
WidokKalendarzaWPF.xaml.cs           (główny, kalendarz)
WidokKalendarzaWPF.Timers.cs         (wszystkie 6 timerów + cancellation tokens)
WidokKalendarzaWPF.Audit.cs          (live notifications + mentions)
WidokKalendarzaWPF.Ranking.cs        (logika ranking dostaw)
WidokKalendarzaWPF.Sms.cs            (SMS + email)
WidokKalendarzaWPF.Edycja.cs         (dialog edycji)
```

#### Faza B — services (4-5 dni)
```
Zywiec/Kalendarz/Services/
├── KalendarzRepository.cs           (CRUD)
├── KalendarzRefreshOrchestrator.cs  (zarządza 6 timerami centralnie)
├── KalendarzAuditPoller.cs          (live audit)
├── KalendarzRankingService.cs       (logika scoring)
└── KalendarzSmsService.cs           (już używa Services/SmsService.cs)
```

#### Faza C — TODO drukowania PDF (4h)
Naprawić TODO z linii ~11247 → faktyczna implementacja eksportu.

#### Faza D — naprawa race conditions (2 dni)
- `_dostawy` ObservableCollection bez locka → ReaderWriterLockSlim
- `_lastSeenAuditId` w transakcji
- CancellationToken w każdym timerze

---

### 3. WidokWstawienia.xaml.cs — **5 333 linii**

**Co tam jest:**
- ~2000 linii: DataGrid + master-detail
- ~1500 linii: tooltipy + ich cache (40-liniowa funkcja close)
- ~800 linii: 3 timery (refresh, mention polling, tooltip close)
- ~500 linii: cache hodowców + avatary
- ~500 linii: filtry + search
- ~33 linii: pozostałe

**Plan rozbicia (6-7 dni):**

#### Faza A — partial files (1 dzień)
```
WidokWstawienia.xaml.cs           (główny)
WidokWstawienia.Tooltipy.cs       (logika tooltipów)
WidokWstawienia.Timery.cs         (3 timery + cancellation)
WidokWstawienia.Cache.cs          (avatary + hodowcy)
```

#### Faza B — services (3 dni)
```
Zywiec/WstawieniaKurczaka/Services/
├── WstawieniaRepository.cs
├── PotwierdzeniaService.cs       (mock dla #3 — będzie obsługiwać dowody)
└── AvatarCache.cs                (LRU cache + thread-safe)
```

#### Faza C — walidatory (1 dzień)
Już QW6 w Części 5 (weekend). Po refactorze — przenieść do `Validators/`.

#### Faza D — nowa funkcja pod ARiMR (2 dni)
Pole "Potwierdzenie hodowcy" w UI + przycisk "📎 Załącz screenshot" (zapisuje do `\\server\Potwierdzenia_Wstawien\`).

---

### 4. PanelPortiera.xaml.cs — **4 231 linii**

**Co tam jest:**
- ~1500 linii: dotykowy UI (3 zakładki + klawiatura numeryczna)
- ~1000 linii: timery (auto-refresh, auto-logout, kamera CCTV)
- ~800 linii: wagi (Brutto/Tara) + walidacje
- ~500 linii: PIN + auth
- ~400 linii: print + sound effects
- ~31 linii: pozostałe

**Plan rozbicia (5-6 dni):**

#### Faza A — partial files (1 dzień)
```
PanelPortiera.xaml.cs           (główny)
PanelPortiera.Timery.cs         (3 timery)
PanelPortiera.Wagi.cs           (logika brutto/tara/netto)
PanelPortiera.Pin.cs            (PIN + auth + bezpieczeństwo)
```

#### Faza B — KRYTYCZNE security fix (2 dni)
- **Usuń hardcoded PIN "1994"** (Część 1 audytu — security issue)
- Wprowadź PIN per portier (tabela `PortierPin`)
- Audit log każdego użycia PIN
- Wprowadź **dane firmy z bazy** (NIP, REGON, adres) zamiast hardcoded
  - To pod transformację sp. z o.o. (01.08.2026) — krytyczne!

#### Faza C — services (2 dni)
```
Portiernia/Services/
├── WagiRepository.cs
├── PortierAuthService.cs       (PIN + audit)
└── KameraCctvService.cs        (osobno, opcjonalnie)
```

#### Faza D — walidacja UX (1 dzień)
- Warning przed auto-logout (5 min) — daj portierowi 30 sek na anulowanie
- Walidator: brutto > tara, sensowne zakresy

---

### 5. PozyskiwanieHodowcowWindow.xaml.cs — **3 312 linii**

**Co tam jest:**
- ~1000 linii: DataGrid 1874 leadów + filtry
- ~800 linii: szablony rozmów (8 hardcoded)
- ~600 linii: mapowanie wojewódzkie po PNA (duży hardcoded dict)
- ~400 linii: avatary
- ~300 linii: pozostałe

**Plan rozbicia (4-5 dni — najniższy priorytet z 5):**

#### Faza A — partial (1 dzień)
```
PozyskiwanieHodowcowWindow.xaml.cs   (główny)
PozyskiwanieHodowcowWindow.Templates.cs  (szablony rozmów)
PozyskiwanieHodowcowWindow.Wojewodztwa.cs (mapowanie PNA)
```

#### Faza B — wyciągnij szablony i mapowanie do DB (2 dni)
- Tabela `PozyskiwanieSzablonyRozmow` (id, nazwa, tresc, kategoria, aktywny)
- Tabela `PolskieKodyPocztowe` (PNA, miejscowosc, wojewodztwo) — istnieje gotowa baza PNA, da się zaimportować
- UI do edycji szablonów

#### Faza C — paging (1 dzień)
- DataGrid z paging zamiast 1874 rekordów na raz
- LRU cache avatary

#### Faza D — scalenie z DOSTAWCY (2 dni, opcjonalnie Q4)
**Najbardziej ambitne:** scalić `Pozyskiwanie_Hodowcy` (leady) i `DOSTAWCY` (aktywni) w jedną tabelę z flagą `IsLead`/`IsActive`. Eliminuje całą dublację z Części 1 audytu.

---

## Harmonogram total (Q3-Q4 2026)

| Tydzień | Co | Plik |
|---|---|---|
| **Q3-1** | Faza A + B Specyfikacji | WidokSpecyfikacje |
| **Q3-2** | Faza B + C Specyfikacji | WidokSpecyfikacje |
| **Q3-3** | Faza D Specyfikacji | WidokSpecyfikacje |
| **Q3-4** | Faza A + B Kalendarza | WidokKalendarzaWPF |
| **Q3-5** | Faza B + C Kalendarza (TODO PDF) | WidokKalendarzaWPF |
| **Q3-6** | Faza D Kalendarza (race conditions) | WidokKalendarzaWPF |
| **Q3-7** | Faza A + B Wstawień | WidokWstawienia |
| **Q3-8** | Faza D Wstawień (potwierdzenia) | WidokWstawienia |
| **Q4-1** | Faza A + B Portiera (security!) | PanelPortiera |
| **Q4-2** | Faza C + D Portiera | PanelPortiera |
| **Q4-3** | Faza A + B Pozyskiwania | PozyskiwanieHodowcow |
| **Q4-4** | Faza C Pozyskiwania | PozyskiwanieHodowcow |
| **Q4-5** | (opcjonalnie) Scalenie tabel hodowców | PozyskiwanieHodowcow + DOSTAWCY |
| **Q4-6** | Bufor / fix-ups / regression testing | wszystkie |

**Razem: 14 tygodni** = ~3,5 miesiąca pracy Sera + ~2-3h/tydzień testów Magdy/Asi.

---

## ⚠️ ZASADY ŻELAZNE refactoru

1. **Nigdy nie refaktor PRZED Magdą.** Magda przychodzi 26.05 — pierwsze 4-6 tygodni stabilizacja, dopiero potem ruszamy z monstrami.
2. **Jedna faza = jeden commit = test ręczny przed kolejną fazą.**
3. **Zawsze backup `.cs` przed Fazą A** (git branch). Roll-back w 5 minut.
4. **Magda/Asia testują 1 dzień każdy plik** po refactor. Jeśli coś działa inaczej — fix przed merge do main.
5. **Nie zmieniamy XAML w trakcie refactor `.cs`.** Wizualnie ma być identycznie.
6. **Każdy nowy service ma async + cancellation token.** Bez wyjątków.
7. **Nie usuwamy `Debug.WriteLine`** — to jedyna telemetria. Migracja do structured logging w Q1 2027.

---

## Metryki sukcesu po Q4 2026

- [ ] **Top 5 plików: średnio 1500 linii** (z 7700 obecnie)
- [ ] **Zero TODO komentarzy** w produkcji
- [ ] **Zero hardcoded PIN-ów / hardcoded danych firmy**
- [ ] **Wszystkie SQL query w `*Repository.cs`** (nie w `.xaml.cs`)
- [ ] **Magda / Asia: zero regresji widocznych w pracy** (testują na bieżąco)
- [ ] **Dynia kontekstowa**: nowa funkcja w specyfikacjach = 4h pracy zamiast 2 dni

---

## Co PO refaktorze (pomysły na 2027)

| Pomysł | Effort | Wartość |
|---|---|---|
| Migracja do structured logging (Serilog) | 2 tyg. | ⭐⭐⭐ |
| Wyciągnięcie connection stringów do appsettings | 1 tydz. | ⭐⭐⭐⭐ |
| Wprowadzenie Dapper zamiast ręcznego ADO.NET (gdzie SQL Server 2008 nie blokuje) | 3 tyg. | ⭐⭐⭐ |
| Częściowy MVVM z CommunityToolkit.Mvvm (tylko nowe moduły) | per moduł | ⭐⭐ |
| Health checks + monitoring | 1 tydz. | ⭐⭐⭐ |
| Permissions per okno (zamiast tylko per kafelek) | 2 tyg. | ⭐⭐⭐⭐ |

---

*Wersja 1.0 • 24.05.2026 • Refactor to maraton, nie sprint. Q3-Q4 2026 dla 5 monstrów.*
