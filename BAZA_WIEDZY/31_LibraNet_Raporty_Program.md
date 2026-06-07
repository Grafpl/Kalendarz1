# 31 — LibraNet „Raporty.exe": pełna dokumentacja pierwotnego programu produkcyjnego

> **Dla kogo ten plik:** przede wszystkim dla **Claude Code** — żeby przy pracy nad dowolnym modułem ZPSP
> rozumiał, **skąd biorą się dane w bazie `LibraNet`**, kto jest ich właścicielem, co wolno nadpisywać,
> a czego dotyka „ten drugi program". To jest mapa systemu, który **tworzy** dane, na których stoi cały ZPSP.
>
> **Metoda powstania:** analiza statyczna binarki `Libranet/Raporty.exe` (Delphi, brak źródeł) — ekstrakcja
> stringów SQL, nazw klas formularzy (RTTI), obiektów pól trwałych ADO (= mapa kolumn), handlerów zdarzeń,
> komunikatów polskich, importów DLL, oraz krzyżowe sprawdzenie w 900 plikach `.cs` ZPSP, które tabele
> Libry są współdzielone, a które wyłączne. Data analizy: **2026-06-02**.
>
> **Status faktów:** ✅ = potwierdzone w binarce/kodzie ZPSP; 🔶 = wywnioskowane z kontekstu (oznaczone w tekście).
>
> **Zobacz też:** [`19_LibraNet_audyt_uzycia.md`](19_LibraNet_audyt_uzycia.md) (jak ZPSP używa LibraNet),
> [`13_Bazy_danych.md`](13_Bazy_danych.md), [`24_Magazyny_i_Lancuch_Produkcji.md`](24_Magazyny_i_Lancuch_Produkcji.md),
> [`18_Analiza_przychodu_szczegoly.md`](18_Analiza_przychodu_szczegoly.md).

---

## SPIS TREŚCI

