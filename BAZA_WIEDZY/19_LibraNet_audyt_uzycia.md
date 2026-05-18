# 19 — LibraNet (192.168.0.109): audyt użycia w ZPSP

**Data audytu:** 2026-05-03 (Sergiusz + Claude, na podstawie grepa kodu).

**Zakres:** Wszystkie tabele/widoki/procedury bazy `LibraNet` używane przez `Kalendarz1.csproj` (ZPSP) — ich rola w systemie i tryb dostępu (READ / WRITE).

**Skala:** ~65 tabel, ~15 widoków, 8 stored procedures.

---

## A. Tabele READ-ONLY (głównie raporty/analizy)

| Tabela | Opis (z kontekstu kodu) | Główni "klienci" w kodzie |
|---|---|---|
| **`AppSettings`** | Konfiguracja aplikacji (klucze/wartości) | Cały program |
| **`Article`** | Katalog towarów (synchronizowany z Handel) | Kartoteka, Partie, Analiza Przychodu |
| **`CenaMinisterialna`** | Cennik ministerialny żywca | DashboardPrzychodu, WidokCena |
| **`CenaRolnicza`** | Cennik rolniczy żywca | jw. |
| **`CenaTuszki`** | Cennik tuszki | jw. |
| **`DOSTAWCY`** | Słownik dostawców (write-once) | Rejestr / In0E / Out1A |
| **`GeoCache`** | Cache geolokacji (lat/lon) | MapaFloty, MapaCRM |
| **`In0E`** | **Wejścia ważeń (przyjęcia)** — łączone z `listapartii.Partia` przez `P1` | Analiza Przychodu, Partie |
| **`KolejnoscTowarow`** | Porządek przetwarzania w halach | Plan dnia |
| **`KonfiguracjaProdukty`** | Parametry produktów dla produkcji | Krojenie, Plan dnia |
| **`KonfiguracjaWydajnosc`** | Parametry wydajności (uzyski) | Krojenie 14A |
| **`Out1A`** | **Wyjścia ważeń (sprzedaż w LibraNet — historyczna)** — Sergiusz: "nie używamy, sprzedaż jest w Symfonia 112" | (legacy) |
| **`PriceType`** | Typy cen (świeży / mrożony / korekta) | Cennik |
| **`operators`** | Pracownicy (z `CreateOperator`, `CloseOperator`) | Audit, partie |

---

## B. Tabele READ/WRITE (transakcyjne — kluczowe dla biznesu)

### Partie ubojowe (rdzeń modułu Lista Partii V2)

| Tabela | Operacje | Co tam |
|---|---|---|
| **`listapartii`** | R/W | MASTER tabela partii — `Partia`, `CustomerID`, `CreateData`, `IsClose`, `StatusV2`, `HarmonogramLp`, `DirID` |
| **`PartiaDostawca`** | R/W | `Partia` ↔ `CustomerID`/`CustomerName` |
| **`PartiaStatus`** | R/W | Historia statusów V2 (PLANNED→IN_TRANSIT→AT_RAMP→VET_CHECK→APPROVED→IN_PRODUCTION→PROD_DONE→CLOSED) |
| **`PartiaAuditLog`** | R/W | Log zmian partii |
| **`QC_Normy`** | R/W | Normy QC (TempRampa ≤4°C, KlasaB ≤20%, Przekarmienie, etc.) — INSERT defaults przy inicjacji |
| **`QC_Zdjecia`** | R/W | Zdjęcia wad (linki + metadata) |

### Harmonogram dostaw + farmer calc

| Tabela | Operacje | Co tam |
|---|---|---|
| **`HarmonogramDostaw`** | R/W | Plan dostaw żywca od hodowców (data, ilość, cena) |
| **`HarmonogramDostaw_AuditLog`** | R/W | Audit log harmonogramu |
| **`FarmerCalc`** | R/W | Rozliczenia z hodowcami (cena × kg × szt) |
| **`FarmerCalcChangeLog`** | R/W | Log zmian rozliczeń |
| **`WstawieniaKurczakow`** | R/W | Wstawienia piskląt (data + 35/42 dni → odbiór) |

### Zamówienia mięsa od klientów

