# Część 3 — Asia jako strażnik kontraktów + projekt "Centrum Asi"

**Profil Asi (z kontekstu Sera):**
- *"Kręgosłup działu zakupu"* — strażnik kontraktów
- Jeździ z Serem na spotkania z prawniczką (umowy hodowców)
- Rozliczenia zakupowe i sprzedażowe (przejmuje od Marleny)
- GUS R09 raz w miesiącu do 8.
- W przyszłości: monitoring wydajności żywca per hodowca
- **Nie łapie za telefon** — pilnuje terminów i zapisów

**Zasada:** Asia ≠ Magda. Asia chce *spojrzenia ogólnego* (alerty, terminy, compliance), nie ekranów operacyjnych. Magda klika codziennie — Asia kontroluje.

---

## A. Gap analysis — co Asia robi dziś vs co ma robić

| Zadanie | Stan dziś | Stan docelowy | Gap |
|---|---|---|---|
| **Umowy hodowców** | Segregatory + folder sieciowy + kafelek "Dokumenty i Umowy" (status binarny) | Pełny rejestr z PDF, datami, alertami, dashboard ARiMR | **Pełna luka → Część 4** |
| **GUS R09** | Istnieje moduł `R09UWindow` (385 linii) w kategorii **PRODUKCJA** | Asia widzi w "Centrum Asi" + 1 klik wysyłki | Skrót + automatyzacja flow |
| **GUS R10/R11 lub inne** | brak | Sprawdzić czy potrzebne (BRC v9 / sprawozdawczość branżowa) | Asia powie czy są |
| **Rozliczenia zakupowe** | Robi w Excelu / Specyfikacjach / Avilog rozproszone | Jeden ekran "Rozliczenia tygodniowe" | Średnia luka |
| **Rozliczenia sprzedażowe** | Przejmuje od Marleny (poza zakresem audytu) | (poza zakresem) | (poza zakresem) |
| **Monitoring wydajności hodowców** | `RaportyHodowcow` (861 linii, raport ad hoc) | Living dashboard z trendami + alerty "X. spadł 3-i raz" | Dorobienie warstwy alertów |
| **Wnioski o zmiany danych hodowców** | `AdminChangeRequestsWindow` (575 linii) | Notification badge "5 do zatwierdzenia" | Notyfikacja |
| **Audyt kto co robi w zakupie** | LIVE audit w Kalendarzu/Wstawieniach, ale Asia musi otwierać każdy moduł | Centrum Asi pokazuje audit cross-module | Średnia luka |

**Konkluzja:** Asia ma 80% narzędzi, ale **rozsianych po 6 oknach**. Brakuje **kokpitu strażnika**.

---

## B. Audyt narzędzi które JUŻ są pod kątem Asi

### B.1 R09U — istniejący moduł (385 linii widok + 221 service + XML gen + walidator + historia)

**Co działa:**
- Hub `SprawozdaniaGusHubWindow` z kafelkami P-02 + R-09U + Ustawienia + Historia
- `R09UDataService.PobierzBrojleryZaMiesiacAsync(rok, miesiac)` — agregacja:
  - r1 = sztuki (`SUM FarmerCalc.LumQnt`)
  - r2 = waga żywa (`SUM FarmerCalc.NettoFarmWeight`)
  - r3 = waga poubojowa brutto (`SUM FarmerCalc.NettoWeight`)
  - r4 = waga handlowa netto (= r3, user koryguje)
  - r5 = wartość zł (z faktur HANDEL FVZ+FVR+FKZ)
- Drill-down lista partii (`R09UDrillDownDialog`)
- Generator XML wzorzec_2026 (`spr_R-09U_wzorzec_2026.xml`)
- Walidator (`R09UValidator`)
- Historia wysyłek (`HistoriaSprawozdanGusWindow` + `MarkAsSentDialog`)

**Flow Asi DZIŚ:**
1. Menu → kategoria **PRODUKCJA I MAGAZYN** (!) → "Sprawozdania GUS"
2. Klik R-09U → `R09UWindow` → wybierz miesiąc/rok → F5 (Load)
3. Przegląd Pozycje + drill-down per partia
4. Ctrl+G (Generate XML)
5. (poza ZPSP) Login Portal Sprawozdawczy GUS → upload XML ręcznie
6. Powrót → Mark as Sent

