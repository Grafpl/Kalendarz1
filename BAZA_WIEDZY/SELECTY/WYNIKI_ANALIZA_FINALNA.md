# 🎯 ANALIZA FINALNA — co myślę o całości

**Data:** 2026-05-04
**Źródło:** 6 plików wynikowych SQL (~21 700 linii)
**Bazy zbadane:** LibraNet (3 rundy), HANDEL (Symfonia), TransportPL, UNISYSTEM (UNICARD)

---

## 🤯 1. NAJWIĘKSZE ODKRYCIE: WagoCounter ŻYJE I PISZE NON-STOP

**Skala danych w `WagoCounter` (per miesiąc, od 2023-08 do dzisiaj):**

| Rok-miesiąc | Wpisów | Dni | Suma sztuk |
|---|---|---|---|
| **2026-05** (do 4 maja) | 15 | 1 | **64 195** ← DZISIAJ |
| 2026-04 | 272 | 19 | **1 091 218** |
| 2026-03 | 269 | 20 | 1 132 241 |
| 2026-02 | 267 | 19 | 1 092 633 |
| 2026-01 | 278 | 20 | 1 161 001 |
| 2025-12 | 287 | 19 | 1 197 849 |
| 2025-11 | 267 | 19 | 1 096 094 |
| 2025-10 | 313 | 22 | 1 293 443 |
| ... | ... | ... | ~1 100 000/m-c |

**32 miesiące ciągłych danych. Średnio ~1.1 mln tuszek/m-c.**

**Dziś 2026-05-04 (start o 3:17 rano):**
- 15 aut przyjętych do 14:01
- 64 195 sztuk policzonych
- Auto #5: 5 757 sztuk w 47 min (07:25→08:05) = tempo ~7400 szt/h
- Auto #11: 3 952 sztuk w 39 min — 6100 szt/h

**To jest kopalnia złota której Sergiusz nie wykorzystywał** — myślał że Wago nie ma API. **Ma — pisze do `WagoCounter` od 2023 roku.**

⚠️ **Skorygowanie mojego wcześniejszego błędu:** w rundzie 2 napisałem "Wago przestało pisać 2025-04-01". To była **moja błędna interpretacja** wyniku `MIN(CalcDate)` (najstarszy wpis, nie najnowszy). Wago pisze **codziennie do dzisiaj**.

---

## 📊 2. KOMPLETNE STATYSTYKI EKOSYSTEMU DANYCH

| Baza | Serwer | SQL Server | Tabel | Widoków | SP | Wierszy |
|---|---|---|---|---|---|---|
| **LibraNet** | 192.168.0.109 | **2022 Developer** | 293 | 48 | 70 | ~5 mln |
| **TransportPL** | 192.168.0.109 | 2022 Developer | **13** | 1 | 2 | ~10k |
| **HANDEL** (Symfonia) | 192.168.0.112 | **2019 Standard** | **889** | **742** | **2305** | ~10 mln |
| **UNISYSTEM** (UNICARD) | 192.168.0.23\SQLEXPRESS | 2022 | ~150 | ~10 | ~5 | **~1 mln rejestracji** |

**HANDEL to GIGANT.** 889 tabel, 742 widoki, 2305 procedur. To pełen Sage Symfonia ERP. Sergiusz z tego korzysta tylko fragmentarycznie (raporty, faktury). **Tu jest mnóstwo niewykorzystanego potencjału.**

**TransportPL to MAŁA baza** — tylko 13 tabel, ale dobrze zorganizowana (FK między Kierowca/Pojazd/Kurs/Ladunek).

**UNISYSTEM ma 714k rejestracji** w `PL_REGISTRATIONS` — to wszystkie wejścia/wyjścia 425 pracowników (425 kart, 599 faktycznie). **Pełna baza HR/RCP od polskiego producenta UNICARD.**

---

## 🗂️ 3. NIESPODZIANKI W HANDEL (Symfonia)

