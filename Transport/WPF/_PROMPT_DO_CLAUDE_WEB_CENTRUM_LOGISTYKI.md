# PROMPT DLA CLAUDE WEB — Centrum Zarządzania Logistyką (20 koncepcji + 20 wizualizacji)

## 📎 PLIKI DO PRZESŁANIA do Claude Web (przeciągnij wszystko do okna chatu)

**Obowiązkowe (5 plików):**
1. **`Screenshot_20.png`** — główny panel transportu (z `C:\Users\PC\Desktop\`)
2. **`Screenshot_21.png`** — edytor kursu (z `C:\Users\PC\Desktop\`)
3. **`PlanowanieTransportuWpfWindow.xaml`** — struktura głównego okna (`Transport/WPF/`)
4. **`EdytorKursuWpfWindow.xaml`** — struktura edytora (`Transport/WPF/`)
5. **Ten plik (`_PROMPT_DO_CLAUDE_WEB_CENTRUM_LOGISTYKI.md`)** — prompt + kontekst

**Mile widziane dodatkowo (jeśli się zmieści):**
6. `Transport/WPF/Views/TimelineDniaView.xaml` — widok Timeline/Gantt
7. `Transport/WPF/Windows/HistoriaZmianKursuWindow.xaml` — okno historii
8. `Transport/WPF/Theme/TransportWpfStyles.xaml` — styl/kolory/typografia
9. **Zrzut Timeline view** (jeśli zrobisz screen)
10. **Zrzut "Wolne zamówienia" w edytorze** (jeśli rozdzielne)

> Po wkleju instrukcji poniżej **włącz tryb research / web search** w Claude Web — prompt wymaga przeglądnięcia konkretnych produktów TMS i odniesień ze źródłami.

---

═════════════════════════════════════════════════════════════════
KOPIUJ POD TYM ────────────────────────────────────────────────
═════════════════════════════════════════════════════════════════

# Twoja rola

Jesteś **senior UX designerem z 15-letnim doświadczeniem w projektowaniu interfejsów dla operatorów (dispatcher, kontroler ruchu, logistyk transportu)**, jednocześnie ekspertem od **Transport Management Systems (TMS)** w branży **cold-chain / food logistics**, oraz **doświadczonym projektantem aplikacji desktop WPF**.

Twoim zadaniem jest stworzenie **kompleksowej koncepcji wizualnej** dla "Centrum Zarządzania Logistyką" — modułu transportu w istniejącej aplikacji desktop dla zakładu drobiarskiego w Polsce.

**Twój deliverable to:**
1. **20 nowych funkcji** (konkretnych, wdrażalnych, opartych o realne TMS z 2024-2026)
2. **20 wizualizacji UX** (mockupy ASCII / wireframe + opis layoutu)
3. **TOP 5 deep-dive** z pełnymi mockupami
4. **Trendy branżowe 2025-2026**
5. **Plan implementacji w 3 sprintach**

Każda propozycja MUSI być inspirowana realnym produktem ze źródłem (link, fragment dokumentacji), wdrażalna w stack'u WPF .NET 8 + SQL Server, i pasować do polskiego logistyka w branży świeżego mięsa.

---

# 📊 KONTEKST FIRMY (czytaj uważnie — to nie korporacja!)

| Parametr | Wartość |
|---|---|
| **Branża** | Drobiarstwo — ubojnia + przetwórnia + dystrybucja |
| **Firma** | Piórkowscy (Polska) |
| **Obrót** | ~258 mln PLN rocznie |
| **Skala produkcji** | 200 ton mięsa drobiowego dziennie |
| **Lokalizacja zakładu** | Koziołki 40, 95-061 Dmosin (województwo łódzkie, centralna Polska) |
| **Promień dostaw** | 50–400 km (głównie Mazowsze, Łódzkie, Wielkopolska, Śląsk) |
| **Klienci** | Sieci sklepów (Biedronka, Lidl, Auchan, Carrefour) — wymagają **awizacji ±15 min**. Restauracje (Marriott, Sphinx) — 11:00–13:00. Hurtownie mięsne — elastyczne |
| **Produkt** | Mięso drobiowe świeże (chłodnia 0–4°C obowiązkowa, HACCP), mrożone, podroby, odpady, karma. **Specjalne kursy: Halal (osobny kierowca + pojazd + dokumenty), Eksport** |
| **Flota** | ~10 pojazdów chłodniczych (E2 plastikowe palety, naczepa H1 = 33 palety, 36 E2/paletę) |
| **Kierowcy** | ~12 — pełni etat, niektórzy specjalizują się w konkretnych klientach (Marriott zna Kowalskiego od 3 lat) |
| **Logistycy planiści** | 1-2 osoby na zmianę, ~10-15 kursów dziennie do zaplanowania |
| **Integracje** | Webfleet (TomTom) GPS + trip reports. Sage Symfonia (księgowość). Własna baza LibraNet (zamówienia/produkcja) |

**Specyfika operacyjna:**
- **Świeży produkt** — opóźnienie 2h = strata towaru, łańcuch chłodniczy musi być nieprzerwany
- **Bardzo wczesne dostawy** — sieci handlowe chcą 4:00–7:00 rano, restauracje na lunch
- **Powrót pustych palet E2** — klient zwraca, trzeba liczyć (palety ~50 zł/szt × 100 sztuk w obiegu = 5000 zł "krążących")
- **Reklamacje** — logistyk musi szybko odpowiedzieć "kto wiózł, kiedy, ile" (jest osobny moduł reklamacji)
- **Dynamiczne zmiany** — handlowiec edytuje zamówienie 3× dziennie (więcej, mniej, inna godzina), logistyk musi akceptować
- **Drugi kurs** — auto wraca o 12:00, może wziąć drugi kurs popołudniowy → istotne dla rentowności

---

# 🖥️ STAN AKTUALNY APLIKACJI (po analizie 2 screenów + XAML)

## Aplikacja-host
- **Nazwa wewnętrzna:** ZPSP (Zajebisty Program Sergiusza Piórkowskiego)
- **Plik wykonywalny:** Kalendarz1.exe (~250k LOC)
- **Moduł, którym się zajmujemy:** Transport WPF Sandbox — czysto WPF, izolowany od starego WinForms transportu
- **Stack:** C# .NET 8.0, WPF (target net8.0-windows7.0), code-behind (decyzja architektoniczna BEZ MVVM), SQL Server (2008 R2 dla LibraNet, nowszy dla TransportPL/Handel)
- **Conn-stringi:** hardcoded per okno (legacy)

## Główny panel transportu (Screenshot #1)

**Toolbar (góra):**
- Strzałki ← → do nawigacji dnia, DatePicker (`5.06.2026`), button "Dziś"
- Nazwa dnia tygodnia ("piątek", w akcent turkus)
- Segmented toggle: **Lista** / **Timeline**
- Search box (Ctrl+F-able)
- Po prawej: **+ Nowy kurs** (primary turkus button), Edit, Delete, Refresh, Auto (auto-refresh 45 s)

**Lista kursów (środkowy DataGrid, ~9-10 wierszy):**
Kolumny w kolejności:
1. **🔔 (44 px)** — amber pigułka z liczbą oczekujących zmian zamówień (TransportZmiany pendingi)
2. **Wyj.** — godzina wyjazdu (`04:00`, `10:00`, ...)
3. **Kierowca** (168 px) — kropka koloru indywidualnego (8×8) + ikona 👤 + nazwa; kolory deterministyczne z hash(KierowcaID), 16-kolorowa paleta
4. **Pojazd** — kropka koloru pojazdu (niezależna od kierowcy!) + ikona 🚚 + rejestracja
5. **Trasa** (`Width="*"`) — **auto-wygenerowana** z nazw klientów po `Kolejnosc`: `Klient1 → Klient2 → ... → KlientN` (Distinct, tooltip pełna lista przy >5 stopów)
6. **Ładunek** — KG **bold** 13 pt (`15 600 kg`) + drugi wiersz mały szary `31/33 pal · 1240 poj` (M = pojemność pojazdu)
7. **Wypełnienie** — % bold (`94%`) + mini-pasek wypełnienia, kolor: zielony <75%, pomarańcz 75-100%, czerwony >100%
8. **Handl.** — do 3 nakładających się avatarów handlowców (zdjęcia z `\\192.168.0.170\Install\Prace Graficzne\Avatary\{userId}.png`, fallback inicjały) + skrót typu "Jola, Teresa"
9. **Aktywność** (220 px) — **stacked avatary**: duży 32 px twórca + mały 16 px modyfikator w prawym-dolnym z białą obwódką (jak GitHub PR). Plus tekst: `Utworzył: Ilona Krakowiak · 03.06 14:36` + drugi wiersz `✎ Zmienił: ... · ...`

**Wiersz kursu:**
- Lewy 4-pikselowy pasek koloru statusu: pusty/przeładowany=czerwony, brak kierowcy/pojazdu=amber, OK=zielony
- Hover: light bg #F0F4F5
- Selected: light teal #D2EEF0
- Wysokość 48 px

**Panel WOLNE ZAMÓWIENIA (prawa kolumna, ~440 px szerokości, cała wysokość):**
- Header: "WOLNE ZAMÓWIENIA" + pigułka liczby (np. 4)
- Toolbar: search + radio Ubój/Odbiór + Refresh + **+ Dodaj** (primary)
- Grupowanie po dniu odbioru: nagłówek `📅 06.06 sob.` + badge liczby
- Karta zamówienia (kompaktowa):
  - Lewa: avatar handlowca (30 px circular)
  - Środek: nazwa klienta (bold) + drugi wiersz mały szary `Ubój 05.06 pt. · Ogólne`
  - Prawa kolumna: godzina awizacji (bold turkus, `04:00`) + data odbioru małym (`06.06 sob.`)
  - Druga linia: **pigułka KG bold turkus** (`2 040 kg`) + pigułka pojemników (`136 poj.`) + pigułka palet (`3,8 pal`)
- Highlight `🤝 razem` (turkus pill) przy klientach jeżdżących razem z odbiorcami z aktualnego kursu (90 dni)

**Panel detali zmian (dół, ~220 px max, auto-show gdy zaznaczony kurs ma pendingi):**
- Amber bar (`#FFF7E0` / `#F0E0B0`):
  - "🔔 3 zmian do akceptacji · kurs #1831"
  - Buttony po prawej: `📜 Historia` + `📝 Edytor` + **`✓ Akceptuj wszystkie (3) — Enter`** (primary)
- Karty zmian (2-kolumnowy UniformGrid, grupowane po kliencie):
  - Header sekcji: szare tło #EEF1F4, `🏢 Damak` + amber pigułka licznika
  - Karta jednowierszowa MinHeight 50:
    - 3 px akcent pionowy (kolor typu zmiany)
    - 38 px emoji typu (📦 pojemniki, ⚖ waga, ⏰ awizacja, 📅 termin, 🚫 anulowanie, 🆕 nowe, 🏠 odbiorca, 🏭 produkcja)
    - TypLabel (`Termin`, bold)
    - **Stare → Nowa** w pillsach: szary `2026-05-27` → turkus bold `2026-06-06`, Δ z jednostką (`+5 kg`, `-3 poj.`)
    - "👤 zgłosił + czas temu"
    - `[✓]` `[✗]` (26×24)

**Status bar (sam dół):**
- "Załadowano 10 kursów na 05.06.2026" + skrót "Dwuklik na kurs = edycja · przeciągnij wolne zamówienie na kurs"

## Edytor kursu (Screenshot #2)

**Alert bar (góra, amber):**
- "🔔 3 zmian zamówień czeka na akceptację" + `Pokaż ▼` + **`✓ Akceptuj wszystkie (3)`**

**Lewa karta — formularz kursu:**
- Tytuł: `📝 Edycja kursu #1831` + po prawej meta `Utworzył: 51991 · 05.06 08:26`
- Pola (WrapPanel, jeden wiersz):
  - KIEROWCA — combobox 248 px (auto-complete, search), bez "+" (planista wybiera z istniejących)
  - POJAZD — combobox 218 px, pokazuje pojemność (`WGM 7736H - 33 palet`)
  - DATA — DatePicker
  - GODZINY (wyjazd → powrót) — TxtWyjazd `10:30` → TxtPowrot `18:00` + przycisk `🔮 Szacuj`
- TRASA — TextBox `Damak` + button `auto`
- **Pasek pakowania** — fill bar % palet (`106%` czerwony — przeładowanie!) + `35 / 33 palet · 1240 poj. · 1 ład.`
- Ładunki w kursie — DataGrid (Kolejność / Klient / Awiz. / Pojemniki / Uwagi) + drag&drop reorder + buttony Sortuj/↑/↓/🗑

**Prawa karta — WOLNE ZAMÓWIENIA (cała wysokość):**
- Identyczne do panelu w głównym oknie

**Footer:**
- Status text "Błąd wolnych zamówień: Object reference not set..." (rzadki bug do naprawy)
- Anuluj + **ZAPISZ KURS** (primary turkus 190 px)

## Inne widoki (nie na screenach)

**Timeline view (Gantt) — zamiast Lista:**
- Wiersze per kierowca (170 px lewa kolumna z avatarem + nazwiskiem + pojazdem)
- Kanwa: paski kursów po godzinach 06:00–22:00 (zoom 24-64 px/h)
- Pasek koloru statusu + badge `🔔 N` na pasku
- Linia "teraz" (czerwona, auto-update 60 s, tylko dziś)
- Wykrywanie konfliktów (czerwona ramka gdy kierowca ma nakładające się kursy)
- Drag&drop wolnego: na pasek = przypisz, na pusty obszar = nowy kurs z preselekcją kierowcy

**Historia kursu — osobne okno:**
- Dwie zakładki segmented-toggle:
  - **📦 Zmiany zamówień** — wszystkie TransportZmiany dla zamówień (Oczekuje + Zaakcept + Odrzuc), grupowane per klient, filtr po statusie
  - **📜 Zmiany kursu** — KursAuditLog (diff per pole: Kierowca/Pojazd/Godziny/Trasa) z kto + kiedy

**Dialog odrzucenia zmiany** — chipsy gotowych powodów ("Skontaktuję się z handlowcem", "Wymaga rozmowy z kierowcą"...) + pole własne.

---

# 🩹 PAIN POINTS LOGISTYKA (z perspektywy 8h pracy)

Zebrane z rozmów. KRYTYCZNE — projektuj rozwiązania pod te konkretnie:

1. **"Czy auto wróciło na plac?"** — logistyk dzwoni do kierowcy. Webfleet GPS jest, ale nieużywane w UI.
2. **"Kto dziś jest?"** — brak rejestracji obecności kierowców. Decyduje pamięć rano.
3. **Nie wie kiedy auto wróci** → trudno zaplanować drugi kurs popołudniowy.
4. **Klient sieciowy ma okno ±15 min** — opóźnienie = reklamacja. Brak alertu w systemie.
5. **Preferencje klientów w głowie** — Marriott chce 5:30 + Kowalski. Nigdzie nie zapisane.
6. **Palety zwrotne** — który klient ile winien. Excel boczny.
7. **Reklamacja** — "kto wiózł 28.05 do X?" → szukanie po datach.
8. **Drugi kurs możliwy?** — auto wraca 12:00, klient B ma awiz. 14:30 → łatwo to skojarzyć, ale system nie pomaga.
9. **Limit godzin kierowcy** — przepisy (tachograf, kodeks pracy), liczone w głowie.
10. **Temperatura w chłodni** — alert gdy >4°C nie istnieje.
11. **Wzór dnia** — co tydzień te same kursy do tych samych klientów. Wpisywanie ręczne.
12. **Powiadomienia klienta** — "kierowca jedzie, ETA 16:30" — ręczny telefon / email.
13. **Korki / pogoda** — Google Maps wie, system nie.
14. **Halal/eksport** — kierowca i pojazd muszą być certyfikowani, ale system tego nie filtruje.
15. **Awarie i serwisy** — przegląd techniczny / OC / opony — zaskakuje "auto nie pojedzie bo dziś przegląd".

---

# 💻 TECH STACK I CONSTRAINTS

**MUST:**
- Cały kod w **C# .NET 8 + WPF code-behind** (BEZ MVVM — decyzja architektoniczna, firmowa)
- SQL Server (2008 R2 dla LibraNet — brak `TRY_CONVERT`, słabe okienkowe; 2017+ dla TransportPL/Handel)
- Pliki XAML + .cs side by side, ResourceDictionary `TransportWpfStyles.xaml`
- Polski język w UI (logistycy nie znają angielskiego biegle)
- Aplikacja **desktop** (NIE web), LAN-only (zakład w Dmosinie)
- Brak aplikacji mobilnej (kierowcy bez smartphone'ów w pracy)

**MAY:**
- Webfleet API (już skonfigurowane) — GPS, trip reports, send orders
- Nominatim (OSM) geokodowanie
- HTTP requests do zewnętrznych API (jeśli wartość uzasadnia)
- Email/SMS gateway (jeszcze brak — możliwa integracja przez SerwerSMS, Twilio, SMTP)

**MUST NOT:**
- Nowy SaaS abonament > kilkaset zł/m-c bez bardzo dobrego ROI
- Wymaganie nowego komputera / serwera
- Rewrite całego modułu (incremental only)

**Styl wizualny (z `TransportWpfStyles.xaml`):**
- Akcent: turkus **#00838F** (`Accent`), ciemniejszy **#00695C** (`AccentDark`), miękki **#E0F2F1** (`AccentSoft`)
- Tła: białe karty z subtelnym cieniem, `Bg #F4F6F8`, `Surface #FFFFFF`, `SurfaceAlt #FAFBFC`
- Linie: `Line #ECEFF1`
- Tekst: `InkPrimary #1F2733`, `InkSecondary #37474F`, `InkMuted #8A95A3`
- Statusy: `OkFg/Bg #2E7D32/#E7F4E8`, `WarnFg/Bg #B26A00/#FFF3DC`, `CritFg/Bg #C62828/#FDECEC`, `NeutralFg/Bg #5B6776/#ECEFF1`
- Ikony: **Segoe MDL2 Assets** (Add E710, Edit E70F, Delete E74D, Refresh E72C, Contact E77B, Car E804, Calendar E787, Search E721) + emoji unicode dla domeny (📦⚖⏰📅🚫🆕🏠🏭)
- Typografia: `T.Title 18 SemiBold`, `T.Section 10 SemiBold uppercase`, `T.Data 14 SemiBold`, `T.Body 12.5`, `T.Muted 11`
- Karty (`Card` style): `CornerRadius 10`, biały + `DropShadow Blur 12 Opacity 0.08`

---

# 👤 PERSONY UŻYTKOWNIKÓW

## Persona 1: **Marta** — logistyk dzienna (06:00-14:00)
- 38 lat, w firmie 7 lat
- Komputer dobrze, ale **nienawidzi zmian** ("dlaczego znowu inaczej?")
- Pracuje równolegle z 3 monitorami: program + Excel + WhatsApp z kierowcami
- Najgorsza dla niej rzecz: telefon, gdy musi przerwać planowanie i odbierać
- **Cele:** zaplanować dzień do 9:00, reagować na zmiany sieciowych do 12:00
- **KPI:** ile kursów dziennie, ilość spóźnień (chce zero)

## Persona 2: **Piotr** — logistyk popołudniowy (12:00-20:00)
- 29 lat, w firmie 2 lata
- Tech-savvy, chętnie używa nowości jeśli są szybsze
- **Cele:** planowanie na jutro, rozliczanie powrotów, kontakt z kierowcami nocnymi
- **Frustracje:** brak danych o powrotach, ręczne raporty wieczorne

## Persona 3: **Andrzej** — szef logistyki (sporadycznie)
- 52 lata, właściciel-decydent
- Wchodzi raz w tygodniu — chce widzieć dashboard "co się dzieje"
- **Cele:** rentowność transportu, koszty paliwa, KPI kierowców

---

# 🎯 TWOJE ZADANIE — szczegóły deliverable

## A) 20 funkcji (lista priorytetyzowana)

Sformułuj 20 propozycji funkcji. Pokryj różne kategorie:
- **Real-time visibility** (GPS, status pojazdu, ETA) — min. 2
- **Predictive** (forecast capacity, ETA prediction, klient drop probability) — min. 2
- **Communication** (driver, customer, internal team) — min. 2
- **Cold chain / HACCP** (temperatura, dokumenty) — min. 2
- **Compliance** (tachograf, ADR, kodeks pracy kierowcy) — min. 2
- **Performance metrics** (dashboards, KPI per kierowca/klient) — min. 2
- **Workflow optimization** (klawiszologia, batch operations, drag&drop) — min. 2
- **Customer experience** (powiadomienia, tracking, ETA) — min. 2
- **Cost & efficiency** (paliwo, palet zwrotne, kursy puste) — min. 2
- **Risk & exception handling** (awarie, opóźnienia, reklamacje) — min. 2

Dla KAŻDEJ funkcji:

```markdown
### #N. NAZWA FUNKCJI

**Problem:** jakie ból to rozwiązuje (cytat z pain points lub własna obserwacja).

**Co robi:** opis w 2-3 zdaniach.

**Inspirowane przez:** [Onfleet Driver App](https://onfleet.com/features/driver-app) — sekcja "Photo POD".

**Mockup ASCII / wireframe:** (patrz wymagania niżej)

**Implementacja w naszym stacku:**
- Tabele SQL: `KlientPaletyBilans (KlientId, PaletyDoZwrotu, DataAktualizacji, ...)`.
- Pliki do zmian: `KursRow.cs` + `EdytorKursuWpfWindow.xaml` + nowy `PaletyZwrotnePopupWindow.xaml`.
- Reuse istniejącego: `KartotekaOdbiorcyDane`, `WebfleetReportService`.

**Effort:** S (≤4h) / M (1 dzień) / L (3-5 dni) / XL (tydzień+)

**Wartość biznesowa:** konkretnie — "oszczędza 25 min/dziennie", "zmniejsza reklamacje o ~10%", "10k PLN/rok mniej paliwa". Jeśli nie wiesz dokładnie, oszacuj zakres.

**Risk:** technical (czy się zbuduje) / regulatory (compliance) / user adoption (czy logistyk to polubi).
```

## B) 20 wizualizacji UX (mockupy ASCII)

**TO JEST KLUCZOWA CZĘŚĆ.** Logistyk uczy się wzrokowo. Każda funkcja musi mieć **konkretną wizualizację**:

### Wymagania techniczne wizualizacji:

1. **ASCII grid** pokazujący layout (kolumny, wiersze, panele) — używaj `┌─┐│└─┘├┤┬┴┼` znaków
2. **Anotacje boczne:**
   - `→ kolor #00838F (akcent turkus)`
   - `→ FontSize 14, bold`
   - `→ pojawia się gdy: warunek X`
3. **User flow:** strzałki "klik na X → dzieje się Y → wynik Z"
4. **Wymiary:** szerokość/wysokość w px lub %
5. **Integracja z istniejącym layoutem** — pokaż JAK się wpisuje w obecne okno (główny panel/edytor/Timeline)

### Przykład DOBREJ wizualizacji:

```
┌──────────────────────────────────────────────────────────────────────┐
│ 🌡 STATUS CHŁODNI                                          ▼ 1h temu │
├──────────────────────────────────────────────────────────────────────┤
│  WGM 7736H  ▓▓▓▓▓▓▓▓▓░░  3.2°C   ✓ OK                              │
│  WGM 8J11   ▓▓▓▓▓▓░░░░░  5.1°C   ⚠ALERT          [Zadzwoń kierowcy]│
│  WGM 9C89   ▓▓▓▓▓▓▓▓▓▓░  2.8°C   ✓ OK                              │
└──────────────────────────────────────────────────────────────────────┘
     ↑                          ↑              ↑
   widget umieszczony       pasek thermometru  status badge:
   pod toolbarem            (zielone <4, amber  ✓ OK = #2E7D32
   głównego okna            4-6, czerwone >6)  ⚠ ALERT = #C62828
                                                 + auto SMS do kierowcy

Trigger: dane z Webfleet trip reports (jeśli pojazd ma czujnik) lub manualny wpis
         co 2h przez kierowcę przy postoju.

User flow:
  Co 5 min auto-refresh → pojawia się ALERT czerwony
  → Logistyk widzi z drugiej części ekranu
  → Klik "Zadzwoń" otwiera Skype/Teams call do telefonu kierowcy
  → Po rozmowie logistyk zaznacza "✓ Skontaktowano" → alert znika
```

### NIE chcę:

❌ "Modernistyczny dashboard z AI-driven insights" — bez konkretu = bezużyteczne  
❌ Wireframe-y typu Figma sketch jako proza ("nowoczesny dashboard z czystym layoutem...") — chcę grid  
❌ Buzzword'y bez wskazania konkretnego elementu w UI

### CHCĘ:

✅ ASCII rysunki które logistyk może wskazać palcem i powiedzieć "tu chcę guzik"  
✅ Anotacje typografią/kolorami/wymiarami z naszego stylu (`TransportWpfStyles.xaml`)  
✅ User flow krok po kroku z konkretnymi przyciskami

## C) Pokrycie wizualizacji 20×

**Per okno** zaprojektuj koncepcje wzbogacające:

**Główny panel transportu:**
- Mockupy 8-10 ulepszeń w obrębie tego okna (np. dashboard sticky-bar na górze, alert center w prawym górnym, mini-mapa, kafelki KPI, expanded row, etc.)

**Edytor kursu:**
- Mockupy 6-8 ulepszeń w obrębie tego okna (np. side-panel z timeline kursu, photo POD slot, dokumenty checklist, signature pad, etc.)

**Nowe okna/widoki (jeśli proponujesz):**
- Mockupy 4-6 zupełnie nowych okien (np. Dashboard szefa, Mapa floty, Karta klienta-rozszerzona, Driver workspace, etc.)

**Łącznie: 20 wizualizacji** (rozkład wg potrzeb, niekoniecznie 8+8+4).

## D) TOP 5 deep-dive

Z 20 funkcji wybierz 5 absolutnie najlepszych. Dla każdej:

1. **Pełny mockup ASCII** (większy niż w pkt B, z wszystkimi stanami: pusty / normalny / alert / hover / wybrane)
2. **State diagram** (Mermaid albo ASCII): jakie stany ma feature i jak się między nimi przechodzi
3. **Database schema** (CREATE TABLE z indeksami)
4. **Lista plików do zmian** (konkretne `.xaml` i `.cs` z fragmentem kodu do dodania — np. jakie nowe property w `KursRow`)
5. **Krok-po-kroku plan implementacji** (5-10 kroków, każdy z effort w h)

## E) Trendy TMS / Last-mile / Cold-chain 2025-2026

Sekcja research'owa:
- Co dzieje się w branży?
- Jakie funkcje są "must-have" w 2026 a były nice-to-have w 2024?
- Co robi konkurencja drobiarska (Cedrob, Drosed, Suszbrojler)?
- Compliance i regulacje (ESG raportowanie, nowe przepisy ADR 2026, dyrektywa o czasie pracy kierowców)
- Co warto wziąć na radar na 2027-2028?

## F) Plan implementacji w 3 sprintach

Sprint 1 (2 tyg) — TOP 5 funkcji critical-path  
Sprint 2 (2 tyg) — kolejne 5 funkcji średnio-ważnych  
Sprint 3 (2 tyg) — polish + nice-to-have  
**Łącznie ~15 funkcji w 6 tygodni.**

Z każdego sprintu: lista funkcji, effort łączny w godzinach, oczekiwany efekt biznesowy.

---

# 🌐 PRODUKTY DO RESEARCHU (web search OBOWIĄZKOWY!)

**Międzynarodowe TMS / dispatch:**
- Onfleet (https://onfleet.com) — szczególnie Driver app, route optimization, customer notifications
- Routific (https://routific.com) — multi-stop VRP optimization
- OptimoRoute (https://optimoroute.com) — time windows, multi-day
- Bringg (https://bringg.com) — last-mile orchestration
- Locus (https://locus.sh) — dispatch automation
- Samsara (https://samsara.com) — fleet management + cold chain
- Verizon Connect (https://verizonconnect.com) — driver tracking
- Geotab (https://geotab.com) — telematics insights
- Frotcom (https://frotcom.com) — popular w Polsce dla floty
- TruckIn (https://truckin.com) — multi-driver scheduling

**Polski rynek:**
- MyDriver (https://mydriver.pl)
- GBox (https://gbox.pl) — telematyka polska
- inelo (https://inelo.pl) — tachograf, czas pracy
- Routyn (https://routyn.pl)
- Comarch ERP Transport
- TransportEksport (https://transport.online) — community insights

**Cold chain / food logistics:**
- FoodLogiQ (https://foodlogiq.com)
- Tive (https://tive.com) — temperature/location sensors
- Roambee (https://roambee.com) — supply chain visibility
- Carrier Logistics LinkPlus
- Case studies: HelloFresh, Marley Spoon, Glovo Cool Chain

**Last-mile mobile (dla inspiracji UX nawet jeśli my desktop):**
- Onfleet Driver app
- Bringg Driver
- Lalamove
- Glovo Courier

**Akademia:**
- VRPTW (Vehicle Routing Problem with Time Windows) — Solomon benchmarks
- Real-time re-optimization research
- Predictive ETA modele (ML)
- HACCP w transporcie żywności (rozporządzenia UE)

**Polska legislacja:**
- Tachograf cyfrowy (rozporządzenie 561/2006)
- Kodeks pracy kierowcy (ustawa o czasie pracy kierowców)
- ATP convention (chłodnie międzynarodowe)
- HACCP food safety
- Pakiet mobilności 2

---

# 📐 FORMAT OUTPUTU

Struktura odpowiedzi (zachowaj kolejność):

```markdown
# 1. EXECUTIVE SUMMARY (max 15 zdań)
Krótko: co proponujesz, jaki największy zysk, jakie ryzyka.

# 2. PERSONY (potwierdź lub uzupełnij)
Czy moja analiza Marta/Piotr/Andrzej jest trafna? Uzupełnij brakujące jeśli widzisz.

# 3. TABELA 20 FUNKCJI (sortowane wg value/effort)
| # | Funkcja | Kategoria | Effort | Wartość | Risk | Inspiracja |
|---|---|---|---|---|---|---|

# 4. SZCZEGÓŁY 20 FUNKCJI
Per każda: Problem · Co robi · Mockup ASCII · Implementacja · Effort · Wartość · Risk · Inspiracja

# 5. WIZUALIZACJE 20× (rozkład wg okien)
## 5a. Główny panel — 8-10 mockupów
## 5b. Edytor kursu — 6-8 mockupów
## 5c. Nowe widoki — 4-6 mockupów

# 6. TOP 5 DEEP-DIVE
Per każda: Pełny mockup z wszystkimi stanami · State diagram · DB schema · Pliki do zmian · Plan implementacji

# 7. TRENDY 2025-2026
Sekcja research'owa, ze źródłami

# 8. PLAN W 3 SPRINTACH
Sprint 1/2/3 z funkcjami i effort

# 9. PRZYPISY / ŹRÓDŁA
Lista linków z numerami referencji
```

---

# 🚫 ANTY-PRZYKŁADY (NIE chcę tego)

❌ **"Wdrożenie AI/ML pozwoli zoptymalizować trasy o 25%"** — bez konkretu, bez algorytmu, bez nakładu, bez źródła = zero wartości

❌ **"Modernizacja UI w duchu Material Design"** — brak wskazania KONKRETNYCH elementów które zmienia

❌ **"Implementacja Blockchain dla traceability"** — nadinżynierowane, niepasujące do skali

❌ **"Customer self-service portal"** — wymaga aplikacji webowej której nie mamy

❌ **Generic best-practices typu "dashboard powinien mieć KPI"** — chcę KONKRETNE wskaźniki: "Liczba kursów dziś / Powroty <13:00 / Klienci sieciowi z awizacją"

---

# ✅ PRZYKŁAD WZORCOWEJ ODPOWIEDZI (jak chcę żebyś pisał)

### #1. Centrum Alertów Operacyjnych (Operations Alert Center)

**Problem:** Logistyk siedzi w 3 monitorach, alerty rozproszone (telefon, WhatsApp, badge w oknie). Często przegapia ważną zmianę awizacji sieciowej (Biedronka, ±15 min = reklamacja).

**Co robi:** Wąski sticky bar na górze głównego okna z chronologicznym strumieniem alertów: nowa zmiana zamówienia, opóźnienie ETA kierowcy, przekroczenie temp chłodni, klient potwierdza odbiór, awaria pojazdu. Każdy alert ma severity (info/warn/crit), czas (`14:32`), akcję ("Zobacz kurs", "Zadzwoń", "Akceptuj").

**Inspirowane przez:** [Onfleet Live Dashboard](https://onfleet.com/features/real-time-tracking) — sekcja "Activity Feed" + [Bringg Notifications Center](https://bringg.com/features/notifications/)

**Mockup ASCII:**
```
┌─[ Główny panel transportu - sticky top bar - 36 px ]─────────────────────────┐
│ 🔴 14:32  Biedronka Łódź: awizacja zmieniona 14:00→11:30   [kurs #1798] [✓] │
│ 🟡 14:30  Kowalski 9h dziś (limit za 1h)                  [kierowca]    [📞]│
│ 🔴 14:28  WGM 7736H: temperatura 5.8°C >4°C               [Webfleet]    [📞]│
│ 🔵 14:25  Klient PUBLIMAR potwierdza odbiór (5min temu)   [kurs #1796]      │
│ ▼ Pokaż wszystkie (12)                                            🔕 Wycisz │
└──────────────────────────────────────────────────────────────────────────────┘
   ↑
 fixed top-bar nad istniejącym toolbarem, 36 px wysokości
 collapsable do 1 wiersza (najnowszy alert) gdy nie ma nowości
 severity colors:
   🔴 #C62828 (CritFg) — krytyczne: temp, opóźnienie, anulowanie
   🟡 #B26A00 (WarnFg) — uwaga: limit godzin, drugi kurs możliwy
   🔵 #00838F (Accent) — info: nowe zamówienie, potwierdzenie

User flow:
  Backend (cron 30 s) zbiera alerty z 4 źródeł:
  - TransportZmiany (nowe pendingi)
  - Webfleet trip reports (temp, opóźnienia)
  - Kierowcy/Pojazdy (limit godzin, serwis)
  - LibraNet (potwierdzenia)
  → Wpisuje do nowej tabeli AlertyOperacyjne (KursID?, KierowcaID?, PojazdID?, Severity, Tresc, Akcja, Dane)
  → Bar polluje co 30 s przez Dispatcher.Timer
  → Auto-fadeout po 5 min dla info, po 30 min dla warn, sticky dla crit dopóki nie odhaczone
  
  Klik na [✓] = mark as read; klik na linka kursu = scroll do wiersza w liście + zaznacz
```

**Implementacja w naszym stacku:**
- Nowa tabela `TransportPL.dbo.AlertyOperacyjne` (Id, KursID nullable, KierowcaID nullable, PojazdID nullable, Severity varchar(20), Tresc nvarchar(500), Akcja varchar(50), Dane nvarchar(MAX) JSON, CzasUTC, OdczytanePrzez nvarchar(50) nullable, OdczytaneUTC nullable)
- Nowy `Transport/WPF/Services/AlertCenterService.cs`:
  - `PobierzNowezAlertyAsync(DateTime sinceUtc)` — SELECT * FROM AlertyOperacyjne WHERE CzasUTC > @since AND (OdczytanePrzez IS NULL OR Severity='Krytyczne')
  - `OznaczJakoOdczytaneAsync(int alertId, string user)`
  - `WyciszAsync(string typ, TimeSpan duration)` — temporary mute
- Nowy `Transport/WPF/Controls/AlertCenterBar.xaml/.cs` — UserControl 36 px wysokości, ItemsControl z alertami
- W `PlanowanieTransportuWpfWindow.xaml`: dodać `<ctrl:AlertCenterBar Grid.Row="0" />` (przesuń pozostałe wiersze +1)
- Backend triggery do zasilania `AlertyOperacyjne`:
  - W `TransportZmianyService.DetectNewOrdersAsync` po INSERT do TransportZmiany → INSERT do AlertyOperacyjne
  - Cron w `Menu.cs` co 30 s: sprawdza Webfleet temp, limit godzin, serwisy → INSERT do AlertyOperacyjne
- DispatcherTimer w `AlertCenterBar` co 30 s → fetch newAlerts → update ItemsControl

**Effort:** L (3-5 dni)
- Schema + triggery zasilające: 1 dzień
- AlertCenterService + AlertCenterBar UserControl: 1 dzień
- Integracja z PlanowanieTransportuWpfWindow + akcje (klik kursu, klik telefonu): 1 dzień
- Testy + edge cases (overflow, mute, persistence): 1-2 dni

**Wartość biznesowa:**
- Marta przestaje przegapiać zmiany sieciowych — szacunek 5-10 reklamacji/m-c uniknięte
- Czas reakcji na zmianę awizacji z ~30 min (przegapienie + telefon) do <2 min (alert + 1 klik)
- Awaria temp w chłodni wykryta przed dotarciem do klienta → towar uratowany (1 ratowane wysyłka = ~5000 zł)
- Szacunek: 30-50k PLN/rok mniej strat

**Risk:**
- **Technical: low** — wszystkie dane już są (TransportZmiany, Webfleet API, Kursy)
- **Regulatory: brak** (alerty wewnętrzne, nie wymagają compliance)
- **User adoption: medium** — Marta nie lubi nowości. Trzeba zrobić dyskretne, nie pop-up'owe. Default collapsed (tylko najnowszy), expand on demand. Wycisz button dla focus mode.

---

# 🏁 ZACZYNAJ

Pamiętaj:
- **Web search OBOWIĄZKOWY** — minimum 15 cytatów ze źródłami
- **20 funkcji + 20 wizualizacji + TOP 5 deep-dive + trendy + plan 3 sprintów**
- **Polski język** (z wyjątkiem nazw produktów / technicznych terminów które po polsku brzmią głupio)
- **Konkretne mockupy ASCII**, nie proza
- **Realne źródła** z linkami (jeśli nie znajdziesz — powiedz wprost, nie zmyślaj)
- **Implementacja w WPF code-behind**, NIE generic "frontend"
- Zacznij od EXECUTIVE SUMMARY żeby pokazać czuję

Jeśli czegoś nie wiesz / nie możesz znaleźć — napisz "❓ Nie udało mi się znaleźć X, zaproponuj uzupełnienie".

Idź w głąb. Czas pracy: weź tyle ile potrzeba (nie spiesz się). To research dla projektu który będzie używany przez kolejne lata.

═════════════════════════════════════════════════════════════════
KOPIUJ DO TĄD ──────────────────────────────────────────────────
═════════════════════════════════════════════════════════════════
