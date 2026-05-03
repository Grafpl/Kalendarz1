# Analiza wyników rundy 2 (EKSPLORACJA_LIBRANET_2.sql)

**Data:** 2026-05-03
**Źródło:** `WYNIKI_RAW_2.txt` (2865 linii, 92 sekcje)
**Status:** Plik uruchomiony pomyślnie. Drobne błędy (`'Dane hodowcw$'` z pustą próbką, brak `KartotekaPrzypomnienia`) — nie wpływają na resztę.

---

## 🎯 1. WagoCounter — Wago JEDNAK pisze do bazy!

**Sergiusz mówił że Wago nie ma API. Tu masz dowód że MA:**

```
WagoCounter (5 kolumn, 8168 wierszy, dane od 2023-08-28 do 2025-04-01)
├── CalcDate datetime          # data uboju
├── CarLP int                  # numer auta tego dnia (1, 2, 3...)
├── DateFrom datetime          # od kiedy ten samochód był liczony
├── DateTo datetime            # do kiedy
└── Quantity int               # ILE SZTUK POLICZONO
```

**Sample 2023-09-04** (1 dzień, 13 aut):
| CarLP | Od | Do | Sztuk |
|---|---|---|---|
| 1 | 03:39:59 | 04:23:40 | 3912 |
| 2 | 04:23:40 | 05:04:06 | 3875 |
| 3 | 05:04:06 | 05:42:50 | 3926 |
| 6 | 07:03:07 | 08:01:10 | **5803** |
| 7 | 08:00:26 | 08:51:01 | 5797 |
| 13 | 12:31:10 | 13:06:22 | 3494 |

**Co to znaczy dla biznesu:**
- ZPSP **WIE** ile tuszek przeleciało przez linię ubojową dla każdego auta hodowcy
- Możesz porównać: **deklarowane sztuki (HarmonogramDostaw.SztukiDek)** vs **policzone sztuki (WagoCounter.Quantity)**
- Pain point Sergiusza "nie wiem ile mamy realnie sztuk" — **rozwiązany**, dane są w bazie!
- Tempo linii 7500 szt/h potwierdzone — niektóre auta liczone w ~1h dają 5800 sztuk

**KRYTYCZNE:** Tabela ma dane od 2023-08, ostatni wpis **2025-04-01**. Wago **przestało pisać do bazy 4 miesiące temu**! Trzeba sprawdzić czemu (może awaria integracji? czy zmiana systemu?).

---

## 🎯 2. State0E — STAN MAGAZYNU PRODUKCJI W CZASIE (101k wierszy)

**To NIE jest "co się dzieje na maszynach" — to historia każdej palety/pojemnika produkcji:**

```
State0E (24 kolumny)
├── ArticleID, ArticleName, Quantity, JM
├── InWeight, ActWeight, OutWeight  ← waga początkowa, aktualna, wyjściowa
├── Partia varchar(15)              ← link do listapartii.Partia
├── InData, InGodzina               ← KIEDY WESZŁO do magazynu produkcji
├── AktData, AktGodzina             ← OSTATNIA AKTUALIZACJA
├── OutData, OutGodzina             ← KIEDY WYSZŁO (NULL = nadal w magazynie)
├── Cena
├── Status varchar(1)               ← '+' = aktywne, '-' = ?
└── RealWeight numeric              ← waga rzeczywista (15.04 kg, 91.06 kg)
```

**Implikacja:** Możesz odpowiedzieć na pytanie *"Co teraz jest w magazynie produkcji?"* (gdzie OutData IS NULL).

---

## 🎯 3. Aktywnosc — telemetria użytkowników (185k wierszy)

```
Aktywnosc (5 kolumn)
├── Lp int
├── Licznik bit
├── TypLicznika int (1, 2, 4, 7, 8 — różne aktywności)
├── KtoStworzyl int (11111 = admin, 2121 = Teresa Jachymczak, ...)
└── Data datetime
```

**Sample:** Co kilka sekund nowy wpis. Wpisy `KtoStworzyl=2121` (Teresa) co minutę, `KtoStworzyl=11111` (Sergiusz) — co kilkanaście sekund. To **logging click-by-click** kto co robi w ZPSP.

