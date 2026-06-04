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
16. [Scenariusze codzienne](#16-scenariusze-codzienne)
17. [Dlaczego liczby się nie zgadzają](#17-dlaczego-liczby-się-nie-zgadzają)
18. [Edge cases](#18-edge-cases)
19. [FAQ techniczne](#19-faq-techniczne)

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

1. Otwieram kartę, chip churn 🚨 czerwony.
2. **Tooltip chipa**: "Brak zamówienia 95 dni (norma 28) + obrót YoY −67%".
3. **CmbOkres → 12M** (jeśli było inaczej).
4. **📊 Przegląd → wykres trendu** — widzę gwałtowny spadek od września.
5. **📝 Notatki** — szukam czego dotyczył ostatni kontakt (może wiem przyczynę).
6. **📊 Sprzedaż → Anulowane** — czy były jakieś anulowane w tym okresie (sygnał konfliktu).
7. **📊 Sprzedaż → Faktury → Przeterminowane?** — może klient po prostu nie zapłacił i zablokowaliśmy wydania.
8. **Quick action 📞** → dzwonię i pytam.
9. Po rozmowie: notatka + ewentualnie zmiana kategorii w **👤 Klient → Dane** (np. D = blokada).

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

1. Mam klienta A otwartego.
2. Klikam **⚖ Porównaj** w toolbarze.
3. Picker — wybieram klienta B.
4. Otwiera się `PorownanieKlientowWindow` — dwie kolumny z KPI obok siebie.
5. Wiersze: Obrót 12M, Suma kg, Liczba zamówień, Limit, Do zapłaty, Przeterminowane.
6. Wartości lepsze są wyróżnione (zielony bold).
7. ⚠ Porównanie zawsze 12M (nie respektuje CmbOkres głównego okna).

### Scenariusz J: Eksport karty do briefu na spotkanie

1. Klient za 2h przyjeżdża do biura.
2. **Ctrl+E** (lub przycisk 📄 PDF).
3. Spinner. Po 2–3 s otwiera się PDF.
4. Drukuję lub wysyłam emailem.
5. ⚠ PDF zawiera 12M canonical, nie respektuje CmbOkres.

### Scenariusz K: Dane się nie ładują (banner błędu)

1. Otwieram kartę, widzę **żółty banner** na górze Przeglądu.
2. Czytam: "Nie udało się załadować: KPI hero, Porównanie miesięczne".
3. Reszta karty wczytana — widzę listę faktur, mogę pracować.
4. Klikam **🔄 Spróbuj ponownie** w bannerze.
5. Jeśli błąd dalej występuje:
   - Klikam **🐛 Debug** w toolbarze
   - Czytam raport — gdzie zawodzi (HANDEL? LibraNet?)
   - **💾 Zapisz** raport do TXT na Pulpicie
   - Wysyłam do Sergiusza/mnie

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

1. Klient kupuje tylko w listopad–grudzień co rok.
2. CmbOkres → 12M → wykres pokazuje wzrost w sez. + płasko poza sez.
3. Churn pewnie krzyczy "Brak zamówienia 200+ dni" — fałszywy alarm.
4. **📝 Notatki**: wpisać raz na zawsze "Klient sezonowy — wzrost X-XII, reszta roku 0 zam. NIE jest church-zagrożeniem."
5. Następna osoba na zmianie widzi notatkę i nie panikuje.
6. **Konfiguracja scoringu** — można rozważyć obniżenie wagi Częstotliwości albo zwiększenie progu CzestBazaDni żeby sezonowi byli mniej karani (sekcja [14](#14-konfiguracja-scoringu)).

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