| Tabela | Operacje | Co tam |
|---|---|---|
| **`ZamowieniaMieso`** | R/W/D | MASTER zamówień mięsa (klient, data uboju, status) |
| **`ZamowieniaMiesoTowar`** | R/W/D | Linie zamówień (pozycje) |
| **`ZamowieniaMiesoProdukcjaNotatki`** | R/W | Notatki produkcji do zamówienia |
| **`ZamowieniaMiesoSnapshot`** | R/W | Snapshoty zamówień (audit) |
| **`SzablonyZamowien`** + `SzablonyZamowienTowar` | R/W | Szablony powtarzalnych zamówień |
| **`HistoriaZmianZamowien`** | INSERT | Tracking zmian (kto/kiedy/co) |
| **`ZamowienieWydanieRoznice`** | INSERT | Rozbieżności wydania (np. zamówiono 100 kg, wydano 95 kg) |

### Magazyn / wydania / dokumenty WZ

| Tabela | Operacje | Co tam |
|---|---|---|
| **`DokumentyWZ`** | R/W | Dokumenty wydania (linkowane z magazynu) |
| **`StanyMagazynowe`** | R/W | Bieżące stany magazynowe (kg per produkt) |
| **`OdpadyRejestr`** | INSERT | Rejestr odpadów produkcyjnych |
| **`AuditLog_Dostawy`** | INSERT | Log dostaw |

### Transport (uwaga: główne tabele transportu są w `TransportPL`, nie LibraNet!)

| Tabela | Operacje | Co tam |
|---|---|---|
| **`Kierowca`** | R + czasem U | Słownik kierowców (sync z `Driver` z TransportPL) |
| **`Kurs`** | R/W | Kursy (rzadziej — głównie TransportPL.Kurs) |
| **`Ladunek`** | R/W | Ładunki transportu |
| **`MatrycaTransferLog`** | INSERT | Log transferów matrycy |

### Kartoteka odbiorców (CRM klientów)

| Tabela | Operacje | Co tam |
|---|---|---|
| **`KartotekaOdbiorcyDane`** | R/W | Dane klienta (NIP, adres, konta) |
| **`KartotekaOdbiorcyKontakty`** | R/W | Kontakty (telefon, email, osoby kontaktowe) |
| **`KartotekaOdbiorcyNotatki`** | R/W | Notatki swobodne |
| **`KartotekaPrzypomnienia`** | R/W | Przypomnienia (telefonów, ofert) |
| **`KartotekaScoring`** | R/W | Scoring klienta (ile bierze, jak płaci) |
| **`KartotekaHistoriaZmian`** | INSERT | Historia zmian danych klientów |
| **`ContactHistory`** | R/W | Historia rozmów / SMS (CRM) |
| **`SmsHistory`** | INSERT | Wszystkie wysłane SMS |
| **`SmsChangeLog`** | INSERT | Log zmian SMS |

### CRM hodowców (Pozyskiwanie)

| Tabela | Operacje | Co tam |
|---|---|---|
| **`Pozyskiwanie_Hodowcy`** | R/W | 1874 hodowców importowanych z Excela |
| **`Pozyskiwanie_Aktywnosci`** | R/W | Aktywności CRM (rozmowy, oferty, próbne dostawy) |

### Dashboardy / widoki użytkownika

| Tabela | Operacje | Co tam |
|---|---|---|
| **`DashboardWidoki`** | R/W | Spersonalizowane układy dashboardów per user |

### Decision Requests (workflow akceptacji zmian dostawców)

| Tabela | Operacje | Co tam |
|---|---|---|
| **`Dostawcy`** | UPDATE (kredencje) | Słownik dostawców (admin) |
| **`DostawcyCR`** + `DostawcyCRItem` | R/W | Change request dla dostawców (`Status`: `Proposed` → `Zdecydowany`) |
| **`RozliczeniaZatwierdzenia`** | R/W | Akceptacje rozliczeń |

### Avilog (planowanie dostaw żywca)

| Tabela | Operacje | Co tam |
|---|---|---|
| **`AvilogHodowcyMapping`** | R/W | Mapowanie GPS Avilog ↔ hodowcy |

### Towary

| Tabela | Operacje | Co tam |
|---|---|---|
| **`TowarZdjecia`** | R/W | Zdjęcia towarów (linki + metadata) |
| **`ArticleAuditLog`** | INSERT | Log zmian kartoteki |
| **`ArticleFavorites`** | R/W | Ulubione towary użytkownika |

### Feedback / komunikacja

| Tabela | Operacje | Co tam |
|---|---|---|
| **`DostawaFeedback`** | R/W | Feedback do dostaw |
| **`CallReminderLog`** | R/W | Log przypomnień telefonów |
| **`CallReminderContacts`** | R/W | Kontakty do przypomnień |

### Flota (rozszerzenia)

