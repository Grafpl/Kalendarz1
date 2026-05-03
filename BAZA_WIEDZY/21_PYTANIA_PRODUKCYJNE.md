# 21 — Pytania o programy produkcyjne (mega-lista pomysłów)

**Cel pliku:** Zebrać w jednym miejscu **wszystkie pomysły** programów produkcyjnych dla Sergiusza — z kontekstem **dla kogo, z jakich baz, dlaczego**. To jest **lista do rozmowy**, nie do implementacji.

**Format:** Każdy pomysł ma:
- 🎯 **Komu pomaga** (rola + konkretna osoba jeśli wiadomo)
- 📊 **Z jakich baz** (LibraNet / Handel / TransportPL / UNISYSTEM / zewnętrzne)
- ❓ **Pytania do Ciebie** żebyś mi powiedział czy chcesz / jak chcesz
- 🚧 **Blokery techniczne** (jeśli są — np. brak API)

**Sergiuszu:** Przeczytaj. Powiedz `tak / nie / inaczej` przy każdym. Możesz też pisać voice-to-text na końcu pliku w sekcji "Twoje uwagi". Ja potem ten plik przeorganizuję i zacznę z najważniejszymi.

---

# 📋 SPIS POMYSŁÓW (po działach)

## A. BRUDNA STREFA (ubój, patroszenie) — 12 pomysłów
## B. CZYSTA STREFA (klasyfikacja, rozbiór) — 14 pomysłów
## C. MAGAZYN ŚWIEŻYCH + RAMPA — 11 pomysłów
## D. MROŹNIA + SZOKÓWKA — 9 pomysłów
## E. JAKOŚĆ + KAMERY + HACCP — 10 pomysłów
## F. SPRZEDAŻ + CRM KLIENTÓW — 13 pomysłów
## G. ZAKUPY + HODOWCY — 10 pomysłów
## H. TRANSPORT + FLOTA — 11 pomysłów
## I. KSIĘGOWOŚĆ + MARŻA + RAPORTY — 9 pomysłów
## J. HR + KONTROLA GODZIN + KOMUNIKACJA — 11 pomysłów
## K. INFRASTRUKTURA TECHNICZNA — 8 pomysłów

**Razem: ~118 pomysłów do rozmowy.**

---

# A. BRUDNA STREFA (Łukasz Collins, ubój brudna) — 12 pomysłów

## A1. Tablet "Start dnia — Kierownik Uboju" (3:30 rano)

🎯 Łukasz Collins (Kierownik Uboju Brudnej)
📊 LibraNet (`HarmonogramDostaw`, `WstawieniaKurczakow`), UNICARD (kto już przyszedł), AVILOG (auta podjeżdżające)

**Co pokazuje:**
- Lista aut żywca dziś (z `HarmonogramDostaw`) — kolejność, hodowca, sztuki, waga, godzina przyjazdu
- Plan produkcji (żywiec × 78% przelicznik)
- Padłe — przycisk `+N` po znalezieniu martwego kurczaka
- Linia: START / STOP / AWARIA

❓ Czy Łukasz miałby tablet wodoszczelny przy linii? Czy ekran 27" wiszący nad linią?
❓ Co ma się stać po naciśnięciu "AWARIA" — SMS do Sergiusza? Logi w bazie?
❓ Czy "padłe" wpisuje on, czy pracownik z rozładunku?

🚧 Brak na razie — wszystkie dane są w bazie.

---

## A2. Real-time licznik tuszek na linii

