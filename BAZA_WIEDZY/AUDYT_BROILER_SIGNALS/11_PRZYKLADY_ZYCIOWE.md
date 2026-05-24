# 11. Przykłady życiowe — każda funkcja z konkretną historią

> Dla każdej z 12 nowych funkcji i 8 ulepszeń: prosty przykład z imionami, kwotami, godzinami. Jeśli `02_NOWE_FUNKCJE.md` była dla dewelopera (model danych, SQL, KPI), to ten plik jest dla Ciebie — żebyś **fizycznie zobaczył** o co chodzi.

---

## NF01 — FPD Scorecard hodowcy

### Przykład życiowy
**Piątek, 25.05.2026, godzina 14:00.**

Maja (zaopatrzenie) siedzi w biurze i planuje kontrakty na następny kwartał. Otwiera **Hodowcę Wojtka Nowaka** w ZPSP.

```
Wojtek Nowak (PL271045)
FPD Index 12 miesięcy: 32  🟢 (TOP 25%)
```

Otwiera **Hodowcę Mazura Kazimierza**.

```
Mazur Kazimierz (PL271089)
FPD Index 12 miesięcy: 145  🔴 (BOTTOM 10%)
WARNING: 3 partie z FPD >120 w ostatnim półroczu
```

**Decyzja Mai**:
- Wojtek: **+1.5% premia** na nowy kontrakt (TOP 25%).
- Mazur: **-3% kara** + rozmowa "Panie Kazimierzu, musimy porozmawiać o ściółce. W zeszłym kwartale 3 razy mieliśmy FPD ponad normę. Albo zmienia Pan ściółkę na trociny zamiast słomy, albo zmniejszamy stocking density z 22 na 18 ptaków/m². Dawne ceny już nie."

**Ile to warte**:
- Hodowca Mazur ma 12 partii/rok × 7000 szt × 2 kg × 7 zł = **1.18 mln zł** obrót.
- Kara 3% = **35 tys. zł** mniej dla Mazura → motywacja.
- Po roku Mazur poprawia ściółkę → FPD spada do 80 → wraca do BASE.
- Twoja korzyść: mniej FPD = mniej skin lesions = mniej trim na linii = **~50-100 tys. zł oszczędności rocznie z tego jednego hodowcy**.

### Bez systemu
Maja **nie wie** który hodowca ma jakie FPD. Decyzja cenowa "z sufitu" lub "bo Józek mówił". Mazur dostaje normalną cenę → robi tak dalej → Twój zakład traci.

---

## NF02 — Antybiotyki + Withdrawal

### Przykład życiowy
**Poniedziałek, 22.05.2026, godzina 08:30.**

Justyna planuje dostawy na wtorek 23.05. Otwiera ZPSP → "Planowanie dnia":

```
Wtorek 23.05.2026 — Dostawy zaplanowane:
  06:00  Wojtek Nowak (PL271045)         7000 szt   ✅
  09:00  Marcin Adamski (PL271066)       6500 szt   ⚠ ANTYBIO BLOK
                                                       Enrofloxacin do 25.05
                                                       Ubój dozwolony od 07.06
  13:00  Janek Stachura (PL271012)       6800 szt   ✅
  16:00  Tomek Kowalski (PL271033)       7200 szt   ✅
```

Klika "Replan" przy Marcinie. System proponuje:
```
Sugestia: Marcin Adamski → przesuń na 08.06.2026 (czwartek)
Alternatywa: Krzysiek Wójcik (PL271099) — gotowy na wtorek, FPD 65 🟢
```

Justyna wybiera Krzyśka. Wysyła email do Marcina przez Gmail MCP (jednym klikiem):
> "Pan Marcinie, Pana partia z 23.05 przesunięta na 08.06 z powodu okresu karencji enrofloksacyny. Potwierdź proszę. Pozdrawiam, Justyna."

**Czas decyzji**: 90 sekund. Bez systemu Justyna spędziłaby 30-60 minut dzwoniąc i sprawdzając w głowie karencje.

### Bez systemu
**Scenariusz koszmarny**: Marcin **zapomniał** powiedzieć o enrofloksacynie. Justyna planuje ubój 23.05. Mięso idzie do Lidla. Lidl robi rutynowy test na residua → wykrywa antybiotyk. Recall całej partii + kara IW + **utrata kontraktu z Lidlem na 6 miesięcy** = **~5-10 mln zł** straconego obrotu.

