# 05. BRC v9 / IFS v8 mapping — co która funkcja zamyka

> Pełen BRC v9 ma 7 sekcji × ~25 wymagań = ~181 punktów. Tu pokazuję tylko mapowanie **MOICH PROPONOWANYCH FUNKCJI** do **wymagań krytycznych** (Fundamentals).
> Dla pełnej self-assessment użyj NF12 (`BS_ComplianceRequirement` + `BS_ComplianceStatus`).

## BRC v9 sekcje

### 1. Senior Management Commitment (Fundamental)
- ZPSP nie pokrywa bezpośrednio — to dokumentacja organizacyjna.
- **U03** (Hodowca Scorecard) + **NF12** (Dashboard) daje senior managementowi widoczność KPI.

### 2. Food Safety Plan – HACCP (Fundamental)
- **NF04** (Stunning CCP), **NF05** (Scalding CCP), **NF06** (PM Defects), **NF07** (Chilling CCP) — bezpośrednie zamknięcie sekcji 2.3-2.14 (HACCP Plan: hazard analysis, CCPs, monitoring, corrective action).
- **NF10** (Salmonella) — sek. 2.5 (microbiological hazards).
- **NF11** (Foreign Material) — sek. 2.6 (physical hazards).

### 3. Food Safety and Quality Management System
- **3.4** Internal audit — **U03** (scorecard) + **NF12**.
- **3.9** Traceability — **NF09** (4h recall) — **KRYTYCZNE** (auditor testuje na losowych produktach).
- **3.11** Complaint handling — **U01** (Reklamacje closed loop).

### 4. Site Standards
- **4.2** Site security & food defence — częściowo `CentrumNagranAI/` (CCTV).
- **4.6** Equipment — **NF05** (plucker maintenance log).
- **4.7** Maintenance — **NF11** (tool tracking).
- **4.9** Foreign body detection — **NF11**.
- **4.10** Control of operations — **NF04**, **NF05**, **NF07** (CCP electronic monitoring).
- **4.11** Temperature control — **NF03** (transport), **NF07** (chilling).

### 5. Product Control
- **5.1** Product design — **NF01** (FPD scoring spec).
- **5.4** Product release — **NF02** (antybio withdrawal), **NF08** (vision grading A/B/C).
- **5.6** Pathogen control — **NF10** (Salm/Campy LIMS).

### 6. Process Control
- **6.1** Control of operations — wszystkie NFs CCP.
- **6.3** Calibration — log wymagany dla każdej probe/sensor (auditor sprawdza).
- **6.4** Weight, volume, number control — `KartotekaTowarow/` (mam) + **U06** (shelf life).

### 7. Personnel
- **7.1** Training records — `Kontrola Godzin/` (mam) + ewentualne rozszerzenie o szkolenia BRC.
- **7.3** Protective clothing — wpisuje się w **NF11** (color coding).

## Pokrycie sekcji 4 — "process control" (gdzie jest moja największa luka teraz)

| Wymóg BRC v9 sek. 4 | Status teraz | Status po NF wdrożeniu |
|---|---|---|
| 4.2 Site security | CZĘŚCIOWO (CCTV) | TAK |
| 4.6 Equipment maintenance | NIE | TAK (NF05) |
| 4.7 Tool tracking | NIE | TAK (NF11) |
| 4.9 Foreign body detection log | NIE | TAK (NF11) |
| 4.10 CCP electronic monitoring | NIE | TAK (NF04, NF05, NF07) |
| 4.11 Temp control transport+chill | CZĘŚCIOWO | TAK (NF03, NF07) |
| 4.12 Maintenance after-hours | NIE | CZĘŚCIOWO (NF11 log) |

**Stan obecny**: ~1/7 wymagań sekcji 4 = ~15%.
**Stan po wdrożeniu wszystkich NF**: ~6.5/7 = ~93%.

## IFS v8 — pokrywa podobne wymagania

IFS v8 jest komplementarne do BRC v9 — większość pokrycia BRC zamyka IFS:
- IFS sek. 4 (Process Management) ≈ BRC sek. 4 + 6.
- IFS sek. 5 (Measurements, Analysis, Improvements) ≈ BRC sek. 3.4 + 3.11.

## Dlaczego to się opłaca

- **Klienci retail UE (Lidl, Biedronka, Carrefour, Kaufland)** wymagają minimum BRC + często IFS.
- **Eksport**: bez BRC nie wejdziesz do UK retail (Tesco, Sainsbury's wymagają BRC).
- **Wycena firmy**: certyfikat BRC dodaje ~10-15% do EV w wycenie M&A.

## Ścieżka pre-audyt 2026/2027

### Lipiec 2026
- Self-assessment z NF12 (po wdrożeniu QW01-QW05).
- Identyfikacja TOP 10 gaps.

### Wrzesień 2026
- Wniosek ARiMR (z AR01-AR05).

### Q1 2027
- Implementacja ST01 + ST02 + ST04.

### Q3 2027
- Pre-audit BRC (zewnętrzny consultant 1-2 dni, ~10-15 tys. zł).

### Q4 2027
- Audyt BRC oficjalny (~25-40 tys. zł + ~20 tys. zł roczne utrzymanie).

### 2028
- Audyt IFS (jeśli klienci wymagają — często sklepy DE).
