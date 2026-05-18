# Audyt SQL kodu ZPSP — co realnie program robi z bazą

**Data audytu:** 2026-05-04
**Metoda:** grep przez wszystkie `*.cs` w `Kalendarz1/`, ekstrakcja konkretnych SELECT/INSERT/UPDATE/DELETE/JOIN.
**Cel:** Pokazać dla każdej tabeli **realne operacje** (nie tylko strukturę) — gdzie i jak program z niej korzysta.

---

## 📊 LIBRA NET (192.168.0.109)

### `listapartii` — Master partii ubojowych

**Główny SELECT** (`Partie/Services/PartiaService.cs:140-197`):
```sql
SELECT lp.GUID, DIR_ID, Partia, CreateData, CreateGodzina, IsClose,
       CloseData, CloseGodzina, CreateOperator, CloseOperator, ArticleID,
       StatusV2, HarmonogramLp, ...
FROM listapartii lp
LEFT JOIN PartiaDostawca pd ON pd.Partia = lp.Partia
LEFT JOIN operators op_create ON CAST(op_create.ID AS varchar) = lp.CreateOperator
LEFT JOIN operators op_close  ON CAST(op_close.ID AS varchar) = lp.CloseOperator
OUTER APPLY (
    SELECT TOP 1 NettoWeight, LumQnt, Price
    FROM FarmerCalc fc2 WHERE fc2.Partia = lp.Partia ORDER BY fc2.ID DESC
) fc
LEFT JOIN (SELECT P1, SUM(ActWeight) AS WydanoKg, SUM(Quantity) AS WydanoSzt
           FROM Out1A WHERE ActWeight > 0 GROUP BY P1) w_out ON w_out.P1 = lp.Partia
LEFT JOIN (SELECT P1, SUM(ActWeight) AS PrzyjetoKg, SUM(Quantity) AS PrzyjetoSzt
           FROM In0E WHERE ActWeight > 0 GROUP BY P1) w_in ON w_in.P1 = lp.Partia
OUTER APPLY (SELECT TOP 1 KlasaB_Proc, Przekarmienie_Kg
             FROM vw_QC_Podsum WHERE PartiaId = lp.Partia ORDER BY 1 DESC) qcp
OUTER APPLY (SELECT TOP 1 Sonda1 FROM Temperatury
             WHERE PartiaId = lp.Partia AND LOWER(Miejsce) LIKE '%rampa%'
             ORDER BY Id DESC) qct
OUTER APPLY (SELECT TOP 1 Skrzydla_Ocena, Nogi_Ocena, Oparzenia_Ocena
             FROM vw_QC_WadySkale WHERE PartiaId = lp.Partia ORDER BY 1 DESC) qcw
WHERE CreateData BETWEEN @DataOd AND @DataDo
  [AND DIR_ID=@Dzial, IsClose=@Status, Partia LIKE @Szukaj, StatusV2=@StatusV2]
ORDER BY lp.CreateData DESC, CreateGodzina DESC
```

**Filtry charakterystyczne:**
- `IsClose = 0` (otwarte) / `IsClose = 1` (zamknięte)
- `DIR_ID = '1A'` (ubój), `'0E'` (mrożenie), `'0K'` (krojenie)
- `StatusV2 = 'IN_PRODUCTION'` (default)
- `Partia LIKE '%xxx%'` (search)

**Auto-migrations** (`PartiaService.EnsureSchemaAsync()`):
```sql
ALTER TABLE listapartii ADD StatusV2 varchar(30) DEFAULT 'IN_PRODUCTION'
ALTER TABLE listapartii ADD HarmonogramLp int

CREATE TABLE PartiaStatus (ID int IDENTITY PK, Partia varchar(15),
    Status varchar(30), OperatorID, OperatorNazwa, Komentarz,
    CreatedAtUTC datetime2)

CREATE TABLE QC_Normy (ID int IDENTITY PK, Nazwa, Opis,
    MinWartosc, MaxWartosc, JednostkaMiary,
    Kategoria DEFAULT 'TEMPERATURA', IsAktywna, Kolejnosc)
-- + INSERT defaults: TempRampa, TempChillera, KlasaB, Przekarmienie, ...

CREATE TABLE PartiaAuditLog (ID int IDENTITY PK, Partia, Akcja, Opis,
    OperatorID, OperatorNazwa, CreatedAtUTC datetime2)
CREATE INDEX IX_PAL_Partia ON PartiaAuditLog(Partia)
```