| Tabela | Operacje | Co tam |
|---|---|---|
| **`DriverDetails`** | R/W | Szczegóły kierowców (1:1 z `Driver`) |
| **`VehicleDetails`** | R/W | Szczegóły pojazdów (1:1 z `CarTrailer`) |
| **`DriverVehicleAssignment`** | R/W | Przypisania kierowca↔pojazd w czasie |
| **`VehicleServiceLog`** | R/W | Log serwisów |

---

## C. Widoki (VIEWs)

| View | Co | Używany w |
|---|---|---|
| **`v_WstawieniaDoKontaktu`** | Wstawienia kurczaka do kontaktu z dostawcami (filtr DokumentyWZ + map do HarmonogramDostaw) | WstawieniaKurczaka window |
| **`vw_QC_Podsum`** | Podsumowanie QC (klasa B, przekarmienie) per partia | Partie/PartiaService.cs |
| **`vw_QC_WadySkale`** | Wady skalowe (skrzydła, nogi, oparzenia) per partia | Partie/PartiaService.cs:159 |

---

## D. Stored procedures używane przez program

| Procedura | Co robi |
|---|---|
| **`sp_AuditLog_GetByLP`** | Pobranie audit log dla `Lp` |
| **`sp_AuditLog_GetRecent`** | Ostatnie wpisy audit |
| **`sp_BatchUpdateZamowieniaStatus`** | Batch update statusów zamówień |
| **`sp_OznaczNotyfikacjePrzeczytane`** | Ustawienie notyfikacji jako przeczytanych |
| **`sp_PobierzNieprzeczytaneNotyfikacje`** | Pobierz nowe notyfikacje |
| **`sp_UtworzPrzypomnienia`** | Tworzenie przypomnień |
| **`sp_ZapiszOferte`** | Zapis oferty handlowca |
| **`sp_set_session_context`** | Ustawienie kontekstu sesji (`AppUserID`, `ChangeReason`) — używane do audit |

---

## E. Kluczowe relacje (JOINs często wracające w kodzie)

```
listapartii.Partia        ←→  PartiaDostawca.Partia       (hodowca dla partii)
listapartii.Partia        ←→  Out1A.P1                    (wyjścia ważeń, legacy)
listapartii.Partia        ←→  In0E.P1                     (przyjęcia ważeń = ważenia produkcji)
listapartii.CreateOperator ←→ operators.ID                (operator który utworzył partię)
listapartii.HarmonogramLp ←→  HarmonogramDostaw.Lp        (link do planu dostaw)
FarmerCalc.LpDostawy      ←→  HarmonogramDostaw.Lp        (rozliczenie ↔ plan)
ZamowieniaMieso.DataUboju ←   filtr przez magazyn
HarmonogramDostaw         ←→  FarmerCalc                  (deklaracje ↔ rozliczenia)
Dostawcy                  ←→  DostawcyCR                  (change request workflow)
DostawcyCR                ←→  DostawcyCRItem              (master-detail change requests)
KartotekaOdbiorcyDane.ID  ←→  KartotekaOdbiorcyKontakty.OdbiorcaID
                          ←→  KartotekaOdbiorcyNotatki.OdbiorcaID
                          ←→  KartotekaPrzypomnienia.OdbiorcaID
                          ←→  KartotekaScoring.OdbiorcaID
```

---

## F. Charakterystyczne klauzule WHERE w kodzie

```sql
-- Status partii
WHERE lp.IsClose = 0           -- otwarte
WHERE lp.IsClose = 1           -- zamknięte
WHERE lp.StatusV2 IN ('PLANNED', 'IN_TRANSIT', ...)
WHERE ISNULL(lp.IsClose, 0) NOT IN ('Anulowane')

-- Filtr działów
WHERE lp.DIR_ID = '1A'         -- ubój
WHERE lp.DIR_ID = '0E'         -- mrożenie
WHERE lp.DIR_ID = '0K'         -- krojenie

-- Filtr dat
WHERE lp.CreateData >= @DataOd AND lp.CreateData <= @DataDo

-- Filtr zamówień (anulowane wykluczamy)
WHERE z.DataUboju = @D AND ISNULL(z.Status,'Nowe') NOT IN ('Anulowane')

-- Decision Requests
WHERE Status = 'Proposed'      -- oczekujące zatwierdzenia
WHERE Status = 'Zdecydowany'   -- zaakceptowane / odrzucone
```

---

## G. Tabele z bazy `Handel` (192.168.0.112) używane przez program

**Tabele HM.* (Symfonia Handel Master):**
- **`HM.DK`** — Dokumenty handlowe (faktury, WZ, korekty)
- **`HM.DP`** — Pozycje dokumentów (linie)
- **`HM.TW`** — Towary (artykuły)
- **`HM.MZ`** — Magazyny (zapasy)
- **`HM.MG`** — Serie magazynowe
- **`HM.PN`** — Płatności / należności (czasami używane)
- **`HM.DokHandlowe`** — Uogólniony widok dokumentów