### 5 baz na 192.168.0.112
- **HANDEL** (główna, od 2022-01)
- **UBOJNIA50C** — od 2023-03 (osobna baza, prawdopodobnie inna aplikacja)
- **UDPiorkowscy** — od 2020-01 (najstarsza, "Ubojnia Drobiu Piórkowscy")
- **WF_Piorkowscy** — od 2026-02-19 (najnowsza, **WorkFlow** — system zatwierdzania!)
- **WF_SERVICE** — od 2026-02-19 (serwis WorkFlow)

**Co to WF_***?** To prawdopodobnie **Sage Symfonia WorkFlow** — moduł zatwierdzania faktur. Wprowadzony niedawno (luty 2026). **Sergiusz, używasz tego?** Bo to potencjał na obieg faktur w firmie.

### Top tabele HANDEL (top 10)
| Tabela | Wierszy | Co |
|---|---|---|
| `HM.OP` | **1.34 mln** | Operacje (transakcje) |
| `FK.zapisy` | **1.16 mln** | Księga główna — wszystkie zapisy księgowe |
| `HM.PW` | **1.03 mln** | Przyjęcia Wewnętrzne |
| `HM.MZ` | **907 559** | Pozycje magazynowe (każdy ruch) |
| `HM.RO` | **651 070** | Rozchód |
| `Common.UserOperationsHistory` | 630 166 | Audit log |
| `HM.HW` | 450 167 | Historia Wartości |
| `HM.DP` | **428 790** | Linie dokumentów (faktury) |
| `dr.Attachments` | 162 120 (**987 MB!**) | Załączniki dokumentów |
| `HM.MG` | 227 869 | Magazyny (ruchy) |

**HM.TW (towary): 6 463** — w Symfonii jest 6.5k towarów (historycznych), w LibraNet tylko 36 aktywnych. **Ta różnica jest celowa — Symfonia ma wszystko, LibraNet tylko to czego używamy.**

**SSCommon.STContractors: 3 252** kontrahentów w Symfonii vs **2633 w LibraNet.kontrahenci** — synchronizacja częściowa.

---

## 🚛 4. TransportPL — kompletny obraz

### Kierowcy (16 aktywnych, 1 nieaktywny)
- Mariusz Wieczorek, Robert Staroń, Zbigniew Gawęcki, Robert Panak, Andrzej Sasin, Grzegorz Staniszewski, Piotr Szczepaniak, Tomasz Tołkaczewicz, **Radosław Kołodziejczyk** (znany z Fireflies!), Krzysztof Patos, MARIUSZ DROŻDŻYK, **Sławomir Gałek** (Fireflies!), Jacek Seferyński, ARTUR POLCYN, ROBERT PIETRZAK + Damian Duda (zdezaktywowany 2026-04-23)

### Pojazdy (13 aktywnych)
- **Scania ×6** (EBR 90KK, EBR 12JF, EBR 40JL, EBR 28PE, EBR 8J11, EBR 9C90, WGM 7736H)
- **DAF ×1** (EBR 9C89)
- **Renault ×2** (EBR 1E50, EBR 7K90 T480)
- **Mercedes ×2** (EBR C552, EBR 15LE)
- **Bez marki ×1** (EBR 08HY — 4 palety, mała dostawcza)

**Pojemność (PaletyH1):** 4× **33 palety** (długie ciężarówki TIR), 8× **18 palet** (średnie), 1× **8 palet** (Mercedes), 1× **4 palety**.

### Skala kursów
- **2025-09 (start):** 32 kursy
- **2025-10:** 253, **2025-11:** 211, **2025-12:** 226
- **2026-01:** 191, **02:** 176, **03:** 208, **04:** 189
- **2026-05 (do 4 maja):** 12 kursów
- **Łącznie 1 498 kursów + 2 011 ładunków + 4 703 zmian (workflow akceptacji)**

