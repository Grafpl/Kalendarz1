# 28 — Decyzja pensji Maja / Paulina / Teresa — brief dla zewnętrznej rozmowy

> **Status:** brief gotowy do wklejenia do rozmowy z Claude/ChatGPT/innym doradcą.
> **Data:** 2026-05-12
> **Autor analizy:** Sergiusz Piórkowski + Claude Code (analiza HANDEL 112 + LibraNet 109)
> **Źródła:** `BAZA_WIEDZY/SELECTY/analiza_*.sql` (uruchamiane w SSMS na 192.168.0.112 i 192.168.0.109)

---

## 1. Kontekst biznesowy — kim są ludzie i co robią

**Firma:** Drobiarski zakład produkcyjny "Piórkowscy" (ZPSP), 258M PLN obrotu rocznie, 200 ton produkcji/dzień. Sprzedaje świeże/mrożone mięso drobiu do Polski i UE.

**Ludzie pod decyzję:**

### Maja Leonard (UserID=6521 w LibraNet, brak konta w HANDEL)
- Handlowiec sprzedaży mięsa, od grudnia 2023 (przedtem: Daniel Czapnik)
- Obecna pensja: **7000 PLN**
- Otrzymała ofertę z zewnątrz: **9000 PLN**
- Sergiusz rozważa: podwyżkę do **10000 PLN** (z parytetem dla Teresy)
- Alternatywa rozważana przez Sergiusza: przeniesienie Mai do działu zakupu żywca (zastępując Paulinę) za niższą pensję

### Paulina Koncka (UserID=1122 w LibraNet)
- Pracuje w zakupie żywca — buduje plan dostaw hodowców w `HarmonogramDostaw`
- Sergiusz słyszał o **konflikcie z Teresą** (zakup żywca robi i Teresa i Paulina razem) i obawia się że Paulina odejdzie
- Pytanie: czy zatrzymać Paulinę? Czy jej rola jest zastępowalna?

### Teresa Jachymczak (UserID=2121 w LibraNet)
- Sprzedaje mięso (jeden z handlowców) ALE jednocześnie **dominuje w zakupie żywca** (50% wpisów w harmonogramie firmy)
- Otrzymałaby parytet 10k jeśli Maja dostanie 10k
- Pytanie: czy obciążenie i wartość uzasadniają parytet?

### Daniel Czapnik (UserID=9998 w LibraNet, ID=32856 w HANDEL)
- Poprzedni handlowiec, którego klientów przejęła Maja
- Już NIE pracuje w firmie (konto nieaktywne)
- Brak danych historycznych w HANDEL (nigdy nie wystawiał faktur)

---

## 2. Co mówią dane: analiza Mai (HANDEL 112 + LibraNet 109)

### 2.1 Sprzedaż HANDEL: era Daniela (2024-04 do 2025-09) vs era Mai (2025-10 do 2026-05)

**Definicje:**
- "Era Daniela" = 18 miesięcy poprzedzających przejęcie przez Maję
- "Era Mai" = 8 miesięcy od kiedy Maja jest handlowcem tych samych klientów
- Klienci = obecni klienci Mai (29 firm), porównujemy ich obrót w obu okresach

| Metryka | Era przed Mają (18 mies.) | Era Mai (8 mies.) | Zmiana |
|---|---|---|---|
| Liczba aktywnych klientów | 48 | 30 | **−37.5%** |
| Liczba faktur | 1409 | 626 | — |
| Suma netto | 82.65M PLN | 26.00M PLN | — |
| **Netto / miesiąc** | **4.59M PLN** | **3.25M PLN** | **−29.2%** |
| Faktur / miesiąc | 78.3 | 78.3 | 0% (identyczne tempo pracy) |
| Mrożone w mixie | 13.4% | **28.1%** | **+14.7 pp** ⭐ |

