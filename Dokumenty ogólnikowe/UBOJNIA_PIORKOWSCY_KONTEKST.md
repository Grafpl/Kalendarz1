# UBOJNIA DROBIU PIÓRKOWSCY — PEŁNY KONTEKST FIRMY DLA CLAUDE CODE

> **Cel tego dokumentu:** Dać Claude Code (oraz każdej innej AI / nowemu współpracownikowi) pełny obraz firmy, jej historii, struktury, ludzi, procesów, technologii i wyzwań. Dokument napisany jest tak, by po jego przeczytaniu można było natychmiast pracować nad dowolnym zadaniem — kodem ZPSP, dokumentem dla banku, procedurą operacyjną, analizą sprzedaży, cokolwiek.
>
> **Autor:** Sergiusz Piórkowski (Ser) — operacyjny manager firmy od 12+ lat, twórca systemu ZPSP, de facto CEO/CTO.
> **Stan na:** maj 2026.
> **Format:** Markdown (czytelny dla AI, łatwy do przeszukiwania).
>
> **Changelog:**
> - **v2 (02.05.2026)** — poprawki Sera: (1) IFS dodany obok BRC v9 w pakiecie BioEfekt, (2) **klasy wagowe to liczba sztuk w pojemniku 15 kg, NIE waga w kg** (poprawka 8.4), (3) dodany moduł 14A „Krojenie" z analizą screena ZPSP, (4) dodany moduł 14B „Specyfikacja Surowca" z kolumnami Padłe/CH/ZM/NW, (5) **Radek odszedł** (handlowcy: Jola/Maja/Teresa/Ania), (6) JBB Bałdyga to mały klient typu „Łyse" (jedyny w segmencie), (7) **brak skanowania na wydaniu** — tylko panel magazyniera, (8) **spread 2,50 zł/kg jest nieaktualny** — temat pełnej kalkulacji rentowności otwarty.
> - **v1 (02.05.2026)** — pierwsza wersja dokumentu.

---

## SPIS TREŚCI

