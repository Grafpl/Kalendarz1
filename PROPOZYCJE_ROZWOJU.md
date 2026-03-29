# Propozycje rozwoju systemu ZPSP
## Ubojnia Drobiu "PIÓRKOWSCY" — Koziołki 40

**Data:** 29.03.2026
**Autor:** Analiza systemu na podstawie kodu źródłowego
**Zakres:** Nowe funkcjonalności dopasowane do procesów biznesowych ubojni drobiu przetwarzającej ~200 ton/dzień

---

## Spis treści

1. [Automatyzacja łańcucha zamówień](#1)
2. [Inteligentne planowanie produkcji](#2)
3. [System wczesnego ostrzegania](#3)
4. [Zaawansowana logistyka i dostawy](#4)
5. [Kontrola jakości i HACCP](#5)
6. [Zarządzanie relacjami z hodowcami](#6)
7. [Optymalizacja magazynu i mroźni](#7)
8. [Finansowa kontrola marży](#8)
9. [Analityka predykcyjna](#9)
10. [Mobilne rozszerzenia](#10)
11. [Automatyzacja komunikacji](#11)
12. [Zarządzanie wiedzą i szkolenia](#12)
13. [Bezpieczeństwo i audyt](#13)
14. [Integracje zewnętrzne](#14)
15. [Panel klienta (B2B)](#15)

---

<a name="1"></a>
## 1. AUTOMATYZACJA ŁAŃCUCHA ZAMÓWIEŃ

### 1.1 Auto-zamówienia powtarzalne

**Problem:** Wielu klientów zamawia te same produkty co tydzień. Handlowiec musi ręcznie tworzyć zamówienia za każdym razem.

**Rozwiązanie:** System "Zamówienia cykliczne" — klient ma szablon zamówienia z harmonogramem (np. co poniedziałek, co środę i piątek). System automatycznie generuje zamówienia z szablonu na X dni do przodu.

**Jak to działa:**
- Nowa tabela `ZamowienieSzablon` — kopia ZamowieniaMieso z polem `Harmonogram` (JSON: dni tygodnia, wyjątki, daty pauzy)
- Codziennie o 5:00 serwis generuje zamówienia na +3 dni
- Handlowiec dostaje powiadomienie "Wygenerowano 12 zamówień — przejrzyj i zatwierdź"
- Klient może mieć wiele szablonów (letni, zimowy, świąteczny)
- Automatyczna korekta ilości na podstawie historii (np. "zwykle zamawia 120 E2 ale ostatnie 4 tygodnie brał 90 — zasugeruj 90")

**Wartość:** Oszczędność ~2h dziennie pracy handlowców. Eliminacja zapomnianych zamówień.

---

### 1.2 Awizacja dostaw z potwierdzeniem SMS

**Problem:** Klient zamawia na konkretną godzinę (np. 14:00), ale nie wiadomo czy rzeczywiście będzie gotowy do odbioru. Transport jedzie na pusto lub czeka pod bramą.

**Rozwiązanie:**
- 2 godziny przed planowaną dostawą: SMS do klienta "Twoja dostawa z Piórkowscy dotrze ok. 14:00. Potwierdź: TAK / PRZESUN / ANULUJ"
- Klient odpowiada SMS → system aktualizuje kurs transportowy
- "PRZESUN" → pyta o nową godzinę → przeorganizowuje trasę
- Logistyk widzi na ekranie statusy potwierdzeń per klient

**Integracja:** Twilio (już w projekcie) + ZamowieniaMieso.DataPrzyjazdu + Kurs/Ladunek

**Wartość:** Redukcja pustych przejazdów o 15-20%. Lepsze planowanie tras.

---

### 1.3 Automatyczne dzielenie zamówień na klasy wagowe

**Problem:** Klient zamawia "500 kg fileta" ale nie precyzuje z jakiej klasy wagowej kurczaka. Handlowiec musi ręcznie rozbijać zamówienie na klasy wagowe dostępne danego dnia.

**Rozwiązanie:** Algorytm automatycznej alokacji:
- Klient zamawia ilość produktu
- System sprawdza prognozę produkcji na dany dzień (klasy wagowe 5-12)
- Automatycznie rozbija zamówienie na dostępne klasy z uwzględnieniem:
  - Preferencji klienta (np. "preferuje klasy 7-9")
  - Priorytetu klienta (A/B/C — klient A dostaje pierwsze wybory)
  - Historii (jakie klasy brał wcześniej)
- Jeśli brakuje — alert "Brak 120 kg klasy 8 na 15.04 — zaproponuj zamiennik?"

**Wartość:** Eliminacja ręcznej alokacji. Sprawiedliwy podział deficytowych klas.

---

### 1.4 Panel zmiany zamówień z workflow

**Problem:** Klient dzwoni o 6:00 rano "zmień mi 200 E2 na 150 i dodaj 50 kg skrzydełek". Handlowiec zmienia w systemie, ale transport/magazyn/produkcja nie wiedzą o zmianie.

**Rozwiązanie:** Każda zmiana zamówienia po godzinie X (np. po 22:00 poprzedniego dnia) generuje:
- Powiadomienie do logistyka (zmiana wpływa na kurs transportowy)
- Powiadomienie do magazynu (zmiana ilości do wydania)
- Powiadomienie do produkcji (jeśli produkt nie jest na stanie)
- Dashboard "Zmiany dnia" z listą zmian, kto zmienił, o której, co dokładnie

**Obecny stan:** Częściowo istnieje w `TransportZmiany` i `HistoriaZmianZamowien` — trzeba scalić w jedno okno z workflow zatwierdzania.

**Wartość:** Zero zagubionych zmian. Pełna kontrola nad procesem.

---

<a name="2"></a>
## 2. INTELIGENTNE PLANOWANIE PRODUKCJI

### 2.1 Bilans dzienny produkcji vs zamówień

**Problem:** Produkcja wytwarza X kg każdego produktu, zamówienia wymagają Y kg. Nikt nie widzi tego w jednym miejscu w czasie rzeczywistym.

**Rozwiązanie:** Dashboard "Bilans dnia":
- Lewa kolumna: produkcja (z wag na liniach, real-time)
- Prawa kolumna: zamówienia (zsumowane z ZamowieniaMieso)
- Środek: różnica (nadwyżka zielona, niedobór czerwony)
- Per produkt: filet, ćwiartka, skrzydła, podroby, etc.
- Alert gdy niedobór > 10% — "Brakuje 340 kg fileta na dziś — rozważ przesunięcie z mroźni lub zmianę zamówień"

**Dane:** `StanyMagazynowe` (produkcja) + `ZamowieniaMieso`/`ZamowieniaMiesoTowar` (zamówienia) + `MG`/`MZ` z Handla (wydania WZ)

**Wartość:** Natychmiastowa widoczność braków. Proaktywne zarządzanie zamiast reagowania.

---

### 2.2 Optymalizacja rozkroju

**Problem:** Z jednego kurczaka klasy 8 (7.5 kg) wychodzi określona ilość fileta, skrzydeł, podrobów. Ale proporcje zamówień klientów nie pokrywają się z naturalnymi proporcjami rozkroju.

**Rozwiązanie:** Symulator rozkroju:
- Input: planowane uboje (ilość sztuk per klasa wagowa)
- Output: prognozowana produkcja per produkt (filet, ćwiartka, skrzydło, etc.)
- Porównanie z zamówieniami na dany dzień
- Identyfikacja "nadwyżek" (np. "wychodzi 2 tony podrobów ale zamówienia to tylko 800 kg — kieruj na mroźnię lub szukaj klienta")
- Identyfikacja "niedoborów" (np. "zamówienia na filet to 5 ton ale rozkrój daje tylko 4.2 tony — anuluj/przesun 800 kg")

**Dane:** `KonfiguracjaProdukty` (skład kurczaka) + `KonfiguracjaWydajnosc` (współczynniki) + prognoza uboju

**Wartość:** Minimalizacja strat. Lepsze planowanie sprzedaży.

---

### 2.3 Predykcja zapotrzebowania na żywiec

**Problem:** Ile kurczaków zamówić od hodowców na przyszły tydzień? Teraz oparte na doświadczeniu.

**Rozwiązanie:**
- Analiza historii zamówień (ostatnie 4-8 tygodni, ten sam dzień tygodnia)
- Uwzględnienie sezonowości (lato = więcej grilli = więcej skrzydeł/udek)
- Uwzględnienie znanych eventów (święta, promocje sieci handlowych)
- Sugerowana ilość uboju na każdy dzień przyszłego tygodnia
- Przeliczenie na ilość żywca do zamówienia od hodowców

**Wartość:** Redukcja nadwyżek i niedoborów żywca o 10-15%.

---

<a name="3"></a>
## 3. SYSTEM WCZESNEGO OSTRZEGANIA

### 3.1 Alerty operacyjne w czasie rzeczywistym

**Problem:** Problemy operacyjne (opóźnienia, braki, awarie) są wykrywane za późno.

**Rozwiązanie:** Panel alertów (toast notifications + dedykowane okno):

**Alerty produkcji:**
- Partia ma status "Ubierana" od > 4h (normalne to 2-3h) → "Partia #456 — ubój trwa zbyt długo"
- Temperatura mroźni > -16°C → "KRYTYCZNE: Mroźnia 2 temperatura -14°C"
- QC wykrył defekt > 5% partii → "Jakość partii #456 poniżej normy"

**Alerty zamówień:**
- Zamówienie na jutro ale produkt nie ma stanu → "Brak 200kg fileta dla klienta DAMAK na jutro"
- Klient przekroczył limit kredytowy → "DAMAK FOOD limit 2M PLN, wykorzystane 1.95M"
- Zamówienie zmienione po deadline → "Zmiana ZAM_4812 o 6:15 — po terminie edycji"

**Alerty transportu:**
- Kierowca nie wyjechał o planowanej godzinie → "Kurs Łódź-Warszawa — brak wyjazdu, plan 6:00"
- Kierowca pominął przystanek → "Kurs #1348: pominięty klient BIEDRONKA Łódź"
- Pojazd w strefie zakazanej → konfigurowalne geofence

**Alerty zaopatrzenia:**
- Hodowca anulował dostawę żywca → "Kowalski Jan — anulacja 15000 szt na 02.04"
- Partia żywca z podwyższoną śmiertelnością → "Partia od Kowalskiego — 3.2% DOA (norma <1.5%)"

**Implementacja:** Timer co 60s sprawdzający warunki alertów, notyfikacje toast w lewym panelu Menu.cs, dedykowany ekran "Centrum alertów"

**Wartość:** Proaktywne reagowanie zamiast gaszenia pożarów.

---

### 3.2 Dashboard "Poranny przegląd"

**Problem:** Każdy dzień zaczyna się od sprawdzania wielu ekranów: zamówienia, produkcja, transport, magazyn, hodowcy.

**Rozwiązanie:** Jeden ekran "Dzień dzisiejszy" pokazujący:

**Sekcja 1 — Produkcja:**
- Planowane partie ubojowe (hodowca, ilość sztuk, godzina przyjazdu)
- Planowana ilość uboju (kg żywca → kg mięsa po rozkroju)
- Status linii produkcyjnych

**Sekcja 2 — Zamówienia:**
- Ile zamówień do zrealizowania
- Ile kg per produkt do wydania
- Ile klientów
- Flagowane: zmiany po deadline, braki stanów

**Sekcja 3 — Transport:**
- Ile kursów zaplanowanych
- Ile pojazdów dostępnych
- Ile kierowców na zmianie
- Status Webfleet: pojazdy w bazie vs w trasie

**Sekcja 4 — Alertyy:**
- Czerwone: krytyczne (braki, opóźnienia)
- Żółte: ostrzeżenia (limity, terminy)
- Zielone: informacyjne (nowe zamówienia, powroty)

**Wartość:** 5 minut zamiast 30 minut porannego przeglądu. Nic nie umknie.

---

<a name="4"></a>
## 4. ZAAWANSOWANA LOGISTYKA I DOSTAWY

### 4.1 Automatyczna optymalizacja tras

**Problem:** Logistyk ręcznie ustala kolejność przystanków na kursie. Często nieoptymalna trasa.

**Rozwiązanie:** Algorytm TSP (Travelling Salesman Problem):
- Input: lista adresów klientów na kursie (GPS z KlientAdres)
- Start/koniec: baza ubojni (51.86857, 19.79476)
- Uwzględnienie okien dostawczych (klient A: 8-10, klient B: po 12:00)
- Uwzględnienie pojemności pojazdu (nie przekraczaj 33 palet)
- Output: optymalna kolejność przystanków minimalizująca km
- Przycisk "Optymalizuj trasę" w edytorze kursu → przenumerowuje ładunki

**Implementacja:** Greedy nearest-neighbor jako baseline, opcjonalnie OSRM API do route matrix

**Wartość:** Redukcja km o 10-20%. Oszczędność paliwa. Szybsze dostawy.

---

### 4.2 ETA z powiadomieniem klienta

**Problem:** Klient nie wie kiedy dokładnie przyjedzie dostawa. Dzwoni do biura i pyta.

**Rozwiązanie:**
- Monitor kursów (już zbudowany) oblicza ETA per przystanek
- 30 min przed planowanym dotarciem: SMS do klienta "Dostawa z Piórkowscy dotrze za ok. 30 min"
- Klient może otworzyć link z mapą live (WebView z pozycją pojazdu)
- Po dostawie: SMS "Dostawa zrealizowana o 14:23. Dziękujemy!"

**Wartość:** Profesjonalny serwis. Redukcja telefonów do biura o 50%.

---

### 4.3 Rozliczenie paliwa vs trasa GPS

**Problem:** Kierowca tankuje ale nie wiadomo czy przebieg odpowiada zużyciu. Możliwe nadużycia.

**Rozwiązanie:**
- Webfleet podaje odometer na każdym punkcie GPS
- System oblicza przebieg dzienny: odometer koniec dnia - odometer start
- Porównanie z normą spalania pojazdu (VehicleDetails.SrednieSpalanie)
- Alert gdy faktyczne spalanie > 130% normy → "Pojazd EBR 45LK — spalanie 42L/100km (norma 32L)"
- Raport miesięczny per kierowca/pojazd

**Wartość:** Kontrola kosztów paliwa. Wykrywanie nadużyć.

---

### 4.4 Potwierdzenie dostawy z podpisem

**Problem:** Brak cyfrowego dowodu dostawy. Klient może reklamować "nie dostarczono" a kierowca twierdzi że był.

**Rozwiązanie:**
- Kierowca po dostawie otwiera link na telefonie (WebApp, nie natywna apka)
- Klient podpisuje palcem na ekranie
- Zdjęcie rozładunku (opcjonalne)
- Zapis w bazie: podpis (base64), timestamp, GPS pozycja
- Dostępne w panelu reklamacji jako dowód

**Wartość:** Eliminacja sporów o dostawy. Profesjonalna dokumentacja.

---

<a name="5"></a>
## 5. KONTROLA JAKOŚCI I HACCP

### 5.1 Automatyczny scoring jakości partii

**Problem:** QC manualnie ocenia jakość partii. Brak spójnych metryk do porównania dostawców.

**Rozwiązanie:** Automatyczny wynik jakości 0-100 per partia:
- Śmiertelność DOA (Dead on Arrival): < 0.5% = 100 pkt, > 3% = 0 pkt
- Konfiskaty weterynaryjne: < 1% = 100 pkt, > 5% = 0 pkt
- Jednolitość wagowa: odchylenie standardowe klas wagowych
- Wady QC: % wad (krwotoki, złamania, zmiany barwy)
- Temperatura na przyjeździe: 0-4°C = OK, > 8°C = krytyczne

Wynik ważony → numer partii z oznaczeniem (Zielony/Żółty/Czerwony)
- Ranking dostawców po średnim score
- Trend score per dostawca (poprawia się czy pogarsza?)

**Wartość:** Obiektywne porównanie dostawców. Lepsze negocjacje cenowe z hodowcami.

---

### 5.2 Śledzenie temperatury chłodni

**Problem:** Temperatura mroźni/chłodni jest monitorowana ale nie ma centralnego dashboardu z alertami.

**Rozwiązanie:**
- Odczyt z czujników IoT (jeśli dostępne) lub ręczne wpisy co 2h
- Dashboard z wykresem temperatury 24/7 per komora
- Alert SMS gdy temperatura przekroczy próg
- Raport HACCP: ciągłość łańcucha chłodniczego dla audytu
- Per partia: zapis temperatury w każdym punkcie (przyjęcie, ubój, rozkrój, pakowanie, wydanie)

**Wartość:** Compliance HACCP. Bezpieczeństwo żywności.

---

### 5.3 Traceability — śledzenie partii od pola do klienta

**Problem:** Audytor pyta "pokaż mi drogę tego fileta od hodowcy do klienta". Teraz trzeba przeszukać wiele tabel ręcznie.

**Rozwiązanie:** Ekran "Traceability" — wpisz numer partii lub kod klienta:
- Hodowca → data wstawienia kurcząt → data uboju → numer partii
- Partia → QC wynik → produkty z partii → numery WZ → klienci którzy dostali
- Odwrotnie: klient → WZ → partia → hodowca
- Eksport do PDF dla audytora
- Timeline wizualny (Gantt-like) od pola do talerza

**Dane:** listapartii + PartiaDostawca + FarmerCalc + Haccp + MG/MZ (WZ) + ZamowieniaMieso

**Wartość:** Gotowość na audyt w 2 minuty zamiast 2 godzin.

---

<a name="6"></a>
## 6. ZARZĄDZANIE RELACJAMI Z HODOWCAMI

### 6.1 Portal hodowcy (web)

**Problem:** Hodowca nie ma wglądu w swoje dane. Dzwoni i pyta o rozliczenia, harmonogramy, wyniki.

**Rozwiązanie:** Prosty panel webowy (ASP.NET Blazor lub statyczny) dla hodowcy:
- Login: NIP + hasło
- Widoki:
  - Moje rozliczenia (faktury, płatności, salda)
  - Moje dostawy (harmonogram następnych, historia poprzednich)
  - Wyniki moich partii (QC score, waga, klasy wagowe, śmiertelność)
  - Porównanie z średnią zakładową (anonimowo)
  - Wiadomości od zakładu

**Wartość:** Redukcja telefonów od hodowców o 70%. Profesjonalny wizerunek.

---

### 6.2 Predykcja wagi ubojowej

**Problem:** Hodowca wstawia kurczaki ale nie wiadomo kiedy osiągną optymalną wagę ubojową.

**Rozwiązanie:**
- Rejestracja daty wstawienia + rasa + paszarnia
- Krzywa wzrostu (dane historyczne z WstawieniaKurczaka)
- Prognoza: "Za 38 dni (wstawienie 01.03) kurczaki osiągną 2.4 kg — optymalny ubój 07.04"
- Alert do planisty: "Hodowca Kowalski — gotowe do uboju w przyszłym tygodniu, ~15000 szt"
- Dopasowanie do zapotrzebowania produkcji

**Wartość:** Precyzyjne planowanie uboju. Optymalna waga = najlepszy rozkrój.

---

### 6.3 System oceny i rankingu hodowców

**Problem:** Który hodowca jest najlepszy? Kto konsekwentnie dostarcza słaby żywiec?

**Rozwiązanie:** Dashboard hodowców z metrykami:
- Średnia waga żywca (im bliżej optymalnej tym lepiej)
- Jednolitość wagowa (mniejsze odchylenie = lepiej)
- Śmiertelność DOA
- Konfiskaty weterynaryjne
- Terminowość dostaw
- Score zbiorczy 0-100 z trendem

Ranking: Top 10 / Bottom 10
- Top hodowcy → bonus cenowy
- Bottom hodowcy → program naprawczy lub rezygnacja

**Wartość:** Obiektywne zarządzanie bazą dostawców. Poprawa jakości surowca.

---

<a name="7"></a>
## 7. OPTYMALIZACJA MAGAZYNU I MROŹNI

### 7.1 Rotacja FIFO z alertami

**Problem:** Produkt leży w mroźni ale nikt nie patrzy na datę produkcji. Najstarsze powinny wychodzić pierwsze.

**Rozwiązanie:**
- Każda pozycja magazynowa z datą produkcji
- Przy wydaniu WZ: system sugeruje najstarszą partię (FIFO)
- Alert: "Filet z 15.03 leży 14 dni — priorytet wydania!"
- Dashboard: produkty posortowane od najstarszych
- Automatyczne blokowanie: > 90 dni = "zablokowany, wymagana decyzja jakości"

**Wartość:** Eliminacja przeterminowania. Lepsza rotacja. Mniej strat.

---

### 7.2 Prognoza stanów magazynowych

**Problem:** O 15:00 nie wiadomo ile będzie na stanie wieczorem (po zakończeniu produkcji i wydań).

**Rozwiązanie:**
- Stan obecny (z LiczenieMagazynu)
- Plus: planowana produkcja dzisiejsza (z partii w statusie Krojenie/Mrozenie)
- Minus: zamówienia do wydania (z ZamowieniaMieso na dziś, nie zrealizowane)
- = Prognozowany stan na koniec dnia
- Kolorowanie: zielony (nadwyżka), żółty (na styk), czerwony (brak)

**Wartość:** Natychmiastowa widoczność czy starczy towaru. Szybkie decyzje o przesunięciach.

---

### 7.3 Mapa mroźni

**Problem:** "Gdzie jest ten filet z 15.03?" — szukanie w dużej mroźni to strata czasu.

**Rozwiązanie:**
- Wizualna mapa mroźni (regały, rzędy, półki)
- Każda paleta skanowana przy wstawieniu → przypisanie lokalizacji
- Wyszukiwarka: "filet klasa 8 z 15.03" → "Regał C, Rząd 3, Półka 2"
- Kolorowanie: zielony = świeży, żółty = >7 dni, czerwony = >30 dni

**Wartość:** Redukcja czasu szukania o 80%. Lepsza organizacja przestrzeni.

---

<a name="8"></a>
## 8. FINANSOWA KONTROLA MARŻY

### 8.1 Kalkulacja marży per klient per produkt

**Problem:** Wiadomo ile sprzedajemy ale nie wiadomo ile zarabiamy na konkretnym kliencie/produkcie po uwzględnieniu kosztów transportu i produkcji.

**Rozwiązanie:**
- Przychód: cena sprzedaży × ilość (z WZ/faktury)
- Koszt surowca: koszt kg żywca × współczynnik wydajności produktu
- Koszt transportu: km kursu × koszt/km ÷ ilość klientów na kursie
- Koszt pakowania: E2 vs palety H1, folia
- = Marża netto per klient per produkt

Dashboard:
- Top 10 klientów wg marży (nie obrotu!)
- Bottom 10 — "Na kliencie X tracimy 0.15 PLN/kg po kosztach transportu"
- Trend marży miesięczny per klient

**Wartość:** Prawdziwa rentowność zamiast pozornej. Lepsze decyzje cenowe.

---

### 8.2 Symulator cenowy

**Problem:** "Ile mogę dać rabatu klientowi X i nadal zarobić?"

**Rozwiązanie:**
- Input: klient, produkt, ilość, proponowana cena
- System oblicza: marżę, porównanie z ceną średnią, wpływ na rentowność
- "Przy cenie 12.50 PLN/kg zarabiasz 0.80/kg. Przy 12.00 PLN/kg zarabiasz 0.30/kg. Minimum: 11.70 PLN/kg (break-even)."
- Historia cen dla tego klienta i produktu (trend)

**Wartość:** Świadome decyzje cenowe. Eliminacja sprzedaży poniżej kosztów.

---

<a name="9"></a>
## 9. ANALITYKA PREDYKCYJNA

### 9.1 Prognoza zamówień (AI)

**Problem:** Ile zamówień będzie jutro/za tydzień?

**Rozwiązanie:** Model predykcyjny oparty na:
- Historia zamówień (ostatnie 52 tygodnie)
- Dzień tygodnia (poniedziałek vs piątek — inne wolumeny)
- Sezonowość (lato vs zima, święta, grille)
- Pogoda (upał = mniej kurczaków gotowanych, więcej grillowych)
- Wydarzenia (promocje w sieciach handlowych)

Output: "Prognoza na przyszły wtorek: 180 ton zamówień (+/- 15 ton)"
- Per kategoria produktów
- Porównanie z planem produkcji

**Wartość:** Lepsze planowanie produkcji i zakupów żywca.

---

### 9.2 Analiza trendów cenowych

**Problem:** "Czy cena fileta rośnie czy spada? Kiedy renegocjować kontrakty?"

**Rozwiązanie:**
- Wykres cen sprzedaży per produkt (średnia ważona z WZ) — 12 miesięcy
- Wykres cen zakupu żywca (z PlatnosciHodowcy) — 12 miesięcy
- Spread: marża surowcowa (cena sprzedaży - koszt żywca per kg)
- Alerty: "Spread fileta spadł poniżej 2.00 PLN/kg — 12-miesięczne minimum"
- Porównanie z cenami rynkowymi (dane z PorannyBriefing)

**Wartość:** Strategiczne decyzje cenowe. Lepsze timing negocjacji.

---

### 9.3 Analiza utraconych klientów

**Problem:** Klient przestał zamawiać ale nikt tego nie zauważył na czas.

**Rozwiązanie:**
- System monitoruje regularność zamówień per klient
- Alert: "Klient DAMAK FOOD — 14 dni bez zamówienia (normalnie co 3 dni)"
- Dashboard "Klienci w ryzyku" z listą klientów z spadającymi wolumenami
- Automatyczny reminder do handlowca: "Zadzwoń do DAMAK — brak zamówień od 2 tyg."
- Analiza: ostatnie zamówienie → ostatnia reklamacja → kontakt handlowca

**Wartość:** Ratowanie klientów zanim odejdą. Proaktywna sprzedaż.

---

<a name="10"></a>
## 10. MOBILNE ROZSZERZENIA

### 10.1 Mini-panel zamówień (mobilny WebView)

**Problem:** Handlowiec jest u klienta i nie ma dostępu do pełnego systemu.

**Rozwiązanie:** Lekka strona webowa (WebAPI + HTML):
- Login handlowca
- Lista klientów z ostatnimi zamówieniami
- "Szybkie zamówienie" — kopiuj ostatnie z korektą ilości
- Sprawdź stan magazynu w real-time
- Sprawdź cenę i limit kredytowy klienta

**Wartość:** Handlowiec zamawia od razu u klienta. Szybszy proces.

---

### 10.2 Panel kierowcy (telefon)

**Problem:** Kierowca nie ma listy przystanków na ekranie (brak terminala PRO w pojeździe).

**Rozwiązanie:** WebApp na telefon kierowcy:
- Lista przystanków z adresami i nawigacją (link do Google Maps)
- Przycisk "Dotarłem" / "Odjazd" per przystanek
- Notatki: "klient zamknięty", "zmiana adresu"
- Zdjęcie rozładunku
- Status aktualizowany w systemie (Monitor Kursów)

**Wartość:** Kierowca wie dokąd jedzie. System wie gdzie jest kierowca (nawet bez Webfleet PRO).

---

<a name="11"></a>
## 11. AUTOMATYZACJA KOMUNIKACJI

### 11.1 Automatyczne raporty email

**Problem:** Zarząd chce codziennie otrzymywać podsumowanie. Ktoś musi ręcznie przygotowywać.

**Rozwiązanie:** Codziennie o 18:00 automatyczny email:
- Do zarządu: PDF z podsumowaniem dnia (produkcja, zamówienia, transport, jakość)
- Do handlowców: "Twoi klienci jutro: [lista zamówień]"
- Do logistyka: "Kursy na jutro: [podsumowanie]"
- Do hodowców: "Rozliczenie za ostatni tydzień: [kwota]"

**Integracja:** Outlook (już w projekcie) + QuestPDF (już w projekcie)

**Wartość:** Zero ręcznej pracy raportowej. Informacja dociera automatycznie.

---

### 11.2 Centrum powiadomień (Notification Hub)

**Problem:** Powiadomienia są rozproszone — chat, email, SMS, alerty w systemie.

**Rozwiązanie:** Jeden panel "Powiadomienia" w lewym sidepanelu Menu:
- Wszystkie alerty w jednym miejscu
- Filtrowanie: Moje / Krytyczne / Informacyjne
- Akcje: "Przejdź do" (otwiera odpowiedni moduł), "Odrzuć", "Przypomnij za 1h"
- Historia powiadomień (kto co kiedy zobaczył)
- Badge na kafelku z liczbą nieprzeczytanych

**Wartość:** Nic nie umknie. Centralne miejsce informacji.

---

<a name="12"></a>
## 12. ZARZĄDZANIE WIEDZĄ I SZKOLENIA

### 12.1 Baza wiedzy operacyjnej

**Problem:** Wiedza operacyjna jest w głowach pracowników. Jak ktoś odchodzi — wiedza znika.

**Rozwiązanie:**
- Wiki wewnętrzne z procedurami (jak obsłużyć reklamację, jak zamówić transport, jak zrobić inwentaryzację)
- Instrukcje krok po kroku z screenshotami
- FAQ per moduł systemu
- Wyszukiwarka
- Wersjonowanie (kto zmienił procedurę, kiedy)

**Wartość:** Szybsze onboarding nowych pracowników. Standaryzacja procesów.

---

### 12.2 Rozbudowany system quizów

**Problem:** Quiz Drobiarstwo istnieje ale jest ograniczony do jednej książki.

**Rozwiązanie:**
- Quizy per dział: Produkcja, Sprzedaż, QC, Transport, BHP
- Pytania z realnych sytuacji w zakładzie
- Obowiązkowe quizy przy onboardingu + coroczne odświeżenie
- Ranking pracowników (gamifikacja)
- Certyfikaty po zdaniu (PDF)
- Administrator może dodawać pytania

**Wartość:** Podnoszenie kompetencji. Lepsze wyniki audytów.

---

<a name="13"></a>
## 13. BEZPIECZEŃSTWO I AUDYT

### 13.1 Pełny audit trail

**Problem:** "Kto zmienił cenę zamówienia 4812 z 12.50 na 11.80?" — brak śladu.

**Rozwiązanie:**
- Centralna tabela audytu: kto, kiedy, co zmienił, stara wartość, nowa wartość
- Automatyczna dla kluczowych tabel: ZamowieniaMieso, Kurs, listapartii
- Przeglądarka audytu per rekord ("pokaż historię zmian tego zamówienia")
- Eksport do PDF dla compliance

**Wartość:** Pełna rozliczalność. Compliance z ISO/HACCP.

---

### 13.2 Zaawansowane uprawnienia

**Problem:** Uprawnienia to prosty bitstring — albo masz dostęp albo nie. Brak uprawnień per akcja (np. "widzi zamówienia ale nie może zmieniać cen").

**Rozwiązanie:**
- Uprawnienia per moduł + per akcja (Odczyt / Edycja / Usuwanie / Zatwierdzanie)
- Role: Administrator, Handlowiec, Logistyk, Magazynier, QC, Zarząd
- Delegacja: "Zastępuję Annę w dniach 01-15.04 — przejmij jej uprawnienia"
- Logi dostępu: kto co otwierał

**Wartość:** Bezpieczeństwo danych. Zgodność z RODO.

---

<a name="14"></a>
## 14. INTEGRACJE ZEWNĘTRZNE

### 14.1 Integracja z wagami przemysłowymi

**Problem:** Dane z wag (przyjęcie żywca, rozkrój, pakowanie) wpisywane ręcznie.

**Rozwiązanie:**
- Bezpośredni odczyt z wag przez RS-232/USB (System.IO.Ports — już w projekcie!)
- Automatyczny zapis do bazy w momencie ważenia
- Eliminacja błędów ludzkich
- Szybsze tempo pracy (brak przepisywania)

**Wartość:** 100% dokładność wag. Szybszy proces.

---

### 14.2 Integracja z GUS/REGON

**Problem:** Przy dodawaniu nowego klienta trzeba ręcznie wpisywać dane firmy.

**Rozwiązanie:**
- Wpisz NIP → automatyczne pobranie z API GUS:
  - Nazwa firmy, adres, REGON, forma prawna
  - Sprawdzenie VAT (biała lista)
- Auto-fill formularza klienta

**Wartość:** Szybsze dodawanie klientów. Brak błędów w danych.

---

### 14.3 Integracja z bankowością

**Problem:** "Czy klient zapłacił fakturę?" — trzeba sprawdzać w banku ręcznie.

**Rozwiązanie:**
- Import wyciągów bankowych (MT940 lub CSV)
- Automatyczne parowanie wpłat z fakturami (po tytule przelewu)
- Dashboard: zaległości per klient
- Alert: "Klient DAMAK — faktura 45000 PLN przeterminowana 14 dni"
- Blokada zamówień: klient z zaległością > 30 dni = blokada nowych zamówień

**Wartość:** Kontrola należności. Mniej złych długów.

---

<a name="15"></a>
## 15. PANEL KLIENTA (B2B)

### 15.1 Portal zamówieniowy online

**Problem:** Klient dzwoni/maila żeby zamówić. Handlowiec wpisuje ręcznie.

**Rozwiązanie:** Portal webowy dla klientów:
- Login: kod klienta + hasło
- Katalog produktów z aktualnymi cenami i stanami
- Koszyk z walidacją (minimum zamówienia, dostępność, limit kredytowy)
- Historia zamówień (kopiuj z historii)
- Status zamówienia (przyjęte → w produkcji → w transporcie → dostarczone)
- Tracking GPS dostawy (link do mapy)
- Faktury do pobrania (PDF)
- Reklamacje online

**Wartość:** Klient zamawia sam 24/7. Odciążenie handlowców. Profesjonalny wizerunek.

---

## PRIORYTETYZACJA

### Natychmiastowy wpływ (1-2 tygodnie implementacji):
| # | Funkcjonalność | Wpływ | Trudność |
|---|---------------|-------|----------|
| 3.1 | Alerty operacyjne | Bardzo wysoki | Średnia |
| 3.2 | Dashboard poranny | Bardzo wysoki | Niska |
| 2.1 | Bilans produkcji vs zamówień | Wysoki | Niska |
| 7.2 | Prognoza stanów magazynowych | Wysoki | Niska |
| 11.1 | Automatyczne raporty email | Wysoki | Niska |

### Średnioterminowy (2-4 tygodnie):
| # | Funkcjonalność | Wpływ | Trudność |
|---|---------------|-------|----------|
| 1.1 | Auto-zamówienia cykliczne | Bardzo wysoki | Średnia |
| 1.2 | Awizacja SMS | Wysoki | Niska |
| 4.1 | Optymalizacja tras | Wysoki | Średnia |
| 4.2 | ETA z SMS | Wysoki | Niska |
| 9.3 | Analiza utraconych klientów | Wysoki | Niska |
| 5.1 | Scoring jakości partii | Wysoki | Średnia |
| 8.1 | Kalkulacja marży | Bardzo wysoki | Średnia |

### Długoterminowy (1-3 miesiące):
| # | Funkcjonalność | Wpływ | Trudność |
|---|---------------|-------|----------|
| 5.3 | Traceability pełne | Bardzo wysoki | Wysoka |
| 6.1 | Portal hodowcy | Wysoki | Wysoka |
| 15.1 | Portal B2B klienta | Bardzo wysoki | Wysoka |
| 9.1 | Prognoza AI zamówień | Wysoki | Wysoka |
| 2.2 | Optymalizacja rozkroju | Wysoki | Średnia |

---

## PODSUMOWANIE

System ZPSP to już bardzo rozbudowana platforma obsługująca cały łańcuch wartości od hodowcy do klienta. Proponowane funkcjonalności skupiają się na trzech filarach:

1. **AUTOMATYZACJA** — eliminacja ręcznej pracy (auto-zamówienia, auto-raporty, auto-alokacja klas wagowych)
2. **WIDOCZNOŚĆ** — dashboardy, alerty, predykcje (bilans dnia, poranny przegląd, ETA)
3. **INTELIGENCJA** — dane → decyzje (scoring dostawców, marża per klient, prognoza popytu)

Każda propozycja jest oparta na istniejących danych i tabelach w systemie — nie wymaga nowych źródeł danych, tylko nowych sposobów ich prezentacji i analizy.