---

### `ZamowieniaMieso` + `ZamowieniaMiesoTowar` — Zamówienia

**Główny SELECT** (`ZPSP.Sales/SQL/SqlQueries.cs:16-54`):
```sql
SELECT zm.Id, KlientId, SUM(zmt.Ilosc) AS Ilosc,
       DataPrzyjazdu, DataUtworzenia, IdUser, Status,
       LiczbaPojemnikow, LiczbaPalet, TrybE2, Uwagi, TransportKursID,
       (CASE WHEN EXISTS(SELECT 1 FROM ZamowieniaMiesoTowar t
                         WHERE t.ZamowienieId=zm.Id AND t.Folia=1)
             THEN 1 ELSE 0 END) AS MaFolie,
       (CASE WHEN EXISTS(SELECT 1 FROM ZamowieniaMiesoTowar t
                         WHERE t.ZamowienieId=zm.Id AND t.Hallal=1)
             THEN 1 ELSE 0 END) AS MaHallal,
       CzyZrealizowane, DataWydania, DataUboju, Waluta='PLN'
FROM ZamowieniaMieso zm
LEFT JOIN ZamowieniaMiesoTowar zmt ON zmt.ZamowienieId = zm.Id
WHERE DataUboju = @Day
  AND ISNULL(Status, 'Nowe') NOT IN ('Anulowane')
GROUP BY zm.Id, KlientId, ...
ORDER BY zm.Id
```

**Pozycje per zamówienie:**
```sql
SELECT ZamowienieId, KodTowaru, Ilosc, Cena (VARCHAR!),
       Pojemniki, Palety, E2, Folia, Hallal
FROM ZamowieniaMiesoTowar
WHERE ZamowienieId = @OrderId
```

**Batch optymalizacja N+1:**
```sql
SELECT ZamowienieId, KodTowaru, SUM(Ilosc)
FROM ZamowieniaMiesoTowar
WHERE ZamowienieId IN (STRING_SPLIT(@OrderIds, ','))
```

**UPDATE-y:**
```sql
-- Edycja uwag
UPDATE ZamowieniaMieso SET Uwagi=@Uwagi WHERE Id=@Id

-- Anulowanie (soft delete)
UPDATE ZamowieniaMieso
SET Status='Anulowane', AnulowanePrzez=@User,
    DataAnulowania=GETDATE(), PrzyczynaAnulowania=@Przyczyna
WHERE Id=@Id

-- Przywrócenie
UPDATE ZamowieniaMieso
SET Status='Nowe', AnulowanePrzez=NULL, DataAnulowania=NULL,
    PrzyczynaAnulowania=NULL
WHERE Id=@Id
```

⚠️ **Cena jako VARCHAR(20)** — ryzyko błędów rzutowania. Sprawdzane w SELECT-ach przez `ISNULL(Cena, '0') <> '0'`.

---

### `PartiaDostawca` — Hodowcy per partia

Tylko SELECT (read-only), używana jako JOIN w 4+ miejscach:
```sql
SELECT Partia, CustomerID, CustomerName FROM PartiaDostawca
```

---

### `In0E` — Wagi przychody (przyjęcia magazynowe)

**SELECT** (`AnalizaPrzychoduProdukcji/Services/PrzychodService.cs:27-50`):
```sql
SELECT ArticleID, ArticleName, JM, TermID, TermType, Weight, Quantity,
       Direction, Data, Godzina, OperatorID, Wagowy, Tara, Price, P1, P2,
       ActWeight, QntInCont
FROM In0E
WHERE Data BETWEEN @DataOd AND @DataDo
  [AND P1=@Partia, OperatorID=@OpID, TermID=@TermID,
   QntInCont=@Klasa, Godzina BETWEEN @GodzOd AND @GodzDo,
   P1 IN (SELECT Partia FROM PartiaDostawca WHERE CustomerID=@Dostawca)]
ORDER BY Data, Godzina
```

**Hardcoded:** `WHERE ArticleID = '40'` (Kurczak A) — w wielu miejscach do statystyk.

**Direction:** Zawsze `'1A'` (wejście do produkcji 1A).

---

### `Out1A` — Wagi wydania (`Direction='0E'`)

```sql
SELECT P1 AS Partia, SUM(ActWeight) AS WydanoKg, SUM(Quantity) AS WydanoSzt
FROM Out1A WHERE ActWeight IS NOT NULL GROUP BY P1
```

