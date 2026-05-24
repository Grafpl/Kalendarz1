# 📅 META: Timeline 12 miesięcy — co się dzieje miesiąc po miesiącu

> "Plan to nie wishful thinking. To **konkretne kroki w konkretnych miesiącach**. Inaczej wszystko zostaje na poziomie 'kiedyś zrobimy'."

---

## CZĘŚĆ 1: ZAŁOŻENIA TIMELINE

### Twoje constraints
- Pracujesz nad ZPSP **2-3h dziennie** (reszta = operacje firmy)
- Masz pomoc: być może 1 junior IT lub Claude Code jako "drugi developer"
- Budżet hardware: rozłożony na 6-12 miesięcy
- Adopcja zespołu: powolna, ale stabilna

### Strategiczne priorytety
1. **Najpierw compliance** (BRC nie czeka)
2. **Quick wins** dla wiarygodności
3. **Foundation** dla późniejszych pomysłów
4. **AI/ML** na koniec (wymaga danych)

### Kalendarz biznesowy
- **Marzec-Maj**: sezon przed Wielkanocą, dużo produkcji
- **Czerwiec-Sierpień**: lato, łatwiej wdrażać (mniej presji)
- **Wrzesień-Październik**: BRC audit cykl
- **Listopad**: sezon przed Bożym Narodzeniem
- **Grudzień**: planowanie roku

---

## MIESIĄC 1: FUNDAMENTY (Czerwiec 2026)

### Tydzień 1-2: Setup + Quick Win
**Cel**: pokazać szybką wartość żeby zbudować momentum

**Zadania**:
- [ ] Komunikacja zespołowi: "Wdrażamy ZPSP 2.0, oto plan na 12 mies"
- [ ] **#2 DOA Dashboard** — implementacja (5h)
- [ ] Dodaj kolumny do `listapartii`
- [ ] Stwórz proste UI w `WidokFabryka`
- [ ] Pierwsza prezentacja: "Patrz, dziś widzimy DOA"

**Hardware**: zamów teraz (long lead time):
- [ ] PT1000 sondy + konwertery dla parzelnika i chłodni
- [ ] Tablet weterynarza
- [ ] Pierwszy tablet do digital inspection
- [ ] Drukarka etykiet (Zebra ZT411)

### Tydzień 3-4: Lairage + Pasza
**Zadania**:
- [ ] **#5 Lairage Timer** (3 dni) — reuse PartiaStatus
- [ ] **#1 Pasza Calculator** (5 dni) — SMS API setup
- [ ] Test SMS API z 5 hodowcami
- [ ] Tygodniowy stand-up z zespołem: "Co działa, co nie"

### Outcome miesiąca
- ✓ 3 funkcje działające
- ✓ Zespół widzi pierwsze rezultaty (DOA dashboard)
- ✓ Hardware zamówiony, dostawa za 4-6 tyg
- **Wartość run-rate**: 100-150k PLN/rok (z 3 pomysłów)

---

## MIESIĄC 2: COLD CHAIN START (Lipiec 2026)

### Tydzień 1-2: Hardware setup
**Zadania**:
- [ ] Dostawa sprzętu, montaż przez elektryka (mechaniczne wiercenie, kable)
- [ ] Konfiguracja Modbus/TCP
- [ ] Test komunikacji
- [ ] Pierwszy "syrop danych" w bazie

### Tydzień 3-4: Software #19 Faza 1
**Zadania**:
- [ ] Tabele `CCP_Punkt`, `CCP_Sonda`, `CCP_Pomiar`, `CCP_Incydent`
- [ ] `CCPMonitoringService` (BackgroundWorker)
- [ ] Pierwszy dashboard widget w `WidokFabryka`
- [ ] **#9 Scalding** wpięte na te same sondy

### Outcome miesiąca
- ✓ Ciągły monitoring CCP zacząty
- ✓ Pierwsze dane historyczne się zbierają
- ✓ Pierwsze alerty (głównie false positive — tuning w mies 3)
- **Wartość run-rate**: 250-300k PLN/rok

---

## MIESIĄC 3: TABLET WETERYNARZA + STABILIZACJA CCP (Sierpień 2026)

