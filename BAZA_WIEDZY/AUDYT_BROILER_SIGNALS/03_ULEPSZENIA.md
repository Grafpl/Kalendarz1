# 03. Ulepszenia istniejących modułów ZPSP

> Tu nie dodajemy nowych modułów — modyfikujemy to, co już działa, żeby było bliżej standardów Broiler Meat Signals + BRC v9.

---

## U01 — `Reklamacje/` — closed-loop attribution + filtr "prawdziwych"

### Co jest źle teraz
- ~75% rekordów to auto-import faktur korygujących (nie reklamacje jakościowe).
- Brak attribution do konkretnej partii/hodowcy (jeśli klient mówi tylko "tydzień XY").
- Brak feedback loop do scorecardów hodowców.

### Co dodaję
- Pole `TypReklamacji` z enum: `JAKOSC_PM` (np. ascites, cellulitis), `JAKOSC_TRANSPORT` (drip loss, temp), `KOREKTA_FAKTURY` (te auto-import), `LOGISTYKA`, `INNE`.
- Domyślny filtr "wyłącz korekty faktur" w głównym widoku.
- Przy tworzeniu reklamacji typu `JAKOSC_PM` — auto-suggestion 3 najbardziej prawdopodobnych partii (po dacie i kliencie).
- Link `Reklamacje.PartiaDostawcy → BS_PM_DailySummary` — pokazuje co PM inspector zobaczył dla tej partii (closed loop).

### Pliki do tknięcia
- `Reklamacje/Models/ReklamacjeModels.cs` — dodać enum.
- `Reklamacje/Views/ReklamacjaEditWindow.xaml` — dodać combo + button "powiąż z PM defect".
- `Reklamacje/Services/ReklamacjeService.cs` — metoda `SuggestPartieAsync(klientId, dataZdarzenia)`.

### Wartość
- Czysta lista reklamacji = real picture dla CEO.
- Hodowca scorecard zawiera reklamacje konsumentów (już nie tylko PM internal).

---

## U02 — `Partie/` — Status V2 + QC_Normy → wpięcie nowych CCP

### Co jest źle teraz
- `PartiaStatusEnum` ma 10 stanów, ale brak walidacji na poziomie CCP (np. nie blokuje `APPROVED` jeśli chill compliance failed).
- `QC_Normy` to placeholders bez realnych pomiarów.
- Brak strukturalnej decyzji "logistic slaughter Y/N".

### Co dodaję
- W `PartiaService.UpdateStatusV2Async` dodać walidatory:
  - Status `APPROVED` → musi mieć: BS_PathogenSample.Wynik = NEGATIVE (lub LogisticSlaughter zaplanowany), BS_FarmTreatment z DataMozliwegoUboju ≤ dziś.
  - Status `PROD_DONE` → musi mieć: BS_ChillCompliance.EUCompliant = 1, BS_PM_DailySummary istnieje.
  - Status `CLOSED` → musi mieć: BS_PackagingBatch (jeśli partia idzie do MAP).
- W `Partie/Controls/StatusBadge.xaml` dodać tooltip "CCP compliance: 4/5".

### Pliki do tknięcia
- `Partie/Models/PartiaModels.cs` — dodać `CCP_Compliance` view-model.
- `Partie/Services/PartiaService.cs` — `ValidateCCPBeforeStatusChange()`.
- `Partie/SQL/CreatePartieV3.sql` — nowy migration script.

### Wartość
- Niemożliwe zaaprobowanie partii z naruszeniem CCP → automatyczna ochrona BRC compliance.

---

## U03 — `Hodowcy/HodowcaProfileWindow` — Scorecard 12-mies. + ranking

### Co jest źle teraz
- Profil hodowcy ma podstawowe statystyki, ale brak rankingu vs reszta + brak benchmarków branżowych.
- Brak rekomendacji dotyczących pricing per partia.

### Co dodaję
- Tab "Scorecard 360°" — 6 wskaźników w pajączku radarowym:
  1. FPD Index (z NF01)
  2. Hock Burn % (z NF01)
  3. DOA na transporcie (z NF03)
  4. PM Rejection rate (z NF06)
  5. Antybiotic clean % (z NF02)
  6. Reklamacje klientów per partia (z U01)
