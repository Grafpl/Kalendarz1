# RUNDA 5 — Synteza wyników (CROSS_DB + LIBRANET_5 + HANDEL_2)

**Data:** 2026-05-04 21:00 (świeżo po Twoim "Zrobione")
**Pliki:** WYNIKI_CROSS_DB.txt (304 linii), WYNIKI_LIBRANET_5.txt (960 linii), WYNIKI_HANDEL_2.txt (730 linii)

---

## 🔥 NAJWAŻNIEJSZE ODKRYCIA

### 1. **`listapartii.HarmonogramLp` = 100% NULL** (37 810 / 37 810)
- Kolumna istnieje, dodana przez migrację `CreatePartieV2.sql`, ale **NIGDY nie była użyta**.
- Czyli Twój `WidokPartie` nie ma fizycznego linku partia↔harmonogram. Kojarzysz po `CustomerID + DataUboju` (pewnie z dryfem).
- **Implikacja:** Jeżeli chcesz pewnego linku partia→harmonogram (np. żeby wiedzieć, czy partia była z kontraktu/wolnego rynku), trzeba **backfill** po (CustomerID, DataUboju).

### 2. **`In0E.Operator2ID` / `In0E.Wagowy2` = 100% NULL pełna historia (2 111 413 wierszy)**
- Drugi operator nigdy nie był używany. Kolumny istnieją – nikt nie pisze.
- Out1A: ten sam obrazek (2M wierszy, zero Operator2).
- **Implikacja:** Zero-value duplikat schematu. Można bezpiecznie ignorować.

### 3. **`In0E.CustomerID` = 100% NULL w okresie 2026-02-03..2026-05-04** (160 894 wierszy)
- Out1A.CustomerID też NULL w 50 747 wierszach.
- **Implikacja:** Linkowanie ważenia produktu do klienta (do kogo poszło) **NIE działa** w obecnym strumieniu — pewnie wagowy ZPSP w hali nie wpisuje klienta na ważeniach 1A. Musisz to robić przez `DocOut0E + HeaderDocOut0E` (gdzie CustomerID istnieje, ale to 1041 dokumentów łącznie — używane RZADKO).

### 4. **HarmonogramDostaw — 47% bufor "Anulowany"** (2 040 z 4 391 ostatnio)
- Potwierdzonych: 2 244 (51%)
- Anulowanych: 2 040 (47%) — **prawie połowa harmonogramu jest anulowana**
- Do wykupienia: 484, B.Kontr.: 313, B.Wolny.: 206
- **Implikacja:** Anulacje to gigantyczny strumień. Dlaczego? Czy to klient odwołuje, czy hodowca nie chce zdać, czy ZPSP usuwa duplikaty?

### 5. **WagoCounter ↔ FarmerCalc dla dziś (4.05.2026) — sieroty 100%**
- Wago_CarLp 1..15 ma `wago_szt 3925-5777`, `farmer_kg 11740-13760`
- Ale `farmer_dek/farmer_polic` = NULL → **kolumny FarmerCalc.dekarygator/policzaczyzna nie istnieją** lub są inaczej nazwane.
- Wartość: dla auta z 1300 szt × 2.9 kg = 3770 szt waży średnio 13 200 kg = **3.5 kg/sztuka netto** (wcześniej myśleliśmy 1.97 kg)
- Albo to żywiec ze skrzynkami, albo `wago_szt` to inny licznik (klatki?). Trzeba zbadać kolumny FarmerCalc.

---

## 📊 LUDZIE — IDENTYFIKACJA UserID

Ostatecznie potwierdzeni z RUNDA 5:

| UserID | Imię/Nazwisko | Główna aktywność |
|--------|---------------|------------------|
| **432143** | **Anna Jedynak** | 1 804 zmian zamówień / 30 dni — TOP user systemu |
| **6521** | **Maja Leonard** | 742 zmian zamówień, 27 reklamacji, 65 klientów (handlowiec #2) |
| **6611** | **Justyna Chrostowska** | 60 zmian reklamacji od marca, sprawdza 6/m-c |
| **9911** | **Klaudia Osińska** | 57 zmian reklamacji od 13.04, sprawdza 12 w maju |
| **51991** | **Ilona Krakowiak** | 280 zmian zamówień, **721 akceptacji TransportZmian** (TOP akceptujący) |
| **2121** | **Teresa Jachymczak** | 234 zmian, 9 klientów, 8 aktywności |
| **23233** | Małgorzata Stępniak | 196 zmian zamówień |
| **9321** | **Renata Balcerak** | 31 zmian, 8 akceptacji transport |
| **11111** | **Administrator** | 27 zmian, 53 reklamacje (super-user) |
| **1122** | Paulina Koncka | 4 aktywności (rzadko) |
| **SYSTEM** | (procesy automatyczne) | 13 zmian, 5 reklamacji |

**Top zgłaszający TransportZmiany (30 dni):**
- Anna Jedynak: 801 (423 zaakceptowane, 378 oczekujące) — 47% w buforze!
- Ilona Krakowiak: 383 (10 zaakceptowane, 373 oczekujące) — **97% nieprocesowane!** (Ilona zgłasza, ale niezbyt kasuje?)
- Maja Leonard: 279 (150/129)
- Teresa Jachymczak: 239 (96/143)

**Top akceptujący:** Ilona Krakowiak 721 / śr. 688 minut do akceptacji (~11h). Administrator 23 / 6363 min (~4 dni).

**HANDLOWCY (HANDEL DB):**
- **Jola** — 151 klientów (TOP)
- **Maja Leonard** — 65 klientów
- **Ogólne** (kategoria fallback) — 61 klientów
- **Ania** — 32 klientów
- **Teresa Jachymczak** — 9 klientów
- **Radek** — 3, **Daniel** — 1

---

## 💰 SPRZEDAŻ — TOP PRODUKTY (HM.DP, pełna historia)

| Produkt | kod | Suma kg | Suma zł | Średnia cena |
|---------|-----|---------|---------|--------------|
| **Kurczak A** | 66443 | 5 598 406 | **43 933 992** | 8.03 zł/kg |
| **Filet A** | 66445 | 369 812 | **6 396 938** | 17.58 zł/kg |
| **Akumulator do agregatu** | 75723 | 2 szt | **4 668 870** | 2 334 435 zł!!! ⚠️ |
| **Ćwiartka** | 66444 | 343 851 | 1 960 299 | 5.81 zł/kg |
| **Kurczak żywy 7** | 69839 | 316 133 | 1 633 557 | 5.25 zł/kg |
| **Kurczak żywy 8** | 69838 | 156 334 | 847 258 | 5.40 zł/kg |
| **Filet I Mrożony** | 74036 | 33 060 | 561 909 | 17.25 zł/kg |
| **Noga** | 66835 | 38 830 | 317 794 | 8.07 zł/kg |
| **Korpus** | 66442 | 405 091 | 307 094 | 1.24 zł/kg |
| **Pojemnik E-2** | 73732 | 13 694 szt | 273 880 | 20 zł/szt |

⚠️ **Akumulator do agregatu** — 2 sztuki za 4.67 mln zł → to pewnie **SUPER-akumulator** (UPS dla całej fabryki?) albo **literówka faktury** (powinno być pewnie 4 668.70 zł, ktoś zostawił przecinek). Warto sprawdzić.

**Najmniej palet `liczba_dokumentow`:** Filet C (3), Kurczak B (2), Tuba (0), Trybowane ze skórą II (0). Klasy "B" i "C" prawie nie istnieją w sprzedaży.

---

## 📈 SPRZEDAŻ MIESIĘCZNA (FVS = faktura sprzedaży podstawowa)

| Miesiąc | Liczba FVS | Suma brutto |
|---------|------------|-------------|
| 2026-04 | 729 | 19 015 556 |
| 2026-03 | 815 | 21 889 151 |
| 2026-02 | 802 | 21 159 610 |
| 2026-01 | 746 | 19 388 975 |
| 2025-12 | 769 | 18 572 711 |
| 2025-11 | 759 | 17 119 979 |
| 2025-10 | 938 | **29 342 419** ← peak Q4 |
| 2025-09 | 941 | 25 677 763 |
| 2025-08 | 908 | 25 520 292 |
| 2025-07 | 1 007 | 24 917 222 |
| 2025-06 | 1 035 | **31 124 518** ← peak Q3 |
| 2025-05 | 942 | 25 909 973 |

**Wzór:** Q3-Q4 (czerwiec, październik) ~30M, reszta ~20-25M. **Spadek 2026 (~21M/m-c od stycznia)**.
**FVR (rabaty):** zawsze ujemne ~10-17M / m-c → **rabat = 50-70% przed rabatem!?** To może być rozliczenie zaliczek (FVZ) lub kontraktów rolniczych (gdzie bonusy = -prc).

**FKS+FKSB+FWK (korekty):** ~80-100 dokumentów / m-c. Łącznie -200k zł / m-c. **3-5% obrotu = korekta.**

---

## 📋 REKLAMACJE — STAN AKTUALNY

**Statusy:**
- Nowa + ZGLOSZONA: **517** (główny bufor)
- Nowa + POWIAZANA: 57
- Zaakceptowana + ZASADNA: **31**
- Przyjęta + W_ANALIZIE: 9
- Odrzucona + ODRZUCONA: 7
- Razem: ~628

**Priorytet:**
- Normalny: 624 (z czego DecyzjaJakosci=NULL: 602, Zasadna: 18, Niezasadna: 4)
- Wysoki: 4 (z czego Niezasadna: 1)

→ **Decyzja Jakości jest wypełniona w 22 / 628 = 3.5% przypadków**. Reszta to czarna skrzynka.

**Top klienci-reklamacjonariusze:**

| Klient | Rekl. | Ręczne | Suma kg | Suma zł |
|--------|-------|--------|---------|---------|
| **BOMAFAR** (1436) | 154 | 23 | **506 550** | 155 779 |
| SMOLIŃSKI (4779) | 47 | 16 | 25 185 | 50 168 |
| Trzepałka Mariusz (931) | 45 | 7 | 287 695 | 46 157 |
| Centrum Drobiu Anna Piórkowska (4856) | 26 | 1 | 6 181 | 18 501 |
| MAX-MIĘS (5431) | 25 | 1 | 373 340 | 86 583 |
| TWÓJ MARKET (5049) | 36 | 0 | 107 524 | 23 320 |
| BATISTA (4837) | 14 | 0 | 168 417 | 13 370 |

**BOMAFAR to numer 1.** 154 reklamacje, z tego 23 ręczne (15%) — reszta to auto-importy korekt FKS/FKSB/FWK.

**Top produkty reklamowane:**
1. Kurczak A — 141 reklamacji / 365 014 kg / 2 659 627 zł (głównie korekty FVS)
2. Wątroba — 59 reklamacji / 7 162 kg / 23 186 zł
3. Filet A — 44 / 38 603 kg / 653 211 zł
4. Żołądki — 42 / 1 887 kg / 8 542 zł
5. Serce — 41 / 1 847 kg / 11 927 zł
6. Ćwiartka — 41 / 37 116 kg / 219 314 zł

→ **Reklamacje to 65% Kurczak A** — bo to 80% obrotu, więc proporcjonalne.

---

## 🐔 HODOWCY — KLASY WAGOWE

`132_A_klasy_per_hodowca_full` daje pełen ranking. **Top "idealni" (100% klasy 6+7):**

1. **Adamska Agnieszka** (625) — 5 partii, 63 palety, 100% klasa 6/7
2. **Bernadt Justyna** (987) — 7 partii, 84 palety, 100%
3. **Korpas Tomasz** (323) — 7/92, 100%
4. **Milczarek-Grzelak Paulina** (791) — 7/103, 100%
5. **Korpas Piotr** (687) — 7/82, 100%
6. **Stępa Przemysław** (530) — 15/204, 98.5% (2 odpadły do klasy 5)
7. **Prochoń Małgorzata** (302) — 5/57, 98.2%
8. **Kubiak Jacek** (067) — 4/54, 98.1%

→ **Mistrzowie idealnej kalibracji** — 5-7 partii i ZERO odpadu w klasach niewłaściwych.

**Najgorszy widoczny w Top-30:** Głowa Rafał Bios — 80.5% (7 par. odpadło do klasy 5)

→ Ranking hodowców = gotowy moduł. Kolumna `proc_idealna` dostępna w jednym SELECT.

---

## 🔁 FK_ZAPISY (FK.zapisy = księga główna Symfonii)

**1.16M wierszy.** Każdy wpis = jeden zapis księgowy (debet/kredyt).
- pozycja = numer pozycji w dokumencie
- typopisu (1=lista płac etc.)
- typkursu (waluta), kursEuro, przeksKurs, przeksData (przeksięgowania)
- guid (uniqueidentifier — PK alternatywny)
- dataKPKW (data kasy)
- accountId (konto rachunku bankowego)
- splitPayment (MPP)

**Sample:** "Naliczenie listy płac I — Komornik-Narkus", "Komornik-Jaczymczak", "Komornik-Sikorski" — z 31 stycznia 2026, kwoty 373.67, 1079.7, 612.63 zł. **Operator komorniczy** dla 3 osób (Narkus, Jachymczak, Sikorski). Justyna Chrostowska istnieje w Reklamacjach — czy Jaczymczak to ta sama osoba? Może Teresa **Jachymczak** to też **Jaczymczak**? Zatrzymaj uwagę — listy płac mają zajęcia komornicze.

---

## 📦 HM.DK — TYPY DOKUMENTÓW (od 2022 do dziś)

| Typ | Seria | Liczba | Pełen okres |
|-----|-------|--------|-------------|
| **FVS** | sFVS | **41 711** | 2022-01..2027-02 (ktoś ma faktury z przyszłości!) |
| FW | sFW | 17 137 | faktury wewnętrzne |
| **PAR** | sPAR | 16 475 | paragony |
| FVZ | sFVZ | 11 751 | zaliczki |
| fzk | sfzk | 11 283 | (małymi literami — co to?) |
| FPP | sFPP | 5 237 | proforma? |
| FWK | sFWK | 4 160 | korekta wewnętrzna |
| **FKSB** | sFKSB | **3 350** | korekta sprzedaży B |
| **FKS** | sFKS | **3 149** | korekta sprzedaży |
| FVR | sFVR | 3 131 | faktura rabatowa |
| RUZ | sRUZ | 724 | rozliczenie u(?) |
| WDT | sWDT | 102 | wewnątrzwspólnotowa dostawa |
| FVW | sFVW | 100 | faktura wewnętrzna |
| FPP1 | sFPP1 | 71 | proforma 1 |

→ **2027-02-27 mam już FVS** — pewnie ktoś wprowadził fakturę zaliczkową na rok do przodu.

→ **fzk małymi literami** = anomalia w typie (powinno być sFZK?). Nie wiadomo co to.

---

## 🚛 TRANSPORT — GODZINY ZGŁOSZEŃ

`122_A` histogram TransportZmian per godzina (30 dni):

| Godzina | NoweZam | ZmianaIlosci | ZmianaUwag | Anulowanie | Status |
|---------|---------|--------------|------------|------------|--------|
| 5:00 | 3 | 2 | 2 | 0 | poranek czysty |
| 6:00 | 49 | 22 | 13 | 5 | start |
| 7:00 | **68** | 38 | 34 | 13 | rozpędza się |
| **8:00** | **161** | **60** | **67** | **10** | **PEAK** |
| 9:00 | 69 | 17 | 18 | 1 | po peak |
| 10:00 | 86 | 17 | 31 | 1 | drugi peak |
| 11:00 | 83 | 21 | 25 | 2 | spada |
| 12:00 | 59 | 23 | 14 | 2 | obiad |
| 13:00 | 26 | 13 | 9 | 1 | spada |
| 14:00 | 10 | 8 | 2 | 0 | końcówka |
| 15:00 | 6 | 2 | 1 | 0 | dosłownie końcówka |

**WNIOSEK:** **8:00 to ABSOLUTNY peak nowych zamówień transportowych** (161 / godzinę). Druga fala 10:00-11:00. Po 14:00 prawie cisza.

---

## 🏭 WAGOCOUNTER — RYTM PRODUKCJI

**Histogram per godzina (3:00-15:00):**
| Godzina | Aut | Suma szt | Średnia min/auto | Średnia szt/auto |
|---------|-----|----------|------------------|------------------|
| 3:00 | 31 | 124 090 | 40.5 | 4 002 |
| 4:00 | 21 | 92 423 | 39 | 4 401 |
| 5:00 | 28 | 116 481 | 36.7 | 4 160 |
| 6:00 | 30 | 129 724 | 38.4 | 4 324 |
| 7:00 | 28 | 114 826 | 42.9 | 4 100 |
| 8:00 | 24 | 99 642 | 40.6 | 4 151 |
| 9:00 | 29 | 108 353 | 35.7 | 3 736 |
| 10:00 | 24 | 95 373 | 42.6 | 3 973 |
| 11:00 | 20 | 72 616 | 39 | 3 630 |
| 12:00 | 19 | 74 961 | 37.4 | 3 945 |
| 13:00 | 8 | 28 887 | 34.9 | 3 610 |
| 14:00 | 1 | 4 400 | 38 | 4 400 |
| 15:00 | 1 | 4 340 | 39 | 4 340 |

**WNIOSEK:** **3:00-9:00 to godziny szczytu uboju** — 31, 21, 28, 30, 28, 24, 29 aut/godzinę.
Po 13:00 — pojedyncze auta (8, 1, 1).
**Średnio 4 000 sztuk / auto.** Czas przejazdu na rampie 35-43 min.

**Per dzień tygodnia:**
| Dzień | Dni | Suma | Średnia/dzień |
|-------|-----|------|---------------|
| Poniedziałek | 12 | 744 582 | **62 048** |
| Wtorek | 12 | 664 411 | 55 367 |
| Środa | 12 | 750 644 | **62 553** |
| Czwartek | 11 | 611 834 | 55 621 |
| Piątek | 10 | 485 911 | **48 591** ← najmniej |

**WNIOSEK:** Pn+Śr = peak (62k/dzień). Pt = 78% peaku (48k). **Sobota+Niedziela = 0** (brak danych w okresie).

---

## 📞 CALLREMINDER — KTO MA WŁĄCZONE ALERTY

`119_A_callreminderconfig` — 55 wpisów, ale tylko **8 z `IsEnabled=1`**:

| ID | UserID | User | DailyTarget | TerritoryWoj |
|----|--------|------|-------------|--------------|
| 1 | 11111 | Admin | 3 | łódzkie, małopolskie, mazowieckie |
| 3 | 6521 | Maja | 10 | łódz/małop/mazow |
| 4 | 2121 | Teresa | 3 | łódz/małop/mazow |
| 11 | 23231 | (?) | 20 | **15 województw** (cała Polska) |
| 12 | 432143 | Anna | 8 | 15 województw |
| 19 | 9995 | (?) | 8 | 15 województw |
| 54 | 6611 | Justyna | 30 | NULL (brak terytorium) |
| 55 | 1961 | (?) | 30 | NULL |

**Pozostałe 47 osób ma `IsEnabled=0`.** Czyli moduł CallReminder **prawie martwy** — tylko 8 osób aktywnie pracuje, w tym 2 zaczęły niedawno (Justyna 12.02, ?1961 21.02).

**PKD priorytety** — branże mięsne (10.11, 10.12, 10.13, 10.85, 46.32, 47.22) ustawione tylko dla 5 userów (1, 4, 19, 12, 3 = Adm, Teresa, ?, Anna, Maja).

---

## 💬 KOMUNIKACJA — KANAŁY

**SmsHistory:** Ostatnie wpisy 2025-11-28 → 2025-12-03 (potem cisza). Treść: "Piorkowscy z 27 na 28 listopada (Piątek), Załadunek godz.03:00..." — to były automatyczne SMS'y do **hodowcom** o przyjeździe odbioru. **System zatrzymał się 5 miesięcy temu.**

**ContactHistory:** Ostatni wpis **2025-08-10** ("Walasz Andrzej — obrażony o coś"). **Martwy 9 miesięcy.**

**Spotkania:** Tylko 6 wpisów (typ "Zespół"), 2026-01-15..2026-01-19. Potem nic. **Funkcja porzucona po stycznia.**

---

## 🗺 STORED PROCEDURES — TOP

`131_A_top_sp_wywoły`:
- `sp_UtworzPrzypomnienia` — **250 450 wywołań** w 30 dni → ~8 350/dzień → ~6/min. **Wywoływana przez timer w background.**
- `sp_PobierzNieprzeczytaneNotyfikacje` — **250 421** wywołań (parą).
- `AddContactHistory` — 110 wywołań.

**WNIOSEK:** ZPSP odpala timer co ~10 sek dla powiadomień. To może obciążać serwer — sprawdzić, czy nie da się rozprzedzić.

---

## 🔑 FK PEŁNA LISTA

`129_A_full_fk` — 60+ FK w bazie LibraNet. Najważniejsze:
- ReklamacjeHistoria/Towary/Partie/Zdjecia → Reklamacje (CASCADE)
- TransportTrip → Driver (NO_ACTION)
- TransportTripOrder → ZamowieniaMieso (NO_ACTION)
- DriverDetails → Driver, VehicleDetails → CarTrailer, DVA → Driver+CarTrailer (Twoje Floty)
- Pozyskiwanie_Aktywnosci → Pozyskiwanie_Hodowcy
- ZamowieniaMiesoSnapshot → ZamowieniaMieso (CASCADE)
- HR_Nieobecnosci → HR_TypyNieobecnosci, KG_Nieobecnosci → KG_TypyNieobecnosci

**TABELE BEZ FK** (130_A) — duże tabele:
- In0E (2.1M), Out1A (2M), Aktywnosc (185k), State0E (102k)
- listapartii (37k), PartiaDostawca (37k) — **mimo że to relacja, nie ma FK!**
- 'Dane hodowców$' (415) — **nadal istnieje, mimo że błąd 208 mówił że nie**. To Excel-import z $ w nazwie (ze sheetu).

---

## 🛒 KLIENCI — KTO KIEDY ZAMAWIA

`124_A_klient_dzien_tyg` (top 30):
- **939 (Damak/?)** — środa+czwartek+wtorek+piątek po 21+18+15+12 zamówień / 30 dni → **najczęstszy zamawiacz**
- **931 (Trzepałka Mariusz)** — wt 19, czw 17, śr 16, pt 14, pn 13 → **EVERY DAY**
- **317 (?)** — wt 21, czw 18 → wtorek-czwartek
- **4910 (?)** — pt 21 (głównie piątek), śr 14
- **5314 (?)** — wt 14, śr 14, czw 13 → 3 dni naprzeciąg
- **1436 (BOMAFAR)** — pn 12, wt 13, śr 13, czw 12 → 4 razy w tygodniu

→ **Wzorce dnia tygodnia są wyraźne.** Damak = środa-czwartek. BOMAFAR = wszystkie dni. Trzepałka = każdy dzień.

---

## 📊 FARMERCALC — TREND MIESIĘCZNY

`121_A_farmercalc_trend`:
| Miesiąc | Dostaw | Sztuk | Kg | śr_kg/szt | Loss% | śr_cena |
|---------|--------|-------|-----|-----------|-------|---------|
| 2026-05 | 30 | NULL | 194 780 | NULL | 0.03% | 2.534 |
| 2026-04 | 262 | 1 090 883 | 3 171 120 | **2.917** | 0.005% | 5.049 |
| 2026-03 | 268 | 1 131 777 | 3 207 700 | 2.815 | 0.004% | 5.550 |
| 2026-02 | 269 | 1 121 045 | 3 258 170 | 2.908 | 0.005% | 5.038 |
| 2026-01 | 278 | 1 095 090 | 3 109 241 | 2.818 | **0.011%** | 4.711 |

**WNIOSEK:**
- ~270 dostaw/m-c (~9/dzień)
- ~1.1 mln sztuk = ~36 000/dzień (zgadza się z WagoCounter ~62k przy peak Pn+Śr)
- Średnia waga **2.82-2.92 kg/szt**
- Loss = 0.005% (super!)
- **Cena 2026-03 (5.55 zł/kg żywiec) → 2026-04 (5.05 zł/kg) — spadek 9%**
- **2026-01 cena 4.71 zł/kg, 2026-04 = 5.05 zł/kg — wzrost 7%** (sezonowość)

---

## 📦 SAMPLE In0E ostatnie 24h

`135_A_in0e_last24h`: ostatnie ważenia 2026-05-04 20:51-20:27, wszystkie:
- Operator: **0101** (kto to? — sprawdzić w operators)
- Wagowy: **NEPAL** (nazwa terminala/hali — Nepal? to chyba miejsce produkcji)
- TermID: 101 albo 104
- TermType: K2 (klasy 2 — czyli klasyfikator?)
- Partia: 26124001..26124015 — format **8 znaków RR-DDD-XXX** (rok 26, dzień 124, auto 001-015)
- Produkty: Filet I Mrożony (15 kg/szt), Filet II z Piersi Świeży (15 kg), Filet z Piersi Świeży (15 kg), Polędwiczki (15 kg), Filet I Mrożony (15 kg)
- Każdy ważenie = 15.0-15.2 kg → to **kartony 15 kg**!

**WNIOSEK:** Operator 0101 robi 15 kg kartony fileta przez ostatnie 30 minut na terminalu NEPAL.

---

## 🧯 BŁĘDY SQL (do poprawy w SELECT)

W RUNDA 5 wystąpiły błędy:
1. `Driver.ID` — kolumna **nie istnieje** w Driver (jest `GID`?)
2. `Pozyskiwanie_Hodowcy.CustomerID` — nie ma. Trzeba: po nazwie/kodzie.
3. `Dostawcy.DostawcaID` — nie ma. Pewnie po `ID` lub `Kod`.
4. `Notatki.UtworzylID` — nie ma kolumny.
5. `OdbiorcyCRM.IDSymfonii` — nie ma. Ale są: NIP, REGON, ImportID.
6. `kontrahenci.IsClient/IsSupplier/Inactive/Name1` — nie ma (struktura inna niż założyłem).
7. `SmsHistory.DataWyslania` — kolumna nazywa się `SentDate`.
8. `PartiaStatus` / `PartiaAuditLog` — istnieją, ale **wszystkie wiersze są puste** (tabele nie używane).
9. `134_HM.MZ` cross-DB — Linked Server nie istnieje (192.168.0.112 nie dodany do sys.servers).

---

## 🎯 WNIOSKI STRATEGICZNE

### Co działa:
1. **In0E + Out1A** — rdzeń produkcji (4M wierszy łącznie), nadal zapisuje się dziś o 20:51.
2. **WagoCounter** — co dzień nowy wpis (32 miesiące ciągłe).
3. **HM.DP/HM.MZ** Symfonia — Kurczak A 5.6M kg / 43.9M zł, codzienna sprzedaż.
4. **Reklamacje** — 1418 wpisów + auto-import korekt FKS (ostatnia: dziś 2026-05-04).
5. **TransportZmiany** — pełen workflow zatwierdzania, Anna Jedynak top zgłaszacz.
6. **HarmonogramDostaw** — 4 391 wpisów, ale 47% Anulowane.

### Co martwe:
1. **SmsHistory** — od grudnia 2025 cisza (5 m-cy).
2. **ContactHistory** — od sierpnia 2025 cisza (9 m-cy).
3. **Spotkania** — tylko 6 wpisów ze stycznia.
4. **CallReminderConfig** — 47/55 osób IsEnabled=0.
5. **PartiaStatus + PartiaAuditLog** — pusty stół (V2 lifecycle nie używany).
6. **listapartii.HarmonogramLp** — 100% NULL (link nigdy nie wypełniony).
7. **DokumentyWZ** — 1 wpis testowy (Avilog 2026-01-10).
8. **In0E.CustomerID** — 100% NULL w okresie 90 dni.
9. **In0E.Operator2ID + Wagowy2** — 100% NULL pełna historia.
10. **Aktywnosc** — ostatni wpis TypLicznika 7 = **2026-01-20** (3.5 m-cy temu) — moduł padł.

### Co wątpliwe:
1. **FarmerCalc.ŚrKgSztuke 2.92 kg** vs **In0E 15 kg** — to różne JM. FarmerCalc liczy **żywiec na sztukę**, In0E liczy **karton 15 kg**.
2. **Akumulator do agregatu** 2.3M zł — pewnie błąd lub poważny zakup hardware.
3. **HM.DK FVS** ma fakturę z 2027-02-27 — albo zaliczka długoterminowa, albo błąd daty.

---

## 🚀 CO MOŻNA Z TYM ZROBIĆ — REKOMENDACJE

### Priorytet 1 — szybkie wygrane (1-3 dni)
1. **Backfill `listapartii.HarmonogramLp`** — UPDATE po `(CustomerID, DataUboju)`. Zyskasz rzeczywisty link partia↔harmonogram.
2. **Dashboard Anulowanych Harmonogramów** — co tydzień RAPORT: ile dostaw anulowano, kto anulował (Bufor='Anulowany'). 47% to za dużo.
3. **Cleanup Operator2ID/Wagowy2** w In0E/Out1A — DROP COLUMN (zaoszczędzisz 4M × 2 NULL).

### Priorytet 2 — średnie (1-2 tygodnie)
4. **Reaktywacja SmsHistory** — system wysyłał automatyczne SMS do hodowców o przyjeździe odbioru. Działał do grudnia 2025. **Co się stało? Kto to wyłączył?**
5. **Reklamacje DecyzjaJakosci 22/628 = 3.5%** — uzupełnić, bo bez Decyzji nie ma analityki Zasadne/Niezasadne. Klaudia + Justyna mogą to robić.
6. **CallReminder reaktywacja** — 47 osób ma IsEnabled=0. Czy w ogóle warto trzymać moduł, czy go wyłączyć całkowicie?
7. **`In0E.CustomerID` reaktywacja** — operator hali musi wybierać klienta przy ważeniu, żeby było wiadomo dokąd produkt idzie.

### Priorytet 3 — duże (>2 tygodnie)
8. **Ranking Hodowców** — `132_A_klasy_per_hodowca_full` daje gotowe dane. Top 10 idealni 100%, top 10 najgorsi. Powiązać z FarmerCalc.AvgWeight + In0E loss%.
9. **HALA LIVE** — In0E ostatnie 24h pokazuje, że dane lecą każda sekunda. Można zrobić auto-refresh co 30 sek.
10. **Marża top-down** w Dashboardzie Sprzedaży — masz HM.DP (kg, zł, ProductID) + HM.MZ (cena netto). Możesz liczyć marżę per produkt per miesiąc.

---

## 📂 STAN BAZY WIEDZY — RUNDA 5 KOMPLETNA

Wszystkie 7 plików SELECT-ów wykonanych:
1. ✅ EKSPLORACJA_LIBRANET_FULL → WYNIKI_RAW (Runda 1)
2. ✅ EKSPLORACJA_LIBRANET_2 → WYNIKI_RAW_2 (Runda 2)
3. ✅ EKSPLORACJA_LIBRANET_3 → WYNIKI_LIBRANET_3 (Runda 3)
4. ✅ EKSPLORACJA_HANDEL_FULL → WYNIKI_HANDEL (Runda 3)
5. ✅ EKSPLORACJA_TRANSPORTPL_FULL → WYNIKI_TRANSPORTPL (Runda 3)
6. ✅ EKSPLORACJA_UNISYSTEM_FULL → WYNIKI_UNISYSTEM (Runda 3)
7. ✅ EKSPLORACJA_ZALEZNOSCI → WYNIKI_ZALEZNOSCI (Runda 4)
8. ✅ **EKSPLORACJA_LIBRANET_5 → WYNIKI_LIBRANET_5 (Runda 5)** ← TERAZ
9. ✅ **EKSPLORACJA_HANDEL_2 → WYNIKI_HANDEL_2 (Runda 5)** ← TERAZ
10. ✅ **EKSPLORACJA_CROSS_DB → WYNIKI_CROSS_DB (Runda 5)** ← TERAZ

**Łącznie 10 SELECT-ów + 5 plików syntezy** (RUNDA1, RUNDA2, FINALNA, RUNDA4, **RUNDA5 ← ten plik**).

Mam pełny obraz — możemy zaczynać konkretną robotę programistyczną.

---

## ❓ DECYZJA — CO TERAZ?

Ostatecznie 3 pending tasks:
- **PRIORYTET 1**: Marża top-down w Dashboardzie Sprzedaży (mam HM.DP + HM.MZ, idzie się policzyć)
- **PRIORYTET 2**: Alert „Niesprzedane na piątek" w Dashboardzie Sprzedaży

A dodatkowo z RUNDA5 wyrosły propozycje:
- **A)** Dashboard Anulowanych Harmonogramów (47% to za dużo!)
- **B)** Backfill `listapartii.HarmonogramLp` (1 query UPDATE)
- **C)** Ranking Hodowców (`klasy_per_hodowca_full` + scoring)
- **D)** HALA LIVE (auto-refresh In0E co 30 sek)
- **E)** Reaktywacja SmsHistory (znaleźć dlaczego padł)
- **F)** Reklamacje V2 — uzupełnianie DecyzjiJakosci (Klaudia/Justyna jako użytkowniczki)

**Wybierz: A / B / C / D / E / F / Marża / Alert Pt** lub **kombinację** (np. „B+C jednocześnie", „D + Alert Pt").
