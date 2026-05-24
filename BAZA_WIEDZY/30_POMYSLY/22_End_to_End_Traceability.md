# 22. ⭐ End-to-End Traceability per tuszka — PEŁNY PORADNIK

## Co to jest
**Traceability** = w każdej sekundzie wiesz: każda tuszka z którego hodowcy, którego dnia ubita, w której partii, dla którego klienta sprzedana, gdzie pojechała.

## DLACZEGO TO PRIORYTET PRAWNY

### Wymóg UE 178/2002 (Food Law)
> Artykuł 18: **Operatorzy żywności i pasz muszą zapewnić traceability na wszystkich etapach produkcji, przetwórstwa i dystrybucji**.

To znaczy: jeśli klient zgłasza problem zdrowotny → **w 24h musisz powiedzieć** skąd jest produkt.

### Co grozi za brak
- **RASFF alert** (EU rapid alert system) → automatyczne wycofanie produktu we wszystkich krajach
- **Kara EU**: do **5% rocznego obrotu** (dla Was = **12.9 mln zł max kara**)
- **Utrata kontraktów**: Lidl, Tesco, Auchan żądają traceability w warunkach umowy
- **BRC v9 sekcja 3.9**: bez traceability **niezgodność krytyczna**

### Scenariusz katastrofy
**Bez systemu**:
- 14.08 — klient zgłasza Salmonella w produkcie z partii #1247
- Nie wiesz która konkretna paleta, który hodowca
- Wycofujesz **CAŁĄ produkcję** dnia 12.08 = ~200 ton × 12 zł = **2.4M PLN strat** + reklamacje + reputacja
- Sanepid: kara, RASFF alert, panika w mediach

**Z systemem**:
- Klient daje numer z opakowania
- W systemie: paleta 489, partia 1247, hodowca Wiśniewski (farma F-12)
- Wycofujesz **TYLKO paletę 489** = 1000 kg × 12 zł = **12 000 PLN**
- Badasz farmę Wiśniewskiego, znajdujesz źródło, rozwiązujesz

**Różnica: 200× mniej strat** + **brak RASFF** + **opanowana sytuacja**.

---

## CO JUŻ MASZ vs CZEGO BRAKUJE

### Już masz w ZPSP:
- ✅ `PartiaDostawca` — hodowca → partia
- ✅ `listapartii` — partia z metadatą (data, waga)
- ✅ `In0E` — paleta → partia (P1 = klucz)
- ✅ `HM.MG/MZ` — dokumenty (sPZ, sPWU, sPWP, sMM-, sWZ)
- ✅ `HM.KH` — klient
- ✅ Webfleet — gdzie pojechała ciężarówka

### Brakuje:
- ❌ **Link paleta po krojeniu → tuszka surowa** (krojenie miesza palety!)
- ❌ **Link klient (sWZ) → konkretne palety wydania** (jest tylko ogólny)
- ❌ **Numer śledzenia drukowany na opakowaniu** (klient nie zna go)
- ❌ **Lot number** zgodny z normami EU
- ❌ **Recall system** — przycisk "wycofaj partię X" z auto-listą klientów

---

## ARCHITEKTURA TRACEABILITY

### Model danych (uproszczony graph)
```
HODOWCA (Pozyskiwanie_Hodowcy)
    │
    │ 1:N
    ▼
PARTIA (listapartii) ──── PartiaDostawca
    │
    │ 1:N
    ▼
PALETA SUROWA (In0E.P1)
    │
    │ produkcja sPWU → sPWP
    │
    │ Tu jest LUKA: krojenie miesza palety surowe
    │ Jedna paleta wyrobu może zawierać tuszki z 3-5 palet surowych
    │
    ▼
PALETA WYROBU (nowa tabela: PaletaWyrob)
    │
    │ N:M poprzez PaletaWyrobSklad (paleta_wyrob_id, paleta_surowa_id, kg)
    │
    │
    ▼
DOKUMENT WYDANIA (HM.MG sWZ)
    │
    │ 1:N poprzez DokumentPaletWydania
    │
    ▼
KLIENT (HM.KH) + LOT NUMBER drukowany na opakowaniu
    │
    │ powiązanie z Webfleet trip
    ▼
DOSTAWA (GPS+temp z transportu)
```

