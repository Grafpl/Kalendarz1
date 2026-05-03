# UBOJNIA DROBIU PIÓRKOWSCY — PEŁNY OBRAZ FIRMY

> **Dokument operacyjny — wersja maj 2026**
> Skonsolidowany ze wszystkich źródeł: 8 procedur (PROCEDURY_01-08), UBOJNIA_PIORKOWSCY_KONTEKST.md (125 KB), odpowiedzi Sergiusza w PYTANIA_UZUPELNIAJACE.md i PYTANIA_PRODUKCJA.md, audytów 71 okien ZPSP, danych SQL z 2 baz, 10 transkrypcji Fireflies.
>
> **Cel:** żeby Claude (i każdy nowy współpracownik / programista / dyrektor) **w 60 minut** miał kompletny obraz firmy i mógł zacząć produktywną pracę.
>
> **Autor:** Claude Code, na podstawie 100+ tur rozmowy z Sergiuszem.
> **Stan na:** 02.05.2026.

---

## SPIS TREŚCI

1. [TL;DR — firma w 30 sekund](#1-tldr)
2. [Skala i podstawowe dane](#2-skala-i-podstawowe-dane)
3. [Struktura organizacyjna — kto komu podlega](#3-struktura-organizacyjna)
4. [Ludzie z imion — pełna lista](#4-ludzie-z-imion)
5. [PRODUKCJA — szczegółowa anatomia hali](#5-produkcja-szczegółowo)
6. [MAGAZYN — przepływ towaru](#6-magazyn)
7. [MROŹNIA — co gdzie idzie](#7-mroźnia)
8. [JAKOŚĆ — kontrola, reklamacje, BRC/IFS](#8-jakość)
9. [SPRZEDAŻ — handlowcy, klienci, ucinanie](#9-sprzedaż)
10. [ZAKUP ŻYWCA — hodowcy, AVILOG, pasze](#10-zakup-żywca)
11. [TRANSPORT — flota, kierowcy, audyt](#11-transport)
12. [ZPSP — system informatyczny](#12-zpsp)
13. [PROCEDURY — co jest zapisane](#13-procedury)
14. [KLUCZOWE WYZWANIA — 15 największych problemów](#14-kluczowe-wyzwania)
15. [SZCZEGÓŁOWA ANALIZA "BIAŁYCH PLAM" — czego nie wiemy](#15-białe-plamy)
16. [DALSZE KROKI — pogłębianie produkcji/magazynu/jakości](#16-dalsze-kroki)

---

## 1. TL;DR

**Ubojnia Drobiu Piórkowscy** to rodzinna ubojnia drobiu w **Koziołkach** koło Brzezin (woj. łódzkie), założona w 1996 przez **Jerzego Piórkowskiego** (dziadek Sergiusza). Operacyjnie prowadzi ją wnuk **Sergiusz Piórkowski** od 12 lat.

- **Skala:** 70 000 sztuk/dzień, 200 ton tuszki, 318 mln PLN obrotu (2025)
- **Pracownicy:** 173 osób (123 etat + 50 agencja, plus pracownicy z Nepalu)
- **System ERP:** **ZPSP** ("Zajebisty Program Sergiusza Piórkowskiego") — autorski, C#/.NET/SQL, 5 lat rozwoju, 277 tabel, 30+ modułów
- **Klienci:** ~400 (top: Damak, Trzepałka, Bomafar, Publimar, JBB Bałdyga)
- **Hodowcy:** 140+, z czego 40-70 aktywnych
- **Mroźnie:** 3 + szokówka (-30/-40°C) + chłodnia
- **Linia uboju:** 7 500 szt/h (realnie ~7 000)
- **Kluczowe wyzwania 2026:**
  - Wąskie gardło: **produkcja czysta** (krojenie do późnej godziny)
  - 2 programy zewnętrzne (Wago + Radwag) bez dostępu API → blokuje pomiar wydajności hodowcy
  - 71 okien ZPSP do skonsolidowania w 4-5 dashboardów
  - BRC v9 + IFS w trakcie wdrożenia (cel: koniec 2027)

---

## 2. SKALA I PODSTAWOWE DANE

### 2.1 Dane rejestrowe
| Pole | Wartość |
|---|---|
| Pełna nazwa | Ubojnia Drobiu „PIÓRKOWSCY" Jerzy Piórkowski w spadku |
| NIP | **726-162-54-06** |
| PKD | **10.12.Z** (przetwarzanie i konserwowanie mięsa z drobiu) |
| Forma prawna | JDG w spadku (od 02.08.2023) |
| Adres operacyjny | Koziołki 40, 95-061 Koziołki, gm. Brzeziny |
| Województwo | łódzkie, ~30 km NE od Łodzi |
| Założenie | 14.10.1996 przez Jerzego Piórkowskiego |
| Strona | piorkowscy.com.pl (kontrolowana przez zewn. Webemo) |

### 2.2 Skala działalności
| Wskaźnik | Wartość |
|---|---|
| Ubój dzienny | ~70 000 szt / ~200 ton tuszki |
| Ubój roczny | ~17 mln szt / ~50 tys. ton |
| Pracownicy etat | ~123 |
| Pracownicy agencja | ~50 (GURAVO, IMPULS, STAR, ECO-MEN, ROB-JOB, w tym z Nepalu) |
| Razem | ~173 |
| Klienci aktywni | ~400 |
| Hodowcy w bazie | 140+ (40-70 aktywnych w danym mies.) |
| Obroty 2025 | 318 mln PLN (+23% r/r vs 258 mln 2024) |
| Zysk netto 2025 | 7,04 mln PLN |
| EBITDA 2025 | 8,8 mln PLN |
| Marża netto | ~2,29% |
| Mroźnie | 3 mroźnie + chłodnia + szokówka |
| Linia uboju | 7 500 szt/h teoretycznie, ~7 000 realnie |

### 2.3 Lokalizacje
- **Główny zakład Koziołki** — ubój, rozbiór, magazyny, mroźnia, biura
- **Lokalizacja Zgierz** — masarnia rodzinna Marcina Piórkowskiego (wujek Sergiusza)
- **Karma-Max** — firma powiązana (właściciel: Marcin), produkcja karmy z odpadów poubojowych

### 2.4 Pozwolenia i certyfikaty
- ✅ Pozwolenie weterynaryjne (zatwierdzony zakład GIW)
- ⚠️ **Pozwolenie zintegrowane środowiskowe** — aktualne na 508 m³ wody/d, **realnie zużywamy ~800 m³** (KRYTYCZNE — do odnowienia)
- ✅ HACCP wdrożone (Justyna)
- 🔄 **BRC v9** — w trakcie (BioEfekt Global / Wojciech Rybka, certyfikat planowany koniec 2027)
- 🔄 **IFS** — równolegle z BRC (jeden pakiet, BioEfekt)
- ✅ IRZplus / ARiMR — codzienne raportowanie ZURD

---

## 3. STRUKTURA ORGANIZACYJNA

```
┌─────────────────────────────────────────────────────────────┐
│  ZARZĄD                                                      │
│  ├── Sergiusz Piórkowski — operacyjny CEO/CTO de facto       │
│  └── Marcin Piórkowski — zarządca sukcesyjny (formalny)      │
└──────────────────────────┬───────────────────────────────────┘
                           │
                           ▼
┌─────────────────────────────────────────────────────────────┐
│  DYREKTOR ZAKŁADU (Plant Director)                           │
│  Justyna Chrostowska — dyrektor zakładu / główny technolog   │
│  Pain Sergiusza: nie chodzi po hali wystarczająco           │
└──────────────────────────┬───────────────────────────────────┘
                           │
       ┌───────────────────┼───────────────────┬──────────────┐
       ▼                   ▼                   ▼              ▼
┌──────────────┐  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐
│ PRODUKCJA    │  │ JAKOŚĆ       │  │ MAGAZYN      │  │ LOGISTYKA    │
│              │  │              │  │              │  │              │
│ Łukasz       │  │ Klaudia      │  │ Robert       │  │ Ilona Kubiak │
│ Collins      │  │ Osińska      │  │ Stępniak     │  │ (single point│
│ — dyr. tech  │  │ — I zmiana   │  │ Robert       │  │  of failure) │
│ + kier. uboju│  │              │  │ Osiński      │  │              │
│   brudnej    │  │ Gabriela     │  │              │  │ Magda Miler  │
│              │  │ — II zmiana  │  │ MROŹNIA:     │  │ — niewyszk.  │
│ Anna Majczak │  │              │  │ Jan Matusiak │  │ zastępstwo   │
│ — brygadzist │  │ MAŁGORZATA   │  │ Michał       │  │              │
│   hali       │  │ ANIOŁ —      │  │              │  │ ~10-13       │
│              │  │ kier. opa-   │  │              │  │ kierowców    │
│ Kierownik    │  │ kowań E2/H1  │  │              │  │              │
│ Rozbioru     │  │ (NIE QC!)    │  │              │  │              │
└──────────────┘  └──────────────┘  └──────────────┘  └──────────────┘

┌─────────────────────────────────────────────────────────────┐
│  SPRZEDAŻ (handlowcy → podlegają Zarządowi/Sergiuszowi)      │
│                                                              │
│  Pani Jola (Jolanta Kubiak) — od 1996, 60% wolumenu          │
│  Maja Leonard — ESTJ                                         │
│  Teresa Jachymczak — Carrefour, E.Leclerc, Radrob            │
│  Anna Jedynak (Ania) — eksport, mrożonki                     │
└─────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────┐
│  ZAKUPY ŻYWCA (~2 osoby, konflikt!)                         │
│  Teresa — zakupy                                             │
│  Paulina — rozważa odejście                                  │
│  + 56 łapaczy (zewn. ekipa)                                  │
└─────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────┐
│  BIURO                                                       │
│  Marlena Piórkowska — sekretariat (żona Marcina!)            │
│  Edyta — kasjerka (firmowa)                                  │
│  Grażyna — księgowa zewn. (RZiS, bilans)                     │
│  Renata Balcerak, Małgorzata Stępniak — fakturzystki         │
└─────────────────────────────────────────────────────────────┘
```

### 3.1 Kluczowa zasada: jedna droga poleceń (z procedur)
> **Wszystkie polecenia operacyjne (produkcja, magazyn, mroźnia, logistyka) IDĄ WYŁĄCZNIE przez Dyrektora Zakładu (Justyna).**
> Handlowcy NIE wydają poleceń operacyjnych. Mogą tylko składać zamówienia w ZPSP.

---

## 4. LUDZIE Z IMION

### 4.1 Zarząd
| Osoba | Rola | Status / komentarz |
|---|---|---|
| **Sergiusz Piórkowski** | Właściciel + 18 ról | Operacyjny CEO/CTO. Sam programuje ZPSP. Pełni jednocześnie role Dyr. Sprzedaży, Dyr. Zakupów, IT. Wynagrodzenie cel: 20k PLN podstawowa. |
| **Marcin Piórkowski** | Wspólnik / zarządca sukcesyjny | 77% udziałów spadkowych. Prowadzi Karma-Max w Zgierzu. Pojawia się 1-2x/tydzień. **Sergiusz: "Marcin akceptuje że oboje jesteśmy zarządem".** |
| **Marlena Piórkowska** | Sekretarka (żona Marcina) | Sergiusz: **"to nie przyjaciel ani sojusznik. Będzie grała na Marcina"**. Plan: 80% Karma-Max po restrukturyzacji. |

### 4.2 Produkcja
| Osoba | Rola | Co konkretnie robi |
|---|---|---|
| **Łukasz Collins** | Dyrektor techniczny + Kierownik Uboju Brudnej | **Pierwszy w zakładzie codziennie** (3:30, lato 2:30). Przygotowuje ubój. Odpowiada za kurczak patroszony. |
| **Anna Majczak** | Brygadzista hali | Wspiera Kierownika Produkcji. Niejasne dokładnie co. Chce dostęp do ZPSP. |
| **Kierownik Rozbioru** | (imię do potwierdzenia) | Pilnuje godzin pracowników, realizacji zamówień, zarządzania ludźmi, rozliczania produkcji, pojemników z folią. |
| **Kierownik II Zmiany** | (imię do potwierdzenia) | Zmiana B 14:00-21:00 (kontynuacja, sprzątanie, przygotowanie na rano). |

### 4.3 Jakość
| Osoba | Rola | Charakterystyka |
|---|---|---|
| **Justyna Chrostowska** | Plant Director / Główny Technolog | 30+ lat doświadczenia. Robi HACCP też w Zgierzu. **Aktywnie używa kamer Hikvision**. Limit decyzyjny reklamacji 1000 zł. **Pain point Sergiusza: nie chodzi po hali wystarczająco.** Powinna obserwować, sprawdzać kierowników. |
| **Klaudia Osińska** | QC I zmiana | **Bardzo zaangażowana, chce się rozwijać**. Sergiusz ocenia 5/10 (brak systematyczności). **Plan: mentor + plan szkoleniowy z egzaminem.** |
| **Małgorzata Anioł** | Kierownik Opakowań (E2 + H1) | UWAGA: **NIE jest QC!** Wysyła salda, potwierdza, egzekwuje zwroty pojemników/palet. (Wcześniej myślałem że to QC II zmiana — myliłem się.) |
| **Gabriela** | QC II zmiana (nowa, była w opakowaniach) | Bardzo zaangażowana. Plan: mentor + szkolenie + egzamin (jak Klaudia). |
| **Asystent Jakości** | (do zatrudnienia) | Już jest druga osoba od jakości, ale **brak zakresu obowiązków**. |

### 4.4 Magazyn / Mroźnia
| Osoba | Rola |
|---|---|
| Robert Stępniak | Magazynier (kompletacja, załadunek) |
| Robert Osiński | Magazynier |
| Jan Matusiak | Mroźnia |
| Michał | Mroźnia |

### 4.5 Sprzedaż (4 aktywne handlowczynie)
| Osoba | Klienci | Wolumen / komentarz |
|---|---|---|
| **Pani Jola (Jolanta Kubiak)** | Damak, Trzepałka, RADDROB Chlebowski, sklepy ABC, Dino Polska | **12 klientów, ~1640 t/mies (60% wolumenu)**. Od 1996. **Pisze karteczki zamiast ZPSP** (chroniczny problem). Sprzedała 4,5 t żołądków bez wpisu. **Sergiusz: "nie umie używać ZPSP, ale to problem UI, nie Joli"**. Panel Pani Joli już istnieje, do poprawy. |
| **Anna Jedynak (Ania)** | Makro C&C, Selgros, Polomarket, eksport | 9 klientów, ~1110 t/mies. |
| **Teresa Jachymczak** | Carrefour, E.Leclerc, Radrob, Ladros, Delikatesy Centrum | 8 klientów, ~1080 t/mies. |
| **Maja Leonard** | Stokrotka, mniejsze sieci | 6 klientów, ~280 t/mies. ESTJ. |

**Byli handlowcy:**
- **Daniel** — odszedł 2024. **Sergiusz: "za bardzo miał aspiracje na prezesa, kłócił się ze mną".** Próbował wyciągnąć prowizje od klientów po odejściu.
- **Radek Marciniak** — odszedł 2026. Klienci przejęci przez resztę.

### 4.6 Zakupy żywca
| Osoba | Rola | Status |
|---|---|---|
| **Teresa (zakupy)** | Dział zakupów | Konflikt z Pauliną (kwiecień 2026) |
| **Paulina** | Dział zakupów | **Sergiusz: "prawdopodobnie odejdzie w poniedziałek po rozmowie z Teresą"** |

### 4.7 Transport / Logistyka
| Osoba | Rola | Status |
|---|---|---|
| **Ilona Kubiak** | Koordynator Logistyki | **Single point of failure**. Audyt Locura wykazał: 8000L paliwa rozjazd, GPS pokazuje firmowe auto pod prywatnym adresem. |
| **Magda Miler** | Zastępstwo Iloy | Niewyszkolona — plan: 3-mies. szkolenie. |
| **Panak** | Kierowca | **0,60 zł/km** (najwyższa). Faworyzowany przez Ilonę. |
| **Tołkaczewicz** | Kierowca | 0,55 gr/km solówki |
| **Drożdżyk** | Kierowca | Stałe trasy Damak + Trzepałka (rano). |
| **Łukaszewicz, Banek, Patos** | Kierowcy | Wielodniówkowcy. |
| **Robert Staroń** | Kierowca | **Grozi odejściem** — konflikt z Iloną o serwis (zignorowała prośbę o węże). |
| **Gałek** | Kierowca | **Odchodzi** — audyt wykazał najwięcej nadpłat. |
| **Kołodziejczyk** | Kierowca | Aktywny. |

**Stawka aktualna (od maja 2026):** **70-75 gr/km z delegacjami w jednej stawce** (poprzednia 0,69 + delegacje osobno).

### 4.8 Biuro
| Osoba | Rola |
|---|---|
| **Edyta** | **Kasjerka** (wydaje pieniądze, NIE księgowa!) |
| **Grażyna** | Księgowa zewnętrzna (RZiS, bilans) |
| **Renata Balcerak, Małgorzata Stępniak** | Fakturzystki |
| **Marlena** | Sekretariat |

### 4.9 Doradcy zewnętrzni
| Osoba | Rola | Komentarz |
|---|---|---|
| **Mec. Przemysław Urbaniak** | Prawnik (TaxLawPro Warszawa) | Przekształcenie sp. z o.o. Sergiusz ocenia 6/10. |
| **Wiesław Oślewski** | Konsultant dotacyjny ARiMR | Prowizja 2,8% od dotacji. **Konflikt: Wiesław mówi DZIERŻAWA, Urbaniak — APORT.** |
| **Grzegorz** | Doradca finansowy (niezależny) | EBITDA, Net Debt, Ekomax, key man risk. |
| **Wojciech Rybka** | BioEfekt Global — BRC v9 + IFS | 60-70 projektów w łódzkim. Pakiet ~133k PLN. |
| **Mariusz/Piotr Domagała, Maciej Józefowicz** | Magik sp. z o.o. — chłodnictwo | Etap 1: 2,8 mln PLN. **Wybrane, w realizacji od maja 2026.** |
| **Wojtek (zewn. transport)** | Partner transportowy | Korekty fakturowania, AVILOG. |
| **Robert Kuczyński** | Przedstawiciel WebFleet | API + kamerki w autach (od 2 lat nieużywane). |
| **Bartosz Ulężałka** | Webemo (hosting/IT) | Trzyma domenę piorkowscy.com.pl — do odzyskania. |

---

## 5. PRODUKCJA — SZCZEGÓŁOWO

### 5.1 Rytm dnia
**Zmiana A (5:00-13:30)** + **Zmiana B (14:00-21:00)** + **przed-startowa (3:30-5:00)**

```
03:00 ─── AVILOG przyjeżdża z żywcem (samochód jeden po drugim)
03:30 ─── Łukasz Collins startuje pierwszego kurczaka
04:00 ─── Pełny ubój
05:00 ─── Pełna zmiana A wchodzi
05:30 ─── Pierwsze tuszki wchodzą do produkcji czystej (po chillerze)
06:00 ─── Klasyfikacja A/B na pełnych obrotach
07:00 ─── Justyna kontroluje temp + czystość po nocnej
09:00 ─── Wyrywkowa kontrola Klaudii (etykiety, kategorie, kamery)
10:00 ─── DEADLINE handlowcy: wszystkie zamówienia na DZIŚ w ZPSP
11:00 ─── Kontrola krojenia (oznakowanie, temp produktów)
13:00 ─── Spotkanie operacyjne: handlowcy + Justyna + Sergiusz → decyzja krojenie/mrożenie
13:30 ─── Koniec zmiany A
14:00 ─── Zmiana B wchodzi (kontynuacja + sprzątanie)
14:00 ─── DEADLINE zamówienia na JUTRO
15:00+ ── Kontrola II zmiany
21:00 ─── Koniec zmiany B
```

**Latem:** ubój zaczyna się **2:30** (zamiast 3:30), żeby uniknąć upałów.

### 5.2 Anatomia linii uboju (PRODUKCJA BRUDNA)
```
auta AVILOG → Klatki → Zawieszanie żywego (4-6 osób) → Ogłuszanie
    → Wykrwawianie → Skubarka (parzelnia) → Patroszarka MEYN MOUNTAINEER (2015)
    → Konfiskaty (lekarz powiatowy) → Chiller tunelowy (-2 do +4°C, 60-90 min)
```

**Kluczowe fakty:**
- **Patroszarka:** Meyn Mountaineer 2015. Stan techniczny ogólnie dobry, ale **noże i system transportu psują się** krytycznie → przestoje
- **Wymiana planowana:** Meyn Maestro IX 2026 (~5 mln PLN, pod dotację ARiMR)
- **Padłe w transporcie:** pracownik widzi → odkłada do **osobnego kontenera** → firma utylizująca odbiera (kategoria 1/2)
- **Konfiskaty** (decyzja weterynarza) — kategorie wewnętrzne **CH / ZM / NW** (sumują się w `Specyfikacja Surowca` ZPSP)

### 5.3 Anatomia linii produkcji czystej (KLASYFIKACJA + ROZBIÓR)

```
chiller → wybijanie tuszki na wannę (4-6 osób)
    ↓
zawieszanie na linii wagowej (waga RADWAG)
    ↓
DECYZJA KLASA WAGOWA (program WAGO):
    rozmiar 6 → 6 sztuk w pojemniku 15 kg (~2.5 kg/szt)
    rozmiar 7 → 7 sztuk (~2.14 kg/szt)
    rozmiar 8 → 8 sztuk (~1.88 kg/szt)
    rozmiar 9 → 9 sztuk (~1.67 kg/szt)
    rozmiar 10/11 → mniejsze
    ↓
KLASYFIKACJA WZROKOWA A vs B (1-2 sek na sztukę):
    - A: pełnowartościowa → idzie na klientów hurtowych, sieci
    - B: ze skazami (krwiak, złamanie, żółć, oparzenie, czerwony filet, otwarte rany)
        ↓ pracownik przesuwa DŹWIGNIĘ W GÓRĘ na widelcu lub naciska GUZIK
        ↓ program ważący wie że ma to NIE UWZGLĘDNIAĆ
        ↓ na końcu linii: 2 osoby przewieszają na linię rozbieralni
    ↓
TUSZKA A → pakowanie (15 kg netto pojemnik E2)
    ↓ (waga paletowa RADWAG)
    ↓ ETYKIETOWANIE (terminal przy wadze drukuje etykietę)
    ↓
MAGAZYN ŚWIEŻY 65554

LINIA ROZBIORU (tuszka B + tuszka A skierowana do krojenia):
    Korpus + filet razem → MASZYNA ROZDZIELAJĄCA (Meyn) →
        → KORPUS → bezpośrednio do pojemnika
        → FILET → ręczne czyszczenie (usuwanie balonów, krwiaków, zakrwawionych miejsc)
        → ĆWIARTKA (~33% uzysku z tuszki)
        → SKRZYDŁO (~9%)
        → PAŁKA / NOGA
        → POLĘDWICZKI, MIELONE, TUBA (z drobnych odpadów — KTO DECYDUJE: do ustalenia z Justyną i Anną Majczak)
        → Pozostałe odpady → Karma-Max (kategoria 3) lub utylizacja (kategoria 1/2)
```

### 5.4 Współczynniki uzysku (z modułu ZPSP "Krojenie")
| Element | % uzysku z tuszki |
|---|---|
| Filet I | 29,5% |
| Filet II | 1,9% |
| Ćwiartka I | 33,4% |
| Ćwiartka II | 2,0% |
| Skrzydło I | 8,7% |
| Skrzydło II | 1,0% |
| Korpus | 22,7% |
| Pozostałe (odpad) | 0,8% |
| **SUMA** | ~100% |

**Przelicznik żywiec → tuszka:** ~78% (200t żywca → ~156t tuszki).

### 5.5 Wagi na hali
| Waga | Producent | Cel |
|---|---|---|
| Waga selektywna (klasa wagowa + A/B) | **WAGO** | Decyduje o korytarzu pakowania, integracja z dźwignią/guzikiem klasyfikatora |
| Waga paletowa | **RADWAG** | Tuszka całość (15 kg pojemnik) |
| Wagi platformowe (elementy) | **RADWAG** | 2× 15 kg + 1× podroby |

**🔥 PROBLEM KRYTYCZNY:** Sergiusz **NIE MA dostępu do bazy/API** żadnego z 2 zewnętrznych programów (WAGO + RADWAG). To blokuje:
- Pomiar **realnego % klasy A vs B per hodowca** (kluczowy KPI)
- Pomiar **realnego tempa linii** (real-time)
- Rejestrację przestojów

**AKCJA:** Sergiusz prosi dostawców o dostęp do baz / API.

### 5.6 Klasy wagowe (ROZMIARY 6-11) — co to znaczy
**WAŻNE — częsta pomyłka:** „rozmiar 7" **NIE** oznacza 7 kg ani 7 kg żywca. **To liczba sztuk tuszek mieszczących się w pojemniku E2 do nominalnych 15 kg netto.**

| Rozmiar | Liczba sztuk w pojemniku 15 kg | Średnia waga tuszki | Charakter |
|---|---|---|---|
| 6 | 6 szt | ~2,50 kg | Duża, najcięższa |
| **7** | **7 szt** | **~2,14 kg** | **Standard (najpopularniejszy)** |
| **8** | **8 szt** | **~1,88 kg** | **Standard niższy** |
| 9 | 9 szt | ~1,67 kg | Mniejsza |
| 10 | 10 szt | ~1,50 kg | Mała |
| 11 | 11 szt | ~1,36 kg | Bardzo mała |

Klient zamawia po **rozmiarze**, nie wadze tuszki — bo dla niego liczy się jak będzie pakował dalej.

### 5.7 Cele produkcyjne
- **80% tuszka A**, 20% klasa B (przekazana na rozbiór)
- **Filet ~30%** uzysku w rozbiorze (cel z procedury)
- **Krojenie = strata** ekonomiczna (chyba że tuszka B z konkretnymi skazami)
- **Mrożenie = strata -18%** wartości handlowej

### 5.8 Wąskie gardło 2026
**Sergiusz: "Wąskie gardło jest obecnie na produkcji czystej. Proces krojenia powoduje że filet, krojenie i korpus są do późnej godziny, a na elementy musimy długo czekać."**

→ **Rozwiązanie:** patroszarka Meyn Maestro (IX 2026, pod dotację ARiMR) — wydajność z 7500 → 9000+ szt/h.

### 5.9 Konfiskaty i padłe — workflow w ZPSP
**Moduł "Specyfikacja Surowca" (14B w UBOJNIA_PIORKOWSCY_KONTEKST.md):**

| Kolumna | Opis |
|---|---|
| Data uboju | Dzień ubicia partii |
| Hodowca | ID + nazwa z `WidokHodowcy` |
| NumIRZ | Identyfikator ARiMR (np. „068736945-001") |
| Sztuki żywe (przyjęte) | Z wagi samochodowej |
| Waga żywa (kg) | Z wagi samochodowej |
| **Padłe** | Sztuki padłe w transporcie / przy zawieszaniu — ręcznie |
| **CH** | Konfiskata (chłonność / cellulit) |
| **ZM** | Konfiskata (zmiany chorobowe / wady) |
| **NW** | Konfiskata (nadwaga / niewłaściwy) |
| Suma konfiskat | `= CH + ZM + NW` |
| Sztuki ubite | `= Sztuki żywe - Padłe - Konfiskaty` |
| Waga tuszki | Z wagi linii |
| Uzysk % | `Waga tuszki / Waga żywa × 100` (cel ~78%) |
| Klasa A / B | Procentowy podział |

**Workflow:** Wstawienie żywca → ubój → konfiskaty → wpis do ZPSP (Justyna/Klaudia/Anna Majczak) → eksport CSV do IRZplus jako ZURD.

### 5.10 Statystyki referencyjne (norma branżowa)
- Padłe w transporcie: 0,1-0,5% (sezon: lato wyższe)
- Konfiskaty z linii: 0,5-1,5% (CH+ZM+NW razem); >2% = sygnał alarmowy
- Uzysk żywiec → tuszka: 78% średnio (sezonowo 76-80%)

---

## 6. MAGAZYN

### 6.1 Mapa magazynów w Symfonii (HANDEL)
| Kod | Nazwa | Funkcja |
|---|---|---|
| **65554** | Świeże po uboju | sPWU, PWP, RWP, sPZ żywca — główny chłodzony |
| **65556** | Wydania | sWZ, sWZ-W, sWZK — fizycznie oddzielne, rampa załadunkowa |
| 65552 | Drugi magazyn produkcji | sPW, sPZZ, sRW, sMM |
| 65547 | Paczkowane | sPPK, sRPK |
| 65562 | Mrożonki / półprodukty | sPPM, sRPM |
| 65559 | Pomocniczy | sMP, sMW |
| 65543, 65566 | Wydaniowe | sKWM, sPZ, sPZK, sWZ |

### 6.2 Workflow wydania (PROCEDURY_04_MAGAZYN)
```
1. Magazynier sprawdza zamówienia w ZPSP (panel magazyniera)
2. Sprawdza godzinę wyjazdu (deadline od Koordynatora Logistyki)
3. Kompletuje towar wg listy z ZPSP
4. Stosuje FIFO (najstarszy pierwszy)
5. Brakuje? NATYCHMIAST do Dyrektora (NIE szuka sam rozwiązania)
6. Załadunek (sprawdza etykiety, pojemniki, temperatury)
7. Potwierdza zaladunek w ZPSP (z ewentualnymi korektami)
8. Przekazuje dokumenty kierowcy
9. Sprawdza czy kierowca dostał WSZYSTKO
```

### 6.3 Brak skanowania na wydaniu (KLUCZOWY FAKT)
**Sergiusz: "Brak skanowania na wydaniu — tylko panel magazyniera."**

- Aktualnie: magazynier kompletuje **wzrokowo** wg listy z ZPSP
- Etykiety GS1-128 drukowane na produkcji, ale **na wydaniu nikt ich nie skanuje**
- Etykiety pełnią rolę **informacyjną** (data, partia, klasa) — nie systemową
- **Plan 2027:** RFID + skanowanie pod dotację ARiMR

### 6.4 Czas wydania
- 30-60 min na samochód (1-5 t)
- 5 aut jednocześnie (ile rampy?) — **do potwierdzenia**

### 6.5 FIFO — zasada bezwzględna
> **Najstarszy najbliżej wyjścia. Bez wyjątków.** "Klient woli świeże" → odpowiedź magazyniera: "wydaję wg FIFO".
> Towar z poniedziałku wychodzi PRZED towarem z wtorku — nawet jeśli handlowiec prosi o świeże.

### 6.6 Inwentaryzacja
- **Codziennie:** koniec dnia + rano (podwójne liczenie). Rozbieżność → NATYCHMIAST Dyrektor.
- **Tygodniowo:** pełna fizyczna w mroźni.

### 6.7 Stan magazynowy — podwójna kontrola
- Na koniec dnia (po ostatnim załadunku) — co zostało
- Na początek dnia (przed pierwszym załadunkiem) — co jest
- Rozbieżność = problem

### 6.8 Eskalacja przy braku towaru
1. Magazynier stwierdza brak
2. NATYCHMIAST informuje Dyrektora Zakładu (Justyna)
3. Jeśli niedostępna — wpis na grupę WhatsApp Handlową
4. Brak reakcji w 10 min — telefon do Zarządu (Sergiusz)
5. Czeka na decyzję o ucinaniu
6. Realizuje DOKŁADNIE wg decyzji
7. Dokumentuje w ZPSP

**ZAKAZ:** Magazynier NIE decyduje sam komu dać towar.

### 6.9 Pojemniki E2 i palety H1
- **Pojemnik E2:** standard, 36 lub 40 szt na palecie
- **Paleta H1:** drewniana, standardowa
- **Małgorzata Anioł** — kierownik opakowań (od ostatnich miesięcy):
  - Wysyła salda klientom
  - Potwierdza zwroty
  - Egzekwuje zaległe pojemniki/palety
  - **„Zamrożony pieniądz"** — ile jest u klientów

### 6.10 Etykietowanie
- **Drukarki Zebra ZPL** (termiczne)
- Etykieta: data, partia, klasa, kod GS1-128, waga
- 36 000+ etykiet w `EtykietyZbiorcze`
- Drukowane na **terminalach przy wagach platformowych** (po ważeniu)

---

## 7. MROŹNIA

### 7.1 Komponenty
- **3 mroźnie** (komory chłodnicze -18 do -20°C)
- **Szokówka** (-30 do -40°C, ~12h przed mroźnią)
- **Chłodnia** (osobna od magazynu świeżego — do potwierdzenia)
- **Chiller tunelowy** (na linii uboju, -2 do +4°C, 60-90 min)

### 7.2 Decyzja "co mrozić" (PROCEDURY_05_MROZNIA)
- **Spotkanie codzienne 13:00** — Justyna + Sergiusz + handlowcy
- Bilans niesprzedanego → krojenie czy mrożenie?
- **Kalkulator decyzji w ZPSP (moduł Krojenie 14A)** liczy 3 scenariusze:
  - Sprzedaż tuszki as-is (baseline)
  - Krojenie + sprzedaż elementów świeżych
  - Krojenie + mrożenie + sprzedaż taniej

**Wynik typowy (dla 19 800 kg):**
- Tuszka: 145 134 zł (baseline)
- Krojenie świeże: 136 167 zł (-8 967 zł)
- Mrożenie: 99 259 zł (-45 875 zł STRATA -2,32 zł/kg)

→ **MROŻENIE = OSTATECZNOŚĆ.** Robi się tylko gdy:
- Brak odbiorcy (kryzys rynkowy, anulacje)
- Konieczność biosekuracji (HPAI)
- Strategiczne pod eksport (Ania)

### 7.3 Workflow mrożenia
```
1. Dyrektor Zakładu decyduje (spotkanie 13:00)
2. Wpis zlecenia mrożenia do ZPSP
3. Pracownik mroźni przyjmuje towar — WAGA na wejściu
4. Etykietuje KAŻDY pojemnik: data mrożenia, produkt, waga, partia
5. Wpis do ZPSP faktyczna ilość przyjęta
6. Układa wg FIFO (najstarszy NAJBLIŻEJ wyjścia)
```

### 7.4 Workflow wydania z mroźni
```
1. Handlowiec wpisuje zamówienie na mrożone w ZPSP (TYLKO za zgodą Sergiusza)
2. Dyrektor zatwierdza wydanie
3. Wydanie — WAGA na wyjściu
4. Porównanie waga wejściowa vs wyjściowa (norma strat ≤2%)
5. Strata >2% — NATYCHMIAST raport do Dyrektora
6. Wpis do ZPSP faktycznej ilości wydanej
7. ZAWSZE FIFO
```

### 7.5 Kontrola
- **Codziennie rano:** temperatury wszystkich mroźni (-18°C min, -20°C cel)
- **Co tydzień:** pełna inwentaryzacja fizyczna (vs ZPSP)
- Towar bliski daty → alarm do Dyrektora

### 7.6 Strata wartości handlowej
**-18% wartości** vs świeży produkt (heurystyka, spójna z modułem ZPSP).

---

## 8. JAKOŚĆ

### 8.1 Harmonogram dnia (PROCEDURY_07_JAKOSC)
| Godzina | Co |
|---|---|
| 7:00 | Temperatury chłodni/mroźni/hal, czystość po nocnej zmianie |
| 9:00 | Wyrywkowa kontrola produkcji (etykiety, kategorie, kamery, higiena) |
| 11:00 | Kontrola krojenia (oznakowanie, temp produktów) |
| 13:00 | Spotkanie handlowe — decyzja niesprzedane/mrożenie |
| 14:00-14:30 | Wydawanie materiałów eksploatacyjnych |
| 15:00+ | Kontrola II zmiany |

### 8.2 Reklamacje — workflow (PROCEDURY_07_JAKOSC)
```
1. Klient zgłasza (przez handlowca lub bezp.) → numer reklamacji
2. Klasyfikacja: jakościowa / ilościowa / transportowa
3. Badanie: hala, dokumenty, kamery, logi ZPSP
4. Konsultacja z Dyrektorem (Justyna) + Zarządem (Sergiusz)
5. Decyzja: pełne / częściowe / odrzucenie
6. Odpowiedź klientowi DO 15:00 tego samego dnia
7. Zamknięcie 48h
8. CAPA: co zmieniamy, kto, do kiedy
9. Archiwizacja
```

### 8.3 Limity decyzyjne
- **Justyna do 1000 PLN** — sama decyduje
- **Powyżej 1000 PLN** — eskalacja do Sergiusza

### 8.4 Wielki problem: 75% pseudo-reklamacji
**Sergiusz potwierdził:** 247 reklamacji w 90 dniach = 75% to **automatyczne faktury korygujące** z Symfonii (`SyncFakturyKorygujace()` co 5 min ściąga z Symfonii):
- Status = "Nowa", StatusV2 = "ZGŁOSZONA", TypReklamacji = "Faktura korygująca"
- **Brak `PrzyczynaGlowna`** — automaty bez opisu
- Nikt ich nie zamyka → zawyżają statystyki
- **Plan:** odfiltrować w UI, pokazać tylko realne reklamacje

### 8.5 Próbki laboratoryjne
- Salmonella, Campylobacter, Listeria, mikrobiologia
- **Laboratorium z Brudzewa** — kontakt prywatny WhatsApp Justyny
- Wymazówki — co tydzień
- Wyniki pozytywne → NATYCHMIAST Dyrektor

### 8.6 BRC v9 + IFS — wdrażanie 2026-2027
**Partner:** BioEfekt Global / Wojciech Rybka

**Pakiet (~133 tys. PLN):**
- Projekt technologiczny — 43k
- Dokumentacja BRC v9 — 45k
- Dokumentacja IFS — w pakiecie
- Obsługa miesięczna BRC/IFS/HACCP — 6,5k/mies
- Audyt certyfikujący BRC ~10k
- Audyt IFS — osobna ścieżka

**Harmonogram:**
- **Q2 2026:** pozwolenie zintegrowane (woda 800 m³ — KRYTYCZNE)
- **Q3 2026 - Q1 2027:** projekt technologiczny (Rybka, plan pod 150 tys. szt./tydz.)
- **Q1 2027 - Q3 2027:** dokumentacja + szkolenia + audyt
- **Koniec 2027:** certyfikaty

**Dlaczego BRC + IFS:**
- BRC = wymóg Tesco, brytyjskie + większość polskich sieci
- IFS = wymóg sieci niemieckich + francuskich (Lidl, Aldi, Carrefour, Edeka)
- Razem = drzwi do każdej sieci EU + eksport bezpośredni 2027

### 8.7 Justyna — pain Sergiusza
> **Sergiusz: "Justyna jako dyrektor zakładu powinna częściej spacerować po uboju, sprawdzać kierowników, usprawniać procesy. Patrzy w kamery, ale nie do końca to robi."**

→ **Plan ZPSP dla Justyny:**
- Duży monitor 4K w jej biurze
- LIVE monitoring kluczowych wskaźników
- Raporty + alerty na żywo
- Klikalne kamery Hikvision RTSP

### 8.8 Klaudia + Gabriela — plan rozwoju
**Sergiusz: "Klaudia jest bardzo zaangażowana, chce się rozwijać. Plan: mentor + szkolenie teoretyczne + praktyczne + egzamin."**
**To samo dla Gabrieli (była opakowania → QC II zmiana).**

---

## 9. SPRZEDAŻ

### 9.1 Top klienci (z odpowiedzi Sergiusza + dokumentów)
| Klient | Kategoria | Handlowczyni | Komentarz |
|---|---|---|---|
| **DAMAK** | Hurtownia | Pani Jola | Codziennie, kierowca Drożdżyk, ranne okno |
| **TRZEPAŁKA AGMAR** | Hurtownia | Pani Jola | Codziennie, ~25-tonowe trasy |
| **BOMAFAR** | Hurtownia | — | Okno 15:00 |
| **PUBLIMAR** | Hurtownia | — | Okno 14:00 |
| **RADROB** | Hurtownia | Teresa | — |
| **LADROS** | Hurtownia | Teresa | — |
| **PIEKARSKA / BIESARSKA** | Hurtownia | — | — |
| **SZUBRYT, DESTAN** | — | — | — |
| **PODOLSKI** | Hurtownia | — | — |
| **PAMSO Pabianice** | Hurtownia | — | — |
| **WIERZEJKI Trzebieszów** | Hurtownia | — | — |
| **DROBEX (Bogusławski)** | Konkurent / partner | — | Model „syn z 20% + umowa cywilno-prawna" |
| **MARKET (nowy)** | Sieć | — | 4 dni/tyg., 1,5-1,7t |
| **EUROPE TRADE / EGE FOOD / EUREKA / KAPTAIN FOOD** | Trading | Ania | Eksport |
| **JBB BAŁDYGA — ŁYSE** | Zakłady mięsne | — | **Jedyny mały klient typu „mięsny w hurcie"** |

### 9.2 Segmentacja klientów (VIP / P2 / P3 / P4 / P5)
**7 kryteriów ważonych:**
1. Warunki płatności (waga ×3) — przedpłata > 30 dni
2. Obrót miesięczny (×2) — >200k / 50-200k / <50k
3. Marża (×2)
4. Staż współpracy
5. Elastyczność asortymentowa
6. Przewidywalność zamówień
7. Znaczenie strategiczne

**Klasy:** VIP / P2 / P3 / P4 / P5 (20-110 pkt).

### 9.3 Dzień pracy handlowca (PROCEDURY_01_HANDLOWCY)
| Godzina | Czynność |
|---|---|
| 6:30-8:00 | Zbieranie i wpisywanie zamówień do ZPSP |
| Do 7:20 | Zatwierdzanie cen |
| 8:00-9:00 | Weryfikacja bilansu |
| Do 9:00 | Logistyk: 70% kursów gotowych |
| 9:00-10:00 | Spotkanie operacyjne (2-3x/tydz) |
| **DEADLINE 10:00** | **Zamówienia na DZIŚ w ZPSP** |
| 10:00-13:00 | Bieżąca obsługa + CRM/pozyskiwanie |
| 13:00-14:00 | Zamówienia na JUTRO |
| **DEADLINE 14:00** | **Zamówienia na JUTRO w ZPSP** |
| 14:00-14:30 | Weryfikacja końcowa |
| **WTOREK** | Spotkanie rozszerzone (myjka, opakowania, finanse) |

### 9.4 Bilans i bufor
- **Bilans = Przychód + Stany - Zamówienia**
- **Bufor 5-6 ton** ZAWSZE zachowany
- **Piątek: bufor MUSI być sprzedany do 0** (nie przeżyje weekendu)
- **Nadwyżki → CHŁODNIA, NIE mroźnia**

### 9.5 Proporcjonalne ucinanie
**Kiedy produkcja < zamówienia → KAŻDY klient dostaje TEN SAM PROCENT.**

**Algorytm Sergiusza w ZPSP:**
- <5% odchylenia → zespół ucina sam
- 5-20% → zespół + zatwierdzenie Sergiusza
- >20% → tylko Sergiusz decyduje

**Realne incydenty:**
- "Trzepałka" — Jola obiecała 7,5t mając 5t → klient Radrob na końcu trasy nie dostał NIC
- "Radrob" — skumulowany niedobór 12,7t w styczniu 2026

### 9.6 Pani Jola — case
**Sergiusz: "Jola pisze karteczki zamiast wpisywać do ZPSP. Sprzedała 4,5 tony żołądków bez wpisu do systemu. Kontroluje ~60% wolumenu (Damak, Trzepałka, AGMAR). Most do najważniejszych klientów."**

**Strategia:** dywersyfikacja — Maja i Ania budują własne relacje z klientami Joli.

**Sergiusz: "Jola nie umie używać ZPSP, ale to NIE jest problem Joli, tylko UI."**

**Panel Pani Joli już istnieje** (uproszczony). Trzy życzenia Joli:
1. Prostszy sposób wprowadzania danych
2. Łatwy dostęp do informacji
3. Lepsza organizacja (mniej przytłaczające)

### 9.7 Polityka cenowa
| Produkt | Kto ustala |
|---|---|
| Świeże | Handlowiec (rynkowo) + obowiązek raportu Zarządowi |
| **Mrożone** | **WYŁĄCZNIE Zarząd (Sergiusz)** — przed wpisaniem zamówienia |
| **Rabaty** | **WYŁĄCZNIE Zarząd**, na uzasadniony wniosek |

### 9.8 Limity kredytowe (Hermes)
- Każdy klient ma limit ustalony przez ubezpieczyciela Hermes
- ZPSP blokuje zamówienie po przekroczeniu limitu / przeterminowaniu
- Eskalacja: 1-3 dni przypomnienie → 7 dni ostrzeżenie → 14 dni wstrzymanie

### 9.9 Lista 58 sieci do pozyskania (Top 10)
- Topaz (120+ sklepów, wschód)
- Prim Market (60+)
- API Market (22, mazowieckie)
- Wafelek (23)
- Chata Polska (210+, wlkp + łódzkie)
- Chorten Północ / PD
- Top Market (580+)
- Społem Łódź / Tomaszów
- Dino Polska (2500+)
- Lewiatan (3200)

**Dźwignia:** BRC v9 (sieci wymagają).

---

## 10. ZAKUP ŻYWCA

### 10.1 Liczby
- **140+ hodowców** w bazie, **40-70 aktywnych**/mies
- Strategia **50/50 kontrakt vs wolny rynek**
- Cykl wstawień: 35-45 dni od pisklaka do uboju
- Promień: większość hodowców 30-40 km od zakładu

### 10.2 AVILOG (zewn. transport żywca)
- Plan w PDF/Excel → ZPSP `WidokMatrycaWPF` (parser PDF)
- Wolumen: ~8 500 t/kwartał, ~34 000 t/rok
- Wartość: ~3,9 mln PLN/rok przy 114 zł/t
- Wypełnienie aut: 93%
- Negocjacje 2026: klauzula sunset (cena spada gdy ON spada)
- Cel: 117-119 PLN/t z sunset zamiast 125-130 bez

### 10.3 Pasza (TASOMIX, De Heus, Ekoplon)
- Brojler ALFA, Grower 1/2, Finiszer, Starter (kat. 65883, jednostka tona)
- Sergiusz **kupuje paszę** i **odprzedaje hodowcom kontraktowym**
- Hodowca bierze paszę "na fakturę", odejmuje od ceny żywca

### 10.4 Pisklęta — JDA Jeżów
- Umowa: JDA → Sergiusz (kupuje pisklaki) → **Stróżewski** (tuczy) → Sergiusz (odbiera kurczaka)
- 7 wstawień × 39 000 szt w 2026
- Cena ustalana 7 dni przed nakładem jaj
- **Kara umowna setki tys. PLN** za późną rezygnację

### 10.5 Konflikt Teresa/Paulina
**Sergiusz (po Fireflies 22.04 + odpowiedź): "Paulina prawdopodobnie odejdzie w poniedziałek po rozmowie z Teresą."**

### 10.6 Łapacze (zewn. ekipa)
- Plan kwiecień 2026: zwiększyć do **56 osób**
- Płatność za auta (nie godziny)

### 10.7 Zaległości hodowców
**>500 tys. PLN zaległości** od hodowców (głównie za paszę). Procedura windykacji niesformalizowana.

### 10.8 HPAI / Newcastle Disease
- HPAI: 19 ognisk Polska, **2 łódzkie**
- Newcastle: ognisko **12 km od zakładu** (luty 2026)
- Plan: moduł geofencing biosekuracja w ZPSP (alert gdy auto wjeżdża do strefy)

---

## 11. TRANSPORT

### 11.1 Flota
| Typ | Liczba | Ładowność |
|---|---|---|
| Solówki (chłodnie) | 5 | 10 500 kg |
| TIRy (ciągnik + naczepa chłodnia) | 5 | 19 800 kg |
| Bus duży (Sasin) | 1 | 3 500 kg |
| Bus mały | 1 | (w naprawie) |
| **RAZEM** | **12** | |

Zasięg: ~400 km. Dalej — zewnętrzny.

### 11.2 Kierowcy
**~10 dostępnych z 13** na liście. Stawki **70-75 gr/km z delegacjami w jednej stawce** (od maja 2026).

### 11.3 WebFleet (TomTom Telematics)
- API: WEBFLEET.connect + DRIVE.connect
- Klucz API uzyskany luty 2026
- Plan 4 fazy:
  - **Faza 1:** Live tracking + raporty paliwowe
  - **Faza 2:** Geofencing biosekuracja
  - **Faza 3:** Auto-dispatch (eliminacja Iloy SPOF)
  - **Faza 4:** ETA dla klientów + analiza kosztów

### 11.4 Audyt Locura 2026
- ~36 000 PLN nadpłat zidentyfikowanych
- **Gałek** — największe źródło → odchodzi
- **8 000 L paliwa rozjazd** w 72 dni na dystrybutorze Swimmer
- **GPS pokazał trasę firmowego auta pod prywatny adres Iloy** wielokrotnie
- Sabotaż — pojedynczy incydent

### 11.5 Niewykorzystane systemy (KOSZT!)
- **Kamerki w autach** — zamontowane, **nieużywane od 2 lat** (firma płaci)
- **TachoShare** wykupiony, nieużywany
- Plan: aktywować lub odłączyć

### 11.6 Karty tachografu
- Kierowcy nie zawsze logują się swoimi kartami
- **Plan: "kto nie loguje się swoją kartą, nie wyjeżdża"**

---

## 12. ZPSP — SYSTEM INFORMATYCZNY

### 12.1 Skala
- **277 tabel**, ~4,5 mln rekordów
- **30+ modułów**
- **5 lat rozwoju** przez Sergiusza (single dev)
- Wartość: 1-3 mln PLN
- **71 okien** w `Menu.cs` (do skonsolidowania!)

### 12.2 Stack technologiczny
| Warstwa | Tech |
|---|---|
| Język | C# 12 / .NET 6+ |
| UI | WPF + DevExpress (DXGrid, DXChart) |
| Baza | SQL Server 2022 (LibraNet @ 192.168.0.109) |
| Linked server | Sage Symfonia Handel @ 192.168.0.112 |
| ORM | ADO.NET, raw SQL |
| IDE | Visual Studio 2022 + Claude Code CLI |

### 12.3 Infrastruktura
- **Serwer Fujitsu** @ 192.168.0.112 — Symfonia Handel + FK + KSeF
- **Serwer HP** @ 192.168.0.109 — ZPSP + LibraNet
- **3-ci komputer** — bramki/wagi + UNICARD RCP
- **~25 stacji roboczych**
- VPN dla zdalnych handlowczyń
- **Mobile app** — sprzedaż, zakup, transport (Sergiusz potwierdził)
- **Backup ZPSP — NIE robi codziennego, do ogarnięcia**

### 12.4 Główne moduły (skrót)
**Sprzedaż:** WidokZamowienia, HandlowiecDashboard, KreatorOfert, PanelPlatnosci, Reklamacje, Pojemniki, KartotekaOdbiorcow

**Zakup żywca:** WidokHodowcy, WstawieniaKurczakow, WidokPartie, RankingHodowcow, CenyDzienne

**Produkcja:** WidokProdukcja, ModulKrojenie, AnalizaTygodniowa, SpecyfikacjaSurowca

**Magazyn:** WidokMagazyn, PanelMagazyniera, EtykietyZbiorcze

**Transport:** WidokFlota, WidokMatrycaWPF, TDriver, TVehicle, TTrip

**CRM:** CallReminderConfig, HandlowcyCRM, Zadania

**Inne:** Operatorzy, Dashboard CEO TV (10 widoków), KSeF, IRZplus, MarketIntelligence

### 12.5 Programy zewnętrzne (kluczowe!)
1. **WAGO** — waga selektywna (klasy A/B + klasy wagowe)
2. **RADWAG** — wagi platformowe i paletowe
3. **UNICARD** — RCP karty czasu pracy
4. **Hikvision** — kamery (Justyna)
5. **Swimmer** — dystrybutor paliwa
6. **Cent** — celno-skarbowy (Ilona)
7. **WebFleet** — GPS floty
8. **Sage Symfonia** — Handel + FK + Kadry+Płace + KSeF Plus

**🔥 PROBLEM:** Brak dostępu API/bazy do **WAGO + RADWAG** — blokuje pomiar wydajności hodowcy.

### 12.6 KSeF
- Od 1.02.2026 schemat FA(3)
- Symfonia 2025.2 generuje stary FA(2) → **konwerter FA(2)→FA(3) Sergiusza działa**
- Wolumen: 30-50 faktur/dzień

### 12.7 IRZplus / ARiMR
- Codzienne ZURD (Zgłoszenie Uboju w Rzeźni Drobiu)
- REST API → "Access denied" (wymaga umowy z ARiMR)
- Aktualnie: eksport CSV → import w portalu

### 12.8 Plan rewolucji 71 okien → 5 dashboardów
1. **Cockpit Właściciela** (Sergiusz)
2. **Hala Produkcyjna LIVE** (Justyna + Łukasz)
3. **Dashboard Sprzedaży** (handlowczynie + Sergiusz)
4. **Plan & Bilans Produkcji** (planista + Justyna)
5. **Cockpit Jakości** (Klaudia + dział jakości)

**Plus:** narzędzia operacyjne (Lista Partii, Kartoteka Towarów, Transport, etc.) — bez zmiany.
**Usuwamy:** Komunikator + Centrum Spotkań (już wstrzymane).
**Scalamy:** Analiza Przychodu + Analiza Wydajności → Hala LIVE.

---

## 13. PROCEDURY (PROCEDURY_01-08)

### 13.1 Główne zasady (powtarzane w każdej procedurze)

**Zasada A:** Tylko **jedna droga poleceń** — Dyrektor Zakładu.
**Zasada B:** **ZPSP = jedyne źródło prawdy.** Karteczki, Excel, ustne zakazane.
**Zasada C:** **FIFO bezwzględne** wszędzie.
**Zasada D:** **Eskalacja** — brak/rozbieżność → NATYCHMIAST Dyrektor.
**Zasada E:** **Konsekwencje progresywne** — rozmowa → upomnienie → konsekwencje kadrowe.

### 13.2 Komunikacja
- **WhatsApp grupy:** Handlowa, Produkcyjna, Jakość
- **Plan migracji:** Microsoft Teams (kanały #sprzedaz, #produkcja, #logistyka, #jakosc, #zarzad)
- **Sergiusz: "Na razie nie trzeba teamsów"** — odsunięte

### 13.3 Onboarding agencyjnych (BRC/IFS)
1. Karta identyfikacyjna
2. Buty + odzież robocza
3. Szkolenie stanowiskowe (KONKRETNY dział) z podpisem
4. Weryfikacja po 1 tyg + 1 mies
5. **BEZ szkolenia + podpisu → NIE wchodzi na halę**

### 13.4 Naprawcze przy awarii ZPSP
- Handlowiec MUSI kontynuować zamówienia (kartka, Excel, telefon)
- **Po przywróceniu** — wszystkie zamówienia do ZPSP
- "System nie działał" = niedopuszczalne

---

## 14. KLUCZOWE WYZWANIA — 15 największych problemów

### Operacyjne
1. **🔥 Wąskie gardło — produkcja czysta** (krojenie do późnej godziny)
2. **🔥 2 programy zewnętrzne (WAGO + RADWAG) bez dostępu API** — blokuje % klasy A/B per hodowca
3. **Klasa B = 250 ton zalega** w magazynie (z danych SQL)
4. **Piątkowe niesprzedane** — 17.04.2026: 57 ton zostało (29% produkcji!)
5. **Brak skanowania na wydaniu** — manual + ryzyko błędów (do 2027 RFID)

### Ludzie
6. **Pani Jola — karteczki zamiast ZPSP** (problem chroniczny, 60% wolumenu zagrożone)
7. **Justyna — nie chodzi po hali wystarczająco** (pain Sergiusza)
8. **Konflikt Teresa/Paulina** — Paulina prawdopodobnie odejdzie
9. **Klaudia 5/10 + Gabriela** — potrzebują mentora i planu szkoleniowego

### IT
10. **75% reklamacji = pseudo (auto-import z Symfonii)** — zawyża statystyki
11. **Marża per produkt — niewiadoma** (Sergiusz: "nie wiem jaką mam marżę")
12. **71 okien ZPSP** — chaos, do skonsolidowania w 5 dashboardów
13. **Brak codziennego backupu ZPSP** — krytyczne ryzyko

### Strategiczne
14. **Spread 2,50 zł/kg jest nieaktualny** — pełna kalkulacja rentowności otwarta
15. **Pozwolenie zintegrowane 800 m³ vs 508 m³ limit** — KRYTYCZNE WIOŚ

---

## 15. BIAŁE PLAMY — czego jeszcze nie wiem

### Produkcja
- **Liczba osób per stanowisko** (do wypełnienia w `STANOWISKA_PRODUKCJI.md`)
- **Kto decyduje co produkować** (mielone, polędwiczki, tuba) — Sergiusz: "Justyna + Anna Majczak"
- **Skrawki/odpady** — gdzie idą konkretnie
- **Czystość pojemników E2** — kto sprawdza
- **Awarie patroszarki Meyn 2015** — częstotliwość, koszty napraw

### Magazyn / Mroźnia
- **Ile palet mieści magazyn 65554** — pojemność maksymalna
- **Czas wydania** — typowy 1 ton (15 min? 45 min?)
- **Co dokładnie idzie do której mroźni** (komora 1/2/3)
- **Inwentaryzacja tygodniowa** — ile godzin, ile osób

### Jakość
- **Procedury Klaudii** — co konkretnie robi w trakcie obchodu
- **Plan szkoleniowy Klaudii i Gabrieli** — szczegóły
- **Zakres obowiązków drugiej osoby od jakości** — brak

### Sprzedaż
- **Top 5 klientów obrotowo** (nie tylko "duzi")
- **Konkretne klienci eksportowi Ani** (do których krajów)
- **Marża per klient** — top 5 i bottom 5

### Transport
- **Imię Kierownika Rozbioru, Kierownika II Zmiany**
- **Kto realnie planuje trasy** (Ilona + ?)

### Procesy
- **Co dokładnie robi Anna Majczak** — Sergiusz: "wspiera kierownika produkcji" — niejasne
- **Spotkanie 9:00 i 13:00** — gdzie się odbywa, ile uczestników, jak długo
- **Komunikacja kryzysowa** — szablon, kto kogo informuje

---

## 16. DALSZE KROKI

### Krótkoterminowo (najbliższe 4 tygodnie)
1. **Sergiusz wypełnia `STANOWISKA_PRODUKCJI.md`** w poniedziałek (chodzi po hali, nagrywa)
2. **Sergiusz pyta dostawców WAGO + RADWAG o dostęp do baz** — odblokuje pomiar wydajności
3. **Backup ZPSP codzienny** — kluczowe ryzyko, do natychmiastowej naprawy
4. **Audyt panel Pani Joli** — co konkretnie poprawić (3 życzenia Joli)

### Średnioterminowo (Q2 2026)
5. **Marża top-down per produkt** w Dashboardzie Sprzedaży (mam wzór z PokazKrojenieMrozenie)
6. **Filtr auto-import w Reklamacjach** + osobna zakładka „Korekty Symfonii"
7. **Alert „Klasa A < 75%"** w DashboardPrzychodu
8. **Tab „Plan vs Realizacja"** w Dashboardzie Sprzedaży
9. **Cockpit Właściciela MVP** — rozbudowa PulpitZarzadu (4 nowe KPI + alert bar)

### Długoterminowo (2026-2027)
10. **Rewolucja 71 okien → 5 dashboardów**
11. **Hala Produkcyjna LIVE** (scalenie ProdukcjaPanel + DashboardPrzychodu)
12. **Patroszarka Meyn Maestro** (IX 2026, dotacja ARiMR)
13. **BRC v9 + IFS audyt** (koniec 2027)
14. **Eksport bezpośredni** (DE/NL/RO)
15. **Email 7:00 raport poranny** (PorannyBriefing → SMTP)

---

# 🎯 CO TERAZ — PRZESŁANIE OD CLAUDE

**Sergiuszu**, mam teraz pełny obraz. Po przeanalizowaniu wszystkiego widzę że:

## A. Najpilniejsze (rób natychmiast)
1. **Backup ZPSP codzienny** — godzina pracy, krytyczne ryzyko
2. **Skontaktuj się z dostawcami WAGO + RADWAG** o API — odblokuje pomiar efektywności hodowców
3. **Wypełnij `STANOWISKA_PRODUKCJI.md`** — bez tego nie zaplanuję mapy hali

## B. Co JESZCZE chcę pogłębić (odpowiedz na to)
1. **Awarie patroszarki Meyn 2015** — opowiedz mi 3 ostatnie awarie (co, ile czasu, ile straciliście)
2. **Spotkanie 13:00 — pokaż mi jak się odbywa** dosłownie. Kto siada, ile minut, jak prezentuje liczby
3. **Wąskie gardło na produkcji czystej** — opowiedz dzień gdy "elementy długo czekały" — konkretny przykład
4. **Anna Majczak** — porozmawiaj z Justyną, daj mi konkretny zakres czynności
5. **Druga osoba od jakości (bez zakresu obowiązków)** — kto to, co potrafi, co planujesz?

## C. Następny dokument który napiszę
Po Twoich odpowiedziach napiszę **`PRODUKCJA_MAGAZYN_JAKOSC_PROGRAM.md`** — szczegółowy plan **programistyczny** dla 3 najważniejszych dla Ciebie działów:
- Mapa procesów (BPMN-style)
- Konkretne ekrany/tablety per stanowisko
- Lista KPI per stanowisko
- Plan integracji WAGO + RADWAG
- Procedury cyfrowe (zastępujące papierowe)
- Roadmap programowania (3, 6, 12 miesięcy)

To będzie dokument **na 200+ KB**, taki sam szczegółowy jak ten — ale skupiony na technicznym planie programowania trzech działów.

**Daj sygnał:** mam zacząć?