**Interpretacja:**
- Maja utrzymuje **identyczne tempo pracy** (78 faktur/miesiąc) ale na MNIEJSZYM portfelu klientów
- **Skurczyła portfel** z 48 do 30 klientów (utraciła 18)
- **Spadek obrotu −29.2%** miesięcznie
- **ALE:** przekierowała mix na mrożonki (z 13% na 28%) — wyższe marże

### 2.2 Klienci utraceni (era Daniela → ZERO pod Mają)

Najgorsi:
- **BIMEX Sp.Komandytowa**: 2541k PLN/mies → **196k PLN/mies = −92.3%**. Strata 28M PLN/rok!
- **Wierzejki**: 671k → 144k = **−78.6%**
- **MAT-TEAM Mateusz Blim**: 304k/mies → **ZERO** + 230k PLN przeterminowane 529 dni
- **LECH-DRÓB**: 119k → 0
- **KUHNE & HEITZ HOLLAND**: 111k → 0
- **WOJSZKI**: 185k → 0
- **ESS-FOOD A/S** (2 firmy): 167k → 0
- **MARK'S**: 49k → 0
- **MIR-KAR, NIFICO, MISKAT** — wszyscy → 0

### 2.3 Klienci zbudowani / rozwinięci przez Maję

Top zwycięstwa:
- **SMOLIŃSKI**: 139k → 716k = **+411%** ⭐ (jej numer 1 obecnie)
- **FRESH FROZEN FOOD PARK Kowal**: 20k → 110k = **+436%**
- **STOCZEK NATURA**: 37k → 154k = **+310%**
- **LIMPEX Belgia**: 91k → 222k = **+142%**
- **AGROFOOD POLAND CHMIELNICKI**: 78k → 185k = **+137%**

Klienci CAŁKOWICIE NOWI (nigdy nie kupowali przed Mają):
- **TWÓJ MARKET**: 342k/mies (8 mies. obrotu = 1.71M PLN)
- **SZUBRYT Zakłady**: 449k/mies (ALE pracowali tylko 3 mies. potem przestali)
- **VINK P.ZIĘBA**: 209k/mies
- **IMK DROB**: 112k/mies
- **WIŚNIEWSKI PROGRES**: 28k/mies
- **R-TRADE**: 38k/mies
- **HAGA**: 96k (1× wyjątkowo)
- **AVALON FOODS**: 9k/mies

### 2.4 LibraNet — efektywność zamówień Mai vs benchmark (era Mai)

| Metryka | Maja | Ania | Teresa | Radek |
|---|---|---|---|---|
| Zamówień (8 mies.) | 449 | 2637 | 390 | 165 |
| Wartość zamówień | 18.2M | 50.6M | 23.5M | 3.1M |
| **% Zafakturowane** | **57.91% ✅** | 55.06% | 53.85% | 18.79% |
| **% Anulowane** | **8.46% ✅** | 9.29% | 14.87% ⚠ | 13.33% |
| % E2 (premium) | 25.45% | 58.89% | 70.22% ✅ | 75.11% |
| **% Halal** | **17.28% ✅ UNIKAT** | 0.57% | 0.68% | 7.15% |
| Reklamacje/100 zam | 6.68 ⚠ | 0.11 | 0.26 | 0 |
| Modyfikacje/zam | 8.16 ⚠ | 3.91 | 5.36 | 2.98 |
| Wydano vs Zam | -9.85% | -9.27% | -19.22% ⚠ | -15.09% |
| HHI portfela | **922 ✅ ZDROWY** | 1715 | 4597 ⚠ | brak |

**Maja unikalność strukturalna:**
- **17.28% obrotu w Halal** — JEDYNY handlowiec firmy obsługujący ten segment
- **15.69% eksportu** (Estonia, Belgia, Holandia, Szwecja, Dania, Rumunia) — JEDYNY eksporter
- **28.12% mrożone** — JEDYNY ze znaczącym udziałem (Jola 2.3%, Ania/Teresa/Radek 0%)
- **HHI 922** — najzdrowszy portfel w firmie (Teresa 4597 = uzależnienie od 2 klientów = RADDROB+Ladros)
- **57.91% zafakturowania** — najwyższe (zamówienia faktycznie zamieniają się w pieniądze)
- **8.46% anulacji** — najniższe (solidne planowanie)