---

### `Article` — Towary (kartoteka)

**SELECT** (`KartotekaTowarow/ArticleService.cs:133-143`):
```sql
SELECT GUID, ID, ShortName, Name, Grupa, Grupa1, Cena1, Cena2, Cena3,
       Rodzaj, JM, WRC, Wydajnosc, Ingredients1-8, Duration,
       TempOfStorage, Halt, Przelicznik, CreateData, CreateGodzina,
       ModificationData, ModificationGodzina, RELATED_ID1-3,
       isStandard, StandardWeight, StandardTol, StandardTolMinus,
       NameLine1, NameLine2
FROM Article
[WHERE GUID=@GUID OR Halt=0 OR Halt=1]
ORDER BY Name
```

**INSERT/UPDATE z auto-timestamp:**
```sql
INSERT INTO Article (..., CreateData, CreateGodzina, ...)
VALUES (..., CONVERT(varchar(10), GETDATE(), 120),
              CONVERT(varchar(8), GETDATE(), 108), ...)

UPDATE Article SET ..., ModificationData=CONVERT(...),
                       ModificationGodzina=CONVERT(...)
WHERE GUID=@GUID
```

**Inline edit (whitelist Cena1/Cena2/Cena3/Halt):**
```sql
UPDATE Article SET [fieldName]=@Value, ModificationData=...
WHERE GUID=@GUID
```

**Audit log:**
```sql
INSERT INTO ArticleAuditLog (ArticleGUID, ArticleID, FieldName,
    OldValue, NewValue, ChangedBy, ChangedAt)
VALUES (...)
-- Wstawiane przy każdym UPDATE
```

**Linki:**
- `JOIN TowarZdjecia ON TowarId=CAST(Article.ID AS int) AND Aktywne=1`
- `JOIN ArtPartitionD ON ID=Article.ID`
- `JOIN KonfiguracjaProduktow ON TowarID=CAST(Article.ID AS int) AND Aktywny=1`
- `ArticleFavorites (ArticleGUID, UserID)` — toggle ulubione

---

### `FarmerCalc` — Skup żywca

**KPI dashboard** (`Services/DashboardService.cs:71-83`):
```sql
SELECT COUNT(*), COUNT(DISTINCT CustomerGID),
       SUM(LumQnt), SUM(NettoWeight),
       SUM((Price + Addition) * NettoWeight * (1 - Loss/100)),
       AVG(Price + Addition), AVG(Loss),
       SUM(IncDeadConf), COUNT(DISTINCT DriverGID)
FROM FarmerCalc
WHERE DataPrzyjecia BETWEEN @od AND @do
```

**Top hodowcy:**
```sql
SELECT TOP N c.Name, c.City,
       SUM(NettoWeight) AS WagaSuma,
       SUM((Price+Addition)*NettoWeight) AS WartoscSuma,
       COUNT(*) AS LiczbaDostaw
FROM FarmerCalc fc JOIN Customer c
WHERE DataPrzyjecia >= @od
GROUP BY c.Name, c.City
ORDER BY WagaSuma DESC
```

**Statystyki kierowcy (`FlotaService`):**
```sql
SELECT COUNT(*) AS KursySkup30d, SUM(DistanceKM) AS Km30d
FROM FarmerCalc
WHERE DriverGID = @DID
  AND CalcDate >= DATEADD(DAY, -30, GETDATE())
```

---

### `HarmonogramDostaw` — Plan dostaw

**SELECT** (`HarmonogramDostawRepository.cs:28-48`):
```sql
SELECT h.LP, h.DataOdbioru, h.Dostawca,
       h.Utworzone, h.Wysłane, h.Otrzymane, h.Posrednik,
       h.Auta, h.SztukiDek, h.WagaDek, h.SztSzuflada,
       ISNULL(u1.Name, h.KtoUtw) AS KtoUtw, h.KiedyUtw, ...
FROM HarmonogramDostaw h
LEFT JOIN operators u1 ON u1.ID = h.KtoUtw
LEFT JOIN operators u2 ON u2.ID = h.KtoWysl
LEFT JOIN operators u3 ON u3.ID = h.KtoOtrzym
WHERE h.Bufor = 'Potwierdzony'
  AND h.DataOdbioru BETWEEN [dolnaGranica] AND DATEADD(DAY, 2, GETDATE())
ORDER BY h.DataOdbioru DESC
```

