# 09 — Transport (flota + AVILOG + planowanie kursów)

## Flota

- **12 pojazdów** (samochody chłodnie + ciągniki + naczepy)
- **13 kierowców** aktywnych
- **WebFleet (TomTom)** — tracking GPS, API dostępne
- **Stawka km kierowcy:** **0.69 zł** (od maja 2026, łączy delegacje — wcześniej oddzielnie)
- **Paliwo:** **3.71 zł/km** = **70% kosztu kursu**

---

## Trzy bazy danych transportu (uwaga!)

| DB | Server | Co tam |
|---|---|---|
| **TransportPL** | 192.168.0.109 | Główne tabele: Kierowca, Pojazd, Kurs, Ladunek |
| **LibraNet** | 192.168.0.109 | `ZamowieniaMieso` — zamówienia mięsa od klientów |
| **Handel** | 192.168.0.112 | Kontrahenci (`STContractors`, `STPostOfficeAddresses`) |

---

## Tabele TransportPL (kluczowe)

```sql
dbo.Kierowca:
  KierowcaID int PK
  Imie nvarchar(50)
  Nazwisko nvarchar(80)
  Telefon nvarchar(30)?
  Aktywny bit=1
  UtworzonoUTC datetime2

dbo.Pojazd:
  PojazdID int PK
  Rejestracja nvarchar(20)
  Marka, Model nvarchar(50)?
  PaletyH1 int=0  -- ile palet H1 mieści
  Aktywny bit=1

dbo.Kurs:
  KursID bigint PK IDENTITY
  DataKursu date
  KierowcaID int? FK
  PojazdID int? FK
  Trasa nvarchar(120)?
  GodzWyjazdu, GodzPowrotu time?
  Status nvarchar(20)='Planowany'   -- realnie używa się tylko 'Planowany'
  PlanE2NaPalete tinyint=36         -- standard pakowania

dbo.Ladunek:
  LadunekID bigint PK IDENTITY
  KursID bigint FK
  Kolejnosc int
  KodKlienta nvarchar(50)?          -- "ZAM_{id}" gdy z LibraNet
  PojemnikiE2 int=0
  PaletyH1 int?
  PlanE2NaPaleteOverride tinyint?
  Uwagi nvarchar(255)?
  TrybE2 bit=0                      -- czy w trybie pojemników
```

**Skala (na 2026-02):** 979 kursów, 1333 ładunków, 15 kierowców aktywnych, 13 pojazdów aktywnych.

---

## Logika pakowania

**Konfiguracja standardowa:**
- **36 lub 40 pojemników E2 na paletę H1**
- **33 palety H1 na naczepę**

**Algorytm Sergiusza (greedy):**
- Próbuje upakować maksymalnie ładunków na auto
- Pozwala na podziały (split) gdy klient ma więcej niż mieści się w jednym aucie
- Nominalne wypełnienie vs maksymalne (115%) — tolerancja ostatniej palety

**Override per-load:** `PlanE2NaPaleteOverride` — gdy klient ma niestandardowe pojemniki.

---

## Workflow planowania kursu

1. **6:00-8:00:** Handlowcy zbierają zamówienia z awizacją (data + godzina odbioru)
2. **Zamówienia w `LibraNet.dbo.ZamowieniaMieso`** — z polem `Status='Oczekuje'`, `TransportStatus='Oczekuje'`
3. **8:00-9:00:** Logistyk układa kursy w **TransportMainFormImproved** (ZPSP):
   - Wybiera pojazd + kierowcę
   - Drag&drop zamówień na kurs
   - Algorytm pakowania waliduje czy mieści się
4. **Do 9:00:** **70% kursów gotowych**
5. Zamówienie powiązane z kursem dostaje `TransportStatus='Przypisany'`, `TransportKursID = X`
6. Auto wyjeżdża → `TransportStatus='W trasie'`
7. Auto wraca → `TransportStatus='Zakończony'`

---

## AVILOG (planowanie odbioru żywca)

**Co to:** Wewnętrzny system planowania odbioru kurczaków od hodowców (NIE od klientów — to inny etap).

**Funkcja:**
- Sergiusz/Paulina wpisują kontrakty z hodowcami (data wstawienia, ilość piskląt, planowana data odbioru 35/42 dni)
- AVILOG **precyzyjnie planuje godziny przyjazdu samochodów** — jeden po drugim, bez przerwy
- Cel: kurczaki **nie stoją na placu** (stres = strata wagi + jakość)

**Status integracji z ZPSP:** Wewnętrzne dane firmy, integracja w ZPSP istnieje (`Wstawienie.cs`, `WidokAvilog.cs`, `WidokAvilogPlan.cs`).

---

## TransportPL — moduły w ZPSP

