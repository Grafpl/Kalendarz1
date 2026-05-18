# 13 — Bazy danych (4 instancje SQL Server)

## Mapa baz

| DB | Server | Konto | Wersja SQL Server | Kto używa |
|---|---|---|---|---|
| **HANDEL** | 192.168.0.112 | sa | SQL 2017+ | Sage Symfonia, ZPSP |
| **LibraNet** | 192.168.0.109 | pronova/pronova | **SQL Server 2022** (potwierdzone 2026-05-12 — `STRING_AGG`, `STRING_SPLIT`, window functions działają) | ProNova, ZPSP |
| **TransportPL** | 192.168.0.109 | pronova/pronova | SQL 2022 | ZPSP transport |
| **UNISYSTEM** | 192.168.0.23\SQLEXPRESS | (SSPI?) | SQL Express | UNICARD RCP, ZPSP HR |

**Aktualizacja 2026-05-12:** LibraNet jest na SQL Server 2022 (nie 2008 R2 jak wcześniej myśleliśmy). `TRY_CONVERT`, `TRY_CAST`, `STRING_AGG`, window functions wszystkie działają. **JEDNAK** kod ZPSP nadal pisany defensywnie z myślą o starszym SQL (CONVERT zamiast TRY_CONVERT, string daty).

---

## HANDEL (Symfonia) — schemat główny

**Server:** 192.168.0.112
**Connection:** `Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True`

### Magazyny (zaktualizowane 2026-05-09 — real names z Symfonii)

> ⚠️ **Wcześniejsze mapowanie było mylne**. Aktualne, zweryfikowane przez parsowanie sufiksów MM+/MM- — patrz `24_Magazyny_i_Lancuch_Produkcji.md` dla pełnego opisu.

| ID | Skrót Symfonii | Nazwa | Główne serie |
|---:|---|---|---|
| **65555** | M. UBOJ | Magazyn ubojni (tuszki/podroby) | sPWU |
| **65554** | M. PROD | Magazyn produkcji / krojenia | sPWP, sRWP, sMM- |
| **65556** | M. DYST | Magazyn dystrybucji (sprzedaż) | sWZ, sMM+ |
| **65552** | M. MROŹ | Mroźnia | sMM+, sMM- |
| **65562** | M. MASAR | Masarnia | sPPM, sRPM |
| **65547** | KARMA | Magazyn produkcji karmy | sPPK, sRPK |
| **65551** | M. ODPA | Magazyn odpadów | sMM+ |
| **65564** | M. ROZCH | Magazyn rozchodu (buffer) | sMM+, sRWP |
| **65559** | Mag. opak. | Magazyn opakowań | sMW, sMP |
| **65550** | Mag. faktur | Magazyn faktur (sPZ od hodowców) | sPZ, sWZ-W |
| **65543** | Mag. 65543 | TASOMIX-specific (paszy) | sPZ |
| **65566** | Mag. 65566 | Samol/Ekoplon | sPZ |
| 65882 | (kategoria) | Żywiec — kat. towarów (NIE magazyn) | TW.katalog |
| 65883 | (kategoria) | Pasze — kat. towarów (NIE magazyn) | TW.katalog |

**KLUCZOWE**: Sage Symfonia **NIE TRZYMA nazw magazynów w bazie HANDEL** — są w UI/konfiguracji.
Real nazwy zostały wyekstrahowane z sufiksów `kod` w MM+/MM- (np. `"0001/22/MM-/M. PROD"` → magazyn=65554 = M. PROD).
Implementacja: `MagazynyHelper.LoadFromDatabaseAsync()` w `AnalitykaPelna/Services/`.

### Najważniejsze tabele (Sage Symfonia)

