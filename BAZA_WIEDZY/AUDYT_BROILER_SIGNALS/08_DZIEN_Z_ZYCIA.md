# 08. Dzień z życia ZPSP — przed i po wdrożeniu nowych funkcji

> 6 scenariuszy z prawdziwymi godzinami, ludźmi, wydarzeniami. Każdy pokazuje: **co dziś robi się ręcznie / w głowie / na kartce** vs **co będzie robić system**. Wszystkie liczby i osoby przykładowe.

---

## Scenariusz 1: Poranny odbiór partii od hodowcy Wojtka — 04:30 

### DZIŚ

**04:30** — Marek (kierowca) wjeżdża na rampę z 7000 ptaków od hodowcy Wojtka. Wyjechał z fermy o 02:15. 
**04:35** — Justyna (rampa) liczy palety, otwiera kontener — widzi 3 martwe ptaki na wierzchu, 2 w środku. Notuje na kartce "5 DOA".
**04:45** — Janek (weterynarz) ogląda partię, klika kreseczki na druku "ASCITES / POLYSER / CACHEX". Po pół godzinie kartka ma 12 kresek. Wkłada do segregatora "Maj 2026".
**05:00** — Marek pyta "kasa za przejazd?" — Justyna zwraca mu papier z DOA. Kierownik wpisuje w Excelu wieczorem.
**Tydzień później** — nikt nie zapamiętał, że ta partia miała 5 DOA (0.07%), 12 wad PM. Wojtek dostaje normalną cenę.

**Problem**:
- Wojtek nie wie, że jego ptaki mają więcej polyserositis niż średnia.
- Marek nie wie, że jego trasa była za długa (2h 15min) → zarobił sobie te 5 DOA.
- BRC auditor pyta "pokaż mi DOA i welfare per partia ostatnie 6 miesięcy" → masz kartki w segregatorze, ale brak agregatów.

### PO WDROŻENIU (NF03 + NF06)

**04:30** — Marek wjeżdża. WebFleet GPS zarejestrował 02h 15min jazdy, czujnik w naczepie pokazał max 26°C (HOTSPOT alert wysłany do biura o 03:42).
**04:32** — Aplikacja na tablecie Justyny (`RampInspectionTablet.xaml`) sama wykryła kurs z systemu. Justyna w 90 sekund klika:
```
Total: 7000 | DOA: 5 (0.07%) ✅
Fractures: 2 | Trapped: 0 | Supine: 1 | Haematomas: 4
Splayed legs: 0 | Crowding: 1/3 | ThermalStress: 2/3 ⚠
Rejections at ramp: 0
[FOTO] [FOTO] [Zapisz]
```
**04:33** — System wylicza: Welfare Index = **78/100** (HotspotEvent obniżył ThermalStress score).
**04:45** — Janek wchodzi do `PMInspectionTablet.xaml`. Tablet sam pokazuje partię Wojtka. Klika kafelki:
- POLYSER: 4
- ASCITES: 3
- HEPAT: 2
- WB: 1
- HAEM_EXT: 2
- (FAECAL): 0
- 14 sztuk total / 7000 = **0.20% rejection** ✅
**05:00** — Marek widzi w aplikacji: "kurs OK, welfare 78, premia kierowcy: 50 zł (>= 75 pkt)".
**Wieczorem** — Wojtek dostaje SMS: "Twoja partia z 23.05: PM rejection 0.20% (TOP 30%), Welfare 78. Twój scorecard 12-mies: 82/100 (wcześniej 79)."

**Wartość**:
- Wojtek wie i koryguje.
- Marek wie i poprawia trasę.
- Janek nie ma kartek.
- BRC: 30 sekund i auditor ma dane od 6 miesięcy.

---

## Scenariusz 2: Antybiotyki — withdrawal check (08:00, planowanie tygodnia)

### DZIŚ

**08:00 poniedziałek** — Justyna planuje tygodniowe dostawy. Ferma Marcina (numer ARiMR PL271022) ma "rzucać" w środę 24.05.
**08:30** — Justyna dzwoni do Marcina: "Marcinie, byłeś u weterynarza w tym tygodniu?"
**08:32** — Marcin: "Tak, podałem enrofloksacynę 3 dni temu, kurs 5 dni".
**08:35** — Justyna szuka w głowie / w notesie: enrofloksacyna — ile karencji? 13 dni. Liczy: ostatnie podanie 25.05 + 13 = ubój dozwolony **od 07.06**. 
**Środa nie**. Przesuwa partię na 8 czerwca, rusza obdzwaniać innych.

**Problem**:
- Justyna musi pamiętać 7 rodzajów antybiotyków × ich karencje.
- Co jeśli Marcin zapomniał powiedzieć o doxycycline z poprzedniego tygodnia?
- Audyt BRC: "pokaż mi dokumentację withdrawal okresów ostatniego roku". Justyna pokazuje karteczki/Excel.

