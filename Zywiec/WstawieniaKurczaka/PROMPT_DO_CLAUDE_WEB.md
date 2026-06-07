# PROMPT DO CLAUDE WEB — REDESIGN CENTRUM ZAKUPÓW ŻYWCA

> **Skopiuj wszystko poniżej (od linii `==== START PROMPTU ====` do `==== KONIEC PROMPTU ====`) i wklej do Claude.ai (Sonnet 4.5 lub Opus 4).**
>
> **Załączniki, które musisz dołączyć w czacie** (drag & drop pliki do okna chatu):
>
> 1. `screenshot_panel_glowny.png` — Twój screen z `Panel Główny Wstawień` (4 sekcje: Lista / Przypomnienia / Nadchodzące / Historia)
> 2. `screenshot_modyfikacja_wstawienia.png` — Twój screen z `WstawieniaKurczaków → Modyfikacja` (Słąbkowska Agnieszka, Szablon Dostaw)
> 3. `WidokWstawienia.xaml` (ścieżka: `Zywiec/WstawieniaKurczaka/WidokWstawienia.xaml`)
> 4. `WstawienieWindow.xaml` (ścieżka: `Zywiec/WstawieniaKurczaka/WstawienieWindow.xaml`)
> 5. `CLAUDE.md` (root projektu — kontekst biznesowy)
> 6. **OPCJONALNIE** (jeśli Claude poprosi):
>    - `WidokWstawienia.xaml.cs` — code-behind głównego okna (~3800 linii, kluczowe partie)
>    - `WstawienieWindow.xaml.cs` — code-behind okna edycji
>    - `BAZA_WIEDZY/0` — opis tabel SQL
>
> ⚠️ Jeśli Claude.ai limituje liczbę plików — załącz 1-3 priorytetowo, resztę wklej jako tekst gdy poprosi.

---

==== START PROMPTU ====

# 🎨 REDESIGN: CENTRUM ZARZĄDZANIA ZAKUPAMI ŻYWCA — KONCEPCJE WIZUALNE

## ROLA

Jesteś **senior UX/UI designer** z 15-letnim doświadczeniem w aplikacjach biznesowych B2B/ERP. Twoja specjalność: **dashboards operacyjne dla pracowników wykonujących powtarzalne czynności z dużą ilością danych** (centra obsługi klienta, logistyka, zakupy, ubezpieczenia, banki). Znasz Material Design 3, Fluent 2, IBM Carbon, Salesforce Lightning Design System. Tworzysz koncepcje które są **NATYCHMIAST używalne przez prawdziwych pracowników na linii produkcyjnej** — nie modernistyczne dla portfolio, ale wydajne dla 8h dziennie ciężkiej pracy.

Twoim zadaniem jest stworzenie **WIELU konkretnych koncepcji wizualnych** dla aplikacji biznesowej w której pracują zakupowcy żywca w firmie drobiarskiej. Mają mieć "**Centrum Zarządzania Zakupami**" które realnie usprawni ich codzienną pracę — nie wygląd, lecz funkcjonalność i percepcję informacji.

---

## KONTEKST PROJEKTU

### Firma

**Piórkowscy** — ubojnia drobiu w Polsce, ok. **258 mln zł obrotu rocznie**, **200 ton drobiu/dzień**. Wewnętrzna nazwa aplikacji: **ZPSP** ("Zajebisty Program Sergiusza Piórkowskiego" — slang firmowy). Aplikacja produkcyjna obsługuje cały biznes: zakupy żywca od hodowców, ubój, logistyka, sprzedaż, HR, transport.

### Konkretne okno do redesignu

Aplikacja zawiera moduł **Cykle Wstawień Kurczaków** (folder `Zywiec/WstawieniaKurczaka/`), w którym zakupowcy **codziennie** pracują od 7:00 do 17:00. Moduł składa się z:

#### 1. **Panel Główny Wstawień** (główne okno — `WidokWstawienia.xaml`, screenshot #1)
4 panele danych jednocześnie:
- **Lewa kolumna (60% szerokości):** Lista Wstawień — wstawienia kurczaków zaplanowane przez hodowców, kolumny: LP, Hodowca, Data, Ilość sztuk, Typ umowy, Cena, Kto utworzył/Kiedy, Kto potwierdził/Kiedy. Sortowanie/filtrowanie. ~7500 wstawień w bazie, na ekranie 100 najnowszych.
- **Środkowa kolumna góra (20% szer.):** Przypomnienia — hodowcy do których trzeba zadzwonić bo dawno nie kontaktowano się (>35 dni). Kolumny: LP, Data, Hodowca, Ilość, Telefon, Notatki, Za ile dni. ~137 wpisów dziennie.
- **Środkowa kolumna dół (20% szer.):** Nadchodzące wstawienia — wstawienia w ciągu 14 dni do tyłu i 14 do przodu. Zakupowiec musi zadzwonić potwierdzić termin (czasem hodowcy zmieniają). Tło wierszy wg pilności (czerwone/żółte/zielone). ~38 wpisów.
- **Prawa kolumna (20% szer.):** Historia Kontaktów — wszystkie ostatnie 90 dni rozmów / SMS-ów / notatek. Kolumny: Hodowca, Kto kontaktował, Następne planowane, Notatka, Kiedy.

Funkcje już zaimplementowane (zachować w redesignu):
- Menu kontekstowe PPM na każdym wierszu (Potwierdź / Edytuj / SMS / Dodaj telefon)
- Skróty klawiszowe: S=SMS krótki, Shift+S=SMS pełny, F=Potwierdź, R=Nie odebrał, Enter=Edytuj, Del=Usuń
- 8 wariantów gotowych SMS-ów (oficjalny / krótki / przyjazny / z pytaniem / przypomnienie / 3× warianty dla hodowców z wolnego rynku)
- Auto-snooze 3 dni po wysłaniu SMS-a o potwierdzenie (wiersz znika z Nadchodzących)
- ⭐ ikona "stały klient" przy hodowcach którzy mieli kiedyś dostawę potwierdzoną
- Audyt ładowania (przycisk 🔍 Audyt — diagnostyka wydajności)
- Statystyki (przycisk 📊 Statystyki — kto ile wstawień stworzył/potwierdził/SMS-ów wysłał)

#### 2. **Okno Modyfikacji Wstawienia** (`WstawienieWindow.xaml`, screenshot #2)
Edycja pojedynczego wstawienia hodowcy:
- Pola: Data wstawienia, Ilość sztuk, Po 3% upadku, Suma odebranych, Różnica
- **Szablon Dostaw** — wstawienie kurczaków produkuje **kilka dostaw rozłożonych w czasie** (zwykle 2 dostawy: pierwsza po 35 dniach mały kurczak, druga po 42 dniach duży). Dla każdej dostawy: Doba, Data, Dni od wstawienia, Średnia waga, Sztuk/pojemnik, Mnożnik, Sztuki łącznie, Liczba aut, Auto Wyłączenie/Włączenie godz.
- Akcje: Anuluj / Zapisz / przyciski na każdej dostawie (kalendarz, usuń)
- Przyciski u góry: Kalendarz / Pomoc / Seria / Dostawy (toggle widoczność)

#### 3. **Dialog Szczegóły Wstawienia** (otwierane dwuklikiem na wstawieniu)
Tooltip-like dialog ze szczegółowymi danymi: historia ważenia palet z LibraNet, klasy wagowe kurczaków (4-12), średnia waga palety (500-600 kg), liczba ważeń, rzeczywista vs deklarowana ilość, anomalie.

### Stack techniczny (ograniczenia)

- **WPF .NET 8.0** — `net8.0-windows7.0` target
- **Code-behind, NIE MVVM** — projekt celowo używa wzorca code-behind
- **DevExpress dostępny** dla zaawansowanych gridów
- **LiveCharts.Wpf** w packages (dla wykresów)
- **OxyPlot.Wpf** w packages
- **ClosedXML** (Excel export gotowy)
- **System.Windows.Media.Imaging** (avatary z plików)
- **Microsoft.Data.SqlClient** (DB)
- Baza danych: 4 instancje SQL Server (głównie LibraNet 192.168.0.109)
- Polskie znaki w UI (ą, ę, ó, etc.)
- **Paleta firmowa**: zielony główny `#5C8A3A`, akcent ciemny zielony `#4B732F`, niebieski info `#3498DB`, czerwony alert `#E74C3C`, żółty ostrzeżenie `#F39C12`, szare tła `#ECF0F1` / `#F5F7F8`, ciemny tekst `#2C3E50`, średni tekst `#37474F` / `#546E7A`, jasny tekst `#7F8C8D` / `#95A5A6`
- Czcionka systemowa: **Segoe UI**