| Tabela | Co | Klucze |
|---|---|---|
| `STContractors` | Kontrahenci (klienci + dostawcy) | `Id` |
| `STPostOfficeAddresses` | Adresy kontrahentów | `OwnerID` → STContractors |
| `SSCommon.ContractorClassification` | **Klasyfikacja kontrahenta — wymiary** (Handlowiec, Kilometry, Blokada powiadomień). **3 INSTEAD OF triggery** (TH_IOI/IOU/AD)! Aby zmienić handlowca: SET zarówno `CDim_Handlowiec` (int FK) jak i `CDim_Handlowiec_Val` (denormalizowana wartość). Inaczej trigger nadpisze `_Val` na NULL. Pełen opis w `26_Modul_Zamowien_v2.md`. |
| `HM.MG` | Pozycje magazynu (dokumenty) | `kod` (= NumerWZ czasem) |
| `HM.MZ` | Linie pozycji magazynu | `MGID` → HM.MG |
| `HM.MZ.ProductionLineID` | **PUSTE** — moduł produkcji nigdy nie wdrożony |
| `MF.Production*` (87 tabel) | **PUSTE** — moduł nigdy nie wdrożony |
| `DP` | Dokumenty produkcji | `kosztAproksymowany` **niewiarygodny** (czasem = ilosc, czasem 0) |
| `HM.DK` | Header dokumentu handlowego (faktura/PZ/WZ) | `id` PK, `kod` = numer faktury (np. `3191/26/FVS`). Kolumny VAT: `netto`, `vat`, `walNetto`, **`walBrutto`** (kwota brutto całego dokumentu). |
| `HM.DP` | Pozycje dokumentu handlowego | `super` → HM.DK.id. **Kolumny VAT:** `cena` (netto/szt), `wartNetto`, `wartVat` (kwota VAT), `wartstvat` (stawka × 100, np. **800 = 8%**, **2300 = 23%**, **500 = 5%**), **`walBrutto`** (brutto pozycji = wartNetto + wartVat), `stvat` (FK do słownika stawek, rzadko używane). Pełen sample → `SELECTY/EKSPLORACJA_VAT_FAKTURY.sql`. |
| `HM.TW` | Towary | `id` PK, `vatsp` (FK stawka sprzedaży), `vatzk` (FK stawka zakupu). Na pozycji DP używa się `wartstvat` zamiast TW.vatsp (można zmienić na pozycji). |

**Series dokumentów:**
- **sPZ** — przyjęcie (zakup żywca, paszy)
- **sPWU** — przyjęcie produkcji ubojowej (świeże, magazyn 65555)
- **sPWP / PWP** — przychód wewn. produkcja (elementy po krojeniu, mag. 65554)
- **sRWP / RWP** — rozchód wewnętrzny produkcyjny (do krojenia)
- **sRWU** — rozchód wewn. ubój (żywiec na ubój)
- **sWZ** — wydanie zewnętrzne (do klienta, z 65556)
- **sMM- / MM-** — przesunięcie międzymagazynowe rozchód (`khdzial` = cel!)
- **sMM+ / MM+** — przesunięcie międzymagazynowe przychód
- **sPPM** — przychód masarnia (mag. 65562)
- **sPPK** — przychód karma (mag. 65547)
- **sPKM** — korekta magazynowa
- **sMW** — wydanie z magazynu opakowań (mag. 65559)
- **FVS** — faktura sprzedaży
- **FKS** — faktura korygująca sprzedaży
- **FKSB** — faktura korygująca sprzedaży B
- **FWK** — faktura wewnętrzna korygująca

> Pełna mapa wszystkich serii (z opisami, magazynami, rolą w łańcuchu) → `23_HANDEL_Schema_Sage_Symfonia.md` sekcja 3 + `SeriaSymfoniaHelper` w kodzie.

### 🚨 Krytyczne gotchas SQL na HANDEL (czytaj PRZED pisaniem zapytań!)

1. **`MG.anulowany = 0` ZAWSZE** — Sage zostawia anulowane dokumenty
2. **`HM.MZ.kod` może się duplikować** — wielolinijne dokumenty potrzebują CTE z DISTINCT
3. **`sMM-` i `sMM+` to OSOBNE dokumenty** — magazyn docelowy MM- siedzi w `MG.khdzial` (Sage repurposuje to pole!)
4. **`HM.MZ.ilosc` ma niespójne znaki** — używaj `ABS(MZ.ilosc)`, kierunek wyciągaj z `MG.seria`
5. **NIE MA tabeli słownikowej magazynów** w bazie — nazwy parsuj z sufiksów `kod` w MM+/MM-
6. **`STRING_AGG` wymaga SQL 2017+** — działa na HANDEL, NIE na LibraNet (tam STUFF + FOR XML)
7. **`HM.MG.ProductionOrderID`, `MF.Production*`** — PUSTE (moduł produkcji nigdy nie wdrożony)
8. **Daty**: HANDEL OK z DateTime; LibraNet wymaga string `yyyy-MM-dd`