**Bolączki:**
- ❌ **W złym module** — Asia szuka w "Produkcji", a robi to dla zakupu
- ❌ **Brak proaktywnego alertu** — Asia musi pamiętać "do 8. dnia miesiąca"
- ❌ **5-krokowy flow** — Ser obiecał "jedno kliknięcie", dziś klików 8+
- ❌ **Brak eskalacji** — jeśli Asia zapomni, nikt nie zauważy
- ❌ **Hardcoded conn stringi** w `R09UDataService` linie 21-24 (oba)
- ❌ **r4 = r3 domyślnie** — Asia musi pamiętać żeby ręcznie skorygować o ubytki

### B.2 RaportyStatystykiWindow — 861 linii pod Asię do rozbudowy

- Ranking hodowców + progi (yellow/red)
- Brakuje: **alertu "ten hodowca spadł 3-i raz pod próg"** — Asia powinna widzieć trend automatycznie, nie szukać
- Brakuje: **eksportu ARiMR-ready PDF**

### B.3 AdminChangeRequestsWindow — pod Asię gotowy ale bez notyfikacji

- Workflow OK
- Brakuje: **badge na kafelku** w głównym menu "5 wniosków pending"
- Brakuje: **alert dziennego maila** "masz 3 wnioski starsze niż 2 dni"

---

## C. "Centrum Asi" — pełny mockup + spec

**Idea:** jedno okno otwierane rano (lub jako tła w trybie zawsze-na-wierzchu), agregujące wszystko czym Asia żyje.

### C.1 Mockup

```
┌══════════════════════════════════════════════════════════════════════════════╗
║  🏠 CENTRUM ASI • Strażnik Kontraktów • Sobota 24.05.2026 • 09:14            ║
╠══════════════════════════════════════════════════════════════════════════════╣
║                                                                                ║
║  ⏰ TERMINY (top priority)                                                     ║
║  ┌──────────────────────────────────────────────────────────────────────┐   ║
║  │ 📅 GUS R09 za KWIECIEŃ → deadline 08.05 (-16 dni TEMU)              │ ⚠️ ║
║  │     Status: ✓ Wysłane 06.05.2026 (Asia)                              │   ║
║  │     ✅ Wszystko OK                                                     │   ║
║  │ ─────────────────────────────────────────────────────────────────── │   ║
║  │ 📅 GUS R09 za MAJ → deadline 08.06 (+15 dni)                        │ ✅ ║
║  │     Status: ⏳ Auto-szkic powstanie 01.06 — Asia przejrzy + 1 klik   │   ║
║  │ ─────────────────────────────────────────────────────────────────── │   ║
║  │ 📜 KONTRAKT KOWALSKI → wygasa 15.07.2026 (+52 dni)                  │ 🟡 ║
║  │     Status: 📞 Tereska/Magda dzwoni za 4 tygodnie (12.06)            │   ║
║  │ ─────────────────────────────────────────────────────────────────── │   ║
║  │ 📜 KONTRAKT NOWAK BIS → wygasa 03.06.2026 (+10 dni)                 │ 🔴 ║
║  │     Status: ❗ ESKALACJA — Tereska zadzwoni dziś                    │   ║
║  └──────────────────────────────────────────────────────────────────────┘   ║
║                                                                                ║
║  🎯 ARiMR COMPLIANCE (live)                                                   ║
║  ┌──────────────────────────────────────────────────────────────────────┐   ║
║  │  Surowiec pod 3-letnim kontraktem: ████████░░░░ 67% ✅ (min 50%)    │   ║
║  │                                                                        │   ║
║  │  Hodowcy "spotowi" (do zakontraktowania):                            │   ║
║  │   • ABRAMOWICZ — 8 dostaw / 6 m-cy → propozycja kontraktu          │   ║
║  │   • CHOJNACKI  — 6 dostaw / 6 m-cy → propozycja kontraktu          │   ║
║  │   • DABROWSKI  — 5 dostaw / 6 m-cy → propozycja kontraktu          │   ║
║  │                                          [Generuj umowy w 1 kliku]   │   ║
║  └──────────────────────────────────────────────────────────────────────┘   ║
║                                                                                ║
║  📥 SKRZYNKA ASI (do akcji)                                                   ║
║  ┌──────────────────────────────────────────────────────────────────────┐   ║
║  │  📝 5 wniosków o zmianę danych (3 starsze niż 2 dni)                │ 🟡 ║
║  │  📊 R09 maj — auto-szkic gotowy do przejrzenia (1 klik wyślij)      │ ⏳ ║
║  │  💼 Faktura Avilog za tydzień 19 — sprawdź vs Matryca                │   ║
║  │  📑 Skan podpisanej umowy KOWALSKI BIS — do dorzucenia do rejestru   │   ║
║  └──────────────────────────────────────────────────────────────────────┘   ║
║                                                                                ║
║  📈 TRENDY HODOWCÓW (ostatnie 4 tyg.)                                         ║
║  ┌──────────────────────────────────────────────────────────────────────┐   ║
║  │  🔴 JANKOWSKI — padłe wzrosły 3-i raz (4.2% → 5.8% → 6.5%)          │   ║
║  │  🔴 WIŚNIEWSKI — różnica wagi -8% (sygnał problemu z paszą)         │   ║
║  │  🟢 KOWALSKI BIS — najlepszy wzrost wydajności (+2.1%)              │   ║
║  └──────────────────────────────────────────────────────────────────────┘   ║
║                                                                                ║
║  💬 LIVE AUDIT (cross-module, ostatnie 24h)                                   ║
║  ┌──────────────────────────────────────────────────────────────────────┐   ║
║  │ 14:23 Magda dodała wstawienie #4521 — KOWALSKI, 28 000 szt          │   ║
║  │ 14:21 Tereska edytowała dostawę 28.05 — zmieniła godz. 6→9          │   ║
║  │ 09:14 (Asia) — przegląd skrzynki                                     │   ║
║  └──────────────────────────────────────────────────────────────────────┘   ║
║                                                                                ║
║  [📊 Pełne Kontrakty]  [📋 Statystyki]  [📞 Log rozmów]  [⚙️ Ustawienia]   ║
╚══════════════════════════════════════════════════════════════════════════════╝
```

