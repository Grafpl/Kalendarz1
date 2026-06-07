# PROMPT DLA CLAUDE WEB — Research funkcji TMS dla modułu Transport WPF

> **Instrukcja dla użytkownika:** skopiuj całą zawartość tego pliku (od pierwszej linii MARK ───── DO ───── poniżej) i wklej do Claude Web. Włącz tryb research (Web search). Claude Web wykorzysta to do analizy stanu sztuki w TMS / dispatch software 2025-2026 i zaproponuje konkretne funkcje pod ten program.

───── KOPIUJ OD TUTAJ ─────

# Twoja rola

Jesteś ekspertem od **Transport Management Systems (TMS)** i **dispatch/fleet software**. Twoim zadaniem jest **przeszukać internet** w poszukiwaniu najlepszych funkcji używanych przez nowoczesne programy do planowania transportu w 2025-2026 i zaproponować konkretne usprawnienia dla istniejącego programu opisanego niżej.

**Wymagania jakościowe:**
- Konkrety, nie buzzwordy. „AI/ML" tylko jeśli realnie pomaga w opisanej skali — bez ozdobników.
- Każda propozycja MUSI być inspirowana realnym produktem / publikacją / praktyką branżową ze źródłem (nazwa produktu, link, fragment dokumentacji).
- Pomysły mają być **wdrażalne w naszym stacku** (WPF .NET 8, SQL Server, Webfleet API już zintegrowane, code-behind bez MVVM).
- Skala: 1-2 planistów, ~12 kierowców, ~10 pojazdów, ~10-15 kursów dziennie. **NIE** proponuj rozwiązań „korporacyjnych" wymagających działu IT.

---

# Kontekst firmy

- **Branża:** drobiarstwo — zakład „Piórkowscy", Polska
- **Skala:** 258 mln PLN obrotu rocznie, 200 t/dzień ubojowa, ekspediowane mięso świeże + mrożone
- **Lokalizacja:** Koziołki 40, 95-061 Dmosin (woj. łódzkie). Centralna Polska — typowy zasięg dostaw 50–400 km
- **Klienci:** sieci sklepów (Biedronka, Lidl, Auchan, Carrefour) + restauracje + hurtownie + masarnie. Awizacje sieciowych ±15 min, restauracje 11:00–13:00, hurtownie elastyczne
- **Produkt:** mięso drobiowe świeże (chłodnia 0–4°C obowiązkowo, łańcuch HACCP), mrożone, podroby, odpady, karma
- **Flota:** ~10 pojazdów chłodniczych (E2 / palety H1, 33 palety/naczepa), kierowcy ~12
- **Integracje istniejące:** Webfleet (TomTom) GPS, Sage Symfonia (Handel), własna LibraNet (zamówienia/produkcja)

---

# Co JUŻ działa (do oceny przed proponowaniem)

## Główne okno planowania
Dwa widoki przełączane segmented-toggle:

**Widok LISTA** — DataGrid z kolumnami:
- 🔔 N (badge oczekujących zmian zamówień)
- Wyj. (godzina wyjazdu)
- Kierowca + indywidualna kropka koloru (deterministyczna z hash KierowcaID, 16-kolorowa paleta)
- Pojazd + niezależna kropka koloru (hash PojazdID)
- Trasa — **auto-generowana** z nazw klientów ładunków: „Klient1 → Klient2 → ... → KlientN"
- Ładunek — KG bold + „N/M palet · K poj." (M = max pojemność pojazdu)
- Wypełnienie — % + mini-pasek + kolor (zielony/pomarańcz/czerwony)
- Handl. — avatary handlowców (zdjęcia z sieciowego dysku, fallback inicjały z hash koloru)
- Aktywność — stacked avatary: duży twórca + mały modyfikator (jak GitHub) + tekst „Utworzył X · dd.MM HH:mm", drugi wiersz „✎ Zmienił Y · dd.MM HH:mm"