1. [Streszczenie wykonawcze (TL;DR)](#1-streszczenie-wykonawcze-tldr)
2. [Podstawowe dane firmy](#2-podstawowe-dane-firmy)
3. [Historia firmy (1996 → dziś)](#3-historia-firmy-1996--dziś)
4. [Struktura prawna i właścicielska — sytuacja spadkowa](#4-struktura-prawna-i-właścicielska--sytuacja-spadkowa)
5. [Przekształcenie w sp. z o.o. — projekt strategiczny](#5-przekształcenie-w-sp-z-oo--projekt-strategiczny)
6. [Wyniki finansowe i kondycja firmy](#6-wyniki-finansowe-i-kondycja-firmy)
7. [Struktura organizacyjna — kto kto jest](#7-struktura-organizacyjna--kto-kto-jest)
8. [Produkcja — ubój, rozbiór, klasyfikacja](#8-produkcja--ubój-rozbiór-klasyfikacja)
9. [Sprzedaż i klienci](#9-sprzedaż-i-klienci)
10. [Zakup żywca i hodowcy](#10-zakup-żywca-i-hodowcy)
11. [Transport i logistyka](#11-transport-i-logistyka)
12. [Magazyn, mroźnia, FIFO](#12-magazyn-mroźnia-fifo)
13. [Jakość, BHP, certyfikacja](#13-jakość-bhp-certyfikacja)
14. [System ZPSP — szczegółowy opis techniczny](#14-system-zpsp--szczegółowy-opis-techniczny)
14A. [Moduł ZPSP „Krojenie" — kalkulator decyzji dziennej](#14a-moduł-zpsp-krojenie--kalkulator-decyzji-dziennej)
14B. [Moduł ZPSP „Specyfikacja Surowca" — ewidencja uboju](#14b-moduł-zpsp-specyfikacja-surowca--ewidencja-uboju)
15. [Infrastruktura IT i integracje](#15-infrastruktura-it-i-integracje)
16. [Projekty inwestycyjne 2026](#16-projekty-inwestycyjne-2026)
17. [Banki, kredyty, finansowanie](#17-banki-kredyty-finansowanie)
18. [Dotacja ARiMR — wrzesień 2026](#18-dotacja-arimr--wrzesień-2026)
19. [Kontakty zewnętrzne (prawnicy, doradcy, dostawcy)](#19-kontakty-zewnętrzne)
20. [Karma-Max — firma powiązana](#20-karma-max--firma-powiązana)
21. [Konkurencja i otoczenie rynkowe](#21-konkurencja-i-otoczenie-rynkowe)
22. [Compliance i ryzyka regulacyjne](#22-compliance-i-ryzyka-regulacyjne)
23. [Profil Sergiusza i styl pracy](#23-profil-sergiusza-i-styl-pracy)
24. [Słownik branżowy i pojęcia](#24-słownik-branżowy-i-pojęcia)
25. [Otwarte problemy i top-of-mind](#25-otwarte-problemy-i-top-of-mind)
26. [Załącznik A — kluczowe daty](#załącznik-a--kluczowe-daty)
27. [Załącznik B — typowe zadania na tej firmie](#załącznik-b--typowe-zadania-na-tej-firmie)

---

## 1. STRESZCZENIE WYKONAWCZE (TL;DR)

**Ubojnia Drobiu „PIÓRKOWSCY" Jerzy Piórkowski w spadku** to rodzinna ubojnia drobiu w Koziołkach koło Brzezin (woj. łódzkie), założona w 1996 roku przez Jerzego Piórkowskiego, dziadka obecnego operacyjnego managera Sergiusza. Firma:

- przetwarza **~70 000 sztuk drobiu dziennie (~200 ton)**,
- zatrudnia **~173 osób** (123 etatowych + ~50 z agencji),
- ma **roczne obroty ~318 mln PLN** (2025), z czego zysk netto ~7 mln PLN, EBITDA ~8,8 mln PLN,
- ma **praktycznie zerowe zadłużenie bankowe** (Net Debt/EBITDA = 0,36),
- obsługuje **~400 klientów** i współpracuje ze **~140 hodowcami**,
- działa pod **NIP 726-162-54-06**, **PKD 10.12.Z** (przetwarzanie drobiu).

**Stan prawny jest niestandardowy:** firma działa jako **JDG w spadku** po Jerzym (zm. 02.08.2023). Zarządcą sukcesyjnym jest **Marcin Piórkowski** (wujek Sergiusza, ~77,27% udziałów spadkowych, formalnie podpisuje wszystkie dokumenty). Sergiusz ma 8,3% spadku (formalnie) ale **operacyjnie prowadzi firmę od 12 lat** i zbudował od podstaw cały system ERP („ZPSP"). Mama Sergiusza (Anna) i brat (Kamil) mają po 8,3%.

**Twardy deadline 1 sierpnia 2026:** JDG w spadku formalnie wygasa po dwóch latach + przedłużeniach, więc cała firma musi do tego dnia zostać przekształcona w sp. z o.o., albo przestaje istnieć jako podmiot prawny. To jest **najwyższy priorytet 2026 roku** i nadrzędny względem wszystkich innych projektów.

**System ZPSP** (Zajebisty Program Sergiusza Piórkowskiego) to autorskie ERP-owe oprogramowanie napisane od zera w **C# / .NET / WPF z DevExpress** na bazie **SQL Server 2022**. Ma 277 tabel, 4,5 mln rekordów, 30+ modułów (sprzedaż, zakupy, produkcja, magazyn, transport, fakturowanie, KSeF, IRZplus, WebFleet, CRM, dashboard CEO, kamery Hikvision RTSP). System integruje się z **Sage Symfonia Handel + FK** przez linked server. Sergiusz jest **jedynym programistą i jedynym posiadaczem wiedzy** o tym systemie. Wartość rynkowa ZPSP szacowana na 1–3 mln PLN.

**Kluczowe inwestycje 2026:**
- Modernizacja chłodnictwa z freonu R507A na glikol R1234ze (firma Magik, ~4,7 mln PLN w dwóch etapach; etap 1 ~2,8 mln pożyczka leasingowa, etap 2 pod dotację),
- **Patroszarka Meyn Maestro** (~5 mln PLN, wrzesień 2026 pod dotację),
- **Dotacja ARiMR PS WPR I.10.7.1** — ostatni nabór wrzesień 2026, do 10 mln PLN dotacji (50% z 20 mln inwestycji); następny dopiero 2029,
- **Certyfikacja BRC v9** (BioEfekt Global / Wojciech Rybka, ~133 tys. PLN łącznie),
- **Migracja na Microsoft 365 + Teams** (odzyskanie kontroli nad domeną piorkowscy.com.pl od zewnętrznego kontrahenta Webemo).

**Kluczowi partnerzy:**
- **Mec. Przemysław Urbaniak** (TaxLawPro Warszawa) — prawnik prowadzący przekształcenie sp. z o.o.,
- **Wiesław Oślewski** — konsultant dotacyjny ARiMR (prowizja 2,8% od dotacji),
- **Banki:** Pekao (obrotówka „Żubr"), Millennium (Konrad Gruszewski / Adam Kolasik), BNP Paribas (Andrzej Kruszyński), Pekao Leasing (Andrzej Chrustowicz), Santander Leasing, PKO Leasing,
- **Magik sp. z o.o.** (Mariusz Domagała, Piotr Domagała, Maciej Józefowicz) — chłodnictwo,
- **BioEfekt Global** (Wojciech Rybka) — projekt technologiczny + BRC.

**Profil Sergiusza:** ESTP, ADHD (Medikinet CR 30), bezpośredni, dyktuje voice-to-text, używa równolegle Claude / ChatGPT / Gemini do analiz biznesowych, gra na pianinie cyfrowym Kawai ES920, ma partnerkę i małe dziecko, wraca do treningów po wieloletniej przerwie. Komunikuje się po polsku, lubi konkrety, plain language, ponumerowane listy, gotowe zdania do użycia. Pracuje 12–16 godzin dziennie.

---

## 2. PODSTAWOWE DANE FIRMY

### 2.1 Dane rejestrowe

| Pole | Wartość |
|---|---|
| Pełna nazwa | Ubojnia Drobiu „PIÓRKOWSCY" Jerzy Piórkowski w spadku |
| Nazwa skrócona | Ubojnia Drobiu Piórkowscy |
| NIP | **726-162-54-06** |
| PKD główne | **10.12.Z** (Przetwarzanie i konserwowanie mięsa z drobiu) |
| Forma prawna | JDG w spadku (jednoosobowa działalność gospodarcza w spadku) |
| Zarządca sukcesyjny | Marcin Piórkowski |
| Adres | Koziołki 40, 95-061 Koziołki, gm. Brzeziny |
| Lokalizacja | woj. łódzkie, ok. 30 km na NE od Łodzi |
| Adres rejestrowy historyczny | 95-061 Dmosin (przed przeniesieniem do Koziołek) |
| Strona www | https://piorkowscy.com.pl/ (kontrolowana przez zewnętrznego kontrahenta) |
| Telefon kontaktowy | 506 262 541 |

### 2.2 Skala działalności

| Wskaźnik | Wartość |
|---|---|
| Ubój dzienny | ~70 000 sztuk drobiu / ~200 ton |
| Ubój roczny (skala) | ~17 mln sztuk / ~50 tys. ton |
| Pracownicy etatowi | ~123 |
| Pracownicy z agencji | ~50 (GURAVO, IMPULS, STAR, ECO-MEN, ROB-JOB, m.in. pracownicy z Nepalu) |
| Łącznie zatrudnienie | ~173 |
| Klienci aktywni | ~400 |
| Hodowcy w bazie | 140+ (40–70 aktywnych w danym miesiącu) |
| Obroty 2025 | ~318 mln PLN (+23% r/r vs 258 mln PLN w 2024) |
| Zysk netto 2025 | ~7,04 mln PLN (vs 3,02 mln PLN w 2024 — wzrost x2,3) |
| EBITDA 2025 | ~8,8 mln PLN |
| Marża netto | ~2,29% (typowa dla branży drobiarskiej) |
| Net Debt / EBITDA | 0,36 (doskonały wynik) |
| Płynność bieżąca | 0,86 (poniżej 1,0 — typowe dla drobiarstwa, gdzie hodowcom płaci się szybciej niż klienci płacą firmie) |

### 2.3 Lokalizacje fizyczne

- **Główny zakład Koziołki** — ubój, rozbiór, magazyny, mroźnia, biura. Adres operacyjny.
- **Lokalizacja Zgierz** — masarnia rodziny (Marcin), formalnie odrębna, ale Justyna (Plant Director ubojni) historycznie pomagała z HACCP w Zgierzu.
- **Działki w spadku:** Kobylin, Stefanów, Koziołki (w tym kluczowe 83/2, 84, 85 BEZ KW), Kołacin, Zarzęcin, Obra Dolna i inne. Część rolne (wymagają procedury KOWR).

### 2.4 Pozwolenia i numery weterynaryjne

- Pozwolenie zintegrowane środowiskowe — **aktualne, ale na 508 m³ wody/dzień podczas gdy realne zużycie ~800 m³/dzień** → ryzyko compliance, do odnowienia.
- Pozwolenie weterynaryjne — TAK (jest, kategoria zatwierdzonego zakładu uboju drobiu w systemie GIW).
- HACCP — wdrożone (Justyna prowadzi).
- BRC v9 — **W TRAKCIE WDROŻENIA** (BioEfekt Global, planowany certyfikat koniec 2027).
- IFS — **TEŻ W TRAKCIE** w ramach pakietu z BioEfekt (równolegle z BRC, projekt obejmuje obie ścieżki certyfikacji).
- IRZplus / ARiMR — codzienne raportowanie ZURD (Zgłoszenie Uboju w Rzeźni Drobiu), zintegrowane z ZPSP.

### 2.5 Branżowe pozycjonowanie

W województwie łódzkim działa ok. 20–24 zatwierdzonych zakładów uboju drobiu. Piórkowscy plasują się **w górnej części segmentu średnich zakładów**. Po upadłości EXDROB-u w Kutnie (74-letni lider regionalny, 2022 r.) firma umocniła pozycję regionalnego lidera. Nie konkuruje skalą z gigantami (Cedrob, SuperDrob, Drosed), ale wygrywa świeżością, elastycznością i lokalnymi relacjami. Rynek mocno presjonowany przez import filetu z Brazylii (~13 PLN/kg vs Piórkowscy ~15–17 PLN/kg).

---

## 3. HISTORIA FIRMY (1996 → DZIŚ)

### 3.1 Założenie i okres dziadka (1996–2023)

- **14 października 1996** — Jerzy Piórkowski zakłada Ubojnię Drobiu w Woli Cyrusowej (później przeniesioną do Koziołek). Zaczyna jako mała ubojnia z kilkoma tysiącami sztuk dziennie.
- **Lata 2000–2010** — sukcesywna rozbudowa: nowa hala uboju, własne mroźnie, własna chłodnia, instalacja flotatora do oczyszczania ścieków (kluczowa — pozwoliła zwiększyć moce do 100 tys. sztuk).
- **Lata 2010–2020** — Sergiusz dołącza do firmy w wieku 18 lat (a wcześniej, od dzieciństwa, hoduje brojlery). Pracuje kolejno w każdym dziale firmy z polecenia dziadka — magazyn, produkcja, sprzedaż, zakupy, transport. To trwa łącznie ~14 lat — buduje dogłębną wiedzę operacyjną.
- **2018–2023** — Sergiusz przejmuje zarządzanie operacyjne. Zaczyna budować ZPSP (od 2020 — pisze go po godzinach pracy, na własnym sprzęcie, w czasie wolnym). Firma rośnie z ~200 mln do 318 mln obrotu pod jego kierownictwem.
- **23 lutego 2018** — śmierć Teresy Piórkowskiej (babcia). Spadek ustawowy 1/3 dla każdego z trzech: Jerzy, Andrzej (ojciec Sergiusza), Marcin.
- **13 grudnia 2021** — śmierć Andrzeja Piórkowskiego (ojciec Sergiusza). Sergiusz zrzeka się spadku po ojcu na rzecz mamy (Anny) i brata (Kamila).
- **22 października 2021** — ostatni testament dziadka: Marcin 5/6 (83,3%), Andrzej 1/12, Sergiusz 1/12. (Wcześniejsze testamenty z 2020 i lutego 2021 dawały 5/6 Sergiuszowi — testament się odwrócił po kłótni Sergiusza z dziadkiem; pogodzili się, ale dziadek nie zmienił testamentu.)
- **2 sierpnia 2023** — śmierć Jerzego Piórkowskiego. Powstaje JDG w spadku.

### 3.2 Okres Sergiusza (od 2023)

- **2023–2024** — pierwszy rok JDG w spadku. Marcin formalnie zarządcą sukcesyjnym, Sergiusz operacyjnie prowadzi firmę.
- **2024** — przyspieszenie rozwoju ZPSP. Migracja kluczowych modułów z legacy Delphi (Raporty.exe, IT9000PC.exe, ProNova).
- **2024–2025** — uporządkowanie sprzedaży: zwolnienia 3 handlowców (w tym Daniela), reorganizacja działu z 6 do 2,5 etatu. Mimo redukcji obroty rosną z 258 mln (2024) do 318 mln (2025).
- **2 sierpnia 2025** — pierwsze przedłużenie zarządu sukcesyjnego.
- **2025** — audyt transportu (Locura), audyt sprzedaży, deep-dive w bazę Symfonii i ZPSP, integracja KSeF, integracja IRZplus.
- **Marzec 2026** — finalizacja umowy z Magikiem na chłodnictwo (etap 1 ~2,8 mln PLN brutto). Spotkania z bankami. Konflikt aport vs dzierżawa między Urbaniakiem a Wiesławem.
- **Kwiecień 2026** — kluczowy okres negocjacji z Marcinem o strukturę nowej spółki. Audyt kierowców (~36 tys. PLN nadpłat zidentyfikowane). Zwiększenie zespołu łapaczy do 56 osób. Rezygnacja z Ekomax na rzecz pożyczki leasingowej.
- **Maj 2026** — finalizacja umowy z Adamem Kolasikiem (Millennium Bank) na pożyczkę leasingową.
- **2 sierpnia 2026** — TWARDY DEADLINE: JDG w spadku wygasa, sp. z o.o. musi istnieć.
- **Wrzesień 2026** — złożenie wniosku ARiMR (do 10 mln PLN dotacji). OSTATNI nabór tej perspektywy UE.
- **2027** — realizacja inwestycji z dotacji + audyt certyfikujący BRC.
- **2029** — najbliższy nabór ARiMR po wrześniu 2026 (jeśli ten się nie uda).

### 3.3 Kluczowe momenty w historii ZPSP

- **2020** — pierwsza wersja ZPSP. Zaczyna jako prosta ewidencja zamówień.
- **2021–2022** — moduły sprzedaży, zakupu, magazynu.
- **2023** — moduł transportu, integracja z WebFleet GPS (TomTom Telematics).
- **2024** — moduł CRM dla handlowców, kreator ofert PDF, dashboard CEO TV (10 widoków rotujących), HandlowiecDashboard z analityką sprzedaży.
- **2025** — integracja z Sage Symfonia (linked server), KSeF, IRZplus (ZURD), AVILOG PDF parser, UNICARD time-tracking, Hikvision RTSP, system pojemnikowy z barcode GS1-128.
- **2026** — WEBFLEET.connect (live tracking, geofencing biosekurujący, auto-dispatch), OPC-UA na wagi, MarketIntelligence (Brave Search + GPT-4o).

---

## 4. STRUKTURA PRAWNA I WŁAŚCICIELSKA — SYTUACJA SPADKOWA

### 4.1 Drzewo spadkowe

```
Jerzy Piórkowski (dziadek) ────── Teresa Piórkowska (babcia)
         │                                  │
         │ wspólność majątkowa               │
         │                                  │
   ┌─────┴──────┐                  zm. 23.02.2018
   │            │
   │            │
Andrzej     Marcin            (dwie linie spadkowe)
(ojciec)    (wujek)
zm. 13.12.2021
   │
   ├── Sergiusz (Ser) — operator firmy, twórca ZPSP
   ├── Kamil — brat
   └── Anna — mama (żona zmarłego Andrzeja)
```

### 4.2 Aktualne udziały spadkowe (po dziadku, testament z 22.10.2021)

| Spadkobierca | Udział | Komentarz |
|---|---|---|
| Marcin Piórkowski | **5/6 = 83,3%** (formalnie 77,27% po niektórych obliczeniach związanych z odpadnięciem 1/12 Andrzeja) | Zarządca sukcesyjny. Prowadzi też Karma-Max. |
| Sergiusz Piórkowski | **1/12 = 8,3%** | Operator firmy. |
| Andrzej Piórkowski | 1/12 (ale zmarł PRZED dziadkiem → udział odpada lub przechodzi ustawowo na Annę / Sergiusza / Kamila) | Komplikacja prawna. |
| Anna + Kamil | częściowo | Po Andrzeju (po zrzeczeniu się Sergiusza). |

**Po babci Teresie (1/3 ustawowo):** Jerzy 1/3, Andrzej 1/3, Marcin 1/3. Po śmierci Andrzeja jego część przeszła na Annę, Sergiusza i Kamila.

**Komplikacje:**

- **Babcia zmarła PRZED dziadkiem** → wspólność majątkowa = osobna masa spadkowa po niej. Część przedsiębiorstwa formalnie przeszła na Jerzego dopiero po śmierci Teresy.
- **Andrzej zmarł 52 dni PO testamencie z 22.10.2021** → art. 963 KC: udział testamentowy odpada (brak podstawienia w testamencie).
- **Działki pod ubojnią (83/2, 84, 85 w Koziołkach) NIE MAJĄ księgi wieczystej** — musi to być uregulowane przed aportem/dzierżawą.
- **Część działek to nieruchomości rolne** → wymagają procedury KOWR (zgoda lub powiadomienie).
- **Dział spadku NIE został przeprowadzony** — wszyscy mają ułamkowe udziały, nic fizycznie nie podzielone.
- **Spłata 100 tys. PLN dla wnuków (Kamil, Wiktoria, Jakub)** wynikająca z testamentu — termin 12 mies. od otwarcia spadku (do 02.08.2024) → już zrealizowana.

### 4.3 Zarządca sukcesyjny

Marcin Piórkowski jest formalnym zarządcą sukcesyjnym (od 2023). Na papierach podpisuje:
- umowy z dostawcami,
- umowy bankowe,
- decyzje strategiczne wymagające reprezentacji,
- dokumenty pracownicze.

Sergiusz operacyjnie prowadzi firmę, ale nie ma formalnego umocowania. Przedstawia się czasem jako „operacyjny manager" lub „de facto CEO/CTO". Na spotkaniach z bankami i partnerami pojawia się sam — Marcin często nie ma czasu (prowadzi Karma-Max).

### 4.4 Termin JDG w spadku

- Otwarcie spadku: **02.08.2023**.
- JDG w spadku ustawowo trwa max **2 lata** = do **02.08.2025**.
- Przedłużenie sądowe: **TAK, do 02.08.2026** (jedno przedłużenie).
- **Drugie przedłużenie wątpliwe** (Grzegorz, doradca finansowy: „nie wiem ile to można przedłużać").
- **TWARDY DEADLINE 1 SIERPNIA 2026** (Urbaniak: „1 sierpnia, nie 2"): po tej dacie JDG wygasa, firma przestaje istnieć jako podmiot prawny. Trzeba mieć sp. z o.o. albo inną formę.

---

## 5. PRZEKSZTAŁCENIE W SP. Z O.O. — PROJEKT STRATEGICZNY

### 5.1 Cel

Przeprowadzenie pełnego procesu prawnego: założenie spółki z o.o., przeniesienie działalności (aport lub dzierżawa — patrz konflikt poniżej), uregulowanie spraw spadkowych, ustalenie struktury zarządu, zabezpieczenie własności intelektualnej (ZPSP), koordynacja z dotacją ARiMR i finansowaniem bankowym.

### 5.2 Konflikt strategiczny: APORT vs DZIERŻAWA

Dwóch ekspertów ma RÓŻNE rekomendacje:

**Mec. Przemysław Urbaniak (TaxLawPro Warszawa) — APORT:**
- Wnosić aportem przedsiębiorstwo do nowej spółki.
- Standardowe rozwiązanie dla przekształceń rodzinnych.
- Projekt umowy spółki gotowy od maja 2025 (czekał 10 miesięcy).

**Wiesław Oślewski (konsultant dotacyjny ARiMR) — DZIERŻAWA:**
- Aport = podatek od zapasów (potencjalnie miliony PLN, bo wycena majątku obejmuje magazyn + należności + maszyny).
- Nowa spółka **bez aportu** = traktowana jako 100% innowacyjny, nowy podmiot = MAKSYMALNE punkty w ocenie wniosku ARiMR.
- Stara JDG (spadkobiercy jako osoby fizyczne) dzierżawi zakład nowej spółce. Dzierżawa elastyczna podatkowo, czynsz negocjowalny.
- Po 6 latach dzierżawa staje się „nierejestrowalna" w podatkowym sensie.

**Status:** Telekonferencja Urbaniak + Wiesław + Sergiusz NIE ODBYŁA SIĘ. Te dwie osoby nigdy ze sobą nie rozmawiały. To **PRIORYTET #1** do rozstrzygnięcia.

### 5.3 Proponowana struktura nowej spółki

**Wariant Urbaniaka (proporcjonalnie do spadku):**
- Marcin Piórkowski — **77%** udziałów
- Sergiusz Piórkowski — **6%**
- Anna Piórkowska — **8,5%**
- Kamil Piórkowski — **8,5%**

**Restrukturyzacja Karma-Max** (rekomendacja Wiesława):
- Marlena (żona Marcina) — 80%
- Marcin — 20%
- Cel: uniknąć powiązania kapitałowego (>25%) między ubojnią a Karma-Max → obie firmy mogą OSOBNO wnioskować o dotacje.

### 5.4 Struktura zarządu — kluczowy punkt sporny

Sergiusz chce być **prezesem zarządu** — motyw: odpowiedzialność majątkowa za kredyty na miliony, ARiMR wymaga key man, banki patrzą kto zarządza, a nie kto formalnie ma udziały. Chce mieć formalne umocowanie odpowiadające 12-letniej rzeczywistości operacyjnej.

Marcin obawia się: utraty kontroli, ogłoszenia upadłości pod nieobecność, przejęcia.

**Proponowane zabezpieczenia dla Marcina (z dokumentu maj 2025):**

1. **Łączna reprezentacja** — żadna decyzja bez podwójnego podpisu.
2. **Veto** na zobowiązania >100 tys. PLN.
3. **Limit długu** >1 mln PLN za zgodą zgromadzenia wspólników (Marcin 77% = decyduje).
4. **Comiesięczne raporty** zarządu dla wspólników.
5. **Coroczny niezależny audyt** finansowy.
6. **Kadencja prezesa z możliwością odwołania** uchwałą zgromadzenia.
7. **D&O insurance** (Directors & Officers).
8. **Zakaz konkurencji** (z wyłączeniem Karma-Max — komplementarna, nie konkurencyjna).
9. **Dywidenda min 50%** zysku netto.
10. **Prawo pierwokupu** udziałów (nikt obcy nie wejdzie).
11. **Klauzula key man** (Sergiusz osobą kluczową, mechanizm zastępstwa).
12. **Mediacja → arbitraż** w razie sporu zarządczego.

**Kompromis rozważany:** Marcin = Przewodniczący Rady Nadzorczej (formalnie WYŻEJ niż prezes), Sergiusz = Prezes operacyjny. Marcin nadzór, Sergiusz wykonanie.

### 5.5 ZPSP — własność intelektualna

ZPSP MUSI być formalnie zabezpieczony **PRZED** założeniem spółki. Najlepsza opcja: **umowa licencyjna**, gdzie:
- Sergiusz Piórkowski (osoba fizyczna) licencjonuje system spółce.
- Opłata: 15–25 tys. PLN/miesiąc (180–300 tys. PLN/rok), rozliczana ryczałtem 8,5%.
- Licencja niewyłączna, terminowa (5 lat z auto-przedłużeniem).
- Wypowiedzenie z 12-miesięcznym vacatio (firma ma czas na zastępstwo).

**Dlaczego nie aport ZPSP:** wniesienie systemu jako majątek spółki = utrata własności intelektualnej + kłopoty z ceną transferową + brak zabezpieczenia w razie odejścia z zarządu.

### 5.6 Harmonogram (cel)

| Krok | Termin | Status |
|---|---|---|
| Telekonferencja Urbaniak + Wiesław + Ser | Q2 2026 | DO ZROBIENIA |
| Decyzja aport vs dzierżawa | Q2 2026 | OTWARTE |
| Lista nieruchomości i decyzji administracyjnych dla Urbaniaka | Q2 2026 | DO ZROBIENIA |
| Negocjacje z Marcinem ws. struktury zarządu | Q2 2026 | W TOKU |
| Umowa licencji ZPSP | przed założeniem spółki | DO ZROBIENIA |
| Akt notarialny umowy spółki | maj-czerwiec 2026 | DO ZROBIENIA |
| Wpis do KRS | czerwiec-lipiec 2026 | DO ZROBIENIA |
| Nowy NIP, REGON, konta bankowe | lipiec 2026 | DO ZROBIENIA |
| Powiadomienie pracowników (30 dni przed aportem, art. 231 KP) | lipiec 2026 | DO ZROBIENIA |
| Spółka działająca | **NIE PÓŹNIEJ NIŻ 31.07.2026** | — |
| JDG wygasa | **02.08.2026** | TWARDY DEADLINE |
| Wniosek ARiMR | wrzesień 2026 | — |

---

## 6. WYNIKI FINANSOWE I KONDYCJA FIRMY

### 6.1 Rachunek zysków i strat 2025

| Pozycja | 2025 | 2024 |
|---|---|---|
| **Przychody** | **318 089 tys. PLN** (~318 mln) | 258 258 tys. PLN |
| ├ sprzedaż produktów (mięso, tuszki) | 284 336 tys. | 218 921 tys. |
| └ sprzedaż towarów (odsprzedaż) | 32 557 tys. | 36 877 tys. |
| **Koszty operacyjne** | **310 809 tys.** | 253 903 tys. |
| ├ surowce i energia (głównie żywiec, prąd) | 246 600 tys. (77% przychodów) | 190 990 tys. |
| ├ wynagrodzenia | 11 090 tys. | 9 639 tys. |
| ├ usługi obce | 15 487 tys. | 12 282 tys. |
| └ amortyzacja | 1 508 tys. | 1 435 tys. |
| **Zysk ze sprzedaży** | **7 279 tys. PLN** | 4 355 tys. |
| Odsetki od kredytów | 262 tys. | 320 tys. |
| **Zysk netto** | **7 038 tys. PLN** | 3 023 tys. |

**Interpretacja:**

- **Wzrost przychodów +23%** r/r — wynik konsolidacji rynkowej po upadłości EXDROB i lepszej polityki cenowej.
- **Zysk netto x2,3** — dowód że firma nie tylko sprzedaje więcej, ale **lepiej**: mniej zamrażania (zamrożona tuszka traci 18% wartości), lepsze marże, lepsze zarządzanie kosztami.
- **Marża 2,29%** — typowa dla drobiarstwa. Każdy 1 grosz na kg = 730 tys. PLN/rok.
- **Surowiec 77% przychodów** — żywiec to absolutnie dominujący koszt. Stąd waga relacji z hodowcami.
- **Odsetki 262 tys. PLN** = praktycznie zerowe zadłużenie. To **rzadkość** przy obrotach 318 mln. Banki to kochają.

### 6.2 Bilans 2025

| Aktywa | Wartość | Pasywa | Wartość |
|---|---|---|---|
| **Aktywa razem** | **33,6 mln PLN** | **Pasywa razem** | **33,6 mln** |
| Aktywa trwałe (maszyny, budynki) | 14,3 mln | Kapitał własny | 8,9 mln (wzrost x2 r/r z 4,4 mln) |
| Aktywa obrotowe | 19,3 mln | Zobowiązania długoterminowe | 237 tys. (kredyty) |
| ├ zapasy | 1,4 mln | Zobowiązania krótkoterminowe | 22,5 mln |
| ├ należności od klientów | **17,6 mln** | ├ wobec hodowców | 17,4 mln |
| └ gotówka | 287 tys. (TYLKO!) | └ kredyty krótkoterminowe | 3 257 tys. |

**Kluczowe wskaźniki bankowe:**

| Wskaźnik | Wartość | Ocena |
|---|---|---|
| Płynność bieżąca | **0,86** | Poniżej 1,0 (norma drobiarstwa, hodowcom płaci się szybciej niż dostają od klientów) |
| Net Debt / EBITDA | **0,36** | DOSKONAŁY — spłaciłby wszystko w <5 miesięcy |
| EBITDA | **8,8 mln** | (zysk 7 mln + amortyzacja 1,5 mln + drobne korekty) |
| Cash flow operacyjny | ~8,5 mln/rok | Z tego spokojnie obsługuje raty pożyczki leasingowej (30–40 tys./mies.) |
| Wzrost kapitału własnego r/r | x2 (4,4 → 8,9 mln) | Firma buduje wartość |

**Czerwone flagi:**
- Należności wzrosły z 12,1 do 17,6 mln (+5,5 mln) → klienci płacą wolniej. **Faktoring** do rozważenia.
- Gotówka 287 tys. przy obrotach 318 mln → praktycznie zerowa rezerwa. Cała kasa zamrożona w cyklu należności-zobowiązań.
- Pobrania prywatne 2,5 mln z zysku netto (Marcin jako zarządca sukcesyjny) — Grzegorz zwracał uwagę.

### 6.3 Sezonowość

- **Q1 (styczeń–marzec)** — sezon słaby. W Q1 2023 firma miała stratę -2,18 mln PLN (sezonowość + ceny rynkowe).
- **Q2 (kwiecień–czerwiec)** — wzrost popytu, sezon grilowy.
- **Q3 (lipiec–wrzesień)** — szczyt: wakacje, grille, festiwale. Wymaga DUŻEJ chłodni — stąd KRYTYCZNOŚĆ inwestycji w chłodnictwo na lato 2026.
- **Q4 (październik–grudzień)** — stabilizacja, sezon świąteczny.

### 6.4 Dynamika rynkowa (luty 2026)

- Ceny tuszki spadały od grudnia 2025.
- Filet z Brazylii w cenie 13 PLN/kg dociska polskich producentów (Piórkowscy ~15–17 PLN/kg).
- Gomak (Godzianów) miał problemy → Sergiusz przejął część ich hodowców.
- Zawirowania na rynku oleju napędowego (wojna USA/Izrael-Iran, kwiecień 2026) → AVILOG presjonuje na podwyżki taryf transportowych żywca.

---

## 7. STRUKTURA ORGANIZACYJNA — KTO KTO JEST

### 7.1 Hierarchia (de facto)

```
ZARZĄD (właścicielski/spadkowy)
├── Marcin Piórkowski — zarządca sukcesyjny, formalna sygnatura
└── Sergiusz Piórkowski — operacyjny manager (CEO/CTO de facto)
       │
       ├── PRODUKCJA (podlega Justynie)
       │     └── Justyna [Chrostowska] — Plant Director (formalnie „główny technolog")
       │           ├── Anna Majczak — brygadzista hali produkcyjnej
       │           ├── Małgorzata Anioł — kontrola jakości (II zmiana)
       │           ├── Klaudia Osińska — kontrola jakości (I zmiana, ocena Sera 5/10)
       │           └── pracownicy hali (rzeźnicy, patroszący, pakowacze, sprzątacze)
       │
       ├── SPRZEDAŻ (handlowcy, podlegają Sergiuszowi)
       │     ├── Pani Jola (Jolanta Kubiak) — staż od 1996, ~60% wolumenu, „karteczki", konfliktowa
       │     ├── Maja Leonard — ESTJ, energiczna
       │     ├── Teresa Jachymczak — Radrob, Ladros, Carrefour, E.Leclerc
       │     └── Anna Jedynak (Ania) — eksport, mrożonki, Makro/Selgros
       │     [Daniel — odszedł 2024 / Radek — odszedł 2026]
       │
       ├── ZAKUPY ŻYWCA (dział zakupów, ~2 osoby)
       │     ├── Teresa (zakupy) — ?? (potencjalny konflikt z Pauliną)
       │     └── Paulina — rozważa odejście (Fireflies 22.04)
       │
       ├── LOGISTYKA / TRANSPORT
       │     ├── Ilona Kubiak — single point of failure, jedyna planistka
       │     ├── Magda Miler — niewyszkolone zastępstwo Iloy
       │     └── Kierowcy (~10–13)
       │
       ├── BIURO
       │     ├── Marlena (sekretarka) — biuro, poczta, faktury, ubezpieczenia
       │     ├── Edyta — księgowa firmowa
       │     ├── Renata Balcerak, Małgorzata Stępniak — fakturzystki
       │     └── Grażyna — księgowa zewnętrzna (RZiS, bilans)
       │
       └── MAGAZYN / MROŹNIA (podlega Justynie)
             ├── Robert Stępniak, Robert Osiński — kompletacja, załadunek
             └── Jan Matusiak, Michał — mroźnia
```

### 7.2 Kluczowe osoby — szczegółowo

**Justyna [Chrostowska]** — Plant Director, formalnie „główny technolog". 30+ lat doświadczenia. Frustracja: brak narzędzi do egzekwowania, „zamęczanie pod dyktando" innych osób. Aktywnie używa kamer (Hikvision RTSP) do nadzoru hali. Robi ZA DARMO HACCP w masarni Marcina (Zgierz). Kluczowa relacja partnerska Sera. Oficjalne zwycięstwo: zarządza całą produkcją, magazynem, mroźnią.

**Pani Jola (Jolanta Kubiak)** — staż od 1996 roku. Mitomanka, konfliktowa, prowadzi „karteczki" zamiast wpisywać do ZPSP. Sprzedała kiedyś 4,5 t żołądków bez wpisu do systemu. Kontroluje ~60% wolumenu sprzedażowego (Damak, Trzepałka, AGMAR). „Most" do najważniejszych klientów. Strategia: dywersyfikacja — Maja i Ania budują swoje relacje z jej klientami, żeby Jola nie była jedynym mostem. Tematyka „karteczki ZAKAZANE" w procedurach.

**Ilona Kubiak** — single point of failure logistyki. Jedyna osoba znająca trasy. Audit Locura wykazał: rozjazd 8000 litrów paliwa w 72 dni na dystrybutorze wewnętrznym (Swimmer), GPS-y pokazują trzy razy auto firmowe pod prywatnym adresem Ilony, faworyzacja kierowcy Panaka. Konflikty z kierowcami (Robert Staroń: „prosiłem o zapasowe węże, Ilona zignorowała, wąż pękł w trasie"; „Ilona daje godziny na pałę, jak chcesz wiedzieć to dzwoń sam do magazynu"). Justyna ma od kwietnia 2026 nadzorować plan tras Iloy.

**Marlena Piórkowska** — sekretarka. Żona Marcina. 20% udziałów w Karma-Max (po restrukturyzacji). Centralna postać w sprawach ubezpieczeniowych (PZU, Hermes), kadrowych, reprezentacyjnych. Inteligentna, lojalna wobec Marcina, ale racjonalna — w negocjacjach o spółkę argumenty „prezes = odpowiedzialność, nie władza" trafiają do niej.

**Marcin Piórkowski** — wujek Sergiusza, zarządca sukcesyjny ubojni, prowadzi własną firmę Karma-Max (produkcja karmy z odpadów poubojowych). 77% spadku po ojcu Jerzym. Bezpośredni („proszę walić wprost"), praktyczny, planuje halę magazynową za 2 mln PLN, zna swoją branżę (HD/HDI, kategoria 3, ARiMR Karma-Max). Operacyjnie ubojnią się NIE zajmuje — pojawia się 1–2x w tygodniu na kilka godzin. Boi się: utraty kontroli, podpisania czegoś czego nie zrozumie, utraty Karma-Maxa.

**Edyta** — księgowa firmowa. Bankowość, wyciągi, rozliczenia. Pracuje z Symfonią Finanse + Księgowość.

**Grażyna** — księgowa zewnętrzna, robi RZiS i bilans. Fizycznie pisała na drukowanym RZiS notatki Grzegorza (EBITDA, Net Debt) podczas spotkania.

**Klaudia Osińska** — kontrola jakości. Ocena Sera 5/10 (brak systematyczności). Justyna ją trenuje.

**Anna Majczak** — brygadzista hali. Podejmuje plan dnia od „P. Dyrektor" (Justyny). Chce ZPSP dla zamówień produkcji.

**Małgorzata Anioł** — kontrola jakości (II zmiana). Wpisywanie produkcji, godziny pracowników agencyjnych. Też chce ZPSP.

**Daniel** — były handlowiec. Zwolniony, próbował wyciągnąć prowizje od swoich klientów po odejściu. Ostrzeżenie dla NDA i zakazu konkurencji.

### 7.3 Łapacze drobiu

Zewnętrzna ekipa łapaczy do odbioru żywca z ferm hodowców. Plan kwiecień 2026: zwiększyć zespół do **56 osób**. Płatność za auta (nie za godziny). Czasem konflikty (procedury BHP, sezonowość).

### 7.4 Pracownicy z Nepalu

Rekrutowani przez agencje (głównie GURAVO, IMPULS). Pracują na hali. Wymagają wsparcia językowego (tłumaczenia, ikony w UI ZPSP, instrukcje wizualne). Stawki rynkowe.

---

## 8. PRODUKCJA — UBÓJ, ROZBIÓR, KLASYFIKACJA

### 8.1 Zmiany robocze

**Zmiana A (5:00–13:30)** — ubój + pierwszy rozbiór:
- 3:30 — wybicie pierwszego kurczaka (Kierownik Uboju + ekipa).
- 5:00 — pełny start linii.
- 5:30–6:00 — pierwsza tuszka wchodzi do produkcji „czystej" (po skubarce, patroszarce, chillerze).
- 13:00 — spotkanie operacyjne ws. niesprzedanego towaru: handlowcy + Dyrektor (Justyna). Decyzja: krojenie / mrożenie.
- 13:30 — koniec zmiany A.

**Zmiana B (14:00–21:00)** — kontynuacja produkcji + sprzątanie + przygotowanie na rano. Czasem kontynuacja uboju przy dużym wolumenie.

**Latem (gorące dni):** ubój zaczyna się o 2:30 — żeby nie operować na największych temperaturach.

### 8.2 Linia uboju

- **Wydajność teoretyczna:** 7500 sztuk/h.
- **Wydajność realna:** ~7000 sztuk/h (przestoje, zmiany noży, awarie).
- **Patroszarka (obecna):** Meyn (starsza), planowana wymiana na Meyn Maestro we wrześniu 2026 pod dotację (~5 mln PLN).
- **Rozbiór:** Meyn + ręczne dokończenie. Filetowanie ręczne.

### 8.3 Klasyfikacja A vs B

- **Klasa A** — pełnowartościowa tuszka, idzie na klientów hurtowych, sieci.
- **Klasa B** — z drobnymi skazami (krwiaki, złamania, żółć), idzie na rozbiór: filet, ćwiartki, pałki.
- Klasyfikacja: ręczna / wzrokowa, na linii (po patroszarce / po chillerze).
- Proporcje: ~80% A / ~20% B.

### 8.4 Klasy wagowe (rozmiary 6/7/8/9/10/11)

**WAŻNE — częsta pomyłka:** „rozmiar 7" NIE oznacza 7 kg ani 7 kg żywca. **To liczba sztuk tuszek, które mieszczą się w pojemniku E2 do nominalnych 15 kg netto.**

Mechanika:
- Pojemnik E2 zawsze pakowany jest na ~15 kg netto (±tolerancja).
- **Rozmiar = ile tuszek mieści się w tym pojemniku 15 kg.**
- Z tego wyliczasz średnią wagę tuszki: `15 kg ÷ rozmiar`.

Tabela klas:

| Rozmiar | Liczba tuszek w pojemniku 15 kg | Średnia waga tuszki | Charakter |
|---|---|---|---|
| 6 | 6 szt | ~2,50 kg | Duża tuszka, najcięższa |
| 7 | 7 szt | ~2,14 kg | Standard |
| 8 | 8 szt | ~1,88 kg | Standard niższy |
| 9 | 9 szt | ~1,67 kg | Mniejsza |
| 10 | 10 szt | ~1,50 kg | Mała |
| 11 | 11 szt | ~1,36 kg | Bardzo mała |

**Najpopularniejsze rozmiary:** **6, 7, 8.**

Klient zamawia po rozmiarze, nie po wadze tuszki, bo dla niego liczy się jak będzie pakował dalej (sieć/restauracja/zakład mięsny). Plan produkcji uwzględnia mix rozmiarów wg kontraktów tygodniowych.

### 8.5 Przeliczniki

- **Żywiec → tuszka: ~78%** (200 ton żywca → ~156 ton tuszki na koniec dnia).
- **Tuszka A vs B:** ~80% A, ~20% B.
- **Krojenie B:** filet ~30%, ćwiartki ~25%, pałki/skrzydła reszta.
- **Mrożenie:** strata wagi -2% (norma), strata wartości handlowej -18% vs świeży.

### 8.5A Decyzja: krojenie vs sprzedaż tuszki vs mrożenie — moduł ZPSP

Jeden z najważniejszych modułów ZPSP to **„Krojenie"** — kalkulator który pokazuje, czy danego dnia opłaca się sprzedać tuszkę, pokroić ją na elementy i sprzedać świeże, czy zamrozić i sprzedać taniej później. Pełny opis modułu: **patrz sekcja 14A**.

Realny przykład z ekranu (19 800 kg tuszki, cena rynkowa 7,33 PLN/kg):

| Scenariusz | Wartość | Wynik vs sprzedaż tuszki |
|---|---|---|
| **Sprzedaż tuszki** (19 800 kg × 7,33 zł) | 145 134 zł | (baseline) |
| **Krojenie + sprzedaż elementów świeżych** (po -11 647 zł kosztów krojenia) | 136 167 zł | **-8 967 zł** w tym układzie cen *(uwaga: poprzednio na ekranie było +0,45 zł/kg — różnica zależy od bieżących cen elementów)* |
| **Krojenie + mrożenie + sprzedaż taniej** (po -15 119 zł kosztów mrożenia + zaniżenie ceny) | 99 259 zł | **-45 875 zł STRATA** (-2,32 zł/kg) |

**Wniosek operacyjny:** **MROŻENIE = OSTATNIA OPCJA.** Daje strukturalnie kilkadziesiąt tysięcy strat na każdej operacji. Stąd polityka „sprzedawać świeże na bieżąco, mrożenie tylko gdy nie ma odbiorcy".

Pełna struktura kosztów krojenia + mrożenia (per kg towaru): patrz sekcja 14A.

### 8.6 Halal

Robione okazjonalnie dla wybranych klientów (sieci, eksport). Wymaga: oddzielnego ubojowca z certyfikatem, oddzielnej godziny w ciągu zmiany, dokumentacji.

### 8.7 Strefa zagrożona

Kurczaki z restrykcyjnych stref (HPAI, Newcastle Disease) wymagają osobnego oznaczenia, dokumentacji, czasem nie mogą iść do określonych klientów. W lutym 2026 ognisko Newcastle Disease 12 km od zakładu — pełen protokół biosekuracji.

### 8.8 Konfiskaty i padłe — gdzie w ZPSP

- **Padłe w transporcie** (kurczaki padłe podczas zawieszania na linię): wpisywane w ZPSP w **menu → Specyfikacja Surowca → kolumna „Padłe"**.
- **Konfiskaty z linii** (decyzja weterynarza powiatowego, klasyfikacja kategoria 1/2 odpadów): w tej samej tabeli — **suma kolumn `CH` (chłonność/cellulit?), `ZM` (zmiany?), `NW` (niewłaściwy/nadwaga?)**. To są kategorie wewnętrzne kodowania konfiskat na linii.
- **Weterynarz powiatowy** obecny na zakładzie codziennie (kontrola ante-mortem i post-mortem).
- Padłe + konfiskaty → utylizacja (kategoria 1/2). Część surowca, która nie spełnia wymogów na karmę, też idzie do utylizacji; reszta → **Karma-Max** (HD/HDI, kategoria 3).

### 8.9 Wychłodzenie (chiller)

Po patroszeniu tuszka idzie do **chillera tunelowego** — temperatura schładzania -2 do +4°C. Czas: ok. 60–90 min. Logowanie temperatury automatyczne (HACCP).

### 8.10 Cele cenowe i sprzedażowe (cele 2026)

- Tuszka A: ~8,50 PLN/kg
- Filet (klasa B): 15–17 PLN/kg
- Udko: ~6,50 PLN/kg
- Skrzydło: ~9,00 PLN/kg

### 8.11 Spread żywiec→produkt — uwaga o aktualności

Historyczne „spread 2,50 PLN/kg" było **umownym celem „wyjścia na zero"** wyliczonym z grubsza, ale jest **DAWNO NIEAKTUALNE**. Żeby wiedzieć, czy między zakupem żywca a sprzedażą produktu jest realny zysk, trzeba zsumować **wszystkie koszty firmy** (energia, ludzie, opakowania, transport, amortyzacja, podatki, finansowanie, BHP, weterynaria, certyfikacja), nie tylko cenę żywca i sprzedaży.

**Action item:** zbudować w ZPSP osobny moduł kalkulacji rentowności rzeczywistej per partia / per dzień / per klient — uwzględniający pełny koszt jednostkowy (CCJW — całkowity koszt jednostkowy wytworzenia). To temat na 2026 r.

---

## 9. SPRZEDAŻ I KLIENCI

### 9.1 Top klienci

Klienci kluczowi (wolumenowo i strategicznie):

| Klient | Kategoria | Handlowiec | Komentarz |
|---|---|---|---|
| **DAMAK** | Hurtownia | Pani Jola | Codziennie, kierowca Drożdżyk, ranny okno |
| **TRZEPAŁKA AGMAR** | Hurtownia | Pani Jola | Codziennie, ~25-tonowe trasy |
| **BOMAFAR** | Hurtownia | — | Okno 15:00 |
| **PUBLIMAR** | Hurtownia | — | Okno 14:00 |
| **RADROB** | Hurtownia | Teresa | — |
| **LADROS** | Hurtownia | Teresa | — |
| **PIEKARSKA** (Biesarska?) | — | — | — |
| **SZUBRYT** | — | — | — |
| **DESTAN** | — | — | — |
| **PODOLSKI** | Hurtownia | — | — |
| **PAMSO** (Pabianice) | Hurtownia | — | — |
| **WIERZEJKI** (Trzebieszów) | Hurtownia | — | — |
| **DROBEX** (Bogusławski) | — | — | Konkurent / partner |
| **MARKET** (nowy) | Sieć | — | 4 dni/tyg, 1,5–1,7 t |
| **EUROPE TRADE / EGE FOOD / EUREKA / KAPTAIN FOOD** | Trading | Ania | Eksport |
| **JBB BAŁDYGA — ŁYSE** | Zakłady mięsne (przetwórnia) | — | **Jedyny mały klient typu „mięsny w hurcie"** — szczególny segment |

### 9.2 Handlowcy — przypisania (stan aktualny)

- **Pani Jola** (12 klientów, ~1640 t/miesiąc) — Damak, Trzepałka, RADDROB Chlebowski, sklepy ABC, Dino Polska
- **Ania** (9 klientów, ~1110 t) — Makro C&C, Selgros, Polomarket, eksport
- **Teresa** (8 klientów, ~1080 t) — Carrefour, E.Leclerc, Radrob, Ladros, Delikatesy Centrum
- **Maja** (6 klientów, ~280 t) — Stokrotka, mniejsze sieci, ESTJ — energiczna

**Byli handlowcy (nie pracują już w firmie):**
- **Daniel** — odszedł, próbował wyciągnąć prowizje od swoich klientów po odejściu (ostrzeżenie dla NDA i zakazu konkurencji w nowych umowach).
- **Radek Marciniak** — **ODSZEDŁ** (stan: maj 2026). Klienci po nim przejęci przez resztę zespołu.

**Po reorganizacji** dział sprzedaży zredukowany z 6 do ~3,5 etatu, mimo to obroty wzrosły z 258 do 318 mln PLN — dowód, że nadmiar handlowców nie korelował z wynikiem.

### 9.3 Segmentacja klientów (VIP / P2 / P3 / P4 / P5)

Scoring 7-kryterialny, wagi:
1. Warunki płatności (waga x3) — przedpłata bije 30 dni.
2. Obrót miesięczny (waga x2) — progi: >200k / 50–200k / <50k PLN.
3. Marża (waga x2).
4. Staż współpracy.
5. Elastyczność asortymentowa (kto bierze wszystko = wyżej).
6. Przewidywalność zamówień.
7. Znaczenie strategiczne (bonus).

Wzór punktowy: każdy klient dostaje 20–110 pkt → klasa **VIP / P2 / P3 / P4 / P5**.

### 9.4 Kontrakty tygodniowe

Klienci kluczowi mają e-mailowe potwierdzenie warunków na tydzień (cena, ilość, harmonogram). To jest podstawa planowania produkcji i transportu.

### 9.4A Mali klienci typu „mięsny w hurcie" (Łyse)

Specyficzny segment — **mali klienci typu zakłady mięsne biorące w niewielkich ilościach**. Aktualnie firma ma w tym segmencie **TYLKO JEDNEGO** klienta:

- **JBB Bałdyga (Łyse)** — zakłady mięsne, przetwórnia. Bierze niewielkie ilości w stosunku do tego, jak duży jest sam JBB. To „mały" klient w naszych książkach, mimo że nazwa znana w branży.

Plan: ten segment ma potencjał wzrostu (małe i średnie zakłady mięsne, masarnie regionalne). Wymaga jednak przewagi cenowej + szybkości dostawy + jakości — czyli dokładnie tego, co Piórkowscy mają jako lokalna ubojnia z własnym transportem 400 km zasięgu.

### 9.5 Limity kredytowe

- **Ubezpieczyciel** (Hermes) ustala limit kupiecki dla każdego klienta na podstawie sprawozdań finansowych klienta.
- Limit wpisywany do Symfonii (`STContractors.CreditLimit`).
- Klient bez limitu = cash-only / przedpłata.

### 9.6 Rabaty i ceny

- **Rabaty** udziela WYŁĄCZNIE Zarząd (Sergiusz / Marcin).
- **Ceny mrożonek** ustala WYŁĄCZNIE Zarząd.
- **Karteczki ZAKAZANE** — wszystko musi być w ZPSP. Tematyka egzekwowania trwa od lat.

### 9.7 Lista 58 sieci do pozyskania

Excel z 58 potencjalnymi sieciami — TOP 10 to:
- **Topaz** (120+ sklepów, wschód Polski)
- **Prim Market** (60+ sklepów)
- **API Market** (22 sklepy, Mazowieckie)
- **Wafelek** (23 sklepy)
- **Chata Polska** (210+ sklepów, Wielkopolska + Łódzkie)
- **Chorten Północ / PD** (kujawsko-pomorskie / wielkopolskie)
- **Top Market** (580+ sklepów)
- **Społem Łódź / Tomaszów Maz.**
- **Dino Polska** (2500+ sklepów)
- **Lewiatan** (3200 sklepów, „Sklepy STĄD")

Cel: pozyskać 5–10 nowych sieci do końca 2026, używając certyfikatu BRC v9 jako dźwigni (wszystkie sieci wymagają BRC/IFS).

### 9.8 Spotkania operacyjne

- **9:00 (2–3x w tygodniu)** — sala konferencyjna, prowadzi Sergiusz lub Justyna. Dni: pon/śr/pt typowo.
- **13:00 (codziennie)** — handlowcy + Justyna + Sergiusz. Decyzja: krojenie / mrożenie nadwyżek.
- **Wtorkowe rozszerzone** — myjka pojemników + opakowania + finanse + należności.

### 9.9 Komunikacja

- **WhatsApp grupy:** Handlowa, Produkcyjna, Jakość. Główne narzędzie operacyjne.
- **Plan migracji na Microsoft Teams** (kanały: #sprzedaz, #produkcja, #logistyka, #jakosc, #zarzad).
- **E-maile:** kontrakty tygodniowe, oferty, korespondencja formalna.

### 9.10 Anulacje i reklamacje

- **Anulacja popołudniowa** (klient dzwoni o 16:00) — dziś chaotyczna procedura. Kto odbiera? Jak info dochodzi do hali? Kto reaguje? — **PROBLEM DO PROCEDURALIZACJI**.
- **Reklamacje** → handlowiec zgłasza w ZPSP z poziomu faktury (rodzaj, priorytet, partia, zdjęcie). Klaudia obsługuje.
- ~75% reklamacji = automatyczne korekty faktur. Plan: budowa systemu PRZED-fakturowego wykrywającego rozjazdy między ilością załadowaną a fakturowaną.

---

## 10. ZAKUP ŻYWCA I HODOWCY

### 10.1 Liczby

- **140+ hodowców w bazie**, **40–70 aktywnych** w danym miesiącu.
- Strategia **50/50 kontrakt vs wolny rynek** (gwarancja vs taniej).
- Cykl wstawień: ~35–45 dni od pisklaka do uboju.
- Promień: większość hodowców 30–40 km od zakładu (bardzo blisko, niski koszt transportu).

### 10.2 Tabela hodowców (skrót)

W ZPSP jest tabela `WstawieniaKurczakow` (2453+ wpisy):
- Data wstawienia,
- Hodowca,
- Liczba sztuk piskląt,
- Pasza (BROJLER ALFA, Brojler Grower 1/2/Finiszer/Starter),
- Dostawca paszy (TASOMIX, De Heus, Ekoplon),
- Ubytek (% śmiertelności w trakcie hodowli) — typowo 3–7%,
- Planowana data uboju (=wstawienie + 35–42 dni).

### 10.3 Pasza

Firma kupuje paszę i odprzedaje hodowcom kontraktowym. Faktury sPZ od TASOMIX, De Heus, Ekoplon. To dodatkowa zależność — hodowca kontraktowy bierze paszę „na fakturę", odejmuje od ceny żywca przy odbiorze.

### 10.4 Ceny

Pracownik zakupów wpisuje codziennie do ZPSP **trzy ceny rynkowe**:
- **CenaTuszki** — rynkowa cena tuszki (z e-drób / PIR).
- **CenaRolnicza** — ministerialna oficjalna cena żywca.
- **CenaMinisterialna / CenaŁączona** — wskaźniki referencyjne.

Plan: scrapping automatyczny (1x dziennie) zamiast ręcznego wpisywania.

### 10.5 Dział zakupów — sytuacja kadrowa

- **Teresa** + **Paulina** — 2 osoby. Konflikt (Fireflies 22.04). Paulina rozważa odejście.
- **Łapacze drobiu** — zewnętrzna ekipa, rośnie do 56 osób.

### 10.6 Zaległości

Z Fireflies: **>500 tys. PLN zaległości** od hodowców (głównie za paszę). Procedura windykacji niesformalizowana.

### 10.7 Kryzys epidemiologiczny

- **HPAI (ptasia grypa)** — w Polsce 19 ognisk, 2 łódzkie. Strefa restrykcyjna może blokować dostawy z konkretnych ferm.
- **Newcastle Disease** — luty 2026, ognisko 12 km od zakładu. Pełna procedura biosekuracji + dokumentacja IRZplus + raport do weterynarza powiatowego.
- W ZPSP planowany **moduł geofencing biosekuracja** (WebFleet) — alert gdy auto wjeżdża do strefy zagrożonej.

### 10.8 Hodowcy kontraktowi vs wolny rynek

- **50% kontrakt** — gwarancja odbioru po ustalonej cenie. Hodowca bierze paszę od firmy, dostaje gwarancję odbioru. Cena niższa (4,40 PLN/kg ostatnio).
- **50% wolny rynek** — kupowane na bieżąco, wyższa cena (5,23 PLN/kg ostatnio).

Plan: monitoring marży per dostawca (waga × cena tuszki - cena zakupu × uzysk) → ranking hodowców jakościowo-ekonomiczny.

### 10.9 Pisklęta — JDA Jeżów

Pisklęta od JDA (Hatchery, Jeżów). Umowa JDA → Sergiusz (kupuje pisklaki) → Stróżewski (tuczy) → Sergiusz (odbiera kurczaka). 7 wstawień po 39 000 szt. w 2026. Cena ustalana 7 dni przed nakładem jaj. Własność piskląt przechodzi na firmę dopiero po zapłacie. Kara umowna za późną rezygnację: równowartość partii (~setki tys. PLN przy 39 000 szt.).

---

## 11. TRANSPORT I LOGISTYKA

### 11.1 Flota

| Pojazd | Liczba | Ładowność | Komentarz |
|---|---|---|---|
| Solówki (mniejsze ciężarówki chłodnie) | 5 | 10 500 kg | — |
| TIRy (ciągnik + naczepa chłodnia) | 5 | 19 800 kg | — |
| Bus duży (Sasin) | 1 | 3 500 kg | sprawny |
| Bus mały | 1 | — | w naprawie |
| **Razem** | **12** | — | — |

Zasięg operacyjny: ~400 km. Dalej — zewnętrzny transport (mrożonki, eksport).

### 11.2 Kierowcy (luty 2026)

Na liście 13 kierowców, realnie ~10 dostępnych:

| Kierowca | Status | Stawka | Komentarz |
|---|---|---|---|
| **Panak** | Aktywny | 60 gr/km na dużym (najwyższa) | Faworyzowany przez Ilonę (audit Locura) |
| **Tołkaczewicz** | Aktywny | 55 gr/km solówki | Wyjątkowa stawka |
| **Drożdżyk** | Aktywny | 50/55 gr/km | Stałe trasy Damak + Trzepałka |
| **Łukaszewicz** | Aktywny | — | Wielodniówkowiec (pn-sob) |
| **Banek** | Aktywny | — | Wielodniówkowiec |
| **Patos** | Aktywny | — | Opiekuje się niepełnosprawnym wujem |
| **Gawęcki** | Na zwolnieniu | 55 gr/km solówki | Wyjątkowa stawka |
| **Czapla** | Na zwolnieniu | — | — |
| **Szczepaniak** | Na zwolnieniu | — | Zastąpiony przez Polcyna |
| **Polcyn** | Nowy | — | — |
| **Staroń (Robert)** | Grozi odejściem | — | Konflikt z Iloną o serwis |
| **Gałek** | Odchodzi | — | Wypowiedzenie, audyt wykazał najwięcej nadpłat |
| **Kołodziejczyk** | Aktywny | — | — |

### 11.3 System rozliczeń

- **Do 350 km:** stawka za km + 60 PLN ryczałtu.
- **Powyżej 350 km:** tylko kilometrówka.
- **Solówki:** 0,50 PLN/km (wyjątki 0,55).
- **Duże:** 0,55 PLN/km (Panak: 0,60).
- **Delegacja krajowa:** 45 PLN/dobę powyżej 8h.
- **Niedziele:** kierowcy negocjują dopłaty — strategia Sera: gra na zwłokę.

### 11.4 GPS i tachograf

- **WebFleet** (TomTom Telematics) — Ilona codziennie, planowanie tras.
- **Kamerki w autach** — zamontowane, **NIEUŻYWANE od 2 lat** (firma płaci, nikt nie podgląda). Robert Kuczyński (przedstawiciel) odpowiedzialny za naprawy.
- **TachoShare wykupiony, NIEUŻYWANY** (audit Locura).
- **Karty tachografu** — kierowcy nie zawsze logują się swoimi (nieprawidłowość). Plan: „kto nie loguje się swoją kartą, nie wyjeżdża."

### 11.5 Audyt kierowców 2026

Sergiusz przeprowadził forensic audit na bazie danych WebFleet GPS + arkusze tras + faktury paliwowe (Swimmer, Cent):

- **~36 000 PLN nadpłat** zidentyfikowanych (2023–2026).
- **GAŁEK** — największe źródło: systematyczne zawyżanie krótszych tras.
- **Rozjazd 8000 litrów paliwa** w 72 dni na dystrybutorze wewnętrznym Swimmer.
- **GPS pokazał trasę firmowego auta pod prywatny adres Iloy** — wielokrotnie.
- **Sabotaż** — pojedynczy incydent (zniszczenie wyposażenia auta).

### 11.6 AVILOG (zewnętrzny transport żywca)

AVILOG prowadzi transport żywca z ferm hodowców do zakładu. Plan w PDF/Excel → ZPSP `WidokMatrycaWPF` (parser PDF).

**Wolumen kwartalny:** ~8500 t, **roczny:** ~34 000 t, **wartość:** ~3,9 mln PLN/rok przy 114 PLN/t.

**Statystyki AVILOG (Q2 2025):**
- Wypełnienie aut: 93% (12,2 t / 13,2 t max). Bywały tygodnie 87%.
- 5,27 godz./kurs, z czego 2,30 jazda + 56% „inne" (załadunek, mycie, pauzy, oczekiwanie).
- Wynajem dzienny 740 PLN × 8 aut × 53 dni = 313 760 PLN/kwartał.

**Negocjacje kwiecień 2026** (Wojtek/Gabryś):
- Avilog chce podwyżki — argument: paliwo (ON Orlen Ekodiesel wzrosło z 5800 do 6500+ PLN/m³ po wybuchu wojny USA-Iran).
- Sergiusz negocjuje: klauzulę sunset (cena spada gdy ON spada), wymianę aktualnych statystyk (te są sprzed roku), zniżkę przy wzroście wolumenu (Perzyna stawia kurnik 100 tys. szt. 20 km od zakładu, Bernat z Piotrkowa).
- Cel: utrzymać 117–119 PLN/t z klauzulą sunset zamiast 125–130 PLN/t bez.

### 11.7 Kursy własne — typowe trasy

- Damak (codziennie, rano)
- Trzepałka (codziennie, rano)
- Bomafar (15:00)
- Publimar (14:00)
- Łądrosz, Piekarska, Radrob (regularne)
- Market (nowy, 4 dni/tyg.)

### 11.8 Plan modułu transportowego ZPSP (4-fazowy)

**Faza 1: Live tracking + raporty paliwowe**
- WEBFLEET.connect API (`showObjectReportExtern`, `showVehicleReportExtern`).
- Rejestracja każdego tankowania w ZPSP (data, kierowca, pojazd, litry, km).
- Tygodniowy raport zużycia per pojazd. Alert >15% odchylenie.

**Faza 2: Geofencing biosekuracja**
- Strefy HPAI / Newcastle.
- Alert gdy auto wjeżdża do strefy zagrożonej.
- `getQueueMessagesExtern` (kolejka eventów).

**Faza 3: Auto-dispatch (eliminacja SPOF Iloy)**
- `showNearestVehicles` API.
- Algorytm: zamówienie + lokalizacja klienta + dostępne auta + pauzy 561/2006 + waga → najbliższe auto.
- Justyna nadzoruje plan (ZPSP propozycja → Justyna akceptuje → Ilona implementuje).

**Faza 4: ETA dla klientów + analiza kosztów per klient/hodowca**
- Notyfikacja klienta o ETA.
- Koszt transportu per dostawa → marża per klient.

---

## 12. MAGAZYN, MROŹNIA, FIFO

### 12.1 Pomieszczenia

- **Magazyn 65554** — świeże po uboju. Główny chłodzony magazyn.
- **Magazyn 65556** — wydania. 110k+ dokumentów wydania w bazie. Fizycznie oddzielne pomieszczenie z rampą załadunkową.
- **Mroźnie:** 3 komory + chłodnia + szokówka (-30 do -40°C).
- **Chiller tunelowy** (po patroszarce, schładzanie tuszek -2 do +4°C, 60–90 min).

### 12.2 Pojemniki E2

Standard pojemników do świeżych produktów (na palecie 36 lub 40 szt.). Sergiusz negocjuje stale o opakowania zwrotne (Avilog brak ewidencji opakowań — audit Locura).

### 12.3 FIFO

**Bezwzględne FIFO** (First In, First Out). Najstarsze najbliżej wyjścia. Etykiety z datą widoczne. Magazynierzy sprawdzają.

### 12.4 Inwentaryzacja

- **Codzienna** — podwójne liczenie: koniec dnia + rano. Wpisy w ZPSP.
- **Tygodniowa pełna** — w mroźni. Trwa kilka godzin, kilka osób.
- **Rozbieżności >5 kg** → NATYCHMIAST Dyrektor.

### 12.5 Standard wagi

- W ZPSP: `Article.StandardWeight = 15 kg`, `StandardTol = 0,2–0,3` kg.
- Pojemniki na ~15 kg towaru.
- Przy odchyleniu >tolerancja → flag w systemie.

### 12.6 Etykietowanie

- Drukarki Zebra/Sato.
- `EtykietyZbiorcze` w bazie (~36k wpisów).
- Etykieta zawiera: data, partia, klasa, kod GS1-128.
- Kod GS1-128 = unikalny identyfikator pojemnika dla traceability „od pola do stolu".

### 12.7 Mrożenie

- **Decyzja co mrożyć** — codzienne spotkanie 13:00. Bilans niesprzedanego towaru → krojenie lub mrożenie.
- **Strata wagi:** 2% (norma). Powyżej 2% → raport.
- **Strata wartości:** -18% vs świeży (mrożona tuszka).
- **Etykiety mroźnicze:** data mrożenia, data przydatności (zwykle 12 miesięcy), numer partii, waga.
- Plan: dodanie daty mrożenia na etykiecie (obecnie nieobecna — plan naprawczy).

### 12.8 Wydania

- **NIE MA SKANOWANIA kodów na wydaniu** (stan: maj 2026). To jest temat do wdrożenia w 2027 (ZPSP/RFID pod dotację ARiMR).
- Aktualnie wydania działają przez **panel magazyniera w ZPSP** — magazynier:
  1. Otwiera panel magazyniera w ZPSP, widzi listę zamówień gotowych do wydania,
  2. Kompletuje paletę wg listy (ręcznie, wzrokowo),
  3. Klika „skompletowane / wyda" → ZPSP generuje WZ (Wydanie Zewnętrzne),
  4. Drukuje WZ → kierowca podpisuje,
  5. Towar idzie na samochód.
- Czas: 30–60 min na samochód (1–5 t).
- **Awizacja klienta** — godzina odbioru. Plan: slot booking w ZPSP.
- **Kody GS1-128** drukowane są przy etykietowaniu na produkcji (osobny moduł), ale **na wydaniu nikt ich nie skanuje** — etykiety pełnią rolę informacyjną (data, partia, klasa), nie systemową.

### 12.9 Mrożona historia w bazie

W tabeli `State0E` (mroźnia) jest towar od 2022-01-03 (np. Korpus z Kurczaka, ID 16, 7000+ pojemników). To historia transakcji, niekoniecznie fizyczne 4-letnie mięso.

### 12.10 Eksport mrożonek

- Mały, głównie pośrednicy (Ania).
- Cel 2026: eksport bezpośredni do DE / NL / RO.
- Wymaga: certyfikatu BRC v9, etykiet obcojęzycznych.

---

## 13. JAKOŚĆ, BHP, CERTYFIKACJA

### 13.1 HACCP

Wdrożone, prowadzi Justyna. Codzienne pomiary:
- Temperatury (chłodnie, mroźnie, samochody) — logowanie automatyczne.
- Próby mikrobiologiczne (laboratorium z Brudzewa, kontakt prywatny WhatsApp Justyny).
- Mycie i dezynfekcja (mata higieniczna, śluza wejściowa).
- Wymazówki — co tydzień.

### 13.2 BRC v9 + IFS — wdrażanie 2026–2027

**Partner: BioEfekt Global, Wojciech Rybka.** 60–70 projektów w powiecie łódzkim od 2005, zna lokalną weterynarię (Pan Adam z inspektoratu w Łodzi).

**Pakiet (umowa kwiecień 2026, ~100 tys. PLN netto + obsługa miesięczna):**

1. **Projekt technologiczny** — 43 000 PLN. Plan zakładu, mapy stref, wymiary pomieszczeń. Niezbędny do pozwolenia zintegrowanego.
2. **Dokumentacja BRC v9** — 45 000 PLN.
3. **Dokumentacja IFS** — w trakcie, w pakiecie z BRC (BioEfekt prowadzi obie ścieżki równolegle, większość dokumentacji jest wspólna).
4. **Obsługa miesięczna BRC/IFS/HACCP** — 6 500 PLN/mies.
5. **Audyt certyfikujący BRC** — ~10 000 PLN (z rabatem BioEfekt, jednostka certyfikująca).
6. **Audyt certyfikujący IFS** — osobna ścieżka, podobna kwota.

**Dlaczego BRC + IFS:**
- **BRC** (British Retail Consortium) — wymagany przez sieci typu Tesco, Sainsbury's, brytyjskie i większość polskich sieci.
- **IFS** (International Featured Standards) — wymagany przez sieci niemieckie i francuskie (Lidl, Aldi, Carrefour, Auchan, Edeka, Rewe).
- Mając OBA — masz drzwi do każdej sieci handlowej w UE. **Eksport bezpośredni 2027 wymaga obu.**
- Większość dokumentacji jest wspólna, więc koszt drugiego certyfikatu jest ułamkowy względem pierwszego.

**Harmonogram:**
- **Faza 1 (Q2 2026):** pozwolenie zintegrowane środowiskowe (woda 800 m³ vs limit 508 m³ — KRYTYCZNE).
- **Faza 2 (Q3 2026 – Q1 2027):** projekt technologiczny (Rybka, plan pod 150 tys. szt. tygodniowo, 1000 m³ wody, patroszarka, myjka, BRC/IFS strefy).
- **Faza 3 (Q1 2027 – Q3 2027):** dokumentacja BRC + IFS + szkolenia + audyt → certyfikaty koniec 2027.

**Warunek:** Sergiusz przedłożył umowę z BioEfekt do Marcina (formalny sygnant). Negocjował 8 punktów: termin gwarancji, klauzula aktualizacji pod nowe pozwolenie, mapy DWG AutoCAD, terminy płatności, kary za zwłokę.

### 13.3 Pozwolenie zintegrowane

- Aktualne: na **508 m³ wody/dzień**.
- Realne zużycie: **~800 m³/dzień**.
- Ryzyko compliance: kontrola WIOŚ → kary, zatrzymanie produkcji.
- Plan: aplikacja o nowe pozwolenie pod 150 tys. szt./tydzień + 1000 m³ wody.
- Projekt technologiczny Rybki = załącznik do wniosku o pozwolenie.

### 13.4 ZUS S3 — dotacja BHP

**Wniosek o dofinansowanie BHP** (do 345 000 PLN):
- Wózki widłowe.
- Dźwigniki produkcyjne.
- Szafy chemiczne.
- Modernizacja BHP hali.

### 13.5 Wody, ścieki

- **Flotator** — od 5 lat. Pozwala uboju do 100 tys. szt./d.
- **Plan:** oczyszczalnia ścieków (~1,5 mln PLN, w pakiecie ARiMR).
- **Gospodarka wodną:** szara woda, recykling (~1 mln PLN ARiMR).

### 13.6 Compliance weterynaryjne

- **IRZplus / ARiMR** — codzienne ZURD (Zgłoszenie Uboju w Rzeźni Drobiu). Format: jedno zgłoszenie ZURD = jeden transport (jedna specyfikacja). Eksport CSV z ZPSP do portalu ARiMR. Próba pełnej automatyzacji przez API zablokowana („Access denied" — konto bez uprawnień API).
- **GIW** — kontrola weterynaryjna. Lekarz powiatowy obecny codziennie.
- **WIOŚ** — środowiskowa.
- **Sanepid** — żywnościowa.

### 13.7 Ubezpieczenia (PZU, lutry 2026)

Polisy:
- **OC** — odpowiedzialność cywilna.
- **Mienie** — pożar, kradzież, zdarzenia losowe.
- **Maszyny** — awaria, eksplozja.
- **Cargo** — transport.
- **Elektronika** — komputery, infrastruktura IT (niedoubezpieczona — Sergiusz zidentyfikował lukę).
- **Hermes** — ubezpieczenie należności (limit kupiecki na każdego klienta).

Audyt PZU (luty 2026): rekomendacje BHP, dodatkowe drzwi (Biomar oferta), termowizja w marcu, przegląd transformatorów.

---

## 14. SYSTEM ZPSP — SZCZEGÓŁOWY OPIS TECHNICZNY

### 14.1 Co to jest ZPSP

**ZPSP** = **Zajebisty Program Sergiusza Piórkowskiego**.

Autorski system ERP zaprojektowany i napisany od podstaw przez Sergiusza w czasie wolnym (ostatnie 5 lat ciągłego rozwoju). Stanowi **kręgosłup operacyjny całej firmy**. Cała sprzedaż, magazyn, transport, hodowcy, faktury, raporty — wszystko działa na ZPSP.

**Wartość rynkowa:** szacowana na **1–3 mln PLN** (5 lat × pełnoetatowe wynagrodzenie programisty + DevExpress + integracje).

**Miejsce w strukturze:** Sergiusz jest **JEDYNYM** programistą i **JEDYNYM** posiadaczem wiedzy o tym systemie. Klauzula key man w umowie spółki to bezpośrednia konsekwencja.

### 14.2 Stack technologiczny

| Warstwa | Technologia |
|---|---|
| Język | **C# 12 / .NET 6+** |
| UI | **WPF + DevExpress** (DXGrid, DXChart, ThemedWindow, RibbonUI) |
| Baza danych | **SQL Server 2022** (LibraNet @ 192.168.0.109) |
| Linked server | **Sage Symfonia Handel** @ 192.168.0.112 |
| ORM / dostęp | ADO.NET, raw SQL (procedury składowane, widoki) |
| Solution name | **Kalendarz1** |
| IDE | Visual Studio 2022 + Claude Code CLI |
| Repo | GitHub Desktop (branche, commits, merges) |
| NuGet | DevExpress, MathNet, Newtonsoft.Json, RestSharp, BarcodeStandard |
| Architektura | Desktopowa (klient SQL → serwer SQL), VPN dla zdalnego dostępu (handlowcy) |

### 14.3 Infrastruktura serwerowa

```
+--------------------------+      +--------------------------+      +-------------------+
|  SERWER FUJITSU          |      |  SERWER HP               |      |  3-CI KOMPUTER    |
|  192.168.0.112           |      |  192.168.0.109           |      |  bramki/wagi      |
|  Sage Symfonia           |      |  ZPSP + LibraNet         |      |                   |
|  ├── Handel              |      |  ├── 277 tabel           |      |                   |
|  ├── FK                  |      |  ├── 4,5 mln rekordów    |      |                   |
|  ├── Kadry+Płace         |      |  └── linked server       |      |                   |
|  └── KSeF Plus           |      |      do .112             |      |                   |
+--------------------------+      +--------------------------+      +-------------------+
        ^                                  ^                                ^
        |                                  |                                |
        |       +--------------------------|---------------------+         |
        +-------|  Stacja robocza Sera    |                     |---------+
                |  (Visual Studio,        |                     |
                |  GitHub, Claude CLI)    |                     |
                +-------------------------+                     |
                                                                |
         Handlowcy (VPN) ------------------------------ZPSP UI ---+
```

### 14.4 Główne moduły ZPSP (~30+)

#### Sprzedaż i handel
- `WidokZamowienia` — panel zamówień, real-time dla 4–5 handlowców.
- `HandlowiecDashboard` — Top 10 klientów, udział %, analiza cen, porównanie okresów (rok/rok).
- `KreatorOfert` — pobieranie odbiorców z CRM lub Symfonii, wybór towarów, parametry, generowanie PDF, automatyczny e-mail z załącznikiem, archiwum, ranking.
- `PanelPlatnosci` — dashboard należności (terminowe, przeterminowane, przekroczone limity), pobieranie z Symfonii.
- `WidokFaktury` — pobieranie faktur z Symfonii, podgląd, statystyki sprzedażowe.
- `Reklamacje` — handlowiec zgłasza, dodaje rodzaj/priorytet/partię/zdjęcie, status workflow.
- `Pojemniki` — przyjmowanie/wydawanie, integracja z Symfonią, statystyki per odbiorca.
- `KartotekaOdbiorcow` (`KartotekaOdbiorcyDane` 22 kol., `KartotekaOdbiorcyKontakty`, `KartotekaOdbiorcyNotatki`, `KartotekaScoring`, `OdbiorcyCertyfikaty`, `OdbiorcyDaneFinansowe`, `OdbiorcyTransport` 21 kol.) — pełna kartoteka per handlowiec.
- `WidokKalendarzZamowien` — kalendarzowy widok zamówień z kolorowym oznaczeniem statusów.

#### Zakup żywca i hodowcy
- `WidokHodowcy` — baza hodowców z pełnymi danymi.
- `WstawieniaKurczakow` (2453+ wpisy) — cykl wstawień, kalendarz z kolorami.
- `WidokPartie` — partie żywca, kalkulacja terminu uboju.
- `RankingHodowcow` — jakość, komunikacja, elastyczność, marża per partia.
- `CenyDzienne` — wpisywane przez pracownika zakupów (CenaTuszki, CenaRolnicza, CenaMinisterialna).

#### Produkcja
- `WidokProdukcja` — dashboard klas wagowych A/B, planowane krojenie.
- **`ModulKrojenie` — kalkulator decyzji dziennej (krojenie vs tuszka vs mrożenie). Patrz sekcja 14A.**
- `AnalizaTygodniowa` — produkcja tygodniowa, prognoza uboju.
- `BilansProdukcji` — zamówienia vs produkcja, niesprzedane.
- `SpecyfikacjaSurowca` — przyjęcie żywca, kolumna „Padłe" + sumaryczne kolumny CH/ZM/NW (konfiskaty z linii).
- Rejestracja produkcji w czasie rzeczywistym (z wag).

#### Magazyn
- `WidokMagazyn` — stany w czasie rzeczywistym (`State0E`).
- **`PanelMagazyniera` — UI dla magazynierów: lista zamówień gotowych do wydania, kompletacja wzrokowa, przycisk „Wyda", auto-generacja WZ. NIE używa skanowania (w 2026), to zostanie dodane w 2027 z RFID.**
- `EtykietyZbiorcze` (36k+) — drukowanie GS1-128 na etykietach (informacyjnie, NIE używane w workflow wydania).
- `KodyKreskowePalet` — wdrożenie pełnego śledzenia (planowane 2027 pod dotację).
- FIFO/FEFO logiczna kolejność.

#### Transport (TMS)
- `WidokFlota` — mapa pojazdów, status real-time.
- `WidokMatrycaWPF` — import planu transportu od AVILOG (parser PDF/Excel).
- `TDriver`, `TVehicle`, `TCarTrailer`, `TTrip`, `TTripLoad`, `TTripLog`, `TTripTelemetry`, `TransportTrip`, `TransportTripOrder` — pełen TMS.
- Widoki: `vTTripFill`, `vTTripLoadSummary`, `vTTripSpaceFill`.
- Przypisywanie kierowców do tras, integracja z WebFleet.
- Rozliczenia kilometrowe i delegacje (planowana automatyzacja, dziś Excel).

#### CRM
- `CallReminderConfig` (39 kol.) — przypomnienia o telefonach.
- `HandlowcyCRM`, `WlascicieleOdbiorcow`, `Zadania`.
- System priorytetów PKD.

#### Operatorzy i uprawnienia
- `Operators` (56 wpisów, ~20 aktywnych) — bitmaskowy system uprawnień.
- `Dostep` (legacy) — stara tabela ProNova.
- `UserPermissions` (nowa) — uprawnienia name-based.
- `UprawnieniaTerytorialne` — operator widzi tylko swoje województwa/powiaty.

#### Dashboard CEO TV
- 10 widoków rotujących na TV w biurze:
  1. Sprzedaż dziś vs średnia.
  2. Top 5 klientów dziś.
  3. Stany mrożni.
  4. Kalendarz wstawień.
  5. Wskaźniki produkcji.
  6. Stan floty (mapka WebFleet).
  7. Reklamacje otwarte.
  8. Należności przeterminowane.
  9. Pogoda + alerty HPAI.
  10. Notatki dnia.

#### KSeF
- Konfiguracja Symfonia KSeF Plus (Optimum, 20 000 operacji/rok, 3 400 PLN).
- Konwerter FA(2) → FA(3) dla Symfonia 2025.2 (do czasu 2026 z natywnym FA3).
- Obsługa 30–50 faktur/dziennie.

#### IRZplus/ARiMR (ZURD)
- Wysyłka codzienna ZURD: hodowca, NumIRZ, sztuki, waga, padłe, data uboju.
- Próba REST API → odmowa (Access denied, brak uprawnień API).
- Aktualne podejście: eksport CSV → import w portalu IRZplus.

#### MarketIntelligence
- Brave Search API + OpenAI GPT-4o.
- Monitoruje newsy: ceny rynkowe, konkurencja (Cedrob, SuperDrob, Drosed), HPAI.

#### Inne
- `IntegracjaSymfonii` — pobieranie kontrahentów z `[Handel].[SSCommon].[STContractors]`, opiekun (handlowiec), `CreditLimit`.
- `IntegracjaWebFleet` — `WebfleetService` klasa C# z `WEBFLEET.connect` API.
- `IntegracjaUNICARD` — karty RCP czasu pracy.
- `IntegracjaHikvision` — RTSP z kamer hali.

### 14.5 Tabele w bazie LibraNet (kluczowe)

| Kategoria | Tabele kluczowe |
|---|---|
| Sprzedaż | `Zamowienia`, `ZamowieniaTowary`, `ZamowieniaMieso`, `ZamowieniaMiesoTowar`, `ZamowieniaStatus` |
| Klienci | `Odbiorcy` (28 kol.), `KartotekaOdbiorcyDane` (22 kol.), `KartotekaOdbiorcyKontakty`, `KartotekaScoring`, `OdbiorcyCertyfikaty`, `OdbiorcyDaneFinansowe`, `OdbiorcyTransport` (21 kol.), `Katalog` (89 kol.), `STContractors` (linked Symfonia) |
| Produkty | `Article`, `Articles`, `Towary` |
| Hodowcy | `WidokHodowcy`, `WstawieniaKurczakow` (2453+), `Hodowcy` |
| Produkcja | `Specyfikacje`, `PartiaProdukcyjna` |
| Magazyn | `State0E` (mroźnia, partie historyczne), `EtykietyZbiorcze` (36k+) |
| Transport | `Driver`, `CarTrailer`, `Trucks`, `TDriver`, `TVehicle`, `TTrip`, `TTripLoad`, `TTripLog`, `TTripTelemetry`, `TransportTrip` |
| Faktury | `Faktury` (z Symfonii), `FakturyKorekta`, `WZ` |
| Pojemniki | `Pojemniki`, `PojemnikiRuch` |
| CRM | `CallReminderConfig` (39 kol.), `HandlowcyCRM`, `WlascicieleOdbiorcow`, `Zadania` |
| Uprawnienia | `Operators`, `Dostep` (legacy), `UserPermissions`, `UprawnieniaTerytorialne`, `MapowanieHandlowcow`, `UserHandlowcy` |
| Reklamacje | `Reklamacje`, `ReklamacjeStatusy` |

**Łącznie:** 277 tabel, ~4,5 mln rekordów.

### 14.6 Legacy systemy (do migracji)

- **Raporty.exe** (Delphi, autorska aplikacja dziadka, ProNova) — prosta kartoteka, bitmaska uprawnień.
- **IT9000PC.exe** (Delphi, wagi) — komunikacja ze starymi wagami SQL.

ZPSP zastępuje oba — proces migracji w toku.

### 14.7 Skrypty AmBasic w Symfonii Handel

Sergiusz pisze również skrypty `.sc` w **AmBasic** (wbudowany język Symfonii Handel) — np. `WZ.sc` do eksportu faktur:
- Bierze zamówienia z LibraNet (`ZamowieniaMieso` + `ZamowieniaMiesoTowar`) na wybrany dzień uboju.
- Pokazuje listę 20 pozycji, użytkownik wybiera „W" (wszystkie) lub konkretne ID.
- Tworzy FVS w buforze Symfonii i zaznacza zamówienie jako `CzyZafakturowane = 1`.

Komponenty AmBasic: `form`, `ExecForm`, `button`, `Datedit`, `IORec`, `dokFvs.beginSection("daneKh")`, `ImportSP`, `getAdoConnection`, `Dispatch`, `createObject("ADODB.Connection")`, `noOutput`.

### 14.8 Co ZPSP DAŁO firmie

- Wzrost obrotów 258 → 318 mln PLN (+23%).
- Zysk netto x2,3 (3 → 7 mln).
- Eliminacja „karteczek" (w trakcie).
- Centralna kontrola sprzedaży, magazynu, transportu.
- Compliance KSeF, IRZplus.
- Możliwość audytu kierowców (~36 tys. PLN identyfikowanych nadpłat).
- Profesjonalny image dla banków i partnerów (BNP, Pekao, Magik).

### 14.9 Ryzyko key man

ZPSP rozumie tylko Sergiusz. Klauzula w umowie spółki: w razie jego odejścia (rezygnacja, odwołanie, śmierć) — firma ma 12 miesięcy na znalezienie zastępstwa lub przejście na zewnętrzne rozwiązanie ERP (np. Comarch, SAP, Symfonia ERP). To jest kluczowy argument za pozycją prezesa zarządu.

---

## 14A. MODUŁ ZPSP „KROJENIE" — KALKULATOR DECYZJI DZIENNEJ

Jeden z najczęściej używanych modułów operacyjnych ZPSP. Odpowiada na pytanie zadawane CODZIENNIE o 13:00:

> *„Mamy nadwyżkę X kg tuszki. Co zrobić — sprzedać tuszkę, pokroić na elementy, czy zamrozić?"*

### 14A.1 Co liczy moduł

Moduł bierze:
- **Wagę nadwyżki** (przykład z ekranu: 19 800 kg).
- **Bieżącą cenę tuszki rynkową** (przykład: 7,33 PLN/kg → wartość 145 134 zł).
- **Bieżące ceny elementów** (filet, ćwiartka, skrzydło, korpus itd.).
- **Procentowy podział tuszki na elementy** (uzysk).
- **Stawki kosztów krojenia i mrożenia** (per kg towaru).
- **Cenę sprzedaży mrożonego towaru** (zwykle z dyskontem).

I porównuje **3 scenariusze**:
1. **Sprzedać tuszkę as-is** → baseline.
2. **Pokroić → sprzedać elementy świeże** → baseline + zysk z elementów - koszt krojenia.
3. **Pokroić → zamrozić → sprzedać taniej** → baseline + zysk z elementów - koszt krojenia - koszt mrożenia - zaniżenie ceny.

### 14A.2 Przykładowe wyliczenie z ekranu (19 800 kg, cena tuszki 7,33 zł)

**SCENARIUSZ 1 — sprzedaż tuszki:**

| Pozycja | Wartość |
|---|---|
| 19 800 kg × 7,33 zł | **145 134 zł** |

**SCENARIUSZ 2 — krojenie + sprzedaż elementów świeżych:**

Podział tuszki na elementy i ich wycena:

| Element | % uzysku | KG | Cena/kg | Wartość |
|---|---:|---:|---:|---:|
| Filet I | 29,5% | 5 841,00 | 16,26 zł | 94 974,66 zł |
| Filet II | 1,9% | 376,20 | 11,12 zł | 4 183,34 zł |
| Ćwiartka I | 33,4% | 6 613,20 | 5,46 zł | 36 108,07 zł |
| Ćwiartka II | 2,0% | 396,00 | 3,50 zł | 1 386,00 zł |
| Skrzydło I | 8,7% | 1 722,60 | 4,30 zł | 7 407,18 zł |
| Skrzydło II | 1,0% | 198,00 | 3,98 zł | 788,04 zł |
| Korpus | 22,7% | 4 494,60 | 0,66 zł | 2 966,44 zł |
| Pozostałe | 0,8% | 158,40 | 0,00 zł | 0,00 zł |
| **RAZEM ELEMENTY** | **100,0%** | **19 800,00** | **7,47 zł** | **147 813,73 zł** |
| Zysk vs tuszka (BEZ kosztów krojenia) | | | +0,14 zł/kg | **+2 679,73 zł** |
| **Koszt krojenia** | | | -0,59 zł/kg | **-11 647,06 zł** |
| **Wartość elementów PO kosztach krojenia** | | | 6,88 zł/kg | **136 166,67 zł** |
| **Różnica vs sprzedaż tuszki** | | | -0,45 zł/kg | **-8 967,33 zł** *(w tym konkretnym układzie cen)* |

**Uwaga interpretacyjna:** w pokazanym przykładzie krojenie świeżych daje stratę -0,45 zł/kg vs sprzedaż tuszki, **ale to zależy od bieżących cen elementów**. Inaczej liczone w innym dniu (np. wyższa cena fileta) → wynik dodatni. Dlatego moduł liczy się CODZIENNIE z bieżącymi cenami.

**SCENARIUSZ 3 — krojenie + mrożenie + sprzedaż taniej:**

Koszty mrożenia (na 19 800 kg towaru):

| Pozycja | Stawka | Kwota |
|---|---|---:|
| Rozważanie 15 kg towaru | 1,50 zł/kg | 1 980,00 zł |
| Folia (za każde 10 kg) | 0,38 zł | 752,40 zł |
| Sznurek (za każde 3 000 kg) | 24,00 zł | 158,40 zł |
| Paleta drewniana (za każde 750 kg) | 13,20 zł | 348,48 zł |
| Karton (za każde 10 kg) | 3,00 zł | 5 940,00 zł |
| Energia (za każde 1 kg) | 0,30 zł | 5 940,00 zł |
| **RAZEM KOSZT MROŻENIA** | **0,76 zł/kg** | **15 119,28 zł** |

Plus zaniżenie ceny przy sprzedaży mrożonego (-1,10 zł/kg, czyli -21 788,53 zł).

| Pozycja | Wartość |
|---|---:|
| Wartość elementów po kosztach krojenia (z scenariusza 2) | 136 166,67 zł |
| - Koszt mrożenia | -15 119,28 zł |
| - Koszt zaniżenia ceny (mrożone tańsze o 1,10 zł/kg) | -21 788,53 zł |
| **Wartość elementów mrożonych po zniżce** | **99 258,86 zł** *(5,01 zł/kg)* |
| **STRATA vs sprzedaż tuszki** | **-45 875,14 zł** *(-2,32 zł/kg)* |

### 14A.3 Wnioski operacyjne (BARDZO WAŻNE)

1. **Sprzedać świeże > wszystko inne.** Świeża tuszka jest niemal zawsze najlepsza (najniższe koszty pośrednie).
2. **Krojenie ma sens TYLKO** gdy ceny elementów są na tyle wyższe od ceny tuszki, by pokryć koszt krojenia (~0,59 zł/kg). To zależy od dnia.
3. **Mrożenie = OSTATNIA OPCJA.** Daje strukturalnie kilkadziesiąt tysięcy strat na każdej operacji 20-tonowej. Robi się to tylko gdy:
   - Brak odbiorcy na świeże (kryzys rynkowy, anulacje).
   - Konieczność biosekuracji (strefa HPAI, towar nie do wysyłki).
   - Strategiczne mrożenie pod eksport (Ania, mrożonki dla pośredników).
4. **Decyzja codzienna 13:00** podejmowana przez Justynę + handlowców + Sergiusza (gdy jest) na podstawie tego modułu.
5. **Strata mrożenia -18% wartości** (heurystyka z ZAŁĄCZNIKA A) jest **SPÓJNA z modułem** — różnica 145 134 → 99 258 to ~32% straty na 19 800 kg, czyli średnio ok. 2,32 zł/kg na tuszce wartej 7,33 zł.

### 14A.4 Stałe parametry modułu (per kg towaru)

| Parametr | Wartość | Komentarz |
|---|---|---|
| Koszt krojenia | ~0,59 zł/kg | praca, energia, narzędzia |
| Koszt rozważania (mrożenie) | 1,50 zł / 15 kg towaru | praca |
| Folia mroźnicza | 0,38 zł / 10 kg | materiał |
| Sznurek | 24,00 zł / 3 000 kg | materiał |
| Paleta drewniana | 13,20 zł / 750 kg | materiał |
| Karton | 3,00 zł / 10 kg | materiał |
| Energia (mrożenie) | 0,30 zł / 1 kg | prąd |
| **Razem koszt mrożenia** | **~0,76 zł/kg** | suma |
| Typowe zaniżenie ceny mrożonego | ~1,10 zł/kg | rynek |

**Te stawki są aktualizowane przez Sergiusza w ZPSP** — gdy rosną ceny prądu, opakowań, robocizny, parametry należy aktualizować, inaczej moduł da fałszywe wskazanie.

### 14A.5 Wizualne elementy modułu

Moduł ma czytelny interfejs WPF:
- **Lewy panel** — tabela podziału tuszki (% / kg / cena / wartość), kalkulator kosztu krojenia, kalkulator kosztu mrożenia (kafelki turkusowe per pozycja).
- **Prawy panel** — wynikowe pola z dużymi liczbami (zielone = zysk, czerwone = strata) i wskaźnikami per kg.
- **Górny pasek** — przycisk „Ceny" (aktualizacja cen rynkowych), wybór dnia tygodnia.
- Klocki ze zdjęciami (tuszka, elementy, paleta, karton, energia) — żeby było intuicyjne dla pracowników mniej technicznych.

### 14A.6 Co można w nim ulepszyć (backlog)

- **Auto-aktualizacja cen elementów z modułu sprzedaży** (zamiast ręcznego wpisywania).
- **Symulacje what-if** — co jeśli cena tuszki spadnie o 0,50 zł/kg? Co jeśli filet pójdzie do 18 zł?
- **Historia decyzji** — co było wybierane, jaki realny wynik finansowy.
- **Powiadomienie do Marcina i Pani Joli** o decyzji (Teams / SMS).
- **Integracja z planem produkcji** — automatyczna decyzja o krojeniu klasy B vs sprzedaży tuszki A.

---

## 14B. MODUŁ ZPSP „SPECYFIKACJA SUROWCA" — EWIDENCJA UBOJU

### 14B.1 Cel

Centralna tabela ewidencji ubitej partii. Każda dostawa żywca od hodowcy = jedna „specyfikacja surowca". Wprowadzana po zakończeniu uboju partii.

### 14B.2 Struktura

Wybrane kolumny tabeli:

| Kolumna | Opis |
|---|---|
| Data uboju | Dzień ubicia partii |
| Hodowca | ID + nazwa hodowcy z `WidokHodowcy` |
| Numer IRZ | Identyfikator z systemu IRZplus (np. „068736945-001") |
| Sztuki żywe (przyjęte) | Ile sztuk dotarło do zakładu |
| Waga żywa | KG przyjęte (waga samochodowa) |
| **Padłe** | **Sztuki, które padły w transporcie / podczas zawieszania na linię** — wpisywane ręcznie po zakończonym przyjęciu |
| **CH** | Konfiskata kategoria CH (chłonność / cellulit / inna kategoria wewnętrzna) |
| **ZM** | Konfiskata kategoria ZM (zmiany chorobowe / inne wady) |
| **NW** | Konfiskata kategoria NW (nadwaga / niewłaściwy / inna) |
| Suma konfiskat | `= CH + ZM + NW` (wyświetlana automatycznie) |
| Sztuki ubite | `= Sztuki żywe - Padłe - Konfiskaty` |
| Waga tuszki | KG po uboju (z wagi linii) |
| Uzysk % | `Waga tuszki / Waga żywa × 100` (cel ~78%) |
| Klasa A / Klasa B | Procentowy podział |
| Komentarz weterynarza | Notatka lekarza powiatowego |

### 14B.3 Workflow

1. **Wstawienie żywca** — auto AVILOG przyjeżdża rano, dane wpisuje rejestracja (waga samochodowa).
2. **Ubój** — partia idzie na linię, padłe są zliczane w trakcie zawieszania.
3. **Konfiskaty** — weterynarz powiatowy zaznacza odrzucone tuszki (kategoria CH/ZM/NW) na linii. Zliczone na koniec zmiany.
4. **Wpisanie do ZPSP** — Specyfikacja Surowca uzupełniana przez wyznaczonego pracownika produkcji (Justyna / Klaudia / Anna Majczak).
5. **Eksport do IRZplus** — z tej tabeli generuje się ZURD (Zgłoszenie Uboju w Rzeźni Drobiu).
6. **Analiza** — Sergiusz / Justyna używają tej tabeli do oceny:
   - Jakość partii hodowcy (% padłych, % konfiskat).
   - Uzysk per dostawca (kluczowy wskaźnik marżowy).
   - Trendy chorobowe (jeśli wzrost CH lub ZM → sygnał do weterynarza).

### 14B.4 Lokalizacja w UI ZPSP

**Menu → Specyfikacja Surowca → [wybór dnia] → tabela partii.**

Kolumny `Padłe`, `CH`, `ZM`, `NW` widoczne natywnie w gridzie DevExpress — sortowanie, filtrowanie, eksport do Excel.

### 14B.5 Powiązania

- **`WidokHodowcy`** — przypisanie hodowcy.
- **`WstawieniaKurczakow`** — pełen cykl od wstawienia (35–45 dni temu) do uboju.
- **`Padłe + Konfiskaty` → `RankingHodowcow`** — automatyczna ocena jakościowa per dostawca.
- **Eksport CSV → IRZplus / ARiMR** (ZURD) — codziennie.

### 14B.6 Statystyki referencyjne (norma branżowa)

- **Padłe w transporcie:** typowo 0,1–0,5% (zależy od pogody, długości transportu, kondycji partii).
- **Konfiskaty z linii:** typowo 0,5–1,5% (CH+ZM+NW razem). Wzrost do >2% = sygnał alarmowy → weterynarz, hodowca.
- **Uzysk % żywiec→tuszka:** średnio 78%, sezonowo 76–80%.

---



## 15. INFRASTRUKTURA IT I INTEGRACJE

### 15.1 Sieć i serwery

- **Siec lokalna (LAN):** 192.168.0.x.
- **Serwer Fujitsu** @ 192.168.0.112 — Sage Symfonia (Handel + FK + Kadry+Płace + KSeF Plus).
- **Serwer HP** @ 192.168.0.109 — ZPSP + LibraNet (SQL Server 2022).
- **3-ci komputer** — bramki wejściowe + UNICARD RCP.
- **Stacje robocze:** ~25 w sieci. Marka: różne, Dell Precision T3610 itd.
- **Wi-Fi i LAN** w zakładzie — zarządzane przez Sergiusza.

### 15.2 VPN

Handlowcy łączą się zdalnie przez VPN do ZPSP. Plan: migracja na cloud / bezpieczniejsze rozwiązanie.

### 15.3 Sage Symfonia

- **Wersja:** Symfonia Handel **2025.2** (wersja modułu 25.20.1.0). Aktualizacja do 2026 (FA(3) natywnie) — w planach.
- **Numery seryjne:** CMF-101972, H50-103288 (i in.).
- **KSeF Plus** — pakiet Optimum (20 000 operacji, 3400 PLN/rok).
- **Linked server** z LibraNet (192.168.0.109 ↔ 192.168.0.112).
- **Tabele kluczowe Symfonii:** `[Handel].[SSCommon].[STContractors]` (kontrahenci), `ContractorClassification.CDim_Handlowiec_Val` (przypisanie handlowca), `CreditLimit`.

### 15.4 KSeF (Krajowy System e-Faktur)

- Od 1 lutego 2026 schemat **FA(3)** (KSeF 2.0).
- Symfonia 2025.2 generuje przestarzały **FA(2)** → ZPSP używa Symfonia KSeF Plus (chmura konwertuje na FA(3)) lub własnego konwertera FA(2)→FA(3) (gotowy).
- **Wolumen:** 30–50 faktur/dziennie.
- **Plan:** integracja bezpośrednia ZPSP → KSeF API (REST), z OAuth, zarządzaniem UPO (Urzędowe Poświadczenia Odbioru).

### 15.5 IRZplus / ARiMR

- **Wymóg:** codzienne ZURD (Zgłoszenie Uboju w Rzeźni Drobiu).
- **Format:** jedno zgłoszenie ZURD = jeden transport (jedna specyfikacja).
- **Zawartość:** hodowca, NumIRZ (np. „068736945-001"), sztuki, waga, padłe, data uboju, „Przyjęte z działalności" (np. „068736945-001-001").
- **Próba REST API → Access denied** (konto bez uprawnień API; wymaga umowy z ARiMR).
- **Aktualnie:** eksport CSV z ZPSP → import w portalu IRZplus.

### 15.6 WebFleet (TomTom Telematics)

- API: **WEBFLEET.connect** + **DRIVE.connect** (tachograf).
- Endpoint: `https://csv.webfleet.com/extern`.
- Autoryzacja: account + username + password + API key.
- Wrapper .NET: `scottyearsley/tomtom-webfleetconnect` (GitHub) — Sergiusz nie używa, zrobił własny `WebfleetService` w C#.
- **Klucz API uzyskany** (luty 2026, kontakt Robert Kuczyński, przedstawiciel WebFleet).

Plan wdrożenia (4 fazy, sekcja 11.8).

### 15.7 UNICARD (RCP)

System kart wejściowych do rejestracji czasu pracy. Integracja z ZPSP — kto kiedy wszedł, kto na hali, kto na agencji. Każdy pracownik ma kartę, niektóre grupy (kierowcy, łapacze) — bez kart.

### 15.8 Hikvision RTSP

Kamery monitoringu (hala produkcyjna, magazyn). Justyna używa do nadzoru („patrzę na kamerze, nie muszę zejść na halę").

### 15.9 Dystrybutor paliwa Swimmer

Wewnętrzny dystrybutor paliwa. Ilona autoryzuje tankowania. **Kierowcy nie widzą licznika** (problem). Audyt Locura: rozjazd 8000 l w 72 dni.

Plan: ZPSP jako pośrednik — każde tankowanie rejestrowane automatycznie z identyfikacją kierowcy + pojazdu + KM + litrów. Brak edycji bez śladu.

### 15.10 Cent (system celno-skarbowy)

Obsługiwany przez Ilonę. Rozliczenia podatkowe transportu (środki transportu, deklaracje ministerialne).

### 15.11 Domena i hosting

- **Domena:** piorkowscy.com.pl — zarejestrowana w **Domena.pl** (Bydgoszcz).
- **Hosting:** dhosting.pl. Login: `iebei7_piorkows`.
- **Konto:** **NA NAZWISKO BARTOSZA ULĘŻAŁKI** (zewnętrzny IT, firma Webemo).
- **Webmail:** dpoczta.pl (POP3 → Mozilla Thunderbird).
- **Plan:** odzyskanie kontroli, migracja na Microsoft 365 + Teams (~860 PLN/mies. za 20 osób).

### 15.12 Microsoft 365 + Teams (plan)

Migracja:
- Pakiet: Microsoft 365 Business Standard (ok. 60 PLN/użytkownik/mies.).
- Poczta `@piorkowscy.pl` przez M365.
- Teams jako główne narzędzie komunikacji (zamiast WhatsApp grup).
- Kanały: `#sprzedaz`, `#produkcja`, `#logistyka`, `#jakosc`, `#zarzad`.
- SharePoint dla dokumentów.
- Bot Framework SDK w C# — pełna interakcja Teams z ZPSP (alerty produkcyjne, statusy zamówień, raporty).
- Incoming Webhooks → automatyczne alerty z ZPSP do Teams.

### 15.13 Fireflies.ai

Automatyczna transkrypcja spotkań (Teams, telefon). Integracja planowana z Teams. Sergiusz wrzuca transkrypcje spotkań do projektów Claude jako kontekst.

### 15.14 Inne planowane integracje

- **SMSAPI.pl** — powiadomienia SMS dla hodowców i klientów (ETA dostawy, zmiany planu).
- **OPC-UA** — bezpośrednia integracja wag z ZPSP (real-time).
- **Bank API** (PKO, BNP, Pekao via Nasz Bank/MAP Solutions) — pobieranie wyciągów, weryfikacja przelewów.
- **Microsoft Graph API** — Teams, Outlook, SharePoint, OneDrive.
- **Brave Search + GPT-4o** — MarketIntelligence (już zintegrowane).

### 15.15 Backup i bezpieczeństwo

- SQL Server backup codzienny.
- Plan: zewnętrzne backupy (chmura).
- VPN dla zdalnych użytkowników.
- Firewall, kontrola dostępu (UserPermissions w ZPSP, AD na Symfonii).

### 15.16 Telefony firmowe

Plan zakupu nowych. Aktualnie analiza zużycia (Paulina, Teresa) na podstawie faktur Orange.

### 15.17 Stacja transformatorowa

Oferta przeglądu od Gąsiorowskiego: 16 950 PLN. Agregat zapasowy. Wyłącznik awaryjny. Plan zintegrowany z modernizacją chłodnictwa.

### 15.18 Letniak (sprzęt sieciowy w domu)

Domowa siec Sera (NVR + switch + AP). Tematy: szafa rack 19", grzałka antykondensacyjna, dławiki PG, UPS Eaton Ellipse PRO 850, korytka PVC. Zwykła praca DIY.

---

## 16. PROJEKTY INWESTYCYJNE 2026

### 16.1 Łączny plan inwestycji 2026–2027

| Inwestycja | Kwota | Źródło finansowania | Termin |
|---|---|---|---|
| **Chłodnictwo Etap 1** (modernizacja, glikol R1234ze) | ~2,4–2,8 mln PLN | Pożyczka leasingowa (Pekao L. / Santander L. / mBank / Millennium) | Q2 2026 |
| **Chłodnictwo Etap 2** (agregat 1000 kW, wieża, przeróbka 11 chłodnic) | ~2,5–3,0 mln PLN | Dotacja ARiMR (50%) + leasing | Q3 2026 |
| **Patroszarka Meyn Maestro** | ~4–5 mln PLN | Dotacja ARiMR (50%) + leasing | Wrzesień 2026 |
| **ZPSP/RFID** (rozbudowa traceability, tablety na hali) | ~1–2 mln PLN | Dotacja ARiMR | 2026–2027 |
| **Fotowoltaika + wiatraki + magazyn energii** | ~2–3 mln PLN | Dotacja ARiMR (Energia Plus NFOŚiGW jako plan B) | 2027 |
| **Oczyszczalnia ścieków** | ~1–1,5 mln PLN | Dotacja ARiMR | 2027 |
| **Gospodarka wodna (szara woda, recykling)** | ~1 mln PLN | Dotacja ARiMR | 2027 |
| **Biogazownia mikro** | ~1 mln PLN | Dotacja ARiMR | 2027 |
| **HPP** (High Pressure Processing) | ~2 mln PLN | Dotacja ARiMR | 2027 |
| **BHP — wózki, dźwigniki, szafy** (ZUS S3) | ~345 tys. PLN | Dotacja ZUS S3 | 2026 |
| **BRC v9 + IFS — projekt + dokumentacja + obsługa** | ~133 tys. PLN + obsługa miesięczna | Środki własne | 2026–2027 |
| **Stacja transformatorowa, agregat** | ~17 tys. PLN | Środki własne | 2026 |
| **Mikroinwestycje** (piec kotłowni, naprawy) | bieżące | Środki własne | bieżące |
| **ŁĄCZNIE PROJEKT** | **~18–20 mln PLN** | Mix dotacja + leasing + środki własne | **2026–2029** |

**Cel:** ~10 mln PLN dotacji ARiMR (50% z 20 mln inwestycji).

### 16.2 Chłodnictwo — szczegóły

**Stan obecny:**
- Czynnik: **freon R507A** (wycofywany ekologicznie, drogi).
- Schemat: bezpośrednie odparowywanie freonu w chłodnicach.
- Energia: 100 kWh (przykład).
- Ryzyko: serwis, wycofanie z rynku, awaryjność.

**Stan po modernizacji:**
- Czynnik: **R1234ze** (HFO, niski GWP, ekologiczny) + **glikol propylenowy** w obiegu pośrednim.
- Schemat: chiller ochładza glikol w obiegu zamkniętym → glikol płynie do chłodnic (bezpieczny przy żywności).
- Energia: 60 kWh (-40%).
- Korzyści: oszczędność energii, zgodność z UE, bezpieczeństwo, niższe koszty serwisu.

**Etap 1 (lato 2026):**
- 5 nowych chłodnic glikolowych.
- Chiller wynajmowany od Magika (bezpłatnie do 31.10.2026, potem 4000 PLN/tydzień).
- 6 chłodnic + 2 przeróbki istniejących.
- Cena: ~2,14 mln PLN netto + konstrukcja pod wieżę 86 tys. PLN + dodatkowe = ~2,8 mln brutto.
- Gwarancja: 2 lata. Serwis: max 6h przyjazd, 2 przeglądy rocznie max 10 tys./przegląd.

**Etap 2 (jesień 2026, pod dotację):**
- Agregat wody lodowej **1000 kW** (R1234ze).
- Wieża chłodnicza.
- Przeróbka 11 istniejących chłodnic.
- Cena: ~2,5–3,0 mln PLN netto. Rabat 10% wynegocjowany.

**Dostawca: Magik sp. z o.o.**
- Mariusz Domagała (handlowiec, główny kontakt).
- Piotr Domagała (formalny sygnant umowy).
- Maciej Józefowicz (techniczny).
- Rafał (inżynier konstruktor — wizyta w zakładzie).
- Przedpłata 526 440 PLN wpłacona 13.03.2026.

### 16.3 Patroszarka Meyn Maestro

- Dostawca: **Unimash** (planowany).
- Cena: ~4–5 mln PLN.
- Korzyści: wydajność 7500 → 9000+ szt./h, wyższy uzysk, mniejsze straty, automatyzacja.
- Termin: wrzesień 2026 (pod dotację ARiMR — efekt zachęty, nie kupować przed wnioskiem).

### 16.4 BRC v9 — szczegóły patrz sekcja 13.2.

### 16.5 ZPSP/RFID

- Rozbudowa modułu traceability (kody GS1-128 → RFID).
- Tablety na hali produkcyjnej (przyciski STOP/START linii, przyczyny przestojów).
- Auto-detekcja przestojów z wag (jeśli `In0E` nie rejestruje >5 min → przestój).
- Mierzenie wydajności per pracownik (kg/h fileta itd.) → premia od wydajności.

### 16.6 Fotowoltaika + magazyn energii

- Lokalizacja: dach zakładu.
- Plan: 200–500 kWp.
- Magazyn energii: bateria.
- Wiatraki: kontrowersyjne (zależne od warunków).

### 16.7 Oczyszczalnia ścieków + gospodarka wodna

- Pełna oczyszczalnia własna + recykling szarej wody.
- Cel: zużycie wody z 800 m³/d na 500–600 m³/d.
- Krytyczne dla pozwolenia zintegrowanego.

---

## 17. BANKI, KREDYTY, FINANSOWANIE

### 17.1 Aktualne zobowiązania

| Bank | Produkt | Kwota | Marża | Komentarz |
|---|---|---|---|---|
| **Pekao SA** | Obrotówka „Żubr" | (stan zeruje na bieżąco) | — | Aktywna, nie potrzeba nowej |
| Leasing (firma X) | Ciągnik siodłowy | (rata mies.) | — | Aktywny |
| Leasing (firma Y) | Volvo XC90 | (rata mies.) | — | Auto firmowe |

### 17.2 Pożyczka leasingowa na chłodnictwo Etap 1

**Kwota:** ~2,8 mln PLN brutto (~2,14 mln netto + VAT + dodatkowe).

**Cel warunków (target):**
- **Wkład własny:** max 10% (240 tys. PLN).
- **Okres:** 48–60 miesięcy.
- **Karencja kapitałowa:** 12 miesięcy (przez pierwszy rok tylko odsetki — czekamy na zaliczkę ARiMR).
- **Wcześniejsza spłata:** 0% (bez kary — żebyśmy mogli zamknąć z zaliczki ARiMR po wrześniu 2027).
- **Zabezpieczenie:** **TYLKO** zastaw na finansowanej instalacji + weksel in blanco. **ZERO HIPOTEK** na nieruchomościach.
- **Marża:** WIBOR 1M + 1,3–1,5%.

### 17.3 Oferty banków (stan kwiecień 2026)

| Bank | Doradca | Oferta | Marża | Status |
|---|---|---|---|---|
| **Pekao Leasing** | Andrzej Chrustowicz | Pożyczka leasingowa | WIBOR 1M + 1,3% | Najlepsza marża |
| **Santander Leasing** | Adam Kolasik (697 891 110) | Pożyczka leasingowa | WIBOR 1M + 1,36% | Druga oferta |
| **PKO Leasing** | Justyna Czyżewska-Derach (667 682 692) | Pożyczka leasingowa | — | Czeka |
| **Millennium Leasing** | Konrad Gruszewski / Adam Kolasik (nowy kontakt) | Pożyczka leasingowa | — | W toku |
| **mBank Leasing** | Anna Pastuszak (785 197 751) | Pożyczka leasingowa | — | **ODMOWA** (firma JDG w spadku → finalizacja po przekształceniu) |
| **Millennium** | Konrad Gruszewski | Obrotówka 4 mln WIBOR+0,9% | — | Nie potrzebna |
| **BNP Paribas** | Andrzej Kruszyński | Obrotówka 3 mln WIBOR+1,0% (z hipoteką!) + Inwestycyjny 2 mln WIBOR+1,3% (Ekomax) | — | Hipoteka problematyczna |
| **PKO BP** | Jacek Gosławski | — | — | Nie skontaktowany formalnie |

### 17.4 Decyzja: Ekomax vs ARiMR

**Ekomax BGK** = gwarancja 80% + dopłata 20% (~428 tys. PLN umorzenia z 2,14 mln). Państwowa pomoc.

**Konflikt:** Wiesław (konsultant) mówił że Ekomax + ARiMR = podwójna pomoc publiczna na tę samą inwestycję = ZAKAZ („kryminał"). 

**Stanowisko Grzegorza (doradca):** Ekomax NIE koliduje z ARiMR jeśli środki są na różne środki trwałe w ramach jednej technologii.

**Decyzja końcowa:** **REZYGNACJA z Ekomax.** Powód: zbyt długa procedura (6+ miesięcy) + ryzyko kolizji + brak pilnej potrzeby. Pożyczka leasingowa załatwia sprawę.

### 17.5 Bilans Sergiusza wobec banków

**Atuty:**
- 318 mln obrotu / 7 mln zysku.
- Net Debt/EBITDA = 0,36 (DOSKONAŁY).
- Cash flow 8,5 mln/rok.
- Zero dużych kredytów.
- Stabilność: firma przetrwała COVID, Newcastle Disease, kryzysy rynkowe.
- Dywersyfikacja: 140+ hodowców, 400+ klientów, żaden >5% wolumenu.
- Lokalność: hodowcy 30–40 km od zakładu (przewaga nad Gomakiem).

**Słabości (ale możliwe do uzasadnienia):**
- Płynność 0,86 (poniżej 1,0) — typowa dla drobiarstwa.
- JDG w spadku — przekształcenie w toku, deadline 02.08.2026.
- Key man Sergiusz — pojawia się w analizach bankowych.

### 17.6 Faktoring

W rozważaniach. Należności wzrosły o 5,5 mln. Faktoring pomógłby uwolnić zamrożoną gotówkę (17,6 mln należności → ~10 mln szybciej dostępne). Dostawcy: Pekao Faktoring, BNP Faktoring, Coface, Atradius.

### 17.7 Białe certyfikaty URE

Dodatkowe 50–90 tys. PLN za oszczędność energii (chłodnictwo). NIE jest pomocą publiczną → kompatybilne z dotacją ARiMR. Plan: aplikacja po zakończeniu modernizacji.

### 17.8 Kontakty bankowe — relacje

- **Andrzej Kruszyński (BNP Paribas)** — najszczerszy (8/10), zna sentyment do firmy („znałem Teresę i Jurka"), tłumaczy szczegóły.
- **Konrad Gruszewski (Millennium)** — proaktywny.
- **Andrzej Chrustowicz (Pekao Leasing)** — najlepsza marża.
- **Adam Kolasik (Santander/Millennium nowy)** — rzeczowy.
- **Doradca Grzegorz** — niezależny, doświadczony, doradzał ws. EBITDA, Net Debt, key man risk, tactique do banków.

---

## 18. DOTACJA ARIMR — WRZESIEŃ 2026

### 18.1 Program

- **Nazwa:** Plan Strategiczny dla Wspólnej Polityki Rolnej 2023–2027 (PS WPR).
- **Interwencja:** **I.10.7.1** — „Rozwój współpracy w ramach łańcucha wartości".
- **Dotyczy:** przetwórstwa rolnego (drobiu).
- **Termin składania:** **wrzesień 2026**.
- **Kwota:** do **20 mln PLN inwestycji netto**, dotacja **50%** = do **10 mln PLN bezzwrotnie**.
- **OSTATNI nabór** w tej perspektywie UE — następny dopiero **2029**.

### 18.2 Punktacja (kluczowe kryteria)

- **Innowacyjność (100% jeśli nowa spółka)** — argument za nową spółką bez aportu.
- **Traceability** („od pola do stolu") — GS1-128, ZPSP/RFID dadzą punkty.
- **Ochrona środowiska** (>20%) — chłodnictwo R1234ze, fotowoltaika, oczyszczalnia, gospodarka obiegu zamkniętego (Karma-Max).
- **Teren wiejski** — ✅ Koziołki.
- **Brak KPO** (Krajowy Plan Odbudowy) — dodatkowe punkty bo Piórkowscy nie brali KPO.
- **Gospodarka obiegu zamkniętego** — odpady poubojowe → karma w Karma-Max → DODATKOWE punkty.

### 18.3 Mechanizm finansowy (dźwignia 1:4)

```
KROK 1: TERAZ — pożyczka leasingowa Etap 1
   Wkład własny: 240 tys. PLN
   Etap 1 chłodnictwa: 2,4 mln PLN
   Karencja 12 mies.

KROK 2: WRZESIEŃ 2026 — wniosek ARiMR
   Inwestycja całkowita: 20 mln PLN netto
   Dotacja: 10 mln PLN

KROK 3: PO PODPISANIU UMOWY (~luty-marzec 2027) — zaliczka
   ARiMR przelewa 50% dotacji = 5 mln PLN
   PRZED rozpoczęciem inwestycji

KROK 4: SPŁATA POŻYCZKI LEASINGOWEJ
   Z 5 mln zaliczki spłacasz 2,2 mln pożyczki Etap 1
   Zostaje 2,8 mln na rozpoczęcie kolejnych etapów

KROK 5: 2027–2029 — realizacja kolejnych etapów
   Każdy etap → wniosek o płatność → refundacja 50%
```

**Efekt:** za **4–5 mln PLN z kieszeni** robisz inwestycję za **20 mln PLN** (dźwignia 1:4).

### 18.4 Co wkładamy do wniosku

| Inwestycja | Kwota | Kategoria | Priorytet |
|---|---|---|---|
| Patroszarka Meyn Maestro | 4–5 mln | Innowacyjność | NAJWYŻSZY |
| Chłodnictwo Etap 2 | 2,5–3 mln | Środowisko | NAJWYŻSZY |
| ZPSP/RFID | 1–2 mln | Innowacyjność + Śledzenie | WYSOKI |
| Fotowoltaika + wiatraki + magazyn | 2–3 mln | Środowisko | ŚREDNI |
| Oczyszczalnia ścieków | 1–1,5 mln | Środowisko | ŚREDNI |
| Gospodarka wodna | 1 mln | Środowisko | ŚREDNI |
| HPP | 2 mln | Innowacyjność | NISKI |
| Biogazownia mikro | 1 mln | Środowisko | NISKI |
| **RAZEM** | **18–20 mln** | — | **= 9–10 mln dotacji** |

### 18.5 Wymagania formalne

- **Spółka MUSI istnieć przed złożeniem wniosku** (stąd deadline maj-czerwiec 2026 na rejestrację).
- **Brak innej pomocy publicznej** na te same inwestycje (stąd rezygnacja z Ekomax).
- **Trwałość 5 lat** — przez 5 lat po zakończeniu firma działa, maszyny używane, zatrudnienie utrzymane.
- **Efekt zachęty** — NIE MOŻNA zacząć inwestycji (kupić, podpisać umowę, wpisać do dziennika budowy) PRZED złożeniem wniosku.
- **Schemat z Magikiem:** dla Etapu 1 kupujesz teraz, ale BRAK protokołu odbioru → formalnie inwestycja nie jest zakończona → dotacja na Etap 2 OK.

### 18.6 Karma-Max — osobny wniosek

Marcin (jeśli zrestrukturyzuje Karma-Max: Marlena 80% / Marcin 20%) → Karma-Max NIE powiązany kapitałowo z ubojnią (Marcin 77% w ubojni, ale tylko 20% w Karma-Max < 25% próg) → osobny wniosek na halę magazynową (~2 mln, dotacja 50% = 1 mln).

### 18.7 Konsultant: Wiesław Oślewski

- 22 lata doświadczenia w dotacjach ARiMR.
- Prowizja: **2,8% od dotacji** wypłaconej (~280 tys. przy 10 mln).
- Biznesplan: ~25 tys. PLN (refundowany z dotacji jako koszt kwalifikowany).
- **Umowa nadal USTNA** (nie podpisana formalnie!) — do uregulowania.
- Rekomenduje dzierżawę zamiast aportu (konflikt z Urbaniakiem).

### 18.8 Gwarancja bankowa na zaliczkę

ARiMR wymaga **gwarancji bankowej** na zaliczkę (5 mln PLN). Koszt: 1–2% rocznie = 50–100 tys. PLN/rok. Banki, które oferują: większość. Bardzo ważne aby wpisać to do umów kredytowych.

---

## 19. KONTAKTY ZEWNĘTRZNE

### 19.1 Prawnicy

- **Mec. Przemysław Urbaniak** — kancelaria **TaxLawPro Warszawa**.
  - Prawnik prowadzący przekształcenie sp. z o.o.
  - Ocena Sera: 6/10 (powolny, projekt umowy stał 10 miesięcy, nie zna ARiMR).
  - Kontakt regularny — cotygodniowe spotkania w 2026.

- **Mec. Bulejak** — wcześniej rozważany, niepełna współpraca.

- **Notariusz Rutkowski** (Brzeziny) — testament dziadka i akty notarialne.

### 19.2 Doradcy finansowi

- **Wiesław Oślewski** — konsultant dotacyjny (ARiMR). 22 lata. Prowizja 2,8%. Mówi prosto, używa analogii. Mocno preferuje dzierżawę nad aportem.

- **Grzegorz** — doradca finansowy (niezależny). Pomaga Sergiuszowi rozumieć finanse, banki. EBITDA, Net Debt, Ekomax, key man risk — to jego pojęcia. Spotkania regularne.

### 19.3 Banki — patrz sekcja 17.

### 19.4 Dostawcy strategiczni

- **Magik sp. z o.o.** — chłodnictwo. Mariusz Domagała / Piotr Domagała / Maciej Józefowicz / Rafał (inżynier).
- **BioEfekt Global** — Wojciech Rybka. Projekt technologiczny + BRC v9.
- **Unimash** — patroszarka Meyn (planowany).
- **Burzyński (Mar-Burz)** — transformator 800 kVA olejowy (oferta).
- **Słomski (C.O. SERWIS)** — naprawy kotłowni (DUNGS, COPRIM).
- **TASOMIX, De Heus, Ekoplon** — pasza (sPZ).
- **JDA Jeżów** — pisklęta (umowa wstawień 2026).
- **Stróżewski** — hodowca tuczący kurczaki dla firmy.
- **AVILOG** — transport żywca (Wojtek, Gabryś).
- **WebFleet (TomTom)** — Robert Kuczyński (przedstawiciel).
- **Webemo** — Bartosz Ulężałka (hosting/IT, do odzyskania kontroli).
- **Mar-Burz / Burzyński** — transformatory.

### 19.5 Audytorzy

- **Locura** (Piotr Susz) — audyt transportu (2025).
- Plus rozważane: Logisys, LoginProjects, Logit.

### 19.6 Ubezpieczyciele

- **PZU** — polisa firmowa (po audycie luty 2026).
- **Hermes** — ubezpieczenie należności (limity kupieckie).
- **Wcześniej:** Warta, UNIQA — odmówiły.

### 19.7 Personel zewnętrzny

- **Leokadia** — broker ubezpieczeniowy (PZU).
- **Agnieszka Wrońska** — rzeczoznawca, wycena działek (2023).

### 19.8 Konkurenci (też kontakt)

- **Drobex (Bogusławski)** — relacja partnerska, syn z 20% + umowa cywilno-prawna (model do nauczenia się).
- **Cedrob** — gigant branżowy.
- **Roldrob** — łódzki konkurent.
- **Aves (1990)** — łódzki, bezpośredni konkurent.
- **Reydrob** — restrukturyzacja 2023–2024 (potencjał przejęcia).
- **Gomak (Godzianów)** — rozbiór bez własnego uboju, partner/konkurent.
- **DOR-PRZEM** — dystrybutor mięsa (1995).

---

## 20. KARMA-MAX — FIRMA POWIĄZANA

### 20.1 Profil Karma-Max

- **Właściciel:** Marcin Piórkowski (zarządca sukcesyjny ubojni).
- **Lokalizacja:** Zgierz (oraz biuro w Brzezinach).
- **Działalność:** produkcja karmy zwierzęcej (głównie dla zwierząt domowych) z odpadów poubojowych ubojni Piórkowscy.
- **Plan:** budowa hali magazynowej (~2 mln PLN), dokumentacja HD/HDI (kategoria 3 odpadów).
- **Pracownicy:** ~kilkanaście osób.
- **Marlena Piórkowska** (żona) — pomaga przy operacjach.

### 20.2 Kategoria HDI / HD

- **HDI** = kategoria 3 odpadów poubojowych (do utylizacji / produkcji karmy).
- **HD** = surowiec do bezpośredniego przetworzenia na karmę.
- Karma-Max potrzebuje HD, nie HDI. Wiesław pokazał jak to rozwiązać przez „pozostałość przetwórczą" (gospodarka obiegu zamkniętego).

### 20.3 Restrukturyzacja Karma-Max

**Stan obecny:** Marcin jako jedyny właściciel.

**Plan:**
- Marlena 80% udziałów.
- Marcin 20%.
- Cel: brak powiązania kapitałowego z ubojnią (Marcin 77% w ubojni × 20% w Karma-Max < 25% próg) → obie firmy mogą OSOBNO wnioskować o dotacje ARiMR.

### 20.4 Strumień odpadów ubojnia → Karma-Max

- **Ubojnia → Karma-Max:** odpady poubojowe (głowy, łapki, narządy wewnętrzne nieprzeznaczone do spożycia ludzkiego).
- **Karma-Max → ubojnia:** karma dla zwierząt jako produkt finalny.
- **Wartość:** to jest **gospodarka obiegu zamkniętego** — daje DODATKOWE punkty w ARiMR.

### 20.5 Konflikty interesów

- **Plan Marcina (kwiecień 2026):** chce kupić bagi (UTV) na firmę ubojni „bo to ciągnik siodłowy" — Sergiusz pokazał że to byłaby firma jako prywatna skarbonka, narracja podatkowa nie wytrzymuje. Marcin po dyskusji: „już nie biorę. Poczekam." (= odsunięte w czasie, wróci).
- Sergiusz konsekwentny: rzeczy prywatne niech idą na firmy prywatne, a nie wspólną firmę dziadka.

---

## 21. KONKURENCJA I OTOCZENIE RYNKOWE

### 21.1 Polski rynek drobiu

- **Konsumpcja per capita:** ~30–32 kg/rok (4× więcej niż w PRL).
- **Polska:** największy producent drobiu w UE (ok. 17% produkcji UE).
- **Sektor:** ~250 zatwierdzonych ubojni, ok. 50 wylęgarni, 40+ wytwórni paszy, dziesiątki tysięcy ferm.
- **Łańcuch wartości:** genetyka → wylęgarnie → fermy tuczu → ubojnie → przetwórnie → dystrybucja → handel.

### 21.2 Województwo łódzkie

- 20–24 zatwierdzonych ubojni.
- **EXDROB Kutno** (1948–2022) — historyczny lider, upadłość 2022. Luka rynkowa wykorzystana przez Piórkowskich i innych.
- **Roldrob, Aves, Reydrob, Gomak, DOR-PRZEM, RADDROB** — bezpośredni konkurenci/partnerzy.
- **Piórkowscy:** górna część segmentu średnich (60–70 tys. szt./d). Po EXDROB-ie umocnili pozycję.

### 21.3 Konkurenci ogólnokrajowi

- **Cedrob** — największy w Polsce (Mława + kilka zakładów).
- **SuperDrob** — konkurent gigantyczny.
- **Drosed (LDC Polska)** — francuska grupa, duży gracz.
- **Wipasz** — paszowy gigant + ubojnia.
- **Indykpol** — głównie indyk.
- **Animex / Smithfield** — wieprzowina + drób.
- **JBB Bałdyga** — przetwórnia. **Klient mały, segment „Łyse"** (nie duży gracz dla nas, choć sami są dużą firmą).

### 21.4 Import — zagrożenie

- **Filet z Brazylii** — ~13 PLN/kg vs Piórkowscy 15–17 PLN/kg.
- Strategia obronna: **świeżość, elastyczność, lokalność** (mięso z dnia uboju, dostawa <400 km).
- BRC v9 jako dźwignia do sieci (które wymagają BRC).

### 21.5 Otoczenie regulacyjne

- **UE:** Common Agricultural Policy (PS WPR), zakaz klatek bateryjnych (2027).
- **Polska:** ARiMR (dotacje), GIW (weterynaria), WIOŚ (środowisko), Sanepid.
- **HPAI / Newcastle** — co kilka lat fala.
- **Welfare (dobrostan):** trend EU na lepsze warunki hodowli — implikacja dla cen.

### 21.6 Trendy

- **Rosnący eksport** UE → Bliski Wschód, Afryka.
- **Spadek konsumpcji wieprzowiny** → drób korzysta.
- **Roślinne alternatywy** — niewielkie, ale rosną.
- **Konsolidacja** branży — duzi rosną, mali znikają.

---

## 22. COMPLIANCE I RYZYKA REGULACYJNE

### 22.1 Weterynaria (GIW)

- Lekarz powiatowy CODZIENNIE w zakładzie.
- Kontrola ante-mortem (przed ubojem) i post-mortem (po uboju).
- ZURD codzienne (IRZplus).
- Pozwolenia weterynaryjne (zatwierdzony zakład uboju drobiu).
- BHP zwierząt (rozporządzenia o humanitarnym uboju, ogłuszanie, wykrwawianie).

### 22.2 Środowisko (WIOŚ)

- **Pozwolenie zintegrowane:** aktualne ale na 508 m³ wody, realne 800 m³ → KRYTYCZNE.
- Zarządzanie ściekami (flotator + plan oczyszczalni).
- Zarządzanie odpadami (kategoria 1, 2, 3).
- Emisje (R507A → R1234ze).

### 22.3 BHP

- Tachografy kierowców (logowanie kartą).
- Karty UNICARD pracowników hali.
- BHP hali (wózki, dźwigniki, szafy chemiczne — plan ZUS S3).
- Wypadki przy pracy (procedura BHP, raporty).

### 22.4 Higiena żywności (Sanepid)

- HACCP wdrożone.
- BRC v9 w toku (cel: koniec 2027).
- Wymazówki, próby mikrobiologiczne (laboratorium z Brudzewa).
- Mycie i dezynfekcja po każdej zmianie.

### 22.5 Podatki

- Symfonia FK + KSeF od 2026.
- US: VAT, CIT (PIT przez JDG w spadku), ZUS.
- KSeF FA(3) od 1 lutego 2026.

### 22.6 Praca

- Symfonia Kadry+Płace.
- 30-dniowe powiadomienie pracowników przed aportem (art. 231 KP).
- Przepisy o czasie pracy kierowców (rozporządzenie WE 561/2006).
- Pracownicy z zagranicy (Nepal) — zezwolenia, agencje.

### 22.7 Spadek

- JDG w spadku — deadline 02.08.2026.
- Dział spadku po dziadku NIE przeprowadzony.
- Kw nieruchomości (83/2, 84, 85 BEZ KW).
- KOWR — działki rolne.
- Spłata 100 tys. PLN dla wnuków — zrobiona.
- Podatek od spadku — Grupa zero (zwolnienie), zgłoszono w 6 miesięcy.

### 22.8 Domena i cyberbezpieczeństwo

- Domena piorkowscy.com.pl pod kontrolą zewnętrznego (Webemo) — do odzyskania.
- Backup SQL Server.
- VPN, firewall, kontrola dostępu.

---

## 23. PROFIL SERGIUSZA I STYL PRACY

### 23.1 Sergiusz Piórkowski w skrócie

- **Wiek:** ~30 lat.
- **Pozycja:** operacyjny manager Ubojni Drobiu Piórkowscy, twórca ZPSP, de facto CEO/CTO.
- **Historia:** wnuk założyciela. Praca u dziadka od 18 r.ż. (12+ lat), wcześniej hodowca brojlerów od dzieciństwa (14 lat).
- **Wykształcenie:** 4 kursy zawodowe — Dyrektor Sprzedaży, Specjalista/Manager Zakupów, Managerski, Sprzedaży Ogólnej.
- **Rodzina:** partnerka, małe dziecko. Mama Anna. Brat Kamil. Wujek Marcin (zarządca) + ciocia Marlena.

### 23.2 Osobowość (MBTI: ESTP)

- **Extraverted Sensing-Thinking-Perceiving.**
- Bezpośredni, taktyczny, czyta ludzi i sytuacje w locie.
- Lubi konkrety, nie znosi „korpogadki".
- Praca po godzinach: ZPSP (5 lat), audyty kierowców, analizy.

### 23.3 ADHD

- Diagnoza, **Medikinet CR 30** dziennie.
- Pułapka: ucieka w „dopaminowe króliczki" (nowe projekty: WEBFLEET, BRC, ZPSP) zamiast nudnych powinności (badania zdrowotne, kolonoskopia).
- Strategia: deadliny zewnętrzne, reguła „ostatnich 20%", bez telefonu w pierwszej godzinie dnia.

### 23.4 Profil w Breaking Bad (jako odniesienie metaforyczne)

- **Mike Ehrmantraut** + warstwa **Gusa Fringa** + niebezpieczny wątek **Walta**.
- Mike: operacyjny kręgosłup, wie wszystko, audyty, suchy/bezpośredni.
- Gus: budowa systemów (ZPSP), gra long game (transformacja prawna).
- Walt: niedocenienie (ZPSP wart milion, ma 6%). Walt to ostrzeżenie, nie wzorzec.

### 23.5 Komunikacja

- **Język:** polski, voice-to-text dyktowane.
- **Styl:** bezpośredni, krótkie zdania, jak najmniej preambuł.
- **Lubi:** plain language, gotowe formułki/zdania, ponumerowane listy, konkretne next steps.
- **Nie lubi:** softeningu, korpomowy, ogólników, pytań „a może byś rozważył".
- **Tempo:** szybkie. Nie czeka. Dyktuje voice messages.
- **Używa równolegle:** Claude, ChatGPT, Gemini — do różnych analiz.

### 23.6 Hobby i pasje

- **Pianino cyfrowe Kawai ES920** — uczy się grać.
- **Wraca do treningów** po wieloletniej przerwie.
- **AI** — używa wszystkich trzech wielkich modeli na bieżąco.
- **Breaking Bad** (lubi serial), Mob Psycho 100 (anime).

### 23.7 Style pracy

- 12–16 godzin dziennie.
- Otwiera zakład o 5 rano.
- Codzienne spotkania 9:00 i 13:00.
- Spotkania z bankami, prawnikami, dostawcami, audytorami.
- Wieczorem: ZPSP, dokumenty dla Marcina, AI-asystowane analizy.
- Sobota/niedziela: podwórko, dziecko, partnerka — ale często wpada „chcę na komputer". Uczy się to balansować.

### 23.8 Wartości i etyka

- Transparentność („jeżeli coś kupujemy na swoje firmy, bierzmy to na swoje firmy").
- Lojalność wobec rodziny dziadka („nigdy nie zamierzałem działać na szkodę firmy").
- Pragmatyzm („wolę porządek podatkowy niż agresywne optymalizacje").
- Praca: nie robi roszczeniowych żądań, ale stawia twarde warunki (prezes zarządu, licencja ZPSP).

### 23.9 Ogólne preferencje przy korzystaniu z AI / Claude

- **Polski.**
- **Konkretne dokumenty Word / Excel / PDF** jako wyniki.
- **Skrypty bash / Python** OK gdy potrzeba przetwarzania danych.
- **Lubi długie, dogłębne analizy** — ale z TL;DR na początku.
- **Nie zna każdego pojęcia** — często prosi o słownik prostych definicji z analogiami.
- **Voice-to-text** = literówki, fonetyczne przetworzenia (toleruje, nie irytuje).
- **Nazywa AI „Claude"** — preferuje bezpośrednie zwroty.
- **Wieloturowe rozmowy** — buduje na poprzednich kontekstach.
- **Wrzuca transkrypcje Fireflies** ze spotkań jako kontekst.
- **Lubi metafory** (samochód = silnik = mechanik; bagaż w aucie; prawo jazdy na firmę).

---

## 24. SŁOWNIK BRANŻOWY I POJĘCIA

### 24.1 Branżowe drobiarskie

| Pojęcie | Definicja prosta |
|---|---|
| **Uzysk (yield)** | Ile kg mięsa dostajesz z jednego kurczaka. Lepsza patroszarka = wyższy uzysk. |
| **Tuszka A vs B** | A = pełnowartościowa (sieci, hurt), B = ze skazami (rozbiór: filet, ćwiartki). 80/20. |
| **Klasy wagowe (6/7/8/9)** | Rozmiary tuszek, w przybliżeniu odpowiadają wadze w kg żywca. |
| **Padłe / konfiskaty** | Kurczaki padłe w transporcie + odrzucone przez weterynarza. Kategoria odpadów 1/2. |
| **Chiller** | Tunel chłodzenia tuszek po patroszeniu, -2 do +4°C. |
| **Patroszarka** | Maszyna do automatycznego usuwania wnętrzności. |
| **Filet** | Najcenniejsza część (mięsień piersiowy). Krojenie ręczne. |
| **Halal** | Ubój zgodny z islamem, oddzielny ubojowiec, certyfikat. |
| **HPAI** | Highly Pathogenic Avian Influenza — wysokopatogenna ptasia grypa. |
| **Newcastle Disease** | Choroba wirusowa drobiu, strefy restrykcyjne. |
| **ZURD** | Zgłoszenie Uboju w Rzeźni Drobiu (IRZplus / ARiMR). |
| **IRZplus** | System rejestracji i identyfikacji zwierząt ARiMR. |
| **HACCP** | Hazard Analysis Critical Control Points — system bezpieczeństwa żywności. |
| **BRC** | British Retail Consortium — standard certyfikacji żywności (sieci wymagają). |
| **IFS** | International Featured Standards — alternatywa BRC. |
| **GS1-128** | Standard kodów kreskowych do identyfikacji opakowań. |
| **Traceability** | Śledzenie produktu „od pola do stolu". |
| **Flotator** | Urządzenie do oczyszczania ścieków (separuje tłuszcz/osady). |
| **HDI / HD** | Kategorie odpadów poubojowych. HD = surowiec na karmę, HDI = utylizacja. |
| **Wstawienie** | Dostarczenie piskląt do hodowcy (cykl 35–45 dni do uboju). |
| **Ubytek** | Procent śmiertelności w trakcie hodowli (3–7%). |
| **Łapacze** | Zewnętrzna ekipa łapiąca kurczaki na fermie i ładująca do transportu. |
| **AVILOG** | Zewnętrzna firma transportu żywca (kontrahent). |
| **Mrożona tuszka** | Strata wagi 2%, wartości handlowej 18% vs świeży. |
| **Strefa zagrożona** | Restrykcyjna strefa HPAI/Newcastle wokół ogniska choroby. |

### 24.2 Chłodnictwo

| Pojęcie | Definicja |
|---|---|
| **R507A** | Freon — czynnik chłodniczy obecny w starej instalacji. Wycofywany. |
| **R1234ze** | HFO — nowy czynnik, ekologiczny, niski GWP (Global Warming Potential). |
| **Glikol propylenowy** | Czynnik pośredni w obiegu zamkniętym, bezpieczny przy żywności. |
| **Chiller (agregat wody lodowej)** | Maszyna chłodząca glikol — 1000 kW w Etapie 2. |
| **Wieża chłodnicza** | Wymiennik ciepła (skrapla parę). |

### 24.3 Spadek i prawo

| Pojęcie | Definicja |
|---|---|
| **JDG w spadku** | Jednoosobowa działalność gospodarcza po śmierci właściciela, prowadzona przez zarządcę sukcesyjnego. |
| **Zarządca sukcesyjny** | Osoba prowadząca firmę po śmierci właściciela (max 2 lata + przedłużenia). |
| **Aport** | Wniesienie majątku (nieruchomości, maszyn, zapasów) do nowo zakładanej spółki. |
| **Dzierżawa** | Wynajem zakładu spółce od osób fizycznych (spadkobierców). |
| **ZCP** | Zorganizowana Część Przedsiębiorstwa — fragment firmy nadający się do aportu. |
| **Prokurent** | Pełnomocnik spółki z prawem podpisywania umów do określonej kwoty. |
| **Beneficjent rzeczywisty** | Osoba fizyczna kontrolująca firmę >25%. Wpis do CRBR. |
| **Łączna reprezentacja** | Wymóg podpisu DWÓCH członków zarządu na każdym dokumencie. |
| **Veto** | Możliwość zablokowania decyzji przez drugiego członka zarządu. |
| **D&O ubezpieczenie** | Directors & Officers — ubezpieczenie odpowiedzialności członków zarządu. |
| **Klauzula key man** | Postanowienie chroniące spółkę przed odejściem kluczowej osoby. |
| **art. 299 KSH** | Odpowiedzialność majątkowa członka zarządu za zobowiązania spółki gdy nie zgłasza upadłości. |
| **art. 116 Ordynacji** | Odpowiedzialność członka zarządu za zaległości podatkowe spółki. |
| **art. 586 KSH** | Odpowiedzialność karna członka zarządu za niezgłoszenie upadłości. |
| **art. 231 KP** | Przejście pracowników przy aporcie/dzierżawie (powiadomienie 30 dni przed). |
| **art. 963 KC** | Co się dzieje z udziałem testamentowym jeśli spadkobierca zmarł przed spadkodawcą. |
| **KOWR** | Krajowy Ośrodek Wsparcia Rolnictwa — procedura zgody przy obrocie ziemią rolną. |
| **CRBR** | Centralny Rejestr Beneficjentów Rzeczywistych. |

### 24.4 Finanse i bankowość

| Pojęcie | Definicja |
|---|---|
| **EBITDA** | Earnings Before Interest, Taxes, Depreciation, Amortization. Ile gotówki firma generuje. |
| **Net Debt** | Łączne zadłużenie minus gotówka. |
| **Net Debt/EBITDA** | Wskaźnik zdolności kredytowej. <1,0 doskonałe, <3,0 OK. |
| **Płynność bieżąca** | Aktywa obrotowe / zobowiązania krótkoterminowe. >1,0 dobre. |
| **DSCR** | Debt Service Coverage Ratio — czy stać Cię na obsługę długu. |
| **Kowenant** | Zasady kredytowe które trzeba spełniać (np. „płynność min 1,0"). |
| **Cross-default** | Niespłacenie w jednym banku → wszystkie banki mogą wypowiedzieć. |
| **Pożyczka leasingowa** | Kredyt celowy z karencją, niski wkład, zabezpieczenie tylko na finansowanym aktywie. |
| **Leasing operacyjny** | Wynajem długoterminowy, leasingobiorca to nie właściciel. |
| **Refinansowanie** | Nowy kredyt na lepszych warunkach żeby spłacić stary. |
| **Faktoring** | Sprzedaż należności do banku za szybszą gotówkę. |
| **Białe certyfikaty URE** | Świadectwa efektywności energetycznej, można sprzedać na giełdzie. |
| **WIBOR** | Polska stopa referencyjna (1M, 3M itd.) + marża = oprocentowanie. |
| **Pomoc publiczna** | Każde wsparcie państwa: dotacje, Ekomax, ulgi. NIE łączyć dwóch na tę samą inwestycję. |
| **Efekt zachęty** | NIE zaczynaj inwestycji PRZED wnioskiem o dotację. |
| **Trwałość projektu** | 5 lat utrzymania firmy/zatrudnienia po dotacji. |
| **Koszt kwalifikowany** | Wydatek objęty dotacją. NETTO. VAT odzyskasz osobno. |
| **Zaliczka dotacji** | 50% PRZED inwestycją po podpisaniu umowy z ARiMR. |
| **Gwarancja bankowa** | Zabezpieczenie zaliczki ARiMR (1–2% rocznie). |
| **Hermes** | Firma ubezpieczająca należności (limity kupieckie). |
| **MŚP** | Mikro/Mała/Średnie Przedsiębiorstwo — limit 249 osób. |
| **Małe-mid-cap** | 250–499 osób, średnia firma. |
| **Pobrania prywatne** | Wypłaty z zysku netto przez właściciela JDG. |
| **Należności przeterminowane** | Klienci, którzy nie zapłacili w terminie. |

### 24.5 IT i systemy

| Pojęcie | Definicja |
|---|---|
| **ERP** | Enterprise Resource Planning — system zarządzania firmą (jak ZPSP). |
| **WPF** | Windows Presentation Foundation — framework GUI w .NET. |
| **DevExpress** | Komercyjna biblioteka komponentów UI. |
| **Linked server** | Połączenie SQL między dwoma serwerami (LibraNet ↔ Symfonia). |
| **AmBasic** | Język skryptowy w Symfonii Handel. |
| **KSeF** | Krajowy System e-Faktur. Schemat FA(3) od 2026. |
| **FA(2) / FA(3)** | Schematy XML faktur KSeF (2 stary, 3 obowiązkowy). |
| **UPO** | Urzędowe Poświadczenie Odbioru (KSeF). |
| **WEBFLEET.connect** | API TomTom Telematics dla floty. |
| **OPC-UA** | Standard komunikacji przemysłowej (wagi, sterowniki). |
| **RTSP** | Real-Time Streaming Protocol (kamery Hikvision). |
| **RFID** | Radio-Frequency Identification (zamiennik kodów kreskowych). |
| **REST API** | Standardowy interfejs HTTP do komunikacji systemów. |

---

## 25. OTWARTE PROBLEMY I TOP-OF-MIND

### 25.1 KRYTYCZNE (A)

1. **Konflikt aport vs dzierżawa** (Urbaniak vs Wiesław) — telekonferencja NIEODBYTA. PRIORYTET #1.
2. **Deadline 02.08.2026** — JDG wygasa, sp. z o.o. musi istnieć (4 miesiące zapasu).
3. **Negocjacja struktury zarządu z Marcinem** — Sergiusz chce być prezesem.
4. **Restrukturyzacja Karma-Max** (Marlena 80%, Marcin 20%) — wymóg dotacyjny.
5. **Dział spadku po dziadku** — nieprzeprowadzony.
6. **KW dla działek 83/2, 84, 85** — brak wpisu, blokuje aport.
7. **Pozwolenie zintegrowane** — nieaktualne (508 m³ vs 800 m³).
8. **Wniosek ARiMR wrzesień 2026** — ostatni nabór, do 10 mln PLN.

### 25.2 WAŻNE (B)

9. **Pożyczka leasingowa Magik** — finalizacja umowy z bankiem.
10. **BRC v9 + IFS — pozwolenie zintegrowane → projekt → dokumentacja → audyt 2027.**
11. **Audyt kierowców — zamknięcie naprawcze** (procedury, karty tachografu, dyspozytor).
12. **Dyspozytor Iloy — single point of failure** — szkolenie zastępcy lub auto-dispatch ZPSP.
13. **Pani Jola — dywersyfikacja klientów** — Maja/Ania budują własne relacje.
14. **Konflikt Teresa/Paulina w dziale zakupów.**
15. **Zaległości >500 tys. od hodowców — windykacja.**
16. **Migracja Microsoft 365 + Teams** — odzyskanie domeny od Webemo.
17. **Symfonia Handel 2026 — aktualizacja na natywny FA(3).**
18. **Negocjacje AVILOG** — klauzula sunset, statystyki Q1 2026.

### 25.3 PROJEKTY (C)

19. **WebFleet API integracja faza 1** — live tracking + raporty paliwowe.
20. **WebFleet faza 2** — geofencing biosekuracja.
21. **WebFleet faza 3** — auto-dispatch.
22. **ZPSP/RFID** — rozbudowa traceability.
23. **HandlowiecDashboard** — analityka per handlowiec.
24. **Tablet na hali** — przyciski przestoju.
25. **MarketIntelligence** — Brave + GPT-4o na newsy branżowe.
26. **Faktoring** — uwolnienie 17,6 mln należności.
27. **ZUS S3** — dotacja BHP do 345 tys. PLN.
28. **Ubezpieczenia PZU** — usuwanie luk (elektronika niedoubezpieczona).

### 25.4 OPERACYJNE (bieżące)

29. **Codzienne ZURD (IRZplus).**
30. **KSeF — 30–50 faktur dziennie.**
31. **Spotkania 9:00 i 13:00.**
32. **Newsy HPAI / Newcastle.**
33. **Anulacje popołudniowe — proceduralizacja.**
34. **Karteczki Joli — egzekwowanie.**
35. **Reklamacje — automatyczne korekty faktur.**
36. **Pozyskanie 5–10 nowych sieci handlowych** (z listy 58).

### 25.5 OSOBISTE (Sergiusz)

37. **Badania zdrowotne** — kolonoskopia odwlekana.
38. **Psychiatria — ADHD/depresja** — dawka leków, follow-up.
39. **Trening — powrót po latach przerwy.**
40. **Pianino — Kawai ES920.**
41. **Dziecko, partnerka — work/life balance** (saboty z rodziną zamiast komputera).

---

## ZAŁĄCZNIK A — KLUCZOWE DATY

| Data | Wydarzenie |
|---|---|
| 14.10.1996 | Założenie Ubojni Drobiu Piórkowscy przez Jerzego (dziadka). |
| 23.02.2018 | Śmierć Teresy Piórkowskiej (babcia). |
| 16.04.2018 | Protokół dziedziczenia po Teresie (Rep A 1334/2018). |
| 04.2020 | Pierwszy testament Jerzego — Sergiusz 5/6 (83%). |
| 02.2021 | Drugi testament Jerzego — Sergiusz 5/6. |
| 22.10.2021 | **Trzeci testament Jerzego — Marcin 5/6 (83%), Sergiusz 1/12.** |
| 13.12.2021 | Śmierć Andrzeja (ojciec Sergiusza). |
| 02.2022 | Upadłość EXDROB Kutno. |
| 02.08.2023 | **Śmierć Jerzego (dziadek). Otwarcie spadku.** Powstanie JDG w spadku. |
| 03.08.2023 | Sergiusz odbiera wypisy testamentów (Rep A 2434, 2435). |
| 02.08.2024 | Termin spłaty 100 tys. dla wnuków (zrealizowany). |
| 2024 | Reorganizacja sprzedaży, redukcja działu z 6 do 2,5 etatu. Wzrost obrotów. |
| 02.08.2025 | Pierwsze przedłużenie zarządu sukcesyjnego. |
| 12.2025 | Ceny rynkowe spadają. Anulacje zamówień. |
| 02.2026 | Ognisko Newcastle Disease 12 km. Audyt PZU. Audyt Locura. |
| 09.01.2026 | Pierwsza analiza oferty Magik (2,92 mln). |
| 13.03.2026 | Przedpłata 526 440 PLN dla Magik. |
| 24.03.2026 | Spotkanie z Urbaniakiem (telekonferencja, 30 min). |
| 04.2026 | Audyt kierowców (-36 tys. PLN nadpłat). Negocjacje umowy spółki. Negocjacje Avilog. |
| 28.04.2026 | Konflikt Marcin/Sergiusz ws. zakupu UTV na firmę. |
| Q2 2026 | Telekonferencja Urbaniak + Wiesław (do zorganizowania). Decyzja aport/dzierżawa. |
| Maj 2026 | Akt notarialny umowy spółki (cel). |
| Lipiec 2026 | Pracownicy powiadomieni (30 dni przed aportem). |
| **02.08.2026** | **TWARDY DEADLINE — JDG wygasa.** |
| Wrzesień 2026 | **Ostatni nabór ARiMR — wniosek do 10 mln PLN.** Patroszarka pod dotację. |
| Q4 2026 | Etap 1 chłodnictwo zakończony. Etap 2 startuje pod dotację. |
| Q1 2027 | Zaliczka ARiMR 5 mln (po podpisaniu umowy). |
| Q3 2027 | Audyt certyfikujący BRC v9 (cel). |
| 2029 | Najbliższy nabór ARiMR po wrześniu 2026. |

---

## ZAŁĄCZNIK B — TYPOWE ZADANIA NA TEJ FIRMIE

Poniżej lista typowych zadań, które Sergiusz daje AI / Claude / Claude Code. Pomagaj mu z dowolnym z nich.

### B.1 Programowanie ZPSP

- Nowy moduł WPF (XAML + C# + DevExpress).
- Refaktoryzacja istniejącego widoku.
- SQL: nowa tabela / widok / procedura składowana / trigger.
- Integracja z API zewnętrznym (KSeF, IRZplus, WebFleet, Hermes).
- Migracja danych z legacy Delphi (Raporty.exe).
- Optymalizacja zapytań.
- Skrypt AmBasic w Symfonii Handel.
- Konwerter formatu (FA2 → FA3, CSV ARiMR).
- Parser PDF (AVILOG matryce transportu).
- Import/eksport Excel/CSV.

### B.2 Dokumenty biznesowe

- Procedury operacyjne (200+ stron, 60+ procedur).
- Zakres obowiązków pracowników.
- Umowy: chłodnictwo Magik, BRC BioEfekt, leasing, dzierżawa.
- Listy klientów / hodowców z analizą.
- Plany inwestycyjne.
- Prezentacje (Gamma.app).
- E-maile do banków, prawników, partnerów.

### B.3 Analizy

- Audyt kierowców (GPS + arkusze + faktury).
- Audyt sprzedaży / handlowców (klient × handlowiec × marża).
- Marża per klient / per hodowca.
- Sezonowość, prognozy.
- Konkurencyjna analiza rynku.
- ROI inwestycji (chłodnictwo, patroszarka).
- Wskaźniki finansowe (EBITDA, cash flow, NPV).

### B.4 Negocjacje i komunikacja

- Argumenty do rozmowy z Marcinem (struktura spółki).
- Pytania do banków, prawników, doradców.
- E-maile do partnerów (Magik, BioEfekt, AVILOG).
- Skrypty na rozmowy telefoniczne.
- Wiadomości do pracowników (egzekwowanie procedur).

### B.5 Compliance i prawo

- Strategie spadkowe.
- Umowy spółki z o.o.
- Zabezpieczenia w umowie wspólników.
- Licencja ZPSP.
- KOWR, KW, działki rolne.
- KSeF, IRZplus, BRC, HACCP, pozwolenie zintegrowane.

### B.6 Osobiste

- Plany treningowe.
- Wsparcie przy ADHD / motywacji.
- Wybór sprzętu (pianino, trampolina dla rodziny).
- Życie/rodzina/sobota balans.
- Health management (kolonoskopia, leki).

### B.7 Format wyników

Sergiusz preferuje:
- **Dokumenty Word (.docx)** dla raportów, umów, propozycji.
- **Excel (.xlsx)** dla danych, kalkulacji, list.
- **PDF** dla finalnych dokumentów do druku.
- **Markdown** dla notatek roboczych i kontekstu (np. ten dokument).
- **Skrypty bash / Python** dla operacji na danych.
- **Kod C# / SQL / XAML** dla ZPSP.

Zawsze zaczyna od **TL;DR** (1–2 akapity), potem **konkretne kroki**, na końcu **opcjonalna głębsza analiza**.

---

## KOŃCOWA NOTATKA

Ten dokument jest **żywy** — będzie aktualizowany w miarę rozwoju firmy. Zawiera stan na **maj 2026**.

Po przeczytaniu tego dokumentu powinieneś:

1. **Rozumieć kontekst firmy** — branża, skala, ludzie, technologie.
2. **Wiedzieć kto jest kim** — Sergiusz, Marcin, Justyna, Pani Jola, Ilona, prawnicy, doradcy.
3. **Znać główne wyzwania** — deadline 02.08.2026, konflikt aport/dzierżawa, pozycja prezesa, dotacja wrzesień, key man risk.
4. **Rozumieć ZPSP** — co robi, jak jest zbudowany, dlaczego jest krytyczny.
5. **Mówić językiem Sergiusza** — bezpośredni, polski, konkretny, plain language z analogiami.

**Sergiusz nie czeka. Daj odpowiedź szybko, konkretną, gotową do użycia. Jeśli nie wiesz — powiedz wprost. Jeśli wiesz — daj plan, kod, dokument lub e-mail gotowy do wysłania.**

— Koniec dokumentu.