### C.2 Spec techniczna

**Plik:** `Hodowcy/Centrum/CentrumAsiWindow.xaml` (+ `.xaml.cs` ~600 linii target — disciplined size)

**ViewModel-light approach** (zgodnie z regułą code-behind, ale wydzielić DTO):
- `Hodowcy/Centrum/Models/CentrumAsiDto.cs` — `TerminItem`, `ComplianceSnapshot`, `SkrzynkaItem`, `TrendItem`, `AuditItem`
- `Hodowcy/Centrum/Services/CentrumAsiService.cs` — agregacja 5 sekcji asynchronicznie (`Task.WhenAll`), TTL cache 5 min

**Refresh strategy:**
- Auto-refresh co 5 min (cache invalidate)
- Force refresh F5
- Live audit z `LibraNet.AuditLog` polling co 30 sek (re-use mechanizmu z Kalendarza)

**Data sources:**
- Terminy GUS — `GusSubmissionsRepo.GetRecentAsync("R-09U", 6)` + deadline calc (`8.` następnego miesiąca)
- Terminy kontraktów — `KontraktyHodowcow` nowa tabela (Część 4)
- ARiMR Compliance — agregacja `KontraktyHodowcow` + `FarmerCalc` (% surowca pod 3-letnim)
- Skrzynka — `DostawcyCR` (count where status='Proposed'), `R09USzkice` (auto-szkic ready), `FakturyAvilog` (do weryfikacji)
- Trendy — `RaportyStatystykiWindow` logic + alert "3 raz z rzędu pod progiem"
- Audit — `AuditLog` JOIN z modułami

**Permissions:**
- `accessMap[N] = "CentrumAsi"` — przyznać tylko Asi, Serowi, Justynie
- W kategorii **ZAOPATRZENIE I ZAKUPY** lub nowa kategoria **"DZIAŁ ZAKUPU — KONTROLA"**

### C.3 Stałe UI elementy

- **Tryb "Always on top"** (opcjonalny checkbox) — Asia trzyma okno z boku ekranu cały dzień
- **Mini-mode** (Alt+M) — zwija do paska "5 terminów dziś, 2 alerty" — Asia chowa gdy pracuje w innym oknie
- **Print PDF** (Ctrl+P) — wydruk dziennego briefa dla spotkania z Serem