**Tabele SSCommon.* (Symfonia Common):**
- **`SSCommon.STContractors`** — Dostawcy / odbiorcy
- **`SSCommon.ContractorClassification`** — Klasyfikacja kontrahentów
- **`SSCommon.STPostOfficeAddresses`** — Adresy kontrahentów

**Główne moduły wykorzystujące Handel:**
- `AnalizaTygodniowa` (Dashboard Analityczny)
- `DashboardPrzychodu` (Przychód Żywca LIVE)
- `HandlowiecDashboard` (analizy marży)
- `Reklamacje` (auto-import korekt FKS/FKSB/FWK)
- `Faktury / KartotekaTowarow` (dane towarów)

**Magazyny w Handel (z kontekstu biznesowego):**
- `65554` świeże po uboju, `65556` wydania, `65552` drugi produkcji, `65547` paczkowane, `65562` mrożonki, `65559` pomocniczy, `65883` pasze (kategoria)

---

## H. Co kod NIE używa (mimo że istnieje w bazie)

- **`Out1A`** — Sergiusz: *"nie wiem do czego dokładnie służy, sprzedaż jest w Symfonia 112"*. Service `LoadSalesAsync` był napisany historycznie, ale zakładka usunięta z UI.
- **`Haccp`** — wzmianki w kodzie są, ale aktywne czytanie tylko w `Partie/Services/PartiaService.cs:GetHaccpAsync`. Niewiele rekordów w użyciu.

---

## I. Bazy danych poza LibraNet (pełen ekosystem)

| Baza | Serwer | Co zawiera | Kto używa |
|---|---|---|---|
| **LibraNet** | 192.168.0.109 | **Tu wszystko opisane wyżej** | ZPSP (główny) |
| **TransportPL** | 192.168.0.109 | Główne tabele transportu (`Kierowca`, `Pojazd`, `Kurs`, `Ladunek`) | ZPSP/Transport |
| **HANDEL** | 192.168.0.112 | Symfonia Handel | ZPSP/Analizy + Symfonia |
| **UNISYSTEM** | 192.168.0.23\SQLEXPRESS | UNICARD RCP (godziny pracowników) | KontrolaGodzin |
| **ZPSP** | 192.168.0.23\SQLEXPRESS | Tabele HR_* (urlopy, wnioski) | KontrolaGodzin |

---

## Zobacz też

- [`13_Bazy_danych.md`](13_Bazy_danych.md) — ogólny opis 4 baz w firmie
- [`18_Analiza_przychodu_szczegoly.md`](18_Analiza_przychodu_szczegoly.md) — szczegóły `In0E` + `Article` + `PartiaDostawca`
- [`20_SELECTY_DLA_SSMS.md`](20_SELECTY_DLA_SSMS.md) — gotowy zestaw SELECT-ów do uruchomienia w SSMS
- [`12_ZPSP_program.md`](12_ZPSP_program.md) — architektura programu

---

## J. Aktualizacja 2026-05-12 — eksploracja schematu i analiza Maja/Paulina

### Co potwierdzono w SSMS

- **LibraNet to SQL Server 2022** (nie 2008 R2). `STRING_AGG`, `TRY_CONVERT`, window functions wszystkie działają natywnie.
- **`HarmonogramDostaw`** ma 47 kolumn. Główni twórcy (`KtoStwo`) od 2024-10:
  - Teresa Jachymczak (2121) — 1881 wpisów = 49.83%
  - **Paulina Koncka (1122)** — 972 wpisów = 25.75%
  - Administrator (11111) — 922 wpisów = 24.42%
- **Brak podziału terytorialnego** między Paulina/Teresa — 130+ dostawców obsługiwani przez OBOJE.
- **30 dokumentów modyfikowanych przez drugą osobę** w ostatnim miesiącu — wskazuje na napięcia/poprawki.

### Kolumny `HarmonogramDostaw` — poprawki nazw

- **`KmK`** (km do kurnika), **`KmH`** (km do hodowcy) — NIE `KM`
- **`Wysłane`** (z polskim `ł`) — NIE `Wyslane`
- **`Posrednik`** (bit) — czy dostawa przez pośrednika
- **`Utworzone`, `Wysłane`, `Otrzymane`** — flagi przepływu (czy harmonogram wysłany do hodowcy)
- **`KtoStwo`, `KtoMod`, `KtoUtw`, `KtoWysl`, `KtoOtrzym`** — 5 różnych userów (5 momentów audytu)
- **`KtoWaga`, `KtoSztuki`** — kto potwierdził wagę/sztuki po dostawie
- **`DataPotwWaga`, `DataPotwSztuki`** — kiedy potwierdzono