- Per wskaźnik: badge "TOP 10%" / "AVG" / "BOTTOM 25%" — względem reszty 140 hodowców.
- Auto-rekomendacja pricing: bazowa cena ± modyfikator (np. TOP 10% +3%, BOTTOM 25% -5%).
- Print/email "Roczna karta hodowcy" (PDF) — do podpisu kontraktu w nowym roku.

### Pliki do tknięcia
- `Hodowcy/Models/HodowcaProfilModels.cs` — dodać `Scorecard360Model`.
- `Hodowcy/Views/HodowcaScorecardTab.xaml` (nowy).
- `Hodowcy/Services/HodowcaScorecardService.cs` (nowy).

### Wartość
- Dyscyplina hodowców → lepsza partia → mniejszy rejection → wprost zysk.
- Ranking sam w sobie motywator (kompetycja).
- 140 hodowców × 5% poprawa średnia per partia (10 partii/rok) = znaczna redukcja rejection downstream.

---

## U04 — `AnalitykaPelna/` — sub-zakładka "Wydajność → PM Defects → Hodowca"

### Co jest źle teraz
- AnalitykaPelna ma świetną wizualizację bilansu, ale nie ma drill-down "który hodowca odpowiada za odrzuty".
- `WydajnoscService` operuje na agregatach, nie na strukturach BS_PM_Defect.

### Co dodaję
- Nowa sub-zakładka w "Wydajność" → **"PM Defects → Hodowca"**:
  - Lewa kolumna: lista TOP 10 hodowców wg PM rejection % ostatnich 30 dni.
  - Prawa kolumna: dla wybranego hodowcy → wykres słupkowy "polyser / ascites / WB / WS / BCO / ...".
  - Drill-down: kliknięcie wady → lista partii → kliknięcie partii → wszystkie BS_PM_Defect rekordy + zdjęcia.

### Pliki do tknięcia
- `AnalitykaPelna/Views/WidokWydajnosc.xaml` — dodać TabItem.
- `AnalitykaPelna/Views/WidokPMDefects.xaml.cs` (nowy widget).
- `AnalitykaPelna/Services/PMDefectsAttributionService.cs` (nowy).

### Wartość
- Decyzje pricing/kontrakty dla hodowców na podstawie twardych danych.
- Identyfikacja "zły hodowca" → rozmowa / zerwanie kontraktu → poprawa średniej.

---

## U05 — `MarketIntelligence/` — HPAI + logistic slaughter scheduler

### Co jest źle teraz
- `MarketIntelligence/` świetnie zbiera ceny + HPAI alerts, ale brak operacyjnego use case dla planowania uboju.
- `intel_HpaiAlerts` poza CreateTables.sql (z memory `project_poranny_briefing`).

### Co dodaję
- Subskrypcja `MarketIntelligence` ↔ `Partie`:
  - HPAI alert w promieniu 10 km od fermy → flaga `Hodowca.HpaiRisk = HIGH` → blokada nowych zamówień + przyspieszenie uboju aktualnej partii.
  - Salmonella/Campy pozytywny w okolicy → auto-zaplanowanie testów u sąsiednich hodowców (z `Hodowcy` GPS).
- Briefing rano dodaje sekcję "Planowanie dnia uboju": które partie idą jako pierwsze (Salm- + ATB-clean), które na koniec (Salm+).

### Pliki do tknięcia
- `MarketIntelligence/Services/HpaiMonitorService.cs` — emit event.
- `Partie/Services/PartiaService.cs` — handler na event.
- `MarketIntelligence/Views/BriefingOnePagerWindow.xaml` — dodać sekcję "Planowanie uboju".

### Wartość
- HPAI ognisko: 31 mln PLN/incident (z audytu branżowego). System ochrony przed wjazdem ptaków zarażonych = wartość samego utrzymania ciągłości.

---

## U06 — `KartotekaTowarow/` — shelf life + MAP per produkt