---

## DATABASE SCHEMA

### Nowe tabele
```sql
-- LibraNet (bo paleta zaczyna się tu)
CREATE TABLE PaletaWyrob (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    LotNumber NVARCHAR(50) NOT NULL UNIQUE,  -- np. 'PIO-2026-05-23-001'
    DataProdukcji DATE NOT NULL,
    SmianaProdukcyjna NVARCHAR(20) NULL,
    LiniaProdukcyjna NVARCHAR(20) NULL,
    OperatorId NVARCHAR(50) NULL,
    KodTowaru NVARCHAR(50) NOT NULL,
    NazwaTowaru NVARCHAR(200) NULL,
    LiczbaSztuk INT NULL,
    WagaCalkowitaKg DECIMAL(10,2) NOT NULL,
    DataWaznosci DATE NULL,
    StatusPalety NVARCHAR(20) NOT NULL DEFAULT 'NA_MAGAZYNIE',
    -- NA_MAGAZYNIE, WYSLANO, ZWROT, ZUTYLIZOWANO, WYCOFANO
    DokumentMGId BIGINT NULL  -- powiązanie z dokumentem HM.MG po wytworzeniu (sPWP)
);
CREATE INDEX IX_PaletaWyrob_LotNumber ON PaletaWyrob(LotNumber);
CREATE INDEX IX_PaletaWyrob_DataProdukcji ON PaletaWyrob(DataProdukcji);
CREATE INDEX IX_PaletaWyrob_StatusTowar ON PaletaWyrob(StatusPalety, KodTowaru);

CREATE TABLE PaletaWyrobSklad (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    PaletaWyrobId BIGINT NOT NULL FOREIGN KEY REFERENCES PaletaWyrob(Id),
    PaletaSurowaP1 INT NOT NULL,  -- klucz In0E.P1
    PartiaId INT NOT NULL,  -- listapartii.LP
    WagaKgUdzial DECIMAL(10,2) NOT NULL,  -- ile kg z tej palety surowej trafiło
    Notatki NVARCHAR(500) NULL
);
CREATE INDEX IX_PaletaWyrobSklad_PaletaWyrob ON PaletaWyrobSklad(PaletaWyrobId);
CREATE INDEX IX_PaletaWyrobSklad_PaletaSurowa ON PaletaWyrobSklad(PaletaSurowaP1);
CREATE INDEX IX_PaletaWyrobSklad_Partia ON PaletaWyrobSklad(PartiaId);

CREATE TABLE DokumentPaletWydania (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    DokumentMGId BIGINT NOT NULL,  -- HM.MG.id (sWZ)
    PaletaWyrobId BIGINT NOT NULL FOREIGN KEY REFERENCES PaletaWyrob(Id),
    LiczbaSztuk INT NULL,
    WagaKgWydana DECIMAL(10,2) NOT NULL,
    DataWydania DATETIME NOT NULL
);
CREATE INDEX IX_DokPaletWyd_MG ON DokumentPaletWydania(DokumentMGId);
CREATE INDEX IX_DokPaletWyd_Paleta ON DokumentPaletWydania(PaletaWyrobId);

CREATE TABLE DostawaKlientGPS (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    DokumentMGId BIGINT NOT NULL,  -- HM.MG.id (sWZ)
    StartDateTime DATETIME NOT NULL,
    EndDateTime DATETIME NULL,
    KierowcaId NVARCHAR(50) NULL,
    PojazdRej NVARCHAR(20) NULL,
    WebfleetTripId NVARCHAR(100) NULL,
    KlientAdres NVARCHAR(500) NULL,
    KlientNIP NVARCHAR(30) NULL,
    DataDoreczenia DATETIME NULL,
    PodpisOdbiorcy NVARCHAR(200) NULL,
    PotwierdzeniePath NVARCHAR(500) NULL  -- ścieżka do PDF/zdjęcia podpisu
);

-- Tabela wycofania (recall)
CREATE TABLE Recall (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    RecallNumber NVARCHAR(50) NOT NULL UNIQUE,  -- 'REC-2026-001'
    DataInicjacji DATETIME NOT NULL,
    InicjowanyPrzez NVARCHAR(50) NULL,
    PowodRecall NVARCHAR(500) NULL,
    KategoriaRecall NVARCHAR(50) NOT NULL,  
    -- BEZPIECZENSTWO_ZYWNOSCI, ALERGENY, MIKROBIOLOGIA, JAKOSC, INNE
    StatusRecall NVARCHAR(20) NOT NULL DEFAULT 'OTWARTY',
    -- OTWARTY, W_REALIZACJI, ZAMKNIETY, ESKALOWANY_DO_RASFF
    
    -- Zakres
    TypZakresu NVARCHAR(20) NOT NULL,  
    -- PALETA, PARTIA, HODOWCA, DATA_PRODUKCJI, TOWAR
    ZakresIdentyfikator NVARCHAR(200) NULL,
    
    -- Statystyki
    LiczbaPaletDotknietych INT NULL,
    LiczbaKlientowDotknietych INT NULL,
    WagaKgDotknieta DECIMAL(12,2) NULL,
    WartoscPLN DECIMAL(12,2) NULL,
    
    -- Działania
    PowiadomieniWyslano DATETIME NULL,
    ProduktZebrany_kg DECIMAL(12,2) NULL,
    ProduktZutylizowany_kg DECIMAL(12,2) NULL,
    DataZamkniecia DATETIME NULL,
    NotatkiKoncowe NVARCHAR(MAX) NULL
);

CREATE TABLE RecallPalety (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    RecallId BIGINT NOT NULL FOREIGN KEY REFERENCES Recall(Id),
    PaletaWyrobId BIGINT NOT NULL FOREIGN KEY REFERENCES PaletaWyrob(Id),
    Status NVARCHAR(20) NOT NULL DEFAULT 'OBJETA_RECALL',
    -- OBJETA_RECALL, KONTAKT_KLIENT, ODEBRANA, ZUTYLIZOWANA, NIEODNALEZIONA
    KlientPowiadomiony BIT NOT NULL DEFAULT 0,
    DataKontaktu DATETIME NULL,
    Notatki NVARCHAR(1000) NULL
);
```