**Z systemem**: niemożliwe. Marcin musi wpisać kurację w `BS_FarmTreatment`. System blokuje.

---

## NF03 — Transport CCP (welfare index + DOA + temp)

### Przykład życiowy
**Środa, 24.05.2026, godzina 04:30.**

Marek (kierowca) wjeżdża na rampę z partią od hodowcy Wojtka. Justyna otwiera tablet `RampInspectionTablet.xaml`.

System automatycznie pokazuje:
```
Kurs #1042 | Wyjazd: Krasnystaw 02:15 | Wjazd: Koziołki 04:25
Czas transportu: 2h 10min  ✅ (norma <4h)
Max temp w naczepie: 27.3°C  ⚠ HOTSPOT (norma <25°C)
   Odczyty 03:45-04:10 — front_top sensor
```

Justyna ogląda kontener. Liczy:
```
DOA: 5 / 7000 = 0.07%  ✅
Fractures: 2
Trapped: 0
Supine birds: 1
Haematomas: 4
Splayed legs: 0
Crowding score: 1/3
Thermal stress score: 2/3  ⚠ (powiązany z hotspot)
Rejections at ramp: 0
```

System wylicza:
```
Welfare Index: 78/100  🟡 (próg 75 OK, ale obniżony przez hotspot)
```

**Co się dzieje automatycznie**:
- Marek dostaje SMS: "Kurs OK, welfare 78. Premia 50 zł (>=75)."
- Pojazd N-12 dostaje flag "VENTILATION CHECK" — bo to 3 hotspot alert w tym miesiącu.
- Wojtek wieczorem dostaje email: "Twoja partia: PM rejection 0.2%, Welfare 78. Scorecard 12-mies: 82/100."

### Bez systemu
Wczoraj kierowca utknął w korku 2h dłużej. DOA wzrasta o 48%. Justyna policzyła 5 martwych i napisała na karteczce. **Marek nie wie**, Wojtek nie wie, Marcin (kierownik) nie wie. Pojazd N-12 jeździ dalej z popsutą wentylacją. Za 2 tygodnie kolejny incident.

---

## NF04 — Stunning CCP monitor

### Przykład życiowy
**Piątek, 26.05.2026, godzina 11:23.**

Adam (operator linii) zauważa na taśmie po parzelniku **fioletowego ptaka**. Marian (brygadzista) dostaje SMS przed Adam zdąży zawołać:

> "⚠ Purple bird detected — Linia 1, Station 3, 11:23. Confidence 94%. Voltage 105V (target 110-130) ⚠ ZA NISKO. Sprawdź stunner."

Marian patrzy na tablet:
```
Last 10 min:
  Voltage: 105V (target 110-130)  ⚠ ZA NISKO
  Frequency: 200 Hz
  Current: 95 mA (norma min 100)  ⚠
  EU Compliant: NO for last 6 min
```

**Akcja**: Marian zwiększa V do 125V. Sprawdza ostry nóż back-up killera. W 5 minut wszystko OK.

```
11:28 — Voltage 125V ✅ Current 158mA ✅ EU Compliant: YES
Purple birds detected in last 10 min: 0 ✅
```

**Wieczorny raport dla CEO**:
> "1 incident dziś, root cause: voltage drift na stunner #1. Resolved w 5 min. Brak innych incydentów welfare. Compliance EU 1099/2009: 99.97%."

### Bez systemu
Adam zauważa purple bird. Idzie do Mariana, który mówi "zostaw, jest robota". Linia leci dalej. Stunner ma drift voltage przez 4 godziny. Razem **~30 purple birds** (czyli 30 ptaków żywych w gorącej wodzie). Welfare violation. **Auditor BRC** przyjeżdża za 6 miesięcy: "pokaż mi procedurę przy purple bird." → Nie ma. **Major NC, certyfikat zagrożony**.

---

## NF05 — Scalding + Plucking monitor

### Przykład życiowy
**Wtorek, 27.05.2026, godzina 14:20.**

Operator parzelnika zauważa że woda w tanku jest "jakby cieplejsza niż zwykle". Otwiera dashboard:

```
Scalder Tank 2 — ostatnie 60 min:
Setpoint: 53.5°C
Average: 56.8°C  ⚠ Deviation +3.3°C
Alert: 14:00 — manual override przez operatora poprzedniego shift?
```

Klika "audit log":
```
13:50 — Operator Adam K. ustawił setpoint 53.5 → 56.5 ("plucking issue, more heat needed")
13:55 — Linia: skin ruptures count wzrosły z 1.2% do 4.8% ⚠
14:00 — Operator nie wrócił do 53.5
```

**Diagnoza**: poprzedni operator zwiększył temp żeby "naprawić plucker", ale problem był w **starych palcach**, nie w temperaturze. Teraz mamy **scalded meat** (gotowane piersi).

**Akcja**:
- Setpoint z powrotem do 53.5.
- Wymiana 50 starych palców w pluckerze (powinno być 70 dziś, było 0).
- Plucker maintenance log: "wymieniono 50 fingers stations 1-2".

**Strata z tego incydentu**:
- 20 minut scalded meat × 100 ptaków/min × 2 kg × 12 zł = **~48 tys. zł** straty (zamiast pełnej wartości — odrzut lub downgrade).

**Z systemem**: wykryte w 20 minut.
**Bez systemu**: wykryte po fakcie przez Janka na PM (godzina później) lub przez klienta jako reklamacja (tydzień później).

---

## NF06 — PM Defects tablet (Janek z 14 kafelkami)

### Przykład życiowy
**Środa, 28.05.2026, godzina 06:00.**

Janek (weterynarz) zaczyna shift. Bierze tablet z ładowarki. Loguje się. Tablet **sam wie** która partia leci na linii (z PLC):

```
Aktualna sesja: 17
Linia: 1
Partia: 5891 (Wojtek Nowak)
Liczba ptaków zaplanowana: 7000
```

Janek staje na platformie inspekcyjnej. Ptaki lecą po 4500/h (75/min). Janek ma platformę "parzysta" (co drugi ptak).

W pierwszej godzinie klika:
- POLYSER (zapalenie błon) — 4 razy
- ASCITES (wodobrzusze) — 3 razy
- HEPAT (wątroba) — 2 razy
- CELLUL (cellulitis) — 1 raz
- HAEM_EXT (krwiaki) — 2 razy
- WB (wooden breast) — 1 raz
- BCO_FEM (kość) — 0
- Pozostałe — 0

**Tablet pokazuje na bieżąco**:
```
Dziś dla partii 5891:
  Birds: 1250 (50% partii)
  Defects: 13 (1.04%)  ⚠ wzrost
  Top: POLYSER 4, ASCITES 3, HEPAT 2
  Akcja: PARTIAL: 8, COMPLETE: 5
```

O godzinie 08:00 system wysyła alert:
> "⚠ Partia 5891 — rejection rate 1.04% (norma <0.5%). Top: POLYSER. Powiadom Hodowcę Wojtka — możliwy problem z klimatem na fermie."

Maja otwiera profil Wojtka:
```
Wojtek Nowak — Scorecard 12 mies:
  FPD: 32  🟢
  Hock: 6%  🟢
  DOA: 0.18%  🟢
  PM Rejection: 0.42% → 0.65%  🟡 (wzrost!)
  Antybio clean: 90%  🟢
  Reklamacje: 3  🟢
  Score: 82 → 79  ⬇

Trend POLYSER: 0.2% (kw1) → 0.3% (kw2) → 1.2% (dziś)  ⚠⚠
```

**Akcja Mai**: telefon do Wojtka, "Panie Wojtku, mamy nagły wzrost polyser w dziś partii. Czy ostatnio coś się zmieniło na fermie? Wentylacja? Pasza?"

Wojtek: "A tak, wentylator w hali #2 się popsuł w niedzielę, czekam na serwis."

**Diagnoza**: wentylator → wzrost wilgotności → bakterie → polyser u ptaków. Rozwiąż wentylator, problem znika za 1-2 partie.

### Bez systemu
Janek liczy kreseczki na kartce. Wieczorem oddaje. **Tydzień później** ktoś zobaczy że Wojtek miał gorsze partie. Wojtek "nie pamięta" co się działo. Problem trwa miesiącami.

