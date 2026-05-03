# 13 — Bazy danych (4 instancje SQL Server)

## Mapa baz

| DB | Server | Konto | Wersja SQL Server | Kto używa |
|---|---|---|---|---|
| **HANDEL** | 192.168.0.112 | sa | SQL 2017+ | Sage Symfonia, ZPSP |
| **LibraNet** | 192.168.0.109 | pronova/pronova | **SQL 2008 R2** (lub starszy) | ProNova, ZPSP |
| **TransportPL** | 192.168.0.109 | pronova/pronova | SQL 2008 R2 | ZPSP transport |
| **UNISYSTEM** | 192.168.0.23\SQLEXPRESS | (SSPI?) | SQL Express | UNICARD RCP, ZPSP HR |

**KRYTYCZNE dla LibraNet:** SQL Server starszy → **brak `TRY_CONVERT`**. Używać `CONVERT(varchar(10), DATEADD(...), 120)`.

---

## HANDEL (Symfonia) — schemat główny

**Server:** 192.168.0.112
**Connection:** `Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True`

### Magazyny (kategorie)

| Symbol | Nazwa | Typ dokumentów |
|---|---|---|
| **65554** | Świeże po uboju | sPWU, PWP, RWP, sPZ |
| **65556** | Wydania | sWZ, sWZ-W, sWZK |
| **65552** | Drugi magazyn produkcji | różne |
| **65547** | Paczkowane | sPPK |
| **65562** | Mrożonki / półprodukty | sPPM |
| **65559** | Pomocniczy | różne |
| **65883** | Pasze (kategoria) | tona, dostawcy: TASOMIX/De Heus/Ekoplon |

### Najważniejsze tabele (Sage Symfonia)

| Tabela | Co | Klucze |
|---|---|---|
| `STContractors` | Kontrahenci (klienci + dostawcy) | `Id` |
| `STPostOfficeAddresses` | Adresy kontrahentów | `OwnerID` → STContractors |
| `HM.MG` | Pozycje magazynu (dokumenty) | `kod` (= NumerWZ czasem) |
| `HM.MZ` | Linie pozycji magazynu | `MGID` → HM.MG |
| `HM.MZ.ProductionLineID` | **PUSTE** — moduł produkcji nigdy nie wdrożony |
| `MF.Production*` (87 tabel) | **PUSTE** — moduł nigdy nie wdrożony |
| `DP` | Dokumenty produkcji | `kosztAproksymowany` **niewiarygodny** (czasem = ilosc, czasem 0) |

**Series dokumentów:**
- **sPZ** — przyjęcie (zakup żywca, paszy)
- **sPWU** — przyjęcie produkcji ubojowej (świeże)
- **PWP** — produkcja wewnętrzna przyjęcie
- **RWP** — rozchód wewnętrzny produkcyjny (do krojenia)
- **sWZ** — wydanie zewnętrzne (do klienta)
- **FVS** — faktura sprzedaży
- **FKS** — faktura korygująca sprzedaży
- **FKSB** — faktura korygująca sprzedaży B
- **FWK** — faktura wewnętrzna korygująca

---

## LibraNet — schemat główny

**Server:** 192.168.0.109
**Connection:** `Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True`
**SQL Server:** **starszy (2008 R2 lub wcześniej)** — bez `TRY_CONVERT`!

### Najważniejsze tabele

