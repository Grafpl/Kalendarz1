# Analiza wyników rundy 1 (EKSPLORACJA_LIBRANET_FULL.sql)

**Data:** 2026-05-03
**Plik źródłowy:** `WYNIKI_RAW.txt` (9115 linii)
**Status:** Plik uruchomiony pomyślnie. 2 błędy: brak tabel `SzablonyZamowien` i `KartotekaPrzypomnienia` (mimo że ZPSP ma do nich odwołania w kodzie).

---

## 1. Środowisko

| Co | Wartość |
|---|---|
| Wersja SQL | **SQL Server 2022 RTM Developer Edition** (16.0.1000.6) |
| Collation | Polish_CI_AS |
| Nazwa fizyczna bazy | **`PiorkowscyLibraNet`** (NIE `LibraNet`!) |
| Rozmiar danych | 2776 MB (2.7 GB) |
| Rozmiar logów | 240 MB |
| Inne bazy na 109 | LibraNet, **TransportPL** (wcześniej myślałem że jest, potwierdzone) |

**Implikacje:**
- Wszystkie nowoczesne funkcje dostępne: `TRY_CONVERT`, `STRING_AGG`, JSON, window functions, `DATEFROMPARTS`. Stare przekonanie "to starszy SQL bez TRY_CONVERT" — **NIEPRAWDA, można pisać czysto**.
- Nazwa fizyczna `PiorkowscyLibraNet` ≠ logiczna `LibraNet` — alias jest, ale w niektórych miejscach kodu może się gdzieś pojawić pełna nazwa.

---

## 2. Skala bazy

| Wskaźnik | Wartość |
|---|---|
| Tabele | **293** |
| Widoki | 48 |
| Procedury | 70 |
| Funkcje | 3 |
| Triggery | 6 (z czego 4 aktywne, 2 wyłączone) |
| Foreign keys | 59 |

**Top tabele po liczbie wierszy:**
| Tabela | Wiersze | MB |
|---|---|---|
| **`In0E`** | **2 108 520** | 545 |
| **`Out1A`** | **2 005 205** | 547 |
| `Aktywnosc` | 185 004 | 5.7 |
| `State0E` | 101 668 | 35.8 |
| `listapartii` | 37 795 | 9.3 |
| `PartiaDostawca` | 37 750 | 7.5 |
| `EtykietyZbiorcze` | 36 365 | 8.4 |
| `Haccp` | 22 717 | 3.3 |
| `OdbiorcyCRM` | 20 399 | 13.5 |
| `TymczasowiOdbiorcy` | 20 378 | 9.4 |
| `WagoCounter` | **8 168** | 0.5 |
| `Pozyskiwanie_Hodowcy` | 1 874 | 0.7 |
| `kontrahenci` | 2 633 | 0.8 |

**Implikacje:**
- **`Out1A` jednak ma 2M wierszy** — Sergiusz wcześniej mówił "nie używamy", ale dane są. Trzeba zbadać aktywność (kiedy ostatni INSERT, czy żywa).
- **`WagoCounter` istnieje (8 168 wierszy)** — czyli system Wago JEDNAK pisze do bazy ZPSP! Sergiusz mówił że nie ma API. **Wymaga osobnego SELECT-u w rundzie 2.**
- **3 tabele klientów: `OdbiorcyCRM` (20 399) + `TymczasowiOdbiorcy` (20 378) + `kontrahenci` (2 633)** — dlaczego trzy? Trzeba zbadać.
- `EtykietyZbiorcze` (36k) — co to dokładnie? Etykiety produktów?

---

## 3. Najważniejsze tabele — kluczowe odkrycia

### `listapartii` (19 kolumn)
```
GUID, DIR_ID, Partia, GrupaTowarowa, ArticleID,
CreateData (varchar 10!), CreateGodzina (varchar 8!), CreateOperator (varchar 6!),
ModificationData, ModificationGodzina, CloseData, CloseGodzina, CloseOperator,
IsClose smallint, CalcMethod, CalcData, CalcGodzina,
StatusV2 varchar(30) DEFAULT 'IN_PRODUCTION', HarmonogramLp int
```