**Wszystkie 1498 kursów mają status "Planowany"** — kursy nigdy nie są updatowane do "Zakończony"/"W trasie" — dead column!

### Co dziś (2026-05-04)
- INEX, RADDROB Chlebowski, BIMEX, PODOLSKI, SMOLIŃSKI, BOMAFAR, **Romanowska→Trzepałka** (kombinowana trasa!), **PUBLIMAR→Ladros**, PIEKARSCY, EGE FOOD, Damak, KODAR

**Anna Jedynak** (handlowiec/fakturzystka?) i Administrator wprowadzają zmiany.

---

## 🎯 5. UNISYSTEM (UNICARD) — kompletny system HR

### Skala
- **425 pracowników** (T_UXUSUD_USERS_DATA)
- **300 fizycznych kart** + 599 historycznych przypisań
- **306 zmian roboczych** (T_RCSCSH_SHIFTS) + 306 daily schedules
- **714 661 rejestracji** w PL_REGISTRATIONS (główna tabela)
- **246 564 w T_RCRER_REGISTRATIONS** (przekonwertowane)
- **47 553 wyników analizy** (T_RCREAR_ANALYSIS_RESULTS)
- **5 298 policzonych interwałów** (T_RCWACI_CALCULATED_INTERVALS)
- **1 460 rozliczonych dni** (T_RCWAAD_ACCOUNTED_DAYS)

### Tablice typu "T_RCP_USERS_*" 
**Wszystkie z 0 wierszy!** UNICARD ma **kompletną strukturę zaawansowanego HR** (limity urlopów, godziny pracy, wyłączenia, miejsce kosztu, niepełnosprawność, system pracy), ale **NIE używa tych funkcji**. Tylko surowe rejestracje wejść/wyjść + nazwy kart.

**Implikacja:** Cała wartość dodana HR (urlopy, nadgodziny, premie) jest **na zewnątrz UNICARD** — w `KontrolaGodzin` modułu ZPSP (~3100 linii kodu) plus tabele `HR_*`/`KG_*` w LibraNet. Sergiusz pisze własny HR na danych UNICARD.

---

## 💡 6. TOP 10 ODKRYĆ KTÓRE ZMIENIAJĄ MOJE ZROZUMIENIE

| # | Odkrycie | Dlaczego ważne |
|---|---|---|
| 1 | **WagoCounter pisze codziennie 32 miesiące** | Sergiusz nie wiedział — to LICZNIK TUSZEK który on chciał. Już mamy. |
| 2 | **Article.StandardTol/StandardTolMinus istnieją** | Tolerancje per towar od dawna w bazie (Kurczak A: ±0.31/-0.14) |
| 3 | **HM.TW = 6 463 towarów** vs Article = 36 | Symfonia ma wszystko historyczne, ZPSP wycina aktywne 36 |
| 4 | **6 baz na serwerze 109+112** | LibraNet, TransportPL, HANDEL, UBOJNIA50C, UDPiorkowscy, WF_Piorkowscy, WF_SERVICE — niesamowicie rozproszone |
| 5 | **Reklamacje 75% to auto-import korekt Joli** | `WymagaUzupelnienia=1` + `ZrodloZgloszenia='Symfonia'` — wystarczy filtr |
| 6 | **425 pracowników w UNICARD** | Sergiusz mówił 100+, w bazie 425 (z historią) |
| 7 | **OdbiorcyKurczaka 28 ubojni** | Animex, SuperDrob, Cedrob, Drobboks, Lipce z numerami — TYLKO TYM SPRZEDAJESZ NADWYŻKI |
| 8 | **133 hodowców z PUSTYM CustomerName** mają wiele ID | Dramat data quality |
| 9 | **Operator2ID 100% NULL** | Usunąć z modeli C# |
| 10 | **Klasy ujemne -1, -6, -10 w QntInCont** = STORNO | Razem z ujemną Weight i ActWeight |