---

## D. Nowe widoki dla Asi (lista konkretna)

| # | Nowy widok | Plik | Effort | Pilność |
|---|---|---|---|---|
| **D1** | **Centrum Asi** (kokpit) | `Hodowcy/Centrum/CentrumAsiWindow.xaml` | 3-4 dni | wysoka |
| **D2** | **Rejestr Kontraktów Hodowców** | `Kontrakty/RejestrKontraktowWindow.xaml` | Część 4 | krytyczna |
| **D3** | **Dashboard ARiMR Compliance** | `Kontrakty/ArimrComplianceWindow.xaml` | Część 4 | krytyczna |
| **D4** | **Tracker GUS** (kalendarz miesięczny + auto-szkic) | `Sprawozdania/Views/TrackerGusWindow.xaml` | 2 dni | wysoka |
| **D5** | **Rozliczenia tygodniowe (jeden ekran)** | `Hodowcy/Centrum/RozliczeniaTygodnioweWindow.xaml` | 2 dni | średnia |
| **D6** | **Log rozmów z hodowcami (aktywni)** | `Hodowcy/Centrum/LogRozmowWindow.xaml` + tabela `HodowcaRozmowa` | 2 dni | średnia |
| **D7** | **Alerty Statystyk Hodowców** — nakładka na `RaportyStatystykiWindow` | rozbudowa | 1 dzień | średnia |
| **D8** | **Notification badges** na kafelkach Wnioski Zmian / Centrum Asi | rozbudowa Menu.cs | 4h | wysoka |

---

## E. Automatyzacja GUS R09 — design "jednego kliknięcia"

### E.1 Co się zmienia (vs istniejący flow)

| Krok | Dziś | Po automatyzacji |
|---|---|---|
| Trigger | Asia pamięta o 8. | **System sam** 1. każdego miesiąca generuje szkic |
| Generacja szkicu | Asia: Menu → Hub → R09U → wybierz okres → F5 | **Auto w tle** + notification "R09 maj gotowy do przejrzenia" |
| Przegląd | Asia drill-down ręcznie | Asia otwiera link z notyfikacji → już załadowane |
| Korekta r4 (ubytki) | Asia pamięta że r3=r4 i ręcznie wpisuje | **Auto-suggest** "ubytki średnio 1,8% w tym miesiącu, ustawić r4 = 98,2% × r3?" |
| Walidacja | Klik "Walidator" | **Auto** — szkic nie powstaje gdy walidacja fails (log błędu do Asi) |
| Generate XML | Ctrl+G | **Zrobione w tle** przy generacji szkicu — XML gotowy do pobrania |
| Upload Portal Sprawozdawczy | **Ręczny upload** (GUS nie ma API publicznego) | (NIE DA SIĘ zautomatyzować — Portal Sprawozdawczy GUS wymaga login + 2FA, brak public API) |
| Mark as Sent | Klik Mark as Sent | **Auto** — Asia uploaduje XML, system wykrywa zmianę statusu w pliku response (jeśli GUS go zwraca) lub Asia klika 1 raz "Wysłałam" |

### E.2 Realny "jeden klik" Asi

```
1. Otwórz Centrum Asi (rano)
2. Klik "📊 R09 maj — auto-szkic gotowy"  ← jedyne kliknięcie!
3. Okno R09UWindow otwiera się z załadowanym szkicem + walidacją OK
4. Asia przegląda, zatwierdza r4 (auto-suggest)
5. Klik "📤 Wyślij" → otwiera Portal Sprawozdawczy GUS + kopia XML do schowka
6. (poza ZPSP) Login GUS + paste + submit
7. Wraca → Centrum Asi pyta "Wysłałaś?" → 1 klik "Tak"
```

**Skrócenie z ~10 klików do 3** (+ ręczny upload, którego nie obejdziemy).

### E.3 Eskalacja

- **Deadline -7 dni** → push notification w ZPSP do Asi
- **Deadline -3 dni** → email do Asi + Sera
- **Deadline -1 dzień** → email do wszystkich w dziale + alert na ekranie Centrum Asi (czerwone tło)
- **Deadline minięty bez wysyłki** → **alert ☎️ przy logowaniu Asi i Sera**, blokowanie zamykania okna do potwierdzenia