### Tydzień 1-2: #11 Digital Inspection
**Zadania**:
- [ ] Setup Blazor Server app
- [ ] Tabela `WetInspectionRecord`
- [ ] UI tableta: ikony 12 typów wad
- [ ] Trening weterynarza (2-3 dni 1:1)
- [ ] Pilot: jedna zmiana przez tydzień

### Tydzień 3-4: Tuning CCP + alerty
**Zadania**:
- [ ] Analiza false positive z mies 2
- [ ] Tuning progów alertów
- [ ] Konfiguracja SMS dla mechanika dyżurnego
- [ ] Pierwsze raporty PDF dla BRC compliance

### Outcome miesiąca
- ✓ Weterynarz na tablecie (90% adoption)
- ✓ CCP w pełni operacyjny, mało false positives
- ✓ Pierwsze tygodniowe raporty
- **Wartość run-rate**: 500-600k PLN/rok

---

## MIESIĄC 4: AI FORENSIC + COST CALC (Wrzesień 2026)

### Tydzień 1-2: #12 Forensic Hematoma
**Zadania**:
- [ ] Setup Claude API + credit ($20)
- [ ] Tabele `HematomaSession`, `HematomaPhoto`, `HematomaAnalysis`
- [ ] `HematomaAnalysisService` z Claude SDK
- [ ] Prompt tuning (5-10 iteracji)
- [ ] Integracja z `Reklamacje`
- [ ] Pilot: pierwsze 5 reklamacji

