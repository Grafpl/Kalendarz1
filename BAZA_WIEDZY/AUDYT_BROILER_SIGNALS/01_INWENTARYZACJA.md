# 01. Co już mam w ZPSP — inwentaryzacja względem 12 obszarów książki

> Bazuje na: CLAUDE.md + skanie kodu + `BAZA_WIEDZY/30_POMYSLY/00_INDEX_I_ROADMAPA.md`.
> Status: **NIE** / **CZĘŚCIOWO** / **TAK**.

## 1. Przed ubojem / ferma hodowcy — CZĘŚCIOWO

- `Hodowcy/Models/HodowcaProfilModels.cs` — profil hodowcy, dane kontaktowe, AnimNo (ARiMR).
- `HodowcaStatystyki`: `SrWiekDni`, `SrWagaSzt`, `StratySztProc` — agregaty z partii.
- **Brak**: programy żywieniowe, antybiotyki + okresy karencji, hatchery quality signals, FPD/hock burn, litter type/quality, wiek startu (pierwsze 10 dni).

## 2. Transport żywca — CZĘŚCIOWO

- `Transport/` (TransportPL): `Kurs`, `Ladunek`, `Kierowca`, `Pojazd`, palety E2 (36 szt/paleta, 33 palety/naczepka).
- `Flota/`, `MapaFloty/` — GPS WebFleet, kursy historyczne, mapa pojazdów dziś/historycznie.
- `Transport/SQL/alter_link_to_flota.sql` — link kierowca/pojazd ↔ flota.
- **Brak**: rejestr czasu od catching do bleeding (feed withdrawal compliance), temperatura+RH w naczepie/kontenerach, DOA per kurs, welfare index 9-pkt, microclimate hotspots, fractures/trapped body parts/supine birds count.

## 3. Odbiór i AM inspection — CZĘŚCIOWO

- `PartiaModels.PartiaStatusEnum` — stany `AT_RAMP`, `VET_CHECK` (10-state lifecycle).
- `Partie/SQL/CreatePartieV2.sql` — pole `StatusV2`, audit log, normy QC.
- `BAZA_WIEDZY/SELECTY/20_haccp_jakosc.sql` — placeholder `Haccp`, `QC_Normy`, `QC_Zdjecia`.
- **Brak**: rejestracja DOA na rampie z fotodokumentacją, FCI (Food Chain Information) 24-72h przed dostawą, temperatura kontenerów na rampie (>4°C alert), waga vs deklaracja hodowcy, czas oczekiwania (max 2h, po tym DOA +3% per 15min).

## 4. Stunning & bleeding — NIE

- **Zero** danych w bazie: brak Hz/V/mA water bath, brak CAS CO2 gradient, brak temp gazu CAS, brak procentu purple-after-scalder, brak red wing tips %.
- BRC v9 sekcja 4.2 wymaga monitoringu — **całkowita luka**.

## 5. Scalding & plucking & evisceration — NIE

- `BAZA_WIEDZY/30_POMYSLY/09_Scalding_Monitor.md` + `10_Plucking_Damage_Tracker.md` — istnieją tylko jako idee.
- Brak: temp scalder/tank (0.1-0.2°C precyzja wymagana), licznik wymiany plucking fingers (norma: 200/dzień przy 200k ptaków, ~1 finger/1000), skin ruptures > 3cm count, evisceration paddle calibration log, crop drill hits, faecal contamination rate.

## 6. Patroszenie + PM inspection — CZĘŚCIOWO

- `Reklamacje/Models/ReklamacjeModels.cs` — workflow reklamacji, atrybut do partii/hodowcy (`PartiaDostawcy`).
- `BilansMaterialowyModels.cs` — `WydajnoscUbojuProc`, `WydajnoscKrojeniaProc`, kosze.
- `BAZA_WIEDZY/30_POMYSLY/11_Digital_Inspection_Sheet.md` — pomysł istnieje.
- **Brak**: strukturalna rejestracja wad PM (polyserositis, ascites, hepatitis, cellulitis, cachexia, BCO, wooden breast, white striping, spaghetti meat, GMD, DMP, BBS, TD), kosze odrzutów z przyczyną, target rejection rate <0.5%, kategoryzacja A/B/C, partial vs complete rejection.

## 7. Chilling & cold chain — CZĘŚCIOWO (placeholder)

