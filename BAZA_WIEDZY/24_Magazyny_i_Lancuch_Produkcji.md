# 24. Magazyny i łańcuch produkcji (HANDEL/Sage)

> Pełna mapa magazynów Symfonii w bazie HANDEL Pióroskovskich + łańcuch produkcji + wydajności + przepływy MM-.
> Odkryte przez analizę kodów MM+/MM- i agregację `sPWU`/`sPWP` per magazyn (2026-05-09).

---

## 1. Mapa magazynów (14 ID, real names)

Sage Symfonia **NIE TRZYMA nazw magazynów w bazie HANDEL** (patrz `23_HANDEL_Schema_Sage_Symfonia.md` sekcja 4.5). Real nazwy zostały **wyekstrahowane z sufiksów kodów dokumentów** `MM+`/`MM-` (np. `"0001/22/MM-/M. PROD"` → magazyn=65554, sufiks "M. PROD" = nazwa).

### 1.1. Aktywne magazyny produkcyjne (główny łańcuch)

| ID | Skrót Symfonia | Pełna nazwa | Rola w łańcuchu | Główne serie |
|---:|---|---|---|---|
| **65555** | M. UBOJ | Magazyn ubojni | Tuszki / podroby / odpady po uboju | `sPWU` (PRZ), `sMM-` (do M.PROD) |
| **65554** | M. PROD | Magazyn produkcji / krojenia | Krojenie tuszek na elementy | `sPWP` (PRZ), `sRWP` (do krojenia), `sMM-` (na DYST/MROŹ) |
| **65556** | M. DYST | Magazyn dystrybucji | Sprzedaż klientom | `sMM+` (z PROD), `sWZ` (do klientów) |
| **65552** | M. MROŹ | Mroźnia | Mrożenie nadprodukcji | `sMM+` (z PROD), `sMM-` (back do PROD) |
| **65562** | M. MASAR | Masarnia | Wędliny i przetworzone | `sPPM` (PRZ), `sRPM` (rozchód) |
| **65547** | KARMA | Magazyn produkcji karmy | Karma dla zwierząt z odpadów | `sPPK` (PRZ), `sRPK` (rozchód) |
| **65551** | M. ODPA | Magazyn odpadów | Pióra, krew, niejadalne | `sMM+` (z UBOJ/PROD) |
| **65564** | M. ROZCH | Magazyn rozchodu | Pomocniczy buffer | `sMM+`, `sRWP` |
| **65559** | Mag. opak. | Magazyn opakowań | Folie, taśmy, etykiety | `sMW`, `sMP` |

### 1.2. Magazyny faktur / zakupowe (zewnętrzne)

| ID | Skrót | Co tu trafia | Charakter |
|---:|---|---|---|
| **65550** | Mag. faktur | sPZ od hodowców (główny) + sWZ-W wewnętrzne | Buffer fakturowy (1.5M kg, 102k pozycji) |
| **65543** | Mag. 65543 | sPZ od TASOMIX (paszy) | Specyficzny dostawca (120k kg) |
| **65566** | Mag. 65566 | sPZ od Samol/Ekoplon | Inny dostawca pasz/żywca (35M kg) |

### 1.3. Kategorie towarów (wpadają jako "magazyn" w `MG.katalog`)

| ID | Nazwa | Charakter |
|---:|---|---|
| 65882 | Kategoria: Żywiec | Kurczak żywy 7-12 (klasy wagowe) |
| 65883 | Kategoria: Pasze | Pasze dla drobiu |

**UWAGA**: 65882 / 65883 to NIE magazyny tylko **`HM.TW.katalog`** wartości. Czasem mylone bo mają podobne ID. Filtr `WHERE MZ.magazyn IS NOT NULL AND MZ.magazyn IN (...realne magazyny...)`.

### 1.4. Magazyny historyczne / nieaktywne

W okresie 2022-2026 część magazynów zostala zlikwidowana lub przemigrowana. Sprawdzaj `MAX(data)` per magazyn — jeśli `> rok temu` to nieaktywny.

---

## 2. Łańcuch produkcji ZPSP (od żywca do klienta)