**Słabości Mai widoczne w danych:**
- **6.68 reklamacji na 100 zam.** vs Teresa 0.26 (ale 75% to autoimport FKS = realnie ~12 zamiast 30 = 2.7 zam.)
- **8.16 modyfikacji na zamówienie** vs Teresa 5.36 (najwięcej w firmie = niezdecydowanie klienta lub niedopracowane pierwsze wersje)
- **CRM CallReminder**: 100+ otwarć narzędzia, **0 zarejestrowanych telefonów** (otwiera ale nie używa)
- **0 wpisów w `Notatki`** (nie zostawia śladu kontaktu z klientem)

### 2.5 Należności Mai (HANDEL — przeterminowane)

- **MAT TEAM Blim**: 229,796 PLN przeterminowane **529 dni** ⚠ — krytyczne, do windykacji/odpisu
- STOCZEK NATURA: 268,676 PLN przeterminowane 42 dni
- BATISTA: 85,843 PLN przeterminowane 7 dni (po terminie)
- Razem przeterminowane Mai: 737k PLN / 31.6% portfela
- vs Teresa 27.7%, Ania 68% (ale małe kwoty), Jola 48.7%, Ogólne 83.4% (Sergiusza personalnie)

---

## 3. Co mówią dane: Teresa — porównanie pod parytet

| Aspekt | Teresa | Maja |
|---|---|---|
| Wartość portfela | 33.8M PLN (8 mies.) | 26.0M PLN (8 mies.) |
| Klientów | 7 | 30 |
| **HHI (koncentracja)** | **4597 ⚠ WYSOKA** | 922 ✅ |
| Eksport | 0% | 15.69% |
| Halal | 0% | 17.28% |
| Mrożone | 0% | 28.12% |
| % zafakturowane | 53.85% | 57.91% ✅ |
| % anulowane | **14.87% ⚠** | 8.46% ✅ |
| % E2 (premium) | **70.22% ✅** | 25.45% |
| Wydano vs Zam | **−19.22% ⚠** | −9.85% |
| Reklamacje/100 | 0.26 ✅ | 6.68 |
| Modyfikacje/zam | 5.36 | 8.16 ⚠ |
| Top klient | RADDROB Chlebowski (33M PLN, ~1 klient = 60% portfela) | SMOLIŃSKI (22% portfela) |

**Teresa portfolio = uzależnienie od 2 wielkich klientów:**
- RADDROB Chlebowski: 32,906,403 PLN obrotu (12 mies.)
- Ladros: 21,420,997 PLN (12 mies.)
- Razem ~80% jej obrotu = ekstremalne ryzyko

**Teresa jest dodatkowo aktywna w zakupie żywca:**
- **50% wpisów w `HarmonogramDostaw` firmy** (1881 wpisów od 2024-10 — DOMINUJĄCA)
- Konkurencyjna pozycja z Pauliną (zob. niżej)

---

## 4. Co mówią dane: Paulina (zakup żywca)

### 4.1 Skala działania w bazie

| Metryka | Wartość | Kontekst |
|---|---|---|
| Wpisów w HarmonogramDostaw | 972 (od 2024-10) | **25.75% wszystkich w firmie** |
| Sztuk żywca zaplanowanych | 15,045,039 | ~55% tego co Teresa |
| Hodowców obsługiwanych | ~130 (TOP 30 generuje 80% wolumenu) | wszyscy wspólni z Teresą |
| Potwierdzeń wagi po dostawie | 655 | aktywne potwierdzanie |
| Potwierdzeń sztuk po dostawie | 634 | aktywne potwierdzanie |