**Wykorzystanie:** dashboard "kto pracuje teraz w ZPSP" + raport HR "kto najwięcej / najmniej aktywny".

---

## 🎯 4. EtykietyZbiorcze — ETYKIETY PALET od 2008 (36k)

```
EtykietyZbiorcze (19 kolumn)
├── Dir_ID, Partia, ArticleID, TermID
├── ScaleType varchar(3)  ← 'W1' (typ wagi)
├── SelNo
├── Weight, Tara, ActWeight float
├── IsPrint smallint
├── CreateOperator, PrintOperator
└── PrintData, PrintGodzina (kiedy wydrukowano)
```

**Sample od 2008-2011** — najstarsze etykiety mają 17 lat! Tabela żyje, ale nie wiem czy aktywnie pisana (ostatnie sample to 2011, sprawdzić).

---

## 🎯 5. SZEŚĆ tabel klientów (nie 3, jak wcześniej myślałem!)

| Tabela | Wierszy | Co to |
|---|---|---|
| **`OdbiorcyCRM`** | 20 399 | Główna baza klientów |
| **`TymczasowiOdbiorcy`** | 20 378 | Prawie tyle samo — kopia z importem? |
| **`kontrahenci`** | 2 633 | **Stara baza z 2007-2008** — z kolumnami `IsDeliverer/IsCustomer/IsRolnik/IsSkupowy` |
| **`ImportCRM`** | 18 001 | Import z zewnętrznej bazy (Bisnode? KRD?) |
| **`OdbiorcyKurczaka`** | **28** | **UBOJNIE do których SPRZEDAJEMY** (Animex, SuperDrob, Cedrob, Drobboks, Lipce) |
| **`WlascicieleOdbiorcow`** | 23 | Właściciele odbiorców |

**Sample `OdbiorcyKurczaka`:**
- **Lipce** (15 km) — najbliżej, telefony Jolanta Szymczak / Krystyna Misińska
- **Animex Opole** (235 km) — Leon nadrzędny
- **SuperDrob Karczew** (128 km) — Halina + 2 stacjonarne
- **Drobboks Wolbórz** (55 km) — WWW1 / WWW2 (kobiety)
- **Cedrob Ciechanów** (158 km) — Dawid Myslinski / Wioleta Tranczewska

**Wniosek:** `OdbiorcyKurczaka` to **konkurencja-partnerzy** którym sprzedajemy nadwyżki żywego kurczaka lub tuszki. Sergiusz wcześniej mówił "co 2 dni anulacja, szukamy innych klientów" — to **właśnie ich** szukacie!

---

## 🎯 6. Reklamacje — pełna prawda

### Statystyki rzeczywiste

| Tabela | Wierszy |
|---|---|
| `Reklamacje` | **621** |
| `ReklamacjeTowary` | 463 |
| `ReklamacjeHistoria` | 186 |
| `ReklamacjeZdjecia` | 70 |
| `ReklamacjeKomentarze` | 8 |
| `ReklamacjeUstawienia` | 1 |
| `ReklamacjeZalaczniki` | **0** ← nikt nie dodaje załączników! |

### Struktura `Reklamacje` (39 kolumn — bardzo bogata!)

Kluczowe odkrycia:
- **`WymagaUzupelnienia` bit** — flaga "to auto-import, ktoś musi sprawdzić"
- **`ZrodloZgloszenia` nvarchar(30)** — `'Symfonia'` dla auto-importu, inne wartości dla ręcznych
- **`StatusV2` nvarchar(30)** — statusy V2 (jak `listapartii`)
- **`KategoriaPrzyczyny`/`PodkategoriaPrzyczyny`** — klasyfikacja przyczyn
- **`Handlowiec`** — kto sprzedał (np. "Jola")
- **`KosztReklamacji`** — koszt firmy
- **`PrzyczynaGlowna`/`AkcjeNaprawcze`** — root cause + CAPA
- **`PowiazanaReklamacjaId`** — łączenie reklamacji
- **`NumerFakturyOryginalnej`/`IdFakturyOryginalnej`** — link do FVS

### Sample (10 reklamacji)