Lewy 4-pikselowy pasek statusu wiersza: pusty/przeładowany=czerwony, brak kierowcy/pojazdu=amber, OK=zielony.

**Widok TIMELINE (Gantt)** — wiersze per kierowca, paski kursów po godzinach 06:00–22:00:
- Pasek koloru-statusu jak w liście
- Badge `🔔 N` na pasku gdy zmiany
- Wykrywanie konfliktów (czerwona ramka gdy kierowca ma nakładające się kursy)
- Linia „teraz" (auto-update 60 s, tylko dziś)
- Drag&drop wolnego zamówienia: na pasek = przypisanie, na pusty obszar wiersza = nowy kurs z preselekcją kierowcy + godzina zaokrąglona do 15 min

## Panel wolnych zamówień (prawa kolumna, cała wysokość)
- Grupowanie po dniu odbioru (Awizacja)
- Per karta: avatar handlowca + nazwa klienta + godzina + KG bold + pojemniki + palety
- Drag&drop multi-select: na wiersz kursu = dodaj N zamówień, na puste miejsce listy = otwórz edytor nowego kursu z N zamówieniami jako ładunki
- Filtr: tekst (klient/handlowiec) + radio Ubój/Odbiór
- Akcja „Odbiór własny" (zdejmuje z puli)

## Edytor kursu
- Formularz: kierowca (combobox + auto-complete), pojazd (z pojemnością), data, godziny (wyjazd/powrót), trasa
- **Przycisk „🔮 Szacuj"** powrotu: model geometryczny (Haversine ×1.3 / 60 km/h + 30 min/przystanek)
- Pasek pakowania (% palet z fill-bar, zmiana koloru wg %)
- Lista ładunków — drag&drop reorder, edycja pojemników/uwag, sortowanie po awizacji
- Panel wolnych po prawej (cała wysokość) z **highlightem „🤝 razem"** dla klientów jeżdżących razem z odbiorcami z aktualnego kursu w ostatnich 90 dni (cross-DB CTE)
- Alert bar nad nagłówkiem gdy są oczekujące zmiany zamówień + przycisk „Akceptuj wszystkie"
- Panel inline 2-kolumnowy z kartami zmian (grupowane po kliencie, każda karta: emoji typu + label + Stare → Nowa + Δ z jednostką + 👤 zgłosił + [✓] [✗])