---

## SERWIS TRACEABILITY

**Plik**: `Services/TraceabilityService.cs`

```csharp
public class TraceabilityService
{
    private readonly string _connLibraNet;
    private readonly string _connHandel;

    /// <summary>
    /// FORWARD TRACE: od hodowcy → wszyscy klienci którzy dostali jego produkt
    /// </summary>
    public async Task<ForwardTraceResult> TraceFromHodowca(int hodowcaId, DateTime od, DateTime doDate)
    {
        var result = new ForwardTraceResult { HodowcaId = hodowcaId };

        // 1. Partie tego hodowcy w okresie
        var partie = await GetPartieAsync(hodowcaId, od, doDate);
        result.Partie = partie;

        // 2. Dla każdej partii — palety surowe
        foreach (var partia in partie)
        {
            var paletySurowe = await GetPaletySuroweAsync(partia.Id);
            partia.PaletySurowe = paletySurowe;

            // 3. Dla każdej palety surowej — w jakich paletach wyrobu się znalazła
            foreach (var paletaSur in paletySurowe)
            {
                var paletyWyrobu = await GetPaletyWyrobuZawierajacePalateSurowaAsync(paletaSur.P1);
                paletaSur.PaletyWyrobu = paletyWyrobu;

                // 4. Dla każdej palety wyrobu — do których klientów poszła
                foreach (var paletaW in paletyWyrobu)
                {
                    var dostawy = await GetDostawyDlaPaletyWyrobuAsync(paletaW.Id);
                    paletaW.Dostawy = dostawy;
                }
            }
        }

        result.LiczbaKlientow = result.Partie
            .SelectMany(p => p.PaletySurowe)
            .SelectMany(ps => ps.PaletyWyrobu)
            .SelectMany(pw => pw.Dostawy)
            .Select(d => d.KlientId)
            .Distinct()
            .Count();
        
        return result;
    }

    /// <summary>
    /// REVERSE TRACE: od produktu na półce klienta → hodowca i wszystko po drodze
    /// </summary>
    public async Task<ReverseTraceResult> TraceFromLot(string lotNumber)
    {
        var result = new ReverseTraceResult { LotNumber = lotNumber };

        // 1. Znajdź paletę wyrobu po lot number
        var palaWyrobu = await GetPaletaWyrobuByLotAsync(lotNumber);
        if (palaWyrobu == null)
        {
            result.Error = "Lot number nie znaleziony";
            return result;
        }
        result.PaletaWyrobu = palaWyrobu;

        // 2. Z czego się składała (palety surowe + partie)
        var sklad = await GetSkladPaletyWyrobuAsync(palaWyrobu.Id);
        result.SkladPalet = sklad;

        // 3. Każda partia → hodowca
        foreach (var item in sklad)
        {
            var partia = await GetPartiaWithHodowcaAsync(item.PartiaId);
            item.Partia = partia;
        }

        // 4. Wszystkie wydania tej palety (kto kupił)
        var wydania = await GetWydaniaPaletyAsync(palaWyrobu.Id);
        result.Wydania = wydania;

        // 5. Dostawa GPS (gdzie pojechała ciężarówka)
        foreach (var wyd in wydania)
        {
            wyd.DostawaGPS = await GetDostawyGPSDlaDokumentuAsync(wyd.DokumentMGId);
        }

        return result;
    }

    /// <summary>
    /// RECALL: wycofaj produkt z określonego zakresu
    /// </summary>
    public async Task<RecallResult> InitiateRecall(RecallRequest request)
    {
        var recall = new Recall
        {
            RecallNumber = await GenerateRecallNumberAsync(),
            DataInicjacji = DateTime.Now,
            InicjowanyPrzez = request.UserId,
            PowodRecall = request.Powod,
            KategoriaRecall = request.Kategoria,
            TypZakresu = request.TypZakresu,
            ZakresIdentyfikator = request.ZakresIdentyfikator
        };

        // 1. Znajdź wszystkie palety w zakresie
        var palety = request.TypZakresu switch
        {
            "HODOWCA" => await GetPaletyFromHodowcaAsync(int.Parse(request.ZakresIdentyfikator)),
            "PARTIA" => await GetPaletyFromPartiaAsync(int.Parse(request.ZakresIdentyfikator)),
            "DATA_PRODUKCJI" => await GetPaletyFromDateAsync(DateTime.Parse(request.ZakresIdentyfikator)),
            "TOWAR" => await GetPaletyFromTowarAsync(request.ZakresIdentyfikator),
            _ => throw new ArgumentException("Nieznany typ zakresu")
        };

        recall.LiczbaPaletDotknietych = palety.Count;
        recall.WagaKgDotknieta = palety.Sum(p => p.WagaCalkowitaKg);

        // 2. Znajdź wszystkich klientów, którzy dostali te palety
        var klienci = await GetKlienciDlaPaletAsync(palety.Select(p => p.Id).ToList());
        recall.LiczbaKlientowDotknietych = klienci.Count;

        // 3. Zapisz Recall + RecallPalety
        var recallId = await SaveRecallAsync(recall);
        foreach (var p in palety)
            await SaveRecallPaletAsync(recallId, p.Id);

        // 4. Auto-zmiana statusu palet
        await UpdatePaletStatusAsync(palety.Select(p => p.Id).ToList(), "WYCOFANO");

        // 5. Powiadom klientów (email + SMS)
        foreach (var k in klienci)
            await SendRecallNotification(k, recall);

        return new RecallResult 
        { 
            RecallId = recallId,
            LiczbaPaletDotknietych = palety.Count,
            LiczbaKlientow = klienci.Count,
            WagaTotalKg = recall.WagaKgDotknieta ?? 0
        };
    }
}
```