```
                         ┌──────────────────────────────────────────────────────────────┐
                         │                                                              │
ŻYWIEC (sPZ)             │                  GŁÓWNA OŚ PRODUKCJI                          │
kat. 65882               │                                                              │
~500-600 t/dzień         │                                                              │
       │                 ▼                                                              │
       │         ┌───────────────┐  sMM-  ┌───────────────┐  sRWP  ┌───────────────┐    │
       │  sRWU   │   M. UBOJ     │ ──────►│   M. PROD     │ ──────►│  Krojenie     │    │
       └────────►│   65555       │        │   65554       │        │  (linie cięcia)│   │
                 │               │        │               │        └───────────────┘    │
                 │ Tuszki A/B    │        │ Tuszki, podro │             │              │
                 │ Podroby (3)   │        │ → cięte na    │             ▼              │
                 │ Odpady        │        │   filet/skrzy │      ┌───────────────┐     │
                 │ ~85% z żywca  │        │   /korpus...  │      │  sPWP wyjście │     │
                 └───────────────┘        └───────────────┘ ◄────│  → M. PROD    │     │
                                                  │              └───────────────┘     │
                                                  │ sMM-                                │
                                  ┌───────────────┼──────────┬──────────┐               │
                                  ▼               ▼          ▼          ▼               │
                          ┌───────────┐   ┌──────────┐  ┌─────────┐  ┌──────────┐       │
                          │ M. DYST   │   │ M. MROŹ  │  │ KARMA   │  │ M. ODPA  │       │
                          │ 65556     │   │ 65552    │  │ 65547   │  │ 65551    │       │
                          └───────────┘   └──────────┘  └─────────┘  └──────────┘       │
                                  │                                                     │
                                  │ sWZ                                                 │
                                  ▼                                                     │
                          ┌───────────────┐                                             │
                          │   KLIENCI     │                                             │
                          │ (B2B + retail)│                                             │
                          └───────────────┘                                             │
                                                                                        │
                                                                                        │
   MASARNIA (osobna ścieżka):                                                           │
   M. PROD ──sMM-──► M. MASAR (65562) ──sPPM────► (wędliny) ────sWZ────► klienci        │
                                                                                        │
   OPAKOWANIA (osobna ścieżka):                                                         │
   sMP/sPZ ──► Mag. opak. (65559) ──sMW──► Produkcja (zużycie wewn.)                    │
                                                                                        │
   └──────────────────────────────────────────────────────────────────────────────────┘
```

### 2.1. Etapy łańcucha — szczegółowo

#### Etap 1: ŻYWIEC (wejście)
- **Seria**: `sPZ`, kat. towaru = `65882`
- **Magazyn**: 65550 (Mag. faktur — duża ilość) lub 65556 (M.DYST — bezpośrednio)
- **Kontrahenci**: hodowcy (FK `MG.khid` → `STContractors`)
- **Towary**: `Kurczak żywy 7-12` (klasy wagowe)
- **Wolumen**: ~200 t/dzień (z dostaw od hodowców)

#### Etap 2: UBÓJ
- **Wejście**: `sRWU` (rozchód do uboju z 65550 lub 65556) — ABSi
- **Wyjście**: `sPWU` na magazyn **65555 (M.UBOJ)**
- **Towary wyjścia** (po `MZ.kod`):
  - `Kurczak A` — duża tuszka (kat. 67095)
  - `Kurczak B` — mała tuszka (kat. 67095)
  - `Wątroba`, `Żołądki`, `Serce` — podroby (kat. 67095)
  - Odpady (kat. 67094) — zwykle minimalne kwoty
- **Wydajność**: `sPWU.kg / sPZ.kg × 100%` ≈ **80-85%** (z podrobami i odpadami)
  - Strata 15-20% = pióra / krew / woda / kości głębokie
- **Norma**: 80% z podrobami, 30% bez podrobów (tylko tuszki)

#### Etap 3: TRANSFER UBOJ → PRODUKCJA
- **Seria**: `sMM-` z magazynu **65555** do **65554**
- `MG.khdzial = 65554` (cel docelowy)
- Para: `sMM+` na 65554 z `khdzial = 65555`

#### Etap 4: PRODUKCJA (Krojenie)
- **Wejście**: `sRWP` na 65554 (rozchód do krojenia, kat. 67095) — głównie Tuszka B
- **Wyjście**: `sPWP` na 65554 — elementy:
  - `Filet z piersi`
  - `Skrzydło`
  - `Korpus`
  - `Udo`
  - `Podudzie`
  - inne (kat. 67095/67104)
- **Wydajność**: `sPWP.kg / sRWP.kg × 100%` ≈ **55-65%** (krojenie ma straty)
  - Reszta = kości, ścinki, ubytki

#### Etap 5: ROZDZIAŁ z PRODUKCJI
**4 kierunki przez sMM-** z magazynu 65554:
1. **Do dystrybucji** (M. DYST 65556) — większość, ~85-95% produkcji
2. **Do mroźni** (M. MROŹ 65552) — nadprodukcja, ~3-15%
3. **Do karmy** (KARMA 65547) — odpady jadalne dla zwierząt
4. **Do odpadów** (M. ODPA 65551) — niejadalne