### Kolumny `listapartii` — RZECZYWISTOŚĆ (19 kolumn)

Faktyczne kolumny: GUID, DIR_ID, Partia, GrupaTowarowa, ArticleID, CreateData/Godzina, ModificationData/Godzina, CreateOperator, CloseData/Godzina, CloseOperator, IsClose, CalcMethod, CalcData, CalcGodzina, StatusV2, HarmonogramLp.

**Hodowca NIE w listapartii** — w **`PartiaDostawca`** (klucz: `Partia`).

**Metryki produkcyjne NIE w listapartii** — szukać w:
- Wagi: `In0E` (przyjęcia) + `Out1A` (rozchód) po `P1`
- Klasa B: `WadyPartii` / `PodsumaPartii` (V2)
- Temperatury: `Temperatury`
- Sztuki deklarowane: `FarmerCalc.DeclI*` lub `HarmonogramDostaw.SztukiDek`

### Tabela `operators` (56 wierszy) — kluczowi userzy

| ID | Name | Login? | Rola |
|---|---|---|---|
| 11111 | Administrator | TAK | Sergiusz |
| 1122 | **Paulina Koncka** | TAK | Zakup żywca |
| 2121 | **Teresa Jachymczak** | TAK | Sprzedaż + zakup żywca |
| 6521 | **Maja Leonard** | TAK | Sprzedaż mięsa |
| 432143 | Anna Jedynak | TAK | Sprzedaż |
| 871231 | Radek Marciniak | rzadko | Sprzedaż (mało aktywny) |
| 111222 | Jolanta Kubiak | nie loguje się | Sprzedaż (główna) |
| 6611 | Justyna Chrostowska | TAK | KSeF/księgowość |
| 9998 | Daniel Czapnik | NIE (nieaktywny) | b. handlowiec Mai |
| 5555 / 9991 | Dawid Sosiński / Śluzarek | NIE | b. handlowcy |

**Quirk:** `operators.ID` to `varchar(15)`, ale w praktyce numeryczny string. JOIN z `HarmonogramDostaw.KtoStwo` (int) wymaga **`CAST(O.ID AS int) = H.KtoStwo`**.

### `UserHandlowcy` — mapowanie LibraNetUserID → HandlowiecName

Tabela mapuje `UserID` (varchar = operators.ID) → `HandlowiecName` (Maja, Daniel, Ania, Jola, Radek, Teresa, Paweł, Ogólne).

**Cel:** atrybucja `ZamowieniaMieso.IdUser` do handlowca. To **jedyny twardy klucz** do historycznej analizy aktywności handlowca w czasie — bo HANDEL traci historię w `CDim_Handlowiec_Val` (CURRENT STATE).

### `Pozyskiwanie_Hodowcy.PrzypisanyDo` — PUSTE

Tabela ma 1874 wierszy ale `PrzypisanyDo` jest **pusta dla Pauliny** (0 hodowców formalnie przypisanych). Konsekwencje:
- CRM hodowców NIE jest oficjalnie podzielony między userów
- Aktywności w `Pozyskiwanie_Aktywnosci` Pauliny (207 wpisów) wynikają ze swobodnej pracy
- Konwersja Pauliny w CRM (luty 2026): 37 "Do zadzwonienia → Próba kontaktu", 19 → "Obcy kontrakt", **tylko 2 → Nawiązano kontakt**
- 0 udanych telefonów (UdaneRozmowy=0 we wszystkich miesiącach)

### `FarmerCalc.CreatedBy` — wszystkie NULL

Mimo 1199 wierszy, `CreatedBy` jest puste w 100% przypadków. **Audyt zakupu hodowcy jest ślepy** — nie wiadomo kto rozliczał.

### Sezonowość pracy Pauliny — sygnał alarmowy (HarmonogramDostaw)

Liczba wpisów per miesiąc (KtoStwo=1122):
- 2025-09: 55 → 2025-10: 100 → 2025-11: 96 → **2025-12: 100** (pik)
- 2026-01: 77 → 2026-02: 49 → 2026-03: 48 → **2026-04: 26 → 2026-05: 20** (drastyczny spadek)

**Spadek do 1/5 normalnej aktywności w ostatnich 2 miesiącach.** Możliwe wyjaśnienia: wypalenie, konflikt z Teresą, urlop, lub Teresa przejmuje zadania.