### PO WDROŻENIU (NF02)

**08:00** — Justyna otwiera `Partie/Views/PlanowanieDniaWindow.xaml`. Widzi:
```
Wtorek 23.05: 3 partie zaplanowane ✅
Środa 24.05: 4 partie zaplanowane
  ⚠ Hodowca Marcin (PL271022) — BLOKADA ANTYBIO
     Enrofloxacin podany do 25.05 → uboju dozwolone od 07.06
     [Pokaż treatment record] [Replan auto]
```
**08:01** — Klik "Replan auto" → system proponuje Marcina na czwartek 08.06, automatycznie znajduje wolny slot.
**08:02** — System wysyła email do Marcina: "Twoja partia przesunięta na 08.06 z powodu karencji enrofloksacyny. Konfirmuj otrzymanie."
**08:03** — Justyna idzie na kawę.

**Wartość**:
- Justyna oszczędza 30-60 min/dzień.
- Niemożliwe jest uchybienie withdrawal (audit-safe).
- BRC: tabela `BS_FarmTreatment` z 1500 wpisami → auditor zadowolony w 30 sekund.

---

## Scenariusz 3: Stunning incident — "purple bird" o 11:23

### DZIŚ

**11:23** — Operator linii (Adam) zauważa na taśmie po parzelniku fioletowego ptaka.
**11:24** — Adam idzie do brygadzisty (Marian). "Marianie, miałem chyba purple bird".
**11:25** — Marian: "Zostaw, jest robota. Kto wie ile jeszcze, zapomnij".
**11:30** — Adam wraca do swojej pozycji.
**Nikt nie zapisuje. Linia leci dalej. Jeśli to powtórzy się dziś — nie wiadomo.**

**Problem**:
- Brak audit trail.
- BRC auditor: "Jaką macie procedurę przy purple bird?" → kawa.
- Welfare violation: ptak żywy w gorącej wodzie.

### PO WDROŻENIU (NF04 + U08)

**11:23** — Kamera Hikvision nad linią ciągle leci. Co 30 sek wysyła klatkę do Claude Haiku 4.5 z promptem "Are there any purple-coloured birds visible? Return JSON".
**11:23:15** — Claude: `{"purple_bird_detected": true, "confidence": 0.94, "approximate_count": 1, "shackle_position": "approx center, station 3"}`.
**11:23:16** — `BS_StunningQuality` INSERT: SessionId 1023, PurpleBirds_Cnt: 1, Foto_BlobId zapisane.
**11:23:17** — Alert SMS do Mariana: "⚠️ Purple bird detected — Linia 1, Station 3, 11:23. Sprawdź stunner V/Hz/mA last 10 min."
**11:23:18** — Marian otwiera tablet `StunningBayDashboard.xaml`:
```
Last 10 min:
  Voltage: 105V (target 110-130) ⚠ ZA NISKO
  Frequency: 200 Hz
  Current: 95 mA (norma min 100) ⚠
  EU Compliant: NO for last 6 min
```
**11:25** — Marian zwiększa V do 125V. Compliance OK. Adam sprawdza back-up killer (automatyczne cięcie szyi po stunner).
**11:28** — `BS_StunningQuality` od ostatnich 10 min: purple = 0. ✅
**Wieczorem** — raport dla CEO: "1 incident dziś, root cause: voltage drift, resolved w 5 min."

**Wartość**:
- Audit trail = perfekcyjny.
- Reakcja w minutach a nie tygodniach.
- Welfare: 1 ptak zamiast potencjalnych 100.

---

## Scenariusz 4: Reklamacja klienta Karmar — 13:45 wtorek

### DZIŚ

**13:45** — Telefon. Klient Karmar dzwoni do Joli: "Pani Jolu, dostaliśmy w piątek mięso piersiowe, w 30% paczek była woda na dnie tacek, więc oddajemy."
**13:50** — Jola otwiera Reklamacje, klika "Nowa reklamacja". Wypełnia: klient = Karmar, data = 19.05, opis = "drip loss high".
**14:00** — Jola pyta Marcina (kierownik produkcji): "Co tam było w piątek?"
**14:05** — Marcin: "Hmm, w piątek było 5 partii. Nie wiem która."
**14:10** — Jola wpisuje "do wyjaśnienia". Reklamacja zamknięta jako "zasadna" bez attribution.
**Wojtek (hodowca źródłowy) nie wie. Marek (kierowca) nie wie. Marcin nie wie który chiller miał problem.**

**Problem**:
- 100% bezsensownej pracy.
- Brak danych do poprawy.

### PO WDROŻENIU (U01 + NF07 + NF09)