### 4.2 Trend miesięczny — KRYTYCZNY SYGNAŁ

Liczba wpisów Pauliny w `HarmonogramDostaw` per miesiąc:

```
2024-12: ████████████████████ 123  ← pik (planowanie na 2025)
2025-09: █████████ 55
2025-10: ████████████████ 100
2025-11: ███████████████ 96
2025-12: ████████████████ 100   ← drugi pik
2026-01: █████████████ 77
2026-02: ████████ 49
2026-03: ████████ 48
2026-04: ████ 26                 ← DRASTYCZNY SPADEK
2026-05: ███ 20                  ← do 1/5 normy
```

**Spadek do 20% normalnej aktywności w ostatnich 2 miesiącach.** Może oznaczać:
- Wypalenie / chęć odejścia
- Konflikt eskaluje, Teresa przejmuje zadania
- Urlop (ale nawet wtedy 20 vs 100 to za mało)
- Sygnał że Sergiusz słusznie się martwi

### 4.3 Konflikt Paulina ↔ Teresa — twarde dane

**Brak podziału terytorialnego/branżowego:**
- **130+ hodowców obsługiwani przez OBOJE** (sekcja 11.1 raportu)
- Każdy może wpisać/modyfikować każdą dostawę
- Nie ma reguł kto za kogo odpowiada

**30 dokumentów modyfikowanych przez drugą osobę w ostatnim miesiącu** (sekcja 11.2):

Przykłady poprawiania:
- **Sukiennik Krystyna**: Admin (Sergiusz) stworzył → Paulina modyfikuje 5× w 4 minuty (10:28-10:32 dnia 2026-05-07) — wygląda jak panika/poprawki
- **Saladra Adrian / Bogumiła**: Teresa stworzyła w 2025-06-20 → Paulina modyfikowała 2026-05-07 (po 11 mies.!)
- **Wojciechowski Adam / Paweł, Matusiak Bartłomiej, Król Maciej**: Paulina stworzyła → Teresa/Admin modyfikowali
- **Psuja Ireneusz**: Paulina stworzyła 2026-04-02 → Teresa modyfikowała 2026-05-07

**Interpretacja:** to nie jest spokojny współwłaściciel zadania. To wzajemne "poprawianie" cudzych wpisów = strukturalny konflikt o kompetencje. W przypadku Sukiennik 5 modyfikacji w 4 minuty Paulina prawdopodobnie próbowała naprawiać błąd Admina.

### 4.4 CRM — Paulina prawie nie używa

- **Pozyskiwanie_Hodowcy.PrzypisanyDo**: 0 hodowców formalnie przypisanych do Pauliny (nikt nie używa tej funkcji w firmie!)
- **Pozyskiwanie_Aktywnosci**: tylko 207 wpisów — wszystkie z 02/2026 (113 notatek, 92 zmian statusu) = **jednorazowa akcja**, nie systematyczna praca
- **Konwersja** (StatusPrzed → StatusPo):
  - 37 hodowców "Do zadzwonienia" → "Próba kontaktu" (nieskuteczne telefony)
  - 19 "Do zadzwonienia" → "Obcy kontrakt" (mają już umowę z konkurencją)
  - 6 → "Nie zainteresowany"
  - **TYLKO 2 → "Nawiązano kontakt"** (realnych nowych hodowców pozyskanych = 2)
- **0 udanych telefonów** (UdaneRozmowy=0 we wszystkich miesiącach)

### 4.5 Co Paulina robi solidnie

- **TOP 30 hodowców pokrywa 80% wolumenu** — typowy Pareto, dobrze zorganizowany portfel
- **Średnio 98 dni od stworzenia do potwierdzenia wagi** — odzwierciedla cykl produkcyjny (5-6 tygodni hodowli + harmonogram +30 dni)
- **Pracuje w wtorki (296 wpisów), środy (192), poniedziałki (170)** — typowe godziny biurowe 11-14
- **Niedzielę 56 wpisów** (sic!) — pracuje też w weekendy
- Główni hodowcy: ROL POD Sokołów (1.14M sztuk), Perzyna Tomasz (446k), Wieruszewski (436k), Stróżewski (405k)