**KLUCZOWE:** Daty/godziny w `listapartii` to **`varchar(10)`/`varchar(8)`**, nie native `date`/`time`! Łączenie wymaga konwersji.

**`StatusV2` ma DEFAULT 'IN_PRODUCTION'** — wszystkie nowe partie startują od razu jako "w produkcji" (statusy `PLANNED`, `IN_TRANSIT`, `AT_RAMP`, `VET_CHECK`, `APPROVED` praktycznie nie używane).

### `In0E` (37 kolumn — duża tabela ważeń)

Pełna struktura zawiera m.in.:
- `CustomerID` varchar(10) — klient bezpośrednio
- `DocNo` int, `OrderNo` varchar(20) — link do dokumentu
- `TermID` int, `TermType` varchar(3) — terminal wagi
- `TruckID` int — ID auta
- **`Weight` float** (NIE decimal!), `ActWeight` float, `Tara` float, `Price` float, `PriceZakupu` float
- `P1`, `P2` varchar(15) — partie (P2 zawsze = P1 w 100%, do usunięcia)
- `OperatorID` + **`Operator2ID`** + `Wagowy` + **`Wagowy2`** — DWÓCH operatorów na jednym ważeniu
- `OverWeight` varchar(1) — flaga
- `Temp` varchar(2) — temperatura?
- `Swiadectwo` varchar(10) — świadectwo wet.
- `ZamiastID` — substytut?
- **`ActWeight`** — waga rzeczywista (po tarze)
- **`QntInCont`** int — klasa wagowa (5-12 dla Kurczaka A)

**Zakres:** 5 lat danych — od **2021-03-08 do 2026-04-29**.

**Dziwne wartości `QntInCont`:**
- Klasa `-1` (10 ważeń, 2142 kg) — STORNO?
- Klasa `-6` (1 ważenie, 0 kg)
- Klasa `-10` (1 ważenie, 0 kg)
- Klasa `1` (1 ważenie, 2.4 kg) — błąd?

**Standardowe klasy w 30 dniach (Kurczak A):**
- 5: 295 ważeń, 155 771 kg (bardzo duże ptaki)
- **6: 1503 ważeń, 811 142 kg** ← dominuje (idealna)
- 7: 853 ważeń, 457 142 kg ← druga idealna
- 8: 413 ważeń, 221 601 kg
- 9: 142 ważeń, 74 513 kg
- 10: 76 ważeń, 36 268 kg
- 11: 11 ważeń, 5092 kg
- 12: 0 (nikt nie ubił małych w 30 dni)

### `Article` (38 kolumn — bardzo bogata!)

Kluczowe kolumny które zmieniają wszystko:

```
isStandard smallint
StandardWeight numeric -- waga standardowa per towar
StandardTol numeric    -- tolerancja DODATNIA per towar
StandardTolMinus numeric -- tolerancja UJEMNA per towar
NameLine1 varchar(40), NameLine2 varchar(40) -- nazwa wieloliniowa
Cena1, Cena2, Cena3 float -- 3 ceny (świeży/mrożony/korekta?)
Wydajnosc numeric  -- współczynnik wydajności produkcji
Przelicznik float
Ingredients1-8 varchar(80) -- składniki (etykiety dla klienta)
TempOfStorage varchar(20)
Halt smallint
Duration int
WRC float
RELATED_ID1, ID2, ID3 varchar(10) -- zestawy?
```

**Kurczak A (ID=40):**
- StandardWeight: **15.0** kg (paleta!)
- **StandardTol: 0.31** kg (tolerancja +)
- **StandardTolMinus: 0.14** kg (tolerancja -)
- Cena1: 8.5 PLN/kg
- TempOfStorage: "od -0°C do +4°C"
- Duration: 7 (dni?)
- Przelicznik: 1

