# 🔥 ANALIZA RUNDY 4 — zależności + walidacja danych

**Data:** 2026-05-04 wieczór
**Plik:** `WYNIKI_ZALEZNOSCI.txt` (1541 linii, 45 sekcji)

---

## 🎯 NAJWAŻNIEJSZE: 5 dramatów wykrytych w danych

### 💔 Dramat 1: **CreateOperator/CloseOperator w `listapartii` to 100% sieroty**
```
100_A_partie_brak_operator
  CreateOperator: 37810 partii, 37810 sieroty (100%!)
  CloseOperator:  37810 partii, 21284 sieroty (56%)
```

**Co to znaczy:** Każda partia ma wpisany `CreateOperator` (np. `'1'`, `'2121'`), ale **NIE MA tego ID w tabeli `operators`**. JOIN który robi PartiaService.cs **w 100% zwraca NULL** — czyli wszystkie kolumny "Imie operatora" w Liście Partii są puste.

**Powód:** Najpewniej operators.ID jest int, a CreateOperator jest varchar(6) — i wartości nie pasują (np. operators ma ID 1980 dla "Justyna TERKA", a w partii jest po prostu "1").

**Fix:** Refactor `PartiaService` — albo operators ma inny klucz, albo w partiach są **stare ID z systemu sprzed 2014**.

---

### 💔 Dramat 2: **`FarmerCalc` ↔ `listapartii` BROKEN — różne formaty Partia**
```
98_B_farmer_vs_listapartii: 801 wpisów FarmerCalc, 0 z linkiem do listapartii
```

**Powód:**
- `FarmerCalc.Partia` = `'99026009001'` (11 cyfr: CustomerID 990 + RR 26 + DDD 009 + AAA 001)
- `listapartii.Partia` = `'26124001'` (8 cyfr: RR 26 + DDD 124 + AAA 001)

**To są DWA różne formaty partii** w jednym systemie! Stąd PartiaService.cs używa `OUTER APPLY (...) WHERE fc2.Partia = lp.Partia ORDER BY fc2.ID DESC LIMIT 1` — zawsze zwraca NULL.

**Fix:** Albo dodać `FarmerCalc.PartiaShort = SUBSTRING(Partia, 4, 8)` jako computed column, albo zmienić JOIN warunek na `fc.Partia LIKE '%' + lp.Partia`.

---

### 💔 Dramat 3: **HarmonogramDostaw workflow potwierdzeń = MARTWY**
```
94_A_harmonogram_workflow (top 30 sample):
  Wszystkie wpisy: Utworzone=NULL, Wysłane=NULL, Otrzymane=NULL,
                   KtoUtw=NULL, KtoWysl=NULL, KtoOtrzym=NULL
```

**Co to znaczy:** Workflow w kodzie istnieje (`HarmonogramDostawRepository.UpdateFlag`), ale **w bazie wszystko NULL**. Audit log w `HarmonogramDostaw_AuditLog` ma 26 wpisów (działa od 2026-04-28) — czyli **workflow zaczął działać dopiero TYDZIEŃ TEMU**!

**26 audit entries:**
- `KtoZmienil = 1122` (jeden user — pewnie test) zmieniał `Utworzone`, `Wysłane`, `Otrzymane` od 2026-04-28 do 2026-05-04.

**Fix:** Workflow jest świeżo wdrożony, dane historyczne są puste. Z czasem się wypełni. **Ale dashboards opierające się o `WHERE Bufor='Potwierdzony'` ukrywają 484+ wpisy ('Do wykupienia') i wszystkie z `Utworzone IS NULL`**.

---

### 💔 Dramat 4: **Reklamacje 2026-04 — gigantyczny skok aktywności**
```
99_A_reklamacje_per_miesiac:
  2025-09: 67  (100% auto-import Symfonia)
  2025-10: 89  (100% auto)
  2025-11: 50  (100% auto)
  2025-12: 73  (100% auto)
  2026-01: 55  (100% auto)
  2026-02: 62  (100% auto)
  2026-03: 86  (96% auto, 3 ręczne)
  2026-04: 139  ← 75 auto + 64 RĘCZNE, 20 sprawdzonych!
  2026-05: 7    (3 ręczne, 3 sprawdzone)
```