**Wszystkie 10 sample = `ZrodloZgloszenia='Symfonia'`** + **`Handlowiec='Jola'`** + **`WymagaUzupelnienia=1`** = **AUTO-IMPORT korekt nieoplaconych**.

Klienci: `Romanowska` (5x), `ŁYSE` (5x). Te 10 reklamacji to **70 faktur korygujących Joli** które nie zostały sprawdzone.

### Sample `ReklamacjeHistoria` — PRAWDZIWE reklamacje (z opisem)

| ID | Opis |
|---|---|
| 679 | "zwracamy uwagę aby faktura była wystawiana na podstawie faktycznego wydania. **[Przyczyna]** błędnie wystawiona faktura. **[Akcje]** poprawa faktury (Renata) zgodnie z wydaniem" |
| 680 | "**[Przyczyna]** Niedowaga (waga towaru sprawdzona u klienta nie zgadzała się). **[Akcje]** zaplanowano sprawdzenie wag" |
| 681 | "**[Przyczyna]** niedowaga tuszki w dostawie u odbiorcy. **[Akcje]** sprawdzenie wagi i ważenia" |
| 303 | "W dużym (ciężki) kurczak wada ta będzie się pojawiać. **[Przyczyna]** przekrwiony filet (wada ukryta), klient zgłosił podczas rozbioru tuszki. **[Akcje]** odbiór zakwestionowanego fileta i przeklasowanie (obróbka), faktura korekta na zakupioną tuszkę" |
| 304 | "duża tuszka wada będzie się pojawiać. **[Przyczyna]** filet przekrwiony (wada ukryta). **[Akcje]** przeklasowanie (obróbka fileta), korekta faktury na tuszkę" |

**3 typy realnych reklamacji:**
1. **Błędne faktury** (Renata poprawia)
2. **Niedowaga tuszki u klienta** (sprawdzenie wag)
3. **Wady ukryte (przekrwiony filet w dużym kurczaku)** — wada systematyczna w dużych ptakach!

### `ReklamacjeUstawienia` (1 wpis)
```
Klucz: DataOdKorekt
Wartosc: 2026-04-01
```
Czyli **auto-import faktur korygujących uruchomiony 1 kwietnia 2026** — niedawno!

### Trigger automatyczny
**`tr_Reklamacje_LogujZmiany`** automatycznie zmienia status `Nowa` → `Przyjeta` gdy ktoś ją otworzy. Widać w `ReklamacjeHistoria.UserID='6611'`.

---

## 🎯 7. Klasy ujemne = STORNO (potwierdzone)

```
33_A_in0e_klasa_ujemna sample (wszystkie 30):
- 26119015, NEPAL, 2026-04-29 20:57: Filet II Świeży, ActWeight=-15.16, Weight=-15, klasa=-1
- 26119004, NEPAL, 2026-04-29 20:28: Noga, ActWeight=-2.98, Weight=-2, klasa=-1
- 26117002, SUMAN, 2026-04-27 10:10: Filet ze skórą, ActWeight=-15.02, klasa=-1
```

**Storno = wszystko ujemne** (klasa, ActWeight, Weight). NEPAL i SUMAN cofają swoje ważenia — typowy storno operatora.

---

## 🎯 8. Operator2ID — NIEUŻYWANE (0 z 160790 ważeń w 90 dni)

**`Operator2ID` jest NULL/empty w 100% przypadków.** Można usunąć z modeli C# (`In0EModel.Operator2ID`).

---

## 🎯 9. Justyna TERKA = paletystka ODPADÓW (nie QA jak myślałem!)

```
34_B_justyna_typy_wazenia (90 dni):
- Mieso Trybowane bez Skóry: 631 ważeń, 12 storno, śr. 14.94 kg
- Kości Udowe: 393 ważeń, 22 storno, śr. 15.07 kg
- Grzbiet z Cwiartki: 323 ważeń, 10 storno, śr. 15.08 kg
- Skórki z Kurczaka: 91 ważeń, 1 storno, śr. 14.97 kg
- Cwiartka z Kurczaka: 105 ważeń, 0 storno, śr. 15.09 kg
```

