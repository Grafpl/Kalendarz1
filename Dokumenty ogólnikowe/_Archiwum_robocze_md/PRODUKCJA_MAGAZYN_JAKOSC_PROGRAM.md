# PRODUKCJA + MAGAZYN + JAKOŚĆ — plan programistyczny ZPSP

> **Cel:** szczegółowy plan **technicznego programowania** dla 3 kluczowych działów Ubojni Drobiu Piórkowscy. Towarzyszący dokument dla `FIRMA_PEŁEN_OBRAZ.md`.
>
> **Co zawiera:**
> - Mapy procesów (BPMN-style)
> - Konkretne ekrany/tablety per stanowisko z mockup-ami
> - Listy KPI per stanowisko
> - Plan integracji WAGO + RADWAG
> - Tabele bazy danych do dodania
> - Procedury cyfrowe (zastępujące papierowe)
> - Roadmap 3/6/12 miesięcy
> - Szacowane koszty sprzętu i programowania
>
> **Stan na:** 02.05.2026.

---

## SPIS TREŚCI

1. [TL;DR — strategia 3 działów](#1-tldr)
2. [Mapy procesów — przepływ towaru](#2-mapy-procesów)
3. [PRODUKCJA — szczegółowy plan](#3-produkcja-plan)
4. [MAGAZYN — szczegółowy plan](#4-magazyn-plan)
5. [MROŹNIA — szczegółowy plan](#5-mroźnia-plan)
6. [JAKOŚĆ — szczegółowy plan](#6-jakość-plan)
7. [Integracja WAGO + RADWAG — krok po kroku](#7-integracje)
8. [Tabele bazy danych do dodania](#8-tabele)
9. [Procedury cyfrowe — co zastąpić papier](#9-procedury-cyfrowe)
10. [Roadmap 3/6/12 miesięcy](#10-roadmap)
11. [Szacowane koszty sprzętu + programowania](#11-koszty)

---

## 1. TL;DR

**Strategia 3 działów** w 5 zdaniach:

1. **PRODUKCJA** — Cockpit Justyny + tablety na stanowiskach + integracja WAGO/RADWAG = real-time pomiar wydajności hodowcy, klasy A/B, tempa linii. Kluczowe: dostęp API od dostawców WAGO + RADWAG.

2. **MAGAZYN** — Panel magazyniera 2.0 (tablet wodoszczelny przy rampie) + lista FIFO + checkbox kompletacji + auto-WZ + plomba. Brak skanowania do 2027 (RFID pod dotację), ale workflow uporządkowany.

3. **MROŹNIA** — Mapa 3D komór + alert wieku partii + automat FIFO + integracja z modułem Krojenie (decyzja 13:00). Inwentaryzacja tygodniowa cyfrowa.

4. **JAKOŚĆ** — Cockpit Justyny (4K monitor w biurze) + plan szkoleniowy cyfrowy dla Klaudii/Gabrieli + filtr auto-import reklamacji + cyfryzacja HACCP/QC.

5. **Roadmap:** 3 mies = quick-wins (filtr reklamacji, marża per produkt, alert klasy A), 6 mies = Cockpity i Hala LIVE, 12 mies = pełna integracja WAGO/RADWAG + RFID.

---

## 2. MAPY PROCESÓW

### 2.1 Pełen cykl: od pisklęcia do faktury (40-45 dni)

```
Dzień 0 (35-40 dni przed ubojem):
═══════════════════════════════════════════════
JDA Jeżów (wylęgarnia) → Pisklęta (39 000 szt)
        ↓
Sergiusz / dział zakupów wpisuje WSTAWIENIE do ZPSP:
  Tabela: WstawieniaKurczakow
  Kolumny: Hodowca, DataWstawienia, IloscWstawienia, PaszaPisklak, TypUmowy
        ↓
Stróżewski (lub inny hodowca kontraktowy) tuczy 35-42 dni
        ↓ (ZPSP monitoruje cykl, alert "dziś hodowca X kończy")

Dzień Uboju:
═══════════════════════════════════════════════
Łapacze (56 osób) odbierają z fermy → AVILOG transport
        ↓
3:00 — auta przyjeżdżają do Piórkowskich
        ↓
Brama / waga samochodowa:
  Tabela: SpecyfikacjeSurowca (nowa)
  Wpis: NumerIRZ, hodowca, sztuki, waga żywa
        ↓
Klatki na placu (~30 min)
        ↓
3:30 — Łukasz Collins startuje pierwszego kurczaka

LINIA UBOJU (PRODUKCJA BRUDNA, ~3:30-13:30):
═══════════════════════════════════════════════
Zawieszanie żywego (4-6 osób)
        ↓
Padłe → osobny kontener (utylizacja)
   Wpis ZPSP: SpecyfikacjeSurowca.Padłe (sztuki)
        ↓
Ogłuszanie → wykrwawianie
        ↓
Skubarka (parzelnia, automat + 2 osoby nadzór)
        ↓
Patroszarka MEYN MOUNTAINEER 2015
        ↓
Konfiskaty (lekarz powiatowy)
   Wpis ZPSP: SpecyfikacjeSurowca.CH/ZM/NW (kategorie)
        ↓
Chiller tunelowy (-2 do +4°C, 60-90 min)

PRODUKCJA CZYSTA (~5:30-13:30):
═══════════════════════════════════════════════
Wybijanie tuszki na wannę (4-6 osób)
        ↓
Zawieszanie na linii wagowej (waga RADWAG)
        ↓
Program WAGO decyduje klasę wagową:
  rozmiar 6 / 7 / 8 / 9 / 10 / 11
  (=liczba sztuk w pojemniku 15kg)
        ↓
KLASYFIKACJA WZROKOWA A/B (1-2s/sztukę):
  Pracownik patrzy na tuszkę
  Jeśli wada (krwiak, złamanie, żółć, oparzenie)
    → przesuwa DŹWIGNIĘ W GÓRĘ lub naciska GUZIK
    → program WAGO wie że nie wybija na klasę A
    → idzie do końca linii
    → 2 osoby przewieszają na linię ROZBIERALNI
        ↓
TUSZKA A:                    TUSZKA B (do rozbioru):
  Pakowanie 15kg pojemnik       Maszyna rozdzielająca:
        ↓                          → Korpus (do pojemnika)
  Waga paletowa RADWAG             → Filet (czyszczenie ręczne
        ↓                              z balonów i krwi)
  Etykieta GS1-128                 → Ćwiartka I + II
  (terminal przy wadze)            → Skrzydło I + II
        ↓                          → Pałka / Noga
  Magazyn 65554                    → Mielone / Polędwiczki / Tuba
                                       (kto decyduje?)
                                   → Pozostałe → Karma-Max
                                            ↓
                                  Pakowanie elementów
                                            ↓
                                  Wagi platformowe RADWAG
                                            ↓
                                  Etykiety
                                            ↓
                                  Magazyn 65554

13:00 SPOTKANIE OPERACYJNE:
═══════════════════════════════════════════════
Justyna + Sergiusz + handlowczynie (Jola, Maja, Teresa, Ania)
Bilans dnia (z modułu ZPSP "Krojenie"):
  Scenariusz 1: sprzedaj tuszkę as-is
  Scenariusz 2: krojenie świeże (-0,45 zł/kg)
  Scenariusz 3: mrożenie (-2,32 zł/kg STRATA)
        ↓
Decyzja: krojenie / mrożenie / sprzedaż
        ↓
Broadcast Teams: rozbiór + magazyn + fakturzystki + Pani Jola

KLIENT ZAMAWIA (do 10:00 na DZIŚ, do 14:00 na JUTRO):
═══════════════════════════════════════════════
Damak telefonuje: "4 tony fileta klasa A, jutro"
        ↓
Pani Jola (jej klient) wpisuje do ZPSP:
  Tabela: ZamowieniaMieso + ZamowieniaMiesoTowar
  Status: Nowe → Potwierdzone
        ↓
ZPSP sprawdza limit kredytowy (Hermes)
        ↓
Magazynier widzi w panelu (jutro): "Damak 4t fileta A"

ZAŁADUNEK (rampa 65556):
═══════════════════════════════════════════════
Klient awizuje godzinę (slot booking)
        ↓
Kierowca Drożdżyk wjeżdża na rampę
        ↓
Magazynier (Robert Stępniak / Robert Osiński):
  Otwiera panel magazyniera w ZPSP
  Lista FIFO: najstarsze najpierw
  Kompletuje paletę po palecie
  Klika "skompletowane"
        ↓
Generuje WZ → drukuje
Plombuje samochód
Kierowca podpisuje (papierowo dziś, ekran w przyszłości)
        ↓
Status zamówienia: "W magazynie" → "Załadowane"

FAKTURA:
═══════════════════════════════════════════════
Fakturzystka (Renata / Małgorzata Stępniak):
  Czeka aż magazyn potwierdzi załadunek w ZPSP
  Sprawdza ilość = ZAFAKTUROWANA
  Jeśli zgodne → wystawia FVS w Symfonii
  Jeśli rozbieżność → telefon do handlowczyni
        ↓
Status: "Faktura"
        ↓
Symfonia generuje XML KSeF (FA(3))
        ↓
KSeF Plus wysyła UPO

REKLAMACJA (gdyby się pojawiła za 5 dni):
═══════════════════════════════════════════════
Damak telefonuje: "Filet czerwony"
        ↓
Pani Jola → odbiera → wpisuje reklamację w ZPSP
  Tabela: Reklamacje
  Klasyfikacja: jakościowa
        ↓
Justyna otrzymuje (Teams):
  Numer reklamacji
  Klient: Damak
  Faktura: FVS 3157/26
  Klasa towaru: Filet A
        ↓
ZPSP automat: traceability
  → Która partia? (NumerPartii z etykiety)
  → Który hodowca? (Stróżewski)
  → Kiedy ubita? (35 dni temu)
        ↓
Justyna: konsultacja z Sergiuszem
  Decyzja: pełne / częściowe / odrzucenie
        ↓
Odpowiedź klientowi do 15:00
        ↓
Zamknięcie 48h
CAPA: co zmieniamy, kto, do kiedy
        ↓
ZPSP automat: ranking hodowcy Stróżewski się obniża
Alert do działu zakupów: "Stróżewski: 3 reklamacje w mies. — rozmowa"
```

### 2.2 Strumienie odpadów
```
PRODUKCJA → ODPADY:
├── Padłe w transporcie       → KATEGORIA 1 → utylizacja
├── Konfiskaty z linii (CH/ZM/NW) → KATEGORIA 1/2 → utylizacja
├── Skrawki, skórki, kości    → KATEGORIA 3 → KARMA-MAX
└── Korpus (po fil.)          → produkt do sprzedaży
```

---

## 3. PRODUKCJA — SZCZEGÓŁOWY PLAN

### 3.1 Stanowiska + tablety (mockupy)

#### **Stanowisko 1: Łukasz Collins (Kierownik Uboju Brudnej)**
**Sprzęt:** Tablet 10" (Panasonic Toughbook FZ-S1) w biurze hali brudnej.

**Ekran "Start dnia"** (3:30 rano):
```
┌─────────────────────────────────────────────────┐
│ 🌅 START DNIA — Łukasz Collins | 02.05.2026     │
│                                                  │
│ ┌───────────────────────┬──────────────────────┐│
│ │ 🚚 AUTA AVILOG DZIŚ    │ 📋 ZAMÓWIENIA DZIŚ  ││
│ │                        │                      ││
│ │ 03:00 Stróżewski 2t    │ Damak: 4t Filet A   ││
│ │ 03:30 Przybysz Bogd. 5t│ Trzepałka: 3t Ćw.   ││
│ │ 04:00 Łukawska A. 1.5t │ Bomafar: 1.5t tusz. ││
│ │ ...                    │ ...                  ││
│ │ RAZEM: 70 000 szt      │ RAZEM: 60 t tuszki  ││
│ │ Ważne: 200 t żywej w.  │                      ││
│ └───────────────────────┴──────────────────────┘│
│                                                  │
│ 📊 PLAN PRODUKCJI:                               │
│   Żywiec 200t × 78% = 156t tuszki                │
│   Klasa A (cel 80%) = 125t                       │
│   Klasa B (rozbiór) = 31t                        │
│     → Filet: 9.2t  → Ćwiartka: 10.4t             │
│     → Skrzydło: 2.7t → Korpus: 7.0t              │
│                                                  │
│ ┌─────────────┬─────────────┬─────────────┐    │
│ │ ▶ START LINIA│ + PADŁE: 0 │ ⛔ AWARIA   │    │
│ └─────────────┴─────────────┴─────────────┘    │
└─────────────────────────────────────────────────┘
```

**Funkcje:**
- Klik **START** → log do bazy `LiniaUboju.StartDnia` z czasem + Łukasz ID (UNICARD)
- Klik **+PADŁE** → wpis do `SpecyfikacjeSurowca.Padłe` per hodowca
- Klik **AWARIA** → wybór typu (patroszarka, skubarka, chiller, inne) + auto-broadcast Teams

#### **Stanowisko 2: Klasyfikator A/B**
**Sprzęt:** Tablet 7" wodoszczelny przy linii wagowej (3 sztuki — może rotacja).

**Ekran "Klasyfikacja"**:
```
┌─────────────────────────────────────────────────┐
│ ⚖️ KLASYFIKACJA — 02.05.2026 | 09:42             │
│ Operator: Marek K. | Stanowisko: 2               │
│                                                  │
│ ┌─────────────────────────────────────────┐    │
│ │            ┌───────┐                     │    │
│ │            │   A   │ +1 (zielony, duży) │    │
│ │            └───────┘                     │    │
│ │                                          │    │
│ │  [krwiak] [złamanie] [żółć] [oparzenie] │    │
│ │  [otwarta rana] [czerwony filet] [INNE] │    │
│ └─────────────────────────────────────────┘    │
│                                                  │
│ 📊 LICZNIK DZIŚ:                                 │
│   A: 23 451 szt                                  │
│   B: 4 821 szt (17.1%) ← cel max 20%            │
│                                                  │
│ Rozbicie B:                                      │
│   krwiak: 1 230 (25.5%)                          │
│   złamanie: 870 (18.0%)                          │
│   żółć: 654 (13.6%)                              │
│   oparzenie: 412 (8.5%)                          │
│   ...                                            │
│                                                  │
│ ⚠️ ALERT: % B rośnie 14% → 17.1% przez 1h       │
│   Sprawdź partię: Stróżewski (auto z hod.)       │
└─────────────────────────────────────────────────┘
```

**Logika:**
- Każde naciśnięcie → zapis do `KlasyfikacjaA_B` z timestamp + operator + powód B
- ZPSP automat liczy % per godzina, alert gdy >20%
- Drill-down: bieżąca partia (z `WstawieniaKurczakow` po godzinie)

#### **Stanowisko 3: Anna Majczak (brygadzista hali) — według odpowiedzi Sergiusza**
**Według odpowiedzi:** Anna potrzebuje od ZPSP:
1. **Panel produkcji** (ogólny rytm)
2. **Panel jakości** (% klasy A/B real-time)
3. **Wydajność fileta** (kg/h)
4. **Ilość osób na bieżąco** (z UNICARD)
5. **Awarie linii**

**Sprzęt:** Tablet 12" w jej biurze + 2-3 wyświetlacze na hali.

**Ekran "Hala LIVE":**
```
┌───────────────────────────────────────────────────────┐
│ 🏭 HALA LIVE — Anna Majczak | 02.05.2026 11:32        │
│                                                        │
│ 📊 SZTUK OD STARTU: 41 230 / 70 000 (58.9%)           │
│ ⚡ TEMPO: 7 240 szt/h (cel 7 500)                     │
│                                                        │
│ ┌─────────────────────────────────────────┐           │
│ │ ████████████████░░░░░░░░░░░░░░░░░░░░░░░│ Postęp    │
│ └─────────────────────────────────────────┘           │
│                                                        │
│ ┌────────────────┬──────────────┐                    │
│ │ KLASA A vs B   │ OSOBY NA HALI│                    │
│ │                │              │                    │
│ │  82.9% A       │  Ubój:    24 │                    │
│ │  17.1% B       │  Klasy:    6 │                    │
│ │                │  Rozbiór: 18 │                    │
│ │ ✅ PONAD CEL  │  Magazyn:  4 │                    │
│ │  (cel 80%)     │  Mroźnia:  2 │                    │
│ │                │  RAZEM:   54 │                    │
│ └────────────────┴──────────────┘                    │
│                                                        │
│ 📈 WYDAJNOŚĆ ROZBIORU (kg/h):                         │
│   Filet:     1 240 kg/h (cel 1 500) ⚠️ -17%          │
│   Ćwiartka:  2 100 kg/h (cel 2 000) ✅                │
│   Skrzydło:    580 kg/h (cel 600)  ⚠️ -3%            │
│                                                        │
│ ⚠️ ALERTY:                                            │
│   • Filet poniżej tempa — sprawdź stanowisko 3        │
│   • Stanisław Z. nie zalogowany (zaczął 7:15)         │
└───────────────────────────────────────────────────────┘
```

#### **Stanowisko 4: Cockpit Justyny (Plant Director)**
**Sprzęt:** Monitor 4K w biurze + tablet do hali.

**Co Sergiusz chce dla Justyny** (z odpowiedzi):
> "Wszystko co produkcyjne, jakościowe i magazynowe. Chce mieć duży monitor w swoim biurze, na którym będzie mogła na żywo monitorować kluczowe wskaźniki produkcji, jakości i stanu magazynowego. Chce mieć dostęp do raportów, ale również możliwość monitorowania danych na żywo, aby szybko reagować na ewentualne problemy."

**Ekran "Cockpit Plant Director":**
```
┌──────────────────────────────────────────────────────────────────┐
│ 🎯 COCKPIT JUSTYNY | 02.05.2026 11:32 | LIVE                     │
│                                                                   │
│ ┌─────────────┬─────────────┬─────────────┬─────────────────┐   │
│ │ PRODUKCJA   │ JAKOŚĆ       │ MAGAZYN      │ ALERTY          │   │
│ │             │              │              │                 │   │
│ │ 41 230 szt  │ A: 82.9%     │ 65554:       │ ⚠️ Filet -17% │   │
│ │ 7 240 szt/h │ B: 17.1%     │  78 t        │ ⚠️ Stróżewski │   │
│ │ Plan 70k    │              │              │   3 reklam.   │   │
│ │ -1 awaria   │ Reklamacje:  │ 65556:       │ ✅ Tempo OK   │   │
│ │   8 min     │  3 otwarte   │  12 t        │ ✅ Klasa A 83%│   │
│ │             │  1 pilna     │              │                │   │
│ │             │              │ Mroźnia: 280t│                │   │
│ └─────────────┴─────────────┴─────────────┴─────────────────┘   │
│                                                                   │
│ 📹 KAMERY HIKVISION (klik = full screen):                        │
│ ┌─────┐ ┌─────┐ ┌─────┐ ┌─────┐                                │
│ │Linia│ │Klasy│ │Rozb.│ │Magaz│                                │
│ │ ●   │ │  ●  │ │  ●  │ │  ●  │                                │
│ └─────┘ └─────┘ └─────┘ └─────┘                                │
│                                                                   │
│ 📋 TASK LIST DZIŚ:                                               │
│   ✅ 7:00 Temp chłodni — sprawdzona                              │
│   ⏳ 9:00 Wyrywkowa kontrola                                     │
│   ⏳ 11:00 Kontrola krojenia                                     │
│   ⏳ 13:00 Spotkanie operacyjne                                  │
│   ⏳ 15:00 Kontrola II zmiany                                    │
└──────────────────────────────────────────────────────────────────┘
```

### 3.2 KPI per stanowisko

| Stanowisko | KPI |
|---|---|
| **Łukasz Collins** | Sztuk dziś / cel; tempo szt/h; awarie; padłe; uzysk % |
| **Klasyfikator** | % A vs B per godzina; rozkład powodów B; alert >20% B |
| **Anna Majczak** | Wszystko z hali — produkcja/jakość/wydajność/osoby |
| **Justyna** | Wszystko + magazyn + reklamacje + alerty |
| **Filetowca** | Kg/h fileta per osoba (z RFID karty + waga) |
| **Pakowacz tuszki A** | Pojemniki/h, średnia waga, % poza tolerancją |

### 3.3 Tabele bazy danych (nowe)

```sql
-- Klasyfikacja A/B z czasem i powodem
CREATE TABLE KlasyfikacjaA_B (
    Id INT IDENTITY PRIMARY KEY,
    DataCzas DATETIME2 NOT NULL,
    OperatorID NVARCHAR(20) NOT NULL,  -- z UNICARD
    Stanowisko INT NOT NULL,           -- 1, 2, 3
    Klasa CHAR(1) NOT NULL,            -- 'A' lub 'B'
    PowodB NVARCHAR(50),               -- 'krwiak', 'zlamanie', 'zolc', 'oparzenie', 'inne'
    NumerPartii NVARCHAR(20),          -- z linii ubojowej (powiązanie z hodowcą)
    Hodowca NVARCHAR(100),             -- skrót z PartiaDostawca
    INDEX IX_DataCzas (DataCzas DESC),
    INDEX IX_Hodowca (Hodowca, DataCzas)
);

-- Awarie i przestoje linii
CREATE TABLE PrzestojeLinia (
    Id INT IDENTITY PRIMARY KEY,
    Start DATETIME2 NOT NULL,
    Koniec DATETIME2,
    TypAwarii NVARCHAR(50) NOT NULL,   -- 'patroszarka', 'skubarka', 'chiller', 'inne'
    Opis NVARCHAR(500),
    Operator NVARCHAR(20),             -- kto zgłosił
    ZglaszanyDo NVARCHAR(50),          -- 'mechanik', 'serwis_zewn'
    KosztSzacowany DECIMAL(10,2),
    INDEX IX_Start (Start DESC)
);

-- Wydajność per pracownik (z RFID przy wadze)
CREATE TABLE WydajnoscPracownika (
    Id INT IDENTITY PRIMARY KEY,
    DataCzas DATETIME2 NOT NULL,
    OperatorID NVARCHAR(20) NOT NULL,
    Stanowisko NVARCHAR(50) NOT NULL,  -- 'filet', 'cwiartka', 'skrzydlo', 'palka', etc.
    KG DECIMAL(10,2) NOT NULL,
    Sztuki INT,
    INDEX IX_OperatorData (OperatorID, DataCzas DESC)
);

-- Plan dnia (z modułu Krojenie 14A)
CREATE TABLE PlanDnia (
    Id INT IDENTITY PRIMARY KEY,
    Data DATE NOT NULL,
    KodTowaru INT NOT NULL,            -- z HM.TW
    PlanKG DECIMAL(10,2) NOT NULL,
    RealKG DECIMAL(10,2),
    Ustalono DATETIME2 NOT NULL,
    UstalilUserID NVARCHAR(20),
    INDEX IX_Data (Data DESC)
);
```

### 3.4 Procedury cyfrowe (zastępujące papier)

**Procedura "Start linii":**
1. Łukasz loguje UNICARD karty
2. Sprawdza checkbox: smarowanie / dezynfekcja / woda / temperatury
3. Klik "START" w tablecie → log w bazie
4. Auto-broadcast Teams "Linia START 03:42, plan 70 000 szt"

**Procedura "Awaria":**
1. Operator naciska "AWARIA" na tablecie
2. Wybiera typ: patroszarka / skubarka / chiller / inne
3. ZPSP auto: SMS do mechanika + Łukasza + Justyny
4. Mechanik klika "Przyjąłem zgłoszenie"
5. Po naprawie — operator klika "Naprawione" + opis
6. Auto-log do `PrzestojeLinia`

**Procedura "Klasa B rośnie":**
1. ZPSP wykrywa: % B w ostatniej godzinie >20%
2. Auto-alert do Justyny + Klaudii (Teams)
3. Justyna sprawdza: jaka partia? jaki hodowca?
4. Decyzja: kontynuować / wstrzymać / zgłosić hodowcy
5. Log decyzji w bazie

---

## 4. MAGAZYN — SZCZEGÓŁOWY PLAN

### 4.1 Stanowiska + tablety

#### **Stanowisko 1: Magazynier 65554 (świeże)**
**Sprzęt:** Tablet 10" wodoszczelny w pomieszczeniu magazynowym.

**Ekran "Magazyn świeży":**
```
┌─────────────────────────────────────────────────────┐
│ 📦 MAGAZYN ŚWIEŻY 65554 | 02.05.2026 14:15          │
│                                                      │
│ 📊 STAN AKTUALNY: 78 234 kg w 412 paletach          │
│                                                      │
│ MAPA WIEKU TOWARU:                                   │
│ ┌──────────────────────────────────────────────┐    │
│ │ █ █ █ █ █ █ █ █ █ █ █ █ █ █ █ █             │    │
│ │ Zielony (0-12h):     302 palet (73%)         │    │
│ │ Żółty (12-24h):       89 palet (22%)         │    │
│ │ Czerwony (24h+):      21 palet (5%) ⚠️       │    │
│ └──────────────────────────────────────────────┘    │
│                                                      │
│ ⚠️ FIFO ALERTY:                                     │
│   Paleta 234 (Filet A, 28h, partia 26119001)        │
│   Paleta 235 (Korpus, 26h, partia 26119001)         │
│   → Wydaj jako PIERWSZE jutro                        │
│                                                      │
│ 🌡️ TEMPERATURA: 2.3°C ✅ (cel 0-4°C)                │
│                                                      │
│ ┌─────────────────────────────────────────────┐    │
│ │ DZIŚ WCHODZIŁO Z LINII:                     │    │
│ │   Tuszka A:    78 t   ✅                     │    │
│ │   Filet A:     12 t   ✅                     │    │
│ │   Ćwiartka:    14 t   ✅                     │    │
│ │   Korpus:       8 t   ✅                     │    │
│ │   ...                                        │    │
│ └─────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────┘
```

#### **Stanowisko 2: Magazynier rampa 65556 (wydania)**
**Sprzęt:** Tablet 10" przy rampie + drukarka Zebra przy stanowisku.

**Ekran "Rampa — załadunek"**:
```
┌────────────────────────────────────────────────────┐
│ 🚛 RAMPA ZAŁADUNKOWA 65556 | 02.05.2026 14:15     │
│                                                     │
│ DZIŚ DO ZAŁADOWANIA:                                │
│ ┌──────────────────────────────────────────────┐  │
│ │ 14:00 ⏳ Damak (4t Filet A) — czeka          │  │
│ │ 14:30 ✅ Trzepałka (3t Ćwiartka) — załad.    │  │
│ │ 15:00 ⏳ Bomafar (1.5t Tuszka) — wjeżdża     │  │
│ │ 15:30 ⏸️ Publimar (2t Korpus) — opóźnione   │  │
│ └──────────────────────────────────────────────┘  │
│                                                     │
│ AKTUALNY ZAŁADUNEK: DAMAK                          │
│ ┌──────────────────────────────────────────────┐  │
│ │ ✅ Filet A — paleta 234 (450 kg) — FIFO     │  │
│ │ ✅ Filet A — paleta 235 (480 kg) — FIFO     │  │
│ │ ✅ Filet A — paleta 245 (420 kg)            │  │
│ │ ☐ Filet A — paleta 256 (450 kg)            │  │
│ │ ☐ Filet A — paleta 258 (440 kg)            │  │
│ │ ...                                          │  │
│ │                                              │  │
│ │ POSTĘP: 1 350 / 4 000 kg (33%)              │  │
│ └──────────────────────────────────────────────┘  │
│                                                     │
│ ┌──────────────────────────────────────────────┐  │
│ │ ⚠️ BRAK 200 kg — KLIKNIJ JEŚLI BRAKUJE      │  │
│ │ → Auto-alert do Pani Joli + Justyny          │  │
│ └──────────────────────────────────────────────┘  │
│                                                     │
│ ┌─────────────┬─────────────┬─────────────┐      │
│ │ ✅ ZAŁAD.   │ 📄 WZ DRUK  │ 🔒 PLOMBA   │      │
│ └─────────────┴─────────────┴─────────────┘      │
└────────────────────────────────────────────────────┘
```

### 4.2 KPI Magazyn
- **Stan magazynu w czasie rzeczywistym** (kg, liczba palet, średni wiek)
- **% palet >24h** (cel <5%)
- **Czas wydania per auto** (cel 30-60 min)
- **Zgodność ZPSP vs fizyczna inwentaryzacja** (cel ±5 kg)
- **Liczba "BRAK" alertów** (cel 0)

### 4.3 Procedura digital "Wydanie"
1. Klient awizuje (slot booking ZPSP)
2. Kierowca wjeżdża → magazynier otwiera panel
3. Wybiera klienta z listy → ZPSP pokazuje pozycje wg FIFO
4. Magazynier kompletuje paletę po palecie, klika checkboxy
5. Brakuje → klika "BRAK" + powód → auto-alert handlowca
6. Komplet → klika "ZAŁAD." → ZPSP generuje WZ
7. Drukarka Zebra drukuje WZ
8. Klika "PLOMBA" → wpisuje numer
9. Kierowca podpisuje (papierowo lub ekran)
10. Status zamówienia → "Załadowane"

### 4.4 Inwentaryzacja cyfrowa
**Codzienna (koniec dnia + rano):**
- Tablet w magazynie
- Lista palet z ZPSP
- Magazynier skanuje (lub zaznacza wzrokowo)
- Rozbieżność → alert

**Tygodniowa pełna mroźni:**
- Plan: 4-osobowa ekipa
- Tablet z listą zamrożonych pojemników
- Każdy pojemnik: status (jest / brak / inny stan)
- Raport końcowy → różnice

---

## 5. MROŹNIA — SZCZEGÓŁOWY PLAN

### 5.1 Cockpit Mroźni
**Sprzęt:** Tablet 10" przy wejściu do mroźni + duży monitor w biurze Sergiusza/Justyny.

**Ekran "Mroźnia LIVE":**
```
┌──────────────────────────────────────────────────┐
│ ❄️ MROŹNIA — 02.05.2026 14:15                   │
│                                                   │
│ TEMPERATURY:                                      │
│   Komora 1: -19.3°C ✅ (cel -18 do -20)          │
│   Komora 2: -19.1°C ✅                           │
│   Komora 3: -18.7°C ⚠️ (blisko granicy)          │
│   Szokówka: -32.4°C ✅                           │
│                                                   │
│ STAN PARTII (3 komory):                          │
│ ┌────────────────────────────────────────────┐   │
│ │ KOMORA 1 (świeże mrożenie, 0-30 dni):      │   │
│ │   78 partii, 32 t                          │   │
│ │                                             │   │
│ │ KOMORA 2 (30-90 dni):                      │   │
│ │   142 partii, 58 t                         │   │
│ │                                             │   │
│ │ KOMORA 3 (90-180 dni, EKSPORT):            │   │
│ │   89 partii, 42 t                          │   │
│ │                                             │   │
│ │ ⚠️ STARE (>180 dni):                       │   │
│ │   23 partii, 11 t — DECYZJA Sergiusz      │   │
│ └────────────────────────────────────────────┘   │
│                                                   │
│ DZIŚ:                                             │
│   Do mrożenia (z 13:00): 800 kg Filet, 200 kg... │
│   Do wydania: 1 200 kg Filet (eksport Ania)      │
│                                                   │
│ ⚠️ ALERTY:                                       │
│   • Partia 25034 leży 270 dni — SPRAWDŹ          │
│   • Komora 3 temp blisko granicy                 │
└──────────────────────────────────────────────────┘
```

### 5.2 Workflow przyjęcia do mroźni
1. Decyzja 13:00 (Sergiusz/Justyna): mrozić X
2. Pracownik mroźni otwiera tablet → "Do mrożenia dziś" → lista
3. Każdy pojemnik:
   - Waży na wadze paletowej (RADWAG → ZPSP)
   - Drukuje etykietę mrożeniczą (data, partia, waga)
   - Skanuje (lub wpisuje numer pojemnika)
   - Wkłada do komory (system sugeruje którą)
4. ZPSP automat: wpis do `State0E` z datą mrożenia

### 5.3 Workflow wydania z mroźni
1. Handlowczyni (Ania) wpisuje zamówienie mrożone w ZPSP
2. ZPSP wymaga: zatwierdzenie Sergiusza
3. Po zatwierdzeniu → pracownik mroźni widzi zlecenie
4. Lista FIFO → najstarsze
5. Wydaje, waży, etykietuje WZ
6. Strata waga in→out: jeśli >2% → alert

### 5.4 KPI Mroźnia
- **Średni wiek partii** per komora
- **% partii >180 dni** (cel <5%)
- **Strata waga in→out** (cel ≤2%)
- **Temperatura — odchyłki** (cel: 0 minut poza pasmem)
- **Inwentaryzacja tygodniowa — rozbieżność** (cel <1%)

---

## 6. JAKOŚĆ — SZCZEGÓŁOWY PLAN

### 6.1 Cockpit Justyny
**Sprzęt:** Monitor 4K w biurze + tablet do hali + dostęp z phone.

(Wykorzystany w sekcji 3.1)

### 6.2 Reklamacje — filtrowanie pseudo-reklamacji

**Aktualne (kwiecień 2026):** 247 reklamacji w 90 dni, 75% to **automatyczne faktury korygujące** z Symfonii (`SyncFakturyKorygujace()` co 5 min).

**Plan:**
1. **Filtr w UI:**
```sql
WHERE NOT (TypReklamacji = 'Faktura korygujaca' AND ZrodloZgloszenia = 'Symfonia')
```
2. **Osobna zakładka "Korekty Symfonii"** — lista pseudo-reklamacji z możliwością:
   - Połączenia z prawdziwą reklamacją (POWIAZANA)
   - Oznaczenia "OK, juz rozliczone w HANDEL" (IGNOROWANA)
   - Bulk action (auto-zamykanie po 30 dniach)

3. **Statystyki tylko realnych:**
- Top przyczyny (z `PrzyczynaGlowna`, nie NULL)
- Per handlowczyni
- Trend roczny

### 6.3 Plan szkoleniowy Klaudii i Gabrieli (cyfrowy)

**Sergiusz: "Klaudia bardzo zaangażowana, chce się rozwijać. Plan: mentor + szkolenie teoretyczne + praktyczne + egzamin. To samo dla Gabrieli."**

**Moduł "Szkolenia QC" w ZPSP:**
- Curriculum (12 tygodni):
  - Tydzień 1-2: Teoria HACCP
  - Tydzień 3-4: Praktyka — mycie i dezynfekcja
  - Tydzień 5-6: Klasyfikacja A/B (cienie do Justyny)
  - Tydzień 7-8: Reklamacje workflow
  - Tydzień 9-10: BRC/IFS — wymagania
  - Tydzień 11: Próbki mikrobiologiczne (lab Brudzewo)
  - Tydzień 12: Egzamin
- Mentor: Justyna
- Każdy moduł: video + quiz + ćwiczenie praktyczne
- Status w ZPSP: % ukończony

### 6.4 HACCP cyfrowy

**Aktualnie:** 22 717 wpisów w `Haccp` (papier? Excel?)

**Plan:**
- Tablet w każdej strefie (chłodnie, mroźnie, hala, magazyn)
- Auto-pomiar temperatur (z czujników IoT — koszt ~5k PLN za zestaw)
- Manualny wpis: czystość, wymazówki, dezynfekcja
- Auto-raport miesięczny dla audytora BRC

### 6.5 BRC v9 + IFS — dokumentacja cyfrowa

**Plan:**
- Folder w SharePoint (po migracji M365): `BRC_v9` + `IFS`
- Każda procedura: Word + wersja archiwalna + lista akceptujących
- Dashboard "% gotowości BRC" — % wymagań spełnionych
- Audyt cykliczny w ZPSP (Justyna sprawdza listę miesięcznie)
- Plan szkoleń wewnętrznych — co kwartał

### 6.6 KPI Jakość
- **% klasy A** (cel ≥80%, alarm <75%)
- **Liczba reklamacji realnych** (bez auto-import) — trend miesięczny
- **Średni czas zamknięcia reklamacji** (cel ≤48h)
- **% reklamacji per handlowczyni** (porównanie)
- **% reklamacji per hodowca** (ranking jakościowy)
- **% szkoleń ukończonych** (Klaudia, Gabriela, asystent)
- **Próbki mikrobiologiczne** — % pozytywnych (cel <0,1%)
- **% wymagań BRC spełnionych** (panel postępu)

---

## 7. INTEGRACJA WAGO + RADWAG

### 7.1 Stan obecny
- **WAGO** (waga selektywna klasy A/B + klasy wagowe) — **brak dostępu API**
- **RADWAG** (wagi platformowe + paletowa) — **brak dostępu API**

### 7.2 Plan integracji — krok po kroku

**Krok 1 (TYDZIEŃ 1): Kontakt z dostawcami**
- Sergiusz pisze do WAGO + RADWAG:
  > "Prosimy o specyfikację API/SQL/OPC-UA do zewnętrznej integracji z naszym ERP. Cel: real-time pomiar wydajności hodowcy + tempo linii."

**Krok 2 (TYDZIEŃ 2-4): Specyfikacja techniczna**
Trzy potencjalne ścieżki:
- **A. SQL Read-Only** — direct query do bazy WAGO/RADWAG (najszybsze)
- **B. REST API** — endpoint z każdą wagą (bezpieczne, standardowe)
- **C. OPC-UA** — przemysłowy standard (najlepsze, ale złożone)

**Krok 3 (MIESIĄC 2): Implementacja w ZPSP**
- Nowy serwis: `WagoIntegrationService.cs` + `RadwagIntegrationService.cs`
- Pull co 60s z każdego stanowiska
- Zapis do tabel:
  - `WagoEvent` (kiedy, klasa, waga, dźwignia ↑/↓, klasa A/B)
  - `RadwagEvent` (kiedy, stanowisko, kg, partia)
- Cross-join z `KlasyfikacjaA_B` (ręcznie wpisana) → walidacja

**Krok 4 (MIESIĄC 3): Dashboard wydajności hodowcy**
```
HODOWCY — RANKING JAKOŚCI 2026-04
═══════════════════════════════════════════
Hodowca         │ %A   │ %B   │ Padłe │ Konfisk│ Score
──────────────────────────────────────────────
Stróżewski      │ 78%  │ 22%  │ 0.4% │ 1.2%   │ 76 ⚠️
Przybysz Bogdan │ 88%  │ 12%  │ 0.2% │ 0.8%   │ 92 ✅
Kępa            │ 85%  │ 15%  │ 0.5% │ 1.0%   │ 84 ✅
Łukawska        │ 82%  │ 18%  │ 0.3% │ 0.9%   │ 81 ✅
...
```

**Drill-down per hodowca:**
- Trend miesięczny
- Konkretne partie (data, %A, %B, padłe)
- Reklamacje od jego partii
- Auto-rekomendacja: zwiększyć / utrzymać / zmniejszyć kontrakt

### 7.3 Plan B (jeśli dostawcy odmówią API)

**Manualne wpisywanie z tabletu klasyfikatora** (sekcja 3.1, stanowisko 2):
- Klasa A/B + powód B → tabela `KlasyfikacjaA_B`
- Powiązanie z partią po godzinie ubicia (`WstawieniaKurczakow.PlanowanaDataUboju`)
- 95% dokładności (vs 100% z WAGO)

**To wystarczy do startu.** Po API z WAGO — porównanie z manualnym (walidacja, korekta).

---

## 8. TABELE BAZY DANYCH DO DODANIA

```sql
-- Klasyfikacja A/B + powód
CREATE TABLE KlasyfikacjaA_B (...);

-- Awarie linii
CREATE TABLE PrzestojeLinia (...);

-- Wydajność per pracownik (RFID + waga)
CREATE TABLE WydajnoscPracownika (...);

-- Plan dnia (Krojenie 14A)
CREATE TABLE PlanDnia (...);

-- Mapowanie stanowisk
CREATE TABLE Stanowiska (
    Id INT IDENTITY PRIMARY KEY,
    Kod NVARCHAR(20),  -- 'UB-01', 'KL-02', 'RZ-FIL-1'
    Nazwa NVARCHAR(100),
    Strefa NVARCHAR(50),  -- 'ubojnia', 'klasyfikacja', 'rozbior'
    LiczbaOperatorow INT,
    Aktywne BIT
);

-- WAGO eventy (po integracji)
CREATE TABLE WagoEvent (
    Id BIGINT IDENTITY PRIMARY KEY,
    DataCzas DATETIME2,
    Stanowisko INT,
    WagaTuszki DECIMAL(8,3),
    KlasaWagowa INT,         -- 6, 7, 8, 9, 10, 11
    Dzwignia BIT,            -- 1 = w górę = klasa B
    PartiaId INT,
    INDEX IX_DataCzas (DataCzas DESC)
);

-- RADWAG eventy
CREATE TABLE RadwagEvent (
    Id BIGINT IDENTITY PRIMARY KEY,
    DataCzas DATETIME2,
    Stanowisko INT,
    OperatorID NVARCHAR(20),  -- z RFID karty operatora
    KodTowaru INT,            -- z drukarki etykiet
    WagaKg DECIMAL(10,3),
    EtykietaUUID UNIQUEIDENTIFIER,
    INDEX IX_OperatorData (OperatorID, DataCzas DESC),
    INDEX IX_DataCzas (DataCzas DESC)
);

-- Szkolenia QC
CREATE TABLE SzkoleniaQC (
    Id INT IDENTITY PRIMARY KEY,
    OsobaID INT,              -- z UNICARD
    Modul NVARCHAR(100),      -- 'HACCP_teoria', 'BRC_wymagania', etc.
    DataStart DATETIME2,
    DataKoniec DATETIME2,
    StatusUkonczenia DECIMAL(5,2),  -- 0-100%
    WynikQuizu DECIMAL(5,2),
    Mentor NVARCHAR(100),     -- 'Justyna'
    Notatki NVARCHAR(MAX)
);

-- HACCP wpisy
CREATE TABLE HaccpWpisy (
    Id BIGINT IDENTITY PRIMARY KEY,
    DataCzas DATETIME2 NOT NULL,
    OsobaID INT,
    Strefa NVARCHAR(50),
    TypWpisu NVARCHAR(50),    -- 'temperatura', 'czystosc', 'mycie', 'wymazowka'
    Wartosc DECIMAL(10,3),
    Jednostka NVARCHAR(10),
    StatusOK BIT,
    Notatki NVARCHAR(500),
    INDEX IX_DataStrefa (DataCzas, Strefa)
);

-- Audyt mroźni (tygodniowy)
CREATE TABLE AudytMrozni (
    Id INT IDENTITY PRIMARY KEY,
    DataAudytu DATE,
    Komora INT,
    PartiaId INT,
    StatusFizyczny NVARCHAR(20), -- 'jest', 'brak', 'inny'
    UwagiAudytora NVARCHAR(500),
    OsobaAudytujaca NVARCHAR(100)
);
```

---

## 9. PROCEDURY CYFROWE — ZASTĘPUJĄCE PAPIER

| Procedura papierowa (PROCEDURY_0X) | Plan cyfrowy |
|---|---|
| **Plan dnia produkcji** (papier z liczbami) | Tablet Łukasza, auto-update z `PlanDnia` |
| **Klasyfikacja A/B** (notatka w głowie) | Tablet klasyfikatora, każdy klik logowany |
| **Padłe + konfiskaty** (kartka po zmianie) | Tablet zawieszania → przyciski → auto-log |
| **Wydanie towaru** (lista z biura) | Panel magazyniera → checkbox FIFO |
| **Inwentaryzacja codzienna** (papier, kalkulacje ręczne) | Tablet, auto-różnice |
| **Reklamacje** (papier rozdawany Justynie) | ZPSP workflow, alert do 15:00 |
| **Wydanie z mroźni** (zezwolenie ustne) | Wymagana cyfrowa autoryzacja Sergiusza |
| **Onboarding agencyjnych** (kartka + podpis) | Tablet → wideo szkolenie → quiz → podpis cyfrowy |
| **Spotkanie 13:00** (notatki ręczne) | Notatki w ZPSP, auto-broadcast Teams |
| **Audyt BRC wewn.** (lista checklist) | Cyfrowy checklist co miesiąc |

---

## 10. ROADMAP 3/6/12 MIESIĘCY

### 10.1 NA TERAZ (najbliższe 4 tygodnie)
**Quick wins, niski koszt:**
1. ✅ **Backup ZPSP codzienny** (1 dzień) — krytyczne ryzyko
2. ✅ **Filtr auto-import w Reklamacjach** (2h) — sygnał bezsenny → realny
3. ✅ **Marża per produkt w Dashboardzie** (1 dzień) — z formuły top-down
4. ✅ **Alert "% klasy A < 75%"** w DashboardPrzychodu (5 min) — bo już jest sidebar

### 10.2 ZA 3 MIESIĄCE (Q3 2026)
**Pierwsza fala wdrożeń:**
5. **Tablet klasyfikatora A/B** (3 sztuki) — pomiar % B + powodów
6. **Tablet Anny Majczak** (1 szt.) — Hala LIVE
7. **Cockpit Justyny** (rozbudowa PulpitZarzadu) — 4K monitor + LIVE
8. **Tabela `KlasyfikacjaA_B`** + UI
9. **Tabela `PrzestojeLinia`** + tablet awarii
10. **Plan szkoleniowy Klaudii** (cyfrowy, w ZPSP)
11. **Audyt 71 okien** → roadmapa konsolidacji

### 10.3 ZA 6 MIESIĘCY (Q4 2026)
**Hala LIVE i magazyn:**
12. **Hala Produkcyjna LIVE** (scalenie ProdukcjaPanel + DashboardPrzychodu)
13. **Tab "Plan vs Realizacja"** w Dashboardzie Sprzedaży
14. **Panel magazyniera 2.0** — tablet wodoszczelny, FIFO, checkbox
15. **Patroszarka Meyn Maestro** (instalacja IX 2026)
16. **Mroźnia Cockpit** — mapa 3D komór
17. **Tabela `WydajnoscPracownika`** (RFID przy wadze)
18. **Email 7:00** PorannyBriefing → SMTP

### 10.4 ZA 12 MIESIĘCY (Q1-Q2 2027)
**Pełna integracja + rewolucja:**
19. **WAGO API** — real-time klasy A/B + tempo
20. **RADWAG API** — pełna automatyzacja wagi
21. **Cockpit Właściciela** (pełen)
22. **Plan & Bilans Produkcji** (nowy dashboard)
23. **Cockpit Jakości** (rozbudowa)
24. **HACCP cyfrowy** (czujniki IoT + tablety)
25. **RFID skanowanie wydania** (pod dotację ARiMR)
26. **BRC v9 audyt certyfikujący** (Q3 2027)

### 10.5 KPI sukcesu
- **% klasy A** wzrasta z 80% → 85% (lepsze monitorowanie hodowców)
- **Wąskie gardło rozbioru** rozwiązane (Meyn Maestro)
- **Reklamacje realne** spadek 50% (cykl naprawczy w 48h)
- **Czas wydania** spadek 25% (panel magazyniera 2.0)
- **Produkcja na osobę** wzrost (premia od wydajności)
- **Strata mrożenia** spadek poniżej 1.5% (lepszy FIFO)
- **% szkoleń ukończonych** Klaudia/Gabriela = 100%

---

## 11. SZACOWANE KOSZTY

### 11.1 Sprzęt (jednorazowo)
| Pozycja | Liczba | Cena/szt | RAZEM |
|---|---|---|---|
| Tablet Panasonic Toughbook FZ-S1 (10") | 8 | 5 000 | 40 000 |
| Monitor 4K 32" (Cockpit Justyny) | 1 | 4 000 | 4 000 |
| Drukarka Zebra ZD421 (rampy) | 3 | 3 000 | 9 000 |
| Czujniki IoT temperatury (HACCP) | 10 | 800 | 8 000 |
| Czytniki RFID (po dotacji) | 5 | 6 000 | 30 000 |
| Karty RFID dla pracowników | 200 | 50 | 10 000 |
| Monitor TV 55" (digital signage stołówka) | 1 | 3 500 | 3 500 |
| **RAZEM SPRZĘT** | | | **~104 500 PLN** |

### 11.2 Programowanie (czas Sergiusza + ewent. dev pomocniczy)
| Zadanie | Godziny | Koszt @150 PLN/h |
|---|---|---|
| Cockpit Justyny + ekrany | 80 | 12 000 |
| Tablet klasyfikatora + tabele | 60 | 9 000 |
| Panel magazyniera 2.0 | 80 | 12 000 |
| Mroźnia Cockpit + mapa 3D | 60 | 9 000 |
| Hala LIVE (scalenie) | 100 | 15 000 |
| Reklamacje filtr + zakładka | 30 | 4 500 |
| Marża per produkt | 40 | 6 000 |
| Tablet awarii + tabele | 30 | 4 500 |
| Plan szkoleniowy QC moduł | 60 | 9 000 |
| HACCP cyfrowy moduł | 80 | 12 000 |
| Integracja WAGO API | 100 | 15 000 |
| Integracja RADWAG API | 80 | 12 000 |
| Email 7:00 raport (RazorLight + PDF) | 40 | 6 000 |
| RFID wydania (po dotacji) | 100 | 15 000 |
| **RAZEM PROGRAMOWANIE** | **940 h** | **~141 000 PLN** |

### 11.3 Oprogramowanie / licencje (rocznie)
| Pozycja | Cena |
|---|---|
| DevExpress | ~4 500 PLN/rok |
| Microsoft 365 (20 użytkowników) | ~10 320 PLN/rok |
| WebFleet GPS (kontynuacja) | ~12 000 PLN/rok |
| KSeF Plus Optimum | 3 400 PLN/rok |
| SMS API (powiadomienia hodowców) | ~3 000 PLN/rok |
| Backup chmurowy (Azure / OVH) | ~2 400 PLN/rok |
| **RAZEM ROCZNIE** | **~35 620 PLN/rok** |

### 11.4 Total inwestycja 2026-2027
| Kategoria | Kwota |
|---|---|
| Sprzęt | ~105 000 PLN |
| Programowanie | ~141 000 PLN |
| Licencje (rok 1) | ~36 000 PLN |
| Szkolenia BRC/IFS (BioEfekt) | ~133 000 PLN (z FIRMA_PEŁEN_OBRAZ.md) |
| **RAZEM** | **~415 000 PLN** |

**Źródła finansowania:**
- Część (RFID, Patroszarka) — pod dotację ARiMR (50% zwrot)
- Część (Cockpity, tablety) — środki własne
- BRC/IFS — własne (już zatwierdzone)

---

# 🎯 PODSUMOWANIE — 3 PRIORYTETY

**Po przeczytaniu tego dokumentu, sugeruję 3 najważniejsze priorytety na maj 2026:**

## 1. **Skontaktuj WAGO + RADWAG o API** (TYDZIEŃ 1)
Bez tego nie zmierzymy efektywności hodowcy. Plan B (manualne tablety) wystarczy do startu, ale API to game-changer.

## 2. **Daily backup ZPSP** (TYDZIEŃ 1)
Krytyczne ryzyko biznesowe. 1 dzień pracy. **Pojutrze powinno być gotowe.**

## 3. **Filtr auto-import w Reklamacjach** (TYDZIEŃ 1)
2h pracy. Spada "fałszywy" alarm reklamacji z 247 → 60 (realne). Justyna i ja od razu widzimy prawdę.

**Po tych 3** — ruszamy roadmap 3-mies. (tablet klasyfikatora, Cockpit Justyny, plan szkoleniowy Klaudii).

---

**Daj sygnał:** czy zaczynamy któryś z tych 3 priorytetów? Czy najpierw chcesz odpowiedzieć na pytania z `PYTANIA_PRODUKCJA.md` (sceny 5-16) i wypełnić `STANOWISKA_PRODUKCJI.md`?