**ŻYCZENIE z runda 1 — moduł Analiza Przychodu używa hardcoded `0.05 kg` tolerancji** — w tym czasie `Article.StandardTol` PER TOWAR jest dostępne! TODO #4 z `18_Analiza_przychodu_szczegoly.md` rozwiązany — kolumny istnieją.

**Liczba towarów: TYLKO 36** (mała kartoteka).

### `PartiaDostawca` (8 kolumn, prosta)
```
guid uniqueidentifier, Partia varchar(8), CustomerID varchar(10), CustomerName varchar(40),
CreateData/Godzina varchar, ModificationData/Godzina varchar
```

**Dekoder partii zweryfikowany:** sample z 2026-04-29 (dzień 119):
- `26119001`, `26119003`, `26119004`, `26119005`, `26119009-15`
- Numer auta `001-015` jest **GLOBALNY w danym dniu** (wszyscy hodowcy razem) — NIE per hodowca, jak wcześniej zakładałem!

### `HarmonogramDostaw` (47 kolumn)

Bardzo bogata tabela planowania:
- Lp PK, DostawcaID, DataOdbioru date
- `TypUmowy` (Kontrakt/Wolnyrynek)
- `TypCeny` (rolnicza/ministerialna/wolnyrynek/Łączona)
- `PaszaPisklak` — pasza wstawiana
- **Workflow potwierdzeń:** `Utworzone`, `Wysłane`, `Otrzymane`, `PotwWaga`+kto+kiedy, `PotwSztuki`+kto+kiedy, `PotwCena`
- `Kurnik`, `KmK`, `KmH` (km do klienta vs hodowcy?)
- `Ubytek`, `Posrednik`, `Ubiorka`
- DataUtw + KtoStwo + DataMod + KtoMod + KtoUtw + KiedyUtw + ...

**Sample harmonogramu** — dziwactwo: planowane dostawy są nawet z roku 2027 i 2141 (błąd, "Do wykupienia") — `Hinz Dariusz` ma planowane dostawy aż na 2027-06-15. Czyli długoterminowe kontrakty są w bazie.

### `FarmerCalc` (103 kolumny — gigantyczna)

To pełen workflow odbioru żywca z fermy:
- Numer/YearNumber/CarLp — identyfikacja
- Status, CustomerGID, CustomerRealGID (różny od regularnego!), AddressGID, PriceTypeID
- **DWIE WAGI:** `FullWeight`/`EmptyWeight`/`NettoWeight` (firmowa) + `FullFarmWeight`/`EmptyFarmWeight`/`NettoFarmWeight` (na fermie)
- Pieces/PiecesFarm — sztuki
- **Czasy operacyjne:** `PoczatekUslugi`, `Wyjazd`, `DojazdHodowca`, `Zaladunek`, `ZaladunekKoniec`, `WyjazdHodowca`, `Przyjazd`, `KoniecUslugi`
- StartKM, StopKM, DistanceKM
- **Kontrola wet.:** `VetMedDate`, `VetNo`, `VetRate0/1/2`, `VetDate`, `VetUser`, `VetComment` (512 znaków)
- DeclI1-6 (sześć poziomów deklaracji?)
- **Zdjęcia:** `ScanPath`, `ZdjecieTaraPath`, `ZdjecieBruttoPath`
- `PartiaGuid` uniqueidentifier vs `PartiaNumber` nvarchar(50) — różne formaty!
- `NrDokArimr`, `Przybycie`, `PadnieciaIRZ` — IRZplus
- `SymfoniaDocNr`, `SymfoniaExportDate`, `SymfoniaNrFV`, `SymfoniaIdFV` — link do Symfonii