---

## NF07 — Chilling curve + drip loss

### Przykład życiowy
**Czwartek, 29.05.2026, godzina 14:30.**

Linia 1 kończy partię 5891. Tuszki wchodzą do chłodni. Marcin (kierownik produkcji) otwiera `ChillingCurveWindow.xaml`:

```
Sesja #45 — Linia 1, Partia 5891
Start: 14:30  |  Method: AIR + Spray
Probe core temp (insertable, próbka 1/h):

Czas    Temp core   Prognoza time-to-4°C
14:30   38.5°C      Start
15:30   28.0°C      6h 0min  ✅
16:30   18.5°C      5h 45min ✅
17:30   12.0°C      5h 30min ✅
```

Wszystko OK. O godzinie 19:30 partia ma temp <4°C w czasie 5h 15min.

**Wieczorny raport**:
```
Compliance EU 92-116: 100% (cel <6h)
Drip loss test (sample 5 sztuk): średnia 1.3%  ✅ (norma <1.5%)
```

**Jutro 30.05** ekipa pakowania pakuje w MAP. Konsument w Lidlu kupi paczkę → brak wody na dnie tacki → zadowolony.

### Awaryjny scenariusz (NF07 ratuje pieniądze)

**Wtorek, 31.05.2026, godzina 15:00.**

Marcin otwiera dashboard:
```
Sesja #47 — Linia 1, Partia 5895
Start: 14:00  |  Method: AIR + Spray
Probe core temp:
14:00   39.0°C   Start
15:00   35.0°C   Prognoza time-to-4°C: **9h 30min** ⚠⚠
```

**Alert**: temp spada **za wolno**. Chłodnia awaria? Marcin biegnie do chłodni — okazuje się że **kompresor nie pracuje** (od 13:00, nikt nie zauważył).

Marcin wzywa serwis. W 90 minut naprawione. Partia 5895:
- Time_to_4°C: 7h 15min  ⚠ (norma <6h, FAIL)
- Drip loss test: 2.4%  ⚠ (norma <1.5%)

**Akcja**:
- Partia 5895 **NIE może iść** do MAP (drip loss za wysoki, krótszy shelf life). Decyzja: **mrożenie** lub **przetworzenie** (nuggetsy).
- Klient Karmar (który miał dostać 2 palety z 5895) zostaje powiadomiony: "Zamiast partii 5895 dostarczymy partię 5891".
- Awaria chłodni zapisana w log.

**Bez systemu**: partia 5895 idzie do MAP. Klient Karmar dostaje paczki w piątek. Wykrywa drip loss w sobotę. Reklamacja w poniedziałek. **2 dni = drip loss x10 wzrosło**, klient niezadowolony, kara reklamacyjna.

**Z systemem**: Marcin wykrył w 1h, decyzja podjęta świadomie, **klient nie odczul**.

---

## NF08 — Vision Grading A/B/C

### Przykład życiowy
**Wtorek, 02.06.2026, godzina 10:15.**

Linia rozbiórki. Kamera 4K nad linią klasyfikacji. Każda tuszka przechodzi → Claude VLM klasyfikuje w 200ms.

Operator widzi monitor:
```
Vision Grading — Linia rozbioru | Live | Dziś od 06:00

  Klasa A 🟢    Klasa B 🟡    Klasa C 🔴
   3,242         421           18
   88%           11.5%         0.5%

Trend:
06-09: A 90% B 10% C 0.3%
09-12: A 87% B 12% C 0.7%  ⚠ wzrost C
```

**Alert**: o 09:00 zaczął się wzrost klasy C. Operator klika "detale":
```
Top wad w klasie C w ostatniej godzinie:
  • Skin rupture > 5cm: 6 sztuk
  • Pop-out wing: 4 sztuki
  • Hematoma extensive: 3 sztuki
  • Inne: 5
```

**Diagnoza**: pop-out wing wskazuje na **plucker za agresywny** (skrzydła wyrywane z stawu). Operator zwalnia plucker. Klasa C wraca do 0.3%.