### Co jest źle teraz
- `Article` ma dane towaru, brak: typowy `ShelfLifeDays`, typowy `PackagingType`, drip-loss baseline.

### Co dodaję
- Nowe pola w `Article`:
  - `DefaultShelfLifeDays INT`
  - `DefaultPackagingType NVARCHAR(50)`
  - `DripLossBaselinePct DECIMAL(4,2)`
  - `RequiresMAP BIT`
- W `ArticleEditWindow.xaml` nowy panel "Shelf life & Packaging".
- W `BS_PackagingBatch` (z NF09) FK do `Article.Id`, walidacja zgodności.

### Wartość
- Mniej błędów konfiguracji ("dlaczego ten produkt ma 7 dni a powinien mieć 30 w MAP").
- Możliwość naliczania expiry date automatycznie.

---

## U07 — `Flota/` — service log + driver scorecard z NF03

### Co jest źle teraz
- `VehicleServiceLog` istnieje ale nie linkowany do welfare (np. uszkodzona ventylacja w naczepie = wpływ na DOA).
- `DriverDetails` z badaniami/BHP, ale brak scoringu welfare.

### Co dodaję
- W `VehicleServiceLog` dodać kategorie: `VENTILATION`, `TEMP_SENSOR`, `GPS`, `WINDBREAK_MESH`, `CURTAINS`.
- W `DriverDetails` dodać `WelfareScore` (auto-aktualizowany z `BS_RampInspection`).
- W `Flota/Views/WidokFlota.xaml` dodać kolumnę "Avg DOA last 30d" per kierowca i per pojazd.

### Wartość
- Identyfikacja: ten kierowca ma 0.5% DOA (bad), tamten 0.1% (good).
- Identyfikacja: ta naczepa ma hotspots = serwis ventilacji.

---

## U08 — `CentrumNagranAI/` — auto-detection wad PM w trybie ciągłym

### Co jest źle teraz
- CNA działa per-query (operator zadaje pytanie "pokaż mi co działo się o 10:00 na rampie").
- Brak ciągłej analizy linii do auto-wykrywania.

### Co dodaję
- Background service `CentrumNagranAI/Services/ContinuousVLMScanner.cs`:
  - Co 30 sek bierze 1 klatkę z każdej z 4 kamer (post-scalder, post-plucker, PM platform 1, PM platform 2).
  - Sonnet 4.6 z prompt: "Czy widzisz: purple bird? skin rupture >3cm? hematomas? faecal contamination?"
  - Wykrycie → INSERT do BS_StunningQuality / BS_PluckingQuality / BS_PM_Defect.
- Koszt: 4 kamer × 120 klatek/h × 24h = 11520 query/dzień × ~$0.005 = $58/dzień = ~6 800 zł/mies (Haiku 4.5 zamiast Sonnet 4.6 obniży 4×).

### Wartość
- Operator nie musi wszystkiego klikać sam — VLM robi sample, operator weryfikuje wyrywkowo (1× na zmianę).
- Pełne pokrycie czasu produkcji.

---

## Podsumowanie ulepszeń — gdzie najszybszy zwrot

| # | Ulepszenie | Praca dewelopera | Wartość |
|---|---|---|---|
| U01 | Reklamacje closed loop | 2-3 dni | Czyste dane, dyscyplina hodowców |
| U02 | Partie CCP walidator | 2-3 dni | Ochrona BRC compliance |
| U03 | Hodowca Scorecard 360° | 5-7 dni | Pricing oparty na danych, motywacja hodowców |
| U04 | AnalitykaPelna PM drill-down | 3-4 dni | Decyzje o kontraktach |
| U05 | HPAI scheduler | 3-5 dni | Ochrona przed 31M PLN incidentem |
| U06 | Kartoteka shelf life | 1-2 dni | Mniej błędów config |
| U07 | Flota welfare scoring | 2-3 dni | Identyfikacja słabych kierowców/pojazdów |
| U08 | CNA ciągły VLM | 5-7 dni + monthly cost | Pełne pokrycie 24h |

**Całkowita praca ulepszeń**: ~5-6 tygodni jednego deva.