| Tabela | Co |
|---|---|
| **`In0E`** | **Rdzeń ważeń przychodu produkcji** (każdy rekord = 1 fizyczne ważenie) — patrz `18_Analiza_przychodu_szczegoly.md` |
| `Out1A` | **NIE używać** — historyczna tabela sprzedaży, sprzedaż jest w Symfonia 112 |
| `Article` | Słownik towarów (`ID = '40'` = Kurczak Klasy A — jedyny z aktywną klasyfikacją wielkości) |
| `listapartii` | **Lista partii ubojowych** (klucz: `Partia`) |
| `PartiaDostawca` | Powiązanie partia ↔ dostawca |
| `PartiaAuditLog` | Log zmian partii (V2) |
| `PartiaStatus` | Historia statusów V2 |
| `Out1A` | Wyjścia z linii 1A (krojenie) |
| `In0E` | Wejścia 0E (mrożenie) |
| `FarmerCalc` | Rozliczenia z hodowcami |
| `Haccp` | HACCP — pomiary temperatur, CCP |
| `QC*` (różne) | Quality Control — temperatury, oceny wad |
| `QC_Normy` | Konfigurowalne normy QC (V2) |
| `HarmonogramDostaw` | Plan dostaw żywca od hodowców |
| `Wazenia` | Ważenia (RADWAG?) |
| `Article` | Kartoteka towarów |
| `ArticleAuditLog` | Log zmian kartoteki |
| `ArticleFavorites` | Ulubione towary użytkownika |
| `ZamowieniaMieso` | **Zamówienia mięsa od klientów** (klucz `Id`) |
| `Pozyskiwanie_Hodowcy` | CRM hodowców (1874 leadów z Excela) |
| `Pozyskiwanie_Aktywnosci` | Aktywności CRM |
| `Driver` | Kierowcy (rozszerzone w `DriverDetails`) |
| `CarTrailer` | Pojazdy (rozszerzone w `VehicleDetails`) |
| `DriverDetails`, `VehicleDetails` | Rozszerzenia 1:1 |
| `DriverVehicleAssignment` | Przypisania w czasie |
| `VehicleServiceLog` | Serwisy pojazdów |

### Numer partii — formuła (poprawiona)

**Pełna partia w nomenklaturze firmowej:**
```
[CustomerID hodowcy: 3 cyfry] + [Partia: 8 cyfr]
                                  RR  DDD AAA
                                  └── rok (2 cyfry)
                                      └── dzień w roku (3 cyfry, 001-366)
                                          └── numer auta od tego hodowcy w danym dniu (3 cyfry)
```

**Przykład:** `390-26119004`:
- CustomerID 390 = Szymczak Dariusz
- 26 = rok 2026
- 119 = 119. dzień roku = 29 kwietnia
- 004 = 4. auto od tego hodowcy w tym dniu

**Tabele:**
- `LibraNet.dbo.PartiaDostawca` — mapowanie partia↔hodowca (`CustomerID`, `CustomerName`, `Partia`)
- `LibraNet.dbo.In0E.P1` — numer partii (TYLKO 8 cyfr, bez `CustomerID`!) — JOIN niezbędny
- `LibraNet.dbo.listapartii.Partia` — analogicznie

**Mix partii:** gdy mięso z 2 transportów się miesza → tworzona NOWA partia (ten sam numer auta, ale inny `CustomerID` z przodu).

Pełen dekoder + queries SQL: zobacz `BAZA_WIEDZY/18_Analiza_przychodu_szczegoly.md` §7.

### Kolumny `listapartii` (kluczowe)

```sql
listapartii (LibraNet):
  Partia varchar(20) PK     -- numer partii (jak wyżej)
  CustomerID int            -- ID dostawcy (hodowca)
  CustomerName varchar      -- nazwa hodowcy
  CreateData date           -- data uboju
  CreateGodzina time        -- godzina uboju
  CloseGodzina time         -- godzina zamknięcia partii
  DirID varchar(10)         -- dział ('1A', '0E', '0K')
  IsClose bit               -- 1 = zamknięta
  StatusV2 varchar(30)      -- 10-stanowy lifecycle
  HarmonogramLp int         -- link do HarmonogramDostaw.Lp
  SztDekl int               -- sztuki deklarowane
  NettoSkup decimal         -- netto skupu (kg)
  WydanoKg decimal          -- ile wydano
  PrzyjetoKg decimal        -- ile przyjęto
  NaStanieKg decimal        -- aktualne saldo
  WydajnoscProc decimal     -- % wydajności
  KlasaBProc decimal        -- % klasy B
  TempRampa decimal         -- temperatura na rampie
```

---

## TransportPL — schemat

**Server:** 192.168.0.109
**Connection:** `Server=192.168.0.109;Database=TransportPL;User Id=pronova;Password=pronova;TrustServerCertificate=True`

(Zob. `09_Transport.md` dla szczegółowego schematu Kierowca / Pojazd / Kurs / Ladunek.)

### Dodatkowo

| Tabela | Co |
|---|---|
| `TransportZmiany` | Zmiany kursów wymagające akceptacji |
| `vKursWypelnienie` | VIEW — wypełnienie kursu (definicja może być niedostępna, do recreate) |

**Service:** `TransportZmianyService` — workflow akceptacji.