**Wartość**:
- Bez vision grading: Janek (weterynarz) na PM by zauważył **po 30 min**, ale nie skojarzyłby z plucker.
- Z vision grading: wykryte w **5 min**, rozwiązane w **15 min**, oszczędność ~**100 sztuk** klasy C × 12 zł = **~2400 zł** za jeden incident.
- Rocznie ~30-50 takich incidentów = **~100 tys. zł oszczędności**.

---

## NF09 — MAP + Traceability + Recall 4h

### Przykład życiowy: Audytor BRC

**Środa, 03.06.2026, godzina 10:00.**

Auditor BRC siedzi z Tobą. Ma skrzynkę z magazynu wybraną losowo. Mówi:

> "Skanuję ten kod QR. Pokaż mi w 4 godzinach: hodowcę, datę uboju, antybiotyki, temp chłodzenia, wyniki Salmonella, klientów którzy dostali inne paczki tej partii."

Skanujesz QR → otwiera się `TraceabilityWindow.xaml`:
```
Paczka: ANTI-TAMPER-UID 7C8F-2A45-...
Partia: 5891
Hodowca: Wojtek Nowak (PL271045)
Data uboju: 17.05.2026
Antybiotyki ostatnie 30 dni: BRAK (clean) ✅
Chill Time_to_4°C: 312 min ✅
PM Rejection: 0.21%
Pathogen Salmonella: NEGATIVE (test 02.05) ✅
Packaging: MAP_CO2_70, ExpiryDate 16.06
Klienci tej partii (7 palet, 245 paczek):
  • Karmar (Warszawa) — 2 palety
  • Lidl Marki — 3 palety
  • ZPC Kraków — 1 paleta
  • Sklep Adamski — 1 paleta
```

Auditor patrzy zegarek: **30 sekund** od skanowania QR.
> "Doskonale. Sek. 3.9 PASS."

### Przykład życiowy: prawdziwy recall

**Piątek, 05.06.2026, godzina 16:30.**

Dzwoni Lidl: "W jednej paczce z partii 5891 znaleźliśmy plastikowy odłamek (1cm). Klient zwrócił. Co robicie?"

**Bez systemu**: panika 3 dni. Próbujesz znaleźć wszystkich klientów. 5 z 8 znajdujesz. 3 = nie wiadomo gdzie poszło. Konieczność wycofania **całej partii** = ~250 tys. zł straty.

**Z systemem**: 
- 16:31 — skanujesz partię 5891 → masz listę wszystkich 8 klientów.
- 16:35 — wysyłka emaili do wszystkich (prepared template).
- 16:45 — telefon do Karmar, ZPC Kraków, Sklepu Adamski (zatrzymują wydanie).
- 17:00 — Lidl dostaje raport "wycofujemy 3 palety z 5 sklepów".
- 17:30 — recall zamknięty.

**Strata**: ~30 tys. zł (zamiast 250 tys.).

---

## NF10 — Salmonella + logistic slaughter

### Przykład życiowy
**Poniedziałek, 08.06.2026, godzina 06:00.**

Briefing rano. W sekcji "Salmonella update":
```
Wyniki overshoe testów ostatnie 24h (3 partie):
  ✅ Wojtek Nowak — NEGATIVE
  ✅ Krzysiek Wójcik — NEGATIVE
  ⚠ Mazur Kazimierz — POSITIVE Salmonella Enteritidis (SE)

Akcja auto:
  Partia Mazura przeniesiona z slot 09:00 → slot 16:00 (ostatni)
  Po uboju Mazura: full cleaning + disinfection 1h
  Mięso z Mazura: oznaczone jako "wymaga heat treatment"
  → idzie do produktów przetworzonych (cooked products), nie świeże
```

**Wartość**:
- Bez systemu: Mazur idzie pierwszy → chłodnia/linia są skażone → wszystkie partie po nim mają ryzyko → klient dostaje świeże mięso z SE → **massive recall**.
- Z systemem: Mazur ostatni → cleaning → SE nie idzie dalej → kontaminacja zamknięta.

**Norma EU**: Salm+ flocks **MUSZĄ** mieć logistic slaughter (ostatni slot dnia + post-clean). Bez tego — major NC.

---

## NF11 — Foreign material (metal, plastic)

### Przykład życiowy
**Wtorek, 09.06.2026, godzina 11:42.**