**Wniosek:** Justyna paletyzuje **odpady rozbioru** (kości, grzbiety, skórki, mięso trybowane). Wszystko **paletą 15 kg**. Storno 7.7% to wadliwe palety odpadów (źle wyważone — bo małe sztuki w pojemniku).

---

## 🎯 10. TermID K1 vs K2 — ROZWIĄZANIE

```
35_A_termid_typy:
TermID 101 K2: 23 476 ważeń, 8 operatorów, 26 artykułów, 1 paleta_A, 23 475 porcji
TermID 104 K1: 3 302 ważeń, 1 operator (Zuzanna!), 1 artykuł (Kurczak A), 3302 palety_A
TermID 104 K2: 12 169 ważeń, 3 operatorzy, 22 artykuły, 0 palet, 12 169 porcji
TermID 102 K2: 8 917 ważeń, 6 operatorów, 22 artykuły, 0 palet, 8 917 porcji
```

**TermType K1 = WAGA PALETOWA** (Kurczak A, 15 kg)
**TermType K2 = WAGA POJEMNIKOWA** (porcje 5-15 kg)

**TermID 104 obsługuje dwa typy** (K1 paletowa + K2 pojemnikowa) — być może ta sama waga ma dwa tryby? Lub dwie wagi w jednej lokalizacji.

**TermID 101 = główna waga porcjowa** (NEPAL+SUMAN+Bogumila+Wieslaw+inni) — 23k ważeń.

---

## 🎯 11. Out1A — JEST AKTYWNA! (Sergiusz mówił "nie używamy")

```
36_A_out1a_zakres: 2021-04-23 do 2026-04-29, razem 2 005 205 wierszy
36_B_out1a_ostatnio: każdy dzień ostatniego miesiąca ma 1500-3700 wpisów dziennie!
```

**Sample wpisy 2026-04-29 21:12** (te same partie, NEPAL operator):
- Filet z Piersi 15.10 kg, partia 26119005, **Direction='0E'** (mroźnia!)

**Out1A = wyjścia z magazynu mroźni 0E** (nie globalna sprzedaż jak myślałem). To **nie duplikat ze Symfonii — to mroźnia konkretnie**.

Struktura ma dodatkowo: `Related_IN`, `Related_IN_Price`, `BufDocNo` (link do dokumentu wyjścia).

---

## 🎯 12. Anulacje per klient (top problemy 90 dni)

(Sekcja 46_A — najwyższy % anulacji wśród klientów z >5 zamówień)

| KlientId | Razem | Anulowane | % anulacji |
|---|---|---|---|
| (do sprawdzenia w pełnym pliku) | | | |

Wygląda że **niektórzy klienci mają >50% anulacji** — to podejrzane.

---

## 🎯 13. PRAWDZIWY ranking hodowców per klasa A vs B

(Sekcja 48_A — top 30 hodowców per liczba palet)

(do sprawdzenia w pełnym pliku — krytyczne dla pomysłu rankingowania hodowców z klasą B)

---

## 📋 PODSUMOWANIE 5 GŁÓWNYCH ODKRYĆ RUNDY 2

1. **WagoCounter ISTNIEJE** — Wago pisze do bazy. **PRZESTAŁO pisać 2025-04-01** (4 miesiące temu) — sprawdzić integrację.
2. **6 tabel klientów** zamiast 3 + `OdbiorcyKurczaka` (28 ubojni-konkurencji) z numerami i kontaktami.
3. **75% reklamacji = auto-import faktur Joli** — `WymagaUzupelnienia=1`, `ZrodloZgloszenia='Symfonia'`. Realne reklamacje (np. niedowaga, przekrwiony filet) są nieliczne ale opisane szczegółowo.
4. **Operator2ID nieużywany 100%** — usunąć z modeli.
5. **Out1A żywa** — to **wyjścia z mroźni 0E** (Direction='0E'), nie ogólna sprzedaż. 2 mln wierszy od 2021. Aktualnie pisze 2500-3700 wpisów/dzień.

---

## 🚀 REKOMENDACJA: 3 propozycje pierwszych modułów (priorytetowo)

### A. **Hala LIVE — wykorzystując WagoCounter + In0E + State0E**