Każdy z tych transferów ma parę `sMM+` w magazynie docelowym.

#### Etap 6: SPRZEDAŻ
- **Seria**: `sWZ` z magazynu 65556 (M. DYST)
- **Kontrahenci**: klienci B2B (Biedronka, Lidl, hurtownie) + retail
- `MG.khid` → klient w `STContractors`

---

## 3. Wydajności — formuły i normy

### 3.1. Wydajność uboju
```sql
WydajnoscUbojuProc = SUM(sPWU.ilosc) / SUM(sPZ.ilosc) × 100%
```
Lub z `sRWU`:
```sql
WydajnoscUbojuProc = SUM(sPWU.ilosc) / SUM(sRWU.ilosc) × 100%
```
**Norma**: 80-85% z podrobami i odpadami. Bez podrobów (tylko tuszki) ~30%.

**Ślad w bazie**: `BilansMaterialowy.WydajnoscUbojuProc` (klasa w `Models/BilansMaterialowyModels.cs`).

### 3.2. Wydajność krojenia
```sql
WydajnoscKrojeniaProc = SUM(sPWP.ilosc) / SUM(sRWP.ilosc) × 100%
```
**Norma**: 55-65%. Reszta to kości, ścinki, ubytki.

### 3.3. Strata uboju
```sql
StratyUbojuKg = SUM(sPZ.ilosc) - SUM(sPWU.ilosc)
StratyUbojuProc = StratyUbojuKg / SUM(sPZ.ilosc) × 100%
```
**Norma**: 15-25%. > 25% to alert (audyt linii uboju).

### 3.4. Per-klasa wagowa (`In0E.QntInCont` w LibraNet)

W LibraNet (192.168.0.109), tabela `In0E` trzyma ważenia per paleta.

| QntInCont | Klasa | Średnia waga sztuki | Charakter |
|---:|---|---|---|
| 1-3 | (anomalie) | < 1 kg | Odrzucamy |
| 4-7 | **Duży kurczak** | ~14-16 kg/szt, 36 szt/paleta | Główny segment |
| 8-12 | **Mały kurczak** | ~5-7 kg/szt | Drugi segment |
| > 12 | (anomalie) | — | Odrzucamy |

`ActWeight` (waga palety) — **realny zakres 500-600 kg**. Poza tym to anulacje/błędy.

---

## 4. Przepływy MM- (typowe wzorce)

Z analizy danych Pióroskovskich:

### 4.1. Główne kierunki (top przepływów)

```
M. UBOJ (65555)  ──sMM-──►  M. DYST (65556)   [tuszki gotowe do sprzedaży]
M. PROD (65554)  ──sMM-──►  M. DYST (65556)   [elementy po krojeniu]
M. PROD (65554)  ──sMM-──►  M. MROŹ (65552)   [nadprodukcja do zamrożenia]
M. PROD (65554)  ──sMM-──►  KARMA (65547)     [niejadalne dla zwierząt]
M. PROD (65554)  ──sMM-──►  M. ODPA (65551)   [pełne odpady]
M. PROD (65554)  ──sMM-──►  M. MASAR (65562)  [surowiec do wędlin]
M. UBOJ (65555)  ──sMM-──►  M. ROZCH (65564)  [pomocniczy buffer]
```

### 4.2. Backflow (rzadziej)

```
M. MROŹ (65552)  ──sMM-──►  M. DYST (65556)   [rozmrożone do sprzedaży]
M. MROŹ (65552)  ──sMM-──►  M. PROD (65554)   [back do dalszej obróbki]
M. ODPA (65551)  ──sMM-──►  M. PROD (65554)   [reprocessing]
```

### 4.3. Anomalie / błędy

- `magazyn = khdzial` (źródło = cel) — błąd księgowy, sprawdź ręcznie
- Brak pary `sMM+` dla danego `sMM-` — problem integralności
- Zmiana magazynu w środku doby — możliwe ale wymaga potwierdzenia

---

## 5. Implementacja w kodzie ZPSP

### 5.1. `MagazynyHelper.cs` — słownik magazynów
Plik: `AnalitykaPelna/Services/MagazynyHelper.cs`

**Trzy poziomy nazwa magazynów (priorytet od najwyższego)**:
1. **`appsettings.json` → sekcja `MagazynyNazwy`** (manual override)
2. **DB refresh** przez `LoadFromDatabaseAsync(connHandel)` — parsuje sufiksy MM+/MM-
3. **Hardcoded defaults** (sekcja `_defaults` w klasie)