Pełne wyjaśnienia → `23_HANDEL_Schema_Sage_Symfonia.md` sekcja 4 (KLUCZOWE ODKRYCIA).

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
| `ZamowieniaMieso` | **Zamówienia mięsa od klientów** (klucz `Id`, ID nadawane ręcznie via `MAX(Id)+1` — NIE IDENTITY!) |
| `ZamowieniaMiesoTowar` | Pozycje zamówienia (`ZamowienieId`, `KodTowaru`, `Ilosc`, `Cena`, `E2`, `Folia`, `Hallal`, `Strefa`) |
| `ZamowieniaMiesoSnapshot` | Pre-edit snapshot (typ='Realizacja') — auto-tworzony przy edycji zamówienia |
| `UserHandlowcy` | Mapping `HandlowiecName` (z Symfonii) ↔ `UserID` (ZPSP) — używane do avatarów |
| `NotatkiSzablony` | **System propozycji notatek v2** — szablony tworzone przez handlowców (Globalny/PerKlient/PerHandlowiec, Pin, kategorie) |
| `NotatkiUzycia` | **Auto-learning** — log użyć (Wstawiona/Wpisana) z towarami, do rankingu |
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

### Kolumny `HarmonogramDostaw` (LibraNet) — plan dostaw żywca

> Pełna dokumentacja użycia w module Kontrakty/Pokaż ceny: `27_WidokCenWszystkich_modul.md` §6.

```sql
HarmonogramDostaw (LibraNet):
  Lp int PK              -- klucz wiersza
  LpW int                -- numer wstawienia (FK do wstawienia kurczaków)
  Dostawca varchar       -- nazwa hodowcy (denormalizowana)
  DostawcaID int         -- ID hodowcy
  DataOdbioru datetime   -- data planowanego odbioru
  Bufor varchar          -- 'Potwierdzony' = zatwierdzona dostawa; inne = plan
  TypCeny varchar        -- typ ceny: 'wolnyrynek', 'wolnorynkowa', 'rolnicza',
                         --          'ministerialna', 'łączona' (case-insensitive)
  TypUmowy varchar       -- typ umowy z hodowcą
  Cena decimal           -- cena (per kg w kontrakcie, sprawdzić jednostkę)
  SztukiDek decimal      -- sztuki deklarowane
  WagaDek decimal        -- waga średnia per sztuka (kg); SztukiDek×WagaDek = kg żywca
  Auta int               -- liczba aut/transportów (≥ 0, walidacja w UI)
  KmK, KmH int           -- klatki (Kom / Henhouse?)
  SztSzuflada int        -- sztuki w szufladzie
  PotwWaga decimal       -- potwierdzona waga (po odbiorze)
  PotwSztuki int         -- potwierdzona liczba sztuk
  UWAGI varchar          -- uwagi tekstowe
  DataUtw datetime       -- audit: data utworzenia
  KtoStwo varchar        -- audit: kto utworzył (UserID)
```

**Indeks:** `IX_HarmonogramDostaw_Bufor_DataOdbioru` na (Bufor, DataOdbioru) include (Dostawca).
**Klasyfikacja Kontrakt/Wolny:** `LOWER(TypCeny) IN ('wolnyrynek','wolnorynkowa')` = Wolny; reszta = Kontrakt.
**Powiązanie:** `listapartii.HarmonogramLp = HarmonogramDostaw.Lp` (V2, partia ↔ harmonogram).

### Kolumny `listapartii` (FAKTYCZNE 19 kolumn — zweryfikowane 2026-05-12)

> ⚠️ **WCZEŚNIEJSZA WERSJA DOKUMENTACJI BYŁA BŁĘDNA**. `listapartii` ma TYLKO 19 kolumn — nie ma `CustomerID`, `NettoSkup`, `WydajnoscProc`, `KlasaBProc`, `TempRampa` ani innych metryk produkcyjnych. Hodowca jest w **`PartiaDostawca`** (join via `Partia`).