---

## 🎬 7. CO MYŚLĘ O CAŁYM PROGRAMIE

### Mocne strony ZPSP (zaskoczyło mnie ile jest)
1. **70 stored procedures** w LibraNet — dużo logiki biznesowej zamknięte w SP
2. **48 widoków** które robią pre-agregaty (V_HR_*, V_KG_*, vw_QC_*, vw_Saldo*)
3. **Pełen audit logging** (tr_Reklamacje_LogujZmiany, sp_AuditLog_*, KartotekaHistoriaZmian)
4. **Workflow akceptacji** (DostawcyCR, TransportZmiany, ReklamacjeUstawienia, ZamowieniaMiesoSnapshot)
5. **Spotkania + Notyfikacje** (Spotkania, SpotkaniaUczestnicy, SpotkaniaNotyfikacje, FirefliesTranskrypcje 102)
6. **Komunikacja wewnętrzna** (ChatMessages 165, ChatTypingStatus, NotatkiMentions)
7. **Geolokacja klientów** (Latitude/Longitude w `KartotekaOdbiorcyDane`, `GeoCache` 20k)
8. **Scoring klientów** (KartotekaScoring z TerminowoscPkt, RegularnoscPkt, RekomendacjaLimitu)
9. **103 kolumny w `FarmerCalc`** — pełen workflow odbioru żywca z fermy (2 wagi, czasy, wet., zdjęcia, IRZplus, Symfonia)
10. **WagoCounter** — TY JUŻ MASZ licznik tuszek. **Wystarczy go pokazać.**

### Słabe strony ZPSP
1. **6 tabel klientów** (`OdbiorcyCRM` 20k, `TymczasowiOdbiorcy` 20k, `kontrahenci` 2.6k, `ImportCRM` 18k, `OdbiorcyKurczaka` 28, `WlascicieleOdbiorcow` 23) — duplikaty + brak source of truth
2. **Daty jako varchar** w `In0E`, `Out1A`, `listapartii`, `Article` — historyczne quirki, ale komplikuje queries
3. **`Operator2ID` nieużywany** mimo że jest w schema
4. **WSZYSTKIE 1498 kursów = "Planowany"** — status nigdy nie aktualizowany
5. **EtykietyZbiorcze 36k** — sample od 2008-2011, prawdopodobnie martwa tabela
6. **2 brakujące tabele** (`SzablonyZamowien`, `KartotekaPrzypomnienia`) — dead code w ZPSP
7. **133 hodowców z pustym CustomerName** — agregacja per nazwa wymaga normalizacji
8. **Nazwy widoków**: `V_HR_*` vs `vw_HR_*` vs `V_KG_*` — niespójność konwencji

### Ogólny werdykt
**ZPSP to nie jest "amatorski projekt jednej osoby" — to JEST poważny system.** 293 tabele, 70 SP, 48 widoków, pełny audit, workflow, geolokacja, scoring. Ten system **już dziś** robi rzeczy które niektóre firmy 10× większe nie mają. **Sergiusz Cię nie docenia** swojego dzieła.

**Słabość główna:** brak **konsolidacji** — różne moduły mają własne konwencje, własne tabele klientów, własne logi. Refactor "jeden source of truth" da więcej niż dodawanie 100 nowych funkcji.

---

## 🚀 8. CO POLECAM ZROBIĆ — TOP 3 PRIORYTETY

### Priorytet #1: **Hala LIVE — wykorzystując WagoCounter**

**Dlaczego #1:** Dane są. Już dziś WagoCounter ma sztuki tuszek per CarLp per godzina dla każdego dnia. Sergiusz mówił "nie mam jak liczyć tuszek real-time" — **MASZ.**