---

## PERSONA UŻYTKOWNIKA

### "Teresa" — zakupowiec żywca

- Wiek 42 lata, 8 lat pracy w Piórkowskich
- Komputer: stacja Windows 10/11, 1 monitor 24" Full HD (1920×1080)
- Codzienna praca 7:00–17:00 (10h)
- Telefon stacjonarny + smartfon (do SMS-ów i rozmów z hodowcami)
- Pamięta 200 hodowców z imienia, zna ich charaktery (kto drażliwy, kto powolny, kto stały)
- Nie używa myszki idealnie — kursor "lata" gdy się pospieszy
- Często **pracuje pod presją** — telefon dzwoni, hodowca pyta o cenę, drugi czeka pod ramą
- Mówi: *"chcę widzieć w 3 sekundy do kogo dzwonić dzisiaj"*
- Frustracja: zbyt dużo klikania w menu, zbyt dużo okien, brak natychmiastowego feedbacku
- Cele zawodowe: jak najwięcej potwierdzonych wstawień, mało reklamacji, dobre relacje z hodowcami

### Codzienny workflow (typowy dzień):

```
07:00  Otwiera Panel Główny Wstawień
07:05  Sprawdza Przypomnienia (137 wpisów)
07:10  Dzwoni do pierwszego hodowcy / wysyła SMS
07:20  Klika "Potwierdź" gdy hodowca odpisał
07:25  Następny — kolejne 50 przypomnień rano
09:00  Pauza — kawa
09:30  Sprawdza Nadchodzące (38 wpisów) — potwierdzanie terminów
11:00  Dodaje nowe wstawienia (Ctrl+N) — hodowca zadzwonił
12:00  Lunch
13:00  Edycja istniejących wstawień (zmiany dat, ilości)
14:00  Drugie zlecenie potwierdzeń z Przypomnień
15:00  Pisanie notatek do Historii Kontaktów
16:00  Sprawdza statystyki dziennie (przycisk 📊)
17:00  Koniec dnia
```

**Pain points (z czego korzystają obecnie):**

- Wiele kliknięć żeby wykonać prostą akcję (Potwierdź = 1 menu kontekstowe + 1 dialog "czy na pewno")
- Brak natychmiastowego feedbacku po akcji (czy się zapisało? nie wiadomo)
- 4 oddzielne tabele na ekranie — głowa boli od kontekst switching
- Niektóre statusy są tekstowe (kolumna Cena pokazuje "wolny" — to mało wizualne)
- Brak prognoz / sugestii AI (gdyby system sugerował komu zadzwonić jako pierwszy)
- Trudno odróżnić "stałych" klientów od "nowych" / "ryzykownych"
- Brak grupowania wiadomości — Historia jest płaska, nie wiadomo co znaczy "Strefa" w notatce

---

## CO MA POWSTAĆ — TWOJE ZADANIE

Stwórz **30 KONCEPCJI WIZUALNYCH** podzielonych na 3 sekcje (10 + 10 + 10):

---

### SEKCJA A: 10 KONCEPCJI PANELU GŁÓWNEGO WSTAWIEŃ (dashboard)

Dla każdej z 10 koncepcji:

1. **Nazwa koncepcji** (np. *"Command Center 2026"*, *"Kanban Daily"*, *"Mission Control"*, *"Calm Workspace"*)
2. **Główna idea** (1-2 zdania — co odróżnia tę koncepcję od pozostałych)
3. **ASCII mockup** całego okna (1920×1080 wireframe) z dokładnym layoutem, kolorami, marginesami
4. **Hierarchia informacji** — co jest na pierwszy plan, co na drugi, co schowane
5. **Konkretne elementy nowe lub przeprojektowane:**
   - Inny layout siatki (np. 3 kolumny zamiast 4)
   - Karty (cards) vs listy
   - Wykresy / sparklines / progress bars
   - Avatary / ikony / emoji
   - Filtry chipy / quick actions toolbar
   - Sticky headers
   - Floating action button
