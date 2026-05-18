# 23. HANDEL — schemat Sage Symfonia (głębokie odkrycia)

> Dokument bazuje na analizie 1000+ wierszy `HM.MG`, eksploracji w SSMS oraz reverse-engineeringu wykonanym 2026-05-09 przy okazji refactoru "Stan magazynów" w Bilansie materiałowym.
> Server: `192.168.0.112`. Baza: `HANDEL`. Wersja: SQL Server 2017+ (testowane z Sage Symfonia 2.0+).

---

## 1. Architektura tabel HM.*

Sage Symfonia używa **polimorficznej tabeli `HM.MG`** (Master / nagłówki dokumentów) oraz **tabeli linii `HM.MZ`** (pozycje dokumentów). Dodatkowo `HM.TW` (towary) i `HM.MA`/`HM.MAG` (magazyny — *uwaga: w bazie HANDEL Pióroskovskich ich NIE MA*).

### 1.1. `HM.MG` — Magazynowe (nagłówki dokumentów)

**Kluczowa właściwość**: tabela ta przechowuje **WSZYSTKO** — dokumenty (typ=201), foldery (typ=110, np. "Dokumenty magazynowe"), kategorie kontrahentów (typ=102, np. "@Mir") i potencjalnie inne metadane.

| Kolumna | Typ | Opis |
|---|---|---|
| `id` | int PK | ID dokumentu (sekwencyjny, ~65000+) |
| `typ` | int | Typ rekordu: **201** = dokument, 110 = folder, 102 = kategoria |
| `subtyp` | int | Subtyp dokumentu (np. 74=PWP, 76=RWP, 78=WZ, 84=MM-, 82=MM+, 89=PZ) |
| `kod` | varchar | Numer dokumentu np. `"0001/22/MM-/M. PROD"` |
| `seria` | varchar | Seria dokumentu: `sPZ`, `sPWU`, `PWP`, `MM-`, `sMM-`, etc. (literka `s` = nowa generacja) |
| `seriadzial` | int | Dział serii (zazwyczaj 0) |
| `data` | datetime | **Data dokumentu** (UWAGA: zawsze `CAST(MG.data AS DATE)` przy `BETWEEN`) |
| `datasp` | datetime | Data sprzedaży (zwykle = data) |
| `khid` | int | ID kontrahenta (FK do `SSCommon.STContractors.id`) |
| `khadid` | int | ID adresu kontrahenta |
| **`khdzial`** | int | **REPURPOSED dla MM-/MM+!** Normalnie dział kontrahenta. **Dla `sMM-`** trzyma ID magazynu DOCELOWEGO. **Dla `sMM+`** trzyma ID magazynu ŹRÓDŁOWEGO. |
| `magazyn` | int | ID magazynu DOKUMENTU. Dla sMM- = źródło, sMM+ = cel |
| `przychod` | decimal | Przychód magazynowy (kg) — wpisywany ręcznie, nie zawsze spójny |
| `rozchod` | decimal | Rozchód magazynowy (kg) |
| `wartoscWz` | decimal | Wartość WZ (zł lub kg) |
| `iddokkoryg` | int | ID dokumentu KOREKTOWANEGO (np. WZK koryguje WZ) |
| `super` | int | Rodzić dokumentu (wiązanie hierarchiczne, rzadko używane) |
| `katalog` | int | Katalog towarów (zwykle 2500) |
| `rodzaj` | int | Rodzaj (3500 = "Dokumenty magazynowe") |
| **`anulowany`** | bit | **ZAWSZE filtruj `= 0`** w WHERE! Sage zostawia anulowane dokumenty w tabeli |
| `wystawil` | int | ID użytkownika (FK do tabeli userów) |
| `createdBy`, `createdDate` | int, datetime | Audyt utworzenia |
| `modifiedBy`, `modifiedDate` | int, datetime | Audyt modyfikacji |
| `guid` | uniqueidentifier | GUID rekordu (np. dla integracji ESB) |
| `statusFK` | int | Status księgowy (32768 = bufor, inne = zaksięgowane) |
| `statusRDF` | int | Status RDF (raport dziennego ksiegowania) |
| `bufor` | int | Czy w buforze |
| `info` | int | Flagi info |
| `flag` | int | Flagi (256 = przyjęcie, 1024 = MM-, 1280 = MM+, 4 = WZ, 0 = MW/MP) |
| `aktywny` | bit | Aktywność |
| `ProductionOrderID` | int | **PUSTE** — moduł produkcji nigdy nie wdrożony |
| `IsProductionTrash` | bit | **PUSTE** |