```sql
listapartii (LibraNet) — 19 kolumn, 37896 wierszy:
  GUID                varchar(36)  NOT NULL  -- PK techniczny
  DIR_ID              varchar(2)   NOT NULL  -- dział '1A','0E','0K' (UWAGA: 'DIR_ID' nie 'DirID')
  Partia              varchar(15)  NOT NULL  -- numer partii (klucz biznesowy)
  GrupaTowarowa       numeric                -- grupa towarowa (kategoria)
  ArticleID           varchar(10)            -- ID towaru (link do Article)
  CreateData          varchar(10)            -- data uboju w formacie 'yyyy-MM-dd' (STRING!)
  CreateGodzina       varchar(8)             -- godzina uboju 'HH:mm:ss' (STRING!)
  ModificationData    varchar(10)            -- data modyfikacji
  ModificationGodzina varchar(10)            -- godzina modyfikacji
  CreateOperator      varchar(6)             -- ID operatora (link do operators.ID)
  CloseData           varchar(10)            -- data zamknięcia
  CloseGodzina        varchar(8)             -- godzina zamknięcia
  CloseOperator       varchar(6)             -- operator zamykający
  IsClose             smallint               -- 0/1 czy zamknięta
  CalcMethod          varchar(1)             -- metoda kalkulacji
  CalcData            varchar(10)            -- data kalkulacji
  CalcGodzina         varchar(8)             -- godzina kalkulacji
  StatusV2            varchar(30)            -- 10-stanowy lifecycle (PLANNED..CLOSED)
  HarmonogramLp       int                    -- link do HarmonogramDostaw.Lp (V2)
```

**Hodowca dla partii** — JOIN z `PartiaDostawca`:
```sql
PartiaDostawca (LibraNet) — 8 kolumn, 37851 wierszy:
  guid                varchar(36)  PK
  Partia              varchar(8)   -- klucz biznesowy = listapartii.Partia
  CustomerID          varchar(10)  -- ID hodowcy
  CustomerName        varchar(40)  -- nazwa hodowcy (denormalizowana)
  CreateData/Godzina  varchar      -- audit
  ModificationData/Godzina  varchar -- audit
```

**Metryki produkcyjne** (NIE w listapartii!) — gdzie szukać:
- **Wagi przyjęte/wydane** → `In0E` (przychód) + `Out1A` (rozchód), JOIN po `P1` = `Partia`
- **Wydajność** → kalkulowana w aplikacji ze stosunku Out1A/In0E
- **Klasa B / KlasaB_Proc** → `WadyPartii` (klasyczna) lub `PodsumaPartii` (V2)
- **Temperatury** → `Temperatury` (PartiaId, Sonda1/2/3)
- **Sztuki deklarowane** → `FarmerCalc.DeclI1..DeclI6` lub `HarmonogramDostaw.SztukiDek`
- **Status QC** → `QC_Podsum` (V2, vw_QC_Podsum)
- **Skup** → `FarmerCalc` (PayWgt × Price = PayNet)

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

1. ~~Migracja LibraNet do SQL Server 2017+~~ — **JUŻ NA SQL 2022** (potwierdzone 2026-05-12)
2. **Konsolidacja TransportPL → LibraNet** — i tak na tym samym serwerze
3. **Symfonia Production module** — albo wdrożyć (kupiony!) albo przenieść jego rolę całkowicie do ZPSP
4. **Connection strings → konfig** (`appsettings.json`) zamiast hardcoded — testowalność, środowisko dev/prod

---

## Użytkownicy / mapowanie userów między bazami (zweryfikowane 2026-05-12)

### LibraNet — tabela `operators` (56 wpisów aktywnych+nieaktywnych)

```sql
operators (LibraNet):
  GUID                 varchar(36)  PK
  ID                   varchar(15)  -- KLUCZ BIZNESOWY (numeryczny string, np. '1122', '6521')
  Name                 varchar(20)  -- np. 'Paulina Koncka', 'Maja Leonard'
  Access               varchar(100) -- 64-bit mask uprawnień
  CreateData/Godzina   varchar      -- audit
  ModificationData/Godzina varchar  -- audit
  PasswordHash         nvarchar(255)
  PasswordSetAt        datetime
  IsAdmin              bit
  FailedAttempts       int
  LockedUntil          datetime
  LastSuccessfulLogin  datetime
```