---

## UNISYSTEM (UNICARD RCP)

**Server:** 192.168.0.23\SQLEXPRESS
**Funkcja:** Rejestrator Czasu Pracy — godziny przyjścia/wyjścia pracowników (czytniki kart)

### Najważniejsze widoki / tabele

| Obiekt | Co |
|---|---|
| `V_RCINE_EMPLOYEES` | Widok pracowników (używany w KontrolaGodzin) |
| Tabele RCP | Rejestracje wejść/wyjść (czytniki kart) |
| Tabele kart | Karty pracownicze, przypisania |

### Połączenie z ZPSP

**Plik:** `KontrolaGodzin.xaml.cs` (~3100+ linii)
**Modele:** `PracownikModel`, `RejestracjaModel`, `NieobecnoscModel` (na dole pliku)

---

## Rozszerzenia ZPSP (HR_*)

**Tabele HR_*** w **ZPSP** (lub LibraNet — do potwierdzenia):
- `HR_Pracownicy`
- `HR_Wnioski` (urlopowe)
- `HR_Etaty`
- ... (schema w `ZPSP_ModulKadrowy_Tabele.sql`)

**Po co osobno:** UNICARD nie obsługuje pełnej dokumentacji HR (urlopy, wnioski, ankiety). ZPSP dorzuca własną warstwę.

---

## Schema-y SQL skryptów (jednorazowe)

W folderze `Partie/SQL/`:
- `CreatePartieExtras.sql` (v1)
- `CreatePartieV2.sql` (v2 — status/normy)

W folderze `Flota/SQL/`:
- `CreateFlotaTables.sql`

W folderze `KartotekaTowarow/SQL/`:
- `CreateKartotekaExtras.sql`

W folderze `Reklamacje/`:
- `Reklamacje_CreateTables.sql`
- `Reklamacje_AlterTables.sql`
- `Reklamacje_Ustawienia.sql`

---

## Pojęcia produkcyjne ↔ tabele

| Pojęcie | Tabela / kolumna | Komentarz |
|---|---|---|
| Plan dostaw dziś | `LibraNet.HarmonogramDostaw` | Link do partii via `HarmonogramLp` |
| Partia ubojowa | `LibraNet.listapartii` | Klucz: `Partia` |
| Skup (rozliczenie hodowcy) | `LibraNet.FarmerCalc` | Cena, ilość, waga |
| Ważenia | `LibraNet.Wazenia` | RADWAG? Do potwierdzenia |
| QC (temperatury, wady) | `LibraNet.QC*` | Per partia |
| HACCP | `LibraNet.Haccp` | Per partia, CCP |
| Wydanie do klienta | `Handel.HM.MG` (sWZ) | Linki do `STContractors` |
| Faktura | `Handel.HM.MG` (FVS) | + linie w `HM.MZ` |
| Korekta faktury | `Handel.HM.MG` (FKS/FKSB/FWK) | Auto-import → ZPSP reklamacje |

---

## Problemy z bazami (znane)

### LibraNet — SQL Server starszy
- **Brak `TRY_CONVERT`** → używać `CONVERT(varchar(10), DATEADD(...), 120)`
- **Brak typów `datetime2`** w niektórych tabelach
- **Brak window functions** w niektórych przypadkach (sprawdzić wersję)

### HANDEL — Symfonia legacy
- **`HM.MZ.ProductionLineID`** wszystkie wiersze NULL (40513) — moduł produkcji NIGDY nie wdrożony
- **`MF.Production*`** 87 tabel — wszystkie puste
- **`DP.kosztAproksymowany`** niewiarygodny (czasem = `ilosc`, czasem 0)

### Cross-DB
- **`NumerWZ` vs `HM.MG.kod`** — czasem niespójne (data quality)

---

## Pomysły reorganizacji baz (do rozmowy)

1. **Migracja LibraNet do SQL Server 2017+** — odzyska `TRY_CONVERT`, JSON support, etc.
2. **Konsolidacja TransportPL → LibraNet** — i tak na tym samym serwerze
3. **Symfonia Production module** — albo wdrożyć (kupiony!) albo przenieść jego rolę całkowicie do ZPSP
4. **Connection strings → konfig** (`appsettings.json`) zamiast hardcoded — testowalność, środowisko dev/prod
