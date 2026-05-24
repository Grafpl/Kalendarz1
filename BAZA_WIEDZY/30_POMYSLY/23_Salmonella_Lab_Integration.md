# 23. ⭐ Salmonella/Campylobacter Lab Integration — PEŁNY PORADNIK

## Co to jest
**Automatyczna integracja** z laboratoriami mikrobiologicznymi (MIK Lab, J.S. Hamilton, SGS Polska) żeby wyniki badań:
- Trafiały **automatycznie** do bazy ZPSP
- Były **powiązane z partią** (nie tylko jako papier)
- Generowały **alerty** przy pozytywach
- Tworzyły **statystyki per hodowca** / per miesiąc
- Pasowały do **traceability** (#22) jako element raportu RECALL

## Wartość biznesowa

### Compliance
- **EU Regulation 2073/2005** wymaga regularnych badań Salmonella/Campylobacter
- **BRC v9 sek. 5.6** — dokumentacja badań mikrobiologicznych
- Brak organizacji = niezgodności na audytach

### Operacyjna
- **Pozytyw wykryty w 24h** zamiast 5 dni (papier ginie, ktoś zapomina) = szybsza interwencja
- **Trend per hodowca** — wykrywasz że Kowalski ma 5× więcej pozytywów = systemowy problem
- **Korelacja z incydentami CCP** (#19) — czy pozytywy korelują z naruszeniami temperatury

### Finansowa
- Jeden recall z pozytywu = **2-5M PLN strat** uniknięte (jeśli wcześniej wykryjesz)
- Negocjacje cen pasz / dezynfektantów na podstawie danych
- **~300-500k PLN/rok** różnica między "reagujemy" a "zapobiegamy"

---

## OBOWIĄZKI PRAWNE (z czego musisz raportować)

### Typy próbek
1. **Carcass rinse (płukanie tuszki)** — Salmonella po chłodzeniu, częstotliwość wg ryzyka
2. **Neck skin (skóra szyi)** — Campylobacter, próbka 25g
3. **Cecum (jelito ślepe)** — Salmonella przy bawełniu
4. **Stół roboczy / sprzęt** — swab, każdy CCP
5. **Woda** — okresowo (raz/tydzień)
6. **Ściółka / kurniki hodowców** — w razie podejrzeń

### Częstotliwość minimalna (EU 2073/2005)
- 50 tuszek/tydzień testowanych na Salmonella
- Limit: max 7 pozytywów na 50 próbek (rolling 10 weeks)
- Powyżej → korekta + raport do organów

### Limity
- **Salmonella**: zero tolerance (każdy pozytyw = problem)
- **Campylobacter**: <1000 CFU/g (norma EU), <100 CFU/g (norma niemiecka)
- **E. coli**: <100 CFU/g

---

## ARCHITEKTURA

### Jak wyniki trafiają z labu do Was

#### Sposób 1: PDF email (typowy dziś)
```
[Lab] → email PDF "Wyniki badania nr 12345" → [QC otrzymuje]
       → QC drukuje
       → QC wpisuje ręcznie do Excela / na ścianę
       → GUBI SIĘ
```

#### Sposób 2: API laboratorium (ideał, mało labów to ma)
```
[Lab] → REST API → automatyczne pobranie do ZPSP
```

#### Sposób 3: PDF parser (realne rozwiązanie) ⭐ ZALECANY
```
[Lab] → email PDF → [ZPSP email watcher] 
                  → [PDF parser z AI / OCR]
                  → [DB: LabResults]
                  → [Powiadomienia]
```

---

## DATABASE SCHEMA

```sql
-- LibraNet
CREATE TABLE Laboratorium (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Nazwa NVARCHAR(200) NOT NULL,
    Kod NVARCHAR(20) NOT NULL UNIQUE,  -- 'MIK_LAB', 'JSHAMILTON', 'SGS'
    EmailNadawcy NVARCHAR(200) NULL,  -- z którego maila przychodzą wyniki
    ApiEndpoint NVARCHAR(500) NULL,  -- jeśli oferują API
    OsobaKontaktowa NVARCHAR(200) NULL,
    Telefon NVARCHAR(50) NULL,
    EmailKontaktowy NVARCHAR(200) NULL,
    AdresAdres NVARCHAR(500) NULL,
    KontaktKierownik NVARCHAR(200) NULL,
    Aktywne BIT NOT NULL DEFAULT 1
);

CREATE TABLE LabZleceniaBadan (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    NumerZlecenia NVARCHAR(50) NOT NULL UNIQUE,  -- 'PIO-LAB-2026-001'
    LaboratoriumId INT NOT NULL FOREIGN KEY REFERENCES Laboratorium(Id),
    DataPobraniaProbki DATETIME NOT NULL,
    DataWyslaniaProbki DATETIME NULL,
    DataOtrzymaniaWynikow DATETIME NULL,
    
    TypProbki NVARCHAR(50) NOT NULL,  -- 'CARCASS_RINSE', 'NECK_SKIN', 'CECUM', 'SWAB_STOL', 'WODA'
    PobranePrzez NVARCHAR(100) NULL,
    PunktPobrania NVARCHAR(200) NULL,  -- gdzie w fabryce
    
    PartiaId INT NULL FOREIGN KEY REFERENCES listapartii(LP),
    HodowcaId INT NULL,
    
    BadaneParametry NVARCHAR(MAX) NULL,  -- JSON ['SALMONELLA', 'CAMPYLOBACTER', 'ECOLI']
    Status NVARCHAR(20) NOT NULL DEFAULT 'POBRANE',
    -- POBRANE, WYSLANE, W_TRAKCIE, OTRZYMANE_OK, OTRZYMANE_POZYTYW, ZAMKNIETE
    
    KosztBadaniaPLN DECIMAL(10,2) NULL,
    Notatki NVARCHAR(1000) NULL
);
CREATE INDEX IX_LabZlec_Partia ON LabZleceniaBadan(PartiaId);
CREATE INDEX IX_LabZlec_DataPobr ON LabZleceniaBadan(DataPobraniaProbki);
CREATE INDEX IX_LabZlec_Status ON LabZleceniaBadan(Status);

CREATE TABLE LabWyniki (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    ZlecenieId BIGINT NOT NULL FOREIGN KEY REFERENCES LabZleceniaBadan(Id),
    Parametr NVARCHAR(50) NOT NULL,  -- 'SALMONELLA', 'CAMPYLOBACTER'
    WynikSurowy NVARCHAR(200) NOT NULL,  -- 'NIEWYKRYTA', '<10 CFU/g', '1.2x10^3 CFU/g'
    WynikCFU DECIMAL(20,4) NULL,  -- numeryczne CFU/g (jeśli wykryta)
    Jednostka NVARCHAR(20) NULL,
    WynikInterpretacja NVARCHAR(20) NOT NULL,
    -- POZYTYWNY, NEGATYWNY, POWYZEJ_LIMITU, W_NORMIE
    MetodaAnalizy NVARCHAR(100) NULL,  -- 'ISO 6579-1:2017'
    DataAnalizy DATETIME NULL,
    PodpisalAnalityk NVARCHAR(200) NULL,
    ScieżkaCertyfikatPDF NVARCHAR(500) NULL
);

CREATE TABLE LabAlerts (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    WynikId BIGINT NOT NULL FOREIGN KEY REFERENCES LabWyniki(Id),
    AlertDateTime DATETIME NOT NULL,
    AlertPriorytet NVARCHAR(20) NOT NULL,  -- KRYTYCZNY, WYSOKI, SREDNI
    PowiadomieniWyslano NVARCHAR(MAX) NULL,  -- JSON list of recipients
    Status NVARCHAR(20) NOT NULL DEFAULT 'OTWARTY',
    AkcjaPodjeta NVARCHAR(MAX) NULL,
    AkcjaPrzez NVARCHAR(100) NULL,
    AkcjaDateTime DATETIME NULL
);
```

---

## EMAIL WATCHER + PDF PARSER

### Architektura
```
[Skrzynka pocztowa qc@piorkowscy.pl]
            │
            │ IMAP poll co 5 min
            ▼
[ZPSP EmailWatcher Service]
            │
            │ Filtr: from in (lab emails)
            ▼
[Pobranie PDF jako załącznik]
            │
            ▼
[PDF Parser]
   │ Opcja A: Regex (jeśli stały format)
   │ Opcja B: pdftotext + parsing
   │ Opcja C: Claude AI Vision (najelastyczniej) ⭐
            ▼
[Zapis do LabZleceniaBadan + LabWyniki]
            │
            ▼
[Detekcja pozytywów]
            │
            ▼
[Powiadomienia (SMS, email, push)]
```

### Implementacja: EmailWatcher

**Pakiet NuGet**: `MailKit` (free)

```csharp
using MailKit.Net.Imap;
using MailKit.Search;

public class LabEmailWatcherService : BackgroundService
{
    private readonly string _imapHost = "imap.gmail.com";
    private readonly int _imapPort = 993;
    private readonly string _email = "qc@piorkowscy.pl";
    private readonly string _password;  // z secrets

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await ProcessNewEmailsAsync();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Lab email watcher failure");
            }
            await Task.Delay(TimeSpan.FromMinutes(5), ct);
        }
    }

    private async Task ProcessNewEmailsAsync()
    {
        using var client = new ImapClient();
        await client.ConnectAsync(_imapHost, _imapPort, true);
        await client.AuthenticateAsync(_email, _password);
        
        var inbox = client.Inbox;
        await inbox.OpenAsync(MailKit.FolderAccess.ReadWrite);
        
        var labEmails = new[] { "wyniki@miklab.pl", "info@jshamilton.pl", "report@sgs.com" };
        var query = SearchQuery.NotSeen
            .And(SearchQuery.OrAll(labEmails.Select(e => SearchQuery.FromContains(e))));
        var uids = await inbox.SearchAsync(query);
        
        foreach (var uid in uids)
        {
            var message = await inbox.GetMessageAsync(uid);
            await ProcessMessage(message);
            await inbox.AddFlagsAsync(uid, MessageFlags.Seen, true);
        }
        
        await client.DisconnectAsync(true);
    }

    private async Task ProcessMessage(MimeKit.MimeMessage message)
    {
        var lab = DetectLab(message.From.Mailboxes.First().Address);
        
        foreach (var att in message.Attachments.OfType<MimeKit.MimePart>())
        {
            if (att.ContentType.IsMimeType("application", "pdf"))
            {
                var pdfPath = await SaveAttachment(att, lab);
                var results = await ParseLabPdf(pdfPath, lab);
                await SaveResults(results);
                await CheckPositivesAndAlert(results);
            }
        }
    }
}
```

### PDF Parser z Claude AI (Opcja C — najpolecam)

```csharp
public class ClaudeLabPdfParser
{
    private readonly AnthropicClient _claude;

    public async Task<LabResultParsed> ParsePdfAsync(string pdfPath, string labName)
    {
        // 1. Convert PDF to images (pdftoppm lub PdfPig)
        var images = await ConvertPdfToImagesAsync(pdfPath);
        
        // 2. System prompt
        var systemPrompt = @"Jesteś ekspertem od analizy raportów laboratoryjnych z mikrobiologii żywności.
            Otrzymasz zdjęcie/skan raportu z laboratorium. 
            Wyciągnij dane do JSON w formacie:
            {
              ""numer_zlecenia"": ""..."",
              ""data_pobrania"": ""YYYY-MM-DD"",
              ""data_analizy"": ""YYYY-MM-DD"",
              ""typ_probki"": ""CARCASS_RINSE/NECK_SKIN/CECUM/SWAB_STOL/WODA"",
              ""parametry"": [
                {
                  ""nazwa"": ""SALMONELLA/CAMPYLOBACTER/ECOLI"",
                  ""metoda"": ""ISO ..."",
                  ""wynik_surowy"": ""tekst z raportu"",
                  ""wynik_cfu"": liczba_lub_null,
                  ""jednostka"": ""CFU/g lub null"",
                  ""interpretacja"": ""POZYTYWNY/NEGATYWNY/POWYZEJ_LIMITU/W_NORMIE""
                }
              ],
              ""partia_id"": opcjonalnie_jesli_widoczna,
              ""hodowca"": opcjonalnie_jesli_widoczny,
              ""podpis_analityk"": ""..."",
              ""uwagi_dodatkowe"": ""...""
            }";

        // 3. Send to Claude (każda strona oddzielnie lub razem)
        var imagesContent = new List<ContentBase>();
        foreach (var img in images)
            imagesContent.Add(new ImageContent { /* ... */ });
        imagesContent.Add(new TextContent { Text = "Wyciągnij dane z tego raportu lab." });

        var request = new MessageParameters
        {
            Model = "claude-haiku-4-5-20251001",  // Haiku wystarczy
            MaxTokens = 3000,
            System = new[] { new SystemMessage { Type = "text", Text = systemPrompt } },
            Messages = new List<Message>
            {
                new Message { Role = RoleType.User, Content = imagesContent }
            }
        };

        var response = await _claude.Messages.GetClaudeMessageAsync(request);
        var json = response.Content[0].Text;
        
        return JsonSerializer.Deserialize<LabResultParsed>(json)!;
    }
}
```

### Koszt parsing
- Haiku 4.5: $0.001/PDF (1-2 strony)
- 200 PDFów/rok = **$0.20/rok ≈ 1 zł**

---

## DETEKCJA POZYTYWÓW + ALERTY

```csharp
public class LabAlertService
{
    public async Task CheckPositivesAndAlertAsync(List<LabWyniki> wyniki)
    {
        foreach (var w in wyniki)
        {
            if (w.WynikInterpretacja == "POZYTYWNY" || w.WynikInterpretacja == "POWYZEJ_LIMITU")
            {
                var alert = new LabAlert
                {
                    WynikId = w.Id,
                    AlertDateTime = DateTime.Now,
                    AlertPriorytet = w.Parametr == "SALMONELLA" ? "KRYTYCZNY" : "WYSOKI"
                };

                // Wyślij SMS do QM + dyrektora
                var smsRecipients = new[] 
                { 
                    "+48xxx" /*QM*/, 
                    "+48xxx" /*dyrektor*/ 
                };
                
                var smsText = $"⚠ {w.Parametr} POZYTYWNY w probce {w.ZlecenieId.NumerZlecenia}. " +
                              $"Partia: {w.ZlecenieId.PartiaId}. " +
                              $"Działaj wg playbook'a.";
                
                foreach (var phone in smsRecipients)
                    await _smsApi.SendAsync(phone, smsText);

                // Email z PDF raportu
                await _emailService.SendAsync(new EmailRequest
                {
                    To = "qa@piorkowscy.pl,dyrekcja@piorkowscy.pl",
                    Subject = $"🚨 POZYTYW {w.Parametr} - {w.ZlecenieId.NumerZlecenia}",
                    Body = $"Pozytywny wynik badania mikrobiologicznego. Sprawdź ZPSP.",
                    Attachments = new[] { w.ZlecenieId.PdfPath }
                });

                // Push w aplikacji
                await _pushService.BroadcastAsync(new PushNotification
                {
                    Title = "Pozytyw mikrobiologiczny!",
                    Body = $"{w.Parametr} w partii #{w.ZlecenieId.PartiaId}",
                    Priority = "HIGH",
                    OpenUrl = $"/lab/wynik/{w.Id}"
                });

                // Auto-flag partia w listapartii
                await UpdatePartiaStatusAsync(w.ZlecenieId.PartiaId.Value, "RYZYKO_MIKRO");

                alert.Status = "OTWARTY";
                await SaveAlertAsync(alert);
            }
        }
    }
}
```

---

## DASHBOARD MIKROBIOLOGICZNY

**Plik**: `Lab/LabDashboardWindow.xaml`

```
┌──────────────────────────────────────────────────────────┐
│ 🦠 BADANIA MIKROBIOLOGICZNE — DASHBOARD                  │
├──────────────────────────────────────────────────────────┤
│                                                          │
│  📊 OSTATNIE 30 DNI                                      │
│                                                          │
│  Badań:        47   Negatywnych: 43 (91%)               │
│  Pozytywów:    3    (Salmonella: 1, Campy: 2)           │
│  W toku:       4                                         │
│                                                          │
│  🚨 AKTYWNE POZYTYWY (1):                                │
│  • PIO-LAB-2026-038 — SALMONELLA — partia 1247          │
│    Hodowca: Kowalski | Działanie: RECALL inicjowany     │
│                                                          │
│  📈 TREND POZYTYWÓW (12 mies):                          │
│  Sty ▌      Lut ▌▌     Mar ▌      Kwi ▌▌    Maj ▌▌▌    │
│  Cze ▌      Lip        Sie ▌      Wrz ▌▌    Paź ▌      │
│  Lis ▌▌     Gru ▌▌▌                                     │
│                                                          │
│  🏆 RANKING HODOWCÓW (% pozytywów, 12 mies):            │
│  1. Kowalski:    8.2%  (4/49 badań)  ⚠ HIGH             │
│  2. Wiśniewski:  3.1%  (1/32)                           │
│  3. Nowak:       2.4%  (1/41)                           │
│  ...                                                    │
│                                                          │
│  📅 NADCHODZĄCE BADANIA:                                │
│  Jutro: 3 zaplanowane (carcass rinse)                  │
│  Pt:    1 (woda)                                       │
│                                                          │
└──────────────────────────────────────────────────────────┘

[+ Nowe zlecenie]  [📊 Statystyki]  [⚙ Konfiguracja]
```

---

## INTEGRACJA Z #19 (CCP) i #22 (Traceability)

### Korelacja z incydentami CCP
Gdy pozytyw mikro → automatyczne sprawdzenie:
- Czy w dniach przed pobraniem próbki były incydenty CCP?
- Czy chłodnia była stabilna?
- Czy temperatura transportu była OK?

```sql
SELECT 
    lab.NumerZlecenia,
    lab.DataPobraniaProbki,
    lab.WynikInterpretacja,
    ccp.PunktId,
    ccp.StartDateTime AS CCP_Start,
    ccp.WartoscMax,
    ccp.LimitGorny
FROM LabZleceniaBadan lab
JOIN LabWyniki lw ON lw.ZlecenieId = lab.Id AND lw.WynikInterpretacja = 'POZYTYWNY'
LEFT JOIN CCP_Incydent ccp ON ccp.StartDateTime BETWEEN 
    DATEADD(DAY, -7, lab.DataPobraniaProbki) AND lab.DataPobraniaProbki
WHERE lab.DataPobraniaProbki >= DATEADD(MONTH, -3, GETDATE())
ORDER BY lab.DataPobraniaProbki DESC;
```

Wynik: czy pozytywy korelują z incydentami → systemowy problem czy losowy.

### Workflow Recall z labu
Pozytyw Salmonella w partii 1247:
1. Auto-alert (powyżej)
2. QM otwiera ZPSP → Traceability → Recall
3. Pre-wypełnione dane: partia, hodowca, lab number
4. Klik [Inicjuj Recall]
5. **Pełna integracja**, nie trzeba nic wpisywać dwa razy

---

## RAPORTY DLA SANEPID / ORGANÓW

### Raport miesięczny (auto-generated)
```
RAPORT MIKROBIOLOGICZNY — MAJ 2026
PIÓRKOWSCY Sp. z o.o.

1. PODSUMOWANIE
   Badań ogółem:        47
   Salmonella:          22 (1 pozytyw, 21 negatywnych)
   Campylobacter:       18 (2 powyzej limitu, 16 w normie)
   Inne:                7
   
2. POZYTYWY
   - 12.05 partia 1247 SALMONELLA carcass rinse
     Korekta: RECALL REC-2026-001 zamkniety 14.05
   - ...
   
3. TREND
   [wykres pozytywów rok do roku]
   
4. ZGODNOŚĆ Z EU 2073/2005
   ✓ Częstotliwość: 50 prob/tydzień (norma)
   ✓ Rolling 10 tyg: 4 pozytywy / 500 prob = 0.8% (norma <14%)
   ✓ Wszystkie pozytywy z odpowiednią korektą
   
5. ZAŁĄCZNIKI
   - Certyfikaty wszystkich badań
   - Logi CCP w dniach pozytywów
   - Raporty recall

Podpis QM: __________________
Data: 31.05.2026
```

---

## WORKFLOW QC

### Tygodniowy
- **Pn**: planowanie 50 prób tygodnia
- **Każdy dzień**: pobranie 10 prób (różne punkty)
- **Pn-Pt**: dostawa do labu
- **Cz-Sob**: otrzymywanie wyników (auto-import)
- **Pt 14:00**: sprawdzenie raportów tygodnia
- **Nie**: zamknięcie miesiąca jeśli ostatni tydz mies

### Przy pozytywie
1. Auto-alert (SMS+email+push)
2. QM w 30 min potwierdza pozytyw
3. QM uruchamia playbook:
   - Wstrzymanie wysyłki dotkniętej partii
   - Próbka kontrolna (re-test)
   - Audyt hodowcy
   - Komunikacja klientów (jeśli wysłano)
   - RECALL (jeśli krytyczne)
4. Dokumentacja działań w `LabAlerts.AkcjaPodjeta`
5. Zamknięcie alertu po wszystkim

---

## CZAS IMPLEMENTACJI

| Etap | Czas | Koszt |
|---|---|---|
| Tabele bazy (4 nowe) | 6h | — |
| Konto Gmail/IMAP dla qc@ | 1h | 50 zł/mies workspace |
| EmailWatcher service | 16h | — |
| ClaudePdfParser + prompt tuning | 12h | $5 testów |
| LabAlertService (SMS+email+push) | 12h | SMS API ~500/rok |
| Dashboard UI | 24h | — |
| Raport sanepid generator | 12h | — |
| Integracja z #19 CCP korelacja | 8h | — |
| Integracja z #22 Recall | 8h | — |
| Pilot 1 miesiąc | 40h | — |
| **RAZEM** | **~100h** | **~1000 zł/rok recurring** |

---

## RYZYKA

⚠️ **Format PDF różni się między labami** — Claude AI radzi sobie elastycznie, ale testuj per lab
⚠️ **PDF email może mieć inną nazwę załącznika** — parsuj wszystkie PDF, nie polegaj na nazwie
⚠️ **Lab może wysyłać OCR-able vs scanned** — Claude radzi z oboma
⚠️ **Pozytyw to stres** — playbook MUSI być jasny, szkol QM kwartalnie
⚠️ **Email watcher padnie** → monitor (np. healthcheck endpoint co 1h)