Metal detector po pakowaniu **alarmuje**. Paczka spada na taśmę "do investigation".

System auto:
```
Alarm #2147 | Linia: pakowanie 1 | Czas: 11:42
Typ: METAL (Fe)
Foto: [link Hikvision frame 11:42:15]
Partia: 5912 (Krzysiek Wójcik)
Status: INVESTIGATING
Operator: Robert
```

Robert otwiera paczkę → znajduje **staple** (zszywkę 0.8cm). Pochodzi prawdopodobnie z plastikowej skrzyni — ktoś przykleil notatkę zszywką (książka mówi: "stapler are not permitted in slaughterhouses").

**Akcja**:
- Skrzynia z zszywką: oznacz "NIE UŻYWAĆ", wycofaj z obiegu.
- Sprawdź wszystkie pozostałe skrzynie pod kątem zszywek (jednorazowa kampania).
- Alarm zamknięty 11:55, akcja udokumentowana.

**BRC v9 sek. 4.9**: każdy alarm metal/X-ray **musi być investigated i zamknięty**. Bez logu = major NC.

---

## NF12 — BRC v9 compliance dashboard

### Przykład życiowy
**Poniedziałek, 15.06.2026, godzina 09:00. Twoja kawa.**

Otwierasz `BRCDashboard.xaml`:
```
BRC v9 Self-assessment 15.06.2026
Łączny score: 87% CONFORMING 🟢
Major NC: 0 | Minor NC: 3 | Conforming: 158/181

Sekcje:
  1. Senior Management  ████████████░░░░  80%  🟡
  2. HACCP              ███████████████░  95%  🟢
  3. QMS                ██████████████░░  90%  🟢
  4. Site Standards     ██████████░░░░░░  65%  🟡 ← najsłabszy
  5. Product Control    █████████████░░░  85%  🟢
  6. Process Control    ████████████████ 100%  🟢
  7. Personnel          ████████████░░░░  75%  🟡

Pilne gaps (3):
  • Sek. 4.7  Maintenance tool tracking — planowana data 2026-09-15
  • Sek. 4.12 Maintenance after-hours — planowana data 2026-10-01
  • Sek. 7.3  Protective clothing color coding — procedura w pisaniu
```

**3 minuty**: wiesz gdzie jest Twój zakład. Przygotowanie do audytu Q3 2027 = **mniej stresu**.

---

## U01 — Reklamacje closed loop

### Przykład życiowy
**Czwartek, 22.05.2026, godzina 13:45.**

Karmar dzwoni: "drip loss w 30% paczek z piątku 19.05."

Jola otwiera nową reklamację:
```
Klient: Karmar
Typ: JAKOSC_TRANSPORT ▼  (nie KOREKTA_FAKTURY!)
Data: 19.05.2026
[Button] Sugeruj partie
```

Klika → system pokazuje 4 możliwe partie. Jedna ma **chill compliance FAIL** (partia 5895 z poprzedniego scenariusza NF07). Jola klika tę partię → otwiera się TraceabilityFull → widzi że chiller miał awarię → wysyła **prewencyjny alert do innych klientów którzy dostali 5895**.

**Po roku**: U01 + NF07 razem oszczędzają ~30-50 reklamacji rocznie = **~150-300 tys. zł** unikniętych zwrotów + utrzymanie reputacji.

---

## U02 — Partie CCP walidator

### Przykład życiowy
**Środa, 24.05.2026, godzina 11:00.**

Justyna chce zmienić status partii 5891 na `APPROVED` (gotowe do produkcji). Klika "Zmień status".

System sprawdza CCP:
```
Walidacja CCP:
  ✅ BS_PathogenSample: NEGATIVE (02.05.2026)
  ✅ BS_FarmTreatment: brak konfliktu antybio
  ❌ BS_RampInspection: BRAK WPISU dla tego kursu

Status nie może być APPROVED dopóki Justyna nie zrobi rampInspection.
```

Justyna idzie na rampę, klika 5 plus-minus na tablecie, wpis powstaje. Wraca, klika "Zmień status" → APPROVED ✅.

**Bez systemu**: Justyna mogłaby zmienić status bez inspekcji → audytor BRC: "pokaż mi inspekcje ramp wszystkich partii ostatnich 6 miesięcy" → 30% brakuje → MAJOR NC.