---

## 5. Konkretne rekomendacje

### 5.1 Maja → 10 000 PLN: **TAK**, ale z dyscypliną

**Uzasadnienie:**
1. Maja jest **strukturalnie unikalna** — bez niej tracisz cały segment biznesu:
   - **17% Halal** to klienci muzułmańscy których NIKT inny nie obsługuje
   - **16% eksportu** to handel z UE którego NIKT inny nie robi
   - **28% mrożonek** to wyższe marże których NIKT inny nie sprzedaje
2. Ryzyko zastąpienia za 9k przez konkurencję = REALNE (oferta na stole)
3. HHI 922 = najzdrowszy portfel — Maja jest "bezpiecznikiem" jeśli wielki klient Teresy (RADDROB/Ladros) padnie

**Warunki podwyżki — 3 KPI do końca Q3 2026:**

1. **Reaktywować BIMEX albo zamknąć temat:**
   - Historyczny obrót: 45.7M PLN w 18 miesięcy
   - Dzisiaj: 196k/mies = strata 28M PLN/rok
   - Decyzja do 2 tygodni: KTO zerwał umowę? Czy są szanse?
   
2. **MAT-TEAM Blim — windykacja:**
   - 230k PLN przeterminowane 529 dni
   - Albo windykacja albo formalne odpisanie straty
   - Decyzja do 1 miesiąca

3. **Aktywne dzwonienie:**
   - Obecnie: 100+ otwarć CallReminder, 0 zarejestrowanych telefonów
   - Target: 5 telefonów dziennie z notatką w CRM (Notatki/Pozyskiwanie_Aktywnosci)
   - Mierzalne: liczba wpisów w `Pozyskiwanie_Aktywnosci.UzytkownikId='6521'`

### 5.2 Teresa → 10 000 PLN parytet: **TAK warunkowo**

**Uzasadnienie:**
- Wartość 33.8M PLN/8 mies. = większy obrót niż Maja
- ALE: koncentracja HHI 4597 + 50% udział w zakupie żywca = przeciążenie
- Parytet bez dyscypliny = ryzyko że obie pójdą w swoją stronę

**Warunki parytetu:**
1. **Dywersyfikacja portfela** — w ciągu 12 mies. zmniejszyć udział RADDROB+Ladros poniżej 60% (z 80%)
2. **Decyzja: sprzedaż vs zakup** — Teresa nie może robić obu rzeczy dobrze. Albo dystans do zakupu żywca (oddanie Paulinie), albo zmniejszenie portfela sprzedaży
3. **Pakowanie premium 70% E2** — utrzymanie, to jej silna strona

### 5.3 Paulina → ZATRZYMAĆ, ale rozwiązać konflikt z Teresą

**Co mówią dane:**
- Paulina realnie pracuje (25% udziału w harmonogramie, 15M sztuk planowanych, 655 potwierdzeń wagi)
- ALE: spadek aktywności do 1/5 normy w ostatnich 2 mies. + brak podziału z Teresą + wzajemne poprawianie = **konflikt eskaluje**
- Jeśli Paulina odejdzie, Teresa nie da rady przejąć 25% pracy + swoje 50% + sprzedaż mięsa = ryzyko utraty hodowców

**Co zrobić:**
1. **Rozmowa 1:1 z Pauliną** — spytać wprost o sytuację z Teresą i czy myśli o odejściu
2. **Wprowadzić formalny podział** — albo per hodowcy (Paulina/Teresa biorą połowy), albo per region geograficzny, albo per typ umowy (Kontrakt vs Wolnyrynek)
3. **Doprowadzić CRM do porządku**:
   - Zacząć używać `Pozyskiwanie_Hodowcy.PrzypisanyDo` (dziś puste — 0 hodowców przypisanych formalnie!)
   - Włączyć `FarmerCalc.CreatedBy` w aplikacji (dziś też puste — nie wiadomo kto rozlicza)