### 1.2. `HM.MZ` — Pozycje magazynowe (linie dokumentów)

| Kolumna | Typ | Opis |
|---|---|---|
| `id` | int PK | ID linii |
| `super` | int | **FK do `HM.MG.id`** — dokument macierzysty |
| `idtw` | int | FK do `HM.TW.id` — towar |
| `kod` | varchar | Kod pozycji (zwykle = `TW.kod`, np. "Kurczak A", "Filet z piersi") |
| `nazwa` | varchar | Nazwa pozycji (zwykle = `TW.nazwa`) |
| `ilosc` | decimal | Ilość (kg/szt) — **używaj `ABS(ilosc)` bo znaki są niespójne** |
| `magazyn` | int | ID magazynu LINII (może być inne niż `MG.magazyn`!) |
| `cena_brutto`, `cena_netto` | decimal | Ceny |
| `vat` | int | Stawka VAT |

### 1.3. `HM.TW` — Towary (kartoteka)

| Kolumna | Typ | Opis |
|---|---|---|
| `id` | int PK | ID towaru |
| `kod` | varchar | Kod towaru np. "Kurczak A" |
| `nazwa` | varchar | Pełna nazwa |
| **`katalog`** | int | **Kategoria towaru — KLUCZOWA!** Patrz tabela katalogów niżej |
| `aktywny` | bit | Aktywność |
| `info` | varchar | Notatki |

### 1.4. `SSCommon.STContractors` — Kontrahenci

```sql
SELECT id, kod, shortcut, nazwa1, nazwa2, LimitAmount FROM SSCommon.STContractors
```
- `id` ↔ `HM.MG.khid` ↔ `ContractorClassification.ElementId`
- `shortcut` — krótka nazwa (np. "BIEDRONKA", "DEHEUS")
- `nazwa1`, `nazwa2` — pełna nazwa firmy
- `LimitAmount` — limit kredytowy (decimal, NULL akceptowane → ISNULL)

### 1.5. `SSCommon.ContractorClassification` — Wymiary klasyfikacji (Handlowiec, Kilometry…)

🚨 **KRYTYCZNA TABELA z 3 INSTEAD OF triggerami** — patrz `26_Modul_Zamowien_v2.md` §4.

**Schema:**
```sql
ContractorClassification (USER_TABLE):
  Guid                  uniqueidentifier
  ElementId             int           -- = STContractors.id (klucz JOIN)
  CDim_Handlowiec       int           -- ⭐ FK do słownika wymiarów (źródło prawdy!)
  CDim_Handlowiec_Val   nvarchar(1000) -- denormalizowana nazwa (np. "Ania", "Justyna")
  CDim_Kilometry        smallint
  CDim_Blokuj#wysyłanie#powiadomień#o#płatnościach  bit
  CDim_pojHM_6770_1     nvarchar(1000) -- inny wymiar
```

**Triggery:**
- `ContractorClassification_TH_IOI` — INSTEAD OF INSERT
- `ContractorClassification_TH_IOU` — INSTEAD OF UPDATE
- `ContractorClassification_TH_AD`  — AFTER DELETE

**Skutek dla UPDATE:** musisz ustawić `CDim_Handlowiec` (FK) — sam `_Val` zostanie nadpisany na NULL przez trigger! Użyj wzorca UPSERT z kopiowaniem FK z istniejącego klienta:

```sql
DECLARE @hid INT = (SELECT TOP 1 CDim_Handlowiec
                    FROM [HANDEL].[SSCommon].[ContractorClassification]
                    WHERE CDim_Handlowiec_Val = @nazwa AND CDim_Handlowiec IS NOT NULL);
IF EXISTS (SELECT 1 FROM ContractorClassification WHERE ElementId = @id)
    UPDATE ContractorClassification SET CDim_Handlowiec = @hid, CDim_Handlowiec_Val = @nazwa WHERE ElementId = @id;
ELSE
    INSERT INTO ContractorClassification (Guid, ElementId, CDim_Handlowiec, CDim_Handlowiec_Val)
    VALUES (NEWID(), @id, @hid, @nazwa);
```

**JOIN konwencja w projekcie (~50 użyć):**
```sql
LEFT JOIN [HANDEL].[SSCommon].[ContractorClassification] WYM ON c.Id = WYM.ElementId
-- wartość handlowca: WYM.CDim_Handlowiec_Val
```

### 1.6. 🚨 `HM.DK.wystawil` to KSIĘGOWA, NIE handlowiec (odkrycie 2026-05-12)

**Problem:** `HM.DK.wystawil` (int FK → `SSCommon.STUsers.Id`) wygląda jakby był autorem faktury — ale w rzeczywistości to **księgowa która technicznie wbiła fakturę do Sage**, nie handlowiec sprzedażowy.

**TOP wystawcy faktur sprzedaży (typ=0, niezanulowane) — wszyscy = księgowość:**

| Id | Login | Pełne imię | Faktur (2022→) | Suma netto |
|---|---|---|---:|---:|
| 32831 | RB | Renata Balcerak | 33,018 | 454M PLN |
| 32815 | MSS | Małgorzata Stępniak | 30,102 | 418M PLN |
| 32772 | EK | Edyta Kochanowska | 13,022 | 12M PLN |
| 32781 | MM | Magdalena Miler | 5,710 | 196M PLN |
| 32805 | TZ | Teresa Zuchora | 5,355 | 85M PLN |
| 32797 | MD | Marlena Andrzejczak | 4,543 | 7M PLN |
| 32816 | DS | **Dawid Sosiński** (b. handlowiec) | 279 | 4.6M PLN (TYLKO 2022!) |
| **32856** | **Daniel.C** | **Daniel Czapnik** (b. handlowiec) | **0** | — |

**Konsekwencje strategiczne:**

1. **`wystawil` jest BEZUŻYTECZNY do atrybucji handlowca**. Dla każdego klienta — niezależnie od tego kto sprzedawał — wystawi go RB albo MSS (księgowe).
2. **Daniel Czapnik nigdy nie wystawiał faktur** w Sage — jego rola sprzedażowa nie zostawiła śladu w `HM.DK`.
3. **Jedyne źródło atrybucji** = `SSCommon.ContractorClassification.CDim_Handlowiec_Val`. To CURRENT STATE — gdy klient zmieni handlowca, WSZYSTKIE jego faktury historyczne pokażą NOWEGO handlowca w analizach.
4. **Audyt "kto sprzedał klientowi X w roku Y" jest NIEMOŻLIWY** ze strukturalnych danych HANDEL. Trzeba dorobić tabelę historii klasyfikacji albo użyć alternatywnego źródła (np. `LibraNet.ZamowieniaMieso.IdUser`).

**Konsekwencje dla zapytań w ZPSP:**
- Nie używaj `HM.DK.wystawil` do filtrowania per handlowiec — wybierzesz tylko fakturę RB albo MSS.
- Zawsze JOIN przez `ContractorClassification`, akceptując że historia jest tracona.
- Dla SPECYFICZNYCH zapytań "co zrobił Daniel/Dawid" — szukaj w `LibraNet.ZamowieniaMieso.IdUser` (mapowanie przez `UserHandlowcy`) lub w `Pozyskiwanie_Aktywnosci.UzytkownikId`.

**Co dalej:** rozważyć dorobienie tabeli audytu zmian `CDim_Handlowiec_Val` (3 triggery TH_AD/IOI/IOU mogą być rozszerzone o INSERT do log table). Bez tego nie da się odpowiedzieć na pytania typu "ile sprzedał Daniel w 2024" wstecznie.

---

## 2. Katalogi towarów (`HM.TW.katalog`)