**Co to znaczy:** Auto-import korekt Symfonii działa **od września 2025** (nie od 2026-04-01 jak wcześniej myślałem). **Kwiecień 2026 = przełom** — Justyna zaczęła ręcznie wpisywać reklamacje (skok z 3 do 64/m-c).

**Top kategorie prawdziwych reklamacji (kwiecień 2026):**
1. **Mniejsza waga niż na fakturze: 29** ← TO JEST GŁÓWNY PROBLEM (50% reklamacji jakości!)
2. Zły zapach: 14
3. Niewłaściwy wygląd: 8
4. Zła temperatura/rozmrożony: 3
5. Zanieczyszczenie: 2

**Top handlowcy w reklamacjach (6 mies):**
- **Jola: 248** (211 auto + 37 ręczne) → suma_wartości 498k zł
- **Maja: 156** (127 auto + 29 ręczne) → suma_wartości **1.5 mln zł** (większe zamówienia)
- Ania: 17
- Teresa: 12 (200k zł — duże partie)
- Ogólne: 28

**Wniosek:** Maja sprzedaje **3x więcej wartości** niż Jola, ale ma mniej reklamacji liczbowo. **Jola = wąskie gardło reklamacji**, ale tylko per liczba (każda ma niską wartość).

---

### 💔 Dramat 5: **Klasy ujemne `-1, -6, -10` w `In0E.QntInCont` to STORNO ważeń**
Już wykryte w rundzie 2 — potwierdzone tutaj jako wzorzec NEPAL/SUMAN.

---

## ✅ Co DZIAŁA dobrze

### ✅ TransportPL — zerowych broken FK
```
96_B_kursy_bez_kierowcy: 0
96_C_ladunki_bez_kursu:  0
```
**TransportPL ma czyste relacje.** 1498 kursów + 2011 ładunków + 0 sierot.

**`KodKlienta` w `Ladunek`:**
- `'ZAM_*'`: **2001** (99.5%)
- `'INNE'`: 10
**Wzorzec działa dobrze.**

### ✅ ZamowieniaMieso `Cena` jako VARCHAR — wszystkie wartości numeryczne
```
92_B_cena_walidacja:
  razem: 12697, puste: 0, numeryczne_OK: 12697, BLEDNE: 0
```
Mimo że typ varchar, **wszystkie wartości są poprawne liczby**. Refactor na DECIMAL nie pilny.

### ✅ TransportZmiany — workflow akceptacji DZIAŁA aktywnie
```
96_E_zmiany_typy_statusy (od 2026-04-07):
  NoweZamowienie Oczekuje: 636
  ZmianaKg Zaakceptowano: 188 / Oczekuje: 114
  ZmianaPojemnikow Zaakceptowano: 183 / Oczekuje: 112
  ZmianaIlosci Zaakceptowano: 155 / Oczekuje: 68
  ZmianaUwag Zaakceptowano: 127 / Oczekuje: 89
  Anulowanie Oczekuje: 34 / Zaakceptowano: 1
```

**Łącznie 1947 zmian = ~75/dzień, 30% Oczekuje** (zalecam zatwierdzić — 636 nowych zamówień czeka!)

### ✅ Cross-validation In0E vs WagoCounter
```
97_C_wagocounter_vs_in0e (per dzień):
  2026-05-04: 64 195 sztuk z Wago, 228 palet × 1.9 kg/szt
  2026-04-29: 68 184 sztuk, 247 palet × 1.78 kg/szt
  2026-04-23: 67 375 sztuk, 247 palet × 1.96 kg/szt
  2026-04-13: 65 805 sztuk, 196 palet × 1.57 kg/szt (mały dzień)
```

**WagoCounter zgadza się z In0E.** Średnia waga 1 sztuki **dryfuje 1.57 → 1.97 kg** w zależności od dnia → **niesamowity KPI** pokazujący profil wagowy hodowców.

### ✅ Anulacje per klient + przyczyny — zdrowe statystyki
- 13% średnia anulacji (poprawia się z 17% w lutym do 8% w kwietniu)
- 0 zamówień bez pozycji (`100_F`)
- 0 sierot `HarmonogramLp` (`100_G`)

### ✅ KartotekaScoring — kategorie klientów
```
95_B_kategorie:
  C: 24 klientów
  B: 22
  A: 14
  D: 3
```
63 klientów z scoringiem (tylko 0.3% wszystkich 20k klientów w `OdbiorcyCRM` — bardzo mało, do uzupełnienia).