```json
// appsettings.json — manual override jeśli chcesz inną nazwę
{
  "MagazynyNazwy": {
    "65554": { "Skrot": "🥩 Krojenie", "Pelna": "Magazyn produkcji - krojenie tuszek", "KolorHex": "#059669" }
  }
}
```

### 5.2. `SeriaSymfoniaHelper` — klasyfikacja IN/OUT
Plik: `AnalitykaPelna/Models/StanMagazynuModels.cs`

```csharp
SeriaSymfoniaHelper.JestPrzychodem("sPWU")  // → true
SeriaSymfoniaHelper.JestRozchodem("sWZ")    // → true
SeriaSymfoniaHelper.Opis("sMM-")            // → "Przesunięcie międzymagazynowe (rozchód)"
```

### 5.3. `WydajnoscService.LoadFlowChainAsync()` — agregaty łańcucha
Plik: `AnalitykaPelna/Services/WydajnoscService.cs`

UNION ALL 8 zapytań (per etap: Żywiec/Uboj/Prod/Dyst/Klienci + Mroźnia/Karma/Odpady) → 1 wynik z `kg` i `liczbą dokumentów` per etap. Następnie wyliczanie wydajności w `FlowChainSummary`.

### 5.4. `WydajnoscService.LoadTowaryProdukcjiAsync()` — towary z produkcji
Filtr `INNER JOIN ProdukcyjnePozycje` (DISTINCT z PRODUKCJA = sPWU/sPWP/sPPM/sPPK) → tylko towary które kiedykolwiek wyszły z produkcji. Nie wszystkie z magazynu.

Per towar:
- `WyprodukowanoKg` (PWU+PWP+PPM+PPK)
- `ZuzytoKg` (RWP+RWU+RPM+RPK = wsad do dalszej produkcji)
- `SprzedanoKg` (WZ+WZ-W+WZK)
- `Saldo = Wyprodukowano - Zużyto - Sprzedano`
- `NumeryDokumentów` przez STRING_AGG

---

## 6. Skróty branżowe / mentalne

| Skrót w kodzie | Po polsku | Co znaczy |
|---|---|---|
| **A** (Kurczak A) | Tuszka A (klasa 1) | Bez wad, pełna marża |
| **B** (Kurczak B) | Tuszka B (klasa 2) | Z wadami (pęknięcia, sińce), niższa cena |
| **Tuszka** | — | Cały kurczak po uboju, bez wnętrzności |
| **Podroby** | — | Wątroba + żołądki + serce (czasem szyjki) |
| **Element** | — | Filet/korpus/skrzydło/udo — po krojeniu |
| **Tryb** | Trybowanie | Krojenie tuszki na elementy |
| **MM-** | Mag → Mag rozchód | Wyjście z magazynu źródłowego |
| **MM+** | Mag → Mag przychód | Wejście do magazynu docelowego |
| **PWU** | Przychód Wew. Ubojnia | Tuszki / podroby z linii uboju |
| **PWP** | Przychód Wew. Produkcja | Elementy z linii krojenia |
| **PPK** | Przychód Produkcja Karmy | Karma dla zwierząt z odpadów |

---

## 7. Edge cases / wyjątki

### 7.1. Tuszka B → krojenie głębokie
Tuszka B (z wadami) zwykle idzie do krojenia. Bardzo rzadko do sprzedaży — głównie surowiec do filetu / korpusu.

### 7.2. Mroźnia jako buffer
M. MROŹ (65552) używana **w obie strony** — towar może wrócić z mroźni do produkcji (`sMM-` z 65552 do 65554) jeśli zamówienie awaryjne.

### 7.3. Korekty (sPZK, sWZK)
Wpływają na sumy! W bilansach **dodawaj je z odpowiednim znakiem** lub eksportuj osobno. Aktualnie kod traktuje je jako część przychodu/rozchodu.

### 7.4. Anulacje
`MG.anulowany = 1` — **nie wliczaj** w sumy (zawsze filter `= 0`).

### 7.5. Klasy wagowe LibraNet vs HANDEL
LibraNet rozróżnia klasy 1-12 (per `QntInCont`). HANDEL widzi tylko `Kurczak A` / `Kurczak B`. Mapowanie: A = klasy 4-7, B = klasy 8-12 (ale to przybliżenie, granice klasy są firmowe).

---

**Aktualizacja**: 2026-05-09 — dokument utworzony po refactorze "Stan magazynów".
**Powiązane**: 23_HANDEL_Schema_Sage_Symfonia.md, 25_Analityka_Pelna_v2_StanMagazynow.md, 22_Analityka_Pelna_modul.md.