4. **Paulinie dać dodatkowy budżet/premium** za zakup żywca (jeśli realnie obsługuje 25% pracy firmy) — zamiast podwyżki bazowej, premiowanie efektywności

### 5.4 Pomysł "Maja do zakupu żywca" — NIE

**Dlaczego nie:**
1. Maja generuje 26M PLN/8 mies. obrotu z **unikalnymi segmentami** (Halal/eksport/mrożone). Wyrzucenie tego = strata strategiczna.
2. Maja nigdy nie pracowała w zakupie żywca — krzywa uczenia 6-12 mies., w tym czasie firma traci na obu frontach
3. Zakup żywca wymaga relacji z hodowcami zbudowanych przez lata — Paulina (i Teresa) je mają, Maja nie
4. Pomysł oparty na założeniu "obniżymy koszt handlowca" = krótkowzroczny. Lepiej rozwiązać konflikt Paulina↔Teresa.

**Wniosek:** zostawić Maję w sprzedaży, znaleźć inny sposób na obniżenie kosztów (np. wydzielenie pracownika z Pauliny + premium za rezultat zamiast bazy).

---

## 6. Co nadal nie wiemy (ograniczenia danych)

1. **HANDEL nie ma historycznej atrybucji handlowca** — `CDim_Handlowiec_Val` to CURRENT STATE. Nie da się odpowiedzieć na pytanie "ile Daniel sprzedał w 2024" z bazy. (Daniel ma konto Sage ale 0 wystawionych faktur — faktury wystawia zawsze księgowa RB/MSS).
2. **`FarmerCalc.CreatedBy` jest puste** — nie wiadomo kto rozliczał zakupy żywca. Audyt zakupu jest ślepy.
3. **Motywacje odejścia Pauliny** — dane nie mówią dlaczego. Trzeba pytać.
4. **Personalne relacje** Maja↔Daniel, Paulina↔Teresa — emocje, lojalność, historię — to nie jest w bazie.
5. **Plany ekspansji firmy** — czy Halal/eksport to segmenty które rosną? Czy mrożone jest strategicznym kierunkiem? — kontekst który zmieni wagę liczb.

---

## 7. Pytania do Claude/doradcy

1. Czy podwyżka Mai do 10k jest uzasadniona biorąc pod uwagę spadek tempa −29.2% przy jednoczesnej unikalności segmentów?
2. Jak rozwiązać konflikt Paulina↔Teresa strukturalnie, biorąc pod uwagę że formalnego podziału kompetencji w bazie nie ma?
3. Czy parytet Teresa→10k jest mądry przy HHI 4597 (uzależnienie od 2 klientów)?
4. Co zrobić z BIMEX-em (28M/rok historycznego obrotu, dziś -92%)? Reaktywować czy odpuścić?
5. Czy 5-letnia inwestycja w Maję (rozwój Halal/eksportu) jest strategicznie do utrzymania, czy lepiej zrezygnować i zostawić tylko klasyczną sprzedaż?

---

## Załączniki techniczne (do twarzdej weryfikacji)

Wszystkie skrypty SQL są w `BAZA_WIEDZY/SELECTY/`:
- `analiza_era_daniel_dawid_HANDEL.sql` (uruchom na 192.168.0.112)
- `analiza_paulina_v2_LIBRANET.sql` (uruchom na 192.168.0.109)
- `analiza_maja_HANDEL.sql` + `analiza_maja_LIBRANET.sql` — pełna analiza Mai

Liczby w tym briefie pochodzą z uruchomienia tych skryptów 2026-05-12.