**Filtr kluczowy:** `Bufor='Potwierdzony'` — bez tego nie pokazuje. **Pomija niepotwierdzone wpisy**.

**UPDATE:**
```sql
UPDATE HarmonogramDostaw
SET [columnName] = (flag),
    [ktoCol] = CASE WHEN @val=1 THEN @kto ELSE NULL END,
    [kiedyCol] = CASE WHEN @val=1 THEN GETDATE() ELSE NULL END
WHERE LP = @id
-- Dozwolone kolumny: Utworzone, Wysłane, Otrzymane (z Kto/Kiedy)
-- Posrednik (tylko flaga, bez Kto/Kiedy)
```

⚠️ **Brak transakcji** — każda flaga otwiera osobne connection.

**Audit log (opcjonalny):**
```sql
INSERT INTO HarmonogramDostaw_AuditLog
(LP, ColumnName, OldValue, NewValue, UserID, ChangedAt)
VALUES (...)
```

---

### `KartotekaOdbiorcy*` — CRM klientów

**MERGE pattern** (UPSERT, `KartotekaService.cs:273-322`):
```sql
MERGE KartotekaOdbiorcyDane AS target
USING (SELECT @IdSymfonia AS IdSymfonia) AS source
ON target.IdSymfonia = source.IdSymfonia
WHEN MATCHED THEN UPDATE SET
    OsobaKontaktowa, TelefonKontakt, ...,
    DataModyfikacji = GETDATE(),
    ModyfikowalPrzez = @Uzytkownik
WHEN NOT MATCHED THEN INSERT (
    IdSymfonia, OsobaKontaktowa, ..., ModyfikowalPrzez
) VALUES (...)
```

**Auto-create tables** (`KartotekaService.EnsureTablesExistAsync()`):
- `KartotekaOdbiorcyDane` (z `KategoriaHandlowca CHAR(1) DEFAULT 'C'`)
- `KartotekaOdbiorcyKontakty` (z `TypKontaktu` ORDER: Główny→1, Księgowość→2, Opakowania→3, Właściciel→4, Magazyn→5)
- `KartotekaOdbiorcyNotatki`
- `KartotekaTypyKontaktow` (lookup table z domyślnymi typami)

**DELETE soft (Notatki):**
```sql
DELETE FROM KartotekaOdbiorcyNotatki WHERE Id=@Id  -- z check Autor
```

---

## 📊 HANDEL (192.168.0.112)

### `[SSCommon].STContractors` — Kontrahenci Symfonia

```sql
SELECT c.Id, c.Shortcut, c.Name1 AS Nazwa, c.NIP,
       c.Street AS Adres, c.City AS Miasto, c.PostalCode,
       c.Phone AS Telefon, c.Email,
       wym.CDim_Handlowiec_Val AS Handlowiec
FROM [SSCommon].STContractors c
LEFT JOIN [SSCommon].ContractorClassification wym ON c.Id = wym.ElementId
```

### `[HM].TW` — Towary

```sql
SELECT ID AS Id, kod AS Kod, nazwa AS Nazwa, katalog AS Katalog, jm AS JM
FROM [HM].TW
WHERE katalog IN (67095, 67153)  -- 67095=Kurczak A świeży, 67153=Kurczak B mrożony
ORDER BY katalog, kod
```

### `[HM].DK` + `[HM].DP` + `[HM].PN` — Faktury sprzedaży

```sql
SELECT khid, kod AS NumerDokumentu,
       CAST(walbrutto AS DECIMAL(18,2)) AS brutto,
       ISNULL(PN.KwotaRozliczona, 0) AS rozliczono,
       typ_dk AS typ, anulowany,
       data AS data_faktury,
       ISNULL(PN.TerminPrawdziwy, plattermin) AS termin_platnosci
FROM [HM].DK
LEFT JOIN (SELECT dkid, SUM(kwotarozl) AS KwotaRozliczona,
                  MAX(Termin) AS TerminPrawdziwy
           FROM [HM].PN GROUP BY dkid) PN
WHERE khid = @IdSymfonia
  AND typ_dk IN ('FVS', 'FVR', 'FVZ')
  AND aktywny = 1
  AND data >= DATEADD(MONTH, -@Miesiace, GETDATE())
ORDER BY data DESC
```

### `[HM].MZ` + `[HM].MG` — Magazyny