---

## DRUKOWANIE LOT NUMBER NA OPAKOWANIU

### Format lot number
```
PIO-2026-05-23-001
│   │    │  │  │
│   │    │  │  └ numer sekwencyjny w dniu
│   │    │  └─── dzień
│   │    └────── miesiąc
│   └─────────── rok
└─────────────── kod producenta (Piórkowscy)
```

### Hardware drukowania
**Drukarki etykiet do produktu**:
- **Zebra ZT411** (przemysłowa, RFID-ready) — 4000-6000 zł
- **Wago Brother QL-820NWB** (proste etykiety) — 1500 zł
- **TSC TTP-244 Pro** (budget) — 800 zł

**Etykieta na palecie/opakowaniu**:
```
┌─────────────────────────────────────┐
│ ★ PIÓRKOWSCY ★                     │
│                                     │
│ Filet z piersi kurczaka 1kg         │
│                                     │
│ Lot: PIO-2026-05-23-001             │
│ Produkcja: 23.05.2026               │
│ Najlepiej zużyć przed: 30.05.2026   │
│ Waga: 12.5 kg                       │
│                                     │
│ [QR CODE]                           │
│                                     │
│ www.piorkowscy.pl/trace/PIO-2026... │
└─────────────────────────────────────┘
```

**QR code**: prowadzi do publicznej strony z informacją:
- Hodowca (anonimowo: "Hodowca z woj. wielkopolskiego")
- Data uboju
- Temperatura transportu OK
- Certyfikaty (BRC, dobrostan)

