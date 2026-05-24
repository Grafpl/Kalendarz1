# 04. Prioryzacja — co kiedy zrobić (dzień po dniu)

> Trzy koszyki: **QW** (Quick Wins — weekend / kilka dni), **ST** (Strategic — perspektywa roku), **AR** (ARiMR — pod wniosek do IX.2026).
> Wszystkie szacunki czasowe = **godziny prawdziwego klepania kodu**. Plan zakłada 4-5h pracy ZPSP/dzień (reszta dnia to firma).

---

## Koszyk QW — Quick Wins (1 miesiąc, sam, zerowy CAPEX)

### Tydzień 1 — QW01: Reklamacje filtr i typ (U01)

**Cel**: Czysta lista reklamacji bez 75% szumu z faktur korygujących.

| Dzień | Co | Ile godzin |
|---|---|---|
| pon | DDL: alter `Reklamacje` table dodaj kolumnę `TypReklamacji NVARCHAR(30)`. Backfill: wszystkie istniejące jako 'KOREKTA_FAKTURY'. | 2h |
| wt | C#: enum `TypReklamacji` w `Reklamacje/Models/ReklamacjeModels.cs`. Dodaj combo w `ReklamacjaEditWindow.xaml`. | 3h |
| śr | C#: domyślny filtr "wyłącz korekty faktur" w `Reklamacje/Views/ListaReklamacjiWindow.xaml`. Toggle "pokaż wszystko". | 2h |
| czw | C#: method `SuggestPartieAsync(klientId, data)` w `ReklamacjeService.cs` — szuka partii dostarczonych klientowi ±2 dni. | 3h |
| pt | C#: button "Sugeruj partię" w edytorze, lista z 5 sugestiami. Test ręczny + commit. | 3h |

**Razem tydzień 1**: ~13h pracy. **Wartość**: Jola od poniedziałku 2 tygodnia widzi tylko prawdziwe reklamacje.

---

### Tydzień 2 — QW02: Antybiotyki rejestr (NF02 podstawowy)

**Cel**: niemożliwe jest zaplanować partię z naruszeniem withdrawal.

| Dzień | Co | Ile godzin |
|---|---|---|
| pon | DDL: `BS_Antybiotyk`, `BS_FarmTreatment`, `BS_ResidueTest` (z `06_SQL_DDL.sql`). Seed 7 antybiotyków. | 2h |
| wt | C#: `Hodowcy/Views/FarmHealthRecord.xaml` — formularz wpisu kuracji. | 4h |
| śr | C#: walidator w `Partie/Services/PartiaService.CreatePartiaFromHarmonogramAsync` — sprawdź `DataMozliwegoUboju`. | 3h |
| czw | C#: alert czerwony w `NowaPartiaDialog.xaml` przy konflikcie. Override z hasłem (audit log). | 3h |
| pt | C#: dashboard kafelek "Antybio-clean flocks last 90d" w `MarketIntelligence/Briefing`. | 2h |

**Razem tydzień 2**: ~14h. **Wartość**: Justyna ma to za sobą, system pilnuje.

---

### Tydzień 3-4 — QW03: Hodowca Scorecard 360° + FPD (NF01 + U03)

**Cel**: po 30 dniach miesz pierwsze scorecardy 12 hodowców.

#### Tydzień 3:

| Dzień | Co | Ile godzin |
|---|---|---|
| pon | DDL: `BS_FlockScoring`, `BS_HodowcaScorecard`. | 1h |
| wt | C#: `FPDLoggerTablet.xaml` (mockup M03) — 3 duże klasy + Y/N hock + Y/N scratch. | 5h |
| śr | C#: integracja kamery (z `CentrumNagranAI/`) — foto klasy 2 podczas kliknięcia. | 3h |
| czw | C#: serwis `BS_HodowcaScorecardService.RecalculateAsync(hodowcaId)` — agregat 6 wskaźników. | 4h |
| pt | C#: trigger po `BS_PM_DailySummary` UPDATE → recalculate. Test ręczny. | 2h |

#### Tydzień 4:

| Dzień | Co | Ile godzin |
|---|---|---|
| pon | C#: nowy tab `Hodowcy/Views/HodowcaScorecardTab.xaml` — radar chart (LiveCharts). | 5h |
| wt | C#: ranking TOP/MID/BOTTOM, kolory zgodnie z poziomami. | 3h |
| śr | C#: button "Drukuj kartę 12-mies." → PDF (mam już infrastrukturę z Reklamacje). | 4h |
| czw | C#: button "Wyślij hodowcy" (Gmail MCP). | 2h |
| pt | Test ręczny z 3 hodowcami + dokumentacja w `BAZA_WIEDZY/`. | 3h |

**Razem tydzień 3+4**: ~32h. **Wartość**: scorecard działa, po 1 miesiącu kliknięć Justyna/Maja ma realne dane.

---

### Tydzień 5 — QW04: Partie CCP walidator + QW05: Traceability minimum

#### Tydzień 5:

| Dzień | Co | Ile godzin |
|---|---|---|
| pon | C#: `Partie/Services/PartiaService.ValidateCCPBeforeStatusChange()` — sprawdzanie BS_PathogenSample + BS_FarmTreatment. | 4h |
| wt | C#: tooltip "CCP compliance: X/Y" w `Partie/Controls/StatusBadge.xaml`. | 2h |
| śr | DDL: `BS_PackagingBatch`, `BS_TraceabilityScan`. CREATE VIEW `BS_TraceabilityFull`. | 2h |
| czw | C#: `Reklamacje/Views/TraceabilityWindow.xaml` (mockup M06) — sklejony 1 ekran. | 5h |
| pt | C#: button "Skanuj QR" → kamera laptopa lub ręczny scanner. Test. | 3h |

**Razem tydzień 5**: ~16h. **Wartość**: BRC sek. 3.9 i 4.10 częściowo zamknięte.

---

### QW Total: **~75h pracy = ~5 tygodni**. CAPEX: 0. Wszystkie 5 quick winów zrealizowane.

**Po tych 5 tygodniach masz**:
- ✅ Czyste dane reklamacji.
- ✅ Walidację antybiotyków (zero ryzyka uboju w withdrawal).
- ✅ Pierwsze scorecardy 12 hodowców (po miesiącu kliknięć).
- ✅ Niemożliwość zaaprobowania partii bez CCP compliance.
- ✅ Traceability dla audytora BRC (manualne QR scan).

---

## Koszyk ST — Strategic (perspektywa roku, z CAPEX)

### Czerwiec-Lipiec 2026 — ST01 + ST04: Stunning + Scalding (linia ubojowa)

**Założenie**: To samo dotknięcie linii. Robić razem.

**Wymagania zewnętrzne**:
- Spotkanie z dostawcą linii (Marel / Foodmate / Meyn).
- Spec API PLC (Modbus TCP / OPC UA).
- Cena adaptera PLC + instalacja (zwykle 20-40 tys. zł).

**Plan tygodniowy** (10 tygodni):

| Tydzień | Co |
|---|---|
| T1 | Spotkanie z dostawcą linii. Otrzymanie spec API. |
| T2-3 | DDL: `BS_StunningSession`, `BS_StunningParam`, `BS_StunningQuality`, `BS_ScaldingLog`, `BS_PluckerMaintenance`, `BS_PluckingQuality`. |
| T4-5 | C#: serwis czytający z PLC (Modbus TCP library: NModbus4). |
| T6 | C#: `StunningBayDashboard.xaml` (mockup M04). |
| T7 | C#: integracja Hikvision + Claude VLM dla "purple bird detection". |
| T8 | C#: alerty SMS (Twilio / SMS gateway PL). |
| T9 | C#: dashboard scalding + plucker maintenance log. |
| T10 | Test produkcyjny + dokumentacja. |