**Pliki w `Transport/`:**
- `transport-panel-main.cs` — `TransportMainFormImproved` (główny panel, ~1500 linii)
- `transport-editor.cs` — `EdytorKursuWithPalety` (edytor kursu, ~2000 linii)
- `transport-repository.cs` — `TransportRepozytorium` (data access, 827 linii)
- `transport_models.cs` — modele + `PakowanieSerwis` (algorytm pakowania, 507 linii)
- `Formularze/transport_kierowcy_form.cs` — `KierowcyForm` (616 linii)
- `Formularze/transport_pojazdy_form.cs` — `PojazdyForm` (680 linii)
- `Formularze/transport_report_form.cs` — `TransportRaportForm` (524 linie)
- `TransportMapWindow.cs` — mapa GMap.NET (1216 linii)
- `TransportStatystykiForm.cs` — statystyki (674 linie)

**Menu access:**
- `accessMap[16]` = `"UstalanieTranportu"` (główny panel)
- `accessMap[59]` = `"TransportZmiany"` (zmiany do akceptacji)
- Menu category: `"OPAKOWANIA I TRANSPORT"`

---

## Workflow zatwierdzania zmian (TransportZmiany)

**Po co:** Logistyk lub handlowiec może zmienić kurs (dodać/usunąć ładunek), ale zmiana wymaga akceptacji.

**Tabela:** `TransportZmiany` (TransportPL).
**Service:** `TransportZmianyService`.
**Okno:** `TransportZmianyWindow`.

**Menu badges:**
- `_transportPendingBadge` (amber, lewa strona) — czeka na akceptację
- `_transportFreeBadge` (teal, prawa strona) — wolne moce

---

## Flota (Kierowcy + Pojazdy + Przypisania)

**Folder:** `Flota/`
**DB:** LibraNet (192.168.0.109) — extends `Driver` + `CarTrailer` tables

**Tabele rozszerzone:**
- `DriverDetails` (1:1 z Driver) — szczegóły kierowcy (PESEL, prawo jazdy, telefon)
- `VehicleDetails` (1:1 z CarTrailer) — szczegóły pojazdu (VIN, ubezpieczenie, OC/AC)
- `DriverVehicleAssignment` — przypisania kierowca↔pojazd w czasie
- `VehicleServiceLog` — log serwisów

**SQL script:** `Flota/SQL/CreateFlotaTables.sql` (run once in SSMS).

**Service:** `FlotaService` (`Flota/Services/FlotaService.cs`) — async data access.

**Main view:** `WidokFlota.xaml` (UserControl) — 3 zakładki:
1. **Kierowcy**
2. **Pojazdy**
3. **Przypisania**
+ panel **Alerts** (wygasające ubezpieczenia, OC, badania techniczne)

**Dialogi:**
- `DriverEditWindow` (4 zakładki)
- `VehicleEditWindow` (4 zakładki)
- `AssignDriverDialog`
- `ServiceLogDialog`

**Menu:** `accessMap[57]`, kategoria **OPAKOWANIA I TRANSPORT**.

**Uwaga:** `Driver.Name` synchronizowane z `FirstName + " " + LastName` — kompatybilność z ProNova/Raporty.exe.

---

## Mapa Floty

**Plik:** `MapaFloty/...` — pokazuje pozycje pojazdów na mapie (WebFleet API + GMap.NET).

**Klasy:**
- `FleetAlertService` — alerty (kierowca poza trasą, opóźnienie)
- `KursMonitorService` — monitorowanie kursów na żywo

**Adres firmy (centrum mapy):** Ubojnia Koziołki 40, 95-061 Dmosin (51.9148, 19.8089).

---

## Kierowcy — kontakty

(Z rozmów nagranych w Fireflies)

- **Sławomir Gałek** — kierowca (rozmowa nagrana)
- **Radosław Kołodziejczyk** — kierowca (rozmowa nagrana)
- **Ilona/Ilon** — koordynator transport (rozmowa nagrana)
- **Wojtek** — partner zewnętrzny transport

**Kontakt z agentem:** TYLKO przez Sergiusza. Agent nie pisze kierowcom bezpośrednio.

---

## Audyt transportu 2026

**Plik:** `Dokumenty ogólnikowe/Audyt_Transport_2026_PELNY.docx`
**Stan:** `TRANSPORT_STAN_OBECNY.docx` (marzec 2026)

Tematy audytu (do przeczytania w razie pytań szczegółowych):
- Procedury kierowców
- Ubezpieczenia OC/AC pojazdów
- Czas pracy kierowców (prawo)
- Ekonomia kursów (km/zł)

---

## Pomysły rozwoju (do rozmowy z Sergiuszem)

1. **Real-time tracking pojazdów na mapie głównej** — z WebFleet API
2. **Alert kierowcy off-route** — różnica >5 km od planowanej trasy
3. **Optymalizator kursów** — algorytm VRP (Vehicle Routing Problem) zamiast greedy
4. **Aplikacja mobilna kierowcy** — potwierdzenie załadunku, zdjęcie WZ, podpis klienta