**Co zbudować (1 tydzień):**
- Widok WPF z auto-refresh co 1 minutę
- 5 KPI dziś:
  1. **Sztuki ubite dziś** = `SUM(WagoCounter.Quantity WHERE CalcDate=TODAY)`
  2. **Tempo bieżące** = sztuki ostatnie 60 min × 60 / minut
  3. **Aut przyjętych dziś** = `MAX(CarLP)` z dziś
  4. **Kg netto z FarmerCalc** = `SUM(NettoWeight WHERE CalcDate=TODAY)`
  5. **Plan vs realnie** = HarmonogramDostaw.SztukiDek vs WagoCounter.Quantity

- Lista aut dziś z postępem (Auto 1: 4208 sztuk OK / Auto 5: 5757 sztuk **wysoki**)
- Sparkline tempa za ostatnie 8h
- Alert gdy auto trwa >60 min (możliwe utknięcie linii)

**ROI:** Sergiusz codziennie wie ile sztuk ubił. Justyna patrzy w real-time bez chodzenia po hali. Łukasz Collins wie czy dotrzymuje planu.

---

### Priorytet #2: **Reklamacje V2 — odsiać auto-import**

**Dlaczego #2:** Bez tego statystyki reklamacji są **zafałszowane na 75%**. Każdy dashboard w którym reklamacje są pokazywane jest **kłamstwem**.

**Co zbudować (1 tydzień):**
- Widok `vw_ReklamacjePrawdziwe` w bazie:
  ```
  WHERE ZrodloZgloszenia <> 'Symfonia'
     OR (WymagaUzupelnienia = 0 AND DecyzjaJakosci IS NOT NULL)
  ```
- Refactor `FormReklamacjaWindow` — domyślnie filtruje "tylko prawdziwe"
- Toggle "pokaż wszystko + auto-import" dla księgowości
- Statystyki per `KategoriaPrzyczyny` (niedowaga, krwiak, błąd faktury)
- Ranking handlowców per typ przyczyny
- Auto-zamknięcie korekt Symfonii starszych niż 30 dni gdy `WymagaUzupelnienia=0`

**ROI:** Justyna widzi PRAWDZIWE reklamacje (~150 zamiast 621). Może podejmować decyzje na faktach.

---

### Priorytet #3: **Ranking hodowców z klasą wagową**

**Dlaczego #3:** Sergiusz wprost prosił w `PYTANIA_PRODUKCJA.md`: *"Fajnie by było aby przy odebraniu hodowcy można było sprawdzać jak często jego partia jest reklamowana, ile klasy B i A ma."*

**Co zbudować (1 tydzień):**
- Widok `vw_HodowcaScore`:
  - Per CustomerID: średni % klas idealnych (6+7), średnia waga, padłe vs deklarowane
  - Z `FarmerCalc.VetRate0/1/2/VetComment` — kontrola weterynaryjna
  - Z `Reklamacje` (tylko prawdziwe!) — ile było reklamacji powiązanych z partią od tego hodowcy
- Dashboard "Hodowcy" w ZPSP — sortowanie po score
- Alert podczas tworzenia partii: *"Hodowca Stróżewski średnio 18% klasa B, 3 reklamacje w 90 dni"*

**ROI:** Paulina wie z kim renegocjować ceny. Ty wiesz kogo wyciąć z kontraktu.

---

## 🎯 9. CO MNIE UDERZYŁO — REFLEKSJE

### Twój system jest LEPSZY niż myślałem
Pierwsze rozmowy o "5 zmianach w 5 oknach" — myślałem że to mały projekt. Po 21k linii SQL widzę: **to jest Sage Symfonia od dołu integrowany z autorskim ERP od góry**, plus UNICARD HR od boku, plus TransportPL od boku, plus 5 baz na serwerze 112. **Żadna gotowa firma tego by nie zbudowała w tym budżecie.**

