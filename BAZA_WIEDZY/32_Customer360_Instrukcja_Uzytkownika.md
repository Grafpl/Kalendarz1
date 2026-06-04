# Customer360 — instrukcja użytkownika

> Karta klienta 360° w ZPSP — wszystko o jednym kliencie w jednym oknie: KPI, sprzedaż, weryfikacja zamówień vs faktury, dane kontaktowe, notatki, scoring, transport, asortyment.
>
> Dokumentacja po commitach `6a4b3de..305006a` (Customer360 Faza 0–7 + warstwa „wierne zakresom dane / lepsze czytelne").
> Plik aktualizowany ręcznie — przy większych zmianach modułu zaktualizuj sekcję której dotyczą.

---

## 🧭 W 30 sekund — po co ta karta istnieje

**Wyobraź sobie linię produkcyjną** u Piórkowskich, gdzie żywiec wjeżdża, a po drugiej stronie wyjeżdża 200 ton mięsa dziennie. Karta Customer360 jest **odwrotną stroną tej linii** — pakuje rozproszone informacje o każdym kliencie z **3 różnych baz** (Sage, LibraNet, TransportPL) w **jeden ekran** i podaje ci je w 2 sekundy zamiast 20 minut grzebania po systemach.

### Analogia: teczka klienta vs 6 segregatorów

**Bez C360** masz 6 osobnych miejsc:
- Sage HANDEL → faktury, korekty, saldo, przeterminowane
- LibraNet → zamówienia, reklamacje, historia transportu
- KartotekaOdbiorcyDane → dane kontaktowe, preferencje
- Excel od Mai/Pauliny → notatki, ustalenia z rozmów (jeśli w ogóle)
- ProNova → kategoria handlowca, dane meldunkowe
- Twoja głowa → „kojarzę że ten klient zawsze ucina o 5%…"

**Z C360** masz **jedną teczkę** otwierającą się 2 kliknięciami z menu lub Kartoteki. Wszystko czego potrzebujesz żeby:
- **Przygotować się do telefonu** (15 sek zamiast 5 min)
- **Odpowiedzieć księgowej** „ile X zalega" (10 sek zamiast „muszę sprawdzić oddzwonię")
- **Zdecydować czy wydać towar w długi** (kliknięcie zamiast eskalacji do Sergiusza)
- **Wyjaśnić klientowi** dlaczego dostał 1380 kg zamiast 1500 (30 sek zamiast szukania w Sage + LibraNet równolegle)
- **Po rozmowie zapisać** ustalenia żeby kolega po zmianie też wiedział (notatka inline)

### Trzy zasady działania karty (pamiętaj jak idziesz robić cokolwiek)

1. **„Wierne dziś"** — żaden widok nie pokazuje zamówień planowanych na przyszłość jako „historii". Słupek lipca nie wyskoczy w maju.
2. **„Spójne z filtrem"** — selektor okresu (`CmbOkres`) zmienia WSZYSTKIE liczby w widokach historycznych. Wybierasz 6 mies — wszędzie 6 mies. Wyjątek: scoring i churn liczone z 12M kanonicznego (bo to ocena klienta, nie chwilowy stan — analogia: ocena ucznia za rok, nie za ostatni tydzień).
3. **„Świeże tam gdzie patrzysz"** — KPI tile w hero są ZAWSZE świeże z bazy. Cache 7-dniowy jest TYLKO dla obliczania scoringu (drogiego). `Ctrl+R` wymusza przeliczenie scoringu od zera.

---

## Spis treści