To **marketingowy plus** — klient skanuje, widzi że jesteście transparentni.

---

## UI — Okno Traceability

**Plik**: `Traceability/TraceabilityWindow.xaml`

### Zakładka 1: Forward Trace (od hodowcy)
```
┌──────────────────────────────────────────────────────────┐
│ HODOWCA → KLIENCI (forward trace)                        │
├──────────────────────────────────────────────────────────┤
│ Hodowca: [Kowalski ▼]  Okres: [01.05 - 23.05] [Szukaj]  │
├──────────────────────────────────────────────────────────┤
│ Wyniki: Kowalski → 12 partii → 487 palet wyrobu →       │
│         34 klientów                                       │
├──────────────────────────────────────────────────────────┤
│ ▼ Partia #1247 (12.05.2026, 8500 szt, 18 ton)           │
│    ▼ Paleta surowa P1=4892                              │
│       → Paleta wyrobu PIO-2026-05-12-007 (filet, 12kg)  │
│         → Sprzedaż 13.05 Auchan Warszawa (DOK#34521)    │
│       → Paleta wyrobu PIO-2026-05-12-008 (filet, 15kg)  │
│         → Sprzedaż 13.05 Lidl Łódź (DOK#34522)          │
│    ▼ Paleta surowa P1=4893                              │
│       → ... itd                                          │
└──────────────────────────────────────────────────────────┘

[📥 Eksport CSV]  [📄 Raport PDF]  [🚨 Inicjuj Recall]
```

### Zakładka 2: Reverse Trace (od produktu klienta)
```
┌──────────────────────────────────────────────────────────┐
│ KLIENT → ŹRÓDŁO (reverse trace)                          │
├──────────────────────────────────────────────────────────┤
│ Numer lot: [PIO-2026-05-12-007                ] [Szukaj] │
├──────────────────────────────────────────────────────────┤
│ ⚙ Paleta wyrobu PIO-2026-05-12-007                       │
│   Towar: Filet z piersi 1kg                              │
│   Data prod.: 12.05.2026                                 │
│   Linia: Krojenie 2                                      │
│   Operator: Jan Nowak                                    │
│                                                          │
│ 📦 SKŁAD PALETY (z 3 palet surowych):                   │
│   • Paleta surowa P1=4892 → Partia #1247                │
│     → Hodowca: KOWALSKI (farma F-12)                     │
│     → Ubity: 12.05.2026 04:30                            │
│     → Udział w palecie wyrobu: 4.5 kg                    │
│                                                          │
│   • Paleta surowa P1=4895 → Partia #1247                │
│     → Hodowca: KOWALSKI (farma F-12)                     │
│     → Udział: 5.2 kg                                     │
│                                                          │
│   • Paleta surowa P1=4901 → Partia #1248                │
│     → Hodowca: NOWAK (farma F-23)                        │
│     → Udział: 3.3 kg                                     │
│                                                          │
│ 🚛 WYSŁANO DO:                                          │
│   13.05.2026 06:30 — Auchan Warszawa                    │
│   Dokument: WZ#34521                                     │
│   Kierowca: M.Wiśniewski (Volvo PSZ 12345)              │
│   Trip GPS: Webfleet Trip#9847                          │
│   Temp transport: 2.1-3.4°C (norma <4) ✓                │
│   Dostawa: 13.05.2026 09:15 (potwierdzona)              │
└──────────────────────────────────────────────────────────┘

[📄 Raport PDF]  [🔍 Zobacz mapę GPS]  [📞 Kontakt klient]
```