**13:45** — Telefon. Jola otwiera `Reklamacje/Views/ReklamacjaEditWindow.xaml`:
```
Klient: Karmar [auto-wybór z dropdown]
Typ: [JAKOSC_TRANSPORT ▼] (Jola wybiera bo o drip loss)
Data zdarzenia: 19.05 (piątek)
[ButtonSugerujPartie] ← klika
```
**13:46** — System sugeruje:
```
Sugestie partii dla Karmar / 19.05:
  ✓ Partia 5891 (dostawa 18.05, packaging MAP_CO2_70, drip baseline 1.2%)
  ✓ Partia 5892 (dostawa 18.05, packaging MAP_CO2_70)
  ⚠ Partia 5893 (dostawa 19.05, packaging MAP_N2, drip baseline 1.8%)
  ⚠ Partia 5895 (dostawa 19.05, chill compliance: FAIL 7h to 4°C) ← podejrzane!
```
**13:47** — Jola klika partię 5895. Otwiera się `BS_TraceabilityFull`:
```
Partia 5895 (uboj 18.05):
  Hodowca: Wojtek (PL271045)
  PM rejection: 0.4% (OK)
  Chilling: Time_to_4C 425 min (FAIL - target 360 min) ❌
  Drip baseline (NF07 test): 2.4% (nadnorma)
  Packaging: MAP_CO2_70 (OK)
  Klienci: Karmar, Lidl, Sklep Adamski, ZPC Kraków
```
**13:48** — Jola identifies root cause: chillera awaria 18.05.
**13:50** — Jola powiadamia Lidla i ZPC Kraków prewencyjnie: "Możliwy drip loss w partii 5895, sprawdźcie. Reklamacja zwrotna proszę."
**13:51** — Marcin dostaje alert: "Chillera #2 wymaga inspekcji — Time_to_4C 425 min 18.05".
**14:00** — Wojtek (hodowca) bez winy w tym przypadku — chill problem był po jego stronie. Scorecard zostawia.

**Wartość**:
- Recall PREWENCYJNY = ratowanie 3 innych klientów.
- Root cause natychmiast.
- Awaria chillera złapana w 2h.

---

## Scenariusz 5: HPAI alert w okolicy — niedziela 23:50

### DZIŚ

**Niedziela 23:50** — w MarketIntelligence Briefingu jutrzejszego rano (powstaje o 5:00) pojawi się informacja "HPAI confirmed Krasnystaw, 47 km od Koziołek". Justyna o tym przeczyta w poniedziałek o 7:30.
**Poniedziałek 07:30** — Justyna patrzy briefing, widzi HPAI. Wzdycha, "ok, w środę uważamy".
**Poniedziałek 14:00** — kierowca Stasiek wraca z Krasnegostawu (jechał po partię z 30 km od HPAI ogniska). Ptaki w naczepie. Już potencjalne zakażenie.

**Konsekwencja**: jeśli partia z Krasnegostawu była zarażona → ognisko w Koziołkach = **31 mln zł straty** (z audytu branżowego).

### PO WDROŻENIU (U05)

**Niedziela 23:50** — MarketIntelligence HpaiMonitorService wykrywa publication: "HPAI Krasnystaw 23.05 22:00".
**Niedziela 23:51** — System przeszukuje `Pozyskiwanie_Hodowcy` po GPS: znajduje 3 hodowców w promieniu 50 km, w tym Stachura z 28 km.
**Niedziela 23:52** — Auto-flag: `Hodowca.HpaiRisk = HIGH` dla 3 hodowców. Auto-blok nowych zamówień. Powiadomienie SMS do Justyny + CEO + Stasiek (kierowca).
**Poniedziałek 06:00** — Briefing rano ma sekcję "🚨 HPAI ALERT - 3 partie z poniedziałku przesunięte". Justyna wybiera alternatywnych hodowców.
**Poniedziałek 06:30** — Marcin (planista) dostaje propozycję alternatywnego planu uboju.
**Stasiek** nie jedzie do Krasnegostawu. Stado ratuje.

**Wartość**:
- 31 mln zł incident uniknięty.
- Reakcja w minutach od publikacji ogniska.

---

## Scenariusz 6: Audyt BRC v9 zewnętrzny — kontroler przyjeżdża

### DZIŚ

**10:00** — Auditor BRC siedzi z Tobą w biurze. "Sekcja 4.10, pokaż mi CCP monitoring chłodzenia za ostatnie 30 dni."
**10:05** — Otwierasz papierowe dziennik temperatury chłodni (operator codziennie ręcznie notuje). Auditor patrzy: "Te notatki są podpisane, ale nie ma godziny pomiaru, są tylko 4 wpisy dziennie."
**10:10** — Auditor: "Sekcja 3.9 — pokaż mi recall test. Daję Ci skrzynkę z magazynu, wybieram losowo. Powiedz mi, do których klientów poszły inne paczki tej partii w 4h."
**10:15** — Ty: "Hmm, muszę przejrzeć faktury KSeF, znaleźć datę, sprawdzić WZ, zadzwonić do dystrybucji..."
**13:00** — Po 3h: "Mam 5 z 8 klientów, jednego nie mogę zlokalizować."
**Auditor**: "MAJOR NON-CONFORMANCE, sekcja 3.9."