6. **Mikrocopy** (przykładowe teksty po polsku)
7. **Mikrointerakcje** (hover, focus, animacje, sound feedback)
8. **Dlaczego to działa dla Teresy** (psychologia + ergonomia)
9. **Zagrożenia** (co może się nie sprawdzić)
10. **Paleta kolorów konkretna** (HEX) — wykorzystaj firmową ale możesz proponować akcenty

**Pomysły koncepcji (do inspiracji, nie kopiuj 1:1, twórz swoje):**

- Klasyczny dashboard z 4 panelami ale lepiej zaprojektowany
- Kanban (3 kolumny: Do zrobienia / W toku / Zrobione)
- Inbox-style (jak Gmail — 1 lista, każdy wpis to "wiadomość" do obsłużenia)
- Calendar-first (kalendarz na środku, listy po bokach)
- Map-first (mapa Polski z markerami hodowców)
- Timeline-first (oś czasu z dziś jako "now", przeszłość po lewej, przyszłość po prawej)
- Card-deck (każdy hodowca jako karta przewracana jak Tinder — odrzuć/zaakceptuj/zadzwoń)
- KPI-first (cele dzienne na górze "X/Y wykonane", lista to drugi plan)
- Wykresowo-analityczny (sparklines, heatmapy, panel BI)
- Minimalistyczny "calm" — duże fonty, mało elementów, dużo białej przestrzeni

---

### SEKCJA B: 10 KONCEPCJI OKNA MODYFIKACJI WSTAWIENIA