### Zakładka 3: Recall management
```
┌──────────────────────────────────────────────────────────┐
│ 🚨 RECALL MANAGEMENT                                     │
├──────────────────────────────────────────────────────────┤
│ [+ NOWY RECALL]                                          │
├──────────────────────────────────────────────────────────┤
│ AKTYWNE RECALLE (1):                                     │
│                                                          │
│ REC-2026-001 — INIT 14.08 09:00                         │
│ Powód: Pozytywny test Salmonella partia #1247            │
│ Zakres: Partia 1247 (cała)                              │
│ Status: W_REALIZACJI                                     │
│ Klienci: 8 powiadomionych                                │
│ Palety: 24 wycofane / 47 objętych                        │
│ Waga: 432 kg z 924 kg                                    │
│ Wartość: 5 184 zł / 11 088 zł                            │
│                                                          │
│ [Szczegóły]  [Eskalacja RASFF]  [Zamknij]               │
├──────────────────────────────────────────────────────────┤
│ HISTORIA (kliknij dla szczegółów):                       │
│ REC-2025-007 ZAMKNIETY 12.11.2025 (alergeny)             │
│ REC-2025-006 ZAMKNIETY 03.10.2025 (jakość)               │
└──────────────────────────────────────────────────────────┘
```

---

## SCENARIUSZE WYKORZYSTANIA

### Scenariusz 1: Klient zgłasza problem
1. Klient (osoba prywatna lub QC sklepu) skanuje QR code z opakowania
2. Strona pokazuje: "Jeśli masz reklamację, podaj numer lot: PIO-2026-05-12-007"
3. Klient: "Mam zatrucie, jadłem filet z tego opakowania"
4. QC otwiera ZPSP → Traceability → Reverse → wpisuje lot
5. Widzi: hodowca Kowalski (głównie) + Nowak (małżonek), data, temp transport
6. QC sprawdza inne palety z tych partii — czy podobne zgłoszenia
7. Jeśli inni klienci też zgłaszają → **INICJUJ RECALL**

### Scenariusz 2: Recall (krytyczne!)
1. QC: klik [INICJUJ RECALL]
2. Wybiera zakres: "PARTIA 1247"
3. System pokazuje: 47 palet objętych, 8 klientów, 924 kg, 11k PLN
4. QC potwierdza → klik [WYŚLIJ RECALL]
5. System AUTO:
   - Zmienia status 47 palet na WYCOFANO
   - Wysyła SMS do 8 klientów (z RecallNumber + instrukcją)
   - Wysyła email z formal recall notice
   - Tworzy log audytowy
   - Generuje raport dla Sanepidu (PDF)
6. QC monitoruje: kto potwierdził, ile wróciło
7. Po zwrocie wszystkiego: klik [ZAMKNIJ RECALL]

### Scenariusz 3: Audyt BRC v9
- Auditor: "Pokaż mi traceability dla partii z 12.05"
- QC: otwiera ZPSP → wybiera partię → klik [Raport PDF Forward Trace]
- 30 sekund: PDF z pełną drogą produktu
- Auditor: zachwycony

---

## WORKFLOW DLA OPERATORÓW

### Wytwarzanie palety wyrobu (krojenie)
1. Operator skanuje wszystkie palety surowe które trafiają na stół krojenia
2. System tworzy `PaletaWyrob` z auto-generated `LotNumber`
3. Po zakończeniu paczki operator:
   - Waży paletę
   - Drukuje etykietę z lot number + QR
   - Skan etykiety → potwierdzenie w systemie