## System akceptacji zmian
- Trigger SQL na `LibraNet.ZamowieniaMieso` → kolejka `TransportZmianyQueue`
- Detekcja co 30 s: snapshot-diff, loguje pole/stare/nowe do `TransportPL.TransportZmiany`
- 10 typów zmian (Pojemniki/Waga/Awizacja/Termin/Anulowanie/...) z emoji i kolorami
- Scalanie wielu kolejnych edycji tego samego pola w 1 kartę („×7 edycji")
- Akceptacja propaguje: dla `ZmianaPojemnikow` synchronizuje `Ladunek.PojemnikiE2` z bieżącą wartością LibraNet
- Odrzucenie z chipami gotowych powodów („Skontaktuję się z handlowcem" / „Wymaga rozmowy z kierowcą" / „Niezgodne z planem" / „Pomyłka handlowca")
- Cache pendingów 30 s (TTL, invalidate po każdej mutacji)

## Historia kursu (osobne okno)
Dwie zakładki segmented-toggle:
- **📦 Zmiany zamówień** — wszystkie wpisy TransportZmiany dla zamówień w kursie (Oczekuje + Zaakceptowano + Odrzucono), grupowane per klient, filtr po statusie
- **📜 Zmiany kursu** — KursAuditLog (diff per pole nagłówka: Kierowca/Pojazd/Data/Godziny/Trasa/Status) z kto + kiedy

## Integracje
- **Webfleet (TomTom):** GPS pozycja + trip reports (start/end/duration/distance) + sendDestinationOrder (nieużywane). Konto active, account 942879.
- **Geokodowanie:** Nominatim (OSM) + cache `TransportPL.KlientAdres` per ZAM_id, fallback `LibraNet.KartotekaOdbiorcyDane` per IdSymfonia. Pokrycie GPS dziś ~36% ładunków (większość klientów nie geokodowanych — fix w toku).
- **Cross-DB:** TransportPL ↔ LibraNet (oba .109) — cross-DB JOIN działa bezpośrednio. HANDEL na .112 — łączenie tylko w .NET.

## Skróty klawiszowe (PreviewKeyDown na Window-level)
- **F5** — odśwież + invaliduj cache zmian
- **Insert** — nowy kurs
- **Delete** — usuń zaznaczony
- **Enter** — **wyłącznie** akceptuj wszystkie zmiany dla zaznaczonego kursu (nigdy nie przewija/edytuje)
- **Esc** — zwija panel zmian (odznacza wiersz)
- Dwuklik wolnego — nowy kurs z tym odbiorcą
- Dwuklik kursu — otwórz edytor

---

# Tech stack i ograniczenia

**Stack:**
- C# .NET 8.0 (`net8.0-windows7.0`), WPF, code-behind (decyzja architektoniczna — bez MVVM)
- SQL Server 2008 R2 (LibraNet — brak `TRY_CONVERT`, słabe okienkowe) + 2017+ (TransportPL, Handel)
- Connection strings hardcoded per okno (legacy)
- Dependencies: DevExpress (tylko w innych modułach), CommunityToolkit, Microsoft.Data.SqlClient

**Constraints:**
- Aplikacja desktop, **NIE webowa** — sieć lokalna .109 (LAN-only, brak external SSL/VPN dla planisty)
- 1-2 planistów na sesję; **NIE** wielouserowe w sensie kolaboracji real-time
- Kierowcy NIE mają aplikacji mobilnej (jeszcze) — komunikacja przez telefon
- Polskie napisy w UI (logistyk nie zna angielskiego)
- Brak budżetu na płatne SaaS poza istniejącym Webfleet (~tysiąc zł/m-c)
- Bez Pythona/Node — wszystko C#/SQL

---

# Profil użytkownika (logistyk)

- 1-2 osoby planujące — siedzą przed monitorem 8h/dzień
- Codzienna rutyna:
  - **6:00–9:00:** plan dnia, kto/co/dokąd, sprawdzenie kierowców-pojazdów dostępnych
  - **9:00–15:00:** reagowanie na zmiany zamówień (kilkanaście dziennie), awarie, telefon od kierowcy
  - **15:00–18:00:** plan na jutro, rozliczanie powrotów, raporty
- Najbardziej frustrujące dziś: brak widoczności kiedy auto wraca, ręczne pisanie tras, brak wiedzy o przyszłych zmianach klienta
- Cenią: **szybkość** (klawiszologia > mysz), **widoczność** (jeden ekran = cały dzień), **brak niespodzianek**

---

# Twoje zadanie

## Krok 1 — Research internetu
Przejrzyj dokumentacje i case studies następujących produktów / kategorii (oraz innych które uznasz za istotne):

**TMS / dispatch software:**
- Routific, OptimoRoute, Onfleet, Bringg, Locus, Routyn (Polska)
- Samsara, Verizon Connect, Webfleet, Geotab, Frotcom
- SAP TM, Oracle TMS, Manhattan, Descartes (enterprise — czerp idee, nie skala)
- **MyDriver / inelo / GBox (Polska)** — bo polski kontekst regulacyjny

**Food / cold chain logistics:**
- FoodLogiQ, Fresh Origins, Roambee, Tive
- Case studies dostaw świeżych: HelloFresh, Marley Spoon, Glovo Cool Chain
- Specyfika ADR + HACCP w transporcie żywności

**Last-mile / driver UX:**
- Onfleet Driver app, Routific mobile, Bringg Driver
- Electronic POD (Proof of Delivery), photo capture, klient signature

**Akademia / research:**
- VRPTW (Vehicle Routing Problem with Time Windows) — algorytmy 2020+
- Real-time re-optimization (gdy klient zmienia awizację mid-trip)
- Predictive ETA (ML modele oparte na trip data)

## Krok 2 — Filtrowanie pod kontekst
Z tego co znajdziesz wybierz funkcje które:
- ✅ Pasują do skali 1-2 planistów / 12 kierowców / 10 pojazdów
- ✅ Da się wdrożyć w WPF code-behind + SQL Server
- ✅ Wnoszą realną wartość operacyjną dla świeżego mięsa drobiowego
- ❌ NIE wymagają nowego SaaS / abonamentu > 500 zł/m-c (chyba że value jest oczywisty)
- ❌ NIE wymagają aplikacji mobilnej dla kierowcy (jeszcze)
- ❌ NIE są już zaimplementowane (patrz lista wyżej!)

## Krok 3 — Output

### Tabela 15-20 funkcji posortowanych malejąco wg value/effort:

| # | Funkcja | Co robi | Inspirowane przez (źródło) | Effort | Wartość biznesowa | Risk |
|---|---|---|---|---|---|---|
| 1 | ... | ... | ... | S/M/L/XL | „oszczędza 30 min/dziennie" | low/med/high |

- **Effort:** S = ≤4h, M = 1 dzień, L = 3-5 dni, XL = tydzień+
- **Wartość biznesowa:** liczbowo gdzie się da (godziny/zł/błędy)
- **Risk:** technical (czy się zbuduje), regulatory (compliance), user adoption (czy logistyk to polubi)

### Sekcja „TOP 5 z uzasadnieniem"
Wybierz 5 funkcji które dałyby największy skok jakości. Dla każdej:
- Dlaczego akurat ta (vs pozostałe)
- Mockup ASCII / opis UX (jak wyglądałoby w naszym oknie WPF)
- Konkretny pierwszy krok implementacyjny (jaką tabelę SQL stworzyć, którą metodę dorobić)

### Sekcja „Trendy TMS 2025-2026"
Co dzieje się w branży last-mile / cold chain co warto wziąć na radar (nawet jeśli teraz za duże):
- Generative AI w planowaniu trasy
- Autonomous trucks (Aurora, TuSimple — daleko ale...)
- Carbon footprint reporting (ESG — Biedronka tego wymaga)
- Inne

## Format odpowiedzi
- Polski język (logistyk to czyta)
- Każde źródło z linkiem
- Bez fluffu — konkrety
- Markdown, dobrze sformatowany
- Jeśli czegoś nie znajdziesz → powiedz wprost, nie zmyślaj

## Anty-przykład (czego NIE chcę)
> „Wdrożenie AI/ML do optymalizacji trasy zwiększy efektywność o 30%" — bullshit bez źródła, bez kosztu, bez konkretu jak.

## Przykład dobrej odpowiedzi
> „**Geofence alert na bazie**: Onfleet (https://onfleet.com/features/geofencing) używa promienia 200 m wokół bazy. Gdy pojazd wjeżdża/wyjeżdża, automatyczny event w systemie + zmiana statusu kursu na 'In Transit' / 'Returned'. **U nas:** mamy Webfleet GPS, dorobić w `KursMonitorService` cron 60 s sprawdzający dystans pojazdu od `FirmaLokalizacje.UbojniaKoziolki40` (Haversine), event do `KursAuditLog` przy przejściu progu. **Effort:** S (~3h). **Wartość:** logistyk nie dzwoni do kierowcy 'gdzie jesteś', system sam mówi. **Risk:** low (Webfleet już zintegrowane)."

---

Zaczynaj. Przeszukuj internet szeroko. Zwróć minimum 15 funkcji, najlepiej 20+. Idź w głąb branży food logistics i polskich regulacji transportu (tachograf, ADR, ATP dla chłodni).

───── KOPIUJ DO TUTAJ ─────