Aktualne okno (screenshot #2) ma 3 sekcje: podstawowe dane, Szablon Dostaw (tabela), przyciski. Zaprojektuj **10 alternatyw**.

Każda koncepcja:

1. **Nazwa**
2. **Główna idea**
3. **ASCII mockup** całego okna (ok. 960×720 typowe modal)
4. **Co lepsze niż obecne:**
   - Lepsza walidacja w czasie rzeczywistym (np. czerwone obramowanie gdy liczba sprzeczna)
   - Wizualizacja "różnicy" jako pasek progresu zamiast liczby
   - Auto-uzupełnianie cyklu (system sugeruje 2 dostawy bazując na historii hodowcy)
   - Mini-wykres ostatnich wstawień tego hodowcy
   - Powiązane dostawy z LibraNet (live!)
   - Avatar hodowcy + szybki call-out z notatkami
5. **Konkretne UI elementy** (NumericUpDown vs Slider, DatePicker vs InlineCalendar, etc.)
6. **Klawiatura first** — wszystkie pola dostępne bez myszy
7. **Mikrocopy**
8. **Mikrointerakcje**
9. **Dlaczego to działa dla Teresy**
10. **Zagrożenia**

**Pomysły koncepcji:**

- Tradycyjny formularz ale piękniej (zachowuje strukturę, polepsza wygląd)
- Wizard krokowy (3 kroki: Hodowca → Data + Ilość → Dostawy → Zapisz)
- Side-by-side z podglądem (lewa: formularz, prawa: live preview jak wstawienie wygląda)
- Spreadsheet-style (jak Excel, edytujesz komórki bezpośrednio)
- Conversational UI (chat-like: "Kogo dodajemy?" → wybierz → "Ile sztuk?" → wpisz...)
- Card-flip (front: podstawowe, back: zaawansowane szczegóły)
- Inline expandable (rozwija się w samym wierszu listy, bez modala)
- Full-screen takeover (przejmuje cały ekran, bardzo komfortowo)
- Floating panel z auto-save (każda zmiana zapisywana, brak przycisku "Zapisz")
- Voice-first (mówisz "Słąbkowska 50 tysięcy 28 czerwca" → system rozpoznaje)

---

### SEKCJA C: 10 KONCEPCJI DIALOGU SZCZEGÓŁY WSTAWIENIA

Otwierany dwuklikiem na wierszu listy wstawień. Pokazuje wszystkie dane jednego wstawienia + dostawy + historię ważenia palet + klasy wagowe kurczaków + anomalie.

Każda koncepcja:

1. **Nazwa**
2. **Główna idea**
3. **ASCII mockup**
4. **Hierarchia danych** (co jest najważniejsze)
5. **Wizualizacje:**
   - Wykres słupkowy klas wagowych (4–12)
   - Histogram wagi palety (500-600 kg)
   - Pie chart procentowy
   - Sparkline trendu wstawień hodowcy
   - Map / sieć / network graph dostaw
6. **Mikrocopy**
7. **Mikrointerakcje** (drilldown gdy klikam wartość)
8. **Dlaczego to działa dla Teresy**
9. **Zagrożenia**
10. **Paleta kolorów**

**Pomysły koncepcji:**

- Klasyczne panele zakładkowe (3 taby: Podstawowe / Dostawy / Analiza)
- Single-page scroll (wszystko na jednej długiej stronie, scroll)
- Storytelling-style (chronologia: planowanie → wstawienie → odbiór → analiza)
- KPI-first (4-6 dużych kart na górze, szczegóły niżej)
- Sankey diagram (przepływ od wstawienia do dostaw)
- Heat-table (klasy wagowe × dni jako matryca kolorów)
- AI-assistant (Claude w panelu bocznym z analizą "Co tu się stało?")
- Comparison view (porównanie z poprzednim wstawieniem hodowcy)
- Visual timeline (oś poziomy z markerami wydarzeń)
- Minimalist data-table (jedna tabela, klucz=wartość, no chrome)

---

## WYMAGANIA TECHNICZNE I FORMATOWE

### Format Twojej odpowiedzi

Każda z 30 koncepcji w sekcji oznaczonej:

```
═══════════════════════════════════════════════════════
SEKCJA A — KONCEPCJA 1: [Nazwa]
═══════════════════════════════════════════════════════

💡 GŁÓWNA IDEA
(1-2 zdania)

📐 ASCII MOCKUP
┌──────────────────────────────────────────────────────┐
│  [logo] Centrum Zakupów    [search] [stats] [profil] │
├──────────────────────────────────────────────────────┤
│ ...                                                  │
└──────────────────────────────────────────────────────┘

🎨 PALETA KOLORÓW
- Primary: #5C8A3A
- Accent: ...

🧠 HIERARCHIA INFORMACJI
1. Najważniejsze: ...
2. Drugorzędne: ...

🔧 NOWE ELEMENTY
- ...
- ...

✍️ MIKROCOPY (PRZYKŁADY)
- "Teresa, masz dziś 12 telefonów"
- ...

⚡ MIKROINTERAKCJE
- Hover na karcie hodowcy → ...
- Klik na ⭐ → ...

✅ DLACZEGO DZIAŁA DLA TERESY
(uzasadnienie psychologiczne / ergonomiczne)

⚠️ ZAGROŻENIA
- ...

═══════════════════════════════════════════════════════
```

### Wymagania merytoryczne

- **NIE oszczędzaj słów.** Każda koncepcja musi mieć minimum 400 słów. Łącznie 30 koncepcji = oczekuję 12 000–20 000 słów odpowiedzi.
- **Mockupy ASCII konkretne** — nie abstrakcyjne. Z dokładnymi wymiarami px, kolorami HEX, tekstami po polsku.
- **Konkretne dane testowe** w mockupach (nie "Lorem ipsum", a "Słąbkowska Agnieszka 50 000 szt 28.06.2026")
- **Wskazuj konkretne komponenty WPF** które realizują pomysł (np. *"użyj `DataGrid` z `GroupStyle` zamiast osobnej `ListView`"*).
- **Cytuj rzeczywiste polskie sformułowania** zakupowca (*"Pani Słąbkowska zmieniła datę"*, *"Krystyna jeszcze nie potwierdziła"*).
- Jeśli koncepcja wymaga **nowych bibliotek/integracji** (np. Mapbox, MaterialDesignInXamlToolkit) — wyraźnie wskaż.
- Każda koncepcja musi być **inna**. Nie 10 wariantów tego samego.
- Bądź **odważny** — możesz proponować radykalne zmiany. Możesz też proponować zachowawcze ulepszenia. Mieszaj style.

### Po koncepcjach — REKOMENDACJA

Na końcu odpowiedzi (po wszystkich 30 koncepcjach) dodaj sekcję:

```
═══════════════════════════════════════════════════════
🏆 MOJA REKOMENDACJA
═══════════════════════════════════════════════════════
```

W niej:

1. **Top 3 koncepcje dla Panelu Głównego** (z 10) — które polecasz wdrożyć i dlaczego
2. **Top 3 koncepcje dla Modyfikacji Wstawienia** (z 10)
3. **Top 3 koncepcje dla Szczegółów** (z 10)
4. **Najlepsza kombinacja** (jedna koncepcja z A + jedna z B + jedna z C która razem tworzy spójny system)
5. **Roadmapa wdrożenia** — kolejność prac (najpierw to, potem to, na końcu to)
6. **Szacowany czas pracy** (junior dev WPF, ile tygodni)
7. **Główne ryzyka** całego redesignu
8. **Co NIE robić** (anty-wzorce do unikania w tym kontekście biznesowym)

---

## DODATKOWE INSTRUKCJE

- Jeśli czegoś brakuje Ci do dobrego designu — **zadaj pytanie ZANIM zaczniesz**, ale tylko jedno najważniejsze pytanie. Potem działaj.
- Pisz po **polsku**. Nazwy techniczne (DataGrid, StackPanel, RowDefinition, IValueConverter) zostawiaj po angielsku.
- Każda koncepcja jest niezależna — nie odwołuj się "jak w koncepcji 3" — bo czytelnik może czytać tylko jedną.
- Nie używaj słów-wytrychów ("nowoczesny", "intuicyjny", "user-friendly") — pisz konkretnie co i jak.
- Pisz jakbyś projektował **dla siebie do codziennej pracy** — nie dla portfolio na Dribbble.

---

## OCZEKIWANY EFEKT

Sergiusz (właściciel firmy + programista aplikacji) wybierze 2-3 koncepcje z Twojej propozycji, podgryzie te najlepsze pomysły z każdej i razem zbudujemy nowe Centrum Zarządzania Zakupami które:

- ✅ Pracownik użytkuje **8h dziennie bez frustracji**
- ✅ Skraca czas obsługi 1 hodowcy z **3 minut do 30 sekund**
- ✅ Zwiększa **odpowiedzialność** (kto, kiedy, co zrobił → audit trail)
- ✅ Zwiększa **% odpowiedzi hodowców** na SMS-y (z 35% do 50%+)
- ✅ Łapie **anomalie** szybciej (hodowca z dziwną ilością, brakujące dostawy)
- ✅ Daje **satysfakcję** pracownikowi (visible progress, celebracja końca dnia)

GO! Czekam na 30 koncepcji + rekomendacje. Nie spiesz się.

==== KONIEC PROMPTU ====

---

## INSTRUKCJE WDROŻENIOWE PROMPTU (dla Ciebie, Sergiuszu)

### Krok 1: Zrób zrzuty ekranu
1. Otwórz `Panel Główny Wstawień` (max okno) → Print Screen → wklej do Paint → zapisz jako `screenshot_panel_glowny.png`
2. Otwórz dowolne wstawienie do edycji (PPM → Edytuj) → Print Screen → `screenshot_modyfikacja_wstawienia.png`

### Krok 2: Idź na claude.ai (najlepiej Sonnet 4.5 lub Opus 4)

### Krok 3: Pierwsza wiadomość
- Wklej cały tekst z bloku `==== START PROMPTU ====` do `==== KONIEC PROMPTU ====`
- Przeciągnij 2 screenshoty do okna chatu
- Przeciągnij `WidokWstawienia.xaml` (lub wklej zawartość jako tekst — pewnie XAML ma ~480 linii, zmieści się)
- Przeciągnij `WstawienieWindow.xaml`
- Przeciągnij `CLAUDE.md`

### Krok 4: Wyślij. Czekaj 5-15 minut.
Claude wygeneruje BARDZO długą odpowiedź. Jeśli odpowie krótko — napisz "Kontynuuj" lub "Brakuje sekcji C — daj 10 koncepcji szczegółów". Może być wymagane 2-3 wiadomości żeby dokończył wszystkie 30.

### Krok 5: Wybierz koncepcje
Przeczytaj wszystkie 30 + rekomendacje. Wybierz 2-3 ulubione. Wróć tutaj — wkleję mi wybrane koncepcje, a ja zaimplementuję w prawdziwym WPF.

### Pliki opcjonalne (gdy Claude poprosi)

- `WidokWstawienia.xaml.cs` — code-behind, 3800+ linii — możesz wkleić fragmenty (Setup*Columns, Load* metody)
- `WstawienieWindow.xaml.cs` — code-behind 2000+ linii
- `BAZA_WIEDZY/13_Bazy_danych.md` — opis 4 baz danych
- Inne screeny okien — jeśli Claude poprosi o `Statystyki`, `Audyt`, `Ostatnie Wstawienia`
