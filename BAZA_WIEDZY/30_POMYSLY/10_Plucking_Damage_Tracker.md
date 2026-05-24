# 10. ⭐ Plucking Damage Tracker — workflow

## Twoje wyjaśnienie problemu
> "u mnie ludzie wadliwy towar dopiero na produkcji czystej to robią — czyli kwalifikują na klase B i klase A. Chodzi o to ze nie wiem jak sprawić aby to było fajnie monitorowane."

To znaczy, że już teraz **macie klasyfikację A/B** na **produkcji czystej** (krojenie). Problem: nie wiecie **kto/co/kiedy zrobiło tuszkę klasy B**, więc nie umiecie tego naprawić.

## Cel
Połączyć klasyfikację A/B z konkretnym **momentem powstania wady** (skubarka? linia? hodowca?) żeby:
1. Naprawiać przyczynę (a nie tylko liczyć skutki)
2. Mieć dowody w reklamacjach z hodowcami
3. Premiować/karać brygady operatorów

---

## Wartość biznesowa
- **Każdy %% przesunięcia z klasy B → A = ~3-5 zł/kg różnicy**
- Przy 200 t/dzień i 5% klasy B (norma branżowa) = 10 t × 4 zł = **40k PLN/dzień straty**
- Redukcja klasy B z 5% → 3% = **16k PLN/dzień = ~4 mln PLN/rok**
- Konkretnie dla Was (~258M obrotu) = **realne 1-2 mln PLN/rok** (zakładam wolniejszą adopcję)

---

## ARCHITEKTURA WORKFLOW

### Schemat ideowy
```
[Skubarka 1, 2, 3, 4]──>[Patroszenie]──>[Chłodzenie]──>[KROJENIE]
                                                          │
                                                          ▼
                                          [Operator klasyfikuje A/B]
                                                          │
                                                          ▼
                                            [Tablet — wpis przyczyny]
                                                          │
                                                          ▼
                                           [DB ClassificationLog]
                                                          │
                              ┌───────────────────────────┼───────────────────────────┐
                              ▼                           ▼                           ▼
                  [Dashboard godzin]      [Raport per skubarka]      [Raport per hodowca]
```

### Trzy poziomy danych

**Poziom 1: Klasyfikacja ilościowa (już masz)**
- Wpisujesz: "Z partii 1247: 1200 szt klasy A, 65 szt klasy B"
- Pokazuje: **ile**

**Poziom 2: Typ wady per tuszka klasy B** ⭐ NOWE
- Operator klasyfikuje wadę z dropdownu **12 typów** (PDF rozdz. 7):
  - Hematoma pierś
  - Hematoma udo
  - Hematoma podudzie
  - Pop-out skrzydło
  - Pop-out udo
  - Złamanie kości
  - Cellulitis
  - Ascites (puchlinianie)
  - White striping
  - Wooden breast
  - Spaghetti meat
  - Inne (z polem tekstowym)
- Pokazuje: **co** poszło źle

**Poziom 3: Przyczyna powstania wady** ⭐ KLUCZOWE
- Algorytm sugeruje (na podstawie typu wady):
  - Hematoma świeża (czerwona) → **proces uboju** (5-15 min temu)
  - Hematoma ciemna → **łapanie/transport** (6-12h temu)
  - Hematoma zielona → **hodowca** (24-72h temu)
  - Pop-out → **skubarka** (15-30 min temu)
  - WB/spaghetti → **hodowca/żywienie** (cały cykl)
- Operator akceptuje lub zmienia
- Pokazuje: **dlaczego**

---

## DATABASE SCHEMA