4. Auto-zapis do `PaletaWyrobSklad` (kg z każdej palety surowej)

### Sprzedaż (wydanie WZ)
1. Magazynier ładuje paletę na ciężarówkę
2. Skanuje QR palety + dokument WZ
3. System dodaje wpis do `DokumentPaletWydania`

### Dostawa (GPS+temp)
1. Kierowca startuje trasę → Webfleet GPS
2. Tracker temperatury loguje co 5 min
3. Po dostawie kierowca robi zdjęcie podpisu odbiorcy
4. Upload do `DostawaKlientGPS.PotwierdzeniePath`

---

## CHALLENGE: paleta surowa → wyrobu (mieszanie)

### Problem
Na stole krojenia trafiają tuszki z **wielu palet surowych** (operator nie patrzy "z której"). Po krojeniu kawałki **mieszają się** w pojemniku. Końcowa paleta wyrobu zawiera kawałki z 3-5 różnych palet surowych.

### Rozwiązanie 1: Manual (najtaniej)
- Operator co 30-60 minut zmienia paletę wyrobu
- Pamięta które palety surowe były w tym czasie
- Wpisuje w aplikacji "ostatnie 30 min: palety P1=4892, 4893, 4901"
- Niezbyt dokładne, ale **wystarczające do recall** (mała granularność = lekko większy zakres, ale OK)

### Rozwiązanie 2: Half-auto (średnio)
- Skanowanie palet surowych przy wejściu na stół
- System wie co weszło w jakim czasie
- Algorytm: paleta wyrobu zamknięta o 14:30 zawiera surowe ostatnie 45 min × waga
- Średnio dokładne

### Rozwiązanie 3: Full-auto (drogie)
- RFID każda paleta surowa
- Skaner przy każdym stanowisku
- Automatyczne wykrycie skomponowania
- Drogie (RFID reader ~3000 zł/szt), ale 100% dokładne

**Rekomendacja**: zacznij od Rozwiązania 1, ewoluuj.

---

## CZAS IMPLEMENTACJI

| Etap | Czas | Koszt |
|---|---|---|
| Tabele bazy (5 nowych) | 8h | — |
| Serwis TraceabilityService (forward + reverse + recall) | 40h | — |
| UI 3 zakładki | 32h | — |
| Generator etykiet (QR + lot) | 16h | — |
| Hardware drukarki + skanery | — | 3000-7000 zł |
| Integracja z Webfleet (GPS + temp) | 16h | — |
| Workflow operatorów + szkolenie | 24h + 16h szkolenia | — |
| QR landing page (publiczna) | 8h | hosting <100 zł/mies |
| Recall management (powiadomienia SMS/email) | 16h | SMS API ~500/rok |
| Raporty PDF (forward, reverse, recall, sanepid) | 16h | QuestPDF $99/rok |
| Pilot 1 miesiąc | 40h | — |
| **RAZEM** | **~3-4 mies wdrożenia** | **~5-10 tys zł** |

---

## RYZYKA

⚠️ **Operatorzy zapominają skanować** → wprowadź obowiązek + KPI per operator
⚠️ **Drukarki padają w trakcie produkcji** → zapasowa drukarka
⚠️ **Granularność traceability** — im dokładniej, tym drożej i trudniej. Wybierz właściwy poziom
⚠️ **Klient nie patrzy na QR** — to OK, **dla Was traceability ważne**, nie dla klienta
⚠️ **Recall to stres** — przygotuj **playbook** + szkolenie raz na pół roku

---

## DODATKOWE KORZYŚCI

1. **Marketing**: QR code jako USP transparentności
2. **Lojalność klientów premium**: Lidl/Tesco audytują dostawców, traceability = +punkty
3. **Eksport**: niektóre kraje wymagają (USA FSIS, niektóre Azja)
4. **Insurance**: niższe składki za "good traceability practice"
5. **Cross-sell w ZPSP**: integracja z istniejącym `Reklamacje` → szybsza obsługa