- `QC_Normy`: `TempChillera` (-2 do 2°C), `TempTunelu` (<-18°C) — TYLKO normy, brak rejestrów.
- `BAZA_WIEDZY/30_POMYSLY/18_Chilling_Curve_Monitor.md` + `19_Cold_Chain_HACCP.md` + `DEEP_DIVE_19_Cold_Chain.md` — opracowane idee, **niewdrożone**.
- **Brak**: pomiar temp core mięsa (<4°C w 6h — EU 92-116), krzywa chłodzenia, air vs spin chiller differentiation, drip loss measurement, ice spray log, czas między bleeding a chilling start.

## 8. Rozbiór, klasyfikacja, wagowanie — TAK

- `AnalitykaPelna/Models/BilansMaterialowyModels.cs` — pełen bilans materiałowy + Stan Magazynów.
- `In0E.QntInCont` 1-12 (klasy wagowe drobiu): Duży (4-7), Mały (8-12). Realna waga palety 500-600 kg.
- `KartotekaTowarow/` — ArticleService, AuditLog, Favorites, Compare, Print card, zdjęcia (BLOB).
- **Brak**: vision grading A/B/C kategoryzacja per tuszka, automatyczne odrzuty B-grade, attribution wad estetycznych do flocku/operatora.

## 9. Pakowanie, MAP, shelf life — NIE

- `Services/IRZplusService.cs` — lot tylko dla ARiMR (żywiec).
- **Brak**: MAP gas mixture log (CO2/N2 %, O2 %), shelf life per produkt, expiry date generowanie, EAN/GTIN, anti-tamper kody na opakowaniach, scenariusz odzyskania całej partii w 4h ("recall window"), drip loss tracking per packaging type.

## 10. Mikrobiologia & HACCP — BARDZO SŁABO

- `Haccp`, `Out1A`, `OdpadyRejestr` w SQL (placeholder).
- `BAZA_WIEDZY/30_POMYSLY/23_Salmonella_Lab_Integration.md` — idea.
- **Brak**: Salmonella SE/ST registry per partia (overshoe testing 21 dni pre-slaughter, cecum sampling), Campylobacter neck skin sampling, CCP monitoring real-time (chilling, scalding, freezing), sample registry z laboratorium, logistic slaughtering scheduler (Salm+ na koniec dnia).

## 11. Reklamacje, klienci, jakość finalna — TAK

- `Reklamacje/` — workflow 6-state, atrybut do partii, statystyki, PDF raport.
- 75% rekordów to auto-import faktur korygujących — szum, brak filtru "prawdziwe reklamacje jakościowe".
- **Brak**: closed-loop feedback do hodowcy (jego partia = X reklamacji → punkty w scorecard), root cause analysis (cellulitis → litter quality → 3 partie temu zaczęło), drip loss feedback per klient/produkt.

## 12. Compliance — CZĘŚCIOWO

- `Services/IRZplusService.cs` + `IRZplusExportService.cs` — ARiMR ✅
- `Admin/LoginAuditWindow.xaml.cs` — audit log (BRC sekcja 3.3 częściowo).
- Hodowcy mają `AnimNo` (numer ARiMR).
- **Brak**: KSeF integracja Sage (deadline minął 2026-04-01), BRC v9 audit-trail dla sekcji 4 (stunning/blood loss), IFS v8 procedury, mapy CCP elektronicznych, "BRC v9 sek. 3" pokrycie ~38% (z audytu branżowego).

## TOP 8 białych plam (do priorytetyzacji)

1. **HACCP CCP electronic monitoring** — chilling/scalding/freezing/stunning — 0/10 elektronicznie. BRC v9 wymóg.
2. **PM inspection digital** — żadnej wady PM strukturalnie. Inspektor wet ma kartkę i klikacz.
3. **Stunning monitoring** — zero parametrów Hz/V/mA/gaz, zero red wing tips KPI.
4. **Lot numbering & traceability 4h** — recall window niemożliwe do zrealizowania.
5. **Salmonella/Campylobacter LIMS** — brak rejestru sampli, brak logistic slaughtering.
6. **Transport CCP (temp+RH+DOA)** — GPS jest, sensorów temp w naczepie nie ma.
7. **Scalding/plucking defects** — temperatura parzelnika, hematomas po plucking, skin ruptures.
8. **BRC v9/IFS v8 audit trail** — brak elektronicznej dokumentacji compliance dla sekcji 4 (process control).