---

## 🌟 NAJLEPSZE ODKRYCIE: WagoCounter ↔ In0E = PERFEKCJA DASHBOARDU

Połączenie tych dwóch tabel daje **kompletny obraz produkcji per dzień**:

| Data | Sztuk Wago | Palety In0E | kg In0E | **kg/sztuka** |
|---|---|---|---|---|
| **2026-05-04** | 64 195 | 228 | 121 778 | **1.90 kg** |
| 2026-04-29 | 68 184 | 247 | 121 169 | 1.78 kg |
| 2026-04-23 | 67 375 | 247 | 132 131 | **1.96 kg** ← duże ptaki |
| 2026-04-13 | 65 805 | 196 | 103 473 | **1.57 kg** ← małe |
| 2026-04-07 | 44 024 | 146 | 78 435 | 1.78 kg |

**Średnia waga 1 ptaka per dzień = niesamowite KPI dla Sergiusza:**
- Pokazuje **profil hodowców** (czy idą na małe szybkie czy duże dłuższe)
- Pokazuje **klasy wagowe rozkład** (mniejsza średnia = klasy 9-10, większa = klasy 5-6)
- Cross-correlation z `Pozyskiwanie_Hodowcy` da ranking
- **DASHBOARD GOTOWY DO NAPISANIA**

---

## 📊 Ekstra konkrety: dziś 2026-05-04 (czwartek)

### Kursy (TransportPL):
**11 kursów aktywnych dziś:**
- BIMEX (Robert Staroń, SCANIA WGM 7736H, 33 palety, 1260 E2)
- RADDROB Chlebowski (Grzegorz Staniszewski, EBR 8J11, 33 palety, 1320 E2)
- Damak (Robert Panak, EBR 9C90, 33 palety, 1320 E2)
- PUBLIMAR→Ladros (Robert Panak, kombinowana trasa, 1034 E2)
- Romanowska→Trzepałka (Grzegorz Staniszewski, kombinowana, 831 E2)
- BOMAFAR, INEX, PODOLSKI, SMOLIŃSKI, EGE FOOD, PIEKARSCY

### Hodowcy dziś (z WagoCounter + In0E):
- **Ferma Sobota (552):** 6 partii (26124003, 06, 07, 10-15) ← **dominuje 50%**
- **Ferlich Adriana (762):** 2 partie
- **Osadowski Piotr (495):** 2 partie
- **Przybysz Przemysław (539):** 2 partie

### Operatorzy ważenia 90 dni (top 5):
| OperatorID | Wagowy | Ważeń | Storno % | kg |
|---|---|---|---|---|
| 0101 | NEPAL | **91 579** | 0.6% | 1.21 mln |
| 8822 | SUMAN | 38 256 | 0.3% | 567k |
| 4433 | Zuzanna Garnys | 10 956 | 0.08% | **5.8 mln** (paletystka) |
| 8921 | GOPAL | 10 038 | 0.5% | 148k |
| ... | ... | ... | ... | ... |

---

## 🎯 CO MYŚLĘ — KOŃCOWE WNIOSKI

### 1. Twoja baza jest LEPIEJ niż się obawiałem
- **Cena VARCHAR działa OK** (12 697 wszystkich numeryczne)
- **TransportPL ZERO sierot** (Kierowca, Pojazd, Kurs, Ladunek)
- **WagoCounter pisze codziennie 32 m-ce** (Wago działa!)
- **TransportZmiany workflow działa aktywnie** (1947 zmian/30 dni)
- **Reklamacje są bogato opisane** (KategoriaPrzyczyny, PodkategoriaPrzyczyny, Handlowiec)
- **HarmonogramDostaw audit** działa od kwietnia
- **`FarmerCalc` ma 103 kolumny** dające pełną historię odbioru żywca

### 2. Dramaty do naprawy (nie wszystkie razem!)
| # | Dramat | Skala | Trudność fix |
|---|---|---|---|
| 1 | listapartii.CreateOperator 100% sieroty | Wszystkie 37810 partii | **DUŻA** (refactor JOIN, mapowanie ID) |
| 2 | FarmerCalc.Partia ≠ listapartii.Partia (różne formaty) | 801 sierot | Średnia (computed column) |
| 3 | HarmonogramDostaw workflow puste | 484+ wpisów | Łatwa (czas się wypełni) |
| 4 | Reklamacje wybuch w kwietniu | 64 ręczne wpisy | OK (Justyna zaczęła pracować) |
| 5 | KartotekaScoring tylko 63 klientów | Z 20k klientów | Łatwa (auto-import + algorytm) |