1. [TL;DR — model mentalny](#1-tldr)
1a. [Producent, rodowód i wersja](#1a-rodowod)
2. [Czym jest Raporty.exe](#2-czym-jest)
3. [Stack technologiczny — hybryda BDE + ADO](#3-stack)
4. [Mapa zależności (DLL)](#4-dll)
5. [Konstrukcja połączenia i konfiguracja](#5-polaczenie)
6. [Model bezpieczeństwa i uprawnień](#6-bezpieczenstwo)
7. [System metadanych `tabele` — rejestr raportów/menu/asortymentu](#7-tabele)
8. [KATALOG TABEL — pełny opis z kolumnami](#8-katalog-tabel)
9. [Słownik widoków (VIEW)](#9-widoki)
10. [KATALOG FUNKCJI — każdy formularz + handlery UI](#10-katalog-funkcji)
11. [Słownik datasetów ADO → tabele → kolumny](#11-datasety)
12. [Pełna inwentaryzacja SQL (surowe zapytania)](#12-sql)
13. [Logika biznesowa z komunikatów programu](#13-logika)
14. [Łańcuch danych: od piskląt do rozliczenia](#14-lancuch)
15. [Relacje między tabelami (JOIN-y)](#15-relacje)
16. [Eksport i integracje (Excel / e-mail / sieć)](#16-integracje)
17. [Granica Libra ↔ ZPSP — kto czego dotyka](#17-granica)
18. [Ryzyka koegzystencji](#18-ryzyka)
19. [Mapa wygaszenia Libry (gdyby kiedyś zastąpić)](#19-wygaszenie)
20. [Słowniczek pól, skrótów i pojęć](#20-slowniczek)
21. [Jak Claude Code ma używać tej wiedzy](#21-jak-uzywac)
22. [Aneks — surowe dane ekstrakcji](#22-aneks)
23. [Katalog raportów i dokumentów (QuickReport)](#23-raporty)
24. [Etykiety kolumn i komunikaty programu (PL)](#24-etykiety)
25. [Diagram ERD (ASCII) — pełny schemat](#25-erd)
26. [Przykład krok po kroku: rozliczenie hodowcy](#26-przyklad)

---

<a name="1-tldr"></a>
## 1. TL;DR — model mentalny

1. **`Raporty.exe`** (folder `Libranet/`) to **pierwotny program produkcyjny zakładu** — komercyjny **system wagowy LibraNet** firmy **Pro-Nova Sp. z o.o.** (Poznań, rdzeń od 2003, wersja 2.0.3.5). Działał, zanim powstał ZPSP, i **nadal działa równolegle** (build ze stycznia 2024, 3,63 MB). To NIE jest kod Sergiusza — to kupiony system.
2. Natywna aplikacja **Delphi / Embarcadero RAD Studio (VCL)** — nie .NET. Hybrydowa warstwa danych: **ADO (SQLOLEDB)** do SQL Servera + **BDE (`IDAPI32.DLL`)** legacy. Wydruki: **QuickReport 4.06.4**. Sieć: **Indy**. Eksport: **Excel via OLE**.
3. Łączy się z **jedną bazą: `LibraNet` (192.168.0.109)**, login `pronova`, hasło **zaszyfrowane** w `Raporty.ini` (Windows CryptoAPI).
4. Jest **systemem źródłowym (system of record) dla hali produkcyjnej**: ważenia, przyjęcia surowca, ubój, receptury/farsz, normy uzysku, rozliczenia hodowców, skup, HACCP, urządzenia/wagi.
5. To **Libra tworzy** rdzeń danych: `Dostawcy`, `Driver`, `CarTrailer`, `Article`, `FarmerCalc`, `listapartii`, `operators`, ważenia (`In8A`/`Out4A`/`State8A`), receptury (`RecDoc`/`RecHeader`), dokumenty 3A.
6. **ZPSP nie dotyka ~12 operacyjnych tabel Libry** (RecDoc/RecHeader, ArtPartition, DocIn3A/HeaderDocIn3A, In8A/Out4A/State8A, Skupy, PartNorm, Dodatki7, ITDevices, dostep, scaletypes, tabele = **0 plików** ZPSP).
7. **ZPSP współdzieli dane master** (Dostawcy/FarmerCalc/Driver/Article/listapartii/operators) i **dobudowuje** warstwy, których Libra nie ma: `HarmonogramDostaw`, `ZamowieniaMieso`, Customer360, Analityka Pełna, QC V2, AI, Reklamacje.
8. **Granica jest komplementarna, nie konkurencyjna:** Libra = „czujniki hali" (fizyka procesu), ZPSP = „mózg" (planowanie, analiza, sprzedaż, CRM).
9. **Jedyny niebezpieczny styk:** `FarmerCalc` jest pisana z OBU systemów — jedyna transakcyjna tabela o dwustronnym zapisie (ryzyko kolizji rozliczeń).
10. Libra ma **własny model uprawnień** (`dostep`: mainAccess/menuAccess/access2) i **własny rejestr menu/raportów** (`tabele`: REP_MENU/REP_GROUP/REP_ORDER) — oba niezsynchronizowane z `accessMap` ZPSP.

---

<a name="1a-rodowod"></a>
## 1a. Producent, rodowód i wersja

✅ Dane z zasobu wersji (VERSIONINFO) i stringów copyright w binarce:

| Atrybut | Wartość |
|---|---|
| **CompanyName** | **Pro-Nova Sp. z o.o.** (← stąd login do bazy: `pronova`) |
| **FileDescription** | LibraNet Raporty |
| **Comments** | **System wagowy LibraNet** |
| **FileVersion** | **2.0.3.5** |
| ProductVersion | 1.0.0.0 |
| Pochodzenie | „LibraNET SYSTEM — Poznań", „Copyright (c) 2003 A. Lochert", „LibraNet. (c) Dariusz J…" |

🔶 **Rodowód:** LibraNet to **komercyjny system wagowy** firmy zewnętrznej (Pro-Nova Sp. z o.o., autorzy
A. Lochert / Dariusz J., rdzeń ok. 2003 r., Poznań). To NIE jest oprogramowanie napisane przez Sergiusza —
w odróżnieniu od ZPSP. Zakład go **kupił/wdrożył** jako system wagowo-produkcyjny, a ZPSP powstał później
jako własna nadbudowa nad jego bazą.

✅ **Rodzina produktów LibraNet** (stringi w binarce wskazują na siostrzane aplikacje):
- **`Raporty.exe`** — ten program (rozliczenia, produkcja, raporty)
- **LibraNet – Ekspedycja** — 🔶 osobny moduł ekspedycji/wydań (wzmianka „LibraNet - Ekspedycja")
- 🔶 stanowiska ważące (terminale `ITDevices`) jako klienty tego samego systemu

**Konsekwencja dla ZPSP:** baza `LibraNet` ma **narzucony, zewnętrzny schemat** (nazwy tabel/pól pochodzą
od Pro-Nova, nie od Sergiusza). Dlatego konwencje nazewnicze w `LibraNet` (`In8A`, `Out4A`, `FarmerCalc`,
`DeclI1..6`, `RecDoc`) są „obce" względem reszty ZPSP — to dziedzictwo systemu wagowego, nie wybór ZPSP.

---

<a name="2-czym-jest"></a>
## 2. Czym jest Raporty.exe

✅ **Plik:** `Libranet/Raporty.exe`, 3 631 104 B (3,63 MB), PE32 Win32 (Intel i386, GUI), 9 sekcji, build 01.2024.
✅ **Towarzyszący plik:** `Libranet/Raporty.ini` (sekcja `[SQL]`: Server/Database/User/Password).

Nazwa „Raporty" jest **myląca** — to nie jest tylko przeglądarka wydruków. To **pełny program operacyjny**
zakładu drobiarskiego z modułami transakcyjnymi (CRUD hodowców, rozliczenia, ważenia, ubój, receptury,
produkcja farszu, peklowanie, wędzenie, pakowanie) i bogatą warstwą wydruków QuickReport. Nazwa pochodzi
🔶 prawdopodobnie stąd, że historycznie był to moduł raportowo-rozliczeniowy systemu wagowego **LibraNet**
(producent oprogramowania wagowego — stąd nazwa bazy `LibraNet` i tabel `In*`/`Out*`/`State*`).

🔶 **Charakter pracy:** aplikacja desktopowa, jednobazowa, synchroniczna (VCL blokuje wątek UI na czas
zapytania). Wzorzec UI: **jeden formularz na encję** (lista + edytor + raport), z powtarzalnym pasem
przycisków. Nawigacja po rekordach klasycznymi przyciskami DB (`sbtnDBFirst/Prior/Next/Last`),
filtrowanie po dacie/nazwie, eksport do Excela (`sbtnExcell`), podgląd wydruku (`sbtnPreview`).

---

<a name="3-stack"></a>
## 3. Stack technologiczny — hybryda BDE + ADO

| Warstwa | Technologia (✅ potwierdzone w binarce) |
|---|---|
| Język / RAD | Delphi / **Embarcadero RAD Studio** (VCL: `TForm`, `TBitBtn`, `TSpeedButton`, `TDBGrid`, `TDBLookupComboBox`, `TMaskEdit`, `TSpinEdit`, `TTreeView`) |
| Dostęp do danych #1 | **ADO** (`TADOConnection`, `TADOQuery`, `TADODataSet`, `TADOCommand`, `TADOStoredProc`) — provider **SQLOLEDB.1** |
| Dostęp do danych #2 | **BDE** — `IDAPI32.DLL` (Borland Database Engine, legacy; 🔶 prawdopodobnie do starszych ścieżek/migracji) |
| Raporty/wydruki | **QuickReport 4.06.4** (`TQuickRep`, `TQRBand`, `TQRLabel`, `TQRDBText`, `TQRExpr`, `TQRSysData`, `TQRGroup`, `TQRChildBand`, `TQRShape`) |
| Sieć | **Indy** (`TIdStack`, `TIdEncoder`, `TIdDecoder`, `TIdUTF8Encoding`, `TIdSocketListWindow`) |
| Kryptografia | **Windows CryptoAPI** (`CryptEncrypt`/`CryptDecrypt`/`CryptDeriveKey`/`CryptGenRandom`/`CryptHashData`/`CryptAcquireContext`) |
| Eksport | **Excel via OLE Automation** (`Excel.Application`, `Excel2000`, `TExcelApplication`) — komunikat „Excel nie zainstalowany." |
| E-mail | **MAPI** (`MAPI32.DLL`) |
| Baza danych | **MS SQL Server** (LibraNet) — składnia: `GetDate()`, `cast(... as bit)`, `Substring`, `Coalesce`, `NullIf`, `with (nolock)` |

🔶 **Wniosek:** to dojrzała, ale technologicznie schyłkowa aplikacja. Obecność BDE (`IDAPI32`) obok ADO
sugeruje długą historię i migrację z jeszcze starszej warstwy danych. QuickReport 4.x i SQLOLEDB to
technologie nierozwijane od lat — co tłumaczy, dlaczego nadbudowano ZPSP zamiast rozwijać Librę.

---

<a name="4-dll"></a>
## 4. Mapa zależności (DLL)

✅ Importy z binarki (grupowane funkcjonalnie):

| Grupa | DLL | Po co |
|---|---|---|
| System/UI | `Kernel32`, `User32`, `gdi32`, `comctl32`, `comdlg32`, `shell32`, `uxtheme`, `msimg32`, `imm32`, `version` | rdzeń Windows + kontrolki + motywy + dialogi |
| Baza danych | **`IDAPI32.DLL`** (BDE), `oleaut32`, `ole32`, `olepro32` | BDE + Automation/OLE (ADO i Excel) |
| Bezpieczeństwo | `ADVAPI32.dll` | CryptoAPI (szyfrowanie hasła z `.ini`) |
| Sieć | `WS2_32`, `wsock32`, `MSWSOCK`, `Wship6`, `iphlpapi`, `Fwpuclnt`, `netapi32`, `Normaliz`, `IdnDL` | Indy (sockety, IPv6, DNS/IDN) |
| Poczta | `MAPI32.DLL` | wysyłka e-mail |
| Pulpit | `DWMAPI.DLL` | kompozycja okien (Aero) |
| COM+ | `mtxex.dll` | transakcje rozproszone (MTS/COM+) |

---

<a name="5-polaczenie"></a>
## 5. Konstrukcja połączenia i konfiguracja

### `Raporty.ini`
```ini
[SQL]
Server=192.168.0.109
Database=Libranet
User=pronova
Password=q7zxj8zKHkjEOZcRODpWOg==   ; <-- zaszyfrowane Windows CryptoAPI (base64 ciphertext)
```

🔶 **Budowa connection stringu:** Libra czyta 4 klucze z `.ini`, **deszyfruje hasło** przez CryptoAPI,
po czym składa connection string OLE DB:
```
Provider=SQLOLEDB.1;Data Source=192.168.0.109;Initial Catalog=Libranet;User Id=pronova;Password=<odszyfrowane>
```
(✅ stringi `Provider=SQLOLEDB.1`, `Data Source=`, `Initial Catalog=` obecne w binarce; alternatywnie `MSDASQL.1`.)

✅ **Ważne — porównanie z ZPSP:** Libra **szyfruje** swoje jedyne poświadczenie. ZPSP dla kontrastu trzyma
`User Id=pronova;Password=pronova` (218 wystąpień) oraz hasło SA do HANDEL `?cs_'Y6,n5#Xd'Yd`
w **plaintext w 91 plikach** `.cs`. To wyraźna regresja higieny poświadczeń — patrz sekcja 18.

---

<a name="6-bezpieczenstwo"></a>
## 6. Model bezpieczeństwa i uprawnień

✅ **Tabela `dostep`** (access control Libry) — kolumny:
- `username` — login operatora
- `mainAccess` — uprawnienia główne (poziom dostępu do funkcji)
- `menuAccess` — widoczność pozycji menu (sprzężone z `tabele.REP_MENU`)
- `access2` — dodatkowy poziom uprawnień

✅ **Operacja kopiowania uprawnień** (`TAccessForm`) — admin może skopiować prawa z innego usera:
```sql
update dostep set
  mainAccess = (select d.mainaccess from dostep d where d.username='<źródło>'),
  menuaccess = (select d.menuaccess from dostep d where d.username='<źródło>'),
  access2    = (select d.access2    from dostep d where d.username='<źródło>')
where username='<cel>'
```
✅ **Zmiana hasła** (`TPasswordForm`, pola `EdtNewPassword`, `ADOTable1Password`, `ADOTable1PasswordChangedDate`,
`ADOTable1OpenPassword`) — z użyciem CryptoAPI. Komunikat „Brak uprawnień" przy odmowie.

✅ **Operatorzy** (`operators`, dataset `OperatorListQuery`) — kolumny: `GUID`, `ID`, `Name`, `Access`,
`CreateData`, `CreateGodzina`, `ModificationData`, `ModificationGodzina`. To wspólny słownik ludzi (ten sam,
który czyta ZPSP — 56 wierszy: 11111=Administrator, 1122=Paulina, 2121=Teresa, 6521=Maja…). Ale uprawnienia
żyją w `dostep`, **niezsynchronizowane** z `accessMap`/`userPermissions` ZPSP.

🔶 **Rejestr urządzeń `ITDevices`** (DeviceKind, DeviceID, TerminalID, DeviceName, DeviceIP varchar,
DeviceMAC varchar, Description, ProgVer, Created, Modified) — Libra zarządza terminalami/wagami i ich
konfiguracją sieciową (IP/MAC). ZPSP tego nie robi.

---

<a name="7-tabele"></a>
## 7. System metadanych `tabele` — rejestr raportów/menu/asortymentu

✅ **`tabele`** to **metadanowa tabela sterująca** aplikacją (NIE zwykła tabela danych). Kolumny:
- `ID`, `Kind` — identyfikator i typ pozycji (asortyment / raport)
- `REP_DIR` — kierunek/dział (powiązany z `listapartii.DIR_ID`: 1A=ubój, 0E=mrożenie, 0K=krojenie…)
- `REP_GROUP` — grupa w menu
- `REP_MENU` — pozycja menu (+ widoczność wg `dostep.menuAccess`)
- `REP_ORDER` — kolejność wyświetlania
- `REP_TITLE_MIA` / `REP_TITLE_MIE` / `REP_TITLE_B` / `REP_TITLE_C` / `REP_TITLE_D` / `REP_TITLE_N`
  — 🔶 warianty tytułu (prawdopodobnie przypadki gramatyczne PL: Mianownik/Miejscownik/Biernik/…,
  używane do poprawnej odmiany w tytułach wydruków, np. „Raport **uboju**" vs „**Ubój**")

✅ **Użycie** (sterowanie raportami i listą partii):
```sql
from tabele t, listapartii l ...          -- łączenie asortymentu z partiami
select distinct t.ID, t.Kind, t.REP_TITLE_MIA ...
select l.*, t.Rep_TITLE_MIA, a.Name as ArticleName, o1.Name as Operator1Name ... order by t.REP_TITLE_MIA
```

🔶 **Znaczenie:** `tabele` definiuje **asortyment/kategorie produktów** powiązane z kierunkami produkcji
(DIR) oraz steruje **strukturą menu i tytułami raportów**. To „mózg konfiguracyjny" Libry. ZPSP go nie używa
(0 plików) — ZPSP ma własną strukturę menu w `Menu.cs` (`accessMap`/`MenuItemConfig`).

---

<a name="8-katalog-tabel"></a>
## 8. KATALOG TABEL — pełny opis z kolumnami

> Legenda: **R**=SELECT, **W**=INSERT/UPDATE, **D**=DELETE (lub soft `Deleted=1`).
> „ZPSP" = liczba plików `.cs` ZPSP, w których tabela występuje (grep 2026-06-02).
> Kolumny pochodzą z obiektów pól trwałych ADO oraz z listy SELECT — to **rzeczywiste nazwy kolumn**.

### 8.1. Dane master (współdzielone z ZPSP)

#### `Dostawcy` — hodowcy/dostawcy żywca ✅ (Libra R/W/D · ZPSP 91 plików)
Słownik hodowców. **Libra jest twórcą i edytorem** (formularze Hodowcy, dataset `ADOHodowcy`/`ADOHodowcyCalc`).
Kolumny: `GID` (PK), `ID`, `ShortName`, `Name`, `Nip`, `Regon`, `Pesel`, `IDCard`, `IDCardDate`, `IDCardAuth`,
`PriceTypeID` (FK→PriceType), `Addition` (dodatek do ceny), `Loss` (ubytek/strata %),
`IncDeadConf` (uwzględniaj padłe/konfiskaty — bit), `Address`, `PostalCode`, `City`, `ProvinceID`,
`Distance` (km), `Phone1/2/3`, `Info1/2/3`, `Email`, `IsFarmAddress` (bit), `AnimNo` (nr gospodarstwa ARiMR),
`IRZPlus`, `Halt` (zablokowany — bit), `Created/CreatedBy/Modified/ModifiedBy`.
> ⚠️ Klucz w ZPSP: `Dostawcy.ID` = VARCHAR(10) PK; `FarmerCalc` linkuje przez `CustomerGID=Dostawcy.GID`
> (patrz memory `reference_libranet_dostawcy_farmercalc`).

#### `DostawcyAdresy` — adresy ferm hodowcy ✅ (Libra R/W/D)
1:N do `Dostawcy` (FK `CustomerGID`). Hodowca może mieć wiele ferm. Dataset `dsAdresy`.
Kolumny: `GID` (PK), `CustomerGID` (FK), `Kind`, `Name`, `Address`, `PostalCode`, `City`, `ProvinceID`,
`Distance`, `Phone1`, `Info1`, `AnimNo`, `IRZPlus`, `Halt`, `DefAdr` (adres domyślny — bit), `Deleted`,
`Created/CreatedBy/Modified/ModifiedBy`.

#### `Driver` — kierowcy ✅ (Libra R/W/D · ZPSP 23 pliki)
Dataset `ADODriver`. Kolumny: `GID` (PK), `Name`, `Halt`, `Deleted`, `Created`, `Modified`, `ModifiedBy`.
> ZPSP synchronizuje `Driver.Name` z `TransportPL.Kierowca` (memory `project_flota_transport_primary`).

#### `CarTrailer` — pojazdy (auta + naczepy) ✅ (Libra R/W/D · ZPSP 5 plików)
Dataset `ADOCarTrailer`. Kolumny: `ID` (PK), `Kind` (1=auto, 2=naczepa → `KindText`), `Created`, `Modified`.
Operacja **twardego** DELETE: `DELETE FROM CarTrailer WHERE ID='…'`.

#### `Article` — kartoteka towarów ✅ (Libra R · ZPSP 24 pliki)
Kolumny: `ID` (PK), `Name`, `JM` (jednostka miary), `Cena1`, `Cena2`, `Grupa1`, `Przelicznik`, `Halt`.
Synchronizowana z HANDEL. Libra czyta do receptur/produkcji/skupu (wartość zużycia = `UsageKG × Cena1/2`).

#### `PriceType` — typy cen ✅ (Libra R)
Słownik typów cen (świeży/mrożony/korekta). FK z `Dostawcy.PriceTypeID` i `FarmerCalc.PriceTypeID`.

#### `operators` — operatorzy/pracownicy ✅ (Libra R/W · ZPSP 72 pliki)
Patrz sekcja 6. `GUID`, `ID`, `Name`, `BenchName`, `Access`, `NrUboju`, `IndNo`, audyt. Powiązany 1:1 z `dostep`.

#### `kontrahenci` — kontrahenci ✅ (Libra R · ZPSP 25 plików)
🔶 Odbiorcy/klienci (osobno od `Dostawcy`=hodowcy). `select * from kontrahenci`.

### 8.2. Rozliczenia hodowców (transakcyjne, dwustronny zapis!)

#### `FarmerCalc` — rozliczenia z hodowcami ✅ (Libra R/W/D · ZPSP 49 plików) ⚠️ DWUSTRONNY ZAPIS
**Najważniejsza tabela transakcyjna Libry** i jedyna, którą pisze też ZPSP. Jeden wiersz = jedno
rozliczenie dostawy żywca od hodowcy. Dataset `ADOHodowcyCalc` (potwierdzone obiekty pól trwałych):
- **Identyfikacja:** `ID` (PK), `Number`, `YearNumber`, `CalcDate`, `CarLP`, `Status`, `Partia`
- **Hodowca/transport:** `CustomerGID` (FK→Dostawcy), `AddressGID` (FK→DostawcyAdresy), `CarID`, `TrailerID`, `DriverGID`
- **Cennik:** `PriceTypeID`, `Addition`, `Loss`, `IncDeadConf`, `Price1`, `Price2`, `Price`
- **Wagi:** `FullWeight`, `EmptyWeight` (auto pełne/puste – zakład), `FullFarmWeight`, `EmptyFarmWeight` (waga u hodowcy), `NetWeight` (=Full−Empty, wyliczane)
- **Trasa/czas:** `FullDate`, `FullUser`, `EmptyDate`, `EmptyUser`, `StartDate`, `StopDate`, `StartKM`, `StopKM`
- **Weterynaria:** `VetMedDate`, `VetNo`, `VetRate0`, `VetRate1`, `VetRate2`, `VetDate`, `VetUser`, `VetComment`
- **Sztuki deklarowane:** `DeclI1`..`DeclI6` (klasy; `DeclI3+DeclI4+DeclI5` = `SumKonf` konfiskaty)
- **Wynik:** `LumQnt`, `ProdQnt`, `ProdWgt`, `AvgWgt`, `PayWgt`, `PayNet`
- **Audyt:** `Created/CreatedBy/Modified/ModifiedBy`, `Deleted`
- **JOIN:** `WagoCounter` po (`CalcDate`,`CarLP`) → `Quantity as WagoCnt`
> ⚠️ W ZPSP `FarmerCalc.CreatedBy` jest puste w 100% (memory `19_LibraNet_audyt`) — audyt rozliczeń ślepy.

#### `WagoCounter` — licznik ważeń ✅ (Libra R)
Klucz: (`CalcDate`, `CarLP`). `Quantity` = liczba ważeń dla rozliczenia. JOIN do `FarmerCalc`.

### 8.3. Przyjęcia surowca — dział 3A (WYŁĄCZNIE Libra, ZPSP=0)

#### `HeaderDocIn3A` — nagłówki dokumentów przyjęcia 3A ✅ (Libra R · ZPSP 0)
Dataset `PlanProdDSet` (sterowane przez `TPlanProdMainForm`). Kolumny: `DocNumber` (PK), `Partia`,
`CustomerID`, `CustomerName`, `EstWeight` (waga szacowana), `StartData`, `IsClose` (bit), `OrderNo`,
`Forced` (bit — wymuszenie). Pola wyliczane: `SUMAPRZYP` (suma przypraw, `ArticleID '9%'`), `SUMACALA` (suma całkowita kg).

#### `DocIn3A` — pozycje dokumentów przyjęcia 3A ✅ (Libra R · ZPSP 0)
Dataset `PlanProdDetDSet`. 1:N do `HeaderDocIn3A` (FK `DocNumber`). Kolumny: `ArticleID`, `ArticleName`,
`JM`, `OrdWeight` (zamówiona), `Weight` (zważona), `Uwagi`. Filtr `Substring(ArticleID,1,1)='9'` = przyprawy.
🔶 „3A" = strumień przyjęć surowca. Istnieje też `docout0e`/`HeaderDocOut0E` (wyjścia dział 0E=mrożenie).

### 8.4. Receptury, farsz i zużycie surowca (WYŁĄCZNIE Libra, ZPSP=0)

#### `RecHeader` — nagłówki receptur/dokumentów zużycia ✅ (Libra R · ZPSP 0)
Dataset `ReceiptsListDSet`. Kolumny: `GUID` (PK), `RecID`, `RecName`, `ArticleID`, `ArticleName`,
`CreateData`, `CreateGodzina`, `ModificationData`, `ModificationGodzina`, **`Efficiency`** (wydajność),
`SUMAPRZYP` (suma przypraw), `SUMACALA` (suma całkowita zużycia kg).

#### `RecDoc` — pozycje receptur (zużycie materiału) ✅ (Libra R · ZPSP 0)
Dataset `ReceiptDSet`. 1:N do `RecHeader` (FK `GUID_H`). Kolumny: `GUID`, `RecID`, `RelatedRecNo`,
`MaterialID`, `MaterialName`, `JM`, `Kind`, `MustWeight` (czy obowiązkowo ważyć), `UsageKg` (zużycie kg),
**`UsageProc`** (zużycie %), `CreateData/Godzina`, `ModificationData/Godzina`.
Wartość zużycia = `SUM(UsageKg × Article.Cena1/Cena2)`. `MaterialID '9%'` = przyprawy.
🔶 To rdzeń produkcji przetworzonej: **farsz homogenizowany**, **farsz podrobowy**, peklowanie, mieszanki.

### 8.5. Zestawy/kafelki produktów (WYŁĄCZNIE Libra, ZPSP=0)

#### `ArtPartitionH` — nagłówki zestawów ✅ (Libra R/W · ZPSP 0)
Kolumny: `Zestaw`, `GroupID`, `Name`. (ORDER BY `GroupID`)

#### `ArtPartitionD` — pozycje zestawów ✅ (Libra R/W/D · ZPSP 0)
Kolumny: `Zestaw`, `GroupID`, `Position`, `ID`, `Name`, **`Img`** (BLOB — zdjęcie/ikona produktu).
(ORDER BY `Position`) 🔶 System „kafelków" produktów na stanowisku dotykowym (wizualne karty towaru).

### 8.6. Ważenia stacji i ruchy magazynowe (WYŁĄCZNIE Libra, ZPSP=0)

#### `In8A` — wejścia stacji 8A ✅ (Libra R/D · ZPSP 0) — DELETE po `GUID` = korekta ważenia
#### `Out4A` — wyjścia stacji 4A ✅ (Libra R/D · ZPSP 0)
Kolumny (z SELECT `s.*`): `GUID`, `ArticleID`, `ArticleName`, `Partia`, `ActWeight` (waga rzeczywista),
`JM`, `Quantity`, `AktData`, `AktGodzina`. JOIN do `operators` (o.NrUboju, o.IndNo).
#### `State8A` — stan stacji 8A ✅ (Libra R/D · ZPSP 0) — DELETE po `GUID`
#### `Out1A` — wyjścia 1A (legacy) ✅ — w ZPSP traktowane jako legacy (sprzedaż jest w HANDEL)
> 🔶 Schemat numeracji stacji/działów: `1A`=ubój, `3A`=przyjęcia, `4A`/`8A`=ważenia produkcji, `0E`=mrożenie, `0K`=krojenie.

### 8.7. Normy uzysku / wydajność (WYŁĄCZNIE Libra, ZPSP=0)

#### `PartNorm` / `PartNormDetail` — normy uzysku ✅ (Libra R · ZPSP 0)
`PartNormDetail.ArticleID` + `NormNo` — norma uzysku per artykuł. Używane w obliczeniach wydajności
(input kg vs output kg vs norma). Komunikat „UWAGA! Wydajność…", „Nie przeliczone".

### 8.8. Skup żywca „z ręki" (WYŁĄCZNIE Libra, ZPSP=0)

#### `Skupy` / `skupy` — skup pojedynczych sztuk ✅ (Libra R/W · ZPSP 0)
Kolumny: `CreateData`, `CustomerID`, `IndNo`, `CarcassClass` (klasa tuszki), `LiveWeight` (waga żywa),
`HandPrice` (cena ręczna). JOIN do `Dostawcy` (alias „Skupowy") i `skupy` (alias „Skup" = punkt skupu).
GROUP BY `CreateData, CustomerID, IndNo, CarcassClass, LiveWeight, HandPrice`.

### 8.9. Dodatki / przyprawy

#### `Dodatki7` — dodatki/przyprawy ✅ (Libra R · ZPSP 0)
🔶 Słownik dodatków/przypraw używanych w recepturach (alias `Dodatki7 d`).

### 8.10. Ubój i partie

#### `listapartii` — lista partii ✅ (Libra R/W · ZPSP 14 plików)
Kolumny (19, potwierdzone w ZPSP): `GUID`, `DIR_ID`, `Partia`, `GrupaTowarowa`, `ArticleID`,
`CreateData/Godzina`, `ModificationData/Godzina`, `CreateOperator`, `CloseData/Godzina`, `CloseOperator`,
`IsClose`, `CalcMethod`, `CalcData`, `CalcGodzina`, `StatusV2` (dodane przez ZPSP), `HarmonogramLp` (dodane przez ZPSP).
> Libra zarządza cyklem partii: tworzenie/otwieranie/zamykanie („Zamknij partię", „Zadanie nr …").
> 🔶 `UbojTable`/`UbojRow`/`Ubojnia` — tabele/struktury uboju powiązane z partią.

### 8.11. Chłodnia/stany — dokumenty „Ref" 🔶
🔶 Tabele za formularzami `TRefDoc*`/`TRefState*`/`TRefRep*` — dokumenty ruchów i stany chłodni/magazynu.
Konkretne nazwy budowane dynamicznie (nie ujawniły się jako stałe). „Magazynie Zwrot", „mag. porozbiorowym".

### 8.12. Konfiguracja, urządzenia, metadane

| Tabela | Rola | ZPSP |
|---|---|---|
| `AppSettings` ✅ | Konfiguracja aplikacji (klucz/wartość) | wspólna |
| `tabele` ✅ | Rejestr menu/raportów/asortymentu (REP_*) — patrz sekcja 7 | 0 |
| `ITDevices` ✅ | Rejestr terminali/wag (IP, MAC, ProgVer, TerminalID) | 0 |
| `scaletypes` ✅ | Typy wag | 0 |
| `dostep` ✅ | Uprawnienia operatorów (mainAccess/menuAccess/access2) | 0 |

### 8.13. Tabele migracyjne/staging ✅
- `tomek_swag_wazenia`, `tomek_swag_wazenia_zakonczone` — ważenia operatora „Tomek" (staging)
- `pm_swag_wazenia`, `pm_swag_wazenia_zakonczone` — docelowe; kopiowanie `INSERT INTO pm_swag_wazenia SELECT * FROM tomek_swag_wazenia`
🔶 Migracja/konsolidacja ważeń między operatorami/stanowiskami (SWAG = 🔶 nazwa stanowiska/skanera).

---

<a name="9-widoki"></a>
## 9. Słownik widoków (VIEW)

| View | Co | Użycie |
|---|---|---|
| `v_in1a_p2` ✅ | Wejścia działu **1A** (ubój) zagregowane po `p2` (partia) → `sumakg` | Obliczenia wydajności (`avg(i.sumakg)`) |
| `v_in` ✅ | 🔶 Ogólny widok wejść | agregacje wag |
| `V_358` ✅ | 🔶 Widok specjalny (nazwa numeryczna) | nieustalone |

🔶 Widoki `v_in*` dostarczają **zagregowane wagi wejściowe per partia**, które Libra zestawia z wyjściami
(`Out*`) i normami (`PartNorm`) do liczenia uzysku.

---

<a name="10-katalog-funkcji"></a>
## 10. KATALOG FUNKCJI — każdy formularz + handlery UI

> ✅ Lista ~84 klas formularzy aplikacji (poza komponentami QuickReport/VCL).
> Wspólny pas przycisków (handlery potwierdzone): `sbtnAddClick`, `sbtnEditClick`, `sbtnDeleteClick`,
> `sbtnFilterClick`/`sbtnFilterExecClick`/`sbtnFilterClearClick`, `sbtnRefreshClick`, `sbtnPreviewClick`,
> `sbtnExcellClick` (eksport Excel), `sbtnCloseClick`, nawigacja DB `sbtnDBFirst/Prior/Next/Last`,
> `sbtnDatePrev/Next`. Dwuklik na liście (`OnDblClick`), klik nagłówka kolumny (`TitleClick`, sortowanie).

### 10.1. Rozliczenia hodowców (RDZEŃ)
| Formularz | Tabele | Operacje | Handlery specyficzne |
|---|---|---|---|
| `TFarmerCalcForm` | FarmerCalc, Dostawcy, Driver, WagoCounter, PriceType | R/W/D | `ADOListQueryAfterScroll`, `CalcFields`, `CalcTruckType` |
| `TFarmerCalcEditForm` | FarmerCalc, DostawcyAdresy | R/W | `sedtFullWeightChange`, `edtWeightExit/KeyPress`, `edtFarmerName(Change/Enter/Exit/KeyDown)`, `edtCarIDKeyDown`, `edtTrailerIDKeyDown`, `edtDriverNameEnter`, `cbbPriceTypeChange`, `dtpVetMedDateClick`, `edtYearNumberKeyPress`, `CalcIsClose` |
| `TFarmerCalcRepAvilogForm` | FarmerCalc | R | raport Avilog |

### 10.2. Hodowcy / dostawcy
| Formularz | Tabele | Operacje | Handlery |
|---|---|---|---|
| `THodowcyForm` | Dostawcy, PriceType | R | `DBGridHodowcyCalc`, `ADOHodowcyCalc` |
| `THodowcyEditForm` | Dostawcy | R/W/D | `edtNIPKeyPress`, `edtIRZPlusKeyPress`, `edtAdditionKeyPress`, `edtDistanceKeyPress` |
| `THodowcyAdresyForm` | DostawcyAdresy | R/W/D | dsAdresy |
| `THodowcyCenyForm` | Dostawcy, PriceType | R/W | cennik (Addition/Loss/IncDeadConf) |

### 10.3. Flota
| `TDriverForm`, `TDriverEditForm` | Driver | R/W/D (soft) |
| `TCarTrailerForm`, `TCarTrailerEditForm` | CarTrailer | R/W/D (twardy) |
| `TTruckListForm`, `TTruckDetailForm` | CarTrailer/Driver | R · `sbtnTruckParamsClick` |

### 10.4. Kartoteka towarów i zestawy
| `TArticleListForm`, `TArticleListQForm` | Article | R (filtr po CName) |
| `TArticleDetailForm` | Article | R |
| `TPartitionKafelkiForm` | ArtPartitionH/D | R/W/D (+ obrazy Img) |
| `TPartitionCalcDlgForm`, `TPartitionCalcReportForm` | ArtPartition + wagi | R |

### 10.5. Przyjęcia surowca 3A + plan produkcji
| `TPlanProdMainForm` | HeaderDocIn3A, DocIn3A, listapartii, Article | R/W | `PlanProdCB(Change/Enter/Exit/KeyDown)`, `PlanProdDT(Enter/Exit)`, `PlanProdDSet`, `PlanProdDetDSet` |
| `TPlanProdNewPartDlg` | listapartii | W | nowa partia w planie |
| `TPlanProdRptForm`, `TPlanProdRpt2Form` | — | R | raporty planu |
| `TProductionDlgForm`, `TProductionRptForm` | In*/Out* | R | raporty produkcyjne |

### 10.6. Receptury i produkcja przetworzona
| `TReceiptsForm`, `TNewReceiptsForm` | RecHeader, RecDoc | R/W | `ReceiptsListDSet`, `ReceiptDSet`, `ReceiptsListGridKeyDown/TitleClick` |
| `TReceiptArticleListForm` | Article, RecDoc | R | wybór artykułów |
| `TFarszProdForm` | RecDoc | R/W | produkcja farszu (homogenizowany/podrobowy); walidacja „Brak farszu na mag. porozbiorowym" |
| `TPeklowniaForm`, `TPeklowniaReportForm` | RecDoc/RecHeader | R/W | peklowanie + raport |
| `TWedzarniaUbytkiForm`, `TWedzarniaUbytkiReportForm` | (ubytki) | R/W | ubytki wędzenia („Ubytek obliczony/ustalony", „Ubytek KG") |
| `TPakowniaDlgForm`, `TPakowniaRptForm` | Article/Doc | R | pakowanie + raport |
| `TDodatkiForm`, `TNewDodatkiForm` | Dodatki7 | R/W | dodatki/przyprawy |

### 10.7. Wydajność / uzysk
| `TWydajnoscDlgForm` | v_in1a_p2, Out*, PartNorm/PartNormDetail, Article | R | `WydajnoscEditKeyDown`; liczy `sum(out kg)/avg(in kg)` vs norma; ostrzeżenia „Suma surowca = zero", „Wydajność…" |

### 10.8. Lista partii i ubój
| `TListaPartiiForm` | listapartii, tabele, operators, Article | R | sortowanie po `REP_TITLE_MIA` |
| `TEditPartBatchForm` | listapartii | R/W | edycja partii; „Wybierz kierunek partii początkowej", „Zamknij partię" |
| `TEditLiveWeightForm` | listapartii/wagi | R/W | edycja wagi żywej |

### 10.9. Chłodnia / stany / dokumenty magazynowe
| `TRefDocForm`, `TRefDocFiltrForm`, `TRefDocRptForm`, `TRefDocWholeRptForm` | (dynamiczne) | R | dokumenty ruchów (filtr + raport całościowy) |
| `TRefStateForm`, `TStateForm` | (dynamiczne) | R | stan magazynu/chłodni |
| `TRefRepDlgForm`, `TRefRepDetForm`, `TRefRepSumForm` | — | R | raporty (dialog/szczegóły/zbiorcze) |
| `TDokMagForm` | (dokumenty magazynowe) | R/W | „Magazynie Zwrot" |
| `TDirectionForm` | tabele (REP_DIR) | R | kierunki/działy (DIR_ID) |

### 10.10. Skup żywca
| `TSkupyListForm`, `TSkupyDetailForm`, `TSkupyDlgForm`, `TSkupyRptForm` | Skupy, Dostawcy | R/W + raport |

### 10.11. HACCP i jakość
| `THACCPForm` | (HACCP) | R/W | Rejestr HACCP |

### 10.12. Administracja
| `TOperatorListForm`, `TOperatorDetailForm` | operators | R/W | `OperatorListQuery` |
| `TAccessForm` | dostep | R/W | kopiowanie uprawnień |
| `TPasswordForm` | operators/dostep | W | zmiana hasła (CryptoAPI), `PasswordChange` |

### 10.13. Narzędzia / dialogi / infrastruktura
`TConversionForm` (konwersja jednostek), `TDTDateForm` (data), `TSearchDlg` („Kierunek przeszukiwania"),
`TClipboardForm`, `TFileForm`, `TMessageForm`, `TPopupForm`, `TCalculatedPricesForm` (przeliczone ceny),
`TBaseListForm`/`TDetailForm` (klasy bazowe), `TToolDockForm`/`TCustomDockForm` (dokowanie),
kreatory QuickReport (`TQRLabelEditorForm`, `TQRExprEditorForm`, `TQRPreview`, `TQRProgressForm`,
`TSummaryQReportForm`, `TDetailQReportForm`, `TReportForm`, `TRaportyForm`).

---

<a name="11-datasety"></a>
## 11. Słownik datasetów ADO → tabele → kolumny

> ✅ Mapowanie obiektów `TADODataSet`/`TADOQuery` na tabele (z nazw pól trwałych). Przydatne, gdy
> w bazie spotkasz kolumnę i chcesz wiedzieć, który formularz Libry ją wypełnia.

| Dataset | Tabela źródłowa | Kluczowe pola |
|---|---|---|
| `ADOHodowcy` | Dostawcy | GID, ID, ShortName, Name, NIP, PriceType(ID), Addition, Loss, Address, City, PostalCode, Phone, Distance, IsFarmAddress, Halt |
| `ADOHodowcyCalc` | FarmerCalc | (pełna lista — patrz 8.2: Number, CalcDate, CarLP, Weights, Vet*, DeclI1-6, Price*, Pay*, Status, Partia) |
| `ADODriver` | Driver | GID, Name, Halt, Created, Modified |
| `ADOCarTrailer` | CarTrailer | ID, Kind, KindText, Created, Modified |
| `ADOListQuery` | (lista partii/ubój) | ID, IDT, Name, DirID, DirIDL, TruckType, TacaWeight(H), Weight(D) |
| `PlanProdDSet` | HeaderDocIn3A | DocNumber, Partia, CustomerID, CustomerName, EstWeight, StartData, IsClose, OrderNo, Forced, SUMACALA, SUMAPRZYP |
| `PlanProdDetDSet` | DocIn3A | ArticleID, ArticleName, JM, OrdWeight, Weight, Uwagi |
| `ReceiptsListDSet` | RecHeader | GUID, RecID, RecName, ArticleID, ArticleName, Create/Modification Data+Godzina, Efficiency, SUMACALA, SUMAPRZYP |
| `ReceiptDSet` | RecDoc | GUID, RecID, RelatedRecNo, MaterialID, MaterialName, JM, Kind, MustWeight, UsageKg, UsageProc, Create/Modification Data+Godzina |
| `OperatorListQuery` | operators | GUID, ID, Name, Access, Create/Modification Data+Godzina |

---

<a name="12-sql"></a>
## 12. Pełna inwentaryzacja SQL (surowe zapytania wyekstrahowane z binarki)

> ✅ Wszystkie poniższe pochodzą wprost z `Raporty.exe`. `…` = miejsce, gdzie Delphi doklejał dynamiczny
> WHERE/ORDER w runtime. `:Param` = parametry ADO.

### 12.1. SELECT — dane master
```sql
SELECT h.GID,h.ID,h.ShortName,h.Name,h.Nip,h.PriceTypeID,h.Addition,h.Loss,h.Address,h.PostalCode,
  h.City,h.ProvinceID,h.Distance,h.Phone1,h.Phone2,h.Phone3,h.Info1,h.Info2,h.Info3,h.IsFarmAddress,
  h.Email,h.AnimNo,h.IRZPlus,h.IncDeadConf,cast(h.Halt as bit) as Halt,h.Regon,h.Pesel,h.IDCard,
  h.IDCardDate,h.IDCardAuth,h.Created,h.Modified FROM Dostawcy h …

SELECT GID,Name,Address,PostalCode,City,ProvinceID,Distance,Phone1,Info1,AnimNo,IRZPlus,Halt,DefAdr
  FROM DostawcyAdresy …
SELECT GID,Address,PostalCode,City FROM DostawcyAdresy WHERE CustomerGID=…

SELECT GID,Name,Halt,Created,Modified FROM Driver …                        ORDER BY Kind, GID
SELECT ID,Kind,case when Kind=1 then 'auto' when Kind=2 then 'naczepa'
  else '' end as KindText,Created,Modified …

SELECT ID, Name, JM, Cena1, Grupa1, Przelicznik FROM Article WHERE (Halt=0) …
Select ID, Name, JM, Upper(Name) CName, Grupa1 From Article …                Order by CName
```

### 12.2. SELECT — rozliczenia (FarmerCalc, najszersze)
```sql
SELECT h.ID,h.Number,h.YearNumber,h.CalcDate,h.CarLP,h.Status,h.CustomerGID,h.AddressGID,
  ds.Name as CustomerName,ds.Address1,ds.Address2,ds.AnimNo,h.PriceTypeID,h.Addition,h.Loss,
  h.IncDeadConf,h.Price1,h.Price2,h.CarID,h.TrailerID,h.DriverGID,dr.Name as DriverName,
  h.FullDate,h.FullWeight,h.FullUser,h.EmptyDate,h.EmptyWeight,h.EmptyUser,h.FullFarmWeight,
  h.EmptyFarmWeight,h.StartDate,h.StopDate,h.StartKM,h.StopKM,h.VetMedDate,h.VetNo,h.VetRate0,
  h.VetRate1,h.VetRate2,h.VetDate,h.VetUser,h.VetComment,h.DeclI1..h.DeclI6,h.LumQnt,h.ProdQnt,
  h.Partia,h.Price,h.AvgWgt,h.PayWgt,h.PayNet,pt.Name as PriceType,h.Created,h.CreatedBy,
  h.Modified,h.ModifiedBy …
  -- wariant z licznikiem ważeń:
  … w.Quantity as WagoCnt … WagoCounter w on w.CalcDate=h.CalcDate and w.CarLP=h.CarLP WHERE h.ID=…
  ORDER BY h.CalcDate,h.CarLP,h.ID
```

### 12.3. SELECT — przyjęcia 3A (agregacja zamówiono vs zważono)
```sql
Select d.ArticleID, d.ArticleName, d.JM, SUM(d.OrdWeight) as OrdWeight, SUM(d.Weight) as Weight, '' as Uwagi
  from HeaderDocIn3A h, DocIn3A d
  Where h.StartData >= :StartData And h.StartData <= :StopData And h.CustomerID = :CustomerID
    And h.DocNumber = d.DocNumber                Group By d.ArticleID, d.ArticleName, d.JM

Select h.CustomerID, h.CustomerName, d.ArticleID, d.ArticleName, d.JM, SUM(d.OrdWeight) as OrdWeight
  from HeaderDocIn3A h, DocIn3A d Where h.DocNumber = :DocNumber And h.DocNumber = d.DocNumber
  Group By h.CustomerID, h.CustomerName, d.ArticleID, d.ArticleName, d.JM

Select h.Forced, h.DocNumber, h.Partia, h.CustomerID, h.CustomerName, h.EstWeight, h.StartData, h.IsClose, h.OrderNo,
  (Select SUM(d.OrdWeight) from DocIn3A d with (nolock)
     Where d.DocNumber=h.DocNumber and Substring(d.ArticleID,1,1)='9' and Lower(d.JM)='kg') as SUMAPRZYP,
  (Select SUM(d.OrdWeight) from DocIn3A d with (nolock)
     Where d.DocNumber=h.DocNumber and Lower(d.JM)='kg') as SUMACALA
  from HeaderDocIn3A h …                          Order By StartData, …
```

### 12.4. SELECT — receptury / zużycie surowca
```sql
Select h.*,
  (Select SUM(d.UsageKG) from RecDoc d with (nolock)
     Where d.GUID_H=h.GUID and Substring(MaterialID,1,1)='9' and Lower(d.JM)='kg') as SUMAPRZYP,
  (Select SUM(d.UsageKG) from RecDoc d with (nolock)
     Where d.GUID_H=h.GUID and Lower(d.JM)='kg') as SUMACALA
  from RecHeader h with (nolock) Order By …
Select d.*, Coalesce(cast(NullIf(Coalesce(cast(NullIF(d.MustWeight,0) as char(3)),'Nie'),'1')
  as char(3)),'Tak') as cMustWeight from RecDoc d with (nolock) Where d.GUID_H = :GUID_H …
Select Sum(d.Usagekg * a.Cena1) as Wartosc …      -- wartość zużycia po cenie 1
Select Sum(d.Usagekg * a.Cena2) as Wartosc …      -- wartość zużycia po cenie 2
```

### 12.5. SELECT — wydajność / uzysk, ważenia, skup, partie
```sql
-- Wydajność (input z widoku 1A vs output)
select i.p2, o.articleID, oa.name as articlename, sum(o.weight) as sumaoutkg, avg(i.sumakg) as sumainkg, …
  from v_in1a_p2 i … PartNormDetail n on n.ArticleID=o.ArticleID and n.NormNo=…
  group by o.articleID, i.p2, oa.name           order by i.p2, oa.name

-- Ważenia stacji
Select s.GUID, s.ArticleID, s.ArticleName, s.Partia, s.ActWeight, s.JM, s.Quantity, s.AktData,
  s.AktGodzina, o.NrUboju, o.IndNo from …        (JOIN operators o)

-- Skup żywca z ręki
select p.CreateData, p.CustomerID, p.IndNo, p.CarcassClass, p.LiveWeight, p.HandPrice,
  d.[Name] as Skupowy, s.[Name] as Skup …        (JOIN Dostawcy d, skupy s)
  group by p.CreateData, p.CustomerID, p.IndNo, p.CarcassClass, p.LiveWeight, p.HandPrice, d…

-- Lista partii (z metadanymi tabele)
select l.*, t.Rep_TITLE_MIA, a.Name as ArticleName, o1.Name as Operator1Name …
  from tabele t, listapartii l …                 order by t.REP_TITLE_MIA
select distinct t.ID, t.Kind, t.REP_TITLE_MIA …

-- Kafelki
SELECT GroupID,Name FROM ArtPartitionH WHERE Zestaw=…             ORDER BY GroupID
SELECT Position,ID,Name,Img FROM ArtPartitionD WHERE Zestaw=…      ORDER BY Position

-- Uprawnienia
select d.mainaccess / d.menuaccess / d.access2 from dostep d where d.username='…'

-- Przeglądarka (dump całych tabel)
select * from HeaderDocIn3A | HeaderDocOut0E | RecHeader | docin3a | docout0e | recdoc |
  out4a | skupy | operators | kontrahenci | scaletypes | article | tomek_swag_wazenia …
```

### 12.6. INSERT (komplet — 6)
```sql
INSERT INTO ArtPartitionH (Zestaw,GroupID,Name) VALUES (…)
INSERT INTO ArtPartitionD (Zestaw,GroupID,Position,ID,Name) VALUES (…)
INSERT INTO DostawcyAdresy(CustomerGID,Kind,Name,Address,PostalCode,City,ProvinceID,Distance,Phone1,
  Info1,AnimNo,IRZPlus,Halt,DefAdr,Deleted,Created,CreatedBy,Modified,ModifiedBy) …
INSERT INTO ITDevices(DeviceKind, DeviceID, TerminalID, DeviceName, DeviceIP, DeviceMAC, Description,
  ProgVer, Created, Modified) …
insert into pm_swag_wazenia            select * from tomek_swag_wazenia
insert into pm_swag_wazenia_zakonczone select * from tomek_swag_wazenia_zakonczone
```

### 12.7. UPDATE (komplet — 12)
```sql
UPDATE ArtPartitionH SET Name = '…'
UPDATE ArtPartitionD SET ID = '…'   /  SET Name = '…'  /  SET Img = :Img WHERE Zestaw=…  /  SET Img = NULL WHERE Zestaw=…
UPDATE DostawcyAdresy SET Name=:Name,Address=:Address,PostalCode=:PostalCode,City=:City,ProvinceID=:ProvinceID,
  Distance=:Distance,Phone1=:Phone1,Info1=:Info1,AnimNo=:AnimNo,IRZPlus=:IRZPlus,Halt=:Halt,
  Modified=GetDate(),ModifiedBy=:ModifiedBy WHERE GID=…
UPDATE DostawcyAdresy SET Deleted=1,Modified=GetDate(),ModifiedBy=:ModifiedBy WHERE GID=…
UPDATE DostawcyAdresy SET Deleted=1 WHERE CustomerGID=…
UPDATE Driver     SET Deleted=1,Modified=GetDate(),ModifiedBy=:ModifiedBy WHERE GID=…
UPDATE FarmerCalc SET CarLP=…
UPDATE FarmerCalc SET Deleted=1,Modified=GetDate(),ModifiedBy=:ModifiedBy WHERE ID=…
update dostep set mainAccess=(select d.mainaccess from dostep d where d.username='…'), menuaccess=…, access2=…
```

### 12.8. DELETE (komplet — 6)
```sql
DELETE FROM ArtPartitionD WHERE Zestaw=…
DELETE FROM CarTrailer    WHERE ID = '…'        -- twardy
DELETE FROM Dostawcy      WHERE GID = …          -- twardy (master!)
delete from In8A    where GUID = '…'             -- korekta ważenia
delete from Out4A   where GUID = '…'             -- korekta ważenia
delete from State8A where GUID = '…'             -- korekta stanu
```

### 12.6b. SELECT — rozliczenia z nowoczesnym LEFT JOIN (FarmerCalc)
```sql
-- Libra używa też poprawnych LEFT JOIN-ów (nie tylko przecinkowych):
... FROM FarmerCalc h
     LEFT JOIN Dostawcy d  on d.GID  = h.CustomerGID
     LEFT JOIN PriceType pt on pt.ID = h.PriceTypeID …
... FROM FarmerCalc h
     LEFT JOIN Dostawcy ds on ds.GID = h.CustomerGID
     LEFT JOIN Driver dr   on dr.GID = h.DriverGID
     LEFT JOIN PriceType pt on pt.ID = h.PriceTypeID …
```

### 12.9. Charakterystyka stylu SQL Libry
- ✅ **Parametryzacja** dla danych master (`:Name`, `:ModifiedBy`, `:CustomerID`, `:StartData`, `:GUID_H`, `:Img`).
- ⚠️ **Konkatenacja** dla kluczy w `WHERE x = '…'` (GID/ID/GUID/username) — ryzyko niskie (klucze), wzorzec niebezpieczny.
- ⚠️/✅ **JOIN-y MIESZANE** — to ważna korekta: rozliczenia (`FarmerCalc`) używają **nowoczesnych `LEFT JOIN`**,
  natomiast raporty 3A/receptur używają **starych przecinkowych** (`FROM HeaderDocIn3A h, DocIn3A d WHERE …`).
  Czyli kod ewoluował: nowsze ścieżki = LEFT JOIN, starsze = ANSI-89.
- ✅ **`WITH (NOLOCK)`** w raportach (brudne odczyty — typowe dla raportowania na produkcji).
- ✅ **Korelowane podzapytania** dla agregatów (`SUMAPRZYP`/`SUMACALA` jako sub-SELECT per dokument).
- ✅ **Audyt w schemacie:** `Created/CreatedBy/Modified/ModifiedBy` + soft-delete `Deleted=1` w master.
- ⚠️ **Twarde DELETE** na `Dostawcy`/`CarTrailer` (bez soft-delete) i na ważeniach (`In8A`/`Out4A`/`State8A`) — kasowanie bez audytu.

---

<a name="13-logika"></a>
## 13. Logika biznesowa z komunikatów programu

✅ Polskie komunikaty wyekstrahowane z binarki ujawniają reguły i walidacje:

**Cykl życia partii / zadania (ubój → produkcja):**
- „Czy na pewno chcesz **stworzyć** / **otworzyć** / **zamknąć**…" — partie/zadania mają stany open/close (`listapartii.IsClose`).
- „**Zamknij partię**", „**Zamknij zadanie**", „**Zamknij wszystkie zadania dla okresu**", „Zadanie nr …".
- „**Wybierz kierunek partii początkowej**" — wybór `DIR_ID` (działu) przy tworzeniu partii.
- „**Wybierz asortyment**", „asortymentu nie znaleziono" — wybór artykułu z `tabele`/`Article`.
- „Dane partii początkowej…", „Dane zaktualizowano".

**Walidacje (lookupy):**
- „**Nie znaleziono dostawcy / kierowcy / auta / dokumentu!**" — walidacja FK przy rozliczeniu.
- „**Brak numeru partii!**", „**Nie nadano numeru dokumentu!**", „Brak partii nr …".
- „**Brak uprawnień**" — kontrola `dostep`.

**Produkcja / wydajność:**
- „**Brak tego farszu na magazynie porozbiorowym**" — kontrola stanu farszu (mag. porozbiorowy).
- „farsz **homogenizowany**", „farsz **podrobowy**" — rodzaje farszu.
- „**UWAGA! Suma surowca w kilogramach wynosi zero!**" — guard przy liczeniu uzysku.
- „**UWAGA! Wydajność…**", „**Nie przeliczone**" — kontrola obliczeń wydajności.
- „**Ubytek obliczony** / **Ubytek ustalony**", „**Ubytek KG**", „**Konfiskaty KG**", „Tusza klasy …".
- „**Magazynie Zwrot**" — magazyn zwrotów.

🔶 Te komunikaty potwierdzają, że Libra **egzekwuje integralność procesu na hali** (nie pozwoli rozliczyć
bez dostawcy/auta/kierowcy, nie domknie partii bez przeliczenia wydajności, pilnuje stanu farszu).

---

<a name="14-lancuch"></a>
## 14. Łańcuch danych: od piskląt do rozliczenia

🔶 Rekonstrukcja pełnego przepływu (tabele + formularze + komunikaty):

```
0. ASORTYMENT/MENU   → tabele (REP_DIR/GROUP/MENU/ORDER + tytuły) steruje co widać i jak się nazywa
1. HODOWCA           → Dostawcy + DostawcyAdresy (THodowcyForm) — fermy, AnimNo, cennik indywidualny
2. PLAN DOSTAWY      → HarmonogramDostaw  ⚠️ TWORZY ZPSP, nie Libra (Libra nie ma planu dostaw)
3. ODBIÓR/TRANSPORT  → FarmerCalc (ważenie pełne/puste auta + waga u hodowcy, KM, weterynaria, sztuki)
                       ↳ WagoCounter (liczba ważeń); walidacja dostawca/auto/kierowca
4. PRZYJĘCIE 3A      → HeaderDocIn3A + DocIn3A (OrdWeight zamówiono vs Weight zważono; SUMAPRZYP/SUMACALA)
5. UBÓJ (partia UBOJOWA) → listapartii (DIR_ID=1A, Partia, IsClose) + UbojTable/Ubojnia/TTransUbojTable
                       ↳ ważenia stacji In8A/Out4A/State8A (ActWeight, Quantity)
                       ↳ widok v_in1a_p2 agreguje wejścia per partia → „Wydajność poubojowa"
5b. ROZBIÓR (partia ROZBIOROWA) → listapartii (DIR_ID=0K krojenie) — odrębny typ partii
                       ↳ „Kafelki na rozbiorze" (ArtPartition na stanowisku rozbioru)
                       ↳ 🔶 obsługa też rozbioru wieprzowego („Kalkulacja rozbioru wieprzowego")
6. PRODUKCJA         → RecHeader + RecDoc (zużycie: farsz homogenizowany/podrobowy, UsageKg, UsageProc)
                       ↳ Dodatki7 (przyprawy, MaterialID '9%')
                       ↳ Peklownia / Wedzarnia (ubytki) / Pakownia
                       ↳ ArtPartition (kafelki produktów na stanowiskach)
7. WYDAJNOŚĆ/UZYSK   → PartNorm/PartNormDetail: sum(out kg)/avg(in kg) vs norma; „przeliczone"/„nie przeliczone"
8. CHŁODNIA/STANY    → dokumenty Ref* + State* + DokMag (magazyn porozbiorowy, zwroty)
9. ROZLICZENIE       → FarmerCalc: Price/PayWgt/PayNet (− Loss, ± Addition, IncDeadConf) → wypłata hodowcy
10. WYDRUKI/EXPORT   → QuickReport (*RptForm) + eksport Excel (sbtnExcell) + e-mail (MAPI)
```

**Kluczowa obserwacja:** kroki 3–9 to **wyłączna domena Libry** (ZPSP ich nie zapisuje). ZPSP wchodzi
w kroku 2 (`HarmonogramDostaw` — plan dostaw, którego Libra NIE ma) i nadbudowuje analitykę/CRM/sprzedaż
nad danymi z kroków 3–9.

---

<a name="15-relacje"></a>
## 15. Relacje między tabelami (JOIN-y obserwowane w binarce)

```
Dostawcy.GID            1───N  DostawcyAdresy.CustomerGID
Dostawcy.GID            1───N  FarmerCalc.CustomerGID
Dostawcy.PriceTypeID    N───1  PriceType.ID
FarmerCalc.AddressGID   N───1  DostawcyAdresy.GID
FarmerCalc.DriverGID    N───1  Driver.GID
FarmerCalc.(CarLP,CalcDate) ─1 WagoCounter.(CarLP,CalcDate)
HeaderDocIn3A.DocNumber 1───N  DocIn3A.DocNumber
RecHeader.GUID          1───N  RecDoc.GUID_H
RecDoc.MaterialID       N───1  Article.ID  (wartość = UsageKg × Cena)
ArtPartitionH.Zestaw    1───N  ArtPartitionD.Zestaw
PartNormDetail.ArticleID ──    Article.ID   (norma per artykuł, klucz NormNo)
v_in1a_p2.p2            ──     listapartii.Partia  (uzysk: wejścia per partia)
Out4A.ActWeight / Partia ──    listapartii.Partia
tabele.(ID,Kind,REP_*)  ──     listapartii / Article  (asortyment + tytuły raportów)
operators.ID            ──     listapartii.CreateOperator / Out4A.(NrUboju,IndNo)
operators.username      1───1  dostep.username
Skupy.CustomerID        N───1  Dostawcy
```

---

<a name="16-integracje"></a>
## 16. Eksport i integracje (Excel / e-mail / sieć)

| Integracja | Mechanizm (✅) | Uwagi |
|---|---|---|
| **Excel** | OLE Automation (`Excel.Application`, `Excel2000`, `TExcelApplication`) | przycisk `sbtnExcell` na listach; komunikat „Excel nie zainstalowany." gdy brak |
| **E-mail** | MAPI (`MAPI32.DLL`) | 🔶 wysyłka raportów/rozliczeń |
| **Sieć** | Indy (sockety, IPv6, DNS) | 🔶 komunikacja z terminalami/wagami (`ITDevices` IP/MAC) |
| **Wydruki** | QuickReport 4.06.4 → drukarka/podgląd | wszystkie `*RptForm`/`*ReportForm`, `sbtnPreview` |
| **Schowek** | `TClipboardForm` | kopiowanie danych |
| **Pliki** | `TFileForm`, `comdlg32` | dialogi otwórz/zapisz |

---

<a name="17-granica"></a>
## 17. Granica Libra ↔ ZPSP — kto czego dotyka

| Tabela | Właściciel/twórca | Libra | ZPSP (#plików) | Status |
|---|---|---|---|---|
| `Dostawcy` | Libra | R/W/D | 91 | współdzielone (Libra pisze master) |
| `operators` | Libra | R/W | 72 | współdzielone |
| `HarmonogramDostaw` | **ZPSP** | — | 55 | **tylko ZPSP** (Libra nie ma planu dostaw!) |
| `FarmerCalc` | Libra | R/W/D | 49 | ⚠️ **dwustronny zapis** |
| `kontrahenci` | Libra | R | 25 | współdzielone |
| `Article` | HANDEL→Libra | R | 24 | współdzielone |
| `Driver` | Libra | R/W/D | 23 | współdzielone |
| `listapartii` | Libra | R/W | 14 | współdzielone (ZPSP dodał StatusV2/HarmonogramLp) |
| `CarTrailer` | Libra | R/W/D | 5 | współdzielone |
| `RecDoc`/`RecHeader` | Libra | R/W | **0** | 🔴 wyłącznie Libra |
| `ArtPartition*` | Libra | R/W/D | **0** | 🔴 wyłącznie Libra |
| `DocIn3A`/`HeaderDocIn3A` | Libra | R | **0** | 🔴 wyłącznie Libra |
| `In8A`/`Out4A`/`State8A` | Libra | R/D | **0** | 🔴 wyłącznie Libra |
| `Skupy` | Libra | R/W | **0** | 🔴 wyłącznie Libra |
| `PartNorm*` | Libra | R | **0** | 🔴 wyłącznie Libra |
| `Dodatki7` | Libra | R | **0** | 🔴 wyłącznie Libra |
| `ITDevices`/`scaletypes` | Libra | R/W | **0** | 🔴 wyłącznie Libra |
| `dostep` | Libra | R/W | **0** | 🔴 wyłącznie Libra |
| `tabele` | Libra | R | **0** | 🔴 wyłącznie Libra |

**Reguła dla Claude Code:** jeśli zadanie dotyczy tabel 🔴 — **to są dane Libry, nie ZPSP**. ZPSP może je
co najwyżej **czytać** (raporty/analiza), ale ich **nie tworzy i nie powinien nadpisywać** bez świadomej
decyzji (Libra je aktywnie zapisuje — kolizja z działającym programem produkcyjnym).

---

<a name="18-ryzyka"></a>
## 18. Ryzyka koegzystencji

1. ⚠️ **`FarmerCalc` — dwustronny zapis.** Jedyna transakcyjna tabela pisana z Libry (edytor rozliczeń)
   ORAZ z ZPSP (49 plików). Ryzyko: kolizja edycji / nadpisanie pól / niespójny `ModifiedBy`.
   Przy zmianach w ZPSP dotykających FarmerCalc — zakładać współbieżny zapis z Libry. ZPSP nie powinien
   nadpisywać pól wagowych/wet, które wypełnia stanowisko wagowe Libry.
2. ⚠️ **Dwa modele uprawnień.** Libra: `dostep` (mainAccess/menuAccess/access2). ZPSP: `accessMap` +
   `userPermissions`. Plus Libra ma własne menu w `tabele` (REP_MENU). Brak synchronizacji — user może mieć
   inne prawa/widoczność w obu systemach.
3. ⚠️ **Higiena poświadczeń — regresja w ZPSP.** Libra **szyfruje** 1 hasło (CryptoAPI). ZPSP trzyma
   `pronova/pronova` (218×) i hasło SA do HANDEL w **plaintext w 91 plikach**. Dług do spłaty przed
   ewentualnym wygaszeniem Libry.
4. ⚠️ **Twarde DELETE bez audytu.** Libra kasuje twardo `Dostawcy`/`CarTrailer` oraz ważenia
   `In8A`/`Out4A`/`State8A` (korekty po GUID). Jeśli ZPSP raportuje z tych tabel — liczyć się ze
   znikającymi rekordami i brakiem śladu kasowania.
5. 🔶 **Zależność egzystencjalna.** Dopóki Libra produkuje ważenia/zużycia/uzyski (`In8A`/`RecDoc`/`PartNorm`),
   analityka ZPSP ma czym się karmić. Wyłączenie Libry = utrata źródła tych danych.
6. 🔶 **Stack schyłkowy.** BDE + SQLOLEDB + QuickReport 4.x to technologie nierozwijane. Każda zmiana w
   Librze wymaga środowiska Delphi/RAD Studio i znajomości QuickReport — kompetencji rzadkich.

---

<a name="19-wygaszenie"></a>
## 19. Mapa wygaszenia Libry (gdyby kiedyś zastąpić)

🔶 Gdyby ZPSP miał kiedyś przejąć rolę Libry, do reimplementacji jest **12 obszarów** (kolejność wg ryzyka):

| # | Obszar | Tabele do przejęcia | Trudność | Uwaga |
|---|---|---|---|---|
| 1 | Integracja z wagami/terminalami | ITDevices, scaletypes, In8A/Out4A/State8A | 🔴 wysoka | wymaga protokołu sieciowego wag (Indy) — sprzęt |
| 2 | Ważenie produkcyjne | In8A/Out4A/State8A, WagoCounter | 🔴 wysoka | rdzeń pomiaru — sprzęt + UI stanowiskowe |
| 3 | Rozliczenia hodowców (pełne) | FarmerCalc (już R/W w ZPSP) | 🟡 średnia | ZPSP już pisze; dokończyć logikę wag/wet |
| 4 | Receptury / farsz | RecHeader, RecDoc, Dodatki7 | 🔴 wysoka | logika UsageKg/UsageProc, farsz hom./podrob. |
| 5 | Normy uzysku / wydajność | PartNorm, PartNormDetail, v_in1a_p2 | 🟡 średnia | algorytm sum(out)/avg(in) vs norma |
| 6 | Przyjęcia 3A | HeaderDocIn3A, DocIn3A | 🟡 średnia | OrdWeight vs Weight |
| 7 | Kafelki produktów | ArtPartitionH/D (+Img) | 🟢 niska | UI dotykowe |
| 8 | Skup z ręki | Skupy | 🟢 niska | prosty CRUD |
| 9 | Chłodnia/stany/dok. mag. | Ref*/State*/DokMag | 🟡 średnia | nazwy tabel do ustalenia |
| 10 | HACCP | (HACCP) | 🟢 niska | rejestr |
| 11 | Uprawnienia + menu | dostep, tabele | 🟡 średnia | ujednolicić z accessMap |
| 12 | Wydruki QuickReport | (szablony) | 🔴 wysoka | przepisać wszystkie raporty |

**Najtwardszy orzech:** #1/#2 — integracja sprzętowa z wagami (protokół, terminale `ITDevices`). To nie jest
problem softu, tylko sprzętu i protokołów wagowych. Bez tego ZPSP nie zastąpi Libry na hali.

---

<a name="20-slowniczek"></a>
## 20. Słowniczek pól, skrótów i pojęć

| Pole/skrót | Znaczenie |
|---|---|
| `GID` | Global ID (PK GUID-podobny) hodowcy/kierowcy/adresu |
| `CarLP` | Numer porządkowy auta w dniu (klucz do WagoCounter) |
| `FullWeight`/`EmptyWeight` | Waga auta pełnego/pustego (na wadze zakładu) |
| `FullFarmWeight`/`EmptyFarmWeight` | Waga u hodowcy (na fermie) |
| `DeclI1..DeclI6` | Sztuki deklarowane w klasach; `DeclI3+4+5` = konfiskaty (SumKonf) |
| `Addition`/`Loss` | Dodatek do ceny / ubytek (strata %) |
| `IncDeadConf` | Include Dead+Confiscated — uwzględniaj padłe/konfiskaty (bit) |
| `PayWgt`/`PayNet` | Waga/kwota do zapłaty hodowcy |
| `LumQnt`/`ProdQnt`/`ProdWgt` | Ilość ryczałtowa / produkcyjna / waga produkcyjna |
| `AvgWgt` | Średnia waga sztuki |
| `VetRate0/1/2`, `VetNo`, `VetMedDate` | Stawki/numer/data weterynaryjne |
| `OrdWeight`/`Weight` | Waga zamówiona / zważona (3A) |
| `EstWeight` | Waga szacowana (nagłówek 3A) |
| `Forced` | Wymuszenie dokumentu (bit) |
| `UsageKg`/`UsageProc` | Zużycie surowca w kg / w % (RecDoc) |
| `MaterialID '9%'` | Materiały zaczynające się od „9" = przyprawy/dodatki |
| `MustWeight` | Czy pozycja wymaga obowiązkowego ważenia |
| `Efficiency` | Wydajność/uzysk (RecHeader) |
| `ActWeight` | Waga rzeczywista (ważenie stacji) |
| `NormNo` | Numer normy uzysku (PartNorm) |
| `CarcassClass` | Klasa tuszki (skup) |
| `LiveWeight`/`HandPrice` | Waga żywa / cena ręczna (skup) |
| `Halt` | Zablokowany/wstrzymany (bit) |
| `Deleted` | Soft-delete (bit) |
| `DIR_ID` | Dział/kierunek: `1A`=ubój, `0E`=mrożenie, `0K`=krojenie, `3A`=przyjęcia |
| `Zestaw` | Identyfikator zestawu kafelków produktów |
| `REP_TITLE_MIA/MIE/B/C/D/N` | Warianty tytułu raportu (🔶 przypadki gramatyczne PL) |
| `REP_DIR/GROUP/MENU/ORDER` | Konfiguracja menu/raportów w `tabele` |
| `1A`/`3A`/`4A`/`8A`/`0E`/`0K` | Oznaczenia stacji/działów/strumieni w LibraNet |
| Farsz homogenizowany / podrobowy | Rodzaje farszu (RecDoc) |
| Mag. porozbiorowy / Mag. Zwrot | Magazyny: po rozbiorze / zwrotów |
| SWAG | 🔶 nazwa stanowiska/skanera ważenia (`*_swag_wazenia`) |
| BDE (`IDAPI32`) | Borland Database Engine — legacy warstwa danych |

---

<a name="21-jak-uzywac"></a>
## 21. Jak Claude Code ma używać tej wiedzy

1. **Pytanie o pochodzenie danych w LibraNet** → sekcja 8 (katalog tabel) + 17 (granica). Tabela 🔴
   „wyłącznie Libra" = dane tworzy program zewnętrzny; ZPSP tylko czyta.
2. **Zmiana w ZPSP dotykająca `FarmerCalc`** → ryzyko #1. Załóż współbieżny zapis z Libry; nie nadpisuj
   pól wagowych/wet wypełnianych przez stanowisko wagowe.
3. **„Skąd się biorą ważenia / uzyski / zużycie surowca"** → sekcja 14 (łańcuch) + tabele 8.4–8.8.
4. **Pomysł na nowy moduł produkcyjny w ZPSP** → najpierw sprawdź, czy Libra już tego nie robi (sekcja 10).
   Duplikacja zapisu do tych samych tabel = konflikt z działającym programem.
5. **Pytanie o uprawnienia / menu** → dwa modele (sekcja 6 + `tabele` sekcja 7 + ryzyko #2).
6. **Plan wygaszenia / przejęcia Libry** → sekcja 19 (mapa 12 obszarów + trudność).
7. **Nazwa kolumny w bazie, nie wiesz skąd** → sekcja 11 (datasety ADO → tabele → kolumny) + 20 (słowniczek).
8. **Diagnoza znikających rekordów ważeń** → ryzyko #4 (twarde DELETE po GUID, brak audytu).

---

<a name="22-aneks"></a>
## 22. Aneks — surowe dane ekstrakcji

**Statystyki binarki:** 3,63 MB, PE32 i386, 9 sekcji. Wyekstrahowano: **66 SELECT**, **6 INSERT**,
**12 UPDATE**, **6 DELETE**, **~84 klasy formularzy aplikacji**, ~30 importów DLL, dziesiątki obiektów pól
trwałych ADO (mapa kolumn), kilkadziesiąt handlerów zdarzeń, ~50 polskich komunikatów UI.

**Lista wszystkich klas formularzy aplikacji (✅, alfabetycznie):**
TAccessForm, TArticleDetailForm, TArticleListForm, TArticleListQForm, TBaseListForm, TCalculatedPricesForm,
TCarTrailerEditForm, TCarTrailerForm, TClipboardForm, TConversionForm, TDTDateForm, TDetailForm,
TDetailQReportForm, TDirectionForm, TDodatkiForm, TDokMagForm, TDriverEditForm, TDriverForm,
TEditLiveWeightForm, TEditPartBatchForm, TFarmerCalcEditForm, TFarmerCalcForm, TFarmerCalcRepAvilogForm,
TFarszProdForm, TFileForm, THACCPForm, THodowcyAdresyForm, THodowcyCenyForm, THodowcyEditForm, THodowcyForm,
TKontrahentDetailForm, TKontrahentListForm, TListaPartiiForm, TMessageForm, TNewDodatkiForm, TNewReceiptsForm,
TOperatorDetailForm, TOperatorListForm, TPakowniaDlgForm, TPakowniaRptForm, TPartitionCalcDlgForm,
TPartitionCalcReportForm, TPartitionKafelkiForm, TPasswordForm, TPeklowniaForm, TPeklowniaReportForm,
TPlanProdMainForm, TPlanProdNewPartDlg, TPlanProdRptForm, TPlanProdRpt2Form, TProductionDlgForm,
TProductionRptForm, TReceiptArticleListForm, TReceiptsForm, TRefDocFiltrForm, TRefDocForm, TRefDocRptForm,
TRefDocWholeRptForm, TRefRepDetForm, TRefRepDlgForm, TRefRepSumForm, TRefStateForm, TSearchDlg,
TSkupyDetailForm, TSkupyDlgForm, TSkupyListForm, TSkupyRptForm, TStateForm, TTruckDetailForm, TTruckListForm,
TWedzarniaUbytkiForm, TWedzarniaUbytkiReportForm, TWydajnoscDlgForm
(+ infrastruktura: TReportForm, TRaportyForm, TSummaryQReportForm, TPopupForm, TToolDockForm, TCustomDockForm)

**Wszystkie tabele/widoki (✅):**
Dostawcy, DostawcyAdresy, Driver, CarTrailer, Article, PriceType, operators, kontrahenci, FarmerCalc,
WagoCounter, HeaderDocIn3A, DocIn3A, HeaderDocOut0E, docout0e, RecHeader, RecDoc, ArtPartitionH,
ArtPartitionD, In8A, Out4A, State8A, Out1A, PartNorm, PartNormDetail, Skupy/skupy, Dodatki7, listapartii,
UbojTable, dostep, tabele, ITDevices, scaletypes, AppSettings, tomek_swag_wazenia(_zakonczone),
pm_swag_wazenia(_zakonczone); widoki: v_in1a_p2, v_in, V_358.
🔶 dodatkowo (z nazw struktur/raportów): `ListaPartiiTable`/`ListaPartiiRow`, `TTransUbojTable` (transfer uboju),
tabela wózków/pojemników (opakowania zwrotne — „Lista wózków i pojemników"), tabela kontrahentów eksportowych
(„Dokumenty eksportowe").

**Producent i rodzina produktów:** Pro-Nova Sp. z o.o. (Poznań) · System wagowy LibraNet (©2003 A. Lochert,
Dariusz J.) · wersja pliku 2.0.3.5 · siostrzany moduł: **LibraNet – Ekspedycja** · stopka wydruków:
„Raport wygenerowany przez System LibraNet".

**Polskie tytuły raportów (✅, ~30):** Rozliczenie hodowcy / Kilogramowe / Avilog / sumacyjne i szczegółowe
uboju · Raport poubojowy (dla skupów) · Raport wybojowy · Raport wydajnościowy / z wydajności · Raport z
ubytków · Raport kalkulacyjny · Kalkulacja rozbioru wieprzowego · Raport sumacyjny/szczegółowy z
przyjęcia/wydania · Specyfikacja Przyjęcia · Stany magazynowe/towarowe · Raport z dnia · Raport całkowity ·
Raport dla Szefa · Dowód przyjęcia/wydania towaru · Listy: asortymentu/kontrahentów/operatorów/partii/skupów/
towarów/wózków i pojemników.

---

**Powiązane dokumenty Bazy Wiedzy:**
- [`19_LibraNet_audyt_uzycia.md`](19_LibraNet_audyt_uzycia.md) — jak ZPSP używa ~65 tabel LibraNet (komplement)
- [`13_Bazy_danych.md`](13_Bazy_danych.md) — 4 bazy firmy
- [`24_Magazyny_i_Lancuch_Produkcji.md`](24_Magazyny_i_Lancuch_Produkcji.md) — łańcuch produkcji od strony HANDEL
- [`18_Analiza_przychodu_szczegoly.md`](18_Analiza_przychodu_szczegoly.md) — In0E/Article/PartiaDostawca
- memory: `reference_libra_raporty_exe`, `reference_libranet_dostawcy_farmercalc`, `reference_listapartii_19_kolumn`

**Metadane analizy:** analiza statyczna binarki (stringi SQL + RTTI + obiekty pól ADO + importy DLL +
komunikaty PL + zasób wersji), 2026-06-02. Brak dostępu do kodu źródłowego Delphi — opisy logiki oznaczone
🔶 są wywnioskowane z nazw tabel/pól/formularzy/komunikatów i kontekstu domenowego.

---

<a name="23-raporty"></a>
## 23. Katalog raportów i dokumentów (QuickReport)

> ✅ Rzeczywiste polskie tytuły wyekstrahowane z binarki (dekodowanie CP1250). To jest realne menu wydruków
> Libry — pokazuje, jakie zestawienia firma generuje z tego systemu.

### 23.1. Raporty rozliczeniowe (hodowcy / ubój)
| Tytuł raportu | Źródło danych | Co pokazuje |
|---|---|---|
| **Rozliczenie hodowcy** | FarmerCalc + Dostawcy | Pełne rozliczenie dostawy (waga, cena, wet, sztuki, do zapłaty) |
| **Rozliczenie Kilogramowe** | FarmerCalc | Rozliczenie wg kg |
| **Rozliczenie Avilog** | FarmerCalc | Wariant dla planowania Avilog |
| **Rozliczenie sumacyjne uboju** | FarmerCalc/listapartii | Zbiorczo per ubój |
| **Rozliczenie szczegółowe uboju** | FarmerCalc/listapartii | Szczegółowo per ubój |
| **Dokument rozliczenia** | FarmerCalc | Dokument pojedynczego rozliczenia |
| **Raport Avilog** | FarmerCalc | Planowanie/rozliczenie dostaw |

### 23.2. Raporty produkcyjne / wydajnościowe
| Tytuł | Źródło | Co |
|---|---|---|
| **Raport poubojowy** / **…dla skupów** | v_in1a_p2 + Out* | Wyniki po uboju (też dla skupów) |
| **Raport wybojowy** | Out* | 🔶 Wybój (rozbiór poubojowy) |
| **Raport wydajnościowy z …** / **Raport z wydajności na …** | PartNorm + In/Out | Uzysk vs norma |
| **Raport z ubytków na …** | Wedzarnia/RecDoc | Ubytki (wędzenie/produkcja) |
| **Raport kalkulacyjny** / **…z …** | RecDoc + Article | Kalkulacja kosztu/zużycia |
| **Kalkulacja rozbioru wieprzowego** | (rozbiór) | 🔶 Rozbiór wieprzowiny |

### 23.3. Raporty przyjęć / wydań / magazynu
| Tytuł | Co |
|---|---|
| **Raport sumacyjny z przyjęcia na …** / **Raport szczegółowy z przyjęcia na …** | Przyjęcia 3A zbiorczo/szczegółowo |
| **Raport sumacyjny z wydania z …** / **Raport szczegółowy z wydania z …** | Wydania |
| **Specyfikacja Przyjęcia** | Specyfikacja dokumentu przyjęcia |
| **Stany magazynowe** / **Stany towarowe na …** | Stan magazynu na dzień |
| **Raport z dnia** | Dzienny przegląd |
| **Raport całkowity** | Zestawienie całościowe |
| **Raport dla Szefa** | 🔶 Zestawienie zarządcze (dla właściciela) |

### 23.4. Dokumenty obrotu (WZ/PZ/eksport)
| Dokument | Typ | Co |
|---|---|---|
| **Dowód przyjęcia towaru na …** | PZ | Przyjęcie na magazyn |
| **Dowód wydania towaru z …** | WZ | Wydanie z magazynu |
| **Dokumenty przyjęcia / wydania towaru** | — | Listy dokumentów |
| **Dokumenty eksportowe** | — | Eksport |
| **Dokumenty magazynowe** | — | Ogólne dokumenty mag. |
| **Lista naważeń przyjęcia / wydania towaru** | — | Lista ważeń per dokument |
| **Dokument otwarto / zamknięto** | — | Cykl życia dokumentu (komunikaty „Dokumentu jeszcze nie zamknięto") |

### 23.5. Listy / słowniki
**Lista asortymentu**, **Lista kontrahentów**, **Lista operatorów**, **Lista partii**, **Lista skupów**,
**Lista towarów**, **Lista wózków i pojemników w Systemie LibraNet** (← zarządzanie opakowaniami zwrotnymi:
wózki/pojemniki/E2).

Stopka każdego wydruku: **„Raport wygenerowany przez System LibraNet"**.

---

<a name="24-etykiety"></a>
## 24. Etykiety kolumn i komunikaty programu (PL)

> ✅ Realne napisy z UI. Pomagają zrozumieć semantykę pól (gdy w bazie widzisz `Number`, na wydruku to „Nr ubojowy").

### 24.1. Etykiety kolumn / pól (grid + raporty)
`Nazwa towaru`, `Indeks towaru`, `Symbol towaru`, `Nr ubojowy` (=NrUboju), `Data utworzenia`,
`Data modyfikacji`, `Data wydruku`, `Data badania` (weterynaryjnego), `Waga całkowita`, `Partia ubojowa`,
`Partia rozbiorowa`, `Dzień skupu`, `Konfiskaty KG`, `Ubytek KG`, `Tusza klasy …`, `pełna konfiskata`,
`bez usług` (rozliczenie bez kosztów usług).

### 24.2. Komunikaty walidacji (reguły wprowadzania danych)
Libra waliduje pola na wejściu (`*KeyPress`/`*Exit`) i pokazuje:
- **Błędna wartość:** `deklaracji`, `KM`, `ceny`, `dodatku`, `ubytku`, `produkcji`,
  `specyfikacji hodowcy`, `specyfikacji ubojni`
- **Wydajność musi być większa od zera**
- **UWAGA! Suma surowca w kilogramach wynosi zero!**
- **Wprowadzona/Wpisana wartość jest nieprawidłowa**
- **Brak numeru partii! / Nie nadano numeru dokumentu! / Brak partii nr …**
- **Nie znaleziono dostawcy / kierowcy / auta / dokumentu!**

### 24.3. Komunikaty operacyjne / cyklu życia
- **Dane zaktualizowano** / **Zaktualizować dane?** / **Czy chcesz zapisać zmiany? / Zmiany nie zostaną zapamiętane**
- **Czy na pewno chcesz stworzyć / otworzyć / zamknąć …** (partia/zadanie)
- **To zadanie jest już zamknięte** / **Dokument zamknięto / otwarto**
- **Zmieniono hodowcę** / **Zmiana hodowcy**
- **Czy na pewno wybrana pozycja ma być usunięta?** / **Podczas usuwania wystąpił błąd**
- **Brak uprawnień / Brak uprawnień do edycji**

### 24.4. Komunikaty błędów technicznych
- **Nie mogę połączyć się z bazą danych** (błąd połączenia ADO)
- **Nie można uzyskać hasła** (błąd deszyfracji CryptoAPI)
- **Błąd pobierania unikalnego identyfikatora** (generowanie ID)
- **Excel nie zainstalowany.** (brak OLE Excel przy eksporcie)

---

<a name="25-erd"></a>
## 25. Diagram ERD (ASCII) — pełny schemat

```
                          ┌──────────────┐
                          │  PriceType   │  (typy cen: świeży/mrożony/korekta)
                          └──────▲───────┘
                                 │ PriceTypeID
          ┌──────────────────────┼───────────────────────────┐
          │                      │                            │
   ┌──────┴───────┐       ┌──────┴────────┐           ┌───────┴────────┐
   │   Dostawcy   │1─────N│ DostawcyAdresy│           │   FarmerCalc   │  ⚠️ R/W z ZPSP
   │ (hodowcy)    │       │  (fermy)      │◄────N──────│ (rozliczenia)  │
   │ GID(PK),NIP, │       │ GID(PK),      │ AddressGID │ ID(PK),Number, │
   │ AnimNo,cennik│       │ CustomerGID FK│            │ wagi,wet,DeclI*│
   └──────┬───────┘       └───────────────┘            │ Pay*,Price*    │
          │ GID                                        └──┬──────┬──────┘
          │ (CustomerGID)                       DriverGID │      │ (CarLP,CalcDate)
          └────────────────────────────────────┐         │      │
                                                │   ┌─────┴───┐ ┌┴───────────┐
   ┌──────────────┐                             │   │ Driver  │ │ WagoCounter│
   │   Skupy      │N──────────► Dostawcy        │   │ GID(PK) │ │ (licznik   │
   │ (skup z ręki)│  CustomerID                 │   └─────────┘ │  ważeń)    │
   │ LiveWeight,  │                             │   ┌─────────┐ └────────────┘
   │ CarcassClass │                             └──►│CarTrailer│
   └──────────────┘                                 │ Kind 1/2 │
                                                     └──────────┘
   ── PRODUKCJA (wyłącznie Libra) ───────────────────────────────────────────
   ┌───────────────┐         ┌─────────────┐        ┌──────────────┐
   │ HeaderDocIn3A │1───────N│   DocIn3A   │        │   RecHeader  │1──┐
   │ (przyjęcia)   │ DocNumber│ Ord/Weight  │        │ Efficiency,  │   │ GUID
   │ Partia,Forced │         │ ArticleID   │        │ SUMA*        │   │ (GUID_H)
   └───────────────┘         └──────┬──────┘        └──────────────┘   │
                                    │ ArticleID            ┌───────────┴──┐
   ┌───────────────┐                ▼                      │    RecDoc    │
   │ ArtPartitionH │1──┐      ┌───────────┐                │ MaterialID,  │N
   │ (zestawy)     │   │Zestaw│  Article  │◄───────────────│ UsageKg/Proc │
   └───────────────┘   │      │ ID(PK),   │  MaterialID    │ MustWeight   │
   ┌───────────────┐   │      │ Cena1/2,JM│                └──────────────┘
   │ ArtPartitionD │N──┘      └─────▲─────┘                ┌──────────────┐
   │ Position,Img  │                │ ArticleID            │PartNorm(Detail)│
   └───────────────┘                └──────────────────────│ NormNo (uzysk)│
                                                            └──────────────┘
   ── WAŻENIA STACJI / UBÓJ ─────────────────────────────────────────────────
   ┌──────────┐  ┌──────────┐  ┌──────────┐     ┌──────────────┐
   │  In8A    │  │  Out4A   │  │ State8A  │     │  listapartii │  (R/W: ZPSP dodał
   │ (wejścia)│  │ (wyjścia)│  │ (stan)   │◄────│ DIR_ID,Partia│   StatusV2,
   │ DEL GUID │  │ActWeight │  │ DEL GUID │ p2  │ IsClose      │   HarmonogramLp)
   └──────────┘  └────┬─────┘  └──────────┘     └──────┬───────┘
                      │ NrUboju,IndNo                  │ CreateOperator
                      └────────────► operators ◄───────┘
                                     │ ID, username
                                     ▼ 1:1
                                  ┌────────┐      ┌─────────┐
                                  │ dostep │      │ tabele  │ (REP_MENU/GROUP/
                                  │mainAcc.│      │ ORDER,  │  ORDER/TITLE_*)
                                  └────────┘      │ DIR     │  steruje menu+raporty
                                                  └─────────┘
   ── KONFIG/URZĄDZENIA ─── ITDevices (IP/MAC/wagi) · scaletypes · AppSettings
```

Legenda: ⚠️ = dwustronny zapis z ZPSP · `DEL GUID` = twarde kasowanie korekt ważeń.

---

<a name="26-przyklad"></a>
## 26. Przykład krok po kroku: rozliczenie hodowcy (FarmerCalc)

🔶 Rekonstrukcja przepływu jednego rozliczenia (na podstawie pól, komunikatów i kolejności handlerów):

```
KROK 1 — Identyfikacja dostawy
  • Operator wybiera hodowcę (edtFarmerName → walidacja „Nie znaleziono dostawcy!")
    → FarmerCalc.CustomerGID = Dostawcy.GID
  • Wybiera auto i naczepę (edtCarID/edtTrailerID → „Nie znaleziono auta!")
    → FarmerCalc.CarID, TrailerID
  • Wybiera kierowcę (edtDriverName) → FarmerCalc.DriverGID = Driver.GID
  • System nadaje Number / YearNumber, CalcDate, CarLP

KROK 2 — Ważenie transportu
  • Waga pełnego auta (sedtFullWeightChange) → FullWeight, FullDate, FullUser
  • Waga pustego auta                         → EmptyWeight, EmptyDate, EmptyUser
  • NetWeight = FullWeight − EmptyWeight   (waga żywca brutto)
  • Opcjonalnie waga u hodowcy: FullFarmWeight / EmptyFarmWeight
  • WagoCounter.Quantity = liczba naważeń (JOIN po CalcDate,CarLP)

KROK 3 — Trasa
  • StartKM/StopKM, StartDate/StopDate (walidacja „Błędna wartość KM")

KROK 4 — Weterynaria
  • VetMedDate, VetNo, VetRate0/1/2, VetDate, VetUser, VetComment
  • Data badania → kontrola dopuszczenia

KROK 5 — Sztuki i konfiskaty (deklaracja)
  • DeclI1..DeclI6 = sztuki w klasach
  • SumKonf = DeclI3 + DeclI4 + DeclI5  (konfiskaty)
  • „pełna konfiskata" jako przypadek skrajny
  • IncDeadConf (z Dostawcy) decyduje, czy padłe/konfiskaty wliczać do rozliczenia

KROK 6 — Cena i potrącenia
  • PriceTypeID → cennik (Price1/Price2 wg typu)
  • Addition (dodatek do ceny), Loss (ubytek/strata %)  — z Dostawcy lub nadpisane
  • Guard: „Suma surowca w kilogramach wynosi zero!" gdy brak wag

KROK 7 — Wynik (do zapłaty)
  • PayWgt = waga do zapłaty (po potrąceniu Loss/konfiskat wg reguł)
  • Price  = cena finalna; PayNet = kwota netto do wypłaty hodowcy
  • AvgWgt = średnia waga sztuki; ProdQnt/ProdWgt = ilość/waga produkcyjna

KROK 8 — Zatwierdzenie i wydruk
  • Status = zamknięte (CalcIsClose); „Dane zaktualizowano"
  • Wydruk: „Rozliczenie hodowcy" / „Dokument rozliczenia" (QuickReport)
  • Eksport Excel (sbtnExcell) lub e-mail (MAPI)
```

> ⚠️ **Dla ZPSP:** kroki 2 (wagi), 4 (wet), 5 (sztuki) są wypełniane przez stanowisko/operatora Libry.
> Jeśli ZPSP pisze do `FarmerCalc`, **nie powinien nadpisywać** tych pól — patrz ryzyko #1 (sekcja 18).