**Sample 2026-01-09:** 10 dostaw w 1 dniu (CarLp 1-10), każda ~5000 sztuk × 2.25 kg = ~11 000 kg netto. Czas trasy: ~6.5h od wyjazdu do końca usługi. Distance ~115 km / 77 km / 489 km (różne kierunki).

### `WstawieniaKurczakow` (27 kolumn)

Rejestr piskląt:
- `DataWstawienia` + `IloscWstawienia` (np. 87000 piskląt!)
- `DataUbiorki` + `IloscUbiorki` (35 dzień, ~20% sztuk, np. 10 800)
- `DataPelne` + `IloscPelne` (42 dzień, reszta, np. 76 200)
- `PaszaPisklak` Tak/Nie
- `TypUmowy` Kontrakt
- `TypCeny` (Łączona/Ministerialna)
- `isCheck` + `CheckCom` — sprawdzenie
- `isConf` + `DataConf` + `KtoConf` — confirmation
- **`CzasTworzeniaSek`** — czas tworzenia rekordu w sekundach!

**Skala wstawień:** 20 000 - 146 000 piskląt naraz na 1 dostawcę.

### `ZamowieniaMieso` (54 kolumny)

Kluczowe dla sprzedaży:
- `LiczbaPojemnikow`, `LiczbaPalet decimal`, `TrybE2 bit`
- `TransportKursID bigint`, `TransportStatus`
- 5 dat: `DataZamowienia`, `DataProdukcji`, `DataUboju`, `DataPrzyjazdu` (awizacja!), `DataWydania`
- `CzyZrealizowane`, `CzyWydane`, `CzyZafakturowane`, `CzyWszystkoWydane`, `CzyCzesciowoZrealizowane`
- `DataAkceptacjiMagazyn`, `DataAkceptacjiProdukcja` — workflow akceptacji
- `ProcentRealizacji decimal` — KPI
- `Strefa bit` (mała/duża strefa?)
- `NumerWZ`, `DataWystawieniaWZ` — bezpośrednio w zamówieniu
- `NumerFaktury`, `CzyZafakturowane`
- `AnulowanePrzez`, `DataAnulowania`, `PrzyczynaAnulowania`
- `SourceZamowienieId` + `CyklGroupId` — zamówienia cykliczne
- `ZatwierdzoneMrozonki`, `ZatwierdzonePrzez`, `ZatwierdzoneData` — osobny workflow

**Statusy 90 dni:** Wydany 1484 (80%), Anulowane 216 (12%), Nowe 92 (5%), Zrealizowane 69 (4%) — anulacja co 7-me zamówienie!

**TransportStatus 90 dni:** Oczekuje 886, Przypisany 550, **Wlasny 425** (klient swoim transportem 23% wszystkich!)

### Kartoteka Odbiorcy (CRM)

`KartotekaOdbiorcyDane` (22 kolumny) ma:
- **`Asortyment`** (500 znaków) — co klient kupuje
- **`PreferencjePakowania, PreferencjeJakosci, PreferencjeDostawy`**
- **`PreferowanyDzienDostawy, PreferowanaGodzinaDostawy`** — np. "poniedzialek-piatek 06:00-08:00"
- **`KategoriaHandlowca char(1)`** = A/B/C/D
- **`Latitude, Longitude, GeokodowanieData, GeokodowanieStatus`** — GPS klientów
- `Notatki` (max) — np. "Od ostatnich 6 miesięcy kontakt się pogorszył przez zaniżone ceny ze strony Bimexu"

`KartotekaScoring` (12 kolumn):
- `TerminowoscPkt, HistoriaPkt, RegularnoscPkt, TrendPkt, LimitPkt`
- `ScoreTotal int`, `Kategoria nvarchar(20)`
- **`RekomendacjaLimitu decimal`** — auto-rekomendacja
- **`RekomendacjaOpis nvarchar(500)`** — uzasadnienie

