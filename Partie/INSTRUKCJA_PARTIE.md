# INSTRUKCJA OBSLUGI MODULU "LISTA PARTII UBOJOWYCH"

## Spis tresci

1. [Czym jest modul Partie?](#1-czym-jest-modul-partie)
2. [Jak uruchomic modul](#2-jak-uruchomic-modul)
3. [Dwa glowne widoki - przeglad](#3-dwa-glowne-widoki---przeglad)
4. [Widok "Produkcja dzis" - Dashboard](#4-widok-produkcja-dzis---dashboard)
5. [Widok "Lista partii" - Tabela](#5-widok-lista-partii---tabela)
6. [Cykl zycia partii - 10 statusow](#6-cykl-zycia-partii---10-statusow)
7. [Tworzenie nowej partii](#7-tworzenie-nowej-partii)
8. [Zamykanie partii](#8-zamykanie-partii)
9. [Ponowne otwieranie partii](#9-ponowne-otwieranie-partii)
10. [Szczegoly partii (rozwijanie wiersza)](#10-szczegoly-partii-rozwijanie-wiersza)
11. [Porownanie dostawcow](#11-porownanie-dostawcow)
12. [Panel alertow](#12-panel-alertow)
13. [Sparkline - mini wykres produkcji](#13-sparkline---mini-wykres-produkcji)
14. [Szybka zmiana statusu](#14-szybka-zmiana-statusu)
15. [Auto-detekcja statusu](#15-auto-detekcja-statusu)
16. [Eksport do Excel](#16-eksport-do-excel)
17. [Skroty klawiaturowe](#17-skroty-klawiaturowe)
18. [Typowe scenariusze dziennej pracy](#18-typowe-scenariusze-dziennej-pracy)
19. [Slowniczek pojec](#19-slowniczek-pojec)
20. [FAQ - Czesto zadawane pytania](#20-faq---czesto-zadawane-pytania)

---

## 1. Czym jest modul Partie?

Modul "Lista Partii Ubojowych" sluzy do zarzadzania partiami surowca (drobiu) od momentu zaplanowania dostawy, przez uboj i produkcje, az do zamkniecia partii po zakonczeniu przetwarzania.

**Partia** to jednostka organizacyjna produkcji. Kazda partia reprezentuje jedna dostawe drobiu od jednego dostawcy (hodowcy). Partia ma swoj unikalny numer, dostawce, dane skupowe, wazenia produkcyjne, kontrole jakosci i historie zmian statusu.

Modul integruje dane z wielu zrodel:
- **listapartii** - glowna tabela partii (otwarcie/zamkniecie)
- **FarmerCalc** - dane skupowe (waga brutto, tara, netto, cena, kierowca, pojazd)
- **PartiaDostawca** - powiazanie partii z dostawca i swiadectwem weterynaryjnym
- **Out1A / In0E** - wazenia produkcyjne (wydania z dzialu 1A, przyjecia na dzial 0E)
- **QC (PartiaQCTemp, PartiaQCWady, PartiaQCZdjecia)** - kontrola jakosci
- **Haccp** - trasowanie miedzydzialowe
- **HarmonogramDostaw** - plan dostaw na dany dzien
- **QC_Normy** - konfigurowalne normy jakosciowe
- **PartiaStatus** - historia zmian statusu
- **PartiaAuditLog** - log audytowy wszystkich zmian

---

## 2. Jak uruchomic modul

1. Zaloguj sie do aplikacji Kalendarz1 (ekran logowania Menu1.xaml)
2. W glownym menu znajdz kategorie **PRODUKCJA I MAGAZYN**
3. Kliknij kafelek **Lista Partii** (accessMap[58])

Otworzy sie okno "Lista Partii Ubojowych - ZPSP" z dwoma zakladkami:
- **Produkcja dzis** (domyslna) - dashboard dzienny
- **Lista partii** - pelna tabela historyczna

---

## 3. Dwa glowne widoki - przeglad

### Zakladka "Produkcja dzis"
Dashboard pokazujacy sytuacje na dzis. Widac tu:
- Karty aktywnych partii (z metryka i sparkline)
- Karty zamknietych dzis partii
- Harmonogram dostaw na dzis (prawy panel)
- Panel alertow (jesli sa problemy)
- Statystyki zbiorcze u gory

### Zakladka "Lista partii"
Tabela DevExpress z pelna lista partii w wybranym zakresie dat. Mozna:
- Filtrowac po dacie, dziale, statusie, szukac po nazwie
- Rozwijac wiersz klikajac "+" aby zobaczyc 6 zakladek szczegolow
- Eksportowac do Excel
- Otwierac/zamykac partie
- Porownywac dostawcow

---

## 4. Widok "Produkcja dzis" - Dashboard

### 4.1. Pasek narzedziowy (gora)

Na gorze widoku znajduje sie ciemnoniebieski pasek z:
- **Tytul**: "PRODUKCJA DZIS" + aktualna data + zegar (odswiezany co sekunde)
- **Przycisk "+ Nowa partia"** (zielony) - tworzy nowa partie
- **Przycisk "Lista partii"** (niebieski) - przelacza na zakladke z tabela
- **Przycisk "Odswiez"** - reczne odswiezenie danych

### 4.2. Pasek statystyk

Pod paskiem narzedziowym znajduje sie 6 "kart statystyk":

| Statystyka | Opis |
|---|---|
| **Partii dzis** | Laczna liczba partii otwartych/zamknietych dzis |
| **Otwartych** | Ile partii jest aktualnie otwartych (zielona wartosc) |
| **Wydano kg** | Suma kilogramow wydanych dzis (niebieska) |
| **Sr. wydajnosc** | Srednia wydajnosc procentowa (fioletowa) |
| **Sr. temp rampa** | Srednia temperatura na rampie (pomaranczowa) |
| **Plan dostaw** | Ile pozycji w harmonogramie na dzis (zlota) |

### 4.3. Karty aktywnych partii (lewa czesc)

Kazda aktywna (niezamknieta) partia jest wyswietlana jako biala "karta" z nastepujacymi danymi:

**Wiersz 1: Numer partii + Badge statusu**
- Numer partii (np. "26066001") w czcionce Consolas
- Po prawej kolorowy "badge" z aktualnym statusem (np. "W produkcji" na niebieskim tle)

**Wiersz 2: Nazwa dostawcy**
- Pelna nazwa dostawcy, np. "KOWALSKI JAN - FERMA DROBIU"

**Wiersz 3: Kluczowe metryki (4 kolumny)**
- **szt** - sztuki deklarowane ze skupu
- **wydano kg** - ile kilogramow wydano z produkcji
- **na stanie** - ile kg zostalo na stanie (wydano minus przyjeto)
- **QC** - badge kontroli jakosci: "OK" (pelne), "Czesciowe" lub "Brak"

**Wiersz 4: Mini wykres (sparkline)**
- Fioletowa linia pokazujaca skumulowana produkcje kg w kolejnych godzinach
- Widoczna tylko jesli sa dane produkcyjne (minimum 2 punkty godzinowe)
- Pozwala na szybka ocene, czy produkcja rośnie rownomiernie

**Wiersz 5: Godzina + Wydajnosc + Przycisk szybkiej zmiany statusu**
- Godzina otwarcia partii
- Wydajnosc procentowa (fioletowa)
- Przycisk szybkiej zmiany statusu (np. ">> W produkcji") - patrz rozdzial 14

### 4.4. Karty zamknietych partii

Pod sekcja "ZAMKNIETE DZIS" wyswietlane sa karty zamknietych dzis partii.
Sa przygaszone (szary kolor, opacity 0.8) i zawieraja:
- Numer partii + badge statusu
- Nazwe dostawcy
- Wydane kg + wydajnosc

### 4.5. Klikniecie w karte - panel szczegolow (flyout)

**Klikniecie dowolnej karty** (aktywnej lub zamknietej) otwiera panel szczegolow (flyout), ktory zaslania widok kart. Flyout zawiera:

**Naglowek (ciemnoniebieski):**
- Numer partii (duzy) + przycisk "X" do zamkniecia
- Status i dostawca

**Tresc (podzielona na sekcje):**

**Informacje:**
- Dostawca (nazwa + ID)
- Dzial (1A/0E/0K)
- Data i godzina otwarcia
- Kto otworzyl
- Swiadectwo weterynaryjne (jesli jest)

**Metryki:**
- Sztuki deklarowane
- Netto skup (kg)
- Wydano (kg i szt)
- Przyjeto (kg i szt)
- Na stanie (kg)
- Wydajnosc (%)

**Kontrola jakosci:**
- Badge QC (OK/Czesciowe/Brak)
- Klasa B (%)
- Temperatura rampa (z podswietleniem jesli poza norma)
- Wady (skrzydla/nogi/oparzenia) - jesli sa
- Ilosc zdjec QC
- Padle (ilosc padlych)

**Wykres produkcji:**
- Wieksza wersja sparkline (60px wysokosci)

**Akcje:**
- Dla partii aktywnej: przycisk ">> Nastepny status" + przycisk "Zamknij"
- Dla partii zamknietej: przycisk "Otworz ponownie"

**Zamkniecie flyoutu:** przycisk "X" lub klawisz **Escape**.

### 4.6. Harmonogram dostaw (prawy panel)

Prawy panel gorny wyswietla liste planowanych dostaw na dzis z tabeli HarmonogramDostaw. Kazda pozycja zawiera:
- Nazwe dostawcy
- Badge "NOWA" (zielony) lub "MA PARTIE" (szary) - informuje, czy juz utworzono partie
- Ilosc sztuk, wage i cene

**Klikniecie pozycji harmonogramu:**
- Jesli nie ma jeszcze partii (badge "NOWA") - otwiera dialog tworzenia nowej partii z automatycznym wypelnieniem danych z harmonogramu
- Jesli juz ma partie (badge "MA PARTIE") - wyswietla komunikat informacyjny

### 4.7. Automatyczne odswiezanie

Dashboard odswiezasie co 30 sekund (w trybie cichym - bez overlay ladowania).
Przy kazdym odswiezeniu:
1. Pobiera aktualne partie dzisiejsze
2. Pobiera harmonogram
3. Pobiera normy QC
4. Pobiera dane sparkline (godzinowe)
5. Uruchamia auto-detekcje statusu (Feature 10)
6. Generuje alerty (Feature 7)
7. Aktualizuje karty i statystyki

---

## 5. Widok "Lista partii" - Tabela

### 5.1. Pasek filtrow i narzedziowy (gora)

Ciemnoszary pasek z filtrami po lewej i przyciskami po prawej:

**Filtry (lewa strona):**
- **Od/Do** - zakres dat (domyslnie: 7 dni wstecz do dzis)
- **Dzial** - filtr po dziale: Wszystkie / 1A / 0E / 0K
- **Status** - filtr po statusie: Wszystkie / Otwarte / Zamkniete / Zaplanowane / W produkcji / Zamkn. z brakami
- **Szukaj** - pole tekstowe do wyszukiwania po numerze partii lub nazwie dostawcy (Enter zatwierdza)

**Przyciski (prawa strona):**
- **+ Nowa partia** (zielony) - tworzy nowa partie
- **Zamknij** (pomaranczowy) - zamyka zaznaczona partie
- **Otworz** (fioletowy) - otwiera ponownie zamknieta partie
- **Excel** (zielony) - eksport do pliku .xlsx
- **Odswiez** - odswiezenie danych
- **Dostawcy** (fioletowy) - otwiera okno porownania dostawcow
- **Dashboard** (ciemnoniebieski) - otwiera widok "Produkcja Dzis" w nowym oknie

### 5.2. Tabela glowna (DevExpress GridControl)

Tabela wyswietla partie w wybranym zakresie dat. Kolumny:

| Kolumna | Opis |
|---|---|
| (pasek kolorowy) | 6-pikselowy pasek koloru po lewej stronie, odpowiadajacy kolorowi statusu |
| **Partia** | Unikalny numer partii (format RRDDDNNN) |
| **Data** | Data otwarcia (yyyy-MM-dd) |
| **Godz.** | Godzina otwarcia |
| **Dostawca** | Nazwa dostawcy/hodowcy |
| **ID Dost.** | ID dostawcy w systemie |
| **Dzial** | Dzial produkcyjny (1A/0E/0K) |
| **Status** | Aktualny status partii (kolorowy tekst) |
| **Szt. dekl.** | Sztuki deklarowane przez hodowce |
| **Netto skup kg** | Waga netto ze skupu |
| **Wydano kg** | Kilogramy wydane z produkcji |
| **Przyjeto kg** | Kilogramy przyjete |
| **Na stanie** | Roznica: wydano minus przyjeto |
| **Wydajn.%** | Procentowa wydajnosc (wydano/netto*100) |
| **Kl.B %** | Procent klasy B |
| **Temp rampa** | Temperatura na rampie rozladunkowej |
| **QC** | Badge kontroli jakosci |
| **Swiad. wet.** | Numer swiadectwa weterynaryjnego |
| **Otworzyl** | Kto otworzyl partie |
| **Zamknal** | Kto zamknal partie |
| **Zamkniecie** | Data i godzina zamkniecia |

**Kolorowanie wierszy (Feature 1):**
Kazdy wiersz ma kolorowy pasek (lewa strona) oraz kolorowy tekst statusu:
- Jasnoniebieskie tlo = W produkcji
- Szare tlo = Zamknieta
- Zolte tlo = Zamknieta z brakami
- Czerwone tlo = Odrzucona
- Zielone tlo = Zaakceptowana
- Pomaranczowe tlo = Na rampie
- Jasne tlo = Zaplanowana

**Funkcje tabeli:**
- **Sortowanie** - kliknij naglowek kolumny
- **Auto-filtr** - wiersz filtrowania pod naglowkami (wpisz wartosc)
- **Zaznaczanie** - kliknij wiersz aby go zaznaczyc (potrzebne do Zamknij/Otworz)
- **Rozwijanie szczegolow** - kliknij "+" po lewej stronie wiersza

**Podsumowania (dolny pasek tabeli):**
- Laczna liczba partii
- Suma netto kg
- Suma wydano kg

### 5.3. Pasek statusu (dol okna)

Ciemny pasek na dole wyswietlajacy statystyki:
```
Partii: 45 (otwartych: 3, zamknietych: 42) | Dzis: 8 partii, 25 340 kg | Sr. wydajnosc: 73.2% | Sr. klasa B: 4.5% | Sr. temp rampa: 3.2 C
```

---

## 6. Cykl zycia partii - 10 statusow

Partia przechodzi przez nastepujace statusy w swoim cyklu zycia:

```
PLANNED (Zaplanowana)
    |
    v
IN_TRANSIT (W trasie)
    |
    v
AT_RAMP (Na rampie)
    |
    v
VET_CHECK (Kontrola wet.)
    |
    v
APPROVED (Zaakceptowana)
    |
    v
IN_PRODUCTION (W produkcji)
    |
    v
PROD_DONE (Prod. zakonczona)
    |
    v
CLOSED (Zamknieta)  lub  CLOSED_INCOMPLETE (Zamkn. z brakami)
```

Istnieje tez status specjalny:
- **REJECTED (Odrzucona)** - partia odrzucona na dowolnym etapie

### Opis kazdego statusu:

**PLANNED (Zaplanowana)** - kolor szary
- Partia utworzona z harmonogramu dostaw, ale drob jeszcze nie wyjezdza
- Jest to status "poczatkowy" gdy tworzymy partie z harmonogramu przed faktycznym zaladunkiem

**IN_TRANSIT (W trasie)** - kolor niebieski
- Kierowca wyjezdal po drob do hodowcy
- Zmieniamy recznie lub automatycznie gdy system wykryje wyjazd

**AT_RAMP (Na rampie)** - kolor pomaranczowy
- Auto z drobiem dotarlo na rampe rozladunkowa zakladu
- Auto-detekcja: system zmienia na ten status gdy pojawi sie wpis FarmerCalc (ważenie)

**VET_CHECK (Kontrola weterynaryjna)** - kolor ciemnopomaranczowy
- Drob jest kontrolowany przez lekarza weterynarii
- Czekamy na numer swiadectwa weterynaryjnego

**APPROVED (Zaakceptowana)** - kolor zielony
- Lekarz weterynarii zaakceptowal partie
- Auto-detekcja: system zmienia na ten status gdy pojawi sie numer VetNo (swiadectwo)

**IN_PRODUCTION (W produkcji)** - kolor niebieski
- Trwa uboj i przetwarzanie - wazenia produkcyjne sa aktywne
- Auto-detekcja: system zmienia na ten status gdy pojawiaja sie rekordy w Out1A (wazenia)
- To jest domyslny status dla partii otwartych bez StatusV2

**PROD_DONE (Produkcja zakonczona)** - kolor morski
- Produkcja zakonczona, ale partia jeszcze nie jest formalnie zamknieta
- Mozna jeszcze robic korekty, weryfikowac QC

**CLOSED (Zamknieta)** - kolor zielony
- Partia poprawnie zamknieta - kontrola jakosci kompletna (QC OK)
- Wszystkie pozycje checklist QC zostaly spelnieniowane

**CLOSED_INCOMPLETE (Zamknieta z brakami)** - kolor zloty
- Partia zamknieta, ale brakuje pelnej kontroli jakosci
- Np. brak pomiarow temperatury lub brak oceny wad

**REJECTED (Odrzucona)** - kolor czerwony
- Partia calkowicie odrzucona (np. niedopuszczona przez weta)

---

## 7. Tworzenie nowej partii

Sa dwa sposoby tworzenia partii:

### 7.1. Tworzenie z harmonogramu (zalecane)

1. Na dashboardzie "Produkcja dzis" znajdz w prawym panelu **HARMONOGRAM DOSTAW**
2. Znajdz pozycje z zielonym badge "NOWA"
3. **Kliknij te pozycje**
4. Otworzy sie dialog "NOWA PARTIA" z automatycznie wypelnionym:
   - Informacja o pozycji harmonogramu (Lp, dostawca, data)
   - Szczegoly: sztuki, waga, typ ceny, cena
   - Dostawca zostanie automatycznie wybrany z listy
5. Mozesz zmienic dzial (domyslnie 1A)
6. Kliknij **"Utworz partie"**
7. System wygeneruje numer partii w formacie RRDDDNNN i przypisa ja do pozycji harmonogramu

**Format numeru partii RRDDDNNN:**
- RR = ostatnie 2 cyfry roku (np. 26 dla 2026)
- DDD = dzien roku 3-cyfrowy (np. 066 dla 7 marca)
- NNN = numer kolejny tego dnia (np. 001, 002, 003...)

### 7.2. Tworzenie reczne

1. Kliknij przycisk **"+ Nowa partia"** (zielony) w pasku narzedzi
2. W dialogu "NOWA PARTIA":
   - Wybierz **dostawce** z listy rozwijanej (wyszukiwanie po nazwie)
   - Wybierz **dzial** (1A / 0E / 0K)
3. Kliknij **"Utworz partie"**
4. System wygeneruje numer i otworzy partie

### 7.3. Co sie dzieje po utworzeniu partii?

- Nowy rekord w tabeli `listapartii` z IsClose=0
- Wpis w `PartiaAuditLog` (kto, kiedy, co)
- Wpis w `PartiaStatus` (historia statusow)
- Jesli z harmonogramu: kolumna `HarmonogramLp` jest ustawiona
- Nowy status: `IN_PRODUCTION` (lub `PLANNED` jesli z harmonogramu)
- Karta pojawia sie na dashboardzie
- Wiersz pojawia sie w tabeli

---

## 8. Zamykanie partii

### 8.1. Jak zamknac partie

**Z dashboardu:**
1. Kliknij karte partii aby otworzyc flyout
2. Kliknij przycisk "Zamknij"

**Z tabeli:**
1. Zaznacz partie w tabeli (kliknij wiersz)
2. Kliknij przycisk "Zamknij" (pomaranczowy) w pasku narzedzi

### 8.2. Dialog zamykania partii

Otworzy sie okno "ZAMKNIECIE PARTII [numer]" z nastepujacymi sekcjami:

**Informacje o partii:**
- Dostawca
- Data otwarcia
- Kto otworzyl

**Metryki zamkniecia:**
- Wydano (kg i szt)
- Przyjeto (kg i szt)
- Na stanie (kg) - jesli >10 kg, pojawi sie zolte ostrzezenie!
- Wydajnosc (%)

**CHECKLIST QC (kontrola jakosci):**
System automatycznie tworzy liste kontrolna na podstawie danych partii i norm z tabeli QC_Normy:

- Pomiary temperatury - czy zostaly wykonane
- Temperatura w normie - czy srednia mierzy w granicach normy
- Ocena wad (skrzydla, nogi, oparzenia) - czy zostala wykonana
- Klasa B w normie - czy procent klasy B jest w dopuszczalnych granicach
- Swiadectwo weterynaryjne - czy jest numer

Kazda pozycja ma status:
- Zielony "V" = spelnione
- Zolty "!" = ostrzezenie (poza norma)
- Czerwony "X" = brak danych

**Podsumowanie QC:**
- **QC KOMPLETNE (X/X)** - zielone = partia zostanie zamknieta jako "Zamknieta" (CLOSED)
- **QC NIEKOMPLETNE (X/X)** - zolte = partia zostanie zamknieta jako "Zamknieta z brakami" (CLOSED_INCOMPLETE)
- **QC BRAK (0/X)** - czerwone = jak wyzej, ale zero speknien

**Komentarz:**
- Mozesz dodac komentarz do zamkniecia

**Przycisk "Zamknij partie":**
- Jesli QC jest niekompletne, pojawi sie dodatkowe potwierdzenie
- Po zatwierdzeniu partia zostaje zamknieta

### 8.3. Co sie dzieje po zamknieciu?

- `listapartii.IsClose = 1`, `CloseDate`, `CloseTime`, `CloseOperator` ustawione
- `StatusV2` zmieniony na `CLOSED` lub `CLOSED_INCOMPLETE`
- Wpis w `PartiaStatus` (historia)
- Wpis w `PartiaAuditLog`
- Karta przenosi sie do sekcji "ZAMKNIETE DZIS" na dashboardzie
- Wiersz w tabeli zmienia kolor na szary/zolty

---

## 9. Ponowne otwieranie partii

Zamknieta partie mozna otworzyc ponownie, jesli np. popelniono blad.

### 9.1. Jak otworzyc partie ponownie

**Z dashboardu:**
1. Kliknij zamknieta karte aby otworzyc flyout
2. Kliknij przycisk "Otworz ponownie"

**Z tabeli:**
1. Zaznacz zamknieta partie
2. Kliknij przycisk "Otworz" (fioletowy)

### 9.2. Dialog otwierania

- Wyswietla informacje o zamknieciu (kiedy i przez kogo)
- **Wymagane pole: powod ponownego otwarcia** (nie mozna pominac!)
- Po zatwierdzeniu partia wraca do statusu IN_PRODUCTION

---

## 10. Szczegoly partii (rozwijanie wiersza)

W widoku "Lista partii" kliknij **"+"** po lewej stronie wiersza, aby rozwinac szczegoly. Pojawi sie panel z 6 zakladkami:

### 10.1. Zakladka "Wazenia"

Tabela z wszystkimi wazeniami (pojedynczymi operacjami wagowymi) dla tej partii:
- **Godz.** - godzina wazenia
- **Artykul** - nazwa produktu
- **Netto kg** - waga netto
- **Szt** - ilosc sztuk
- **Operator** - kto wayl
- **Kierunek** - "OUT" (wydanie) lub "IN" (przyjecie)
- **Tabela** - zrodlo danych (Out1A, In0E)
- **Data** - data wazenia

### 10.2. Zakladka "Produkty"

Zagregowane dane produkcyjne - ile kilogramow i sztuk kazdego produktu:
- **ID / Artykul** - identyfikator i nazwa produktu
- **Netto kg** - suma netto
- **Wydano** - wydania (dodatnie)
- **Storno** - korekty (ujemne)
- **Szt** - laczna ilosc sztuk
- **Wazen** - ile operacji wagowych
- **% udzialu** - procentowy udzial w calej partii

### 10.3. Zakladka "QC / Jakosc"

Szczegolowe dane kontroli jakosci:

**Temperatury:**
Tabela z pomiarami temperatury w roznych miejscach (rampa, samochod, itp.) z 4 probami i srednia.
Srednia powyzej 4 stopni jest podswietlona na czerwono.

**Ocena wad:**
Gwiazdki od 1 do 5 dla:
- Skrzydla
- Nogi
- Oparzenia

**Podsumowanie:**
- Klasa B (%) - podswietlona na czerwono jesli >20%
- Przekarmienie (kg)
- Notatka QC

**Zdjecia:**
Lista zdjec z kontroli jakosci (typ wady, opis, kto wykonal)

### 10.4. Zakladka "Skup"

Dane z tabeli FarmerCalc - informacje o skupie:
- Dostawca, data skupu
- Kierowca, pojazd (samochod + przyczepa)
- Wagi: brutto, tara, netto
- Sztuki deklarowane, padle
- Cena i wartosc netto
- Kilometry trasy
- Godziny: wyjazd, zaladunek, przyjazd

### 10.5. Zakladka "HACCP"

Trasowanie miedzydzialowe - dane o przeplywach surowca miedzy dzialami:
- Z jakiego dzialu i jakiego artykulu
- Na jaki dzial i jaki artykul docelowy
- Partie zrodlowa i docelowa
- Suma kilogramow
- Zakres dat

### 10.6. Zakladka "Timeline"

Chronologiczna lista wszystkich zdarzen partii:
- Otwarcie partii
- Zamkniecie partii
- Operacje wagowe
- Pomiary temperatury
- Kontrole jakosci
- Zdjecia
- Transport

Kazde zdarzenie ma ikone, czas i opis.

---

## 11. Porownanie dostawcow

Funkcja pozwalajaca porownac wyniki roznych dostawcow w wybranym okresie.

### Jak otworzyc:
1. W widoku "Lista partii" kliknij przycisk **"Dostawcy"** (fioletowy)
2. Otworzy sie okno "Porownanie dostawcow" dla aktualnie wybranego zakresu dat

### Co widac w tabeli:

| Kolumna | Opis |
|---|---|
| **ID Dost.** | Identyfikator dostawcy |
| **Dostawca** | Nazwa dostawcy |
| **Partii** | Ile partii dostarczyl w okresie |
| **Sr. wydajnosc** | Srednia wydajnosc procentowa (domyslne sortowanie malejaco) |
| **Sr. kl. B** | Sredni procent klasy B |
| **Sr. temp** | Srednia temperatura rampa |
| **Suma kg** | Laczna waga netto |
| **Suma szt** | Laczna ilosc sztuk |
| **Sr. padle** | Srednia ilosc padlych |

### Do czego sluzy:
- Identyfikacja najlepszych dostawcow (wysoka wydajnosc, niska klasa B)
- Identyfikacja problematycznych dostawcow (niska wydajnosc, wysoka temperatura, duzo padlych)
- Podejmowanie decyzji o wspolpracy

---

## 12. Panel alertow

Panel alertow pojawia sie na dashboardzie "Produkcja dzis" w prawym dolnym rogu, gdy system wykryje problemy.

### Rodzaje alertow:

**ERROR (czerwony):**
- Temperatura poza norma (np. >4 C na rampie, gdy norma MaxWartosc=4)

**WARNING (zolty):**
- Partia otwarta >3h bez wazen (moze cos nie tak)
- Brak swiadectwa weterynaryjnego dla aktywnej partii
- Klasa B poza norma

**INFO (niebieski):**
- Informacje niewymagajace natychmiastowej akcji

### Jak dzialaja alerty:
- Generowane automatycznie co 30 sekund (przy kazdym odswiezeniu)
- Bazuja na aktualnych danych partii i normach z tabeli QC_Normy
- Liczba alertow wyswietlana jest w naglowku panelu i w stopce dashboardu
- Panel jest ukryty jesli nie ma zadnych alertow

---

## 13. Sparkline - mini wykres produkcji

Sparkline to maly, fioletowy wykres liniowy widoczny na kartach aktywnych partii na dashboardzie.

### Co pokazuje:
- Os X = godziny dnia (od pierwszego wazenia do ostatniego)
- Os Y = skumulowana produkcja w kilogramach (0 = start, max = aktualny poziom)
- Linia rosnie w gore w miare postepowania produkcji

### Jak czytac:
- **Rownomierny wzrost** = produkcja idzie plynnie
- **Plaskie odcinki** = przestoje w produkcji
- **Gwaltowny wzrost** = duze partie ważone na raz
- **Brak sparkline** = za malo danych (mniej niz 2 punkty godzinowe)

### Skad dane:
System pobiera dane z tabeli Out1A (wazenia produkcyjne) agregowane po godzinach, kumulatywnie.

---

## 14. Szybka zmiana statusu

Na kartach aktywnych partii na dashboardzie znajduje sie maly niebieski przycisk z tekstem np. ">> W produkcji" lub ">> Zaakceptowana".

### Jak dziala:
1. Przycisk jest widoczny tylko jesli partia moze byc awansowana do nastepnego statusu
2. Klikniecie zmienia status o jeden krok do przodu (np. PLANNED -> IN_TRANSIT)
3. Zmiana jest natychmiastowa - bez dialogu potwierdzenia
4. W logach zapisywany jest komentarz "Szybka zmiana z dashboard"
5. Dashboard odswiezy sie automatycznie

### Mozliwe przejscia szybkim przyciskiem:
- PLANNED -> IN_TRANSIT
- IN_TRANSIT -> AT_RAMP
- AT_RAMP -> VET_CHECK
- VET_CHECK -> APPROVED
- APPROVED -> IN_PRODUCTION
- IN_PRODUCTION -> PROD_DONE

### Uwaga:
- Nie mozna cofnac statusu szybkim przyciskiem
- Przejscie do CLOSED wymaga pelnego dialogu zamkniecia (z QC checklist)
- Szybka zmiana jest tez dostepna we flyoucie (panel szczegolow)

---

## 15. Auto-detekcja statusu

System automatycznie aktualizuje statusy partii na podstawie danych w bazie.

### Jak dziala:
Przy kazdym odswiezeniu dashboardu (co 30 sek), system sprawdza aktywne partie:

1. **Jesli jest wpis FarmerCalc** (dane z wagi samochodowej / skupu):
   - Jesli aktualny status < AT_RAMP -> zmienia na AT_RAMP
   - Znaczenie: auto dotarlo i bylo wazone

2. **Jesli jest numer VetNo** (swiadectwo weterynaryjne):
   - Jesli aktualny status < APPROVED -> zmienia na APPROVED
   - Znaczenie: weterynarz zaakceptowal partie

3. **Jesli sa rekordy Out1A** (wazenia produkcyjne):
   - Jesli aktualny status < IN_PRODUCTION -> zmienia na IN_PRODUCTION
   - Znaczenie: rozpoczeto uboj i wazenie

### Wazne zasady:
- Auto-detekcja **nigdy nie cofa** statusu - tylko przesuwa do przodu
- Auto-detekcja nie zmienia statusow zamknietych partii (CLOSED, CLOSED_INCOMPLETE, REJECTED)
- Zmiany sa logowane w PartiaStatus z komentarzem "Auto-detection"

---

## 16. Eksport do Excel

### Jak eksportowac:
1. W widoku "Lista partii" ustaw filtry (daty, dzial, status)
2. Kliknij przycisk **"Excel"** (zielony)
3. Wybierz lokalizacje i nazwe pliku
4. Kliknij "Zapisz"

### Co zawiera eksport:
Plik .xlsx z kolumnami: Partia, Data, Dostawca, Dzial, Status, Szt. dekl., Netto skup, Wydano kg, Przyjeto kg, Na stanie, Wydajn.%, Kl.B %, Temp rampa.

Kolumny sa automatycznie dopasowane do szerokosci zawartosci.

---

## 17. Skroty klawiaturowe

| Skrot | Akcja | Widok |
|---|---|---|
| **F5** | Odswiez dane | Dashboard i Tabela |
| **Ctrl+N** | Nowa partia | Dashboard i Tabela |
| **Escape** | Zamknij flyout | Dashboard |
| **Enter** (w polu szukaj) | Wyszukaj | Tabela |

---

## 18. Typowe scenariusze dziennej pracy

### Scenariusz 1: Poczatek dnia - przeglad harmonogramu

1. Otworz modul Lista Partii (wchodzisz na zakladke "Produkcja dzis")
2. Sprawdz prawy panel **HARMONOGRAM DOSTAW** - ile dostaw zaplanowanych
3. Karta statystyk "Plan dostaw" pokazuje liczbe
4. Poczekaj na przyjazd pierwszego auta

### Scenariusz 2: Auto dotarlo na rampe - tworzenie partii

1. Na dashboardzie znajdz pozycje harmonogramu z badge "NOWA" dla dostawcy, ktory przyjechał
2. Kliknij te pozycje harmonogramu
3. W dialogu sprawdz dane (dostawca powinien byc automatycznie wybrany)
4. Wybierz dzial (zwykle 1A)
5. Kliknij "Utworz partie"
6. Nowa karta pojawi sie w sekcji "AKTYWNE PARTIE"

Alternatywnie, jesli partia nie jest w harmonogramie:
1. Kliknij "+" Nowa partia
2. Recznie wybierz dostawce i dzial
3. Utworz

### Scenariusz 3: Monitorowanie produkcji w ciagu dnia

1. Dashboard odswiezasie co 30 sekund
2. Obserwuj karty aktywnych partii:
   - Sparkline powinien rownomiernie rosnac
   - Wydano kg powinna roshnac
   - Status powinien zmieniac sie automatycznie (auto-detekcja)
3. Sprawdzaj alerty w prawym dolnym rogu:
   - Temperatura poza norma -> natychmiast reaguj
   - Partia bez wazen >3h -> sprawdz co sie dzieje
   - Brak swiadectwa wet. -> skontaktuj sie z weterynarzem

### Scenariusz 4: Szybka zmiana statusu w ciagu dnia

Jezeli auto-detekcja nie zmienila statusu (np. dane jeszcze nie wpadly do bazy):
1. Znajdz karte partii
2. Kliknij maly niebieski przycisk ">> [nastepny status]"
3. Status zmieni sie natychmiast

Przyklad: wiesz ze auto juz jest na rampie, ale FarmerCalc jeszcze nie wplyn:
- Kliknij ">> Na rampie" zeby recznie przesunac status

### Scenariusz 5: Sprawdzenie szczegolow partii

1. Kliknij karte na dashboardzie - otworzy sie flyout ze szczegolami
2. Lub przejdz do zakladki "Lista partii" i kliknij "+" przy wierszu
3. Przegladaj zakladki:
   - Wazenia - szczegolowe operacje wagowe
   - Produkty - sumy po artykule
   - QC - temperatury, wady, zdjecia
   - Skup - dane z FarmerCalc
   - HACCP - trasowanie
   - Timeline - historia zdarzen

### Scenariusz 6: Zamykanie partii na koniec zmiany

1. Upewnij sie ze produkcja z tej partii sie zakonczyla (sparkline sie wyplaszczyl, nowe wazenia nie przybywaja)
2. Opcjonalnie: zmien status na PROD_DONE szybkim przyciskiem
3. Kliknij karte partii -> flyout -> "Zamknij" (lub zaznacz w tabeli i kliknij "Zamknij")
4. W dialogu zamkniecia:
   - Sprawdz metryki (wydano, na stanie, wydajnosc)
   - Jesli "Na stanie" > 10 kg - zobaczysz ostrzezenie - to moze oznaczac ze nie wszystko zostalo przyjete
   - Sprawdz CHECKLIST QC:
     - Czy temperatury zmierzone? (zielony V)
     - Czy w normie? (zielony V lub zolty !)
     - Czy wady ocenione? (zielony V)
     - Czy klasa B w normie? (zielony V lub zolty !)
     - Czy jest swiadectwo wet.? (zielony V)
   - Podsumowanie QC powie, czy zamykasz jako "Zamknieta" czy "Zamknieta z brakami"
   - Dodaj komentarz (opcjonalnie)
5. Kliknij "Zamknij partie"
6. Jesli QC niekompletne, pojawi sie dodatkowe potwierdzenie - zdecyduj czy kontynuowac

### Scenariusz 7: Pomylka - ponowne otwarcie partii

1. Znajdz zamknieta partie (w sekcji "ZAMKNIETE DZIS" na dashboardzie lub w tabeli)
2. Kliknij -> flyout -> "Otworz ponownie" (lub zaznacz w tabeli -> "Otworz")
3. **Musisz podac powod** (np. "Blad w wazeniu, korekta")
4. Kliknij "Otworz"
5. Partia wroci do statusu IN_PRODUCTION

### Scenariusz 8: Analiza tygodniowa - porownanie dostawcow

1. Przejdz na zakladke "Lista partii"
2. Ustaw zakres dat (np. ostatni tydzien lub miesiac)
3. Kliknij "Dostawcy" (fioletowy)
4. W oknie porownania:
   - Sortuj po "Sr. wydajnosc" (domyslnie) zeby zobaczyc najlepszych
   - Sprawdz "Sr. kl. B" - im nizsza, tym lepsza jakosc
   - Sprawdz "Sr. temp" - powinna byc <4 C
   - "Sr. padle" - im mniej, tym lepsza logistyka dostawcy

### Scenariusz 9: Raport do Excel

1. Ustaw filtry (np. ostatni tydzien, dzial 1A)
2. Kliknij "Excel"
3. Zapisz plik
4. Otworz w Excel - mozesz dalej analizowac, robic wykresy, itp.

---

## 19. Slowniczek pojec

| Pojecie | Znaczenie |
|---|---|
| **Partia** | Jedna dostawa drobiu od jednego dostawcy, przetwarzana jako jednostka |
| **Numer partii** | Format RRDDDNNN (rok+dzien roku+numer kolejny) |
| **Dzial** | Dzial produkcyjny: 1A (uboj), 0E (przetwarzanie), 0K (kontrola) |
| **Netto skup** | Waga netto drobiu ze skupu (brutto minus tara) |
| **Wydano** | Kilogramy/sztuki wydane z produkcji (z Out1A) |
| **Przyjeto** | Kilogramy/sztuki przyjete na magazyn (z In0E) |
| **Na stanie** | Roznica miedzy wydano a przyjeto - ile jest "w drodze" |
| **Wydajnosc** | Stosunek wydanego produktu do netto skupu (%) |
| **Klasa B** | Procent produktu nizszej jakosci |
| **Temp rampa** | Temperatura zmierzona na rampie rozladunkowej |
| **QC** | Quality Control - kontrola jakosci |
| **Swiadectwo wet.** | Dokument od lekarza weterynarii potwierdzajacy zdrowie drobiu |
| **Padle** | Ilosc drobiu, ktory padl w transporcie |
| **FarmerCalc** | Tabela z danymi skupu (waga, cena, kierowca, itp.) |
| **Out1A** | Tabela wazen produkcyjnych (wydania z dzialu 1A) |
| **In0E** | Tabela wazen magazynowych (przyjecia na dzial 0E) |
| **Harmonogram** | Plan dostaw na dany dzien (tabela HarmonogramDostaw) |
| **Sparkline** | Mini wykres liniowy pokazujacy trend produkcji godzinowej |
| **Flyout** | Wysuwany panel szczegolow po kliknieciu karty |
| **HACCP** | System kontroli bezpieczenstwa zywnosci - trasowanie miedzydzialowe |
| **Storno** | Korekta/anulowanie wazenia (waga ujemna) |
| **Badge** | Mala kolorowa etykieta z tekstem (np. status) |
| **Normy QC** | Konfigurowalne limity (min/max) dla parametrow jakosciowych |

---

## 20. FAQ - Czesto zadawane pytania

**P: Partie sie nie tworzy - blad**
O: Sprawdz polaczenie z baza (192.168.0.109). Jesli baza jest niedostepna, zobaczysz blad polaczenia. Upewnij sie, ze masz uprawnienia do zapisu.

**P: Nie widze harmonogramu na dzis**
O: Harmonogram pochodzi z tabeli HarmonogramDostaw. Jesli jest pusty, oznacza to, ze nie zostal wypelniony na dzisiejszy dzien. Dane do harmonogramu wprowadzane sa osobnym procesem.

**P: Sparkline sie nie pokazuje**
O: Sparkline wymaga minimum 2 punktow godzinowych z wazen produkcyjnych (Out1A). Jesli partia jest swiezo otwarta lub nie ma jeszcze wazen, sparkline nie bedzie widoczny.

**P: Status nie zmienia sie automatycznie**
O: Auto-detekcja sprawdza: FarmerCalc (->AT_RAMP), VetNo (->APPROVED), Out1A (->IN_PRODUCTION). Jesli dane jeszcze nie wpadly do bazy, uzyj szybkiej zmiany statusu recznie.

**P: Zamknalem partie przez pomylke**
O: Uzyj funkcji "Otworz ponownie" - kliknij zamknieta partie i wybierz "Otworz ponownie". Musisz podac powod.

**P: Dlaczego partia zamknieta jest "z brakami"?**
O: Podczas zamykania system sprawdza checklist QC. Jesli brakuje pomiarow temperatury, oceny wad, lub wartosci sa poza norma, partia jest zamykana jako CLOSED_INCOMPLETE. To nie jest blad - to informacja, ze nie wszystkie kontrole zostaly wykonane.

**P: Dane z tabeli sie nie odswiezaja**
O: Nacisnij F5 lub kliknij "Odswiez". Dashboard odswiezy sie tez automatycznie co 30 sekund. Jesli zmieniles filtry dat, kliknij Enter lub zmien wartosc - dane zaladuja sie automatycznie.

**P: Nie moge rozwinac szczegolow wiersza ("+") - blad**
O: Jesli widzisz blad "object reference not set", upewnij sie, ze uzywasz najnowszej wersji aplikacji z ContentDetailDescriptor. Jesli problem sie powtarza, odswiez widok (F5).

**P: Jak eksportowac dane tylko otwartych partii?**
O: W filtrze "Status" wybierz "Otwarte", potem kliknij "Excel". Eksport zawiera tylko aktualnie widoczne (przefiltrowane) partie.

**P: Alerty sie nie pokazuja, choc powinny**
O: Panel alertow jest widoczny tylko na dashboardzie "Produkcja dzis" i tylko gdy sa aktywne alerty. Alerty generowane sa na podstawie norm z tabeli QC_Normy. Jesli normy nie sa skonfigurowane, niektorzy alerty moga nie dzialac.

---

*Dokument wygenerowany dla modulu Lista Partii Ubojowych v2, aplikacja Kalendarz1/ZPSP.*