Kluczowe ID katalogów do filtrowania:

| Katalog | Nazwa | Co zawiera | Użycie |
|---:|---|---|---|
| **65882** | Żywiec | Kurczak żywy 7-12 (klasy wagowe) | Wejście — sPZ od hodowców |
| **67094** | Odpady | Pióra, krew, kości, niejadalne | Wyjście — sPWU/MM- |
| **67095** | Mięso (świeże) | Tuszki A/B, podroby, filet, korpus, skrzydło itp. | Główny output produkcji |
| **67104** | Mięso (inne) | Drugorzędne towary mięsne | Alternatywa dla 67095 |
| **67153** | Mrożone | Towary z mroźni | sMM- → M.MROŹ |

**Reguła**: dla bilansu materiałowego mięsa filtruj `TW.katalog IN (67095, 67104)`. Dla żywca: `katalog = 65882`. Dla pełnej produkcji: `katalog IN (65882, 67094, 67095, 67104, 67153)`.

---

## 3. Series codes — pełna mapa

Każdy dokument w Symfonii ma serię. Litera `s` na początku to nowa generacja Symfonii (po 2021), bez `s` = stara. Funkcjonalnie identyczne.

### 3.1. PRZYCHODY (dodają stan magazynowy)

| Seria | Pełna nazwa | Typowy magazyn | Kategoria |
|---|---|---|---|
| `sPZ`, `PZ` | Przyjęcie zewnętrzne | 65550 / 65543 / 65566 | Skup od hodowców (kat. 65882) lub paszy (65883) |
| `sPZK`, `PZK` | Przyjęcie korekta | dowolny | Korekta PZ |
| `PZH` | Przyjęcie z handlu (zwrot) | 65556 | Zwroty od klientów |
| `sPWU`, `PWU` | Przychód wewnętrzny ubojnia | **65555 (M.UBOJ)** | Tuszki/podroby/odpady po uboju |
| `sPWP`, `PWP` | Przychód wewnętrzny produkcja | **65554 (M.PROD)** | Elementy po krojeniu |
| `sPPM`, `PPM` | Przychód wewnętrzny masarnia | **65562 (M.MASAR)** | Wędliny, produkty masarskie |
| `sPPK`, `PPK` | Przychód produkcja karmy | **65547 (KARMA)** | Karma dla zwierząt |
| `sPKM`, `PKM` | Przychód korekcyjny magazynowy | dowolny | Korekta inwentarzowa |
| `PrW` | Przyjęcie wewnętrzne | dowolny | Rzadkie |
| `sMM+`, `MM+` | Przesunięcie międzymagazynowe (przychód) | docelowy | Para z sMM- |
| `sMP`, `MP` | Przyjęcie do magazynu opakowań | 65559 | Zakup opakowań |

### 3.2. ROZCHODY (zmniejszają stan)

| Seria | Pełna nazwa | Typowy magazyn | Cel |
|---|---|---|---|
| `sWZ`, `WZ` | Wydanie z magazynu | **65556 (M.DYST)** | Sprzedaż klientowi |
| `sWZ-W`, `WZ-W` | Wydanie wewnętrzne | dowolny | Pracownik / użytek wewn. |
| `sWZK`, `WZK` | Wydanie korekta | dowolny | Korekta WZ |
| `sWZKW`, `WZKW` | Wydanie korekta WZ-W | dowolny | Korekta wydania wewn. |
| `sRWU`, `RWU` | Rozchód wewnętrzny ubój | 65555 → 65555 lub 65555 z 65882 | Żywiec do uboju |
| `sRWP`, `RWP` | Rozchód wewnętrzny produkcja | 65554 (M.PROD) | Tuszka do krojenia |
| `sRPM`, `RPM` | Rozchód wewnętrzny masarnia | 65562 | Surowiec do masarni |
| `sRPK`, `RPK` | Rozchód produkcja karmy | 65547 | Składniki do karmy |
| `RWO` | Rozchód operacyjny | dowolny | Rzadkie, koszty |
| `sMM-`, `MM-` | Przesunięcie międzymagazynowe (rozchód) | źródłowy | Para z sMM+ |
| `sMW`, `MW` | Wydanie z magazynu opakowań | 65559 | Wydanie opakowań do produkcji |