```sql
-- Wydania per produkt
SELECT MZ.idtw AS ProduktId, SUM(ABS(MZ.ilosc)) AS Ilosc
FROM [HM].MZ MZ JOIN [HM].MG MG ON MG.id = MZ.super
WHERE MG.seria IN ('sWZ', 'sWZ-W')
  AND MG.aktywny = 1 AND MG.data = @Day
  AND MZ.idtw IN (...)
GROUP BY MZ.idtw

-- Przychody produkcji per produkt
SELECT MZ.idtw, SUM(ABS(MZ.ilosc)) AS Ilosc
FROM [HM].MZ MZ JOIN [HM].MG MG ON MG.id = MZ.super
WHERE MG.seria IN ('sPWP', 'PWP') AND MG.data = @Day
GROUP BY MZ.idtw
```

⚠️ **Tylko READ z HANDEL** — program nigdy nie pisze do Symfonii. Symfonia → ZPSP jednokierunkowo.

---

## 📊 TransportPL (192.168.0.109)

### `Kierowca` + `Pojazd` + `Kurs` + `Ladunek`

```sql
-- Kierowcy aktywni
SELECT KierowcaID, Imie, Nazwisko, Telefon, Aktywny,
       UtworzonoUTC, ZmienionoUTC
FROM Kierowca
WHERE (@TylkoAktywni = 0 OR Aktywny = 1)
ORDER BY Nazwisko, Imie

-- Pojazdy aktywne
SELECT PojazdID, Rejestracja, Marka, Model, PaletyH1,
       Aktywny, UtworzonoUTC, ZmienionoUTC
FROM Pojazd
WHERE (@TylkoAktywne = 0 OR Aktywny = 1)
ORDER BY Rejestracja

-- Kursy + JOIN kierowca/pojazd
SELECT KursID, DataKursu, Trasa, GodzWyjazdu, GodzPowrotu, Status,
       ki.Imie + ' ' + ki.Nazwisko AS Kierowca, ki.Telefon,
       p.Rejestracja, p.Marka, p.Model,
       ISNULL(p.PaletyH1, 33) AS MaxPalety
FROM Kurs k
LEFT JOIN Kierowca ki ON ki.KierowcaID = k.KierowcaID
LEFT JOIN Pojazd p ON p.PojazdID = k.PojazdID
WHERE k.DataKursu = @Day
ORDER BY k.GodzWyjazdu

-- Ładunki kursu
SELECT LadunekID, KursID, Kolejnosc, KodKlienta,
       PaletyH1 AS Palety, PojemnikiE2 AS Pojemniki, Uwagi
FROM Ladunek
WHERE KursID = @KursId
ORDER BY Kolejnosc
```

### `TransportZmiany` — Workflow akceptacji

**Typy zmian (`TypZmiany`):**
- `NoweZamowienie`, `ZmianaIlosci`, `ZmianaTerminu`, `Anulowanie`,
  `ZmianaPojemnikow`, `ZmianaKg`, `ZmianaAwizacji`, `ZmianaUwag`,
  `ZmianaOdbiorcy`, `ZmianaDataProdukcji`, `ZmianaStatusu` (**ignorowane!**)

**Statusy:** `Oczekuje`, `Zaakceptowane`, `Odrzucone`

```sql
-- Liczba dzisiejszych oczekujących (badge w menu)
SELECT COUNT(*) FROM TransportZmiany
WHERE StatusZmiany = 'Oczekuje'
  AND TypZmiany != 'ZmianaStatusu'
  AND CAST(DataZgloszenia AS date) = CAST(GETDATE() AS date)
```

---

## 🚨 PROBLEMY ZNALEZIONE W KODZIE

### 1. Connection strings hardcoded w każdym Service
- LibraNet: `"Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True"`
- HANDEL: `"Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;..."`
- TransportPL: `"Server=192.168.0.109;Database=TransportPL;..."`

**Skutek:** Zmiana hasła = update kilku-set plików C#.

### 2. Cena jako VARCHAR w `ZamowieniaMiesoTowar`
W kodzie: `ISNULL(Cena, '0') <> '0'`. **Brak rzutowania na DECIMAL** w SELECT-ach z arytmetyką → potencjalne błędy.

### 3. Auto-create tables w runtime
`PartiaService.EnsureSchemaAsync()`, `KartotekaService.EnsureTablesExistAsync()`, `FlotaService.EnsureTablesExistAsync()`, `ChatService.InitializeDatabaseAsync()` — **schema migrations zaszyte w kodzie aplikacji**, nie ma osobnych migration scripts.

