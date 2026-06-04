# Customer360 — instrukcja użytkownika

> Karta klienta 360° w ZPSP — wszystko o jednym kliencie w jednym oknie: KPI, sprzedaż, weryfikacja zamówień vs faktury, dane kontaktowe, notatki, scoring, transport, asortyment.
>
> Dokumentacja po commitach `6a4b3de..305006a` (Customer360 Faza 0–7 + warstwa „wierne zakresom dane / lepsze czytelne").
> Plik aktualizowany ręcznie — przy większych zmianach modułu zaktualizuj sekcję której dotyczą.

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

Komu służy:
- **Handlowiec** (Maja, Paulina, Asia) — przed telefonem / spotkaniem sprawdza co się działo, ile zamówień, jakie reklamacje, czy są przeterminowane faktury
- **Sergiusz** — kontrola stanu portfela, decyzje kredytowe, scoring
- **Księgowość** — wgląd w saldo i przeterminowane bez logowania do Sage

**Co BIERZEMY pod uwagę**: tylko `DataPrzyjazdu <= dziś` dla zamówień (po naszych fixach `a9e08a0`, `c148b01`, `77dc732`). Czyli żaden widok nie pokazuje zaplanowanych zamówień jako „historii".

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

Pasek nad zakładkami pokazuje skróconą tożsamość klienta:

### 4.1 Chip Kategoria (`ChipKategoria`)
Pokazuje literę **A / B / C / D** z `KartotekaOdbiorcyDane.KategoriaHandlowca`. Kolor odpowiada kategorii (A=niebieski, D=czerwony).
**Ukryty** gdy brak ustawionej kategorii w danych. Klik nic nie robi — to czysty wskaźnik. Edytujesz na zakładce Klient → Dane.

### 4.2 Chip Churn (`ChipChurn`)
Ryzyko odejścia klienta — liczone w `Customer360KpiCalculator.ObliczChurn` z **danych KANONICZNYCH 12M** (nie zmienia się z CmbOkres).

| Poziom | Ikona | Kryterium | Tło |
|---|---|---|---|
| **OK** | ✅ Aktywny | Kupuje regularnie | jasnozielone |
| **WATCH** | 👀 Obserwuj | Odstęp >2× normy LUB YoY < −30% | jasnożółte |
| **WARNING** | ⚠ Uwaga | Odstęp >4× normy | jasnopomarańczowe |
| **CRITICAL** | 🚨 Krytyczne | Odstęp >4× normy I YoY < −30% | jasnoczerwone |
| **UNKNOWN** | ❓ Brak danych | Klient bez zamówień w 12M lub bez daty | szare |

**Tooltip** = pełne wyjaśnienie ("Brak zamówienia 87 dni (norma 30) + obrót YoY −45%").

### 4.3 Chip Scoring (`ChipScoring`)
Litera **A / B / C / D / F** + punkty 0–100 z `Customer360Scorer.BudujScore` (4 składniki konfigurowalne — sekcja [14](#14-konfiguracja-scoringu)).
Kolor tła = `KategoriaKolor` (zielony A → czerwony F).
Klik **nie otwiera** detalu — żeby zobaczyć rozbicie idź na **Analiza → ⭐ Scoring**.

Wszystkie chipy są `Visibility="Collapsed"` przy pustej karcie — pokazują się dopiero po załadowaniu klienta.

---

## 5. Sparkline trendu w toolbarze

Mała linia 90×22 px obok chipów (`ChipSparkline`). **6 ostatnich miesięcy obrotu z faktur** — najszybszy sygnał trendu BEZ klikania zakładek.

**Kolor linii**:
- **Zielony** (`#16A34A`) — ostatni miesiąc > pierwszy o **>5%**
- **Czerwony** (`#DC2626`) — ostatni < pierwszy o **>5%**
- **Szary** (`#64748B`) — płasko (±5%)

**Tooltip** (najazd kursorem) — lista 6 miesięcy z kwotami w formacie `Customer360Format.FmtZl`:
`6 ostatnich mies: sty 120k · lut 100k · mar 150k · kwi 130k · maj 180k · cze 200k`

**Ukryty** gdy jest mniej niż 2 punkty danych (klient bez historii faktur w okresie, lub zupełnie nowy).
Wartości min/max są skalowane do wysokości 22 px — różnice rzędu kilku procent są widoczne.

---

## 6. Banner błędów ładowania

**Żółty pasek** (`ErrorBanner`) pokazuje się TYLKO gdy część renderów rzuciła wyjątek. Standardowo `Visibility=Collapsed`.

Treść: **„⚠ Nie udało się załadować: KPI hero, Weryfikacja. Pozostała część karty została wczytana — możesz spróbować ponownie."**
+ przycisk **🔄 Spróbuj ponownie** = re-uruchamia `LoadKlientAsync`.

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
Drugi rząd. Każdy tile **dynamicznie zmienia etykietę i wartość** wg `CmbOkres` (sekcja [11](#11-selektor-okresu-cmbokres--pełna-mechanika)):

#### Tile 1: OBRÓT (`TileObrot`)
- **Etykieta**: `OBRÓT 12 MIES` (lub 6 / 3 / CAŁA HISTORIA)
- **Wartość**: `ObrotOkres` zł (z faktur; fallback na zamówienia gdy faktur 0)
- **Sub**: `▲ 12.3% YoY` (lub `vs poprzedni 6 mies`, lub `Cała historia — brak okresu odniesienia` gdy okres=0)
- **Tło tile**: zielony (#ECFDF5) gdy YoY > +5%, czerwony (#FEF2F2) gdy YoY < -10%, biały pośrodku

#### Tile 2: ŚR. WARTOŚĆ FAKTURY (`TileSrFaktura`)
- **Wartość**: `ObrotOkres / LiczbaFakturOkres` — średnia wartość jednej faktury
- **Sub**: `z N faktur (12M)` (lub 6M itd.)
- Tło: zawsze białe (neutralna metryka)

#### Tile 3: ZAMÓWIENIA (`TileLiczbaZam`)
- **Etykieta**: `ZAMÓWIENIA 12 MIES` (dynamiczna)
- **Wartość**: `LiczbaZamowienOkres` (liczba zamówień, **bez przyszłych**)
- **Sub**: `SumaKgOkres kg łącznie`
- Tło: zawsze białe

#### Tile 4: LIMIT / DO ZAPŁATY (`TileLimit`)
- **Wartość**: `LimitKredytowy zł`
- **Sub**: `Do zapłaty: X zł · N fakt.`
- **Tło tile**: czerwony (#FEF2F2) gdy wykorzystanie >100%, jasno-amber (#FFFBEB) gdy ≥80%, białe poniżej

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

| Kolor | Hex | Znaczenie |
|---|---|---|
| Zielony | `#16A34A` | Dobrze, OK, wzrost |
| Czerwony | `#DC2626` | Źle, alert, spadek |
| Żółty/amber | `#F59E0B` / `#EAB308` | Uwaga, pośrednio |
| Niebieski | `#2563EB` / `#1E40AF` | Neutralna informacja, klient, faktura |
| Pomarańczowy | `#F97316` / `#FB923C` | Warning, drugi klient w porównaniu |
| Fiolet | `#7C3AED` | Detal, mniej krytyczny sygnał |
| Szary | `#64748B` / `#94A3B8` | Brak danych, neutralny |

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

### Scenariusz A: Pierwszy raz otwieram kartę

1. Menu → Customer 360 → 🔍 Wybierz klienta
2. W pickerze wpisuję `WŁOSY` (nazwa hurtowni)
3. Wybieram „WŁOSY-DROBIARSTWO Sp. z o.o."
4. Karta się ładuje (spinner). Po 1–3 s widać dane.
5. **Pierwszy rzut oka — pasek na górze**: sprawdzam **sparkline** (czy linia idzie w górę / w dół) i **chip Churn** (czy nie krzyczy 🚨).
6. Jeśli wszystko zielone — czytanie nie jest pilne. Jeśli sparkline czerwony lub chip Churn ostrzega — kliknij **📊 Przegląd → Alerty** → przeczytaj listę.
7. Jeśli dalej niejasne — **📊 Sprzedaż → Porównanie miesięczne** → zobacz w którym miesiącu zaczął się spadek.

### Scenariusz B: Przed telefonem do klienta

1. Otwieram kartę przez Pulpit Portfela (mam listę problemów).
2. **CmbOkres → Ostatnie 3 mies** — chcę widzieć tylko ostatnie 3 miesiące współpracy.
3. **📝 Notatki** → szybko czytam ostatnie 3 wpisy ("co mówiliśmy ostatnio").
4. **📊 Sprzedaż → Faktury** → ostatnie 3 faktury (numer, kwota, status zapłaty).
5. **📊 Przegląd → Top 5 towarów** → co zazwyczaj zamawia (żeby zaproponować promocję).
6. Wybieram telefon z chip toolbara: 📞.
7. **Po rozmowie**: idę na **📝 Notatki** → wpisuję podsumowanie i klikam **➕ Dodaj**.

### Scenariusz C: Klient pyta „dlaczego faktura mniejsza niż zamówienie"

1. Klient mówi: "zamówiłem 1500 kg, a na fakturze widzę 1380 kg, dlaczego?".
2. Otwieram **📊 Sprzedaż → Porównanie miesięczne**.
3. Patrzę na słupek tego miesiąca — czerwony 92% nad parą = niedotrzymanie.
4. **Klik słupka** → drill-down `SzczegolyMiesiacaDialog` — widzę listę faktur i zamówień.
5. Albo: **⚖ Weryfikacja** → klik chipa `✂ Ucięte` → widzę które towary były ucięte.
6. Drill-down dwukliku towaru → szczegóły każdej pozycji.
7. Tłumaczę klientowi: brak na magazynie / problem produkcyjny / zmiana w specyfikacji.
8. Wpis w **📝 Notatki**: "klient niezadowolony z FVS/12345, ucięto 8% — wytłumaczyłem".

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

1. Telefon od księgowej: "klient X nie zapłacił faktury z marca, czy mam blokadę?".
2. Otwieram kartę.
3. **Hero scoringu**: litera + rekomendacja limitu.
4. **Chip Limit** — sprawdzam % wykorzystania.
5. **Chip Przeterminowane** — kwota i max dni.
6. **Tile TileLimit** — jeśli tło czerwone = >100% wykorzystania = poważnie.
7. Decyzja: jeśli litera D/F + wykorzystanie >80% + przeterminowane >30 dni = blokada.
8. **👤 Klient → Dane → Kategoria handlowca → D** (zapisz).
9. **📝 Notatki**: "Wprowadzono blokadę kredytu do uregulowania FVS/123 (40k przeterminowane 35 dni). Decyzja Sergiusz 2026-06-04."

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