🎯 Hala (monitor 65", widoczny dla wszystkich)
📊 **Wymaga API od dostawcy LICZNIKA TUSZEK** (obecnie BRAK)

**Co pokazuje:** "Sztuk dziś: 23 450 / 60 000" + tempo bieżące + sparkline ostatnich 60 min.

❓ Czy dostawca licznika tuszek (kto to jest?) zgodzi się na API/SQL access?
❓ Jeśli nie — czy chcesz **manualny licznik** (Łukasz wpisuje co godzinę)?

🚧 **BRAK API.** Sergiusz prosi dostawcę. Workaround: ręczne wpisy.

---

## A3. Detekcja awarii linii (>5 min stop = SMS)

🎯 Sergiusz + Łukasz
📊 LibraNet (nowa tabela `AwarieLog`) — nie wymaga zewnętrznych systemów

**Logika:** Jeśli licznik tuszek nie tyka się przez 5 min → status "AWARIA" → SMS do Sergiusza.

❓ Czy ma to być automatyczne (z licznika tuszek) czy ręczne klikanie "Awaria"?
❓ Próg: 5 min czy 10 min?

---

## A4. Dziennik awarii i napraw (Meyn Mountaineer)

🎯 Łukasz Collins + Sergiusz (planowanie inwestycji Meyn Maestro)
📊 LibraNet (nowa tabela `AwarieMaszyn`)

**Co rejestruje:** typ awarii, czas trwania, kto naprawił, koszt, wpływ na produkcję (kg utracone).

❓ Czy chcesz mieć tu też **plan przeglądów** (PM = Preventive Maintenance)?
❓ Czy ma się integrować z planem inwestycyjnym Meyn Maestro IX 2026?

---

## A5. Real-time temperatury chłodni (po patroszeniu)

🎯 Justyna + Łukasz
📊 **Wymaga czytników temperatury** (obecnie ręczne pomiary 5x dziennie)

❓ Czy planujesz inwestycję w czytniki IoT?
❓ Jaki budżet na 1 czytnik (sugeruję 200-500 zł)?
❓ Czy ma być integracja z BMS (Building Management System) jeśli istnieje?

🚧 **BRAK fizycznych czytników.** Bez nich tylko ręczne wpisy.

---

## A6. Padłe w transporcie — analityka per hodowca

🎯 Sergiusz + Paulina (zakupy)
📊 LibraNet (`PartiaDostawca`, nowa tabela `PadleTransport`)

**Co pokazuje:** Ranking hodowców po % padłych. Alert: "Hodowca X — 5% padłych w 3 ostatnich dostawach".

❓ Czy to ma być wskaźnik ranking-owy (do wyceny przyszłych dostaw)?
❓ Threshold % padłych który uznajemy za niebezpieczny?

---

## A7. Plan obsady zmiany A (5:00-13:00)

🎯 Łukasz + Sergiusz
📊 UNICARD (kto przyszedł), LibraNet (planowane sztuki)

**Logika:** Z planowanej dostawy (sztuki) + tempa linii (7500/h) wylicza ile pracowników potrzeba. Porównanie z tym kto przyszedł (UNICARD).

❓ Czy chcesz **prognozę zatrudnienia** na podstawie planu produkcji?
❓ Stawka: ile sztuk/h jeden pracownik na zawieszaniu?

---

## A8. Tracking godzin Łukasza i jego ekipy

🎯 HR + Sergiusz
📊 UNICARD (`V_RCINE_EMPLOYEES`)

**Co pokazuje:** Godziny pracy ekipy brudnej + nadgodziny + urlopy.

❓ Już istnieje `KontrolaGodzin` — czy potrzebujesz osobny widok dla brudnej strefy?

---

## A9. Etykietowanie i znakowanie partii

🎯 Hala (zawieszanie + waga)
📊 LibraNet (`listapartii`, `PartiaDostawca`)

**Co robi:** Drukuje etykiety z kodem QR partii (do skanowania w czystej strefie + magazynie).

❓ Czy masz drukarkę etykiet?
❓ Czy klienci by tego potrzebowali (traceability)?

🚧 Wymaga inwestycji w drukarki etykiet + skanery.

---

## A10. Workflow przyjmowania żywca (auto na rampie)

🎯 Łukasz + Paulina
📊 LibraNet (`HarmonogramDostaw`, `WstawieniaKurczakow`)

**Co robi:** Po przyjeździe auta — Łukasz potwierdza w tablecie: numer auta, kierowca, hodowca, wagę bramową, sztuki. Auto przyjmuje status `Przyjęte`.

❓ Skąd waga bramowa — istnieje fizyczna waga przy bramie?
❓ Kto wpisuje sztuki — kierowca czy Łukasz?

---

## A11. Kalkulator efektywności brudnej strefy

🎯 Łukasz + Sergiusz
📊 LibraNet (`In0E`, `WstawieniaKurczakow`)

**KPI:**
- kg żywca / kg tuszki (przelicznik realny vs nominalny 78%)
- sztuk/h (tempo linii vs nominal)
- % padłych
- % awarii

❓ Czy te KPI mają być real-time czy raportowanie codzienne?

---

## A12. Plan przyjęć żywca na 7-30 dni do przodu

🎯 Sergiusz + Paulina
📊 LibraNet (`HarmonogramDostaw`, `WstawieniaKurczakow`, `Pozyskiwanie_Hodowcy`)

**Co pokazuje:** Kalendarz dostaw — który hodowca, kiedy odbiór 35/42 dni od wstawienia, czy AVILOG zaplanował.

❓ Jak wygląda obecny harmonogram — ekran z `HarmonogramDostaw`?

---

# B. CZYSTA STREFA (klasyfikacja, rozbiór) — 14 pomysłów

## B1. Tablet klasyfikatora A/B (zawieszanie)

🎯 Klasyfikatorzy (4-6 osób na zawieszaniu czystej)
📊 LibraNet (nowa tabela `KlasyfikacjaABLog`)

**Po co:** Obecnie klasyfikator tylko podnosi wajchę dla B. Nie zostaje informacja **dlaczego** (krwiak/złamanie/żółć/etc). Ranking hodowców byłby świetny.

**Co robi:**
- Duży **A** zielony przycisk
- **B** + powód (krwiak / złamanie / żółć / oparzenie / inne)
- Auto-licznik dnia: A: 4 521 / B: 312
- Alert: "% klasy B rośnie 18% → 24% — sprawdź partię"
- Ranking hodowców (kto ma najwięcej krwiaków/złamań)

❓ Czy klasyfikatorzy mogą obsłużyć tablet w gumowych rękawicach?
❓ Czy 1-2 sek/sztuka wystarczy żeby kliknąć powód B?
❓ **Kto zarządza tabletami** (kto je czyści, ładuje)?

🚧 **Czeka na WAGO API** — bez tego nie ma synchronizacji z liczbą sztuk.

---

## B2. Real-time % klasy A vs B per partia per hodowca

🎯 Sergiusz + Justyna + Paulina (zakupy)
📊 **Wymaga WAGO API** (obecnie BRAK)

**KPI:** *"Hodowca Stróżewski — 22% klasy B w partii dziś. Średnia 30 dni: 15%. ALERT."*

❓ Czy hodowca ma dostawać auto-SMS gdy % B przekracza próg?

🚧 **BRAK API WAGO.** Bez tego — dane manualne.

---

## B3. Kalkulator rozbioru "Co krojimy dziś"

🎯 Justyna + Teresa (na spotkaniu 13:00)
📊 LibraNet (`listapartii`, `In0E`, `KonfiguracjaWydajnosc`)

**Co robi:**
- Pokazuje ile tuszek dziś (z `In0E`)
- 3 scenariusze: "tylko tuszka" / "krojenie filetowe" / "krojenie ćwiartkowe"
- Dla każdego: kg fileta, ćwiartki, korpusu, skrzydła + szacowana cena
- Decyzja: "JEDZIEMY w scenariusz 2" → broadcast Teams do hali

**Status:** Już istnieje moduł `Krojenie 14A` — pomysł go rozszerzyć.

❓ Czy obecny kalkulator daje to co trzeba?
❓ Czy ma być "wybór jednego scenariusza" czy "porównanie 3 stron-by-strona"?

---

## B4. Monitor "Rozbiór dnia" (czysta strefa, ekran 50")

🎯 Hala czysta strefa (kierownik + pracownicy)
📊 LibraNet (`In0E`, plan produkcji)

**Co pokazuje:**
- Lewa: plan dnia (Filet I: 5800 kg, Ćwiartka: 6600, Skrzydło: 1700, Korpus: 4500)
- Środek: postęp realny vs plan (paski kolorowe)
- Prawa: per pracownik tempo (kg/h fileta)
- Decyzja Dyrektora w trakcie zmiany ("więcej skrzydła") → broadcast

❓ Gdzie postawić monitor? Wodoodporny?
❓ Skąd dane "kg/h fileta" — z RFID przy wadze (brak), czy ręczne?

---

## B5. Wydajność per pracownik czystej strefy

🎯 Sergiusz + Justyna
📊 LibraNet (`In0E.OperatorID`, `Wagowy`), UNICARD (godziny)

**KPI:** kg/h per pracownik (paletujący vs porcjujący).

**Pain point Sergiusza:** *"Nie mogę obliczyć wydajności pracowników brudnej i czystej strefy."*

❓ Czy chcesz to **publicznie** (ranking widoczny) czy tylko dla Sergiusza/Justyny?

---

## B6. Tolerancje wagowe per produkt

🎯 Sergiusz + operatorzy wagowi
📊 LibraNet (`Article` — sprawdzić czy ma kolumny tolerancji), `In0E`

**Po co:** Obecna tolerancja 50 g (`Dokładamy/Niedowaga`) jest **arbitralna**. Trzeba znaleźć tolerancje per produkt w `Article` lub osobnej tabeli.

**Status:** TODO #4 z `18_Analiza_przychodu_szczegoly.md`.

---

## B7. Ranking operatorów wagowych

🎯 Sergiusz + HR
📊 LibraNet (`In0E`)

**Co pokazuje:**
- Liczba ważeń per operator
- % storno (anulacji)
- % "dokładamy" (oddajemy klientowi za darmo) — strata firmy
- Ranking jakości pracy

❓ Czy operatorzy widzą swój ranking? Motywacyjne czy upokarzające?

---

## B8. Decyzja "co produkować" (mielone, polędwiczki, tuba)

🎯 Sergiusz / kierownik rozbioru
📊 Brak — Sergiusz **NIE WIE** kto obecnie decyduje (z `PYTANIA_PRODUKCJA.md` scena 4)

❓ **Kto teraz decyduje?** Trzeba ustalić z Justyną.
❓ Czy chcesz workflow akceptacji (handlowiec → Sergiusz → produkcja)?

---

## B9. Skrawki / odpady — rejestr

🎯 Łukasz + Justyna (BRC/IFS)
📊 LibraNet (`OdpadyRejestr` — istnieje już, sprawdzić użycie)

❓ **Co się dzieje obecnie z odpadami?** Sergiusz: *"NIE WIE."*
❓ Skórki, kości — utylizacja czy karma?

---

## B10. Plan produkcji per zmiana B (14:00-21:00)

🎯 Kierownik II zmiany
📊 LibraNet (`listapartii`, `ZamowieniaMieso`)

**Co pokazuje:**
- Zadania: krojenie, sprzątanie, mycie, załadunki popołud.
- Checklist: co zrobione, kto, kiedy
- Koniec zmiany: raport → mail do Sergiusza+Justyny

❓ Kto jest kierownikiem II zmiany — imię?

---

## B11. Etykietowanie produktów z RADWAG

🎯 Pracownicy czystej strefy (waga platformowa)
📊 **Wymaga API/integracji RADWAG** (obecnie BRAK)

**Co robi:** Po zważeniu pojemnika — automatyczne wpisy do bazy `In0E` (zamiast operator ręcznie).

🚧 **BRAK API RADWAG.** Workaround: drukarka etykiet → ręczne wpisy.

---

## B12. Walidacja partii zamykanej

🎯 Kierownik rozbioru + Justyna
📊 LibraNet (`listapartii.StatusV2`, `QC_Normy`, `QC_Zdjecia`)

**Co robi:** Przed zamknięciem partii — checklist QC (temperatury, klasa B%, przekarmienie, zdjęcia wad). Status V2 → CLOSED tylko gdy wszystko OK.

**Status:** Już zaimplementowane w `Partie/V2`.

❓ Czy lista QC norm jest aktualna w `QC_Normy`?

---

## B13. Mix klas wagowych w pojemniku

🎯 Operator wagi + Justyna
📊 LibraNet (`In0E.QntInCont`)

**Pain point:** `QntInCont = 0` (klasa 0) oznacza zapomnienie operatora **lub mix klas**. Powinno być flagowane.

❓ Czy chcesz **alert dla operatora** "wprowadź klasę przed zważeniem"?
❓ Czy wartość `0` ma być akceptowana czy walidowana?

---

## B14. Raport wad jakościowych z 30 dni

🎯 Justyna + Sergiusz
📊 LibraNet (`QC_Zdjecia`, `vw_QC_WadySkale`)

**Co pokazuje:** Statystyki wad (krwiaki, złamania, oparzenia) per partia per hodowca.

❓ Czy ma być raport e-mail (codziennie) czy tylko on-demand?

---

# C. MAGAZYN ŚWIEŻYCH + RAMPA — 11 pomysłów

## C1. Mapa 2D magazynu świeżych — stan LIVE

🎯 Magazynierzy + kierownik magazynu
📊 LibraNet (`StanyMagazynowe`, `In0E`)

**Co pokazuje:** Pomieszczenie w 2D. Każda paleta jako kwadrat z kolorem (wiek):
- Zielony 0-12h
- Żółty 12-24h
- Czerwony 24h+

❓ Magazyn "bez regałów" — Sergiusz nie zna liczby palet. Czy zacząć od **liczby palet** (CountPalet) bez 2D?

---

## C2. FIFO ranking — co wydać pierwsze

🎯 Magazynierzy
📊 LibraNet (`StanyMagazynowe`, `In0E.Data`, `In0E.Godzina`)

**Co pokazuje:** Lista palet w kolejności FIFO (najstarszy pierwszy) per produkt.

**Pain point:** Obecnie magazynier "wie z głowy" co najstarsze. ZPSP miałby pokazać.

❓ Skąd wziąć datę przyjęcia per paleta? Z `In0E` czy nowa tabela `PaletyAuditLog`?

---

## C3. Tablet "Rampa — Magazynier" (wodoodporny)

🎯 Magazynier na rampie
📊 LibraNet (`ZamowieniaMieso`, `ZamowieniaMiesoTowar`, `DokumentyWZ`)

**Co robi:**
- Lista aut na dziś + status (wjeżdża / na rampie / wyjechał)
- Po wyborze klienta: pozycje do załadowania + checkbox "skompletowane"
- Brakuje? Klik "BRAK" + powód → auto-alert do handlowca
- Auto-WZ + plomba + podpis kierowcy na ekranie

**Status:** Pomysł z `PYTANIA_PRODUKCJA.md` scena 6, Sergiusz: *"Tak"*.

❓ Tablet w jakim formacie? 10" wodoodporny?
❓ Skąd podpis kierowcy — touch screen?

---

## C4. Skanery RFID partii — rozliczenie partii per klient

🎯 Sergiusz (krytyczny pain point)
📊 **Wymaga skanerów RFID + tagów na pojemnikach** (obecnie BRAK)

**Pain point Sergiusza:** *"Wkurza mnie to, że partie kurczaka które wychodzą z magazynu nie są rozliczane."*

**Co robi:** Magazynier skanuje pojemnik → automatycznie wpisuje partie + ilość kg do `DokumentyWZ`.

🚧 **BRAK SKANERÓW.** Inwestycja: skanery (każdy ~3-5 tys. zł), tagi RFID (~2 zł/sztuka).

❓ Czy chcesz inwestycję w RFID? Alternatywa: kody kreskowe (taniej, mniej trwałe).
❓ Czy klienci by tego docenili (lepsza traceability)?

---

## C5. Anatomia jednego zamówienia — workflow

🎯 Wszyscy uczestnicy zamówienia (handlowiec → produkcja → magazyn → fakturzystka → klient)
📊 LibraNet (`ZamowieniaMieso`, `ZamowieniaMiesoTowar`, `HistoriaZmianZamowien`)

**Co pokazuje:** Status pipe:
```
Nowe → Potwierdzone → W produkcji → W magazynie → Załadowane → Faktura → Opłacone
```
Każdy etap: kto, kiedy, co zrobione. Auto-alerty Teams do osób w każdym etapie.

**Status:** Pomysł z `PYTANIA_PRODUKCJA.md` scena 8, Sergiusz: *"Tak"*.

❓ Czy obecny `ZamowieniaMieso.Status` ma już te wszystkie statusy?
❓ Auto-alerty Teams — czy M365 już aktywne?

---

## C6. Tablet kierowcy — odbiór towaru z rampy

🎯 Kierowcy
📊 LibraNet (`ZamowieniaMieso`, `Kurs`, `Ladunek`)

**Co robi:** Kierowca po załadunku potwierdza na tablecie:
- Faktyczna ilość kg per produkt
- Zdjęcie WZ (np. plomby)
- Podpis własny + ewent. kierownika magazynu

❓ Czy kierowcy mają telefon / tablet firmowy?
❓ Aplikacja iOS/Android czy WPF web app?

---

## C7. Bilans dnia LIVE (przychód + stany - zamówienia)

🎯 Sergiusz + Teresa + Justyna
📊 LibraNet (`In0E`, `StanyMagazynowe`, `ZamowieniaMieso`)

**Co pokazuje:** Real-time bilans per produkt:
```
Filet:    przychód +5800 + stan 1200 - zamówienia 6500 = bufor 500 kg ✓
Ćwiartka: przychód +6600 + stan 800  - zamówienia 8000 = bufor -600 kg ⚠️
```

**Pain point:** Sergiusz *"ciężko przewidzieć ile ostatecznie będzie towaru na koniec dnia"*.

❓ Czy bufor 5-6 ton jest globalny (suma wszystkich produktów) czy per produkt?

---

## C8. Alert "Niesprzedane na piątek"

🎯 Handlowcy + Sergiusz
📊 LibraNet (`ZamowieniaMieso`, `StanyMagazynowe`)

**Logika:** W piątek o 12:00 — alert: "Filet 1200 kg niesprzedanych. Mrozić? Czy zostawić?"

**Status:** Task #31 (pending z poprzedniej sesji).

❓ Próg czasowy — 12:00, 13:00, 14:00?
❓ Domyślna akcja jeśli nikt nie odpowie — automatyczne mrożenie?

---

## C9. Liczenie pojemników E2 (cykl)

🎯 Magazynierzy + myjka
📊 LibraNet (nowa tabela `PojemnikiCykl`)

**Co śledzi:**
- Ile E2 w obrocie (na hali, w magazynie, u klientów, w myjce)
- FIFO myjki (najstarszy brudny pierwszy)

**Pain point Sergiusza:** *"Ile mamy pojemników?"* — nie ma takiej liczby w ZPSP.

❓ Czy chcesz to liczyć ręcznie (codziennie ktoś wpisuje) czy w jakiś sposób auto?

---

## C10. Scenariusz nierównowagi: 50/50 produkcji vs 80/20 zamówień

🎯 Handlowcy
📊 LibraNet (`In0E`, `ZamowieniaMieso`)

**Logika:** Gdy produkcja daje 50% mały / 50% duży, a zamówienia 80% duży / 20% mały:
- Auto-propozycja "miks 50/50 dla klientów X, Y, Z"
- Solidarność handlowców (nie zostawiać kolegom mniej pożądanego)

**Status:** Z PROCEDURY_01.

❓ Algorytm proporcjonalny vs FIFO klientów?

---

## C11. Decyzja "tuszka → krojenie → mrożenie"

🎯 Justyna + Teresa (spotkanie 13:00)
📊 LibraNet (`StanyMagazynowe`, `In0E`)

**Co robi:** Po 13:00 — system pokazuje co zostaje. 3 scenariusze:
- Sprzedać świeżą po niższej cenie
- Pokroić (zwiększa szansę sprzedaży)
- Mrozić (-18% wartości, ostateczność)

❓ Kalkulacja kosztu każdej opcji w PLN — z ostatniej ceny / średniej / cennika?

---

# D. MROŹNIA + SZOKÓWKA — 9 pomysłów

## D1. Tablet "Mroźnia — kierownik" (Janek Matusiak)

🎯 Janek Matusiak
📊 LibraNet (nowe tabele `MrozniaSktor` z mapowaniem komór)

**Co pokazuje:**
- 3 mroźnie + szokówka — mapa 3D komór
- Sektory wieku: 0-30 / 30-90 / 90-180 / >180 dni (kolory)
- "Do mrożenia dziś" (z 13:00 spotkania)
- "Do wydania jutro" — automat FIFO
- Alert: "Partia 25034 leży 270 dni — sprawdź"

**Status:** Pomysł z `PYTANIA_PRODUKCJA.md` scena 7, Sergiusz: *"OK"*.

❓ Janek ma tablet czy pracuje "z głowy"?
❓ Mapa 3D — Sergiusz zna układ komór?

---

## D2. Workflow szokówki (24h)

🎯 Janek + magazynierzy
📊 LibraNet (nowa tabela `SzokowkaCykl`)

**Co śledzi:**
- Wsad: data + godzina + co (z `In0E`)
- Wyjmie się: +24h od wsadu (alert)
- Przeładowanie: pojemnik 15kg → 10kg (lepiej się mrozi)

❓ Czy Janek pracuje **codziennie 4-5** czy tylko w piątki?

---

## D3. Inventaryzacja mroźni — co tydzień

🎯 Janek + Justyna
📊 LibraNet (nowa tabela `InwentaryzacjaMroznia`)

**Pain point Sergiusza:** *"Inwentaryzacje są robione co 3 miesiące i zawsze mówią że to przez produkcję."*

**Co robi:** Tablet — Janek przelicza co tydzień stany w mroźni → porównanie z ZPSP → różnice flagowane.

❓ Co tydzień realne czy chcesz **codziennie**?
❓ Czy normy są konfigurowalne (>2% strata = raport)?

---

## D4. Norma straty -18% (in→out)

🎯 Janek + Sergiusz
📊 LibraNet (`StanyMagazynowe`, nowa `MrozniaWaga`)

**Co śledzi:** Wagę przy wsadzie do mroźni vs wagę przy wydaniu. Strata >2% = raport.

---

## D5. Real-time temperatury 3 mroźni + szokówki

🎯 Justyna + Janek
📊 **Wymaga IoT czytników temperatury** (obecnie BRAK)

**Pain point Sergiusza:** *"Nie mam czytników temperatury."*

❓ Inwestycja w czytniki IoT (200-500 zł/sztuka × 5 = ~2500 zł)?
❓ Integracja z BMS jeśli istnieje?

🚧 **BRAK CZYTNIKÓW.** Bez tego ręczne pomiary 5x dziennie (Justyna).

---

## D6. Eksport mrożone — pośrednicy / klienci

🎯 Ania (handlowiec eksportowy) + Sergiusz
📊 LibraNet (`StanyMagazynowe`, `Pozyskiwanie_Hodowcy`?)

**Co pokazuje:** Co w mroźni leży >180 dni — szybki przegląd dla Ania (potencjalna oferta dla pośredników DE/NL/RO).

❓ Cel 2026 — eksport bezpośredni. Czy ma być osobny moduł "Eksport"?

---

## D7. Polibloki — drukowanie etykiet

🎯 Janek + magazynierzy
📊 LibraNet (`In0E`, `listapartii`)

**Co robi:** Po zamknięciu polibloka → drukuje etykietę z partią, datą mrożenia, wagą.

❓ Drukarka etykiet już jest? Jaka?

---

## D8. Czas leżenia w mroźni — alerty

🎯 Janek + Ania
📊 LibraNet (`StanyMagazynowe.DataPrzyjecia`)

**Logika:** Alerty:
- 90 dni: "Sprawdź czy klient eksportowy chce"
- 180 dni: "Obniżamy cenę"
- 270 dni: "Pilnie sprzedać lub utylizacja"

---

## D9. Plan dzienny mroźni (co dziś wkładać, wyjmować)

🎯 Janek
📊 LibraNet (`In0E`, `ZamowieniaMieso` z `Status='Mrożone'`)

**Co pokazuje:** Wieczorne podsumowanie dla Janka:
- Co rano wkłada do szokówki (= wczorajsze niesprzedane)
- Co rano wyjmuje (= sprzed 24h)
- Co dziś idzie do klienta z mroźni (= zamówienia mrożone)

---

# E. JAKOŚĆ + KAMERY + HACCP — 10 pomysłów

## E1. Hala LIVE — widok kamer Hikvision w ZPSP

🎯 Justyna
📊 Hikvision (RTSP — wymaga integracji)

**Co robi:** W ZPSP osobne okno z 4-9 kamerami real-time. Klik kamera → fullscreen.

**Pain point Sergiusza:** *"Justyna zerka w kamery, ale nie chodzi po hali wystarczająco."* Może lepiej widzieć kamery i hale-data jednocześnie.

❓ Ile kamer? Gdzie umieszczone?
❓ RTSP/HLS streaming — pozwoli na to sieć firmowa?

---

## E2. Justyna 5 obchodów (7-9-11-13-15) — checklist

🎯 Justyna
📊 LibraNet (nowa tabela `KontrolaJakosci`)

**Co robi:** O każdej godzinie tablet pokazuje checklist:
- 7:00: Temperatury chłodni/mroźni/hal
- 9:00: Wyrywkowa kontrola produkcji, etykiety, kategorie, kamery
- 11:00: Kontrola krojenia, oznakowanie
- 13:00: Spotkanie handlowe (decyzja)
- 15:00: II zmiana

❓ Czy tablet w ręku przez cały dzień czy stacjonarny?

---

## E3. HACCP — pomiary CCP (Critical Control Points)

🎯 Justyna + Klaudia (asystentka)
📊 LibraNet (`Haccp`)

**Co robi:** Plan CCP per dzień + pomiary + alerty przy odchyleniach.

❓ Tabela `Haccp` istnieje — sprawdzić użycie. Sergiusz: niewiele rekordów.

---

## E4. CAPA register (Działania korygujące i zapobiegawcze)

🎯 Justyna + zarząd
📊 LibraNet (nowa tabela `CAPA_Register`)

**Co śledzi:** Per incydent: co/kto/do kiedy/status.

**Wymóg BRC/IFS.**

---

## E5. Onboarding pracowników agencyjnych — checklist

🎯 Justyna + HR
📊 LibraNet (nowa tabela `Onboarding`)

**Wymóg BRC/IFS:**
1. Karta identyfikacyjna
2. Buty + odzież robocza
3. Szkolenie stanowiskowe + podpis
4. Weryfikacja po 1 tyg + 1 mies.

**Bez podpisu → NIE wchodzi na halę.**

---

## E6. Reklamacje — workflow z auto-importem korekt

🎯 Justyna + handlowcy
📊 LibraNet + HANDEL (auto-import FKS/FKSB/FWK)

**Status:** Już istnieje moduł `Reklamacje/`. Pomysły:
- Filtr "tylko prawdziwe reklamacje" (bez auto-importu) — bo 75% to korekty Symfonii
- Linkowanie reklamacji z partią + hodowcą
- Auto-update rankingu hodowcy

❓ Czy chcesz refactor 4 okien Reklamacji (`FormReklamacja*`)?

---

## E7. BRC/IFS audyt wewnętrzny — checklist

🎯 Justyna
📊 LibraNet (nowa tabela `AudytBRC`)

**Co robi:** Co kwartał — checklist BRC/IFS wymagań → co spełnione, co nie, co naprawić.

---

## E8. Reklamacje per hodowca — ranking

🎯 Sergiusz + Paulina
📊 LibraNet (`PartiaDostawca`, reklamacje)

**Co pokazuje:** Hodowca → liczba reklamacji w 30/90/365 dni.
- "Stróżewski 3 reklamacje — rozmowa"
- Historia per partia (kiedy, co, ile)

---

## E9. Zdjęcia wad QC

🎯 Justyna + Klaudia
📊 LibraNet (`QC_Zdjecia` — istnieje)

**Co robi:** Tablet — Justyna robi zdjęcie wady → linkuje z partią → auto-mail do hodowcy.

❓ Czy obecnie zdjęcia są robione? Gdzie zapisywane?

---

## E10. Audyt mycia (PROCEDURY_08_MYJKA)

🎯 Myjka pojemników + Justyna
📊 LibraNet (nowa tabela `MyjkaAudyt`)

**Co robi:** Per partia E2 z myjki — kto czyścił, kiedy, czy sprawdzona czystość.

**Pain point Sergiusza:** *"Czy ktoś sprawdza czystość pojemników? Nie wiem."*

---

# F. SPRZEDAŻ + CRM KLIENTÓW — 13 pomysłów

## F1. Marża top-down w Dashboardzie Sprzedaży

🎯 Sergiusz
📊 LibraNet + HANDEL (FVS, koszt żywca)

**Algorytm:**
```
Marża = (cena_sprz × ilość) − (cena_żywca × ilość / uzysk_%)
```
Bo `DP.kosztAproksymowany` jest niewiarygodny.

**Status:** Task #30 (pending).

---

## F2. Mobile dla Pani Joli (przez Anię)

🎯 Pani Jola → Ania pośrednik
📊 LibraNet (`ZamowieniaMieso`)

**Po co:** Pani Jola NIE czyta WhatsApp + ma karteczki. Ania pośredniczy. Plan:
- Ania ma tablet → wpisuje zamówienia za Jolę
- Jola dostaje tylko **podgląd** (read-only) — co Ania wpisała
- Akceptacja Joli przez SMS lub kliknięcie

❓ Czy Pani Jola otworzy się na ten flow?
❓ Tablet dla Joli czy tylko Ani?

---

## F3. CRM klientów (Kartoteka Odbiorcy) — usprawnienia

🎯 Handlowcy
📊 LibraNet (`KartotekaOdbiorcyDane`, `KartotekaOdbiorcyKontakty`, `KartotekaOdbiorcyNotatki`, `KartotekaPrzypomnienia`, `KartotekaScoring`)

**Pomysły:**
- Auto-przypomnienia: "Klient X od 14 dni nie zamawiał — zadzwoń"
- Score per klient (płaci dobrze? ile bierze? terminy?)
- Historia rozmów + SMS (`ContactHistory`, `SmsHistory`)

❓ Co jest już w użyciu z tych tabel? Sprawdzę z SELECT-ów.

---

## F4. Dashboard handlowca

🎯 Maja, Ania, Radek, Teresa, Jola
📊 LibraNet (`ZamowieniaMieso`, `KartotekaOdbiorcy*`), HANDEL (FVS)

**Co pokazuje per handlowiec:**
- Zamówienia dziś / tydzień / miesiąc
- Top klienci handlowca
- Cele realizacji
- Przypomnienia telefonów
- Marża per zamówienie

---

## F5. Algorytm proporcjonalnego ucinania

🎯 Handlowcy + Sergiusz
📊 LibraNet (`ZamowieniaMieso`)

**Logika:**
- < 5% odchyłki → zespół ucina sam
- 5-20% → zespół + akcept Sergiusza
- > 20% → tylko Sergiusz

**Status:** W procedurach. Czy już w ZPSP?

❓ Tabela `ZamowieniaMieso.UcinanieStatus`?

---

## F6. Limity kredytowe — workflow

🎯 Fakturzystki + Sergiusz
📊 LibraNet + HANDEL (faktury)

**Logika:**
- 1-3 dni → przypomnienie (auto-mail)
- 7 dni → ostrzeżenie (telefon)
- 14 dni → wstrzymanie sprzedaży (blokada w ZPSP)

❓ Limity kredytowe ustawia ubezpieczyciel — gdzie są przechowywane?

---

## F7. Awansowanie Teresy → Dyrektor Handlowy

🎯 Teresa Jachymczak (przyszły Dyr. Handlowy)
📊 LibraNet (wszystkie tabele Sprzedaż + KontrolaGodzin)

**Co potrzebuje Teresa:**
- Dashboard handlowców (cały zespół)
- Akceptacja ucinania 5-20%
- Akceptacja rabatów
- Zarządzanie spotkaniami 13:00 (zamiast Sergiusza)

❓ Kiedy planowany awans? Jakie kompetencje już ma?

---

## F8. Spadki częstotliwości stałego klienta — alert

🎯 Handlowcy
📊 LibraNet (`KartotekaScoring`, `ZamowieniaMieso`)

**Logika:** Średnia częstotliwość zamówień klienta X = 3 dni. Ostatnie 7 dni cisza → ALERT do handlowca.

**Z procedur:** *"Spadek częstotliwości = SYGNAŁ ALARMOWY"*.

---

## F9. Utrata stałego klienta — raport do Zarządu

🎯 Handlowcy
📊 LibraNet (`KartotekaScoring`)

**Logika:** Klient był stały (>10 zamówień/mies) → przestał (>30 dni cisza) → obowiązek raportu Sergiuszowi w 48h.

---

## F10. Próbne zamówienia — śledzenie

🎯 Handlowcy + Sergiusz
📊 LibraNet (`KartotekaScoring`, `ZamowieniaMieso`)

**Co pokazuje:** Klienci w statusie "Próbne zamówienie" → kiedy zostaną stałymi czy odpadną.

---

## F11. Auto-import faktur korygujących — usprawnienia

🎯 Handlowcy + Justyna
📊 HANDEL (FKS/FKSB/FWK), LibraNet (Reklamacje)

**Pain point:** 75% reklamacji to auto-import. Pomysły:
- Auto-klasyfikacja: "korekta cenowa" vs "korekta ilościowa" vs "reklamacja jakościowa"
- Filtr "tylko prawdziwe reklamacje" w panelach
- Auto-link z handlowcem zamówienia

---

## F12. Polityka cenowa dziś (świeże)

🎯 Handlowcy
📊 LibraNet (`CenaTuszki`, `CenaMinisterialna`, `CenaRolnicza`)

**Co pokazuje:** Cennik na dziś + porównanie ze średnią rynkową + alerty "cena niższa niż próg".

---

## F13. Eksport bezpośredni (omijając pośredników)

🎯 Ania + Sergiusz
📊 LibraNet + HANDEL (cele 2026)

**Co potrzebuje:** Lista klientów eksportowych + ich limity + tracking dostaw + KSeF zagraniczne.

❓ Plan IX 2026 — start eksportu bezpośredniego?

---

# G. ZAKUPY + HODOWCY — 10 pomysłów

## G1. CRM hodowców — usprawnienia

🎯 Paulina (zakupy)
📊 LibraNet (`Pozyskiwanie_Hodowcy` 1874 leads, `Pozyskiwanie_Aktywnosci`)

**Pomysły:**
- Auto-przypomnienia kontaktów (stary lead nie kontaktowany 3 mies.)
- Ranking hodowców (jakość, cena, terminowość)
- Mapa hodowców (geograficznie)
- Filtr leadów "potencjalnie kontraktowi" vs "wolny rynek"

---

## G2. Plan kontraktów (kalendarz wstawień)

🎯 Sergiusz + Paulina
📊 LibraNet (`WstawieniaKurczakow`, `HarmonogramDostaw`)

**Co pokazuje:** Kalendarz na 60 dni — kto wstawia kiedy, ile sztuk, jakie pasze, kiedy odbiór 35/42 dni.

---

## G3. Pasze — śledzenie dostaw

🎯 Paulina + Sergiusz
📊 HANDEL (kategoria 65883), LibraNet

**Co pokazuje:** Dostawy paszy (TASOMIX, De Heus, Ekoplon) per hodowca + per kontrakt.

❓ Czy kontrakt 50/50 jest śledzony w ZPSP? Czy gdzieś indziej (Excel)?

---

## G4. Hodowca — pełna karta

🎯 Paulina + Sergiusz
📊 LibraNet (`Pozyskiwanie_Hodowcy`, `PartiaDostawca`, `FarmerCalc`, kontakty)

**Co pokazuje per hodowca:**
- Dane firmowe + GPS
- Historia dostaw (data, sztuki, klasa B%, reklamacje)
- Wahania jakości
- Pasze które dostał
- Rozliczenia (kg, PLN)

---

## G5. AVILOG integracja

🎯 Paulina + Łukasz
📊 LibraNet (`AvilogHodowcyMapping`)

**Co robi:** AVILOG planuje godziny przyjazdu samochodów. ZPSP wyświetla plan rano w "Start dnia — Łukasz".

❓ Status integracji `AvilogHodowcyMapping`? Jakie dane są mapowane?

---

## G6. Cena żywca — historia 365 dni

🎯 Sergiusz
📊 LibraNet (`CenaMinisterialna`, `CenaRolnicza`, `CenaTuszki`)

**Co pokazuje:** Wykres cen + trendy + porównanie z marżą firmy.

---

## G7. Skup żywca — workflow

🎯 Łukasz (przy bramie) + Paulina
📊 LibraNet (`HarmonogramDostaw`, `WstawieniaKurczakow`, waga bramowa)

**Co robi:**
1. Auto przyjeżdża → potwierdzenie tablet
2. Wagą bramową → waga brutto - tara
3. Sztuki - padłe = sztuki przyjęte
4. Cena z `CenaTuszki` × kg × % = wartość
5. Zatwierdzenie → wpis do `FarmerCalc`

---

## G8. Reklamacja od klienta → ranking hodowcy

🎯 Justyna + Paulina
📊 LibraNet (reklamacje + `PartiaDostawca`)

**Logika:** Reklamacja klienta z partii X → hodowca Y → automat aktualizuje ranking hodowcy.

---

## G9. Hodowcy aktywni vs nieaktywni

🎯 Paulina
📊 LibraNet (`Pozyskiwanie_Hodowcy`, `PartiaDostawca`)

**Co pokazuje:** 140+ rejestrowanych, 40-70 aktywnych. Lista "uśpionych" do reaktywacji.

---

## G10. Ten sam hodowca pod różnymi `CustomerID`

🎯 Sergiusz + Paulina
📊 LibraNet (`PartiaDostawca`)

**Pain point:** Ferma + brat — różne ID, ta sama firma. Trzeba normalizować raport.

❓ Tabela mapowania `HodowcaGroup` (CustomerID → GroupID)?

---

# H. TRANSPORT + FLOTA — 11 pomysłów

## H1. Mapa floty real-time (już istnieje, usprawnienia)

🎯 Sergiusz + koordynator logistyki
📊 WebFleet API + LibraNet

**Status:** Moduł `MapaFloty/` istnieje.

**Pomysły:**
- Alert kierowca off-route (>5 km od trasy)
- Alert opóźnienie (>30 min od planu)
- Real-time czas dojazdu do klienta

---

## H2. Optymalizacja kursów (VRP zamiast greedy)

🎯 Koordynator logistyki + Sergiusz
📊 LibraNet (`Kurs`, `Ladunek`, `ZamowieniaMieso`)

**Algorytm VRP** (Vehicle Routing Problem) zamiast obecnego greedy. Może oszczędzić 5-15% km.

❓ Inwestycja w optimization library (np. OptaPlanner, OR-Tools)?

---

## H3. Aplikacja kierowcy (mobile)

🎯 Kierowcy
📊 LibraNet (`Kurs`, `Ladunek`)

**Co robi:**
- Plan dnia kierowcy (kolejność klientów, godziny awizacji)
- Skanowanie WZ (zdjęcie)
- Podpis klienta
- Auto-update statusu (W trasie / Dostarczono)

❓ iOS/Android — jaki budżet?

---

## H4. Workflow akceptacji zmian transportu

🎯 Logistyk + handlowcy
📊 LibraNet (`TransportZmiany`)

**Status:** Już istnieje (`TransportZmianyService`). Pomysły:
- Auto-akceptacja jeśli różnica <10%
- SMS do handlowca przy zmianie

---

## H5. Czas pracy kierowców (prawo)

🎯 HR + koordynator
📊 WebFleet API + LibraNet (`Kurs`)

**Co śledzi:** Kierowca XXX godzin za kierownicą / dzień, tydzień, miesiąc.

---

## H6. Serwisy pojazdów — plan

🎯 Sergiusz + koordynator
📊 LibraNet (`VehicleServiceLog`, `VehicleDetails`)

**Co pokazuje:**
- Następne serwisy
- Historia napraw
- Koszt utrzymania per pojazd

---

## H7. Ubezpieczenia OC/AC — alerts

🎯 Sergiusz
📊 LibraNet (`VehicleDetails`)

**Co robi:** 30 dni przed wygaśnięciem OC/AC → alert.

---

## H8. Workflow akceptacji zmian dostawców (`DostawcyCR`)

🎯 Sergiusz + Paulina
📊 LibraNet (`DostawcyCR`, `DostawcyCRItem`)

**Status:** Już istnieje (`DostawcyCRItem` z `Status='Proposed'/'Zdecydowany'`). Pomysły:
- Notyfikacje do akceptującego
- Automatyzacja decyzji rutynowych

---

## H9. Awarie pojazdów — log

🎯 Kierowcy + koordynator
📊 LibraNet (nowa `AwariePojazdy`)

**Co śledzi:** Auto X, awaria Y, koszt Z, wpływ na produkcję.

---

## H10. Raport kosztu kursu (PLN per kg dostarczony)

🎯 Sergiusz
📊 LibraNet (`Kurs`, `Ladunek`, kierowca, paliwo)

**Co pokazuje:** Per kurs — koszt (paliwo + kierowca + amortyzacja) / kg dostarczony.

---

## H11. Slot booking dla klientów (samodzielna awizacja)

🎯 Klienci + handlowcy
📊 LibraNet (`ZamowieniaMieso`, web aplikacja)

**Co robi:** Klient sam wybiera slot odbioru z dostępnych. Handlowiec tylko zatwierdza.

❓ Czy klienci by tego chcieli? Inwestycja w portal web.

---

# I. KSIĘGOWOŚĆ + MARŻA + RAPORTY — 9 pomysłów

## I1. Marża dnia LIVE

🎯 Sergiusz
📊 LibraNet + HANDEL (FVS, koszt żywca)

**Co pokazuje real-time:** Przychód dziś - koszt żywca dziś = marża brutto. Per produkt.

---

## I2. Cockpit Sergiusza (1 ekran 4K, 10 KPI)

🎯 Sergiusz (rano i wieczorem)
📊 LibraNet + HANDEL + UNICARD

**Co pokazuje:**
1. Sztuki dziś (plan vs realne)
2. Kg wydane dziś
3. Marża dnia
4. % klasy A vs B
5. Bufor
6. Alerty (kolor czerwony)
7. Lista pracowników na hali
8. Temperatury
9. Reklamacje dziś
10. Anulacje dziś

---

## I3. Raport e-mail wieczorny dla Sergiusza

🎯 Sergiusz (19:00)
📊 LibraNet + HANDEL

**Co robi:** Auto-mail o 19:00 → 5 KPI dnia + alerty + linki do szczegółów. Sergiusz czyta na telefonie 2 min.

---

## I4. Plan vs realizacja (tygodniowo, miesięcznie)

🎯 Sergiusz + Marcin
📊 LibraNet + HANDEL

**Co pokazuje:** Plan tygodniowy / miesięczny vs realizacja. Wykresy + tabele.

**Status:** Częściowo w `AnalizaTygodniowa`.

---

## I5. KSeF — integracja z fakturami

🎯 Fakturzystki + Sergiusz
📊 HANDEL + KSeF API

**Co robi:** Wszystkie FVS auto wysyłane do KSeF.

❓ Status — Symfonia obsługuje KSeF? Czy Sergiusz ogarnia osobno?

---

## I6. Raport HR — koszt pracy per dział

🎯 Sergiusz
📊 UNICARD + ZPSP (HR_*)

**Co pokazuje:** Koszt pracy w PLN per dział per dzień. Etat vs agencja vs Nepalczycy.

---

## I7. Power BI — integracja

🎯 Sergiusz
📊 LibraNet + HANDEL → eksport do Power BI

**Status:** Sergiusz ma `Sprzedaz3.pbix`, `marza.pbix`. Można udostępnić web.

❓ Czy chcesz żeby ZPSP eksportował do Power BI Online?

---

## I8. Raport reklamacji (wartościowy)

🎯 Justyna + Sergiusz
📊 LibraNet (Reklamacje) + HANDEL (FVS)

**Co pokazuje:** Wartość reklamacji w PLN per miesiąc per klient per typ.

---

## I9. Cashflow projection

🎯 Sergiusz
📊 HANDEL (FVS, terminy)

**Co pokazuje:** Prognoza wpływów/wydatków na 30/60/90 dni.

---

# J. HR + KONTROLA GODZIN + KOMUNIKACJA — 11 pomysłów

## J1. KontrolaGodzin — usprawnienia

🎯 HR + Sergiusz
📊 UNICARD + ZPSP HR_*

**Status:** Moduł istnieje (~3100 linii). Pomysły:
- Auto-naliczanie nadgodzin
- Auto-export do PIT-11
- Integracja z eVAT
- Mobile dla pracowników (sprawdzenie godzin)

---

## J2. Wnioski urlopowe — workflow

🎯 Pracownicy + HR + Sergiusz
📊 ZPSP HR_*

**Co robi:** Pracownik wpisuje wniosek → kierownik akceptuje → HR rozlicza.

---

## J3. Plan obsady dnia (kto pracuje gdzie)

🎯 Sergiusz + kierownicy
📊 UNICARD + plan produkcji

**Co pokazuje:** Tablica: pracownik X dziś w hali Y od 5:00 do 13:00.

---

## J4. Konflikty zespołowe — eskalacja

🎯 Sergiusz
📊 LibraNet (nowa tabela `KonfliktyLog`)

**Status:** Sergiusz mediatuje (Jola/Justyna, Teresa/Paulina). Czy chcesz to logować?

❓ Czy ma być formalny mechanizm zgłaszania konfliktów?

---

## J5. Mechanizm "Zgłoś frustrację"

🎯 Wszyscy pracownicy
📊 LibraNet (nowa tabela `FrustracjeLog`)

**Status:** Pomysł Sergiusza z `PYTANIA_PRODUKCJA.md` scena 15.

**Co robi:** Każde okno ZPSP ma ikonkę 🤬 → pracownik klika, opisuje co się nie podoba → tygodniowy raport dla Sergiusza.

---

## J6. Komunikator wewnętrzny (zamiast WhatsApp)

🎯 Wszyscy
📊 LibraNet + Microsoft Teams (planowane M365)

**Status:** Plan migracji WhatsApp → Teams.

❓ Czy ma być budowany w ZPSP czy używać Teams?

---

## J7. Onboarding nowych pracowników (BRC/IFS)

🎯 HR + Justyna
📊 LibraNet (nowa `Onboarding`)

**Wymóg BRC/IFS:**
- Karta + odzież
- Szkolenie + podpis
- Weryfikacja po 1 tyg + 1 mies.
- Bez podpisu → NIE wchodzi.

---

## J8. Awansowanie / planowanie kadrowe

🎯 Sergiusz
📊 ZPSP HR_*

**Pomysły:**
- Plan awansu Teresy → Dyr. Handlowy
- Klaudia → Asystent ds. Jakości
- Kierownik II zmiany — formalizacja

---

## J9. Premie / motywacja per zmiana

🎯 Sergiusz + HR
📊 UNICARD + plan produkcji + jakość

**Co pokazuje:** Per zmiana A/B — wydajność / jakość / premia.

---

## J10. SMS-y do pracowników (przypomnienia)

🎯 HR + kierownicy
📊 LibraNet (`SmsHistory`, `SmsChangeLog`)

**Status:** Tabele istnieją. Co już używa?

---

## J11. Ankieta pracownicza (anonimowa)

🎯 HR + Sergiusz
📊 LibraNet (nowa `Ankiety`)

**Co robi:** Co kwartał — ankieta o satysfakcji pracy. Anonimowa.

---

# K. INFRASTRUKTURA TECHNICZNA — 8 pomysłów

## K1. Migracja LibraNet do SQL Server 2017+

🎯 Sergiusz (jako CTO)
📊 LibraNet

**Po co:** Odzyska `TRY_CONVERT`, JSON support, window functions, `STRING_AGG`.

❓ Plan migracji? Risk?

---

## K2. Connection strings → konfig

🎯 Sergiusz
📊 ZPSP cały kod

**Pain point:** Connection strings hardcoded w klasach. Migracja do `appsettings.json`.

❓ Priorytet? Czy zostawić tak jak jest?

---

## K3. Backupy automatyczne LibraNet + HANDEL

🎯 Sergiusz / IT
📊 SQL Server backups

**Co robi:** Codzienny backup + tygodniowy full + offsite copy.

❓ Status backupów obecnie?

---

## K4. Replikacja LibraNet → DR (disaster recovery)

🎯 Sergiusz
📊 SQL Server replication

**Co robi:** Drugi serwer w innej lokalizacji jako fallback.

❓ Inwestycja: 2-5 tys. zł/rok hosting.

---

## K5. Monitoring SQL Server (alerty wydajności)

🎯 Sergiusz / IT
📊 SQL Server + monitoring (Zabbix, PRTG, Datadog)

**Co robi:** Alert gdy CPU >80%, dysk pełny, deadlock.

---

## K6. Konsolidacja UNISYSTEM + ZPSP HR

🎯 Sergiusz
📊 UNISYSTEM + ZPSP

**Co robi:** Migracja HR_* do bazy LibraNet (jeden serwer 109).

❓ Plan? UNICARD ma osobny serwer?

---

## K7. M365 + Teams migracja

🎯 Sergiusz + zarząd
📊 Microsoft 365

**Status:** Plan 2026.

❓ Kiedy start? Jakie kanały?

---

## K8. CI/CD dla ZPSP

🎯 Sergiusz
📊 GitHub + auto-build

**Co robi:** Auto-build po commicie + auto-deploy na test serwer.

---

# 🌟 PYTANIA OGÓLNE (META-PYTANIA)

## M1. Priorytety
**Z 118 pomysłów — które 3-5 są najważniejsze dla Ciebie w 2026?**

Sugerowana piątka (z dyskusji wcześniejszych):
1. WAGO API (klasy A/B per hodowca)
2. Skanery RFID (rozliczanie partii per klient)
3. Hala LIVE (1 ekran "kto/gdzie/co")
4. Czytniki temperatury IoT
5. Mobile dla Pani Joli (przez Anię)

❓ Zgadza się?

---

## M2. Budżet inwestycyjny

| Inwestycja | Szacowany koszt |
|---|---|
| WAGO/RADWAG API | Nieznane (pertraktacje z dostawcą) |
| Skanery RFID | ~50 tys. zł (skanery + tagi + integracja) |
| Czytniki IoT temperatury | ~5-10 tys. zł |
| Tablety (5 stanowisk) | ~15 tys. zł |
| Drukarki etykiet | ~10 tys. zł |
| Power BI Online | ~500 zł/m-c |
| M365 Teams | ~50 zł/user/m-c |
| **RAZEM** | **~80-100 tys. zł** |

❓ Co możesz przeznaczyć w 2026?

---

## M3. Rozwój ZPSP — wewnętrzny / outsource

**Sergiusz pisze sam** — ale skala 118 pomysłów to **lata pracy** dla jednej osoby.

❓ Czy rozważasz:
- Zatrudnić juniora developera (~80-120k/rok)?
- Outsource konkretnych modułów (~15-30k/moduł)?
- Wziąć agenta AI (Claude Code) do pełnego wykonawstwa?
- Zostawić jak jest (Ty + agent ad-hoc)?

---

## M4. Kolejność wdrażania

**Sugestie startowe (po SELECT-ach z bazy):**

1. **Tydzień 1:** Hala LIVE (UNICARD + StanyMagazynowe)
2. **Tydzień 2-3:** Bilans dnia LIVE
3. **Tydzień 4:** Marża top-down (#F1)
4. **Miesiąc 2:** Workflow akceptacji ucinania
5. **Miesiąc 3:** Mobile dla Pani Joli (przez Anię)

❓ Pasuje?

---

## M5. Co najbardziej Cię irytuje (top 3)?

(Z `16_Frustracje_cele.md`):
1. Justyna nie chodzi po hali wystarczająco
2. Partie z magazynu nie są rozliczane
3. Brak skanerów + brak temperatury real-time

❓ To nadal aktualne? Top priorytet?

---

# ✏️ Twoje uwagi (voice-to-text — wpisuj poniżej)

> [Sergiuszu, tu wpisuj swoje uwagi do każdego pomysłu — `tak / nie / inaczej / kiedy indziej`. Możesz odpowiedzieć selektywnie albo na każdy.]



---

## Następne kroki

1. **Ty:** Uruchom SELECTY z `20_SELECTY_DLA_SSMS.md` w SSMS, wklej wyniki
2. **Ty:** Przeczytaj te 118 pomysłów, zaznacz top 5-10
3. **Ja:** Z wynikami SELECT-ów + Twoimi priorytetami → konkretny plan implementacji najwyższych

**Cel:** Skończyć ten cykl planowania w 1-2 dni, potem **wracamy do kodu** z jasną listą priorytetów.