### Jednocześnie jest "rozproszony"
Tabele klientów w 6 miejscach. Daty jako string. Numer partii w dwóch formatach (`Partia` 8-cyfrowy + `PartiaNumber` 11-cyfrowy w FarmerCalc). Dwa magazyny "świeże po uboju" (65554 LibraNet vs HM.MG 65554 w Symfonii). **Każda nowa funkcja będzie się ślizgać po tych spojach.**

### Co bym zrobił gdybym dziś zaczynał
1. **Konsolidacja klientów** — 1 tabela `Klient` + widoki dla wstecznej kompatybilności
2. **Daty native** — migracja `varchar` dat na `datetime2`/`date` (gdzie się da, ale ostrożnie z `In0E`/`Out1A` — 4 mln wierszy)
3. **Source of truth dla towarów** — albo Symfonia, albo LibraNet, nie oba

Ale **nie radzę tego TERAZ** — to są długie projekty (2-6 miesięcy każdy). **Najpierw użyj tego co masz** (WagoCounter, Reklamacje filter, Hodowcy ranking).

### Twój największy nie wykorzystany asset
**`FarmerCalc` 103 kolumny.** Tu jest pełen workflow odbioru żywca: 2 wagi, czasy trasy, kontrola weterynaryjna z opisem (`VetComment` 512 znaków!), zdjęcia tary i brutto, link do IRZplus i Symfonii. **Dla każdej dostawy żywca masz pełną historię.** Nikt z Twojej konkurencji tego nie ma.

### Twój największy techniczny dług
**Operator2ID** — 100% NULL ale jest w schema. Plus daty jako varchar. Plus 6 tabel klientów. To **nie jest tragedia**, ale **każdy nowy moduł musi się tym przejmować**, co zwiększa złożoność.

---

## 🤝 10. PROPOZYCJA: CO MAMY ZROBIĆ TERAZ

**Wybierz JEDNO:**

**A) Hala LIVE** (Priorytet #1) — 1 tydzień roboty. Wynik: Cockpit dla Justyny + Łukasza + Twoich oczu.

**B) Reklamacje V2** (Priorytet #2) — 1 tydzień. Wynik: Czyste statystyki reklamacji, refactor istniejącego okna.

**C) Ranking hodowców** (Priorytet #3) — 1 tydzień. Wynik: Score per hodowca, alert podczas tworzenia partii.

**D) Wszystkie 3 po kolei** — 3 tygodnie. W tej kolejności (każdy buduje na poprzednim).

**E) "Daj mi DOKUMENTACJĘ"** — przez następne dni piszę kompletną dokumentację bazy w stylu README dla developera (tabele, relacje, queries referencyjne, jak to wszystko działa) **zamiast pisać kod**.

**F) Inny moduł** — masz pomysł czego brakuje? Powiedz, zajrzę.

---

## 📁 Co już mam

W `BAZA_WIEDZY/SELECTY/`:
- `WYNIKI_RAW.txt` (LibraNet runda 1)
- `WYNIKI_RAW_2.txt` (LibraNet runda 2)
- `WYNIKI_LIBRANET_3.txt` (LibraNet runda 3 najgłębsza)
- `WYNIKI_HANDEL.txt` (Symfonia 112)
- `WYNIKI_TRANSPORTPL.txt` (TransportPL)
- `WYNIKI_UNISYSTEM.txt` (UNICARD)
- `WYNIKI_ANALIZA_RUNDA1.md`, `WYNIKI_ANALIZA_RUNDA2.md`
- **`WYNIKI_ANALIZA_FINALNA.md`** (ten plik)

W `BAZA_WIEDZY/`:
- 22 plików dokumentacji firmy/programu/baz (00_START → 21_PYTANIA_PRODUKCYJNE)

**Podstawa pod Twój ZPSP w przyszłości:** masz teraz **kompletną dokumentację 4 baz danych + 21 plików profilu firmy/użytkownika/programu**. Każda nowa rozmowa może zacząć od `00_START_TUTAJ.md` i ma cały kontekst.

**Czekam na decyzję A/B/C/D/E/F.**