---

## 4. KLUCZOWE ODKRYCIA (gotchas)

### 4.1. **`HM.MG.anulowany = 0`** — ZAWSZE w WHERE!
Sage NIE usuwa anulowanych dokumentów, tylko ustawia flagę. Bez filtra anulowane wpływają na sumy.

### 4.2. **`HM.MZ.kod` może być duplikowany** w wielolinjowych dokumentach
Jeden `MG.id` może mieć kilka `MZ` z tym samym `kod` (np. dwie partie tego samego towaru). Przy `STRING_AGG(MG.kod)` powstają duplikaty. Rozwiązanie: **CTE z pre-agregacją per `MG.id+MZ.kod`** zanim aggreguj numery.

### 4.3. **`sMM-` i `sMM+` to OSOBNE dokumenty MG** (nie linie tego samego!)
Wbrew intuicji, każde przesunięcie to **DWA dokumenty** w `HM.MG`:
- `sMM-` w magazynie źródłowym (`magazyn = source`)
- `sMM+` w magazynie docelowym (`magazyn = destination`)

**Subquery na siblingach `MZ` o różnym magazynie NIE DZIAŁA** (tabela `MZ` ma tylko 1 linię w MG dla MM-).

**Rozwiązanie**: użyj `MG.khdzial` — Sage repurposuje to pole:
- Dla `sMM-`: `khdzial = ID magazynu DOCELOWEGO`
- Dla `sMM+`: `khdzial = ID magazynu ŹRÓDŁOWEGO`

Zweryfikowane na 30+ parach dokumentów (2026-05-09).

### 4.4. **`HM.MZ.ilosc` ma niespójne znaki**
Czasem dodatnie, czasem ujemne. **Zawsze `ABS(MZ.ilosc)`** w sumach. Kierunek (przychód/rozchód) wyciąga się z `MG.seria`, nie ze znaku.

### 4.5. **NIE MA tabeli słownika magazynów w bazie HANDEL**!
Sage Symfonia trzyma nazwy magazynów w **UI / konfiguracji aplikacji**, nie w SQL.

Sprawdzone tabele (puste lub bez nazw):
- `HM.MA` — istnieje, ale puste w bazie Pióroskovskich
- `HM.MAG` — nie istnieje
- `HM.MS` — nie istnieje
- `SSCommon.STMagazyny` — nie istnieje
- `HM.WarehouseKinds` — 6 wierszy = TYPY, nie nazwy
- `HM.UserRightsToWarehouses` — uprawnienia, nie nazwy
- `HM._MagazynMap` — wewnętrzna mapa techniczna

**Rozwiązanie**: parsowanie sufiksów kodu `kod` w MM+/MM-:
```
"0001/22/MM-/M. PROD"  → magazyn=65554 ID źródła, sufiks "M. PROD" = nazwa źródła
"0001/22/MM+/M. DYST"  → magazyn=65556 ID celu, sufiks "M. DYST" = nazwa celu
```
Implementacja: `MagazynyHelper.LoadFromDatabaseAsync()` w `AnalitykaPelna/Services/`.

Patrz `BAZA_WIEDZY/24_Magazyny_i_Lancuch_Produkcji.md` dla pełnego mapowania.

### 4.6. **HM.MZ.magazyn vs HM.MG.magazyn**
Większość zapytań używa `MZ.magazyn` (per linia). `MG.magazyn` jest często ten sam co `MZ.magazyn`, ale dla MM- pojawiają się różnice. Filtruj zwykle po `MZ.magazyn`.

### 4.7. **Daty jako string dla LibraNet**
LibraNet to SQL 2008 R2 — brak `TRY_CONVERT`. Daty wysyłaj jako string `yyyy-MM-dd`:
```csharp
cmd.Parameters.AddWithValue("@DataOd", f.DataOd.ToString("yyyy-MM-dd"));
```
HANDEL natomiast OK z `DateTime`:
```csharp
cmd.Parameters.AddWithValue("@DataOd", f.DataOd.Date);
```