**Skala:** 63 wpisów `KartotekaOdbiorcyDane` — bardzo mało (z 20k klientów w `OdbiorcyCRM`!)

---

## 4. Operatorzy ważenia (30 dni)

| OperatorID | Imię | Ważenia | Storno | Palety A | Porcje |
|---|---|---|---|---|---|
| 0101 | **NEPAL** | 26 602 | 174 | 2 | 26 600 |
| 8822 | **SUMAN** | 14 031 | 41 | 0 | 14 031 |
| 4433 | **Zuzanna Garnys** | 3 304 | 2 | **3 304** | **0** |
| 8921 | **GOPAL** | 1 343 | 4 | 0 | 1 343 |
| 777 | Bogumila LATEK | 1 276 | 10 | 0 | 1 276 |
| 3110 | Wieslaw MICHALSKI | 820 | 3 | 0 | 820 |
| 1980 | **Justyna TERKA** | 568 | **44** (7.7%!) | 0 | 568 |
| 2121 | Teresa Jachymczak | 133 | 0 | 0 | 133 |
| 8723 | SURJA | 58 | 0 | 0 | 58 |
| 0000 | Admin | 7 | 0 | 0 | 7 |

**Wnioski:**
- **NEPAL/SUMAN/GOPAL/SURJA** = Nepalczycy (zgodnie z tym co Sergiusz mówił o agencji)
- **Zuzanna Garnys = paletystka 100%** — pełna pewność że robi tylko Kurczak A na palecie
- **NEPAL jako operator paletyzujący** — 2 wyjątki na 26 602 ważeń (przypadkowo zważył paletę?)
- **Justyna TERKA — 7.7% storno** to bardzo dużo! Możliwie kontrola jakości (anulacje błędnych ważeń?). Trzeba zbadać.
- Teresa Jachymczak (handlowiec/zakupy) waży 133 razy w 30 dni? Co tam robi?

---

## 5. Hodowcy

**Top 30 90 dni:** Ferma Sobota (552, 31 partii), Przybysz Łukasz (906, 27), Knera Aleksandra (965, 26), Stróżewski Krzysztof (996, 16).

**DRAMAT data quality:**
- **133 hodowców z PUSTYM CustomerName** mają wiele CustomerID
- "aa" = 5 ID
- "Kołaczyński Bartosz" = 3 ID
- 30+ hodowców ma 2 ID (np. ferma + brat)

---

## 6. Tolerancje empiryczne ważeń (30 dni, top towary)

| Article | Liczba ważeń | Śr. odchylenie kg | Śr. % | Max kg |
|---|---|---|---|---|
| Filet z Piersi (13) | 8783 | 0.111 | 0.77% | 0.56 |
| Ćwiartka (11) | 8762 | 0.128 | 0.93% | 0.96 |
| Korpus (16) | 8686 | 0.141 | 0.96% | 0.5 |
| Kurczak A (40) | 3004 | **2.72** | 0.52% | 11 |
| Wątroba (42) | 2986 | 0.164 | 1.71% | 0.92 |
| **Polędwiczki (47)** | 174+120 | 0.149-0.154 | **3.04% / 6.05%** | 0.8 |
| Skrzydło II (32) | 1618 | 0.082 | 0.81% | 0.18 |

**Dziwne:** Polędwiczki mają **6% odchylenia** (vs 1% dla większych) — bo to małe sztuki w pojemniku, więc precyzja gorsza. Też są DWA wpisy `Article.Name` dla polędwiczek (z polskimi znakami i bez) — dwa różne ID!

---

## 7. Statystyki zamówień

**Anulacje per dzień ostatnie 30 dni** (najwyższe):
- 2026-04-16: 38 zamówień, **7 anulowanych** (18.4%) — najgorszy dzień!
- 2026-04-24: 37 zamówień, **4 anulowane** (10.8%)
- 2026-04-15: 31 zamówień, **4 anulowane** (12.9%)
- 2026-04-20: 30 zamówień, **4 anulowane** (13.3%)
- 2026-04-07: 14 zamówień, **3 anulowane** (21.4%) — niewielka skala ale wysoki %