### 4. Filtr `IsClose=0` bez DEFAULT
W bazie kolumna może być NULL — kod traktuje NULL jak 0, ale `WHERE IsClose=0` **nie złapie NULL**. Trzeba `WHERE ISNULL(IsClose,0)=0`.

### 5. Brak JOIN-ów na `operators` — używają CAST
```sql
LEFT JOIN operators op ON CAST(op.ID AS varchar) = lp.CreateOperator
```
Konwersja w JOIN = brak indeksu = wolny query.

### 6. Brak DELETE — głównie soft delete
Większość tabel używa `Status='Anulowane'`, `Aktywny=0` lub flag `IsClose=1`. Bezpośrednie DELETE tylko w:
- `KartotekaOdbiorcyKontakty`
- `KartotekaOdbiorcyNotatki`
- `ArticleFavorites`

### 7. Brak transakcji w bulk updates
`HarmonogramDostawRepository.UpdateFlag()` otwiera connection dla **każdej flagi osobno** zamiast batch.

### 8. Tabele wymienione w kodzie ale których NIE MA w bazie
- `SzablonyZamowien` — wymienione w kilku Service.cs ale brak w DB
- `KartotekaPrzypomnienia` — wymienione w `KartotekaService.cs` ale brak w DB

### 9. Hardcoded ID
- `WHERE ArticleID = '40'` — **Kurczak A** (najczęstsze hardcode)
- `WHERE katalog IN (67095, 67153)` — kategorie HANDEL towarów mięsnych
- `Bufor='Potwierdzony'` — tylko ten filtr działa

### 10. Komentarze TODO w SQL
- `CenaSkup` w FarmerCalc — TODO: czy NUMBER czy TEXT?
- `Przekarmienie_Kg` — TODO: jednostka

---

## 📈 OPERACJE PER TABELA — Heatmapa

### Najczęściej czytane (READ-heavy)
1. `In0E` — przy każdym dashboardzie/raporcie
2. `Article` — przy każdym oknie towarów
3. `listapartii` — Lista Partii
4. `ZamowieniaMieso` — Sprzedaż dziś
5. `PartiaDostawca` — JOIN dla każdej partii
6. `[HM].DK` (Symfonia) — faktury klienta

### Najczęściej zapisywane (WRITE-heavy)
1. `In0E` (przez Wago) — co kilka sekund nowe ważenie
2. `Out1A` (przez Wago) — wydania mroźni
3. `Aktywnosc` — telemetria użytkowników (185k)
4. `WagoCounter` (przez Wago) — co ~40 minut nowy wpis
5. `HistoriaZmianZamowien` — przy każdej zmianie zamówienia
6. `ArticleAuditLog` — przy każdym update Article
7. `ChatMessages`, `Notatki*` — komunikacja

### Tylko READ (program nie pisze)
- `[HM].*` (cała Symfonia)
- `[SSCommon].*` (cała Symfonia)
- `KodyPocztowe`, `Province`, `GeoCache` (lookup tables)

### Tylko zapis (program nigdy nie czyta)
- `Aktywnosc` (telemetria — pisze, czyta się rzadko)
- `AuditLog_Dostawy` (audit)
- `PartiaAuditLog` (audit)
- `ArticleAuditLog` (audit)

---

## ✅ Co zwalidowano

- Każda tabela kluczowa LibraNet ma swój wzorzec SELECT/INSERT/UPDATE
- HANDEL używany **tylko READ** (faktury, kontrahenci, towary, magazyny)
- TransportPL ma czyste FK między Kierowca/Pojazd/Kurs/Ladunek
- Audit logging zaimplementowany dla: Reklamacje (trigger), HarmonogramDostaw, Article, KartotekaOdbiorcy

## ❓ Co wymaga sprawdzenia w bazie

Plik `EKSPLORACJA_ZALEZNOSCI.sql` (BLOK 91-103):
- Czy są broken FK (sieroty)
- Czy filtry charakterystyczne mają sens (IsClose=0, Bufor='Potwierdzony')
- Czy Cena jako VARCHAR ma błędne wartości
- Czy Operator2ID rzeczywiście niewykorzystywany
- Czy WagoCounter zgadza się z FarmerCalc (sztuki dek vs policzone)