### 4.8. **`STRING_AGG` wymaga SQL 2017+**
HANDEL: OK. LibraNet: NIE — używaj `STUFF + FOR XML PATH('')` jako fallback.

### 4.9. **`MG.ProductionOrderID`, `MG.IsProductionTrash`, `MF.Production*`** — PUSTE
Moduł "Production" Sage'a nigdy nie wdrożony. Tabela `MF` (87 tabel z prefiksem) — pusta.

### 4.10. **`HM.TW.katalog` to INT, nie string!** (gotcha)
Mimo że wartości typu "67095" wyglądają jak string, kolumna jest **INTEGER**. Nie używaj `rd.GetString()`:
```csharp
// ❌ Crash: InvalidCastException "Unable to cast object of type System.Int32 to System.String"
SELECT Id, Kod, katalog FROM HM.TW WHERE katalog IN ('67095','67153')
// rd.GetString(2) → CRASH

// ✅ Poprawnie: cast w SQL i czytaj jako object/string
SELECT Id, Kod, CAST(katalog AS NVARCHAR(32)) AS Katalog FROM HM.TW WHERE katalog IN ('67095','67153')
// rd["Katalog"]?.ToString() → OK
```
Parametry `WHERE katalog IN ('67095','67153')` działają mimo string-vs-int (auto-conversion), ale czytanie wymaga castu.

### 4.11. **`SSCommon.ContractorClassification` ma INSTEAD OF triggery!**
3 triggery (`TH_IOI`, `TH_IOU`, `TH_AD`). Aby zmienić handlowca w UPDATE musisz ustawić **OBYDWIE** kolumny: `CDim_Handlowiec` (int FK) + `CDim_Handlowiec_Val`. Pełen opis i strategia → §1.5 wyżej + `26_Modul_Zamowien_v2.md`.

### 4.12. **CTE pattern dla `STRING_AGG` z dokumentami**
Dla zachowania unikalności numerów dokumentów w grupowaniach:
```sql
WITH PerDok AS (
    SELECT TW.nazwa, MZ.kod, MG.seria, MZ.magazyn,
           MG.id AS DokId, MG.kod AS DokKod,
           SUM(ABS(MZ.ilosc)) AS DokKg
    FROM HM.MG MG
    JOIN HM.MZ MZ ON MZ.super = MG.id
    JOIN HM.TW TW ON TW.id = MZ.idtw
    WHERE MG.anulowany = 0 AND MG.data BETWEEN @DataOd AND @DataDo
    GROUP BY TW.nazwa, MZ.kod, MG.seria, MZ.magazyn, MG.id, MG.kod
)
SELECT nazwa, kod, seria, magazyn,
       SUM(DokKg) AS Kg,
       COUNT(*) AS LiczbaDok,
       STRING_AGG(DokKod, ', ') WITHIN GROUP (ORDER BY DokId) AS NumeryDok
FROM PerDok
GROUP BY nazwa, kod, seria, magazyn;
```

---

## 5. Struktura `HM.MG` — fragmenty z analizy 1000 wierszy

Z dumpu od użytkownika (2026-05-09), kluczowe wzorce:

### 5.1. PZ — przyjęcie zewnętrzne (od hodowcy)
```
id=65570 sPZ data=2021-12-28 magazyn=65550 khid=1586 wartoscWz=-435.47
id=65751 sPZ data=2022-01-04 magazyn=65556 khid=15 (pz 015)
```
Dla **żywca** sPZ idzie zwykle na 65550 lub 65556. `khid` to ID hodowcy, `wartoscWz` to kwota faktury (czasem ujemna).

### 5.2. PWU — przychód ubojnia
```
id=65612 sPWU data=2022-01-03 magazyn=65555 wartk=-527326,89  ← UWAGA: zawsze 65555!
id=65857 sPWU data=2022-01-04 magazyn=65555 wartk=-594956,7
id=66050 sPWU data=2022-01-05 magazyn=65555 wartk=-537209,75
```
**Wszystkie sPWU lądują na 65555** = M.UBOJ (Magazyn Ubojni).