```sql
-- LibraNet
CREATE TABLE ClassificationLog (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    ClassificationDateTime DATETIME NOT NULL,
    PartiaId INT NOT NULL,
    OperatorId NVARCHAR(50) NULL,
    StanowiskoKrojenia NVARCHAR(20) NULL,  -- 'STAN_1', 'STAN_2'...
    KlasaWady NVARCHAR(2) NOT NULL,  -- 'A' lub 'B'
    TypWady NVARCHAR(50) NULL,  -- z 12 typów wyzej
    KategoriaPrzyczyny NVARCHAR(30) NULL,  -- 'PROCES_UBOJU', 'LAPANIE', 'HODOWCA', 'SKUBARKA', 'INNE'
    SkubarkaNr INT NULL,  -- jeśli przyczyna = SKUBARKA
    NotatkiOperatora NVARCHAR(500) NULL,
    ZdjeciePath NVARCHAR(300) NULL,  -- opcjonalnie zdjęcie wady
    WagaSztuki DECIMAL(6,2) NULL
);

CREATE INDEX IX_ClassLog_DateTime ON ClassificationLog(ClassificationDateTime);
CREATE INDEX IX_ClassLog_Partia ON ClassificationLog(PartiaId);
CREATE INDEX IX_ClassLog_Wada ON ClassificationLog(KlasaWady, TypWady);

-- Tabela słownikowa wad
CREATE TABLE WadaTypy (
    Kod NVARCHAR(20) PRIMARY KEY,
    Nazwa NVARCHAR(100) NOT NULL,
    DomyslaPrzyczyna NVARCHAR(30) NULL,
    OpisDlaOperatora NVARCHAR(500) NULL,
    IkonkaPath NVARCHAR(100) NULL
);

INSERT INTO WadaTypy VALUES
('HEMAT_PIERS', 'Hematoma w piersi', 'PROCES_UBOJU', 'Niebieskie/czerwone plamy pod skórą piersi', '/icons/hemat_piers.png'),
('HEMAT_UDO', 'Hematoma w udzie', 'LAPANIE', 'Sniaki w mięsie uda', '/icons/hemat_udo.png'),
('HEMAT_PODUDZIE', 'Hematoma w podudziu', 'LAPANIE', 'Najczęściej z lapania za nogi', '/icons/hemat_podudzie.png'),
('POPOUT_SKRZYDLO', 'Pop-out skrzydła', 'SKUBARKA', 'Wystajaca kosc, czesto skubarka', '/icons/popout.png'),
('POPOUT_UDO', 'Pop-out uda', 'SKUBARKA', 'Wystajaca kosc, czesto skubarka', '/icons/popout.png'),
('ZLAMANIE', 'Złamanie kości', 'TRANSPORT', 'Zlamana piszczel/kosc', '/icons/zlamanie.png'),
('CELLULITIS', 'Cellulitis (zapalenie)', 'HODOWCA', 'Zapalenie tkanki podskornej', '/icons/cellulitis.png'),
('ASCITES', 'Ascites (puchlinianie)', 'HODOWCA', 'Plyn w jamie brzusznej', '/icons/ascites.png'),
('WHITE_STRIPING', 'White striping', 'HODOWCA', 'Biale pasy na filecie', '/icons/wstriping.png'),
('WOODEN_BREAST', 'Wooden breast', 'HODOWCA', 'Twardy filet', '/icons/woody.png'),
('SPAGHETTI', 'Spaghetti meat', 'HODOWCA', 'Wlokniste mieso', '/icons/spaghetti.png'),
('INNE', 'Inne wady', NULL, 'Opisz w polu uwag', '/icons/inne.png');
```

---

## INTERFEJS — Tablet operatora