**Quirk:** `ID` jest typu `varchar(15)` ALE w praktyce zawsze numeryczny string. JOIN z innymi tabelami przez int wymaga **`CAST(O.ID AS int) = innaTabela.UserId`** (`HarmonogramDostaw.KtoStwo` jest `int`).

**Kluczowi userzy (2026-05-12):**

| ID | Name | Rola | Login? |
|---|---|---|---|
| 11111 | Administrator | Admin/Sergiusz | TAK (aktywny) |
| 432143 | Anna Jedynak | Handel | TAK |
| 6521 | Maja Leonard | Sprzedaż mięsa | TAK |
| 2121 | Teresa Jachymczak | Sprzedaż + ostatnio zakup żywca | TAK |
| 1122 | Paulina Koncka | Zakup żywca (CRM hodowcy + harmonogram) | TAK |
| 111222 | Jolanta Kubiak | Sprzedaż mięsa (główna) | (nie loguje się) |
| 871231 | Radoslaw Marciniak | Sprzedaż (Radek) | (rzadko) |
| 6611 | Justyna Chrostowska | KSeF/księgowość | TAK |
| 51991 | Ilona Krakowiak | Handlowiec wewn. | TAK |
| 2121 | Teresa Jachymczak | Sprzedaż + zakup | TAK |
| 9998 | Daniel Czapnik | Były handlowiec | NIE (nieaktywny) |
| 5555 | Dawid Sosiński | Były handlowiec | NIE |
| 9991 | Dawid Śluzarek | Były handlowiec | NIE |

### LibraNet — tabela `UserHandlowcy` (mapowanie UserID → handlowiec)

```sql
UserHandlowcy (LibraNet):
  ID                   int IDENTITY PK
  UserID               nvarchar(50)  -- = operators.ID
  HandlowiecName       nvarchar(...) -- np. 'Maja', 'Daniel', 'Ania'
  CreatedBy/CreatedAt  audit
```

**Kluczowe odkrycie:** to MAPOWANIE WIELU UserID → JEDEN HandlowiecName. Np.:
- `UserID='0000'` (Admin/Sergiusz w starym koncie) mapowane do każdego handlowca jako historia
- `UserID='6521'` (Maja Leonard) → `HandlowiecName='Maja'`
- `UserID='2121'` (Teresa Jachymczak) → `HandlowiecName='Teresa'`
- `UserID='871231'` (Radek Marciniak) → `HandlowiecName='Radek'`

**To pozwala na atrybucję faktur do handlowca** w LibraNet (bo `ZamowieniaMieso.IdUser` jest int).

### HANDEL — `SSCommon.STUsers` (93 wpisy, schemat Sage)

```sql
SSCommon.STUsers (HANDEL):
  Id              int PK             -- 5-cyfrowy ID Sage (np. 32815, 32831)
  LoginName       nvarchar(256)      -- np. 'RB', 'MSS', 'DS', 'Daniel.C'
  Description     nvarchar(510)      -- np. 'Renata Balcerak', 'Małgorzata Stępniak'
  Disabled        bit                -- 1 = nieaktywny
  Hidden          bit
  Authentication  smallint
  ActiveFrom      datetime
  ActiveTo        datetime
  LoginPwd        nvarchar(500)
  UserType        nvarchar(510)
  Guid            uniqueidentifier
```

**Kluczowi wystawcy faktur HANDEL:**