**Top klienci 90 dni:**
- KlientId 939: 90 zamówień, 97 534 pojemników, 2 462 palety
- KlientId 5314: 82 zamówień, **101 263 pojemników** (najwięcej kg!), 2 541 palet
- KlientId 931: 84 zamówień, 53 814 pojemników
- KlientId 4910: 69 zamówień (Marek Kłapot, kategoria A)

---

## 8. Co zaskoczyło i wymaga rundy 2

| # | Zagadka | SELECT do napisania |
|---|---|---|
| 1 | **`WagoCounter` 8168 wierszy** — Wago JEST w bazie! | Struktura + sample 30 + zakres dat |
| 2 | **`Out1A` 2M wierszy** — czy nadal żywa? | Min/max Data, ostatnie wpisy |
| 3 | **`State0E` 101k** — co to? | Struktura + sample |
| 4 | **`Aktywnosc` 185k** — co to? | Struktura + sample |
| 5 | **`EtykietyZbiorcze` 36k** — etykiety produktów? | Struktura + sample |
| 6 | **3 tabele klientów** (`OdbiorcyCRM`, `TymczasowiOdbiorcy`, `kontrahenci`) — po co 3? | Struktury wszystkich |
| 7 | **Klasy ujemne** (-1, -10, -6) — STORNO klas? | Sample wierszy z `QntInCont < 0` |
| 8 | **`Operator2ID`** — kiedy używany? | COUNT gdzie != NULL i sample |
| 9 | **`Reklamacje` 621 + 5 powiązanych** — workflow? | Struktury wszystkich + statystyki |
| 10 | **vw_QC_*** — QC widoki — definicje + sample | Definicje + 5 wierszy każdy |
| 11 | **'Dane hodowcw$'** 415 wierszy ze znakami specjalnymi — co to? | Struktura + sample |
| 12 | **HR_/KG_*** moduły — wszystkie tabele | Lista + struktura kluczowych |
| 13 | **`Notatki` ekosystem (5 tabel)** — po co? | Struktury + relacje |
| 14 | **`ChatMessages` + ChatTypingStatus** — jest chat w ZPSP? | Struktura + zliczenia |
| 15 | **`FirefliesTranskrypcje` 102 wpisów** — transkrypcje są w bazie | Struktura (BEZ treści transkrypcji) |
| 16 | **`intel_Articles` + `intel_Prices`** — co to? | Struktura + sample |
| 17 | **In0E.CustomerID** dla każdego ważenia — dlaczego niektóre = 0? | Distribution + sample |
| 18 | **Storno Justyny TERKA 7.7%** — co konkretnie? | Sample 20 storno z jej OperatorID |
| 19 | **TermID K1 vs K2** — różnica? | Per termiID liczba operatorów + co waży |
| 20 | **DOSTAWCY (870) vs Dostawcy** — wielkie i małe litery! | Compare struktur i sample |

---

## 9. Procedury i widoki które już istnieją (czyli nie pisać od zera!)