### Tydzień 3: #14 Cost Calculator + #15 Ascites + #17 Cellulitis
**Zadania (reuse #11 data)**:
- [ ] Tabela `WadaKosztSlownik`
- [ ] UI "Koszty wad dnia"
- [ ] Auto-email do hodowców z wysokim ascites/cellulitis
- [ ] Dashboard per hodowca

### Tydzień 4: BRC audit prep
**Zadania**:
- [ ] Generator raportów BRC PDF (QuestPDF)
- [ ] Eksport historycznych danych CCP
- [ ] Walidacja: czy dane są pełne, czy są luki

### Outcome miesiąca
- ✓ AI Forensic działa w reklamacjach (WOW moment)
- ✓ Hodowcy dostają raporty miesięczne
- ✓ Gotowość do audytu BRC
- **Wartość run-rate**: 900k-1.1M PLN/rok

---

## MIESIĄC 5: YIELD WATERFALL + CHILLING (Październik 2026)

### Tydzień 1-2: #21 Yield Waterfall
**Zadania**:
- [ ] Tabela `EtapWaterfall`
- [ ] SQL queries cross-DB
- [ ] `WaterfallService`
- [ ] `WidokWodospad` w AnalitykaPelna
- [ ] Drill-down dialogi

### Tydzień 3-4: #18 Chilling Curve + #20 Drip Loss
**Zadania**:
- [ ] Dodatkowe sondy w chłodni (zaktualizuj zamówienie)
- [ ] `ChillingMonitorService`
- [ ] `ChillingCurveWidok`
- [ ] Tabela `DripLossSample` + sampling protocol
- [ ] Pierwsze sampling tygodniowy

### Outcome miesiąca
- ✓ Strategia oparta na waterfall (3 strategiczne decyzje już zrobione)
- ✓ Chilling curve widoczna
- **Wartość run-rate**: 1.4-1.6M PLN/rok

---

## MIESIĄC 6: TRACEABILITY FAZA 1 (Listopad 2026)

### Tydzień 1-2: Setup #22
**Zadania**:
- [ ] Tabele `PaletaWyrob`, `PaletaWyrobSklad`, `DokumentPaletWydania`
- [ ] Drukarka etykiet już zainstalowana (z mies 1)
- [ ] `TraceabilityService` — forward i reverse trace
- [ ] Workflow operatorów krojenia (skanowanie palet surowych)
- [ ] Trening operatorów (2 dni)

### Tydzień 3-4: Roll-out + integracja
**Zadania**:
- [ ] Wszystkie linie krojenia drukują lot numbers
- [ ] QR codes generated
- [ ] Pierwsza prawdziwa "Reverse trace" w odpowiedzi na pytanie klienta
- [ ] Integracja z `DostawaKlientGPS` (Webfleet)

### Outcome miesiąca
- ✓ Pełna traceability palety wyrobu
- ✓ Możliwość recall (jeszcze nie testowane na prawdziwym)
- ✓ QR codes na etykietach (marketing)
- **Wartość run-rate**: 1.8-2M PLN/rok

---

## MIESIĄC 7: SALMONELLA LAB + RAG START (Grudzień 2026)

### Tydzień 1-2: #23 Salmonella Lab
**Zadania**:
- [ ] Konto qc@piorkowscy.pl Gmail
- [ ] `LabEmailWatcherService` (MailKit)
- [ ] `ClaudePdfParser` z prompt tuning
- [ ] Tabele `LabZleceniaBadan`, `LabWyniki`
- [ ] Pilot z 5 raportami lab (history backfill)

### Tydzień 3-4: #29 RAG Chat — pierwsza wersja
**Zadania**:
- [ ] PostgreSQL + pgvector setup
- [ ] Document extractor (PDF, DOCX, MD)
- [ ] Voyage embeddings setup
- [ ] Indeksacja: Broiler Signals + 8 procedur + BAZA_WIEDZY
- [ ] Blazor app UI (mobile-friendly)
- [ ] Pilot z 3-5 pracownikami

### Outcome miesiąca
- ✓ Lab wyniki auto-import (oszczędność czasu QM)
- ✓ Pierwsze pytania pracowników do AI Chat
- **Wartość run-rate**: 2.1-2.4M PLN/rok

---

## MIESIĄC 8: WS/WB DETECTOR + HEAT STRESS (Styczeń 2027)

### Tydzień 1-2: #13 WS/WB Detector
**Zadania**:
- [ ] Zamów kamerę 4K (Hikvision DS-2CD2T46G2) ~3000 zł
- [ ] Mount nad linią cięcia
- [ ] AI Vision pipeline (reuse #12 infrastruktury)
- [ ] Prompt klasyfikujący NORMAL/WS/WB/SM
- [ ] Tabela `FiletDetection`
- [ ] Auto-segregacja na 3 pojemniki (premium/B/przerób)

### Tydzień 3-4: #3 Heat Stress Index
**Zadania**:
- [ ] Open-Meteo API integration
- [ ] HSI calculator
- [ ] Mapa Polski z pinami partii (reuse map z Webfleet)
- [ ] Alerty dyspozytora

### Outcome miesiąca
- ✓ WS/WB auto-detection działa
- ✓ Heat stress monitoring (zimowy okres, ale gotowe na lato)
- **Wartość run-rate**: 2.8-3.2M PLN/rok

---

## MIESIĄC 9: PLUCKING DAMAGE FULL ROLL-OUT (Luty 2027)

### Tydzień 1-2: #10 Plucking — pełne wdrożenie
**Zadania**:
- [ ] Dodatkowe tablety dla wszystkich stanowisk krojenia
- [ ] Pełen workflow A/B + 12 typów wad
- [ ] Dashboard live per zmiana
- [ ] Dashboard per hodowca (rozszerzony z #15, #17)
- [ ] Dashboard per skubarka

### Tydzień 3-4: Integracja z #12 (foto-AI)
**Zadania**:
- [ ] Operator opcjonalnie robi foto wadliwej tuszki
- [ ] AI klasyfikuje typ + sugeruje przyczynę
- [ ] Operator akceptuje lub edytuje
- [ ] Pełna automatyzacja workflow

### Outcome miesiąca
- ✓ Wszystkie 12 typów wad śledzone elektronicznie
- ✓ AI assistance w klasyfikacji
- ✓ Konkretne raporty hodowcom (faktura + faktura A/B)
- **Wartość run-rate**: 3.5-4M PLN/rok

---

## MIESIĄC 10: KPI COCKPIT (Marzec 2027)

### Tydzień 1-2: Architektura
**Zadania**:
- [ ] Identyfikacja wszystkich 30 KPI (z różnych pomysłów)
- [ ] Tabele `KPI_Definition`, `KPI_Snapshot`
- [ ] `KPICockpitService` — agregaty

### Tydzień 3-4: UI + drill-down
**Zadania**:
- [ ] `KPICockpitWindow.xaml` — grid 5×6 kafelków
- [ ] Każdy kafelek: liczba + trend + alert badge
- [ ] Klik → drill-down do szczegółów
- [ ] Auto-refresh co 1 min

### Outcome miesiąca
- ✓ Sergiusz: 5 min dziennie = full picture
- ✓ Strategiczne decyzje data-driven
- **Wartość run-rate**: 3.7-4.2M PLN/rok

---

## MIESIĄC 11: ML FORECAST V1 (Kwiecień 2027)

### Tydzień 1: Backfill datasetu
**Zadania**:
- [ ] SQL ETL z HANDEL + LibraNet
- [ ] Tabela `MlYieldDataset` z 200-300 rekordów
- [ ] Walidacja danych

### Tydzień 2: Trening
**Zadania**:
- [ ] ML.NET LightGBM
- [ ] Pierwszy model, expected MAE ~3-4%
- [ ] Heurystyka równolegle (5h roboty)
- [ ] A/B testing: ML vs heurystyka vs intuicja Sergiusza

### Tydzień 3-4: UI + workflow
**Zadania**:
- [ ] `YieldPredictorWindow.xaml`
- [ ] SHAP-like explainability
- [ ] Sergiusz używa do nowych decyzji zakupowych
- [ ] Logging real vs predicted (do retraining)

### Outcome miesiąca
- ✓ ML działa, ale jeszcze nie idealne
- ✓ Sergiusz porównuje predykcje z intuicją
- **Wartość run-rate**: 4-4.5M PLN/rok (ML jeszcze nie pełnej mocy)

---

## MIESIĄC 12: STABILIZACJA + RECALL DRILL (Maj 2027)

### Tydzień 1-2: Recall management
**Zadania**:
- [ ] Tabele `Recall`, `RecallPalety`
- [ ] Workflow inicjowania recall
- [ ] Powiadomienia (SMS + email) klientom
- [ ] Generator raportów dla sanepidu
- [ ] **Recall drill** — symulacja z fikcyjnym pozytywem

### Tydzień 3: BRC audit zewnętrzny
**Zadania**:
- [ ] Przygotowanie dokumentacji
- [ ] Demo systemów dla auditora
- [ ] Oczekujemy oceny AA lub AA+

### Tydzień 4: Annual review + planning rok 2
**Zadania**:
- [ ] Liczby: ile zaoszczędziliśmy w 12 mies
- [ ] Feedback zespołu
- [ ] Plan rok 2: rozszerzenia, optymalizacja
- [ ] Celebration: pierwszy rok ZPSP 2.0 zakończony sukcesem

### Outcome miesiąca
- ✓ Pełen safety + quality stack
- ✓ BRC AA/AA+
- ✓ Wartość run-rate: **4.5-5M PLN/rok** (z czego ~50% realnie zmaterializowane)

---

## ROK 2 (Czerwiec 2027 — Maj 2028)

### Fokus
- **Optymalizacja**: tuning wszystkiego co wdrożone
- **ML refinement**: MAE z 3% do 2%
- **Drobne pomysły**: #4 (crop), #6 (wykrwawianie AI), #7, #8, #16 (heatmap złamań), #24 (risk score), #26 (welfare), #27 (rejection trends), #28 (photo AI rozszerzony)
- **Premium clients acquisition**: nowi klienci dzięki USP
- **Eksport rozwój**: Niemcy, może Skandynawia

### Outcome rok 2
- Run-rate: **5.5-6.5M PLN/rok**
- Pozycja: top 5 w segmencie

---

## ROK 3 (Czerwiec 2028 — Maj 2029)

### Fokus
- **SaaS pilot**: jeden inny zakład jako klient white-label
- **AI rozszerzenia**: predictive drip loss, predictive klasa B
- **Welfare certification**: pełen ETG, Tierwohl
- **Eksport poza UE**: Korea/Japonia jeśli pasujący

### Outcome rok 3
- Run-rate: **7-8M PLN/rok**
- Pozycja: lider segmentu w PL

---

## CZĘŚĆ 2: TYDZIEŃ TYPOWY SERGIUSZA W TRAKCIE WDRAŻANIA

### Poniedziałek
- 09:00-10:00: KPI cockpit, weekend review (jak system działał)
- 10:00-12:00: Operations + meeting z zespołem
- 13:00-15:00: ZPSP development (2h focused)
- 15:00-18:00: Operations + klienci/hodowcy

### Wtorek-Czwartek
- Podobnie, z 2-3h ZPSP development na środku dnia

### Piątek
- 09:00-12:00: Operations
- 13:00-15:00: Weekly review (analiza co zrobione w ZPSP)
- 15:00-17:00: Planning następnego tygodnia ZPSP

### Weekend
- Sobota rano: tylko jeśli krytyczne
- Niedziela: rodzina, **nie ZPSP**

### Total godzin ZPSP na tydzień: ~10-12h
### Total godzin na 12 mies: ~500-600h
### Plus help (Claude Code / junior): efektywnie ~1000-1200h

---

## CZĘŚĆ 3: RYZYKA TIMELINE

### Ryzyko 1: Hardware nie przychodzi na czas
**Mitygacja**: zamów wcześnie (mies 1), redundantny dostawca

### Ryzyko 2: Sergiusz wypala się
**Mitygacja**: 
- Nie więcej niż 3h/dzień ZPSP
- Weekend off
- Co kwartał: 2 tyg break od programowania
- Rozważ junior IT pomoc po mies 6

### Ryzyko 3: Operacje nie pozwalają na development
**Mitygacja**:
- Deleguj operacje (Marcin, Justyna, Maja)
- Sergiusz: 50% operacje, 50% strategia + ZPSP
- Po roku: operacje samodzielnie, Sergiusz głównie ZPSP/strategia

### Ryzyko 4: Zespół nie nadąża z adopcją
**Mitygacja**:
- Spowolnij tempo, mniej pomysłów na miesiąc
- Lepiej 1 pomysł dobrze niż 3 połowicznie
- Trening + tygodniowe stand-upy

### Ryzyko 5: Krytyczna awaria techniczna
**Mitygacja**:
- Backup wszystko (off-site)
- Fallback procedures (papier)
- 1 dzień slack per sprint

---

## CZĘŚĆ 4: MILESTONES — co świętować

### Po mies 3
**Milestone**: Pierwsze 500k PLN/rok run-rate
**Celebration**: dinner zespołu kluczowego

### Po mies 6
**Milestone**: Cold Chain + Traceability operacyjne
**Celebration**: ogłoszenie BRC compliance achievement

### Po mies 9
**Milestone**: 1M PLN/rok run-rate
**Celebration**: małe gala dla wszystkich pracowników, premie

### Po mies 12
**Milestone**: BRC AA+ + pełen safety/quality stack
**Celebration**: gala + media (artykuł w "Polskie Drobiarstwo")

### Po roku 2
**Milestone**: 5M+ PLN/rok wartości
**Celebration**: wyjazd integracyjny zespołu

### Po roku 3
**Milestone**: Pozycja lidera segmentu
**Celebration**: konferencja branżowa jako prelegent, networking

---

## CZĘŚĆ 5: ALTERNATYWNE TIMELINES

### Wersja "AGRESYWNA" (6 mies, intensywnie)
- Wymaga: 1-2 junior IT pomoc
- Cel: kompresja 12 mies do 6 mies
- Ryzyko: wypalenie, gorsza jakość

### Wersja "BEZPIECZNA" (24 mies, powoli)
- Tylko Sergiusz
- Cel: rozłożenie do 24 mies
- Plus: mniej ryzyka, lepsza adopcja
- Minus: konkurencja Was wyprzedza, niższa wartość kumulatywna

### Wersja REKOMENDOWANA (12 mies, kontrolowanie)
- Wybór: balance między ambicją i wykonalnością
- Co kwartał: ewaluacja, dostosowanie tempa

---

## CZĘŚĆ 6: GŁÓWNY MORAŁ

**12 miesięcy to realny, ambitny ale wykonalny timeline.**

**Klucze do sukcesu**:
1. **Zacznij od quick wins** (DOA, Lairage, Pasza) — momentum
2. **Hardware wcześnie** (long lead times)
3. **Zespół z Tobą** (komunikacja, trening)
4. **Faza po fazie** (nie wszystko naraz)
5. **Monthly review** (co działa, co nie)
6. **Don't burn out** (3h/dzień, weekend off)

**Po 12 miesiącach**: ZPSP 2.0 = **pierwszy w Polsce zakład drobiarski z full digital quality stack**. To pozycja, którą **nikt szybko nie powtórzy**.

**Inwestycja czasu**: 1000-1200h Sergiusza + 100h zespołu
**Zwrot**: 4-5M PLN/rok run-rate + dziesiątki milionów PLN wzrostu wyceny firmy

**To jest najlepiej zainwestowany czas Twojej kariery.**