### Hardware
- **Tablet**: Samsung Galaxy Tab Active3 (8") ~2000 zł, lub Lenovo M8 (~600 zł na początek)
- **2-4 tablety** = 1 na stanowisko krojenia, lub 1 na 2 stanowiska
- **Stojak ścienny** lub przemysłowy uchwyt z VESA (~150 zł/szt)
- **Klawiatura nie potrzebna** — tylko dotyk

### Aplikacja (UWP / Xamarin / .NET MAUI lub WebApp w Blazor)
**Najszybsze rozwiązanie**: **Blazor Server** (ASP.NET Core) — działa w przeglądarce tabletu, full reuse C# z ZPSP.

### Workflow operatora
```
┌─────────────────────────────────────────┐
│  Stanowisko: 2     Operator: Janek      │
│  Aktualna partia: 1247 Kowalski         │
├─────────────────────────────────────────┤
│                                         │
│  Tuszka klasy:                          │
│  ┌─────────┐    ┌─────────┐             │
│  │   A     │    │    B    │  ← duzy    │
│  │  +1     │    │  WYBIERZ │   button   │
│  └─────────┘    └─────────┘             │
│                                         │
└─────────────────────────────────────────┘

Po wybraniu B:
┌─────────────────────────────────────────┐
│  Wybierz typ wady:                      │
│                                         │
│  [hemat. pierś] [hemat. udo] [pop-out]  │
│  [złamanie]    [cellulitis] [ascites]   │
│  [WB]          [striping]   [inne]      │
│                                         │
└─────────────────────────────────────────┘

Po wybraniu wady — sugestia przyczyny:
┌─────────────────────────────────────────┐
│  Sugerowana przyczyna: HODOWCA          │
│                                         │
│  Czy zgadza się?  [TAK] [ZMIEŃ]         │
│                                         │
│  [+ Dodaj notatkę]  [📷 Zdjęcie]        │
│                                         │
│  [ZAPISZ]                               │
└─────────────────────────────────────────┘
```

### Czas wpisu = **2-3 sekundy** dla klasy A (tylko klik), **5-8 sekund** dla klasy B (typ wady + akceptacja)

---

## DASHBOARDS

### Dashboard 1: Live na zmianę
```
┌────────────────────────────────────────────────────────────┐
│  PRODUKCJA CZYSTA — DZIŚ 14:23                             │
├────────────────────────────────────────────────────────────┤
│  Stanowisko 1 (Marek): 247 szt A | 18 szt B (6.7%)        │
│  Stanowisko 2 (Janek): 232 szt A | 12 szt B (4.9%)        │
│  Stanowisko 3 (Beata): 198 szt A | 24 szt B (10.8%) ⚠     │
│  Stanowisko 4 (Tomek): 245 szt A | 14 szt B (5.4%)        │
│                                                            │
│  ŚREDNIA: 6.9% — NORMA: <5% — ⚠ ZA WYSOKO                 │
│                                                            │
│  TOP 3 WADY DZIŚ:                                          │
│  1. Hematoma pierś (24 szt) — proces uboju                │
│  2. Pop-out skrzydło (18 szt) — skubarka #2 (sprawdź!)    │
│  3. Wooden breast (12 szt) — hodowca Kowalski             │
└────────────────────────────────────────────────────────────┘
```

### Dashboard 2: Per hodowca (raport miesięczny)
```
HODOWCA: Kowalski (12 partii w maju)
─────────────────────────────────────────
Razem ubitych: 18 400 szt
Klasy B: 1 250 szt (6.8%) — NORMA 5%

Breakdown klasy B per typ:
- Wooden breast:    340 szt (1.8%) ⚠ HIGH
- White striping:   180 szt (1.0%)
- Cellulitis:       95 szt  (0.5%)
- Hematomy stare:   140 szt (0.8%) ← Pytanie do hodowcy
- Hematomy świeże:  220 szt (1.2%) ← Twoja wina (uboj)
- Pop-outy:         180 szt (1.0%) ← Twoja wina (skubarka)
- Inne:             95 szt  (0.5%)

KOSZT KOWALSKIEGO DLA NAS: 1250 szt × 4 zł = 5000 zł
Z czego "wina" hodowcy: ~755 szt × 4 zł = 3020 zł

DECYZJA: rozmowa o redukcji obsady (WB), 
         dyskontować cenę zakupu o 8 gr/kg dla nastepnej partii
```

### Dashboard 3: Per skubarka (problem operacyjny)
```
SKUBARKA #2 — ostatnie 30 dni
────────────────────────────────
Pop-outy ogółem: 850 szt
Skubarka #1: 320 szt
Skubarka #2: 540 szt ⚠ DUZO WIECEJ
Skubarka #3: 290 szt  (wymieniona na nową w lutym)
Skubarka #4: 410 szt

INTERWENCJA: konserwacja skubarki #2 zaplanowana 18.05
            (regulacja przyciskow, wymiana palcow)
```

---

## ALGORYTM SUGESTII PRZYCZYNY (smart)

```csharp
public string SuggestCause(string wadaTyp, DateTime klasyfikacja, int partiaId, int? skubarkaNr)
{
    // pobierz info o partii
    var partia = LoadPartia(partiaId);
    var godzinyOdUboju = (klasyfikacja - partia.UbojDateTime).TotalHours;

    return wadaTyp switch
    {
        "HEMAT_PIERS" when godzinyOdUboju < 4 => "PROCES_UBOJU",  // świeża
        "HEMAT_PIERS" when godzinyOdUboju < 12 => "LAPANIE",
        "HEMAT_PIERS" => "HODOWCA",  // stara, zielona/żółta
        
        "HEMAT_UDO" or "HEMAT_PODUDZIE" => "LAPANIE",  // niemal zawsze
        
        "POPOUT_SKRZYDLO" or "POPOUT_UDO" => "SKUBARKA",
        
        "WHITE_STRIPING" or "WOODEN_BREAST" or "SPAGHETTI" 
            => "HODOWCA",  // genetyka + żywienie
        
        "CELLULITIS" or "ASCITES" => "HODOWCA",  // choroby farmy
        
        "ZLAMANIE" => "TRANSPORT",  // typowo z crates
        
        _ => "INNE"
    };
}
```

---

## INTEGRACJA Z #12 (Forensic Hematoma) i #28 (Photo AI)

**Tryb manualny** (faza 1): operator wybiera typ wady ręcznie.
**Tryb AI-assisted** (faza 2): operator robi zdjęcie tabletem → Claude VLM klasyfikuje **typ wady + sugeruje przyczynę** na podstawie koloru hematomy.

Workflow staje się:
1. Klik klasy B
2. Klik 📷 zdjęcie
3. AI w 3 sek: "Hematoma fioletowa, ~12h, propozycja przyczyny: LAPANIE"
4. Operator akceptuje lub zmienia
5. ZAPISZ

---

## CZAS IMPLEMENTACJI

| Etap | Czas |
|---|---|
| Hardware (2-4 tablety + stojaki + WiFi access point) | 1 dzień + ~5000 zł |
| Backend: tabele + API REST/Blazor | 16-20h |
| UI: Blazor app | 24-32h |
| Sugestia przyczyny + integracja z #12 (foto) | 8-12h |
| Dashboardy: 3 widoki (live + hodowca + skubarka) | 16-20h |
| **RAZEM** | **~80h kodu + 5 tys zł sprzętu** |

---

## QUICK START (MVP w 1 tydzień)

Jeśli chcesz **MVP** żeby zacząć zbierać dane szybciej:

1. **Dzień 1**: Dodaj tabelę `ClassificationLog` do LibraNet
2. **Dzień 2-3**: Prosta strona Blazor z 2 przyciskami: "A +1" i "B +1 → typ wady"
3. **Dzień 4**: Deploy na lokalny serwer + 1 tablet na stanowisku #1
4. **Dzień 5**: Pilot 1 tygodnia — operatorzy używają
5. **Po tygodniu**: Dashboard #1 (live)
6. **Po miesiącu**: Dashboard #2 (hodowca) + Dashboard #3 (skubarka)
7. **Po 3 mies.**: Integracja z #12 (foto + AI)

**Wartość już po miesiącu**: konkretne raporty hodowcom z liczbami, których nie mieli wcześniej.