1. [Po co mi Customer360](#1-po-co-mi-customer360)
2. [Jak otworzyć kartę klienta](#2-jak-otworzyć-kartę-klienta)
3. [Pasek nagłówka — nawigacja i toolbar](#3-pasek-nagłówka--nawigacja-i-toolbar)
4. [Chipy obok nazwy klienta](#4-chipy-obok-nazwy-klienta)
5. [Sparkline trendu w toolbarze](#5-sparkline-trendu-w-toolbarze)
6. [Banner błędów ładowania](#6-banner-błędów-ładowania)
7. [Zakładka 📊 Przegląd](#7-zakładka--przegląd)
8. [Zakładka 💰 Sprzedaż](#8-zakładka--sprzedaż)
9. [Zakładka 👤 Klient](#9-zakładka--klient)
10. [Zakładka 📈 Analiza](#10-zakładka--analiza)
11. [Selektor okresu (CmbOkres) — pełna mechanika](#11-selektor-okresu-cmbokres--pełna-mechanika)
12. [Skróty klawiszowe](#12-skróty-klawiszowe)
13. [Eksport do PDF](#13-eksport-do-pdf)
14. [Konfiguracja scoringu](#14-konfiguracja-scoringu)
15. [Konwencje kolorów (cheat sheet)](#15-konwencje-kolorów-cheat-sheet)
16. [Scenariusze codzienne](#16-scenariusze-codzienne) + [Pułapki](#1699-pułapki--gdzie-ludzie-się-mylą-najczęściej) + [Pierwszy dzień nowego handlowca](#1616-pierwszy-dzień-nowego-handlowca-z-c360)
17. [Dlaczego liczby się nie zgadzają](#17-dlaczego-liczby-się-nie-zgadzają)
18. [Edge cases](#18-edge-cases)
19. [FAQ techniczne](#19-faq-techniczne)
20. [Mapa decyzji — flowcharty](#20-mapa-decyzji--krótkie-flowcharty)
21. [Cheat sheet A4 do druku](#21-cheat-sheet-a4--wydruk-nad-biurko)
22. [Słowniczek terminów technicznych](#22-słowniczek-terminów-technicznych-dla-nie-programistów)

---

## 1. Po co mi Customer360

Karta klienta 360° łączy dane z **3 baz** w jeden ekran:

- **HANDEL (Sage Symfonia)** — faktury sprzedaży, korekty, saldo, limit kredytowy, kategoria handlowca
- **LibraNet** — zamówienia, reklamacje, transport, asortyment, notatki
- **TransportPL** — kursy, kierowcy, ładunki (zakładka Transport)

### Komu służy i czemu

| Rola | Typowy moment użycia | Co zyskuje |
|---|---|---|
| **Maja, Paulina, Asia** (handlowcy) | Telefon klienta, spotkanie | „Czekaj, sprawdzę…" → odpowiedź w 5 sek zamiast oddzwaniania |
| **Sergiusz** | Codzienne przeglądy portfela, decyzje kredytowe | Scoring + chip churn = lista priorytetów bez ręcznego ogarniania |
| **Księgowość** | Pytanie „ile klient X zalega" | Saldo + przeterminowane bez logowania do Sage |
| **Każdy** | Po rozmowie z klientem | Notatka inline = kolega po zmianie wie co było |

### Analogia: jak czujnik temperatury na chłodni

Czujnik na chłodni mówi ci jedną liczbą **„+2.5°C"** czy łańcuch chłodniczy jest OK. Nie musisz schodzić do magazynu, otwierać drzwi, mierzyć termometrem. **Customer360 robi to samo dla klienta** — jedno spojrzenie (sparkline + chip churn + chip scoring) i już wiesz czy klient jest „na temperaturze".

### Konkretne minuty zaoszczędzone

| Czynność | Bez C360 | Z C360 |
|---|---|---|
| „Ile X zalega + jakie faktury" | 3-5 min (Sage + dzwonić do księgowej) | 5 sek (chip Przeterminowane) |
| „Co kupował w ostatnich 3 miesiącach" | 5-10 min (Sage + Excel) | 5 sek (CmbOkres 3M + Top 5) |
| „Czy ten klient ma reklamacje" | 5 min (przejść do modułu Reklamacje) | 2 sek (KPI Reklamacje) |
| „Jaką decyzję podjęliśmy ostatnio" | 10 min (pytać kolegów, szukać Excela) | 2 sek (zakładka Notatki) |
| Przygotowanie briefu przed spotkaniem | 30 min (kopiowanie z 3 systemów) | 5 sek (Ctrl+E → PDF) |

**Co BIERZEMY pod uwagę**: tylko `DataPrzyjazdu <= dziś` dla zamówień (po naszych fixach `a9e08a0`, `c148b01`, `77dc732`). Czyli żaden widok nie pokazuje zaplanowanych zamówień jako „historii" — analogia: kalendarz nigdy nie pokazuje jutrzejszej pracy jako już-wykonanej.

---

## 2. Jak otworzyć kartę klienta

Cztery ścieżki:

### A) Z menu głównego (jeden klient na raz)
**Menu → ANALITYKA → Customer 360** (kafelek pomarańczowo-niebieski, jeśli masz uprawnienia `accessMap[73]`).
Otwiera puste okno → klikasz **🔍 Wybierz klienta…** w toolbarze → picker z listą.

### B) Z Kartoteki Odbiorców
Dwuklik na wierszu klienta → otwiera Customer360 z preselektowanym klientem.

### C) Z Pulpitu Portfela
**Menu → SPRZEDAŻ → Pulpit Portfela** → dwuklik wiersza → karta klienta.

### D) Z Listy klientów z nawigacją ◀ ▶
Gdy otwierasz z Kartoteki lub Pulpitu, lista klientów jest przekazywana do okna — możesz przechodzić **◀** (poprzedni) i **▶** (następny) bez wracania do listy.

**Tryb porównania dwóch klientów**: w toolbarze klikasz **⚖ Porównaj** → picker wybiera klienta B → otwiera się okno `PorownanieKlientowWindow` (osobny widok z dwoma kolumnami KPI obok siebie).

---

## 3. Pasek nagłówka — nawigacja i toolbar

Górny pasek (jasne tło):

| Element | Co robi |
|---|---|
| **◀ ▶** | Nawigacja po liście klientów (gdy karta otwarta z Kartoteki/Pulpitu). Skróty `Ctrl+←` / `Ctrl+→` |
| **🔍 Wybierz klienta…** | Otwiera picker (lista z filtrem nazwy/NIP) |
| **🕘 Ostatnio otwarty** | Wraca do ostatniego klienta z `RecentClientsStore` |
| **CmbOkres** | Selektor zakresu danych (sekcja [11](#11-selektor-okresu-cmbokres--pełna-mechanika)) |
| **⚖ Porównaj** | Picker → otwiera `PorownanieKlientowWindow` (dwa klienci side-by-side) |
| **📥 CSV** | Eksport aktywnej tabeli (zakładka której jesteś) do CSV na Pulpit |
| **📄 PDF** | Eksport całej karty do PDF (sekcja [13](#13-eksport-do-pdf)). Skrót `Ctrl+E` |
| **🐛 Debug** | Otwiera `Customer360DiagWindow` — raport diagnostyczny SQL dla tego klienta (do zgłaszania błędów) |
| **🔄 Odśwież** | Przeładowuje dane. Skrót `F5`. **Ctrl+R** wymusza też przeliczenie scoringu (bypassuje cache 7-dniowy) |

Pod toolbarem — **nazwa klienta** (duży tekst) + chipy (sekcja [4](#4-chipy-obok-nazwy-klienta)) + sparkline (sekcja [5](#5-sparkline-trendu-w-toolbarze)).

---

## 4. Chipy obok nazwy klienta

Pasek nad zakładkami pokazuje **skróconą tożsamość klienta** — jak naklejki na opakowaniu produktu w sklepie: BIO / bez glutenu / data ważności. Patrzysz raz i wiesz najważniejsze.

> **Analogia ogólna**: chipy to **plakietki na koszulce** — pracownik chłodni od razu widzi „to kierowca, to brygadzista, to BHP". W C360: „to klient kat. A, ryzyko OK, scoring B+ z wzrostowym sparkline'em".

### 4.1 Chip Kategoria (`ChipKategoria`)
Pokazuje literę **A / B / C / D** z `KartotekaOdbiorcyDane.KategoriaHandlowca`. Kolor: A=niebieski, D=czerwony.

**→ Po co**: szybkie segregowanie portfela. Kategoria A = klient strategiczny, traktuj jak Halal eksport. D = blokada / windykacja, trzymaj na krótkiej smyczy. Ustawiasz to ręcznie (analiza długoterminowa), w odróżnieniu od Scoringu (liczy się automatycznie).

**→ Analogia**: jak **karta lojalnościowa w drogerii** — Gold/Silver/Bronze. Nie zmienia się przy każdej wizycie, ale wyznacza poziom obsługi.

**Ukryty** gdy brak ustawionej kategorii. Klik nic nie robi — edytujesz na **Klient → Dane**.

### 4.2 Chip Churn (`ChipChurn`)
Ryzyko odejścia klienta — liczone z **12M kanonicznych** (nie zmienia się z CmbOkres — patrz [sekcja 11](#11-selektor-okresu-cmbokres--pełna-mechanika)).

| Poziom | Ikona | Kryterium | Tło |
|---|---|---|---|
| **OK** | ✅ Aktywny | Kupuje regularnie | jasnozielone |
| **WATCH** | 👀 Obserwuj | Odstęp >2× normy LUB YoY < −30% | jasnożółte |
| **WARNING** | ⚠ Uwaga | Odstęp >4× normy | jasnopomarańczowe |
| **CRITICAL** | 🚨 Krytyczne | Odstęp >4× normy I YoY < −30% | jasnoczerwone |
| **UNKNOWN** | ❓ Brak danych | Klient bez zamówień w 12M lub bez daty | szare |

**→ Po co**: ranny przegląd portfela. Otwierasz 10 kart z Pulpitu — czerwony chip = zacznij telefon DZIŚ, nie za tydzień. To **system wczesnego ostrzegania** zanim klient sam zadzwoni „od września kupuję u konkurencji".

**→ Analogia**: jak **lampka „check engine" w samochodzie**. Nie wiesz co dokładnie się dzieje (musisz otworzyć tooltip / wjechać do warsztatu), ale wiesz że nie wolno ignorować.

**Tooltip** = pełne wyjaśnienie z liczbami („Brak zamówienia 87 dni (norma 30) + obrót YoY −45%"). Zawsze najpierw najechać kursorem przed reakcją — sezonowy klient też pokaże czerwone, ale to fałszywy alarm (patrz [Scenariusz M](#scenariusz-m-klient-w-trybie-sezonowym-np-masarnia-świąteczna)).

### 4.3 Chip Scoring (`ChipScoring`)
Litera **A / B / C / D / F** + punkty 0–100. Liczy się automatycznie z 4 składników (Obrót, Częstotliwość, Terminowość, Długość relacji — sekcja [14](#14-konfiguracja-scoringu)).

**→ Po co**: jednoliczbowa ocena „ile ten klient jest dla nas wart". Używasz przy:
- **Decyzjach kredytowych**: klient F = nie wydaj w długi, klient A = możesz pozwolić na chwilowe przekroczenie limitu
- **Priorytetyzacji obsługi**: telefon od klienta A odbierasz natychmiast, klient D — oddzwonisz
- **Rekomendowanym limicie**: scoring podpowiada „rozsądny limit kredytowy" obok litery (na Analiza → Scoring)

**→ Analogia**: jak **ocena BIK w banku**. Nie mówi „dobry/zły człowiek", mówi „jakie ryzyko podejmujesz dając mu kredyt". 4 składniki = 4 wymiary patrzenia jednocześnie.

**Różnica od Kategorii (4.1)**:
- **Kategoria** = ręczne oznaczenie strategiczne („to nasz klient priorytetowy")
- **Scoring** = automatyczna ocena bieżąca („tak wygląda na podstawie liczb")

Klient kategorii A może mieć scoring D (był strategiczny, dziś zaniedbany) — to ważny sygnał: **przepaść między tym jak go traktujemy a tym jak faktycznie kupuje**.

Klik chipa **nie otwiera** detalu — żeby zobaczyć rozbicie idź na **Analiza → ⭐ Scoring**.

Wszystkie chipy są `Visibility="Collapsed"` przy pustej karcie — pokazują się dopiero po załadowaniu klienta.

---

## 5. Sparkline trendu w toolbarze

Mała linia 90×22 px obok chipów (`ChipSparkline`). **6 ostatnich miesięcy obrotu z faktur** — najszybszy sygnał trendu BEZ klikania zakładek.

**→ Po co**: w 1 sekundę widzisz czy klient idzie w górę, w dół, czy stoi w miejscu. Bez sparkline'u musisz iść na zakładkę Sprzedaż, otworzyć Porównanie miesięczne, popatrzeć na słupki. Tu wszystko już jest w peryferyjnym wzroku.

**→ Analogia**: jak **EKG na monitorze pacjenta** — pielęgniarka nie czyta cyfr ciśnienia po cyfrze, patrzy na linijkę pulsu. Wzgórek = ok, opadająca prosta = zawołaj lekarza. Sparkline daje ci to samo dla obrotu.

**Kolor linii** = stan zdrowia klienta:
- **Zielony** (`#16A34A`) — ostatni miesiąc > pierwszy o **>5%** → klient rośnie
- **Czerwony** (`#DC2626`) — ostatni < pierwszy o **>5%** → klient spada
- **Szary** (`#64748B`) — płasko (±5%) → stabilny, ale uważaj

**Tooltip** (najazd kursorem) — lista 6 miesięcy z kwotami:
`6 ostatnich mies: sty 120k · lut 100k · mar 150k · kwi 130k · maj 180k · cze 200k`

→ Dobrze przed telefonem: szybkie kontekst „ostatnio kupował co miesiąc, teraz odwołał — coś się zmieniło".

**Ukryty** gdy jest mniej niż 2 punkty danych (klient bez historii faktur — pewnie nowy lub martwy).
Wartości min/max są skalowane do wysokości 22 px — różnice rzędu kilku procent są widoczne (linia ma kontrast nawet dla małych klientów).

---

## 6. Banner błędów ładowania

**Żółty pasek** (`ErrorBanner`) pokazuje się TYLKO gdy część renderów rzuciła wyjątek. Standardowo `Visibility=Collapsed`.

Treść: **„⚠ Nie udało się załadować: KPI hero, Weryfikacja. Pozostała część karty została wczytana — możesz spróbować ponownie."**
+ przycisk **🔄 Spróbuj ponownie** = re-uruchamia `LoadKlientAsync`.

**→ Po co**: starszy kod karty (przed `3d2e327`) zżerał błędy po cichu — wyciągasz kartę, KPI puste, ty myślisz „klient nic nie kupił", a w rzeczywistości RenderKpi rzucił NRE na pole którego model nie ma. **Teraz widzisz że coś się sypnęło** i wiesz że nie należy wierzyć temu co widzisz. Plus: pozostała część karty się wczytała, więc nadal możesz pracować — banner nie blokuje.

**→ Analogia**: jak **lampka „check engine"** w samochodzie — silnik dalej działa, dojedziesz do warsztatu, ale nie udawaj że wszystko jest OK. Bez lampki: jedziesz świetnie 200 km i nagle stoisz na środku autostrady bo coś było źle od początku.

**Co znaczy każdy element listy**:
- `KPI nagłówek` — `RenderHeader` failed (nazwa, NIP, kategoria, churn, scoring chip)
- `KPI hero` — `RenderKpi` failed (4 tile + chipy finansowe)
- `Scoring` — `RenderScoring` failed (hero card scoringu)
- `Wykres miesięczny` — `RenderMonthlyChart` failed (LiveCharts2)
- `Weryfikacja` — `RenderWeryfikacja` failed (porównanie zamówione vs zafakturowane)
- `Porównanie miesięczne` — `RenderPorownanieChart` failed (słupki ZK vs FK + % nad)
- `Anulowane` — `RenderAnulowaneHeader` failed (header sekcji anulowanych)
- `Alerty` — `RenderAlerty` failed (lista alertów)
- `Detal scoringu` — `RenderScoringDetal` failed (Analiza → Scoring)
- `Zakładka Klient` — `LoadKlientTabAsync` failed (Dane + Kontakty + Notatki)

Pełna treść wyjątku trafia do `Debug Output` (Visual Studio / DebugView) z prefixem `[C360 ...]`.

**Gdy banner widzisz codziennie**: kliknij 🐛 Debug → wyślij raport diagnostyczny do mnie/Sergiusza.

---

## 7. Zakładka 📊 Przegląd

Sekcja **HERO** + dwie sub-sekcje (Top towary, Alerty).

### 7.1 Sekcja HERO scoringu
**Górna karta**: koło z literą scoringu + kategoria („Solidny B (72/100 pkt)") + **rekomendowany limit kredytowy** + opis.
Pod nią pasek **4 chipów stanu**:

| Chip | Co pokazuje | Kolor |
|---|---|---|
| **Limit / Wykorzystanie %** | `WykorzystanieLimitProc` | Zielony <50% / żółty <80% / czerwony |
| **Przeterminowane** | `Przeterminowane` zł + max dni | Zielony gdy 0, czerwony gdy >0 |
| **Od ostatniego zam.** | dni + norma | Zielony jeśli < 2× normy, czerwony powyżej |
| **Reklamacje 12M** | liczba | (zostaje w `KpiReklamacjeProc` poniżej) |

⚠ **Pasek Limit** ma clamp do 100% — gdy klient ma 250% wykorzystania limitu, pasek pokazuje pełen 100%, ale **tekst pokazuje rzeczywisty %** ("250%"). To znany bug review #U3, zaplanowany do naprawy.

### 7.2 KPI FINANSOWE — 4 tile
Drugi rząd. Każdy tile **dynamicznie zmienia etykietę i wartość** wg `CmbOkres` (sekcja [11](#11-selektor-okresu-cmbokres--pełna-mechanika)).

> **Analogia ogólna**: 4 tile to **deska rozdzielcza w aucie** — prędkość, obroty, paliwo, temperatura. Każdy mówi co innego, ale razem dają pełen obraz „jak się jedzie".

#### Tile 1: OBRÓT (`TileObrot`)
- **Etykieta**: `OBRÓT 12 MIES` (lub 6 / 3 / CAŁA HISTORIA)
- **Wartość**: `ObrotOkres` zł (z faktur; fallback na zamówienia gdy faktur 0)
- **Sub**: `▲ 12.3% YoY` (lub `vs poprzedni 6 mies`, lub `Cała historia — brak okresu odniesienia` gdy okres=0)
- **Tło tile**: zielony (#ECFDF5) gdy YoY > +5%, czerwony (#FEF2F2) gdy YoY < -10%, biały pośrodku

**→ Po co**: pierwsza liczba na którą patrzysz przy negocjowaniu rabatu („zarobił dla nas 2.5M w 12M — zasłużył na -3%"), przy decyzji kredytowej („to nasz top 20, podnieś limit") albo przy obronie przed Sergiuszem („dlaczego ten klient ma tak duży rabat? bo ma X obrotu rocznie").

**→ Analogia**: jak **wskaźnik prędkościomierza** — pokazuje aktualne tempo. Strzałka YoY = przyspieszasz/zwalniasz vs poprzedni rok. Zielone tło = jedziesz lepiej niż w zeszłym, czerwone = hamujesz.

#### Tile 2: ŚR. WARTOŚĆ FAKTURY (`TileSrFaktura`)
- **Wartość**: `ObrotOkres / LiczbaFakturOkres` — średnia wartość jednej faktury
- **Sub**: `z N faktur (12M)` (lub 6M itd.)
- Tło: zawsze białe (neutralna metryka)

**→ Po co**: rozróżnia **klienta dużego z dużymi zakupami** (40 faktur × 50k = 2M) vs **klienta dużego z drobnymi częstymi zakupami** (200 faktur × 10k = 2M). Pierwszy — łatwa obsługa, mało papierów. Drugi — angażuje 5× więcej księgowej. **Jest podstawą do decyzji „opłaca się obsługiwać czy nie"** — analogia rabatu hurtowego w odwrotną stronę.

**Wcześniej** w tym miejscu była **„marża"** — wyliczana fałszywie jako `obrot × 12%`. Wywaliśmy ją w `6a4b3de` (Faza 1) — bo lepsze brak liczby niż zmyślona liczba.

#### Tile 3: ZAMÓWIENIA (`TileLiczbaZam`)
- **Etykieta**: `ZAMÓWIENIA 12 MIES` (dynamiczna)
- **Wartość**: `LiczbaZamowienOkres` (liczba zamówień, **bez przyszłych**)
- **Sub**: `SumaKgOkres kg łącznie`
- Tło: zawsze białe

**→ Po co**: częstość kontaktu z klientem. 50 zamówień / 12 mies = ~1 zamówienie tygodniowo = aktywna relacja. 5 zamówień = sporadyczny, prawdopodobnie pieczeniarz albo sezonowy. Im więcej tym **stabilniejszy strumień przychodu** — analogia: regularne raty kredytu zamiast jednorazowego zakupu za gotówkę.

**Suma kg** w sub to twardo policzony **wolumen towaru** — przydaje się przy rozmowach o zwiększeniu wolumenu („w zeszłym roku zrobiłeś u nas 250 ton, w tym idziemy do 300?").

#### Tile 4: LIMIT / DO ZAPŁATY (`TileLimit`)
- **Wartość**: `LimitKredytowy zł`
- **Sub**: `Do zapłaty: X zł · N fakt.`
- **Tło tile**: czerwony (#FEF2F2) gdy wykorzystanie >100%, jasno-amber (#FFFBEB) gdy ≥80%, białe poniżej

**→ Po co**: **najważniejszy tile do decyzji „wydać towar czy odmówić"**. Klient dzwoni „potrzebuję 200 kg pilnie" — rzucasz okiem: limit 100k, do zapłaty 90k = 10k wolnego. Towar za 8k przejdzie, towar za 15k — odmowa lub zapytanie do Sergiusza. **30 sekund decyzji zamiast 30 minut konsultacji**.

**→ Analogia**: jak **wskaźnik paliwa** — czerwone tło = rezerwa, jedziesz po szpitalach (klient zalega, ostrożnie). Amber = jeszcze możesz, ale planuj tankowanie (windykacja). Białe = pełen bak, jedź swobodnie.

### 7.3 Wykres trendu — Obrót miesięczny
LiveCharts2 (`CartesianChart`), 240 px wysokości. Słupki = obrót brutto z faktur per miesiąc + linia średniej.

**Klik słupka** → otwiera `SzczegolyMiesiacaDialog` (drill-down: faktury + zamówienia z tego miesiąca).

**TrendKierunek** w nagłówku wykresu — strzałka `▲ rośnie` / `▼ spada` / `▬ stabilnie` na podstawie średniej pierwszej połowy okresu vs drugiej.

### 7.4 Top 5 kupowanych towarów (`GridTopTowary`)
Tabela z kodem, nazwą, sumą kg, wartością, liczbą zamówień + **zdjęcie towaru** (BLOB z `LibraNet.TowarZdjecia`).
Sortowanie: SumaKg DESC.
**Tylko historyczne zamówienia** (DataPrzyjazdu ≤ dziś).

### 7.5 Alerty
Lista alertów wygenerowanych w `BudujListeAlertow(kpi, weryfikacja)`. Typowe:
- "Przeterminowane: 50 000 zł (max 45 dni)"
- "Klient nie zamawiał od 87 dni (norma 30)"
- "23% niedotrzymania w weryfikacji"
- "Limit przekroczony o 25 000 zł"

Każdy alert ma ikonę i kolor. Pusta lista = "Brak alertów ✓".

---

## 8. Zakładka 💰 Sprzedaż

5 sub-zakładek.

### 8.1 🛒 Zamówienia
`GridZamowienia` — wszystkie zamówienia z `dbo.ZamowieniaMieso` (bez anulowanych — te są w `❌ Anulowane`), **bez przyszłych** (DataPrzyjazdu ≤ dziś).

**Kolumny**:
- Id, DataZamowienia, DataPrzyjazdu, DataUboju, DataWydania
- Status
- Handlowiec (IdUser)
- SumaKg, LiczbaPozycji, Wartosc

Filtr searchowy z toolbara `TxtSzukajGrid` filtruje po aktywnym gridzie.

### 8.2 💰 Faktury
`GridFakturyDetail` + **baner FakturyDiag** na górze.

**Banner `FakturyDiag`**:
- `⚠ Brak faktur dla tego klienta w HANDEL (sprawdź czy khid = KlientId)` — gdy 0
- `📅 N faktur+korekt · 01.01.2026 – 28.05.2026 · [2024:5  2025:12  2026:8]` — przy normalnym wczytaniu
- ⚠ Brakuje wskazania że okres jest aktywny (review #U9, do naprawy)

**Kolumny**:
- Numer faktury, Data wystawienia, Typ_dk (FVS/FVR/FVZ/FKS/FKR)
- Brutto, Wartość netto, SumaKg
- Numer dokumentu źródłowego (dla korekt)

Faktury **WŁĄCZAJĄ KOREKTY** (`OR EXISTS iddokkoryg` — różnica względem zakładki Weryfikacja, patrz [17](#17-dlaczego-liczby-się-nie-zgadzają)).

### 8.3 ⚖ Weryfikacja
**Najważniejsza zakładka** dla handlowca — zamówione vs zafakturowane per towar.

**→ Po co**: **profilaktyka reklamacji**. Klient zamówił 1500 kg ćwiartki, dostał na fakturze 1380 kg — różnica 120 kg = 8% niedotrzymania. **Sam się o tym dowiesz przed klientem** i albo:
- wyjaśnisz proaktywnie („dzień dobry, nie wiem czy zauważył pan że…")
- zlecisz dosyłkę z następnym kursem
- zarobisz punkty zaufania zamiast tracić je w reklamacji

**→ Analogia**: jak **kontrola jakości po produkcji** w zakładzie — sprawdzasz że to co miało wyjść zgadza się z tym co wychodzi. Bez tego: klient odbiera, otwiera palety, dzwoni z reklamacją, ty się tłumaczysz. Z tym: ty dzwonisz pierwszy z planem rozwiązania.

**Werdykt hero** (`VerVerdict` + `VerSub`):
- `✅ Zafakturowane prawidłowo` — realizacja 98–105%
- `⚠ Częściowe niedotrzymanie` — realizacja 90–97%
- `❌ Mocno niedotrzymane` — realizacja 0–89%
- `📦 Brak zamówień w okresie` — gdy 0

**Filter chips** (`ChipUciete / ChipWiecej / ChipBrak / ChipZgodne`):
Klik chipa filtruje listę towarów. Każdy chip pokazuje liczbę pozycji w tym stanie.

**Lista per towar** — kod, nazwa, ZamowioneKg, ZafakturowaneKg, RoznicaKg, status.

**Drill-down**: dwuklik towaru → szczegóły wszystkich pozycji tego towaru.

⚠ **Faktyczna kalkulacja Weryfikacji ignoruje korekty** (`AND DK.typ_dk IN ('FVS','FVR','FVZ')` BEZ `OR EXISTS iddokkoryg`) — różni się od zakładki Faktury. To znany problem review #C1, oczekuje decyzji.
⚠ **Zamówione** = TYLKO zrealizowane (DataPrzyjazdu ≤ dziś), bez planowanych.

### 8.4 ❌ Anulowane
`GridAnulowane` — anulowane zamówienia (`Status IN ('Anulowane','Anulowano')`).

**Header `AnulHeader`** — dynamiczny wg okresu:
- `✅ Brak anulowanych zamówień w ostatnich 6 mies` (lub `w całej historii`)
- `❌ 5 anulowanych zamówień w ostatnich 12 mies`

**Sub `AnulSummary`** — `Łącznie 350 kg / 12 000 zł utraconego obrotu. Sprawdź powody.`

### 8.5 📊 Porównanie miesięczne
Słupki **niebieski (zamówione) | zielony/czerwony (zafakturowane)** per miesiąc. **% realizacji nad każdą parą** — kolor:
- **Zielony** (`#16A34A`) — ≥95%
- **Żółty** (`#EAB308`) — ≥80%
- **Czerwony** (`#DC2626`) — < 80%

`PorownanieInfo` poniżej: `Realizacja zamówień: 92.3% · zamówione 12 500 kg → zafakturowane 11 540 kg · różnica -960 kg (8 mies z zamówieniami)`.

**Tooltip słupka** (najazd kursorem):
```
Marzec 2026
Zamówione: 1 500 kg
Zafakturowane: 1 380 kg
Różnica: -120 kg
Realizacja: 92%
```

**Klik słupka** → drill-down `SzczegolyMiesiacaDialog` (jak w wykresie trendu).

**Okres respektowany** (po naszym fixie `c148b01`). **Bez przyszłości** (po fixie). **Zaufuj** liczbom z tej zakładki — to canonical przekrój zamówione vs zafakturowane.

---

## 9. Zakładka 👤 Klient

3 sub-zakładki.

### 9.1 ✏️ Dane
Pola edytowalne z `KartotekaOdbiorcyDane`:
- Nazwa (read-only, z HANDEL)
- NIP (read-only)
- Adres (read-only)
- **Osoba kontaktowa** (`EdOsoba`)
- **Telefon** (`EdTelefon`)
- **Email** (`EdEmail`)
- **Trasa** (`EdTrasa`) — kod trasy transportu
- **Preferowany dzień dostawy** (`EdDzien`) — pn/wt/śr/cz/pt
- **Preferowana godzina dostawy** (`EdGodzina`)
- **Adres dostawy (inny niż siedziba)** (`EdAdresDostawy`)
- **Preferencje pakowania** (`EdPrefPakowanie`)
- **Preferencje jakości** (`EdPrefJakosc`)
- **Notatki** (`EdNotatki`) — ⚠ stare pole, nie myl z nową zakładką Notatki ([9.3](#93--notatki))
- **Kategoria handlowca** (`EdKategoria`) — A/B/C/D dropdown

Przycisk **💾 Zapisz** (`BtnZapiszDane_Click`) → pisze do `KartotekaOdbiorcyDane` przez `KartotekaService.ZapiszDaneWlasneAsync`.

**Quick actions** (z toolbara nagłówka):
- 📞 Zadzwoń — `tel:` link (otwiera Skype/Teams jeśli ustawione domyślne)
- ✉ Email — `mailto:` link
- 📋 Kopiuj NIP

### 9.2 👥 Kontakty
`GridKontakty` + przyciski **➕ Dodaj / ✏ Edytuj / 🗑 Usuń**.

Dla osób kontaktowych klienta (księgowa, kierownik, dział reklamacji…) — każdy ma imię, telefon, email, rolę, notatkę.
Reuse `KartotekaService.PobierzKontaktyAsync / DodajKontaktAsync / ...`.

Empty state: "Brak dodanych kontaktów. Dodaj pierwsze kontaktem dla tego klienta po rozmowie."

### 9.3 📝 Notatki
**Nowa zakładka** (commit `305006a`) — dziennik klienta.

**→ Po co**: rozwiązuje **najczęstszy ból** w wieloosobowej obsłudze: „Co mu obiecałam ostatnio?", „Maja dzwoniła z nim w piątek, ale jest na chorobowym i nie wiem na czym stanęło". Bez notatek — telefon do koleżanki, próba odtworzenia z głowy, ryzyko że klient usłyszy „zaraz, ale Marta mi mówiła co innego…". Z notatkami — **30 sek scrollowania listy i jesteś w kontekście**.

**→ Analogia**: jak **dziennik kapitana** na statku. Każda zmiana wachty zaczyna się od przeczytania co poprzedni zostawił. Bez dziennika — chaos, kolejny kapitan nie wie czemu statek skręcił na wschód. Z dziennikiem — pełna ciągłość pomimo zmiany ludzi.

**→ Druga analogia (ważniejsza dla Sergiusza)**: jak **karta pacjenta w przychodni**. Lekarz nie pamięta każdego pacjenta — czyta historię, widzi „alergia na penicylinę", „był 3 mies temu z gorączką". Dzięki temu nie powtarza pytań i nie wraca do tematów które klient miał już dawno odhaczone.

**Pasek dodawania** na górze: TextBox + przycisk **➕ Dodaj**.
Tip: Ctrl+Enter w TextBoxie też dodaje (gdy się rozszerzy).

**Counter `LblNotatkiCount`**:
- `Brak notatek — dodaj pierwszą po rozmowie/spotkaniu.` (gdy 0)
- `3 notatek · ostatnia: 03.06.2026 14:32` (gdy >0)

**Lista** — `ItemsControl` z kartami:
- Treść (TextWrap)
- Data + autor (`App.UserID`)
- 🗑 Usuń (z potwierdzeniem)

Dane w `LibraNet.Customer360_Notatki` (tabela tworzona auto przy pierwszym wejściu w zakładkę — `SqlEnsure` w `Customer360NotatkiService`).

**Co WARTO zapisywać** (regułki działają lepiej niż nakazy):
- **Ustalenia z rozmowy** — „obiecaliśmy 30 dni dłuższego terminu na FVS/12345"
- **Sygnały handlowe** — „klient pyta o filety mrożone, na razie nie produkujemy ale warto śledzić"
- **Konflikty / sporne tematy** — „reklamacja R/123 wisi 3 tygodnie, klient zniecierpliwiony"
- **Decyzje strategiczne** — „blokada kredytu do uregulowania zaległości — decyzja Sergiusz 04.06"
- **Powody zmian zachowania** — „klient sezonowy, brak zam w VI-IX to norma — NIE alarmować"
- **Personalia** — „księgowa Anna, +48 600 ..., dzwonić po 9:00 (przed kawą zła)"

**Czego NIE pisać** (nie zaśmiecać):
- Liczb które są w innych miejscach karty (saldo, obrót) — to się aktualizuje, notatka się starzeje
- Plotek bez konkretnej akcji
- Ogólników typu „dobrze poszło" — co dokładnie?

**Typowe użycie**:
- `Maja, 2026-06-03 14:30: rozmowa z księgową — zwrócili uwagę na fakturę FVS/12345, sprawdzić`
- `Sergiusz, 2026-05-28 09:15: blokada kredytu do 15.06 — zalega 80k`
- `Asia, 2026-05-15 11:00: klient chce przejść na palety EUR zamiast E2`

---

## 10. Zakładka 📈 Analiza

**Lazy-load** — wczytuje dane dopiero przy wejściu w tę zakładkę (oszczędność czasu otwarcia karty, commit `c2caee3`).

4 sub-zakładki.

### 10.1 ⭐ Scoring
**Szczegółowy rozbiór scoringu 4-składnikowego** z `Customer360Scorer.BudujScore`.

**Hero scoringu** (kopia z Przegląd) + przycisk **⚙ Ustaw scoring** (otwiera `Customer360ScoringConfigWindow`, sekcja [14](#14-konfiguracja-scoringu)).

**4 składniki** — każdy pasek 0–100 + waga + opis:

| Składnik | Domyślna waga | Opis (treść aktualna z kpi po fixie `fe6e943`) |
|---|---|---|
| **Obrót 12M** | 35% | `{ObrotOkres:N0} zł w ostatnich 12 mies (z faktur)` — bierze świeże kpi.Obrot12M (canonical) |
| **Częstotliwość zamówień** | 25% | `Średni odstęp: 30 dni między zamówieniami` — z `SredniCzasMiedzyZamowieniami` |
| **Terminowość płatności** | 25% | `92% salda w terminie` — z `sc.TerminowoscProc` (cache) |
| **Długość relacji** | 15% | `Współpraca od 3.2 lat` — z `sc.LataRelacji` (cache) |

⚠ **Obrót i Częstotliwość są ŚWIEŻE** (z aktualnego kpi). **Terminowość i Długość są SNAPSHOT** (z cache scoringu, do 7 dni starsze — ale zmiana o tygodnie nie zaburza ułamków).

### 10.2 📜 Historia
`HistoriaZmianService` — wszystkie zmiany w danych klienta (kto, kiedy, co zmienił). Re-use z modułu Kartoteka.

### 10.3 🚚 Transport
**Trzy kolumny**:
- Kierowcy (kursy do tego klienta z ostatnich 12M)
- Pojazdy (ten sam zakres)
- Trasy

Dane z `TransportPL` + `LibraNet.ZamowieniaMieso` (klucz przez `KlientId`).

### 10.4 📦 Asortyment
**Pie chart** udziału towarów w obrocie klienta (`RenderAsortymentUdzial`).
LiveCharts2 `PieChart`, legenda po prawej.
Reuse `KartotekaService.PobierzAsortymentAsync`.

---

## 11. Selektor okresu (CmbOkres) — pełna mechanika

> **Analogia**: CmbOkres to **lupa nad mapą** — możesz zobaczyć całą Polskę naraz (cała historia) albo wczytać się w jedno województwo (3 mies). Ta sama mapa, różny poziom szczegółu. **Ważne**: niektóre rzeczy zostają stałe niezależnie od zoom-a (skala, kompas) — bo nie mają sensu zmieniać się przy każdym ruchu lupy. Tym jest scoring i churn — **patrz na klienta jak na całość, nie na chwilę**.

**→ Po co używać innego okresu niż 12M**:
- **6M** — szybki przegląd „co się dzieje w półroczu" (klient ostatnio mocno przyspieszył? wyhamował?)
- **3M** — kontekst do telefonu („ostatnio rozmawialiśmy 2 mies temu") — pokazuje tylko świeże zamówienia
- **Cała historia** — analiza długoterminowa, np. „czy to nasz klient sezonowy?"

Combo w toolbarze ma 4 wartości (commit `c30034b`):

| Indeks | Etykieta | `_okres` w kodzie | Zakres |
|---|---|---|---|
| 0 | **Cała historia** | 0 | bez dolnego/górnego ograniczenia (do dziś) |
| 1 | Ostatnie **12M** | 12 | -12..0 mies |
| 2 | Ostatnie **6M** | 6 | -6..0 mies |
| 3 | Ostatnie **3M** | 3 | -3..0 mies |

### Co ZMIENIA okres
- **KPI hero tile** — Obrót, Liczba zamówień, Suma kg, Liczba faktur, Śr. wartość faktury
- **Etykiety tile** — `OBRÓT 12 MIES` → `OBRÓT 6 MIES` (dynamicznie)
- **YoY label** — `YoY` → `vs poprzedni 6 mies` (dynamicznie)
- **Wszystkie listy/wykresy w zakładkach** — Zamówienia, Faktury, Weryfikacja, Anulowane, Porównanie miesięczne, Top 5 towarów
- **Header Anulowane** — `w ostatnich 6 mies` / `w całej historii`
- **Wykres trendu (Obrót miesięczny)** — przeskalowuje oś X

### Co NIE ZMIENIA okres
- **Scoring** (4 składniki) — zawsze liczony z 12M kanonicznego, bo to **całościowa ocena klienta**, nie chwilowy stan
- **Churn risk** — z 12M kanonicznego (`SredniCzasMiedzyZamowieniami` z 12M)
- **Chipy** — Kategoria, Churn, Scoring
- **Sparkline w toolbarze** — zawsze 6 ostatnich miesięcy (niezależny od okresu)
- **Reklamacje 12M tile** — zawsze 12M (technicznie limit serwisu)
- **PDF export** — zawsze 12M (canonical)
- **Comparison z PorownanieKlientowWindow** — zawsze 12M

### Jak to wpływa na YoY
- Okres 12M: YoY = obrót -12..0 vs -24..-12
- Okres 6M: YoY = obrót -6..0 vs -12..-6 (label `vs poprzedni 6 mies`)
- Okres 3M: YoY = obrót -3..0 vs -6..-3
- Okres 0 (cała historia): YoY = `Cała historia — brak okresu odniesienia`

---

## 12. Skróty klawiszowe

| Skrót | Akcja |
|---|---|
| `Esc` | Zamknij okno |
| `F5` | Odśwież — re-uruchom `LoadKlientAsync` z cache scoringu |
| `Ctrl+R` | **Wymuś** odświeżenie scoringu (bypassuje 7-dniowy cache w DB + pamięci) |
| `Ctrl+E` | Eksport całej karty do PDF |
| `Ctrl+←` | Poprzedni klient (gdy `_nawigacja` ustawiona) |
| `Ctrl+→` | Następny klient |
| `Ctrl+Enter` (w TextBoxie notatki) | Zapisz notatkę (do dorobienia w przyszłości) |

Implementacja w `Customer360_PreviewKeyDown`.

---

## 13. Eksport do PDF

**Skrót `Ctrl+E`** lub przycisk **📄 PDF** w toolbarze.

Generuje pełen brief klienta jako PDF (QuestPDF), zapisuje na Pulpit z nazwą `C360_<KlientId>_<Data>.pdf` i otwiera w domyślnej przeglądarce PDF.

**Co zawiera PDF** (`Customer360PdfExporter.Generate`):
- Nagłówek: nazwa, NIP, kategoria, handlowiec, scoring
- KPI 12M (canonical) — obrót, zamówienia, suma kg, faktury, śr. wartość faktury, limit, do zapłaty, przeterminowane
- Wykres obrotu miesięcznego (raster PNG z `ChartImageRenderer.RenderObrotMiesieczny`, 1000×320 px)
- Top 10 towarów
- Lista alertów

**NIE zawiera** (jeszcze):
- Notatek
- Pełnej historii faktur (tylko KPI agregaty)
- Detalu weryfikacji
- 1-page brief variant (z listy pomysłów, nie zrobione)

---

## 14. Konfiguracja scoringu

Okno `Customer360ScoringConfigWindow` — dostęp z **Analiza → Scoring → ⚙ Ustaw scoring**.

**13 parametrów + 4 progi**:

| Sekcja | Parametr | Domyślnie | Co zmienia |
|---|---|---|---|
| Wagi (suma = 100) | WagaObrot | 35 | Wpływ Obrotu na Total |
| | WagaCzestotliwosc | 25 | Wpływ Częstotliwości |
| | WagaTerminowosc | 25 | Wpływ Terminowości |
| | WagaDlugosc | 15 | Wpływ Długości relacji |
| Obrót | ObrotNaMaxPkt | 2 000 000 zł | Obrót dla 100 pkt (liniowo skalowane) |
| Częstotliwość | CzestBazaDni | 3 dni | Optimum = 100 pkt |
| | CzestSpadekNaDzien | 2.7 | Punkty traci za dzień powyżej bazy |
| Terminowość | TerminowoscBrakDanychPkt | 60 | Pkt gdy brak danych o terminowości |
| Długość | DlugoscLataNaMax | 3.0 | Lata = 100 pkt |
| | DlugoscMinPkt | 10 | Floor (klient bez faktur nie zero) |
| Progi liter | ProgA | 85 | Total ≥ 85 → A |
| | ProgB | 70 | Total ≥ 70 → B |
| | ProgC | 55 | Total ≥ 55 → C |
| | ProgD | 40 | Total ≥ 40 → D, niżej F |

**Walidacja** (przy zapisie):
- Suma wag = 100 (inaczej "Suma wag musi wynosić 100%")
- `ObrotNaMaxPkt > 0`
- `ProgA > ProgB > ProgC > ProgD > 0`
- **`ProgA ≤ 100`** (commit `3c135b2`) — zapobiega scenariuszowi "wszyscy F"

**Po zapisie**:
1. Wpisuje config do `LibraNet.Customer360_ScoreConfig` (tabela auto-ensure)
2. `InvalidateCache()` — czyści cache configu
3. `WygasCalyCacheAsync()` — invaliduje **wszystkie** scoringi (pamięć + DB)
4. Reload aktualnego klienta z `forceScore: true`

⚠ **Cache scoringu** — kazdy klient ma score zcachowany na 7 dni w `LibraNet.Customer360_ScoreCache`. Bez wygaszenia po zmianie config — wartości w detalu byłyby stare. WygasCalyCacheAsync rozwiązuje.

⚠ **Tabela `Customer360_ScoreCache` wymaga RĘCZNEGO deployu** ze `Customer360/Services/Sql/Customer360_ScoreCache.sql`. Bez tej tabeli scoring działa, ale liczy się od zera za każdym razem (degraduje do cache pamięci na 1h).

---

## 15. Konwencje kolorów (cheat sheet)

> **Analogia**: kolory w C360 to **semafor + triaż w izbie przyjęć**. Zielony — jedź / pacjent stabilny. Żółty — uwaga / czekaj. Czerwony — stop / interweniuj natychmiast. Spójne we wszystkich widokach: tile, chipy, słupki, % nad wykresem, banner błędów. Jak nauczysz się raz, czytasz wzrokiem bez zastanowienia.

| Kolor | Hex | Znaczenie | Gdzie typowo |
|---|---|---|---|
| Zielony | `#16A34A` | Dobrze, OK, wzrost | Sparkline w górę, % realizacji ≥95%, churn OK |
| Czerwony | `#DC2626` | Źle, alert, spadek | Sparkline w dół, przekroczony limit, churn CRITICAL |
| Żółty/amber | `#F59E0B` / `#EAB308` | Uwaga, pośrednio | % realizacji 80-95%, wykorzystanie limitu 80-100% |
| Niebieski | `#2563EB` / `#1E40AF` | Neutralna informacja, klient, faktura | Słupek „zamówione", scoring kolor klienta |
| Pomarańczowy | `#F97316` / `#FB923C` | Warning, drugi klient w porównaniu | Pomarańczowy w PorownanieKlientow = ten drugi |
| Fiolet | `#7C3AED` | Detal, mniej krytyczny sygnał | Składnik scoringu „Długość relacji" |
| Szary | `#64748B` / `#94A3B8` | Brak danych, neutralny | Sparkline płaski, „brak danych" placeholder |

**Blade tła hero tile** (commit `36a7e5d`):
- Jasno-zielone `#ECFDF5` — sukces (wzrost obrotu)
- Jasno-czerwone `#FEF2F2` — alert (spadek obrotu / przeterminowane)
- Jasno-amber `#FFFBEB` — uwaga (>80% wykorzystania limitu / reklamacje)

**Sparkline kolor linii**:
- Zielony `#16A34A` — wzrost >5%
- Czerwony `#DC2626` — spadek >5%
- Szary `#64748B` — płasko

---

## 16. Scenariusze codzienne

> Każdy scenariusz pokazuje **dlaczego to działa** + **co byś robił bez C360** + **co robisz z C360**. Pisane z perspektywy Mai/Pauliny/Asi/Sergiusza — kilkanaście realnych sytuacji które się powtarzają codziennie.

### Scenariusz A: Pierwszy raz otwieram kartę

1. Menu → Customer 360 → 🔍 Wybierz klienta
2. W pickerze wpisuję `WŁOSY` (nazwa hurtowni)
3. Wybieram „WŁOSY-DROBIARSTWO Sp. z o.o."
4. Karta się ładuje (spinner). Po 1–3 s widać dane.
5. **Pierwszy rzut oka — pasek na górze**: sprawdzam **sparkline** (czy linia idzie w górę / w dół) i **chip Churn** (czy nie krzyczy 🚨).
6. Jeśli wszystko zielone — czytanie nie jest pilne. Jeśli sparkline czerwony lub chip Churn ostrzega — kliknij **📊 Przegląd → Alerty** → przeczytaj listę.
7. Jeśli dalej niejasne — **📊 Sprzedaż → Porównanie miesięczne** → zobacz w którym miesiącu zaczął się spadek.

### Scenariusz B: Przed telefonem do klienta

**→ Po co**: różnica między „dzień dobry, jak mogę pomóc" a „dzień dobry, widzę że w maju zamówił pan filety mrożone — chciałbym pana zapytać czy jest pan z dostawy zadowolony, bo zauważyłem że ucięto wam o 5%". **Drugi telefon buduje relację, pierwszy ją traci.**

**Bez C360**: 5-10 minut grzebania w Sage + Excelu + telefon do koleżanki „pamiętasz tego klienta", potem dzwonisz mając wycinkową wiedzę.
**Z C360**: 30 sekund i jesteś w kontekście.

**Krok po kroku:**

1. Otwieram kartę przez Pulpit Portfela (mam listę problemów).
2. **CmbOkres → Ostatnie 3 mies** — chcę widzieć tylko ostatnie 3 miesiące współpracy. *Analogia: lupa nad mapą zoomuje na ostatnie wydarzenia.*
3. **📝 Notatki** → szybko czytam ostatnie 3 wpisy („co mówiliśmy ostatnio"). *Bez tego: pytałabym kolegów albo udawała że pamiętam.*
4. **📊 Sprzedaż → Faktury** → ostatnie 3 faktury (numer, kwota, status zapłaty). *Wiem czy zalega — temat „pieniądze" znowu wpłynie? Mam dane.*
5. **📊 Przegląd → Top 5 towarów** → co zazwyczaj zamawia. *Mogę zaproponować upsell: „zauważyłem że bierze pan głównie ćwiartki — mamy promocję na filety, mogłabym posłać próbkę?"*
6. **Chip Churn + sparkline** — jeszcze przed wybraniem numeru wiem czy „luźna rozmowa" czy „kryzys do ratowania".
7. Wybieram telefon z chip toolbara: 📞 (jeśli mam telefon ustawiony w danych własnych).
8. **Po rozmowie**: idę na **📝 Notatki** → wpisuję podsumowanie i klikam **➕ Dodaj**. *Dziennik kapitana zaktualizowany — Paulina po zmianie wie co było.*

### Scenariusz C: Klient pyta „dlaczego faktura mniejsza niż zamówienie"

**→ Po co**: klient kupuje od ciebie zaufanie razem z mięsem. „Nie wiem skąd ta różnica, oddzwonię" = -5 do zaufania. „Pokażę panu dokładnie co się stało" = +10. **C360 daje ci dane natychmiast podczas rozmowy**.

**Bez C360**: „muszę sprawdzić, oddzwonię w ciągu godziny" → minimum 20 min grzebania w 2 systemach → oddzwaniasz z odpowiedzią, klient już zdążył się zdenerwować.
**Z C360**: 30 sekund podczas tej samej rozmowy → klient ma odpowiedź zanim zdąży się zdenerwować.

**Krok po kroku:**

1. Klient mówi: „zamówiłem 1500 kg, a na fakturze widzę 1380 kg, dlaczego?".
2. Otwieram **📊 Sprzedaż → Porównanie miesięczne**.
3. Patrzę na słupek tego miesiąca — czerwony 92% nad parą = niedotrzymanie. *Widzę problem zanim klient skończył pytanie.*
4. **Klik słupka** → drill-down `SzczegolyMiesiacaDialog` — widzę listę faktur i zamówień. *Wszystkie pozycje na ekranie, identyfikuję który towar ucięto.*
5. Albo: **⚖ Weryfikacja** → klik chipa `✂ Ucięte` → widzę które towary były ucięte. *Filtr od razu pokazuje tylko problematyczne pozycje.*
6. Drill-down dwukliku towaru → szczegóły każdej pozycji.
7. Tłumaczę klientowi: brak na magazynie / problem produkcyjny / zmiana w specyfikacji. *Mówię konkretnie który towar, ile kg, czemu — bez gadulstwa.*
8. Wpis w **📝 Notatki**: „klient niezadowolony z FVS/12345, ucięto 8% — wytłumaczyłem".

**Analogia**: jak **kasjer ze skanerem cen w sklepie** — klient pyta „dlaczego ten chleb 8 zł a nie 6?", kasjer skanuje, pokazuje historię cen w monitorze, problem rozwiązany w 5 sek. Bez skanera: „muszę zadzwonić do menadżera". Klient jest w kolejce, niezadowolenie rośnie.

### Scenariusz D: Klient w czerwonym (Churn CRITICAL)

**→ Po co**: ratujesz relację **zanim się zerwie definitywnie**. Klient który 95 dni nie kupuje już się gdzieś przeniósł albo ma poważny problem. Każdy dzień zwłoki = mniejsza szansa że wróci.

**Bez C360**: dowiadujesz się przypadkiem („coś nie widzę ostatnio FVS dla X") albo wcale, dopóki klient sam nie zadzwoni z formalnym odejściem.
**Z C360**: chip churn 🚨 widoczny od momentu otwarcia karty. Codzienny przegląd Pulpitu Portfela → lista czerwonych klientów do oddzwonienia DZIŚ.

**Krok po kroku:**

1. Otwieram kartę, chip churn 🚨 czerwony.
2. **Tooltip chipa**: „Brak zamówienia 95 dni (norma 28) + obrót YoY −67%". *Już wiem o ile spadł.*
3. **CmbOkres → 12M** (jeśli było inaczej) — chcę widzieć pełny kontekst.
4. **📊 Przegląd → wykres trendu** — widzę gwałtowny spadek od września. *Mam punkt w czasie, mogę pytać „co się stało we wrześniu?".*
5. **📝 Notatki** — szukam czego dotyczył ostatni kontakt. *Może już ktoś wpisał „spór o cenę" — wiem o czym rozmawiać.*
6. **📊 Sprzedaż → Anulowane** — czy były anulowane w tym okresie (sygnał konfliktu).
7. **📊 Sprzedaż → Faktury → Przeterminowane?** — może klient nie zapłacił i ZAblokowaliśmy wydania (sami sobie strzeliliśmy w stopę).
8. **Quick action 📞** → dzwonię z pełną świadomością.
9. Po rozmowie: notatka + ewentualnie zmiana kategorii.

**Analogia**: jak **diagnoza w przychodni**. Lekarz nie strzela „pewnie grypa" — robi wywiad (historia objawów), oglądnięcie (sparkline + KPI), badania (Notatki, Anulowane, Przeterminowane). Dopiero potem zaleca leczenie (telefon + plan). Bez tej diagnozy: „proszę brać aspirynę" do każdego, co rzadko działa.

### Scenariusz E: Zmiana okresu z 12M na 6M

**→ Po co**: różne pytania wymagają różnej skali. „Jak ten klient ogólnie funkcjonuje" = 12M. „Co się dzieje OSTATNIO" = 6M lub 3M. Bez zmiany okresu odpowiadasz na każde pytanie tymi samymi liczbami — często nieadekwatnie.

**Bez C360 (lub bez zmiany okresu)**: wszystkie dane to 12M. Klient mówi „ostatnio dużo zamawiam" — ty patrzysz na liczbę z 12M (50 zamówień) i myślisz „spokojnie". A on miał na myśli „w ostatnich 3 miesiącach 20 zamówień zamiast typowych 5". Rozjazd interpretacji.
**Z C360**: 2 kliknięcia → cała karta odzwierciedla okres o który klient pyta.

**Krok po kroku:**

1. Klient mówi „ostatnio mało kupowałem, jak to wygląda u was".
2. **CmbOkres → 6M** w toolbarze.
3. **Wszystkie tile zmieniają wartości** (Obrót 6M, Zamówienia 6M, ...).
4. Etykiety zmieniają się dynamicznie („OBRÓT 6 MIES" zamiast 12).
5. YoY label: „vs poprzedni 6 mies" (zamiast „YoY") — czyli porównanie z okresem 12-6 mies temu.
6. Wykres trendu obrotu pokazuje 6 miesięcy.
7. **Sparkline** zostaje (zawsze 6M niezależnie od combo — to celowe, sparkline ma być stałym sygnałem).
8. **Scoring/Churn** zostaje (na bazie 12M canonical) — bo to ocena klienta jako CAŁOŚCI, nie chwilowy stan.
9. Top 5 towarów teraz zawiera tylko ostatnie 6M zakupów.

**Analogia**: jak **gałka „skala" w mapie GPS**. Z dużej wysokości widzisz autostrady, ze średniej — drogi krajowe, z bliska — ulice. Ta sama mapa, inny poziom szczegółu. Ale **strony świata (kompas) nie zmieniają się** — bo gdyby się zmieniały przy każdym zoomie, nigdy byś nie wiedział gdzie jest północ. Tym jest scoring w karcie — kompas.

### Scenariusz F: Sprawdzenie przeterminowanych przed wysłaniem windykacji

**→ Po co**: zanim wyślesz formalne pismo windykacyjne, sprawdzasz że nie strzelisz w stopę („zapomnieliśmy że kazaliśmy mu zapłacić ratą"). Cleanup przed eskalacją.

**Bez C360**: księgowa robi wybitnie zestawienie, ty patrzysz na liczby bez kontekstu, podpisujesz windykację. Czasem okazuje się że Sergiusz miał ustalenie „60 dni terminu zamiast 30" i widno.
**Z C360**: 1 minuta i masz pełen obraz: notatki + chip Limit + tile do zapłaty + lista faktur. Decydujesz „windykacja czy nie" z głową.

**Krok po kroku:**

1. Otwieram kartę.
2. **📊 Przegląd → chip Przeterminowane** — kolor czerwony i kwota.
3. **📝 Notatki** — sprawdzam czy nie ma wpisu „Sergiusz: termin 60 dni zaakceptowany" *(częsta pułapka — ktoś dał wydłużenie, nikt nie pamięta).*
4. **📊 Sprzedaż → Faktury** — filtruję po dacie, szukam przeterminowanych.
5. ⚠ W obecnej implementacji brak automatycznego filtra „tylko przeterminowane" — do dorobienia.
6. Eksport do CSV (📥) — wysyłam księgowej z komentarzem „te do windykacji, te z notatek poczekać".
7. **📝 Notatki** → „Wysłano windykację do księgowej 2026-06-03 dla FVS/12345..."

**Analogia**: jak **drugi pilot przed startem**. Pilot nie startuje sam — kapitan-2 przegląda listę kontroli, czyta na głos, kapitan-1 potwierdza. Cleanup procedury redukuje ryzyko głupiej decyzji. Notatki + dane finansowe + lista faktur = twoja lista kontroli przed windykacją.

### Scenariusz G: Po reklamacji klienta

**→ Po co**: reklamacja to **punkt ZWROTNY w relacji** — albo wzmocnisz zaufanie ("zajęliście się sprawnie, dziękuję"), albo zniszczysz ("dwa tygodnie i cisza"). C360 daje ci kontekst, żeby zająć się sprawnie.

**Bez C360**: czytasz reklamację, dzwonisz do klienta nie wiedząc czy to pierwsza czy 5-ta w tym roku, czy klient strategiczny czy okazjonalny. Odpowiadasz tym samym tonem co zawsze.
**Z C360**: zanim wybierzesz numer — wiesz że to klient z 10 reklamacjami (ostrożnie) albo pierwszą (sygnał że problemowi należy się uwaga). Ton dostosowujesz.

**Krok po kroku:**

1. Klient zgłosił reklamację na partię z 25.05.2026.
2. **📊 Przegląd → KPI Reklamacje 12M** — widzę chip czerwony. *Klient z historią reklamacji, czy pierwsza tego roku?*
3. **📊 Przegląd → Alerty** — szczegóły alertu o reklamacjach.
4. **Top 5 towarów** — szukam czy reklamowany towar jest top. *Jeśli tak — pilne, klient od tego zależy.*
5. **📝 Notatki** → wpisuję: „Reklamacja R/2026/123 — partia X z 25.05, problem Y. Powiązana faktura FVS/12345."
6. Otwieram **📋 moduł Reklamacje** (poza C360) żeby załatwić formalnie.

**Analogia**: jak **przyjęcie pacjenta na SOR**. Lekarz nie skupia się tylko na zgłoszonym objawie — pyta o historię (czy podobne wcześniej?), patrzy na dokumentację (alergie, leki). Reklamacja klienta = objaw. Historia w C360 = kontekst medyczny. Bez kontekstu: leczysz objaw, problem wraca.

### Scenariusz H: Klient nowy (1 faktura)

**→ Po co**: pierwsza dostawa to **bramka zaufania**. Klient zapamięta jak go traktowałeś. C360 mówi „tu się zaczyna historia" — od razu wiesz że wszystko jest świeże, brak długiej relacji do bazowania.

**Bez C360**: traktujesz tak jak każdego innego, nie zauważasz że to pierwszy raz, klient nie czuje się „zauważony".
**Z C360**: tile, sparkline ukryty, scoring „F" przez DlugoscMinPkt floor — wszystko krzyczy „NOWY". Wiesz że budujesz, nie kontynuujesz.

**Krok po kroku:**

1. Otwieram klienta świeżo dodanego.
2. **KPI tile** — Obrót pokazuje wartość jednej faktury, Faktur=1.
3. **YoY** — „Brak danych do porównania" (poprzedni okres pusty).
4. **Sparkline** — ukryty (`Visibility=Collapsed`) bo <2 punkty.
5. **Scoring** — litera niska (D albo F), bo Długość relacji = 1 dzień (DlugoscMinPkt floor). *Nie obrażaj się — to nie odzwierciedla potencjału, tylko historię.*
6. **Churn** — „Aktywny ({N} dni od ostatniego, norma 30)" — przy 1 zamówieniu fallback na 30 dni.
7. **📝 Notatki** → zacznij od pierwszego wpisu: „Nowy klient, pierwsza dostawa OK, oczekuje regularnych zam. tygodniowo. Polecony przez X."

**Analogia**: jak **nowo zatrudniony pracownik**. Nie oceniaj go „nigdy nie zrobił nic dla firmy" (formalnie prawda), tylko „witaj, ucz się, pokaż się". Nowy klient w C360 zostawia po pierwszym ślad — Notatki to twój sposób na „witam, oto co już wiem o tobie".

### Scenariusz E: Zmiana okresu z 12M na 6M

1. Klient mówi "ostatnio mało kupowałem, jak to wygląda u was".
2. **CmbOkres → 6M** w toolbarze.
3. **Wszystkie tile zmieniają wartości** (Obrót 6M, Zamówienia 6M, ...).
4. Etykiety zmieniają się dynamicznie ("OBRÓT 6 MIES" zamiast 12).
5. YoY label: "vs poprzedni 6 mies" (zamiast "YoY").
6. Wykres trendu obrotu pokazuje 6 miesięcy.
7. **Sparkline** zostaje (zawsze 6M niezależnie od combo).
8. **Scoring/Churn** zostaje (na bazie 12M canonical) — to ważne, scoring nie powinien wahać się od tego co user wybrał w combo.
9. Top 5 towarów teraz zawiera tylko ostatnie 6M zakupów.

### Scenariusz F: Sprawdzenie przeterminowanych przed wysłaniem windykacji

1. Otwieram kartę.
2. **📊 Przegląd → chip Przeterminowane** — kolor czerwony i kwota.
3. Klik chipa → otwiera **📊 Sprzedaż → Faktury** (lub pozostaję na Przeglądzie z dolną sekcją).
4. **📊 Sprzedaż → Faktury** — filtruję po dacie, szukam wszystkich z `data + plattermin < dziś` i niezamkniętych.
5. ⚠ W obecnej implementacji nie ma automatycznego filtra "tylko przeterminowane" — to do dorobienia.
6. Eksport do CSV (📥) — wysyłam księgowej.
7. **📝 Notatki** → "Wysłano windykację do księgowej 2026-06-03 dla FVS/12345..."

### Scenariusz G: Po reklamacji klienta

1. Klient zgłosił reklamację na partię z 25.05.2026.
2. **📊 Przegląd → KPI Reklamacje 12M** — widzę chip czerwony.
3. **📊 Przegląd → Alerty** — szczegóły alertu.
4. **Top 5 towarów** — szukam czy reklamowany towar jest top.
5. **📝 Notatki** → wpisuję: "Reklamacja R/2026/123 — partia X z 25.05, problem Y. Powiązana faktura FVS/12345."
6. Otwieram **📋 moduł Reklamacje** (poza C360) żeby załatwić formalnie.

### Scenariusz H: Klient nowy (1 faktura)

1. Otwieram klienta świeżo dodanego.
2. **KPI tile** — Obrót pokazuje wartość jednej faktury, Faktur=1.
3. **YoY** — `Brak danych do porównania` (poprzedni okres pusty).
4. **Sparkline** — ukryty (`Visibility=Collapsed`) bo <2 punkty.
5. **Scoring** — litera niska (D albo E), bo Długość relacji = 1 dzień (DlugoscMinPkt floor).
6. **Churn** — `Aktywny ({N} dni od ostatniego, norma 30)` — przy 1 zamówieniu fallback na 30 dni.
7. **📝 Notatki** → zacząć od pierwszego wpisu "Nowy klient, pierwsza dostawa OK, oczekuje regularnych zam."

### Scenariusz I: Klient w trybie porównania

**→ Po co**: kontekst sprzedażowy. „Dlaczego ten klient ma większy rabat niż tamten" wymaga porównania. Liczby obok siebie obiektywizują rozmowę. Bez tego — argumentacja oparta na pamięci („wydaje mi się że X kupuje więcej").

**Bez C360**: otwierasz dwie karty równolegle, scrollujesz między nimi, nic się nie zgadza wymiarowo.
**Z C360**: jeden ekran, dwie kolumny, podświetlony wygrywający — gotowy materiał na rozmowę z Sergiuszem „dlaczego A ma 5% a B 3%".

**Krok po kroku:**

1. Mam klienta A otwartego.
2. Klikam **⚖ Porównaj** w toolbarze.
3. Picker — wybieram klienta B.
4. Otwiera się `PorownanieKlientowWindow` — dwie kolumny z KPI obok siebie.
5. Wiersze: Obrót 12M, Suma kg, Liczba zamówień, Limit, Do zapłaty, Przeterminowane.
6. Wartości lepsze są wyróżnione (zielony bold).
7. ⚠ Porównanie zawsze 12M (nie respektuje CmbOkres głównego okna — to świadome, bo porównanie ma być stabilną wagą).

**Analogia**: jak **dwie próbki na łuskach laboratoryjnych**. Nie patrzysz osobno „ta waży 50g, tamta 45g" — kładziesz obok siebie, łyski pokazują różnicę. Mózg nie zniekształca skali. Tak samo w PorownanieKlientow — obok siebie liczby się nie wykrzywiają w pamięci.

### Scenariusz J: Eksport karty do briefu na spotkanie

**→ Po co**: spotkania z klientem w biurze (rzadziej u nich) to **moment prawdy** — pokażesz że ich rozumiesz albo nie. PDF na 2 strony = ty masz wszystko przed sobą, klient widzi że jesteś przygotowany.

**Bez C360**: kopiujesz liczby z Sage do Worda, wykresy z Excela, wszystko ręcznie, godzina pracy. Albo idziesz „na pamięć" i potem ratujesz się telefonem do biura.
**Z C360**: 5 sekund i PDF gotowy. Czas zaoszczędzony idzie na rzeczywiste przygotowanie merytoryczne (co chcę osiągnąć), nie formatowanie.

**Krok po kroku:**

1. Klient za 2h przyjeżdża do biura.
2. **Ctrl+E** (lub przycisk 📄 PDF).
3. Spinner. Po 2–3 s otwiera się PDF.
4. Drukuję lub wysyłam emailem.
5. ⚠ PDF zawiera 12M canonical, nie respektuje CmbOkres.

**Analogia**: jak **karta lotu (flight plan) dla pilota**. Nie pamiętasz wszystkich namiarów — masz wydrukowane, podświetlone, pod ręką. Brief z C360 = twoja karta lotu na spotkanie. Bez niej: improwizujesz, klient to czuje.

### Scenariusz K: Dane się nie ładują (banner błędu)

**→ Po co**: kiedyś (przed `3d2e327`) ten sam scenariusz powodował **ciche błędy** — patrzyłaś na pustą kartę i myślałaś „klient martwy". Teraz banner mówi „uwaga, nie ufaj danym, spróbuj ponownie albo zgłoś".

**Bez bannera (stary kod)**: pracujesz na danych z części wczytanej, robisz złą decyzję, klient zauważa, tłumaczysz „system mi pokazał inaczej".
**Z bannerem**: widzisz że coś jest popsute → albo retry, albo Debug, albo zgłoś. Nie tracisz danych jako podstawy decyzji.

**Krok po kroku:**

1. Otwieram kartę, widzę **żółty banner** na górze Przeglądu.
2. Czytam: „Nie udało się załadować: KPI hero, Porównanie miesięczne".
3. Reszta karty wczytana — widzę listę faktur, mogę pracować.
4. Klikam **🔄 Spróbuj ponownie** w bannerze.
5. Jeśli błąd dalej występuje:
   - Klikam **🐛 Debug** w toolbarze
   - Czytam raport — gdzie zawodzi (HANDEL? LibraNet?)
   - **💾 Zapisz** raport do TXT na Pulpicie
   - Wysyłam do Sergiusza/mnie

**Analogia**: jak **kontrolka „silnik gorący"** w samochodzie. Auto dalej jedzie, ale wiesz że nie ufasz mu w pełni. Zatrzymujesz się przy najbliższym serwisie. Tak samo banner — pracujesz dalej, ale wiesz że KPI mogą być nieaktualne, decyzje krytyczne odłóż albo zweryfikuj inaczej.

### Scenariusz L: Klient zalega — szybka ocena „kontynuować vs wstrzymać"

**→ Po co**: to **codzienna decyzja Sergiusza** (i czasem Mai gdy Sergiusz nieobecny). Czas decyzji to czas, w którym klient w bramie czeka na towar. Im szybciej tym mniej napięcia po obu stronach.

**Bez C360**: telefon do księgowej („sprawdź ile zalega"), pamięć z rozmów („z miesiąc temu mówiła że zapłaci"), intuicja („wydaje mi się że to porządny gość"). Ryzyko: złe decyzje, eskalacja konfliktów.
**Z C360**: 4 spojrzenia × 5 sek = 20 sek na pełen obraz. Decyzja oparta na danych, nie intuicji.

**Krok po kroku:**

1. Telefon od księgowej: „klient X nie zapłacił faktury z marca, czy mam blokadę?".
2. Otwieram kartę.
3. **Hero scoringu**: litera + rekomendacja limitu. *„Powinien mieć limit 100k, ma 200k — niedopasowanie."*
4. **Chip Limit** — sprawdzam % wykorzystania. *„150% — przekroczony."*
5. **Chip Przeterminowane** — kwota i max dni. *„45k przeterminowane 38 dni."*
6. **Tile TileLimit** — jeśli tło czerwone = >100% wykorzystania = poważnie.
7. Decyzja: jeśli litera D/F + wykorzystanie >80% + przeterminowane >30 dni = blokada.
8. **👤 Klient → Dane → Kategoria handlowca → D** (zapisz).
9. **📝 Notatki**: „Wprowadzono blokadę kredytu do uregulowania FVS/123 (40k przeterminowane 35 dni). Decyzja Sergiusz 2026-06-04."

**Analogia**: jak **light na lotnisku** — pilot patrzy na 3 czerwone i wie że nie startuje. Nie debatuje, nie pyta wieży „naprawdę?". Czerwony = nie. Trzy czerwone (kategoria D + limit >100% + zaległość >30d) = blokada bez konsultacji.

**Ważne**: blokada jest **decyzją odwracalną**. Klient zapłaci, zmienisz kategorię z D na C, wróci do zwykłej obsługi. Notatka pokazuje przyszłej osobie dlaczego było D — żeby nikt nie cofnął przez przypadek.

### Scenariusz M: Klient w trybie sezonowym (np. masarnia świąteczna)

**→ Po co**: sezonowy klient to **fałszywie ujemny w prostym modelu**. Algorytm churn nie wie o sezonowości — krzyczy „bracku, klient gardzi nami!". Człowiek wie. Notatka neutralizuje fałszywy alarm dla wszystkich kolejnych osób.

**Bez C360**: każda nowa osoba która patrzy na klienta wpada w panikę („Boże, 200 dni bez zamówienia!"), dzwoni do klienta i robi sobie głupotę („dlaczego pan nie kupuje? zdarzyło się coś?"). Klient: „bo nie jest sezon, normalnie tak jest". Niepotrzebny stres.
**Z C360**: notatka „klient sezonowy" jest widoczna od razu. Czerwony chip churn ignorowany.

**Krok po kroku:**

1. Klient kupuje tylko w listopad–grudzień co rok.
2. CmbOkres → 12M → wykres pokazuje wzrost w sez. + płasko poza sez.
3. Churn pewnie krzyczy „Brak zamówienia 200+ dni" — fałszywy alarm.
4. **📝 Notatki**: wpisać raz na zawsze „Klient sezonowy — wzrost X-XII, reszta roku 0 zam. NIE jest churn-zagrożeniem."
5. Następna osoba na zmianie widzi notatkę i nie panikuje.
6. **Konfiguracja scoringu** — można rozważyć obniżenie wagi Częstotliwości albo zwiększenie progu CzestBazaDni żeby sezonowi byli mniej karani (sekcja [14](#14-konfiguracja-scoringu)).

**Analogia**: jak **niedźwiedź zimą w klinice weterynaryjnej**. Pierwszy raz widzisz niedźwiedzia śpiącego 4 miesiące → „ratuj!". Doświadczony weterynarz wie: hibernacja, normalne. Notatka „to hibernacja" w karcie zwierzęcia zapobiega każdej kolejnej panice. Twoja notatka „klient sezonowy" robi to samo.

---

### Scenariusz N: Klient blokowany z powodu zaległości — kontekst na rozmowę

**→ Po co**: gdy Sergiusz ustawił blokadę 2 tygodnie temu, klient dzwoni „dlaczego nie wydajecie towaru", a ty właśnie zaczęłaś zmianę. Bez kontekstu wyjdziesz na niekompetentną.

**Bez C360**: „proszę poczekać, sprawdzę z Sergiuszem". Klient czeka. Sergiusz w drodze, oddzwoni za godzinę.
**Z C360**: 5 sek na notatce — czytasz „blokada do 15.06, zalega 80k, decyzja Sergiusz 28.05". Mówisz konkretnie: „pan dyrektor Sergiusz Piórkowski ustalił z państwem 28 maja, że wstrzymujemy wydania do uregulowania faktury FVS/12345 o wartości 80 tys. zł. Mogę pomóc w przyspieszeniu płatności?". Klient: „Aaa, no tak, faktycznie".

**Krok po kroku:**

1. Klient dzwoni, pyta dlaczego nie wydajecie.
2. **📝 Notatki** — czytam ostatnie wpisy. *Widzę decyzję z datą i kontekstem.*
3. **📊 Przegląd → KPI Do zapłaty** — sprawdzam dokładną kwotę. *Mówię konkretnie.*
4. **Chip Limit** — sprawdzam % wykorzystania.
5. Powtarzam klientowi decyzję na podstawie notatki, **bez odsyłania do Sergiusza**.
6. Po rozmowie: notatka „klient zadzwonił 04.06, przypomniany kontekst blokady, obiecał płatność do 15.06".

**Analogia**: jak **zmiana wachty na statku w środku rejsu**. Poprzedni oficer zostawił dziennik: „kurs 270°, 10 węzłów, ostrzegano przed mgłą za 50 mil". Bez dziennika — pytałbyś każdego kto akurat śpi. Z dziennikiem — kontynuujesz bez zatrzymywania statku.

### Scenariusz O: Klient pyta „kiedy ostatnio coś u was zamawiałem"

**→ Po co**: częste pytanie od klienta sprawdzającego pamięć. „Aaa, w marcu chyba?". Ty z C360 mówisz konkretnie: „14 maja, 850 kg ćwiartki + 200 kg pałek na fakturę FVS/12345".

**Bez C360**: „muszę sprawdzić, oddzwonię".
**Z C360**: 3 sekundy. **Chip „Od ostatniego zamówienia"** w hero pokazuje liczbę dni. Sprawdziwsza dokładnie idziesz na **📊 Sprzedaż → Zamówienia** lub **Faktury**.

**Analogia**: jak **kasjer pamiętający klienta** — „witam, panie Janku, miało pan jajka tym razem? Zwykle bierze pan szynkę 200g". Ta osobista pamięć buduje lojalność. C360 jest twoim sztucznym ułatwieniem tego.

---

### 16.16 Pierwszy dzień nowego handlowca z C360

**→ Po co**: nowy handlowiec (Asia, ktoś po Mai) musi wsiąknąć w portfel klientów. Nie ma wspomnień, nie zna historii. C360 to **historia spisana** zamiast „mózg poprzednika".

**Plan dnia (zalecany)**:

1. **Rano: lista priorytetowa**. Otwórz **Pulpit Portfela** → filtr „moja kategoria handlowca". Zobacz 30-50 swoich klientów. Posortuj po obrocie 12M DESC — TOP 5 to twoi VIP-owie, ich pamiętaj na pamięć.

2. **Po kolei każdy z TOP 5**: otwórz kartę, przeczytaj:
   - Hero scoring + churn (czy zdrowy?)
   - **Notatki** (krytyczne — czego nauczyli się poprzednicy?)
   - Top 5 towarów (co kupuje regularnie)
   - **Dane** → preferencje (dzień dostawy, godzina, jakość, pakowanie)
   - Telefon + email zapisz do swojego notesu

3. **Pierwsze telefony**: dzwoń z **listy notatek** w ręku. Każda rozmowa zaczyna się od: „chciałem się przedstawić, jestem nową osobą która zajmuje się państwa kontem". Pokazujesz że masz kontekst — nie zaczynasz od zera.

4. **Po każdej rozmowie**: notatka „przedstawiłem się, ustaliliśmy że...". Zaczynasz budować swoją historię z klientem.

5. **Dzień 2-3**: powtórz dla klientów kategorii B (regularni, ale nie krytyczni).

6. **Tydzień 1**: weź **CmbOkres → 3M** dla każdego klienta i porównaj z 12M. Klient który spadł 50% w ostatnich 3M → priorytet do oddzwonienia.

**Analogia**: jak **lekarz przejmujący gabinet po koledze**. Pierwsze dni czyta karty pacjentów (zwłaszcza tych z chorobami przewlekłymi), zapoznaje się z ich preferencjami, dzwoni do najbardziej krytycznych żeby się przedstawić. Bez tego — pacjent przyjdzie, ty zaczniesz od zera, zaufanie spada. Z tym — kontynuacja opieki, pacjent nie zauważy zmiany lekarza.

**Pierwsza notatka nowego handlowca** dla każdego klienta:
```
Asia [data]: przejęłam obsługę po Mai. Pierwszy kontakt
telefoniczny [data]. Klient: zadowolony / niezadowolony /
neutralny. Ustaliliśmy: [konkrety]. Następny kontakt
planowany [data].
```

To natychmiast osadza cię w roli, plus poprzedni handlowcy widzą że kontynuujesz odpowiedzialnie.

---

## 16.99 Pułapki — gdzie ludzie się mylą najczęściej

> **Po co ta sekcja**: większość pomyłek nie wynika z błędu w danych, tylko z **interpretacji**. Te pułapki są opisane krótko żeby zaoszczędzić ci jeden konkretny błąd zanim go zrobisz.

### Pułapka 1: „Klient ma spadek bo widzę w sparkline'u" — a to listopad-grudzień
**Co się dzieje**: sparkline pokazuje 6 ostatnich miesięcy. Klient sezonowy w styczniu wygląda na „spadającego" — bo poprzednie miesiące to były sezonowe szczyty. Czerwony kolor straszy.
**Jak nie wpaść**: zerknij na **wykres miesięczny w Przeglądzie** (12 miesięcy). Jeśli widzisz powtarzający się wzór roczny — to sezon, nie problem.
**Analogia**: temperatura ciała 36°C wieczorem może wyglądać jak „spadek" względem 37°C w południe. Nie jesteś chora, jesteś po prostu w innej części dnia.

### Pułapka 2: „Scoring jest D, więc klient zły"
**Co się dzieje**: scoring liczy się z 4 składników. Klient świeży (1 miesiąc) automatycznie ma D, bo Długość relacji = 1 dzień (floor punktów). Nie znaczy że klient zły — znaczy że nowy.
**Jak nie wpaść**: spójrz na **detal scoringu** (Analiza → Scoring). Zobaczysz że Obrót i Częstotliwość mogą mieć 100 pkt, a tylko Długość relacji ciągnie w dół.
**Analogia**: nowy pracownik dostaje średnie oceny w przeglądzie — nie dlatego że jest zły, tylko że nie miał szansy się wykazać przez rok. Po roku obraz się wyrównuje.

### Pułapka 3: „CmbOkres = 3 mies, KPI tile mówią 12M"
**Co się dzieje**: po przejściu z innego okna karta może nie odświeżyć. Patrzysz na „Obrót 12 mies" mimo że wybrałaś 3M.
**Jak nie wpaść**: **F5** odświeża zawsze. Po zmianie CmbOkres etykiety **MUSZĄ** się zmienić („OBRÓT 6 MIES" zamiast 12). Jeśli nie zmieniają się — to bug, zgłoś.

### Pułapka 4: „Faktury vs Weryfikacja pokazują różne kg"
**Co się dzieje**: zakładka Faktury wlicza korekty (FKS/FKR), zakładka Weryfikacja nie. Różnica jest celowa (są pod review do decyzji).
**Jak nie wpaść**: jeśli klient pyta „ile mnie kosztował", używaj Fakture (kompletne pieniądze). Jeśli pyta „ile dostarczyliśmy fizycznie", używaj Weryfikacji (sama dostawa bez korekt księgowych).
**Analogia**: różnica między „ile zatankowałeś" a „ile faktycznie zapłaciłeś po rabacie kasiera" — obie liczby są prawdziwe, ale o czymś innym.

### Pułapka 5: „Po Ctrl+R scoring się zmienił"
**Co się dzieje**: zwykły refresh (F5) bierze scoring z cache 7-dniowego. Ctrl+R wymusza przeliczenie od zera. Czasem są drobne różnice (cena średnia faktury zmieniła się o 100 zł itd.).
**Jak nie wpaść**: domyślnie używaj F5. Ctrl+R tylko po zmianie config scoringu albo gdy podejrzewasz że cache jest nieaktualny.
**Analogia**: różnica między „świeże zdjęcie pacjenta" a „zdjęcie sprzed tygodnia ale dobrej jakości". Czasem trzeba świeże, najczęściej tygodniowe wystarczy.

### Pułapka 6: „Klient ma 0 zamówień ale dużą sumę kg"
**Co się dzieje**: ZamowieniaMiesoTowar (pozycje zamówień z ilościami) zaczęły być zapisywane od ~10/2025. Wcześniej zamówienia są w głównej tabeli bez pozycji. KPI „Suma kg" pokazuje 0 dla starych klientów.
**Jak nie wpaść**: patrz na komentarze w bannerze FakturyDiag — informuje od kiedy są pozycje.
**Analogia**: archeologia — masz kości dinozaura, ale brak ścięgien. Wiesz że istniał, ale wcześniejsze szczegóły są niedostępne.

### Pułapka 7: „Sparkline zielony, ale chip Churn czerwony"
**Co się dzieje**: sparkline = 6 ostatnich mies. Churn = całe 12M + ratio do średniej. Klient mógł odżyć ostatnie 6M (sparkline zielony) po długiej ciszy (churn dalej WATCH/WARNING bo nie wyrobił się jeszcze średniej).
**Jak nie wpaść**: oba sygnały są prawdziwe, ale o innym horyzoncie. Sparkline = krótki termin (czy obecnie się dzieje). Churn = średni termin (czy wzór zachowania jest zdrowy).
**Analogia**: gorączka spada (sparkline zielony) ale chory cały tydzień (churn ostrzega) — leczenie działa, ale pacjent jeszcze nie zdrowy.

### Pułapka 8: „Otworzyłem klienta, KPI puste"
**Co się dzieje**: 99% przypadków = banner błędu jest na górze, ale prześlepiłaś.
**Jak nie wpaść**: zawsze najpierw zerknij na top zakładki Przegląd — jeśli żółty pasek = nie ufaj danym, kliknij Spróbuj ponownie.

### Pułapka 9: „Konfiguracja scoringu ProgA = 105"
**Co się dzieje**: skoring liczy się 0-100, jeśli ProgA = 105 to nikt nigdy nie dostanie A. Wszyscy F. Po commit `3c135b2` jest walidacja, ale wcześniej można było zapisać.
**Jak nie wpaść**: trzymaj się domyślnych progów (85/70/55/40) chyba że masz konkretny powód zmiany. Jeśli zmieniasz — sprawdź na 3-5 klientach że scoring wyszedł sensowny.

### Pułapka 10: „Notatka znika"
**Co się dzieje**: kliknęłaś 🗑 omyłkowo. Potwierdzenie tak/nie wciskasz odruchowo „Tak".
**Jak nie wpaść**: notatka usunięta jest tracona definitywnie (brak undo). Jeśli ktoś usunął coś ważnego — zapisz natychmiast nową z treścią z głowy/Outlook.
**Analogia**: jak skreślenie wpisu w dzienniku okrętowym — jak nakreślisz, koniec. Nowa kartka, nowy wpis „przepraszam za pomyłkę, przywrócona treść".

---

## 17. Dlaczego liczby się nie zgadzają

Najczęstsze sytuacje, kiedy „te same dane" pokazują różne liczby w różnych miejscach.

### 17.1 KPI tile vs zakładka Sprzedaż
**Powód**: KPI hero respektuje CmbOkres, ale listy w zakładce też respektują CmbOkres. **Powinno się zgadzać** od commitu `c30034b`.
Jeśli się nie zgadza:
- Sprawdź czy CmbOkres jest ten sam (czasem combo nie odświeża automatycznie po zmianie klienta — `F5` pomaga).
- Sprawdź czy nie jest aktywny filtr searchowy `TxtSzukajGrid` (filtruje aktywny grid).

### 17.2 Faktury vs Weryfikacja
**Powód**: Faktury **WŁĄCZAJĄ korekty** (`OR EXISTS iddokkoryg`), Weryfikacja **NIE** (`typ_dk IN ('FVS','FVR','FVZ')` BEZ EXISTS).
**Skutek**: ZafakturowaneKg w Weryfikacji może być **niższe** niż suma w Fakturach (bo korekty zwiększające ilość nie są wliczane).
To znany problem review #C1. Rozwiązanie: dyskusja czy korekty mają być w Weryfikacji. Dziś — różnica jest, należy mieć świadomość.

### 17.3 Obrót 12M vs Obrót z Porównania miesięcznego
**Powód**: Obrót 12M w tile = z faktur (z fallbackiem na zamówienia). Obrót w Porównaniu = obrót zamówień + obrót faktur w słupkach.
**Sprawdzenie**: kliknij każdy słupek → drill-down pokaże dokładne wartości.

### 17.4 Sparkline pokazuje wzrost, ale tile pokazuje spadek YoY
**Powód**: Sparkline = ostatnie 6 mies. YoY = ostatnie 12M vs -24..-12.
Klient mógł mieć wzrost ostatnio (sparkline zielony) ale gorszy niż rok temu (YoY czerwony).
**Interpretacja**: trend ostatni jest pozytywny, ale historycznie spadek.

### 17.5 Detal scoringu pokazuje stary obrót
**Powód**: cache scoringu 7 dni w `LibraNet.Customer360_ScoreCache`. Detal pokazuje wartości z momentu obliczenia.
**Fix**: po commit `fe6e943` — Obrót 12M i Średni odstęp są **z aktualnego KPI** (świeże). Terminowość i Długość relacji nadal z cache.
**Wymuszenie świeżego scoringu**: `Ctrl+R`.

### 17.6 Liczba zamówień w hero vs w zakładce
**Powód**: hero (`LiczbaZamowienOkres`) = liczba unikalnych Id zamówień. Zakładka pokazuje WIERSZE (z `LEFT JOIN` na pozycjach). Powinno się zgadzać teraz.

### 17.7 Anulowane w nagłówku vs w gridzie
**Powód**: header (`AnulHeader`) jest dynamiczny wg okresu (commit `c30034b` `okresOpis`), grid pokazuje wszystkie wiersze z `GetAnulowaneZamowieniaAsync` (z parametrem `OKRES`).
**Powinno** się zgadzać. Jeśli nie — sprawdź czy `F5` pomaga.

### 17.8 Reklamacje 12M zawsze 12M (mimo zmiany CmbOkres)
**Powód**: `LoadReklamacjeSummaryAsync` nie ma parametru okresu — zawsze 12M.
**Interpretacja**: tile Reklamacje to canonical 12M, nie liczy się z combo. Akceptowalne.

---

## 18. Edge cases

### 18.1 Klient bez ani jednej faktury
- Hero `Obrót`: 0 zł (z fallbackiem na zamówienia)
- YoY: "Brak danych do porównania"
- Sparkline: ukryty
- Scoring: `Długość relacji = DlugoscMinPkt` (floor 10pkt — klient świeży = klient martwy, znany problem review #U4)
- Churn: `Aktywny (N dni od ostatniego, norma 30)` z fallback 30
- Weryfikacja: `📦 Brak zamówień w okresie`

### 18.2 Klient bez kontaktów
- Zakładka Kontakty: empty state "Brak dodanych kontaktów…"
- Toolbar 📞 i ✉ nie działają (brak telefon/email z dane własne) — przycisk widać, klik → nic.

### 18.3 Klient z 0 zł obrotem ale dużym saldem
Hero: Obrót 0 zł, Do zapłaty 50k.
Sytuacja: stary klient, zalega, nic nie kupuje już rok. Churn CRITICAL, blokada.
**Tile TileLimit** może być czerwony (>100% wykorzystania bez nowych zakupów).

### 18.4 Klient sezonowy
- Wykres: nierównomierne słupki
- Churn: krzyczy WARN/CRITICAL przez większą część roku — fałszywie
- **Rozwiązanie**: notatka opisująca sezonowość + osobiście pamiętać

### 18.5 Klient z bardzo wieloma korektami
- Obrót w fakturach = suma z korektami (mogą być ujemne)
- Może wystąpić **fałszywy YoY −80%** gdy okres ma dużo korekt a poprzedni nie (review #C2)
- **Rozwiązanie**: na razie świadomość. Naprawa po decyzji "liczyć korekty czy nie".

### 18.6 Klient z dwoma handlowcami w `ContractorClassification`
- Chip Handlowiec w nagłówku może pokazać alfabetycznie ostatniego (po fixie `213e685` używamy MAX zamiast TOP 1 bez ORDER BY — deterministyczne, ale nie zawsze "ostatnio przypisany").
- **Rozwiązanie**: jeden klient = jeden handlowiec w Sage. Jeśli widzisz dziwnego — wyczyść historię klasyfikacji w Sage.

### 18.7 Bardzo duży obrót (>10M zł)
- Format `FmtZl`: `12.5M`
- Sparkline normalizuje min/max — pokaże trend nawet przy ogromnych liczbach.
- Tile Obrót pokazuje surową liczbę z separatorem: `12 500 000 zł`.

### 18.8 Klient otwierany pierwszy raz w sesji a tabela `Customer360_Notatki` nie istnieje
- `Customer360NotatkiService.GetNotatkiAsync` najpierw uruchamia `SqlEnsure` (CREATE TABLE IF NOT EXISTS).
- Wymaga uprawnienia CREATE TABLE dla `pronova` na LibraNet (jest, bo Hodowcy tabele tworzone).
- Jeśli brak uprawnienia — catch swallow, lista notatek pusta, brak dodawania.
- **Sprawdzenie**: 🐛 Debug → szukać `[C360 notatki get]` w raporcie.

---

## 19. FAQ techniczne

**Q: Gdzie są zapisywane notatki?**
A: `LibraNet.Customer360_Notatki` (192.168.0.109). Tabela auto-tworzona przy pierwszym wywołaniu serwisu.

**Q: Czy mogę dodać kolumnę do tabeli notatek (np. „kategoria")?**
A: Tak, ale wymaga zmiany SQL ensure + model `NotatkaC360` + UI. Lepiej zgłoś jako pomysł — patrząc na codzienność może osobny ticket „kategorie notatek" jest sensowny.

**Q: Cache scoringu 7 dni — gdzie?**
A: `LibraNet.Customer360_ScoreCache`. Wymaga **ręcznego deployu** ze skryptu `Customer360/Services/Sql/Customer360_ScoreCache.sql`.

**Q: Czy mogę wymusić odświeżenie scoringu dla wszystkich klientów na raz?**
A: Po zmianie config — automatycznie (WygasCalyCacheAsync). Manualnie — TODO (do dorobienia jako narzędzie admin).

**Q: PDF nie otwiera się po wygenerowaniu**
A: Sprawdź czy masz domyślny program PDF (Acrobat / Edge / Foxit). Jeśli `Process.Start(path)` nie znajduje skojarzenia — plik jest na Pulpicie, możesz otworzyć ręcznie.

**Q: Wykres się crashuje przy konkretnym kliencie**
A: LiveCharts2 nie lubi NaN/Infinity. Sprawdź czy `kpi.Obrot12MPrev > 0` (przy 0 dzielenie da Infinity). Banner błędów to złapie i wyświetli.

**Q: Dlaczego porównanie miesięczne pokazuje tylko od ~10/2025?**
A: `dbo.ZamowieniaMiesoTowar` (pozycje zamówień) zaczęły być zapisywane od ~10/2025. Wcześniej zamówienia w `dbo.ZamowieniaMieso` istnieją, ale bez rozbicia na towary i ceny. PorownanieInfo informuje.

**Q: Skąd wiem ile mam realnie aktywnych klientów?**
A: `_aktywniCache` (1h cache) — klienci z `DataPrzyjazdu` w ostatnich 12M, `<= dziś`, nie anulowane.
Z poziomu UI: Pulpit Portfela → liczba klientów (KpiCount).

**Q: Jak dodać nową zakładkę do C360?**
A:
1. W `Customer360Window.xaml`: nowy `<TabItem Header="...">` wewnątrz odpowiedniej grupy
2. W `Customer360Window.xaml.cs`: nowa metoda `Render{Nazwa}` / `Load{Nazwa}Async`
3. Wywołanie w `LoadKlientAsync` (jeśli eager) lub w `ZaladujGrupeAsync` (jeśli lazy)
4. Build + test

**Q: Jak dodać nową kolumnę do KlientKpi?**
A:
1. `Customer360/Models/Customer360Models.cs` — dorzucenie property
2. `Customer360/Services/Customer360Service.cs` → `GetKpiAsync` — wypełnienie (raczej w istniejącym `LoadXxxSummaryAsync` lub nowym)
3. Jeśli używane w scoringu — `Customer360Scorer.cs`
4. Jeśli wyświetlane w UI — `Customer360Window.xaml` + `RenderKpi`
5. **Pamiętać**: PorownanieKlientowWindow i PdfExporter też mogą wymagać aktualizacji

---

## 20. Mapa decyzji — krótkie flowcharty

> **Po co**: 5 typowych decyzji którym C360 ma służyć, w formie „jeśli to → tamto". Wydrukuj i powieś nad biurkiem.

### 20.1 „Czy wydać towar w długi?"
```
                        +------------------------+
                        | Klient prosi o towar   |
                        | w długi (X zł)         |
                        +-----------+------------+
                                    |
                       Limit klienta - DoZaplaty >= X ?
                                    |
              +---------------------+--------------------+
              | TAK                                      | NIE
              v                                          v
   +---------------------+                     +-----------------+
   | Chip Przeterminowane|                     | Litera scoring? |
   | == 0 ?              |                     +--------+--------+
   +---------+-----------+                              |
             |                              +-----------+-----------+
   +---------+---------+                    | A / B                 | C / D / F
   | TAK               | NIE                v                       v
   v                   v          +------------------+     +--------------------+
+--------+    +----------------+   | Zapytaj Sergiusza|     | ODMÓW              |
| WYDAJ  |    | Sprawdź        |   | (małe ryzyko)    |     | + notatka          |
+--------+    | Notatki: czy   |   +------------------+     +--------------------+
              | jest wpis      |
              | "termin 60d"?  |
              +--------+-------+
                       |
              +--------+--------+
              | TAK             | NIE
              v                 v
        +----------+      +------------+
        | WYDAJ    |      | Zapytaj    |
        | + odnotuj|      | Sergiusza  |
        +----------+      +------------+
```

### 20.2 „Czy klient w czerwonym jest naprawdę zagrożony?"
```
+------------------------------+
| Chip Churn pokazuje CRITICAL |
+--------------+---------------+
               |
       Notatki zawierają wpis
       "klient sezonowy"?
               |
   +-----------+-----------+
   | TAK                   | NIE
   v                       v
+--------+      +------------------+
| IGNORUJ|      | Sparkline zielony?|
+--------+      +---------+--------+
                          |
              +-----------+-----------+
              | TAK                   | NIE
              v                       v
        +-----------+         +-----------------+
        | Odżywa,   |         | DZWOŃ PRIORYTET |
        | obserwuj  |         | + notatka po    |
        +-----------+         +-----------------+
```

### 20.3 „Klient pyta dlaczego mniejsza dostawa"
```
+------------------------+
| Klient: "Miało być     |
| 1500, dostałem 1380"   |
+-----------+------------+
            v
+-----------------------------+
| Otwórz C360 → Sprzedaż →    |
| Porównanie miesięczne       |
+-------------+---------------+
              v
   Słupek tego miesiąca?
              |
   +----------+----------+
   | Zielony 100%+       | Czerwony <100%
   v                     v
+---------------+   +----------------------+
| Może klient   |   | Klik słupka →        |
| się myli      |   | Drill-down miesiąca  |
| Sprawdź fakt. |   +----------+-----------+
+---------------+              v
                    +----------------------+
                    | Identyfikuj towar    |
                    | który ucięto         |
                    | Wytłumacz, notatka   |
                    +----------------------+
```

### 20.4 „Telefon z księgowej — ile X zalega?"
```
+-------------------------+
| Księgowa pyta o saldo X |
+-----------+-------------+
            v
+---------------------------+
| Otwórz C360 (jeśli aktywne|
| zostawiłaś na innym)      |
+-------------+-------------+
              v
+----------------------------+
| Chip "Przeterminowane"     |
| → kwota + max dni          |
| KPI "Do zapłaty" → łączne  |
| KPI "Limit"                |
+-------------+--------------+
              v
   Odpowiedz: "X zalega Y zł
   przeterminowane Z dni,
   limit kredytowy A, do
   zapłaty łącznie B"
   < 30 sekund od pytania >
```

### 20.5 „Brief klienta na spotkanie za 2h"
```
+------------------------+
| Spotkanie z klientem   |
| za 2h, brak czasu      |
+-----------+------------+
            v
+----------------------------+
| 1. Otwórz kartę            |
| 2. Ctrl+E (PDF całej karty)|
| 3. Wydrukuj                |
+-------------+--------------+
              v
   < 30 sekund pracy >
              v
+---------------------------+
| Resztę 2h spędź na        |
| MERYTORYCZNYM przygot.    |
| (cele, oczekiwania, etc.) |
+---------------------------+
```

---

## 21. Cheat sheet A4 — wydruk nad biurko

> **Po co**: jedna strona, najważniejsze rzeczy. Wydrukuj i powieś. Mózg się przyzwyczai w tydzień.

```
╔═══════════════════════════════════════════════════════════════════╗
║                Customer360 — ŚCIĄGAWKA                            ║
╠═══════════════════════════════════════════════════════════════════╣
║                                                                   ║
║  ◀  ▶          Poprzedni / następny klient (Ctrl+←/→)            ║
║  🔍             Wybierz klienta (picker)                          ║
║  🕘             Ostatnio otwarty                                  ║
║  CmbOkres      Cała / 12M / 6M / 3M (zmienia historię)           ║
║  ⚖             Porównaj z drugim klientem                         ║
║  📥             Eksport aktywnej tabeli do CSV                    ║
║  📄             PDF całej karty (Ctrl+E)                          ║
║  🐛             Debug (raport SQL przy błędach)                   ║
║  🔄             Odśwież (F5)                                      ║
║                                                                   ║
╠═══════════════════════════════════════════════════════════════════╣
║                                                                   ║
║  CHIPY OBOK NAZWY KLIENTA:                                        ║
║  • Kategoria A/B/C/D    → ręczna klasyfikacja strategiczna       ║
║  • Churn ✅👀⚠🚨        → automatyczne ryzyko odejścia (12M)     ║
║  • Scoring litera 0-100 → 4-składnikowa ocena auto                ║
║  • Sparkline 6 mies     → ostatnie 6 mies obrót (zielony=rośnie) ║
║                                                                   ║
╠═══════════════════════════════════════════════════════════════════╣
║                                                                   ║
║  KOLORY (jak semafor):                                            ║
║  🟢 Zielony   = OK, wzrost, w terminie                            ║
║  🟡 Żółty     = uwaga, pośrednio                                  ║
║  🔴 Czerwony  = alert, przekroczenie, spadek >10%                ║
║  ⚪ Białe     = neutralne, brak istotnego sygnału                ║
║  🟣 Szare     = brak danych                                       ║
║                                                                   ║
╠═══════════════════════════════════════════════════════════════════╣
║                                                                   ║
║  SKRÓTY KLAWISZOWE:                                               ║
║  Esc        → Zamknij okno                                        ║
║  F5         → Odśwież (cache scoringu zachowany)                 ║
║  Ctrl+R     → Odśwież + przelicz scoring od zera                 ║
║  Ctrl+E     → Eksport PDF                                         ║
║  Ctrl+←     → Poprzedni klient                                   ║
║  Ctrl+→     → Następny klient                                    ║
║                                                                   ║
╠═══════════════════════════════════════════════════════════════════╣
║                                                                   ║
║  PRZED TELEFONEM (30 sek):                                        ║
║  1. Sparkline                → trend OK?                          ║
║  2. Chip Churn               → czy nie krzyczy?                  ║
║  3. CmbOkres → 3M            → kontekst ostatni                  ║
║  4. Notatki                  → co mówiliśmy ostatnio             ║
║  5. Top 5 towarów            → co kupuje                         ║
║  6. Wybieram numer                                                ║
║                                                                   ║
║  PO TELEFONIE (15 sek):                                           ║
║  Notatki → wpisz podsumowanie + 🔄 Dodaj                         ║
║                                                                   ║
╠═══════════════════════════════════════════════════════════════════╣
║                                                                   ║
║  GDY COŚ NIE DZIAŁA:                                              ║
║  Żółty banner          → kliknij Spróbuj ponownie                ║
║  Dalej błąd            → 🐛 Debug → zapisz raport → wyślij Sergi  ║
║  Liczby nie zgadzają   → F5 (najpierw), potem rozdz. 17 docs     ║
║                                                                   ║
╠═══════════════════════════════════════════════════════════════════╣
║                                                                   ║
║  PEŁNA INSTRUKCJA: BAZA_WIEDZY/32_Customer360_Instrukcja...      ║
║                                                                   ║
╚═══════════════════════════════════════════════════════════════════╝
```

---

## 22. Słowniczek terminów technicznych dla nie-programistów

> **Po co**: w instrukcji są terminy które brzmią obco. Jedna definicja każdego.

| Termin | Co znaczy w prostych słowach | Analogia |
|---|---|---|
| **KPI** | Key Performance Indicator — kluczowy wskaźnik wydajności | „ile waży zwierzę" — jedna liczba mówiąca dużo |
| **Sparkline** | Mała linia trendu | Wykres na termomierze w aptece — kreseczka, nie tabela |
| **Tile** | Kafelek z danymi (białe pudełko z liczbą) | Karta w grze — jedna informacja na jednej karcie |
| **Cache** | Zapamiętany wynik żeby nie liczyć ponownie | Notatka na lodówce „mleko już mam" zamiast otwierać lodówkę za każdym razem |
| **Render** | Wyświetlenie czegoś na ekranie | Pieczenie ciasta — przepis → gotowe ciasto |
| **Drill-down** | Klik w element → szczegóły | Otwarcie pudełka z prezentami — każdy podarunek osobno |
| **YoY** | Year over Year — porównanie z tym samym okresem rok temu | „Czy lepiej niż rok temu?" |
| **Churn** | Ryzyko odejścia klienta | Pacjent który przestał przychodzić — czy umarł czy zmienił lekarza? |
| **Scoring** | Ocena automatyczna 0-100 + litera | Ranking szkolny — średnia ocena |
| **Hero** | Górna sekcja karty (najważniejsze) | Pierwsza strona gazety — nagłówki |
| **Hex (kolor)** | Kod koloru `#16A34A` (zielony) | Numer farby z palety RAL — uniwersalna nazwa koloru |
| **Toolbar** | Pasek narzędzi (przyciski na górze) | Tablica narzędzi w warsztacie |
| **Commit** | Zapis zmian w kodzie | „Stempel w księdze handlowej" — formalny zapis czego dotyczyła zmiana |
| **Build zielony** | Aplikacja kompiluje się bez błędów | Sprawdziłam, że auto pali zanim wyjadę |

---

## Aktualizacje dokumentu

| Data | Commit | Co zmienione |
|---|---|---|
| 2026-06-04 | `305006a` | Pierwsza wersja po Faza 0–7 + warstwa „wierne zakresom danym / czytelność" |

**Najnowsze commity Customer360 dotknięte przez tę instrukcję** (od najstarszego):
- `6a4b3de` Faza 1 — scoring konfigurowalny + marża usunięta
- `c3fc0a3` Anulowane wykluczone z historii i miesięcy
- `0cab2d9` Faza 2 — LiveCharts2 zamiast ręcznych słupków
- `a63bd42` Faza 3 — eksport PDF
- `ec45d77` Faza 4 — UX (spinner, Ctrl+E/R)
- `679dee2` Faza 5 — cache scoringu 7d
- `c2caee3` Faza 6 — lazy-load Analiza
- `124e3dd` Faza 7 — ekstrakcja KpiCalculator + regiony
- `213e685` PortfelService — 3 bugi (handlowiec / MAX termin / dormant z długiem)
- `a9e08a0` Weryfikacja pomija przyszłość
- `c148b01` Porównanie miesięczne — % nad słupkami + CmbOkres + bez przyszłości
- `77dc732` Wszystkie zapytania zamówień pomijają przyszłość
- `c864148` Hex→Brush helper dla całego okna
- `3c135b2` Walidacja ProgA ≤ 100
- `fe6e943` Detal scoringu używa świeżych wartości
- `7f8eb19` FmtKg / FmtZl explicit
- `c30034b` KPI hero respektuje CmbOkres (etykiety dynamiczne)
- `3d2e327` Banner błędów ładowania
- `1c3e631` Sparkline 6 miesięcy w toolbarze
- `36a7e5d` Blade tło hero tile wg statusu
- `305006a` Notatki handlowca per klient