### 3. Najwyższy ROI 1-tygodniowy projekt: **Hala LIVE z WagoCounter**
**Dane są już dziś w bazie. Wystarczy je ładnie pokazać.**

```
KPI dziś 2026-05-04 (real-time):
  ✓ Sztuki ubite: 64 195 (z WagoCounter)
  ✓ Aut przyjętych: 15 (z WagoCounter.MAX(CarLP))
  ✓ Średnia waga sztuki: 1.90 kg
  ✓ Top hodowca dnia: Ferma Sobota (50%)
  ✓ Tempo bieżące: ~4200 sztuk/godzinę
```

### 4. Średnia waga sztuki = MEGA KPI
Trend dziennej średniej kg/sztuka **fluktuuje 1.57 → 1.97 kg** — to ogromny insight który nikt jeszcze nie pokazuje. Sergiusz może patrzeć codziennie i wiedzieć:
- "Dziś mali ptacy → klasy 9-10 dominują"
- "Wczoraj duzi → klasy 5-6"
- Cross z hodowcami → kto dostarcza zgodnie z deklaracją

### 5. Reklamacje per kategoria
**29/64 prawdziwych reklamacji w kwietniu = "Mniejsza waga niż na fakturze"** — to pokazuje że **klienci sprawdzają wagi i są niezadowoleni**. Pewnie waga selektywna ma odchylenia poza normy, ale klient sprawdza i wraca z reklamacją.

**Akcja Sergiusza:** sprawdzić czy WAGO selektywna jest skalibrowana zgodnie z `Article.StandardTol/StandardTolMinus`. Jeśli StandardTol Kurczak A = ±0.31kg na 15kg, ale klient widzi -0.5kg → kalibracja problem.

---

## 🚀 CO POLECAM ZROBIĆ TERAZ — DECYZJA

**Top 3 najwyższy zwrot:**

### A) **Dashboard "Hala LIVE" z WagoCounter + In0E** (1 tydzień)
**Dane:** już w bazie, walidowane.
**Efekt:** Sergiusz, Justyna i Łukasz widzą real-time:
- Sztuki ubite dziś
- Średnia waga ptaka (KPI rzadko spotykane)
- Top hodowca dnia
- Tempo linii bieżące
- Lista aut z postępem (CarLP 1→15)

### B) **Dashboard reklamacji z prawdziwymi przyczynami** (3-4 dni)
**Dane:** już w bazie, audit działa.
**Efekt:** Justyna widzi:
- 29 reklamacji "Mniejsza waga" → trend
- Top handlowcy z prawdziwymi reklamacjami
- Auto-filtr `WymagaUzupelnienia=0` żeby nie zalewać auto-importem
- Akcja: kalibracja WAGO

### C) **Naprawa broken FK listapartii.CreateOperator** (1-2 dni)
**Dane:** wymaga researchu — co to za stare ID, jakie mapowanie do operators.ID
**Efekt:** Wszystkie 37810 partii zaczyna pokazywać poprawne imię operatora w Liście Partii.

---

## ❓ Decyzja

**Wybierasz:**
- **A)** Hala LIVE (WagoCounter + In0E) — **REKOMENDUJĘ**
- **B)** Dashboard reklamacji — łatwy ale mniejszy efekt
- **C)** Naprawa operatorów — techniczna naprawa
- **D)** A+B w 2 tygodnie
- **E)** Wszystkie 3 w 4 tygodnie
- **F)** Inny moduł — powiedz

Lub: **przeczytaj `WYNIKI_ANALIZA_RUNDA4.md`** (ten plik) i daj mi swoją reakcję.

---

## 📁 Co masz teraz

W `BAZA_WIEDZY/`:
- 22 plików profilu firmy / programu / baz
- `AUDYT_KODU_SQL.md` — co kod realnie robi z bazą

W `BAZA_WIEDZY/SELECTY/`:
- 6 plików wyników SQL (~24k linii)
- 4 pliki analizy (rundy 1, 2, finalna, runda 4)
- 5 plików .sql do uruchomienia

**To jest najbardziej kompletna dokumentacja Twojego ekosystemu jaką możesz mieć.**