---

## U03 — Hodowca Scorecard 360°

### Przykład życiowy
**Wtorek, 02.07.2026, godzina 16:00.**

Maja siedzi z **Wojtkiem** na corocznym spotkaniu kontraktowym. Drukuje "Roczna karta hodowcy":

```
Wojtek Nowak (PL271045)  |  Karta 2025/2026
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
Łączny score: 82/100  🥈 TOP 25%

  FPD Index:        32   🟢 (TOP 10%)
  Hock burn:        6%   🟢
  DOA transport:    0.18%🟢
  PM Rejection:     0.42%🟢
  Antybio clean:    90%  🟢
  Reklamacje:       3    🟢

Trend:  74 (2025) → 78 → 80 → 82 ⬆

Rekomendacja cenowa: BASE + 1.5%
```

Wojtek widzi swoje miejsce w rankingu (anonimowo): "TOP 25% z 140 hodowców". Maja: "Panie Wojtku, świetny rok. Może Pan nawet do TOP 10% trafić jeśli FPD pociągnie poniżej 25. Daję Panu nowy kontrakt na rok przy BASE + 1.5%."

Wojtek: dumny, zmotywowany, podpisuje.

**Bez systemu**: rozmowa "Panie Wojtku, było OK, daję te same warunki". Brak motywacji do poprawy.

---

## U04 — AnalitykaPelna PM Defects drill-down

### Przykład życiowy
**Środa, 10.06.2026, godzina 14:00.**

Sergiusz (Ty) otwierasz AnalitykaPelna → nowy tab "PM Defects → Hodowca". Widzisz:
```
TOP 10 hodowców wg PM rejection % (90 dni):

1. ⚠ Mazur K.    1.42%
2. ⚠ Adamski R.  1.18%
3.   Kowalik J.  0.98%
...

Wybrano: Mazur K. → top wada: POLYSER (48%)
```

**Diagnoza w 30 sekund**: Mazur ma chroniczny problem z polyser → klimat na fermie → wentylacja. Decyzja: telefon do Mazura + jeśli nie poprawi w 2 partiach → rozwiązanie kontraktu.

**Wartość**: Mazur ma 1.18 mln zł obrót/rok. Rozwiązanie kontraktu = miejsce dla lepszego hodowcy (TOP 25%). **Lepsze partie = mniej trim + lepsze klientów = +200-500 tys. zł** rocznie.

---

## U05 — HPAI scheduler

### Przykład życiowy
**Niedziela, 28.05.2026, godzina 23:50.**

ARiMR publikuje: "HPAI confirmed Krasnystaw, 23:00, hala 23 000 ptaków, zalecenia kontrolne 50 km".

System auto:
- 23:50: Detekcja publikacji.
- 23:51: Znajduje 3 hodowców w promieniu 50 km (z `Pozyskiwanie_Hodowcy` GPS): Stachura 28 km, Mazur 35 km, Wójcik 44 km.
- 23:52: Auto-flag `HpaiRisk = HIGH`, blok nowych zamówień.
- 23:53: SMS do Justyna, Sergiusz, Stasiek (kierowca poniedziałkowy).

**Poniedziałek 06:00**: Briefing rano ma sekcję "🚨 HPAI ALERT — 3 partie z poniedziałku przesunięte". System sam zaproponował alternatywę.

**Bez systemu**: Stasiek jedzie do Krasnegostawu w poniedziałek 14:00 → wraca z partią z 30 km od HPAI ogniska → **31 mln zł incident** jeśli partia była zarażona.

---

## U06 — Kartoteka shelf life

### Przykład życiowy
**Wtorek, 05.06.2026, godzina 09:30.**

Operator pakowania bierze paczki z partii 5891. Skanuje produkt:
```
Filet drobiowy MAP 1kg
  Default shelf life: 30 dni (z `Article.DefaultShelfLifeDays`)
  Default packaging: MAP_CO2_70
  Pakowanie data: 05.06.2026
  → ExpiryDate auto: 04.07.2026
```

Drukarka drukuje etykietę z **04.07.2026** automatycznie. Operator nie musi liczyć.

**Bez systemu**: operator wpisuje ręcznie → błędy (raz na 100 paczek źle policzony → reklamacja "wczoraj wygasł").