| Id | Login | Opis (rzeczywiste imię) | Rola |
|---|---|---|---|
| 32831 | RB | Renata Balcerak | **#1 księgowa** (33k faktur, 454M PLN obrotu) |
| 32815 | MSS | Małgorzata Stępniak | **#2 księgowa** (30k faktur, 418M PLN) |
| 32772 | EK | Edyta Kochanowska | Księgowość (13k faktur) |
| 32781 | MM | Magdalena Miler | Księgowość (5.7k faktur) |
| 32805 | TZ | Teresa Zuchora | Księgowość (5.3k faktur) |
| 32797 | MD | Marlena Andrzejczak | Księgowość |
| **32856** | **Daniel.C** | **Daniel Czapnik** (b. handlowiec) | **0 wystawionych faktur** |
| **32816** | **DS** | **Dawid Sosiński** (b. handlowiec) | 279 faktur w 2022 (potem zostawił) |
| 32809 | SP | Sergiusz Piórkowski | Właściciel |

### HANDEL — `SSCommon.STPersonUsers` (tabela linkująca osobę z userem)

```sql
SSCommon.STPersonUsers (HANDEL):
  Id              int PK
  PersonId        int             -- FK do STPersons.Id (INTEGER!)
  UserGuid        uniqueidentifier -- FK do STUsers.Guid (GUID, nie Id!)
  OwnerUserGuid   uniqueidentifier
  Guid            uniqueidentifier
```

**⚠️ Quirk Symfonia:** klucze są **MIESZANE** — `PersonId` jako INT do `STPersons`, ale `UserGuid` jako GUID do `STUsers.Guid` (nie `STUsers.Id`!). To dlatego naiwne `PU.PersonGuid = P.Guid` (jak ja pierwszy raz próbowałem) failuje — `PersonGuid` nie istnieje. Poprawny JOIN:

```sql
LEFT JOIN SSCommon.STPersonUsers PU ON PU.UserGuid = U.Guid
LEFT JOIN SSCommon.STPersons     P  ON P.Id        = PU.PersonId
```

### HANDEL — `SSCommon.STPersons` (539 wpisów, osoby fizyczne)

```sql
SSCommon.STPersons (HANDEL):
  Id                int PK
  Firstname         nvarchar(100)
  SecondName        nvarchar(100)
  Surname           nvarchar(100)
  BirthDate         datetime
  Guid              uniqueidentifier
  StringIdent       nvarchar(200)
  DualStringIdent1  nvarchar(128)
  DualStringIdent2  nvarchar(128)
  Note              nvarchar(max)
  ContactGuid       uniqueidentifier
  BankingInfoGuid   uniqueidentifier
  DataSourceName    nvarchar(200)
  ... (12+ pozostałych kolumn audit/anonimizacji)
```

---

## 🚨 KRYTYCZNE odkrycie: `HM.DK.wystawil` to KSIĘGOWA, NIE handlowiec

**Sage Symfonia w polu `wystawil` (int FK do `SSCommon.STUsers.Id`) zapisuje użytkownika który TECHNICZNIE wbił fakturę do systemu** — czyli księgową. NIE jest to handlowiec sprzedażowy.

**Konsekwencje dla analiz historycznych:**

1. **Nie da się odtworzyć "kto sprzedał klientowi X w roku Y"** z `HM.DK.wystawil` — to ZAWSZE księgowa (RB, MSS, TZ, EK, MM).
2. Daniel Czapnik (poprzedni handlowiec Mai) **MA konto w HANDEL** (Id=32856, Login='Daniel.C') ale wystawił **0 faktur**. Nigdy nie używał Sage do księgowania.
3. Dawid Sosiński (Id=32816, Login='DS') wystawił 279 faktur w 2022 r. — ale to incydentalne, NIE jego stałe zadanie.
4. **Jedyne źródło aktualnej atrybucji handlowca**: `SSCommon.ContractorClassification.CDim_Handlowiec_Val` — i to jest **CURRENT STATE**, nadpisywany przy zmianie handlowca.

**Wniosek strategiczny:**
- HANDEL nie ma historycznej atrybucji handlowca w bazie.
- Próba audytu "ile sprzedał Daniel" przez `HM.DK.wystawil` = niemożliwa.
- Alternatywa: LibraNet `ZamowieniaMieso.IdUser` ma historię zamówień per user (ale to zamówienia, nie faktury).
- Jeśli kiedyś będziesz potrzebować HISTORYCZNEJ atrybucji handlowca → trzeba dodać trigger/audit na `CDim_Handlowiec_Val` (obecnie 3 INSTEAD OF triggery są tylko enforcement, nie loguje historii).