**Procedury z konkretną wartością biznesową:**
- `sp_GetDashboardKPIs(@Data)` — KPI dla dashboarda już w bazie
- `sp_PobierzPlanTygodniowy(@DataOd, @DataDo)`
- `sp_GetZamowieniaNaDzien(@DataPrzyjazdu, @Status, @PageNumber, @PageSize)`
- `sp_GetPodsumowanieTowarowNaDzien(@Data)`
- `sp_GetStatystykiAnulowanych(@DataOd, @DataDo)`
- `sp_PobierzRankingHandlowcow`
- `sp_StatystykiReklamacji(@DataOd, @DataDo)`
- `sp_BatchUpdateZamowieniaStatus(@IdsJson, @NowyStatus, @Uzytkownik)`
- `sp_LogujZmianeZamowienia(...)`
- `sp_PobierzHistorieZamowienia(@ZamowienieId)`
- `sp_PobierzOstatnieZmiany(@IloscDni, @LimitWierszy)`
- `sp_PobierzSaldoKontrahenta(@KontrahentId, @DataOd, @DataDo)`
- `sp_PobierzKonfiguracjeNaDzien(@Data)`
- `sp_GenerujNumerOferty (OUTPUT)` + `sp_ZapiszOferte` + `sp_ZmienStatusOferty`
- `sp_StatystykiEksportu(@DataOd, @DataDo)`
- `sp_ResetEksportuSymfonia(@CalcDate, @CarLp)`
- `sp_AuditLog_Insert/GetByLP/GetByUser/GetRecent/Statistics` — pełen audit
- `sp_OznaczNotyfikacjePrzeczytane`, `sp_PobierzNieprzeczytaneNotyfikacje`, `sp_UtworzPrzypomnienia`
- `sp_SaveOrderSnapshot(@ZamowienieId, @TypSnapshotu)` — snapshot Realizacja/Wydanie
- `sp_PobierzOstatniaAktywnosc(@DniBezAktywnosci=90)` — kontrahenci uśpieni!

**Widoki kluczowe:**
- `vw_QC_Podsum`, `vw_QC_TempSummary`, `vw_QC_WadySkale` — QC summary
- `vw_OdpadyDzienne` — dzienne odpady
- `vw_ReklamacjePelneInfo` — reklamacje pełne
- `vw_DostawcyBezSymfonii` — dostawcy bez kontrahenta w Symfonii
- `vw_PdfHistoryWithDetails` — PDF history
- `vw_NadchodzaceSpotkania`, `vw_SpotkaniaKalendarz` — spotkania
- `vw_OfertyLista`, `vw_OfertyStatystyki`, `vw_OfertyTopKlienci` — oferty
- `vw_OperatorzyPelne` — operatorzy
- `V_KG_AlertyNieprzeczytane`, `V_KG_NadgodzinyAktywne`, `V_KG_SpoznieniaMiesiac` — HR alerty
- `V_HR_AlertyNieodczytane`, `V_HR_BilansPodsumowanie`, `V_HR_UrlopydoZatwierdzenia`
- `vTTripFill`, `vTTripLoadSummary`, `vTTripSpaceFill`, `vw_TransportTripWithOrders` — transport
- `vw_AktywnoscUzytkownikow`, `vw_AuditLog_Czytelny`
- `VW_FarmerCalcRecentChanges`, `vw_SpecyfikacjeDoEksportu`
- `vw_PodsumowanieSaldOpakowan`, `vw_SaldaOpakowaniKontrahentow`, `vw_StatusHistoriiSald` — opakowania!

---

## 10. Co zrobić teraz (priorytety dla rundy 2)

1. **WagoCounter** — odpalić struktury + sample + analiza, czy kompatybilne z Wago selektywną
2. **Reklamacje** — kompletna struktura wszystkich 7 tabel (`Reklamacje`, `ReklamacjeTowary`, `ReklamacjeKomentarze`, `ReklamacjeZalaczniki`, `ReklamacjeZdjecia`, `ReklamacjeUstawienia`, `ReklamacjeHistoria`)
3. **vw_QC_* widoki** — definicje + sample
4. **3 tabele klientów** — porównanie + dlaczego są 3
5. **HR_/KG_* moduły** — lista wszystkich tabel + kluczowe struktury
6. **`Article.StandardTol/StandardTolMinus`** — wykorzystaj w module Analiza Przychodu zamiast hardcoded 0.05 kg
7. **Out1A** — czy żywa? sample najnowszych wpisów

→ **Stworzyć plik `EKSPLORACJA_LIBRANET_2.sql`** (mega plik #2 z 60+ SELECT-ami).