---

## U07 — Flota welfare scoring

### Przykład życiowy
**Pierwszy poniedziałek miesiąca.**

System wysyła raport do Sergiusza:
```
Flota — ranking kierowców maja:

🥇 Marek Borowski    24 kursy, DOA 0.12%, Welfare 87/100 → Premia 1200 zł
🥈 Krzysiek Stachura 19 kursów, DOA 0.15%, Welfare 85/100 → Premia 950 zł
🥉 Adam Kowalczyk    21 kursy, DOA 0.18%, Welfare 82/100 → Premia 1050 zł
...

⚠ Janusz Mikulski    18 kursów, DOA 0.48%, Welfare 65/100 → 0 zł (poniżej 75)
   Rekomendacja: szkolenie defensive driving + rozmowa o driving style

Naczepa #N-12: DOA average 0.42% (wyżej niż mediana 0.22%)
   Last service 3 mies temu — sprawdź wentylację
```

**Wartość**: motywacja kierowców → mniejsze DOA → ~5-10% mniej martwych ptaków = **~50-80 tys. zł oszczędności rocznie**.

---

## U08 — CentrumNagranAI ciągły VLM

### Przykład życiowy
**Cały czas pracy linii.**

4 kamery (post-scalder, post-plucker, PM platform 1, PM platform 2). Co 30 sek każda wysyła klatkę do Claude Haiku 4.5.

Prompt: "Are there any: purple birds? skin rupture >3cm? extensive haematomas? faecal contamination? Return JSON."

**Codziennie**:
- ~11 520 query (4 kamery × 120 klatek/h × 24h).
- Koszt: ~6 800 zł/mies.
- Wykrycia: ~30-50 anomalii/dzień.

**Wartość**: 
- Operator nie musi patrzeć non-stop — system robi sample, on weryfikuje wyrywkowo.
- Audit trail dla BRC: 24/7 coverage.
- Wczesne wykrywanie problemów (np. plucker wear) — godzina szybciej = oszczędność tysięcy zł.

---

## Podsumowanie — co Ci to wszystko daje

| Funkcja | Co fizycznie zobaczysz | Ile Ci to oszczędzi (rocznie) |
|---|---|---|
| NF01 FPD Scorecard | Lista TOP/BOTTOM hodowców | 840 tys. (rejection redukcja) |
| NF02 Antybiotyki | Blok pre-slaughter | Brak recall = uratowane 5-10 mln |
| NF03 Transport CCP | Welfare per kurs + premia kierowcy | 210 tys. (DOA redukcja) |
| NF04 Stunning CCP | Alert "purple bird" | 2.5 mln (haematomas redukcja) |
| NF05 Scalding/Plucking | Alarm scalded meat | 2.27 mln |
| NF06 PM Defects | 14 kafelków na tablecie | 1.26 mln |
| NF07 Chilling Curve | Awaria chłodni w 1h | 3.36 mln (drip loss) |
| NF08 Vision Grading | Klasyfikacja A/B/C auto | 2.10 mln |
| NF09 MAP + Traceability | Recall w 30 sek | 3.0 mln (eksport + ochrona) |
| NF10 Salm/Campy | Logistic slaughter auto | Brak recall = bezcenne |
| NF11 Foreign Material | Audit trail | BRC = 130 mln chronionych |
| NF12 BRC Dashboard | Score 87% / sekcji | Przygotowanie audytu w 4h |
| U01 Reklamacje | Closed loop | 150-300 tys. |
| U02 CCP walidator | Niemożliwe naruszenie | Brak audit fail |
| U03 Hodowca 360° | Karta roczna | Motywacja hodowców |
| U04 Analityka PM | Decyzje o kontraktach | 200-500 tys. |
| U05 HPAI scheduler | Auto-blok | 31 mln/incident uniknięty |
| U06 Shelf life auto | Mniej błędów etykiet | Mniej reklamacji |
| U07 Flota welfare | Premia kierowcy | 50-80 tys. (DOA) |
| U08 Continuous VLM | 24/7 coverage | Audit trail + wczesne wykrycia |

**RAZEM**: **~16-20 mln zł/rok bezpośrednio + ~130 mln zł chronionych obrotów**.