**Konsekwencja**: certyfikat zagrożony, klienci jak Lidl mogą zerwać kontrakt.

### PO WDROŻENIU (NF07 + NF09 + NF12)

**10:00** — Auditor: "Pokaż mi CCP chłodnia ostatnie 30 dni."
**10:00:15** — Otwierasz `Compliance/BRCDashboard.xaml`, klikasz sekcja 4.10:
```
BRC v9 Sek. 4.10 — Status: CONFORMING ✅
Evidence: BS_ChillTempLog (15 432 wpisów ostatnie 30 dni)
Avg readings/hour: 60 (sensors)
Compliance rate: 99.7% (32 incydenty / 9 600 sesji)
[Open detail] [Export PDF]
```
**10:01** — Auditor: "Recall test. Skanuję ten kod QR z tej paczki."
**10:01:05** — Skanujesz QR, otwiera się `BS_TraceabilityFull`:
```
Paczka: ANTI-TAMPER-UID 7C8F-...
Partia: 5891
Hodowca: Wojtek (PL271045)
Data uboju: 17.05.2026
Chill Time_to_4C: 312 min ✅
PM Rejection: 0.21%
Packaging: MAP_CO2_70, ExpiryDate 16.06
Klienci tej partii: 
  - Karmar (2 palety, 18.05)
  - Lidl Marki (3 palety, 18.05)
  - ZPC Kraków (1 paleta, 19.05)
  - Sklep Adamski (1 paleta, 19.05)
Razem 7 palet, 245 paczek
[Export recall list PDF]
```
**10:01:30** — Auditor: "OK, dokonale. Sekcja 5.6 pathogen?"
**10:01:40** — Otwierasz NF10 Salmonella dashboard. Auditor patrzy 30 sek.
**10:30** — Auditor wychodzi: "Bardzo dobry self-assessment, minor NC 0, major NC 0."

**Wartość**:
- Certyfikat utrzymany.
- Auditor zaimponowany → szybsze następne audyty.
- Klienci jak Lidl widzą wynik = utrzymanie kontraktów.

---

## Synteza — kto na czym zyskuje

| Osoba | Co dziś robi (czas/tydzień) | Co po wdrożeniu | Wartość |
|---|---|---|---|
| **Sergiusz** | Audyt BRC = 3 dni stresu | Audyt BRC = 4h spokoju | Certyfikat + sen |
| **Jola** | Reklamacja attribution = 30 min/szt | 5 min/szt + closed loop | 4-5h/tydz oszczędności |
| **Justyna** | Planowanie antybiotyków = 1h/dzień | 5 min/dzień, auto | 5h/tydz |
| **Janek (wet)** | Kartka kreseczek = 2h/dzień | Tablet = 1h/dzień, lepsze dane | Lepsza praca + dane do raportów |
| **Maja** | "Wojtek znów słabe ptaki" mglista impresja | Scorecard 12-mies = twarde dane do negocjacji | Lepsze warunki kontraktu |
| **Marcin (prod.)** | Chillera awaria zauważona po 2 dniach (reklamacje) | Awaria zauważona w 2h | Mniej drip loss strat |
| **Marek (kierowca)** | "Trasa OK?" — nigdy nie wie | Premia 50 zł za welfare >75 = motywacja | Lepsze welfare = mniej DOA |
| **Wojtek (hodowca)** | "Czemu mniej dostałem?" — nie wie | Scorecard z 6 wskaźnikami → wie co poprawić | Lepszy biznes |
| **Klient (Karmar)** | Reklamacja = chaos | Reklamacja = prewencyjny recall | Trust |

---

## Bottom line per dzień

Bez systemu Twój dzień to:
- 30% obrony reputacji (reklamacje, niewytłumaczalne odrzuty),
- 30% gaszenia pożarów (awarie sprzętu zauważone post-factum),
- 30% planowania w głowie (kto, co, kiedy, antybiotyki),
- 10% strategicznej pracy (rozwój firmy, BRC, transformacja Sp. z o.o., ARiMR).

Z systemem (po wdrożeniu QW + ST):
- 5% obrony reputacji (closed loop = małe reklamacje),
- 10% gaszenia pożarów (system łapie sam),
- 10% planowania (system planuje),
- 75% strategicznej pracy.

**Twój czas = wartość firmy.**