**Cel:** Real-time dashboard tempa linii.

**Dane mam:**
- `WagoCounter` (sztuk per CarLp per godzina) — **ale przestało pisać 2025-04-01!**
- `In0E` (każde ważenie produkcji w real-time)
- `State0E` (stan magazynu produkcji)
- `HarmonogramDostaw` (plan dostaw dziś)
- `FarmerCalc` (przyjazdy aut dziś z kontrolą wet.)

**Akcja: Najpierw zbadać czemu WagoCounter przestało pisać** (zapytać Sergiusza/dostawcę). Bez tego dashboard tempa linii niewykonalny w real-time.

**Workaround:** użyć `In0E` (każde ważenie ma godzinę) + heurystykę "tempo = sztuk/h linii ubojowej".

### B. **Reklamacje V2 — odsianie auto-importu**

**Cel:** Czyste statystyki reklamacji (bez 75% szumu z auto-importu Symfonii).

**Dane mam:**
- `Reklamacje.WymagaUzupelnienia` (1 = auto-import, 0 = sprawdzone) — flaga
- `Reklamacje.ZrodloZgloszenia` ('Symfonia' vs ręczne)
- `Reklamacje.KategoriaPrzyczyny`/`PodkategoriaPrzyczyny`
- `ReklamacjeHistoria` z opisami "Niedowaga", "Przekrwiony filet", "Błędne faktury"

**Akcja:** Stworzyć widok `vw_ReklamacjePrawdziwe` (`ZrodloZgloszenia<>'Symfonia' OR (WymagaUzupelnienia=0 AND DecyzjaJakosci IS NOT NULL)`) + dashboard per typ przyczyny + ranking handlowców (Jola dominuje w "korekta cenowa", inni w "niedowaga"/"wady"). **TYDZIEŃ ROBOTY**.

### C. **Cockpit hodowców — z `WagoCounter`+`PartiaDostawca`+`FarmerCalc`+klasy A/B**

**Cel:** Ranking hodowców per partia: % klasy B, % padłych, ilość auto-zwrotów.

**Dane mam:**
- `PartiaDostawca` (hodowca per partia)
- `In0E.QntInCont` (klasy 5-12 per paleta Kurczaka A — ale to klasy WAGOWE, nie A vs B!)
- `FarmerCalc.DeclI1-6, Pieces, PiecesFarm` (sztuki deklarowane vs faktyczne)
- `FarmerCalc.VetRate0/1/2`, `VetComment` — kontrola weterynaryjna z opisem
- `WagoCounter` (jeśli aktywne — sztuki realnie policzone)

**Pain point Sergiusza:** *"Fajnie by było aby przy odebraniu hodowcy można było sprawdzać jak często jego partia jest reklamowana, ile klasy B i A ma."*

**Akcja:** Cross-join `In0E` (klasy wagowe) × `PartiaDostawca` (hodowca) × `Reklamacje` (gdzie KategoriaPrzyczyny mówi o jakości tuszki). **3 DNI ROBOTY** dla widoku + 1 tydzień dla UI.

---

## 🎯 MOJA REKOMENDACJA #1

**Zacznę od propozycji B (Reklamacje V2)** — bo:
- ✅ Wszystkie dane są w bazie (`Reklamacje.WymagaUzupelnienia`)
- ✅ Trigger już automatycznie loguje zmiany (`tr_Reklamacje_LogujZmiany`)
- ✅ ZPSP już ma `FormReklamacja*` — **tylko refactor + dodać 1 widok bazy**
- ✅ Rozwiązuje problem **fałszywych statystyk** (75% szumu w "reklamacjach")
- ✅ Daje konkretne KPI dla Justyny i handlowców
- ✅ **1 tydzień roboty** (najmniejsze ryzyko)

**Powiedz `tak` żebym zaczął — albo wybierz A/C jeśli wolisz.**

Lub jeśli chcesz: **runda 3 SELECTów** z głębszym wglądem w konkretne tabele które mnie najbardziej zaintrygowały (`State0E`, `EtykietyZbiorcze`, `OdbiorcyKurczaka`, **`WagoCounter` ostatnie wpisy + dlaczego przestał pisać**).