**Praca dewelopera**: ~150h (3-4h/dzień × 50 dni).
**CAPEX**: ~30 tys. zł (PLC adapter, tablet bay, 50" TV monitor).
**OPEX**: VLM ~3 tys. zł/mies (Haiku 4.5 + smart sampling).
**Płatność zwrotu**: 1 miesiąc operacji (wartość ~2.5M/rok).

---

### Sierpień-Wrzesień 2026 — ST02: Chilling Curve (z chłodnią glikolową)

**Założenie**: planowana inwestycja w **chłodnię glikolową 2,8 mln** — zaprojektować BACnet od początku!

**Krytyczne**: przed zamówieniem chłodni wymagaj od dostawcy:
- BACnet IP output (nie tylko Modbus).
- Probe insertable do core temp.
- Raw data feed (nie tylko alarms).

**Plan tygodniowy** (8 tygodni):

| Tydzień | Co |
|---|---|
| T1 | Spec BACnet od dostawcy chłodni. |
| T2 | DDL: `BS_ChillSession`, `BS_ChillTempLog`, `BS_ChillCompliance`, `BS_DripLoss`. |
| T3-4 | C#: serwis BACnet (library: BACnetSC). |
| T5 | C#: `ChillingCurveWindow.xaml` (mockup M08) z prognoza time-to-4°C. |
| T6 | C#: drip loss formularz pomiaru (sample przez operatora 1x/dziennie). |
| T7 | C#: alerty + dashboard miesięczny compliance. |
| T8 | Test produkcyjny + dokumentacja. |

**Praca dewelopera**: ~100h.
**CAPEX**: ~20 tys. zł (probes + BACnet adapter).
**Płatność zwrotu**: 1 miesiąc (wartość ~3.4M/rok drip loss redukcji).

---

### Październik-Listopad 2026 — ST03: PM Defects tablet (NF06)

| Tydzień | Co |
|---|---|
| T1 | DDL: `BS_PM_DefectDict` (seed 21 wad), `BS_PM_Defect`, `BS_PM_DailySummary`. |
| T2-3 | C#: `PMInspectionTablet.xaml` (mockup M02) — 21 dużych kafelków. |
| T4 | C#: auto-trigger update `BS_PM_DailySummary` (SQL trigger albo .NET service). |
| T5 | C#: integracja z Hodowca Scorecard (closed loop). |
| T6 | C#: dashboard top 3 wady per dzień. |
| T7-8 | Trening weterynarzy (Janek + zastępcy) + zakup 4 tabletów + test pilot. |

**Praca dewelopera**: ~120h.
**CAPEX**: ~15 tys. zł (4 tablety panasonic toughpad lub równoważne + ścienne mocowania).
**Płatność zwrotu**: 2 miesiące (wartość ~1.3M/rok rejection redukcja).

---

### Grudzień 2026 — Styczeń 2027 — ST05: Transport CCP (NF03)

| Tydzień | Co |
|---|---|
| T1 | Zakup 24 czujników (2 per pojazd × 12 pojazdów). SensorPush BT / ESP32 LTE. |
| T2 | Wymontowanie + montaż w jednym pojeździe pilot. |
| T3 | DDL: `BS_TransportClimat`, `BS_RampInspection`. |
| T4 | C#: serwis odbierający dane (HTTP API od czujników). |
| T5 | C#: `RampInspectionTablet.xaml` (mockup M01). |
| T6 | Wymontowanie + montaż w pozostałych 11 pojazdach. |
| T7 | C#: scoring kierowcy + alerty hotspot. |
| T8 | Test produkcyjny + integracja z WebFleet GPS. |

**Praca dewelopera**: ~80h.
**CAPEX**: ~11 tys. zł (24 czujniki).
**Płatność zwrotu**: 2 miesiące (wartość ~210k/rok DOA redukcja).

---

### Luty 2027 — ST06 + ST07: Salmonella LIMS + BRC audit trail

| Tydzień | Co |
|---|---|
| T1 | Umowa z laboratorium (SGS / JS Hamilton / Eurofins) — email feed. |
| T2 | DDL: `BS_PathogenSample`, `BS_LogisticSlaughter`. |
| T3 | C#: OCR PDF lab results (Azure OCR / Tesseract). |
| T4 | C#: scheduler logistic slaughter (auto-przesuwa Salm+ na koniec dnia). |
| T5 | DDL: `BS_ComplianceRequirement`, `BS_ComplianceStatus`. Seed BRC v9 sekcja 4 + 5. |
| T6 | C#: `BRCDashboard.xaml` (mockup M07). |

**Praca dewelopera**: ~60h.
**CAPEX**: ~3 tys. zł (laptop dla labu / drukarka termiczna).
**OPEX**: 30k zł/rok kontrakty laboratoryjne.

---

### ST Total: **~510h pracy = 8-9 miesięcy. CAPEX ~80 tys. zł. Roczny zysk ~10-13M zł.**

---

## Koszyk AR — ARiMR-fundable (deadline IX.2026)

### Strategia wniosku

**Tytuł projektu**: "Modernizacja ubojni drobiu Piórkowscy z wdrożeniem AI quality assurance, real-time CCP monitoring i traceability zgodnej z BRC v9 / IFS v8"

**Łączna wartość projektu**: ~1-2 mln zł (z VAT).

**Wkład własny**: ~40% (400-800 tys. zł).
**Dofinansowanie**: ~60% (600 tys. - 1.2 mln zł).

### AR01 — Vision Grading + Continuous VLM (NF08 + U08) — FLAGSHIP

**CAPEX uzasadniony w wniosku**:
- 4 kamery 4K IP (~5 tys. zł/szt) = 20 tys. zł
- 1 GPU box (RTX 4090 lub edge AI box) = 30 tys. zł
- 2 tablety do live monitoring = 8 tys. zł
- Instalacja + okablowanie = 15 tys. zł
- Razem CAPEX: **73 tys. zł**

**OPEX**: 
- Claude API ~80 tys. zł/rok
- Konserwacja kamer ~5 tys. zł/rok

**Plan w wniosku**:
- Q1 2027: zakup sprzętu + instalacja
- Q2 2027: integracja z ZPSP, kalibracja VLM
- Q3 2027: produkcyjne uruchomienie
- Q4 2027: raport ewaluacji

**Argumenty (kopiuj do wniosku)**:
> "System ciągłego monitoringu wad PM zgodny z BRC v9 sekcja 4. Wykorzystanie generatywnej AI (LLM klasy Claude Sonnet 4.6) do automatycznej klasyfikacji wad jakościowych. Redukcja food waste przez wczesne wykrywanie wad estetycznych. Polski wkład w europejski standard quality assurance broiler meat."

### AR02 — Stunning + Chilling CCP combo (NF04 + NF07) — FLAGSHIP

**CAPEX**:
- PLC adapter dla linii ubojowej = 20 tys. zł
- BACnet adapter dla chłodni glikolowej = 8 tys. zł
- 6 probes insertable (core temp) = 6 tys. zł
- 2 ścienne monitor 50" = 6 tys. zł
- Instalacja + integracja = 15 tys. zł
- Razem CAPEX: **55 tys. zł**

**Argumenty wniosku**:
> "Pierwsza w PL implementacja real-time CCP monitoring na linii drobiarskiej zgodna z dyrektywą EC 1099/2009 (welfare) oraz EC 92-116 (cold chain). Eliminacja DOA + drip loss = zmniejszenie GHG emissions per kg produkowanego mięsa o szacunkowo 5-8%."

### AR03 — End-to-end traceability + MAP (NF09)

**CAPEX**:
- Gas analyzer MAP (Mocon Pak Check / Witt) = 20 tys. zł
- 2 drukarki termiczne anti-tamper = 6 tys. zł
- QR/RFID infrastructure (HHT terminale 10 szt) = 15 tys. zł
- Razem CAPEX: **41 tys. zł**

**OPEX**: gaz MAP ~150 tys. zł/rok (CO2 + N2).

**Argumenty wniosku**:
> "Implementacja systemu traceability pozwalającego na recall w ciągu 4 godzin (BRC v9 sek. 3.9). Wsparcie ekspansji eksportowej (MAP shelf life 30-60 dni umożliwia eksport na rynki Czech, Słowacji, Niemiec). Polski drób z polskim labelem jakości."

### AR04 — Antybiotyki + Salmonella LIMS (NF02 + NF10) — One Health

**CAPEX**: minimalny (~5 tys. zł — laptop dla weterynarza farmy).
**OPEX**: 30 tys. zł/rok kontrakty laboratoryjne.

**Argumenty wniosku**:
> "Pełny rejestr antybiotyków zgodny z Antibiotic Stewardship Programme (UE priorytet). Wczesne wykrywanie SE/ST → ochrona konsumenta. Redukcja AMR (Antimicrobial Resistance) — krytyczne dla zdrowia publicznego."

### AR05 — Welfare Index + Transport CCP (NF03)

**CAPEX**: ~15 tys. zł (24 czujniki IoT + 5 tabletów wstępnych).

**Argumenty wniosku**:
> "Zgodność z EC 1/2005 (animal welfare in transport). Eliminacja DOA + redukcja transport stress = lepsza jakość mięsa. Konkurencja z Mercosur — bronimy się polską jakością."

### AR Total

| Pozycja | Kwota |
|---|---|
| CAPEX wszystkich AR01-AR05 | ~189 tys. zł |
| OPEX roczny | ~265 tys. zł |
| Razem inwestycja (3 lata) | ~984 tys. zł |
| **Wnioskuj o dofinansowanie 60%** | **~590 tys. zł** |

---

## Reguła kolejności (mini-master plan)

```
2026
─────────────────────────────────────────────────────────────────────────
Maj-Czerwiec:    QW01-QW05 (Quick Wins, 5 tygodni)
Lipiec-Sierpień: Składanie wniosku ARiMR + spotkania z dostawcami linii
Wrzesień:        Czekanie na ARiMR (deadline IX) + ST01 start jeśli env. gotowe
Październik:     ST01 (Stunning) + ST04 (Scalding) — linia ubojowa
Listopad:        ST02 (Chilling) — z dostawą chłodni glikolowej
Grudzień:        ST03 (PM Defects tablet)

2027
─────────────────────────────────────────────────────────────────────────
Styczeń-Luty:    ST05 (Transport CCP) + ST06 (Salmonella LIMS)
Marzec:          ST07 (BRC audit trail) + AR projects rusza po decyzji ARiMR
Kwiecień-Maj:    AR01-AR05 implementacja
Czerwiec-Lipiec: Pre-audit BRC zewnętrzny consultant
Sierpień:        Audyt BRC oficjalny (~25-40 tys. zł)
Wrzesień:        Certyfikat BRC v9 🎉
```

---

## Czego NIE robić teraz

- ❌ **Nie zaczynaj od vision grading** bez najpierw NF06 (PM Defects tablet). Vision potrzebuje training data, którą daje NF06.
- ❌ **Nie inwestuj w PLC integration** zanim dostawca linii (Marel/Foodmate) da Ci specyfikację API. To kosztuje 0 zł rozmowa, ale ratuje 30 tys. zł błędnej decyzji.
- ❌ **Nie kupuj zewnętrznego LIMS** jako SaaS — twoje laboratoria i tak wysyłają PDF. OCR + ręczna weryfikacja = 80% wartości za 0% kosztu.
- ❌ **Nie wdrażaj BRC v9 audit trail (NF12) jako pierwszy moduł** — to view na innych modułach. Bez NF03-NF10 nie ma czego pokazać.
- ❌ **Nie rób wszystkiego sam jeśli nie musisz** — ARiMR pieniądze pozwalają na hiring 1 dewelopera full-time (~120 tys. zł/rok) na 2 lata. Pomyśl czy nie warto.
- ❌ **Nie zaczynaj transformacji Sp. z o.o. równolegle z dużą zmianą IT** — najpierw stabilizuj firmę (transformacja 01.08.2026), POTEM (od września) komplikuj IT.

---

## Co zrobić teraz, w ten weekend (jeśli masz 2h)

1. Otwórz `BAZA_WIEDZY/AUDYT_BROILER_SIGNALS/07_SLOWNICZEK.md` i przejrzyj — przygotuj się językowo.
2. Otwórz `08_DZIEN_Z_ZYCIA.md` i przeczytaj scenariusze 1, 2, 4. To 15 min.
3. Otwórz `09_MOCKUPY_UI.md` i przejrzyj mockupy. To 10 min.
4. Zdecyduj: czy QW01 (reklamacje) odpalasz w poniedziałek? Jeśli tak — poniedziałek 9:00 zaczynasz od DDL alter table.
5. Wyślij maila do dostawcy linii (Marel/Foodmate): "Proszę o specyfikację API PLC dla naszej linii. Planujemy integrację z naszym systemem ERP w Q4 2026."

To tyle. Reszta przyjdzie krok po kroku.