### 5.3. PWP — przychód produkcja (po krojeniu)
```
id=65571 PWP data=2022-01-03 magazyn=65554 (trybowanie)
id=65604 PWP data=2022-01-03 magazyn=65554 (Zmiana I)
```
**PWP zawsze na 65554** = M.PROD.

### 5.4. MM- (rozchód MM)
```
id=65577 sMM- magazyn=65554 khdzial=65556 (kod="0001/22/MM-/M. PROD")  → z M.PROD do M.DYST
id=65623 sMM- magazyn=65555 khdzial=65556 (kod="0006/22/MM-/M.UBOJ")   → z M.UBOJ do M.DYST
id=65749 sMM- magazyn=65562 khdzial=65556 (kod="0014/22/MM-/M.MASAR")  → z masarni do dyst.
```

### 5.5. MM+ (przychód MM, dokument parny)
```
id=65579 sMM+ magazyn=65556 khdzial=65554 (kod="0001/22/MM+/M. DYST")  → para 65577
id=65624 sMM+ magazyn=65556 khdzial=65555 (kod="0006/22/MM+/M. DYST")  → para 65623
id=65750 sMM+ magazyn=65556 khdzial=65562 (kod="0014/22/MM+/M. DYST")  → para 65749
```

**Reguła parowania**: `sMM-.id` i `sMM+.id` zwykle są kolejnymi numerami (różnica 1-2). Numer w kodzie (`0001`, `0014`) jest wspólny dla pary. Sufiks `M. PROD` w sMM- = źródło, `M. DYST` w sMM+ = cel.

### 5.6. WZ — wydanie do klienta
```
id=65556 sWZ magazyn=65556 khid=232 wartoscWz=4215 (wz 000005)
id=65557 sWZ magazyn=65556 khid=122 wartoscWz=6181 (wz 000003)
```
**Wszystkie sWZ z 65556** = M.DYST. `khid` to klient.

### 5.7. MW — wydanie z magazynu opakowań
```
id=65558 sMW magazyn=65559 khid=122 (faktura 0001/22/FVS)
```
Wszystkie sMW z **65559** = Magazyn opakowań.

---

## 6. Skrypty eksploracji

W `BAZA_WIEDZY/SELECTY/`:
- `EKSPLORACJA_HANDEL_FULL.sql` — pełna eksploracja schemy
- `EKSPLORACJA_HANDEL_2.sql` — wersja 2
- `EKSPLORACJA_MAGAZYNY_HANDEL.sql` — szukanie magazynów (5 części z fallbackiem)
- `EKSPLORACJA_CROSS_DB.sql` — sprawdzanie cross-DB
- `EKSPLORACJA_ZALEZNOSCI.sql` — zależności między tabelami
- `REKOMENDACJE_INDEKSY_ANALITYKA.sql` — rekomendowane indeksy

Wyniki w `WYNIKI_*.txt` / `WYNIKI_*.md`.

---

## 7. Wyzwania performance

Tabela `HM.MZ` ma **miliony wierszy** (każda pozycja każdego dokumentu od ~2018). Bez indeksów na `super`, `idtw`, `magazyn` - wolne joiny.

**Rekomendacje** (patrz `REKOMENDACJE_INDEKSY_ANALITYKA.sql`):
1. `IX_MZ_super` — JOIN z MG
2. `IX_MZ_magazyn_super` — filtr po magazynie
3. `IX_MZ_idtw` — JOIN z TW

W zapytaniach analitycznych zawsze:
- `CommandTimeout = 60` minimum dla okresów >1 miesiąc
- `MG.[anulowany] = 0` w WHERE
- `MG.[data] >= @DataOd AND MG.[data] <= @DataDo` (BETWEEN ze znanym zakresem)

---

**Aktualizacja**: 2026-05-09 — dokument utworzony po refactorze "Stan magazynów" w Bilansie materiałowym.
**Powiązane**: 24_Magazyny_i_Lancuch_Produkcji.md, 25_Analityka_Pelna_v2_StanMagazynow.md, 13_Bazy_danych.md.