### E.4 Implementacja techniczna

**Trigger automatycznego szkicu:**
- **Opcja A:** Windows Scheduled Task uruchamia `Kalendarz1.exe --gus-r09-szkic` 1. każdego miesiąca o 4:00
- **Opcja B:** Wewnętrzny scheduler w aplikacji (timer w `App.xaml.cs`) sprawdza co godzinę "czy dziś jest 1. dnia miesiąca + 4:00"
- **Rekomendowane:** A (czystsze, nie wymaga zawsze-running aplikacji)

**Nowe artefakty:**
- Nowa tabela `R09USzkice` (LibraNet):
  ```sql
  CREATE TABLE dbo.R09USzkice (
    Lp INT IDENTITY PRIMARY KEY,
    Rok INT NOT NULL,
    Miesiac INT NOT NULL,
    StatusSzkic VARCHAR(20) NOT NULL, -- Created / Validated / Failed / Reviewed / SentByUser
    XmlPath NVARCHAR(500) NULL,
    Snapshot NVARCHAR(MAX) NULL, -- JSON snapshot pozycji
    CreatedAt DATETIME2 NOT NULL,
    ReviewedBy NVARCHAR(50) NULL,
    ReviewedAt DATETIME2 NULL,
    SentByUser NVARCHAR(50) NULL,
    SentAt DATETIME2 NULL,
    CONSTRAINT UQ_R09USzkice UNIQUE(Rok, Miesiac)
  );
  ```
- Modyfikacja `R09UDataService` — metoda `GenerujSzkicAsync(int rok, int miesiac)` (re-use istniejącej logiki + zapis do `R09USzkice`)
- Nowy `R09UAutoService` — invoked z command line / scheduler

**Effort:** **3-5 dni** (zgodne z moim szacunkiem w Części 2, z poprawką że bazujemy na istniejącym kodzie).

---

## F. Rozbudowa RaportyHodowcow pod alerty Asi

**Dziś:** statyczny raport. **Asia chce:** "który hodowca *trend ujemny*?".

### F.1 Nowa logika

```csharp
// pseudokod w RaportyHodowcow.Services/TrendAlertEngine.cs
foreach (var hodowca in hodowcy)
{
    var seriaPadlych = GetLast4Cycles(hodowca.Id, metryka: "Padle%");
    if (seriaPadlych.All(c => c.Wartosc > progRed) && seriaPadlych.Length >= 3)
    {
        alerty.Add(new TrendAlert
        {
            Hodowca = hodowca,
            Metryka = "Padłe %",
            SeveryDays = 14,
            Komunikat = $"3-i raz z rzędu padłe > {progRed}% ({string.Join(" → ", seriaPadlych.Select(s => s.Wartosc + "%"))})",
            ProponowanaAkcja = "Skontaktować z weterynarzem / przegląd paszy"
        });
    }
}
```

### F.2 Co to da Asi

- 1 spojrzenie na sekcję "Trendy Hodowców" w Centrum Asi
- Nie musi otwierać raportu i porównywać miesiąc po miesiącu
- Sygnał "ten kontrakt może być do renegocjacji" → wpływ na strategię

### F.3 Effort

- Engine alertów: 1-2 dni
- Persistencja progów (`AlertyKonfig` tabela) + UI do edycji: 1 dzień
- Integracja z Centrum Asi: 4h
- **Razem: 3-4 dni**

---

## G. Eskalacje i alerty — pełna mapa

| Event | Kto dostaje notyfikację | Kanał | Kiedy |
|---|---|---|---|
| Kontrakt wygasa za 3 mies. | Asia | banner w Centrum Asi | dziennie |
| Kontrakt wygasa za 1 mies. | Asia + Tereska/Magda | banner + push w ZPSP | dziennie |
| Kontrakt wygasa za 7 dni | Asia + Tereska/Magda + Ser | banner + push + **email** | dziennie |
| Kontrakt wygasł bez przedłużenia | wszyscy | banner red + email + alert przy logowaniu | natychmiast |
| ARiMR compliance < 50% | Asia + Ser | banner red w Centrum Asi | natychmiast |
| ARiMR compliance < 55% | Asia | banner yellow | dziennie |
| GUS R09 deadline -7 dni | Asia | banner w Centrum Asi | dziennie |
| GUS R09 deadline -3 dni | Asia + Ser | banner + email | dziennie |
| GUS R09 deadline -1 dzień | wszyscy w dziale | banner red + email | natychmiast |
| GUS R09 nie wysłany w terminie | Asia + Ser | alert przy logowaniu, blokowany do potwierdzenia | natychmiast |
| Wniosek zmiany danych pending > 2 dni | Asia | badge na kafelku | dziennie |
| Hodowca 3-i raz z trendem ujemnym | Asia | sekcja "Trendy" w Centrum Asi | po każdym cyklu |
| Magda dodała wstawienie bez potwierdzenia (24h) | Magda + Asia | banner u Magdy, log u Asi | po 24h |
| Faktura Avilog dotarła | Asia | sekcja Skrzynka | tygodniowo |

---

## H. Korekta Części 2 — R09U auto-gen

W Części 2 punkt **C.2 B1** zapisałem: *"GUS R09 auto-gen — Ser obiecał Asi 'jedno kliknięcie', 3-5 dni"*.

**Korekta:** moduł R09U **już istnieje** (385 + 221 + 179 + 113 + 102 linii). Effort **3-5 dni nadal właściwy**, ale **na shortening flow**, nie greenfield:
- Auto-szkic 1. każdego miesiąca (~1 dzień)
- Tabela `R09USzkice` + service (~1 dzień)
- Integracja z Centrum Asi (skrzynka) (~4h)
- Auto-suggest r4 = r3 × (1 - ubytki%) (~4h)
- Eskalacja email (~4h)
- Skrót w kategorii Zaopatrzenie (~30 min)
- Razem: **~3 dni roboczych** (mniej niż szacowałem).

---

## I. Lokalizacja w menu (decyzja architektoniczna)

**Problem:** Asia ma narzędzia rozsiane między kategoriami:
- `Sprawozdania GUS` w **PRODUKCJA I MAGAZYN**
- `Sprawozdania ZSRIR` w **ZAOPATRZENIE I ZAKUPY**
- `Wnioski o Zmiany` w **ZAOPATRZENIE I ZAKUPY**
- `Statystyki Hodowców` w **ZAOPATRZENIE I ZAKUPY**
- `Dokumenty i Umowy` w **ZAOPATRZENIE I ZAKUPY**

**Rekomendacja:** **Nie tworzyć nowej kategorii.** Zamiast tego:
- **"Centrum Asi"** jako pierwszy kafelek (kolorem wyróżniony) w **ZAOPATRZENIE I ZAKUPY** — agreguje skróty do reszty
- **Skrót do R09U** w "Centrum Asi" (z kategorii Produkcja nie ruszamy — Asia i tak otwiera tylko przez kokpit)
- **Notification badges** na: Wnioski Zmian, GUS, Dokumenty i Umowy

---

## 📌 PODSUMOWANIE CZĘŚCI 3

**8 nowych widoków / rozbudów dla Asi (D1-D8):**

| Priorytet | Widok | Effort |
|---|---|---|
| 🔴 1 | Rejestr Kontraktów + Dashboard ARiMR | Część 4 |
| 🔴 2 | **Centrum Asi (kokpit)** | 3-4 dni |
| 🟠 3 | Tracker GUS + auto-szkic R09 | 3 dni |
| 🟠 4 | Notification badges | 4h |
| 🟡 5 | Engine alertów trendów hodowców | 3-4 dni |
| 🟡 6 | Log rozmów z hodowcami | 2 dni |
| 🟡 7 | Rozliczenia tygodniowe (jeden ekran) | 2 dni |
| 🟢 8 | Pełne eskalacje + email backbone | 1 tydz. |

**Razem dla Centrum Asi (bez Części 4):** ~3 tygodnie pracy Sera.

**Najważniejsza decyzja Sera:** czy Asia ma własną kategorię w menu (np. "DZIAŁ ZAKUPU — KONTROLA") czy tylko jeden kafelek "Centrum Asi" w istniejącej. Rekomendacja: **drugi wariant** (mniej zaburza menu, łatwiej cofnąć jeśli się nie sprawdzi).

**Reguła:** Asia nie powinna nigdy musieć otwierać >1 okna żeby dowiedzieć się "co dziś". Centrum Asi to **single source of truth dla strażnika**.
