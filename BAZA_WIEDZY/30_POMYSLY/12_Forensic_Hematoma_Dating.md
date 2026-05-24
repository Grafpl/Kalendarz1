# 12. ⭐ Forensic Haematoma Dating — PEŁNY PORADNIK

## Idea w 1 zdaniu
**Zdjęcie tuszki z odrzutu → Claude VLM analizuje kolor każdej hematomy → przypisuje wiek hematomy (2 min ÷ 72h) → przypisuje winnego (proces uboju / łapanie / transport / hodowca).**

To **przewaga konkurencyjna której nikt w branży nie ma**. Marel, CSB, Baader — nikt nie ma "AI forensic dla mięsa".

---

## BAZA NAUKOWA (z PDFa Broiler Meat Signals str. 122-125)

### Tabela: Kolor hematomy → wiek

| Kolor hematomy | Wiek | Co to znaczy |
|---|---|---|
| **Czerwony** (jasny, niezmieniony) | ≤ 2 minuty | Świeża rana — **proces uboju** (linia, oparzenie wodą, skubarka, pakowanie) |
| **Ciemnoczerwony / fioletowy** | ~12 godzin | Łapanie + transport — przed ubojem 6-12h |
| **Jasnozielony / fioletowy** | ~36 godzin | Wcześniej w transporcie lub na rampie poprzedniego dnia |
| **Żółto-zielony / pomarańczowy** | ~48 godzin | Hodowca — siniaki sprzed 2 dni (np. ostatnie godziny w kurniku) |
| **Pomarańczowo-żółty** | ≥ 72 godziny | **Hodowca** — siniaki sprzed 3+ dni (długoterminowy problem na farmie) |

### Mechanika biologiczna
Kolory pochodzą z **rozpadu hemoglobiny**:
- Hb (czerwona) → met-Hb (ciemnoczerwona, ~2-6h) → biliwerdyna (zielona, ~24-48h) → bilirubina (żółta, ~48-72h)
- Tempo rozpadu zależy od temperatury (chłodzenie tuszki = zwolnienie) i pH
- W kurczaku po uboju proces **zatrzymuje się przy chłodzeniu**, więc kolor "zamraża" wiek hematomy do momentu schłodzenia

### Lokalizacja → przyczyna
PDF mówi też **gdzie** hematoma najprawdopodobniej powstała:
- Hematoma w piersi → rough loading do crates
- Hematoma w udzie → łapanie za nogi
- Hematoma w podudziu / hock joint → osteochondroza (HODOWCA) lub szackle (linia)
- Hematoma w skrzydle → łapanie + szackling
- "Petechial bleedings" (punktowe, drobne) → za silny prąd przy stunning

---

## WARTOŚĆ BIZNESOWA — szczegółowo

### Przed wdrożeniem
- Reklamacja od klienta: "30 sztuk fileta z siniakami, -8% ceny całej partii"
- Hodowca mówi: "to wasze linia!"
- Wy mówicie: "to wasze łapacze!"
- Brak dowodów = stosunki się psują, ktoś bierze stratę "na zgrywę"

### Po wdrożeniu
- Robicie 30 zdjęć siniakowych tuszek
- AI klasyfikuje:
  - 12 sztuk: czerwone (≤2h) → **WASZA WINA** (proces uboju)
  - 15 sztuk: fioletowe (12h) → **ŁAPACZE** (firma transportowa)
  - 3 sztuki: żółto-zielone (72h) → **HODOWCA**
- **Konkretny raport** z procentowym podziałem winy
- Negocjacje: każdy płaci za swoje

### Liczbowa wartość
- Bez systemu: 100% reklamacji bierzecie na siebie → **~300-500k PLN/rok**
- Z systemem: 100% reklamacji rozliczone proporcjonalnie → **150-300k PLN/rok zaoszczędzone**
- + **utrzymanie relacji** z hodowcami (najcenniejsze, trudne do wycenia)
- + marketing: "Mamy AI forensic" → premium clients (Lidl, Tesco, eksport)

### Wartość USP w przemyśle
- Pierwszy w Polsce = artykuły branżowe, konferencje, prelegent
- Możliwość **sprzedaży modułu** innym ubojniom (mały SaaS na boku!)

---

## ARCHITEKTURA TECHNICZNA

### Komponenty
```
[Tablet operatora / smartfon QC]
        │
        │ 1. Robi zdjęcie tuszki klasy B
        ▼
[Aplikacja Blazor + ZPSP]
        │
        │ 2. Upload PNG → /api/hematoma-analyze
        ▼
[Claude API (Sonnet 4.6 lub Haiku 4.5)]
        │
        │ 3. JSON response: [{location, color, age_h, confidence, cause}]
        ▼
[DB: HematomaAnalysis]
        │
        │ 4. Zapis + powiązanie z PartiaId
        ▼
[Raporty]:
   - Per reklamacja (PDF z foto + analiza)
   - Per hodowca (miesięczny)
   - Per zmiana (czy "świeże" wzrosły = problem na linii)
```

### Co Claude widzi
- Zdjęcie tuszki w wysokiej rozdzielczości (min 1920×1080)
- Wszystkie widoczne hematomy
- Lokalizacja anatomiczna każdej (pierś, udo, skrzydło, podudzie)
- Kolor każdej (precyzyjny RGB + nazwa)
- Estymata wieku każdej (z tolerancją ±25%)
- Sugerowana przyczyna każdej

---

## PROMPT DO CLAUDE'A (najważniejsza część)

### Prompt v1 — wersja produkcyjna

```
Jesteś ekspertem weterynaryjnym specjalizującym się w analizie tuszek drobiowych. 
Otrzymasz zdjęcie tuszki kurczaka brojlera. Twoim zadaniem jest analiza forensyczna 
hematom (siniaków) widocznych na tuszce.

INSTRUKCJE:
1. Zidentyfikuj WSZYSTKIE widoczne hematomy (siniaki, krwiaki, przebarwienia)
2. Dla każdej hematomy określ:
   a. LOKALIZACJĘ ANATOMICZNĄ (jeden z: pierś, lewe_udo, prawe_udo, lewe_podudzie, 
      prawe_podudzie, lewe_skrzydlo, prawe_skrzydlo, szyja, plecy, kuper, inne)
   b. KOLOR DOMINUJĄCY (jeden z: czerwony_jasny, czerwony_ciemny, fioletowy, 
      fiolet_zielony, zielony_jasny, zielono_zolty, zolty_pomaranczowy, pomaranczowo_zolty)
   c. ROZMIAR_CM (oszacowanie średnicy w centymetrach)
   d. SZACUNKOWY_WIEK_GODZIN (na podstawie koloru, zgodnie z tabelą):
      - czerwony_jasny → 0-2h
      - czerwony_ciemny → 2-12h
      - fioletowy → 8-16h
      - fiolet_zielony → 24-36h
      - zielony_jasny → 36-48h
      - zielono_zolty → 48-60h
      - zolty_pomaranczowy → 60-80h
      - pomaranczowo_zolty → 72h+
   e. PEWNOŚĆ_OCENY (low/medium/high)
   f. PRAWDOPODOBNA_PRZYCZYNA (jedna z: 
      PROCES_UBOJU_LINIA, PROCES_UBOJU_SKUBARKA, LAPANIE_TRANSPORT, 
      HODOWCA_FARMA, NIEOKRESLONA)

3. Określ POZIOM JAKOŚCI tuszki ogólnie:
   - KLASA_A (brak istotnych wad)
   - KLASA_B_LEKKA (1-2 małe hematomy, trimmable)
   - KLASA_B_CIEZKA (duże hematomy, znaczna utrata wartości)
   - ODRZUT (>50% utraty wartości)

ZWRÓĆ TYLKO JSON, bez komentarzy:
{
  "klasa_ogolna": "...",
  "liczba_hematom": N,
  "hematomy": [
    {
      "id": 1,
      "lokalizacja": "...",
      "kolor": "...",
      "rozmiar_cm": 5.2,
      "wiek_godzin_min": 8,
      "wiek_godzin_max": 16,
      "pewnosc": "...",
      "przyczyna": "...",
      "uwagi": "krótkie uzasadnienie"
    }
  ],
  "rekomendacja_dzialania": "krótkie",
  "uwagi_dodatkowe": "..."
}
```

### Optymalizacja kosztu
**Modele do wyboru**:
- **Claude Haiku 4.5** — ~$0.001/zdjęcie, dobry do podstawowej klasyfikacji
- **Claude Sonnet 4.6** — ~$0.015/zdjęcie, lepszy dla skomplikowanych przypadków
- **Strategia**: Haiku najpierw (cheap), jeśli `pewnosc == "low"` → re-run na Sonnet

**Prompt caching** (DUŻE oszczędności!):
- System prompt (instrukcja powyżej) cache'owany → płacisz raz na 5 min
- Per zdjęcie tylko input image + krótki prompt
- Oszczędność ~4× kosztu vs bez cache

**Realna kalkulacja**:
- 50 reklamacji/rok × 10 zdjęć każda = 500 zdjęć/rok
- Z cache: 500 × $0.001 (Haiku) = **$0.50/rok = ~2 zł/rok** 😄
- Dodaj 10% przypadków na Sonnet: 50 × $0.015 = $0.75
- **TOTAL: ~3 zł/rok kosztów AI** dla zwrotu 200k PLN

---

## DATABASE SCHEMA

```sql
-- LibraNet
CREATE TABLE HematomaSession (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    SessionDateTime DATETIME NOT NULL,
    PartiaId INT NULL,
    ReklamacjaId INT NULL,  -- jeśli sesja wynika z reklamacji
    KlientId INT NULL,
    OperatorId NVARCHAR(50) NULL,
    LiczbaTuszek INT NOT NULL DEFAULT 1,
    NotatkiOperatora NVARCHAR(1000) NULL,
    StatusSession NVARCHAR(20) DEFAULT 'IN_PROGRESS'  -- IN_PROGRESS, COMPLETED, REVIEWED
);

CREATE TABLE HematomaPhoto (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    SessionId BIGINT NOT NULL FOREIGN KEY REFERENCES HematomaSession(Id),
    PhotoPath NVARCHAR(500) NOT NULL,  -- ścieżka do pliku PNG/JPG
    PhotoDateTime DATETIME NOT NULL,
    TuszkaNumer INT NULL,  -- numer tuszki w sesji
    OpisFoto NVARCHAR(500) NULL,  -- "filet z lewej", "calosc z gory"
    OcenaAI_Status NVARCHAR(20) DEFAULT 'PENDING'  -- PENDING, OK, FAILED
);

CREATE TABLE HematomaAnalysis (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    PhotoId BIGINT NOT NULL FOREIGN KEY REFERENCES HematomaPhoto(Id),
    AnalysisDateTime DATETIME NOT NULL,
    KlasaOgolnaTuszki NVARCHAR(20) NULL,
    LiczbaHematom INT NOT NULL DEFAULT 0,
    AIModel NVARCHAR(50) NULL,  -- 'haiku-4.5', 'sonnet-4.6'
    AIRawResponse NVARCHAR(MAX) NULL,  -- pelen JSON z AI
    KosztAnalizyUSD DECIMAL(10,6) NULL,
    OperatorPotwierdzenie BIT NULL,
    OperatorKorekta NVARCHAR(1000) NULL
);

CREATE TABLE HematomaDetail (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    AnalysisId BIGINT NOT NULL FOREIGN KEY REFERENCES HematomaAnalysis(Id),
    Lokalizacja NVARCHAR(30) NOT NULL,
    Kolor NVARCHAR(30) NOT NULL,
    RozmiarCm DECIMAL(5,2) NULL,
    WiekGodzinMin INT NULL,
    WiekGodzinMax INT NULL,
    Pewnosc NVARCHAR(10) NULL,
    Przyczyna NVARCHAR(50) NULL,
    Uwagi NVARCHAR(500) NULL
);

-- Agregaty per hodowca
CREATE VIEW v_HematomaHodowca AS
SELECT 
    pd.HodowcaId,
    YEAR(hs.SessionDateTime) AS Rok,
    MONTH(hs.SessionDateTime) AS Miesiac,
    COUNT(DISTINCT hs.Id) AS LiczbaSesji,
    COUNT(hd.Id) AS LiczbaHematom,
    SUM(CASE WHEN hd.Przyczyna = 'HODOWCA_FARMA' THEN 1 ELSE 0 END) AS HematomyHodowca,
    SUM(CASE WHEN hd.Przyczyna = 'LAPANIE_TRANSPORT' THEN 1 ELSE 0 END) AS HematomyTransport,
    SUM(CASE WHEN hd.Przyczyna LIKE 'PROCES_UBOJU%' THEN 1 ELSE 0 END) AS HematomyUboj
FROM HematomaSession hs
JOIN HematomaPhoto hp ON hp.SessionId = hs.Id
JOIN HematomaAnalysis ha ON ha.PhotoId = hp.Id
JOIN HematomaDetail hd ON hd.AnalysisId = ha.Id
JOIN listapartii lp ON lp.LP = hs.PartiaId
JOIN PartiaDostawca pd ON pd.Partia = lp.LP
GROUP BY pd.HodowcaId, YEAR(hs.SessionDateTime), MONTH(hs.SessionDateTime);
```

---

## KOD C# — Serwis analizy

**Plik**: `Services/HematomaAnalysisService.cs`

```csharp
using Anthropic.SDK;
using Anthropic.SDK.Messaging;
using System.Text.Json;

namespace Kalendarz1.Services;

public class HematomaAnalysisService
{
    private readonly AnthropicClient _claude;
    private readonly string _systemPrompt;

    public HematomaAnalysisService(string apiKey)
    {
        _claude = new AnthropicClient(apiKey);
        _systemPrompt = File.ReadAllText("Prompts/hematoma_system.txt");
    }

    public async Task<HematomaResult> AnalyzePhotoAsync(string imagePath, bool useSonnet = false)
    {
        var imageBytes = await File.ReadAllBytesAsync(imagePath);
        var base64 = Convert.ToBase64String(imageBytes);
        var mimeType = GetMimeType(imagePath);

        var request = new MessageParameters()
        {
            Model = useSonnet ? "claude-sonnet-4-6" : "claude-haiku-4-5-20251001",
            MaxTokens = 2000,
            Temperature = 0.1m,  // niska dla konsystencji
            System = new[] 
            { 
                new SystemMessage 
                { 
                    Type = "text", 
                    Text = _systemPrompt,
                    CacheControl = new CacheControl { Type = "ephemeral" }  // PROMPT CACHING
                }
            },
            Messages = new List<Message>
            {
                new Message
                {
                    Role = RoleType.User,
                    Content = new List<ContentBase>
                    {
                        new ImageContent
                        {
                            Source = new ImageSource
                            {
                                Type = "base64",
                                MediaType = mimeType,
                                Data = base64
                            }
                        },
                        new TextContent { Text = "Przeanalizuj tę tuszkę." }
                    }
                }
            }
        };

        var response = await _claude.Messages.GetClaudeMessageAsync(request);
        var jsonText = response.Content[0].Text;

        // Parsuj JSON
        var result = JsonSerializer.Deserialize<HematomaResult>(jsonText, new JsonSerializerOptions 
        { 
            PropertyNameCaseInsensitive = true 
        });

        // Koszt
        var inputTokens = response.Usage.InputTokens;
        var outputTokens = response.Usage.OutputTokens;
        var cachedTokens = response.Usage.CacheReadInputTokens ?? 0;
        result.KosztUSD = CalculateCost(useSonnet, inputTokens, outputTokens, cachedTokens);
        result.AIModel = useSonnet ? "sonnet-4.6" : "haiku-4.5";
        result.RawResponse = jsonText;

        return result;
    }

    private decimal CalculateCost(bool sonnet, int input, int output, int cached)
    {
        // Pricing (per 1M tokens):
        // Haiku 4.5: $1 input, $5 output, $0.10 cached
        // Sonnet 4.6: $3 input, $15 output, $0.30 cached
        if (sonnet)
            return (input * 3m + output * 15m + cached * 0.30m) / 1_000_000m;
        return (input * 1m + output * 5m + cached * 0.10m) / 1_000_000m;
    }

    private string GetMimeType(string path) => Path.GetExtension(path).ToLower() switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".webp" => "image/webp",
        ".gif" => "image/gif",
        _ => "image/jpeg"
    };
}

public class HematomaResult
{
    public string KlasaOgolna { get; set; } = "";
    public int LiczbaHematom { get; set; }
    public List<HematomaItem> Hematomy { get; set; } = new();
    public string RekomendacjaDzialania { get; set; } = "";
    public string UwagiDodatkowe { get; set; } = "";
    public decimal KosztUSD { get; set; }
    public string AIModel { get; set; } = "";
    public string RawResponse { get; set; } = "";
}

public class HematomaItem
{
    public int Id { get; set; }
    public string Lokalizacja { get; set; } = "";
    public string Kolor { get; set; } = "";
    public double RozmiarCm { get; set; }
    public int WiekGodzinMin { get; set; }
    public int WiekGodzinMax { get; set; }
    public string Pewnosc { get; set; } = "";
    public string Przyczyna { get; set; } = "";
    public string Uwagi { get; set; } = "";
}
```

### Strategia routingu Haiku/Sonnet
```csharp
public async Task<HematomaResult> AnalyzeWithFallbackAsync(string path)
{
    // Pierwsze podejście: Haiku (tanio)
    var haikuResult = await AnalyzePhotoAsync(path, useSonnet: false);
    
    // Jeśli AI samo zaznaczyło niską pewność → escalate to Sonnet
    var lowConfidenceCount = haikuResult.Hematomy.Count(h => h.Pewnosc == "low");
    if (lowConfidenceCount >= 2 || haikuResult.LiczbaHematom == 0)
    {
        var sonnetResult = await AnalyzePhotoAsync(path, useSonnet: true);
        sonnetResult.UwagiDodatkowe = $"Eskalacja z Haiku (low conf: {lowConfidenceCount}). " + sonnetResult.UwagiDodatkowe;
        return sonnetResult;
    }
    
    return haikuResult;
}
```

---

## UI: Okno reklamacji rozszerzone

**Plik**: `Reklamacje/ReklamacjaWindow.xaml` (rozszerz istniejące)

### Dodaj zakładkę "Analiza Forensyczna AI"

```xml
<TabItem Header="🔬 Analiza AI">
    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>

        <!-- Header z przyciskami -->
        <StackPanel Grid.Row="0" Orientation="Horizontal" Margin="0,0,0,10">
            <Button Content="📷 Dodaj zdjęcia" Click="DodajZdjecia_Click" Padding="12,6"/>
            <Button Content="🤖 Analizuj AI" Click="AnalizujAI_Click" Margin="8,0" Padding="12,6"/>
            <Button Content="📄 Eksport PDF" Click="EksportPDF_Click" Margin="8,0" Padding="12,6"/>
            <TextBlock Margin="20,8,0,0" FontSize="11" Foreground="Gray">
                Sesja: <Run Text="{Binding SessionId}"/> | 
                Zdjęć: <Run Text="{Binding LiczbaZdjec}"/> |
                Koszt AI: <Run Text="{Binding KosztTotal, StringFormat={}{0:F4} USD}"/>
            </TextBlock>
        </StackPanel>

        <!-- Główna część: dwie kolumny -->
        <Grid Grid.Row="1">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="2*"/>
                <ColumnDefinition Width="3*"/>
            </Grid.ColumnDefinitions>

            <!-- Lewa: lista zdjęć -->
            <ListBox Grid.Column="0" ItemsSource="{Binding Zdjecia}" 
                     SelectedItem="{Binding WybraneZdjecie}">
                <ListBox.ItemTemplate>
                    <DataTemplate>
                        <StackPanel Orientation="Horizontal" Margin="4">
                            <Image Source="{Binding PhotoPath}" Width="80" Height="80"/>
                            <StackPanel Margin="8,0">
                                <TextBlock Text="{Binding OpisFoto}" FontWeight="Bold"/>
                                <TextBlock Text="{Binding StatusBadge}" Foreground="Gray"/>
                                <TextBlock>
                                    <Run Text="Hematom: "/>
                                    <Run Text="{Binding LiczbaHematom}" FontWeight="Bold"/>
                                </TextBlock>
                            </StackPanel>
                        </StackPanel>
                    </DataTemplate>
                </ListBox.ItemTemplate>
            </ListBox>

            <!-- Prawa: szczegóły -->
            <Grid Grid.Column="1" Margin="12,0,0,0">
                <Grid.RowDefinitions>
                    <RowDefinition Height="*"/>
                    <RowDefinition Height="200"/>
                </Grid.RowDefinitions>

                <!-- Zoom zdjecia + overlay z markerami hematom -->
                <Border Grid.Row="0" BorderBrush="Gray" BorderThickness="1">
                    <Canvas>
                        <Image Source="{Binding WybraneZdjecie.PhotoPath}"
                               Stretch="Uniform"
                               Canvas.Top="0" Canvas.Left="0"/>
                        <!-- Tu byłyby kółka oznaczające hematomy -->
                    </Canvas>
                </Border>

                <!-- Tabela hematom dla wybranego zdjęcia -->
                <DataGrid Grid.Row="1" ItemsSource="{Binding WybraneZdjecie.Hematomy}"
                          AutoGenerateColumns="False" IsReadOnly="True">
                    <DataGrid.Columns>
                        <DataGridTextColumn Header="#" Binding="{Binding Id}" Width="30"/>
                        <DataGridTextColumn Header="Lokalizacja" Binding="{Binding Lokalizacja}" Width="120"/>
                        <DataGridTextColumn Header="Kolor" Binding="{Binding Kolor}" Width="100"/>
                        <DataGridTextColumn Header="Wiek (h)" Binding="{Binding WiekDisplay}" Width="80"/>
                        <DataGridTextColumn Header="Przyczyna" Binding="{Binding Przyczyna}" Width="150"/>
                        <DataGridTextColumn Header="Pewność" Binding="{Binding Pewnosc}" Width="80"/>
                    </DataGrid.Columns>
                </DataGrid>
            </Grid>
        </Grid>

        <!-- Footer: podsumowanie + winowajcy -->
        <Border Grid.Row="2" Background="#F1F5F9" Padding="12" Margin="0,10,0,0">
            <Grid>
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="*"/>
                    <ColumnDefinition Width="*"/>
                    <ColumnDefinition Width="*"/>
                </Grid.ColumnDefinitions>

                <StackPanel Grid.Column="0">
                    <TextBlock Text="🏭 PROCES UBOJU" FontWeight="Bold"/>
                    <TextBlock>
                        <Run Text="{Binding LiczbaUboj}" FontSize="20"/>
                        <Run Text="hematom (" FontSize="11"/>
                        <Run Text="{Binding ProcUboj, StringFormat={}{0:P0}}" FontWeight="Bold"/>
                        <Run Text=")" FontSize="11"/>
                    </TextBlock>
                </StackPanel>

                <StackPanel Grid.Column="1">
                    <TextBlock Text="🚛 ŁAPANIE / TRANSPORT" FontWeight="Bold"/>
                    <TextBlock>
                        <Run Text="{Binding LiczbaTransport}" FontSize="20"/>
                        <Run Text="hematom (" FontSize="11"/>
                        <Run Text="{Binding ProcTransport, StringFormat={}{0:P0}}" FontWeight="Bold"/>
                        <Run Text=")" FontSize="11"/>
                    </TextBlock>
                </StackPanel>

                <StackPanel Grid.Column="2">
                    <TextBlock Text="🐔 HODOWCA" FontWeight="Bold"/>
                    <TextBlock>
                        <Run Text="{Binding LiczbaHodowca}" FontSize="20"/>
                        <Run Text="hematom (" FontSize="11"/>
                        <Run Text="{Binding ProcHodowca, StringFormat={}{0:P0}}" FontWeight="Bold"/>
                        <Run Text=")" FontSize="11"/>
                    </TextBlock>
                </StackPanel>
            </Grid>
        </Border>
    </Grid>
</TabItem>
```

---

## RAPORT PDF (dla hodowcy / klienta)

### Sekcje raportu
1. **Strona tytułowa**: numer reklamacji, data, klient, partia, hodowca
2. **Podsumowanie wykonawcze**:
   - "Analizowano X tuszek z partii Y"
   - "Z czego A% hematom pochodzi z procesu uboju, B% z transportu, C% z farmy"
3. **Szczegóły per tuszka**:
   - Zdjęcie tuszki
   - Tabela hematom z lokalizacją, kolorem, wiekiem, przyczyną
4. **Wnioski + rekomendacje**:
   - "Zalecamy: hodowcy → kontrola warunków łapania"
   - "Zalecamy: linia → sprawdzić skubarkę #2"
5. **Metryki AI**: model użyty, koszt, czas analizy (transparentność)
6. **Pieczątka cyfrowa**: hash zdjęć + analizy (niezmienność dla audytu)

### Generowanie PDF — biblioteka
- **QuestPDF** (darmowa do 1M PLN przychodu, komercyjna od 100 USD/rok)
- Lub **iText 7** (komercyjna)
- Lub **PdfSharp** + **MigraDoc** (open source)

```csharp
// QuestPDF example
public byte[] GenerateReport(HematomaSession session)
{
    return Document.Create(c =>
    {
        c.Page(p =>
        {
            p.Margin(20);
            p.Header().Text("Raport Forensyczny — Hematomy");
            p.Content().Column(col =>
            {
                col.Item().Text($"Reklamacja: {session.ReklamacjaId}");
                col.Item().Text($"Hodowca: {session.HodowcaNazwa}");
                col.Item().Text($"Data: {session.SessionDateTime:dd.MM.yyyy}");
                
                foreach (var photo in session.Zdjecia)
                {
                    col.Item().Image(photo.PhotoPath, ImageScaling.FitWidth);
                    col.Item().Table(t =>
                    {
                        t.ColumnsDefinition(cd => 
                        { 
                            cd.RelativeColumn(); cd.RelativeColumn(); cd.RelativeColumn(); cd.RelativeColumn();
                        });
                        // Header
                        t.Header(h =>
                        {
                            h.Cell().Text("Lokalizacja");
                            h.Cell().Text("Kolor");
                            h.Cell().Text("Wiek (h)");
                            h.Cell().Text("Przyczyna");
                        });
                        foreach (var hem in photo.Hematomy)
                        {
                            t.Cell().Text(hem.Lokalizacja);
                            t.Cell().Text(hem.Kolor);
                            t.Cell().Text($"{hem.WiekGodzinMin}-{hem.WiekGodzinMax}");
                            t.Cell().Text(hem.Przyczyna);
                        }
                    });
                }
            });
            p.Footer().AlignCenter().Text(t => 
            {
                t.Span("Strona ");
                t.CurrentPageNumber();
                t.Span(" z ");
                t.TotalPages();
            });
        });
    }).GeneratePdf();
}
```

---

## WORKFLOW UŻYTKOWNIKA

### Scenariusz 1: Reklamacja od klienta
1. Klient (Auchan) zgłasza: "30 sztuk fileta z siniakami"
2. QC otwiera ZPSP → Reklamacje → Nowa
3. Wprowadza dane reklamacji
4. Klika "Analiza AI"
5. Klika "+ Dodaj zdjęcia" → robi 10-30 zdjęć (lub uploaduje)
6. Klika "Analizuj AI" → czeka 30-60 sek (paralelne dla 30 zdjęć)
7. System pokazuje breakdown: 12 ubój / 15 transport / 3 hodowca
8. Klika "Eksport PDF" → wysyła do klienta + hodowcy
9. Negocjacje rozliczenia oparte na konkretnym raporcie

### Scenariusz 2: Wewnętrzny audyt (proaktywnie)
1. Raz w tygodniu QC bierze 50 losowych tuszek klasy B
2. Robi zdjęcia, analizuje
3. Raport per zmiana: "wzrost hematom świeżych o 30% w czwartek 14:00-16:00" → coś się stało na linii w tym czasie

### Scenariusz 3: Trend miesięczny per hodowca
1. Co miesiąc system generuje raport per hodowca
2. Pokazuje: % hematom hodowcy / transport / uboju
3. Hodowca Kowalski: 8% hematom "starych" (norma 3%) → rozmowa o problemie w kurniku

---

## RYZYKA I WYZWANIA

### Ryzyko 1: AI się myli
- **Mitygacja**: pewnosc=low → escalate na Sonnet
- **Mitygacja**: operator zawsze potwierdza, może edytować
- **Mitygacja**: zbieraj feedback (operator zmienił → trening)

### Ryzyko 2: Sprzeciw hodowców ("AI kłamie!")
- **Mitygacja**: pokazuj im PDF systemu, kolory rzeczywiście potwierdzone naukowo (PDF Broiler Meat Signals)
- **Mitygacja**: prowadź statystyki dla wszystkich hodowców, nie tylko "trudnych"
- **Mitygacja**: zaproponuj im audyt: weź losowe tuszki, sprawdź z weterynarzem, porównaj z AI

### Ryzyko 3: Koszt API rośnie
- **Mitygacja**: monitoring kosztów, alert gdy >50 zł/mies
- **Mitygacja**: prompt caching (4× taniej)
- **Mitygacja**: Haiku-first

### Ryzyko 4: Jakość zdjęć
- **Wymóg**: dobre oświetlenie, ostrość, neutralne tło
- **Sugestia**: kup namiot fotograficzny przemysłowy (300-500 zł) — stała jakość zdjęć
- **Sugestia**: pierwsze 100 zdjęć z fotografem zawodowym do treningu personelu

---

## CZAS IMPLEMENTACJI

| Etap | Czas | Koszt |
|---|---|---|
| Setup Claude API + credit | — | $20 startowe |
| Tabele bazy + migracje | 4h | — |
| Serwis HematomaAnalysisService | 12h | — |
| UI: zakładka w Reklamacje | 16h | — |
| PDF report generator | 8h | QuestPDF Pro $99/rok |
| Testy + tuning promptu | 12h | $20 testów AI |
| Pilot z 5 prawdziwymi reklamacjami | 1 tydzień | — |
| Trening operatorów QC | 4h | — |
| **RAZEM** | **~60h pracy** | **~$150 + 99/rok** |

---

## TIPS PRO

### Tip 1: Annotated overlay
Zamiast tylko tabeli — **rysuj** kółka na zdjęciu w miejscach hematom z numerkiem. Czyta się to lepiej niż tabela.

```csharp
// Pseudo-kod overlay:
foreach (var hem in result.Hematomy)
{
    var canvas = ... ;
    var ellipse = new Ellipse { Width = 30, Height = 30, 
                                 Stroke = ColorForCause(hem.Przyczyna), 
                                 StrokeThickness = 3 };
    Canvas.SetLeft(ellipse, hem.X);
    Canvas.SetTop(ellipse, hem.Y);
    canvas.Children.Add(ellipse);
    
    var label = new TextBlock { Text = hem.Id.ToString() };
    Canvas.SetLeft(label, hem.X + 8);
    Canvas.SetTop(label, hem.Y + 5);
    canvas.Children.Add(label);
}
```

⚠️ **Problem**: Claude nie zwraca dokładnych współrzędnych pixel-perfect. Trzeba poprosić w prompcie o `bbox: [x1, y1, x2, y2]` w normalised coordinates (0-1).

### Tip 2: Multi-photo session
Jedna reklamacja = wiele zdjęć (pierś, udo, bok, całość). System agreguje z różnych zdjęć tej samej tuszki.

### Tip 3: Historia per hodowca
Dashboard miesięczny:
```
Kowalski — 2026
Sty: 5% hematom hodowcy   ████░░░░
Lut: 4% hematom hodowcy   ███░░░░░  
Mar: 6% hematom hodowcy   ████░░░░
Kwi: 12% hematom hodowcy  █████████ ⚠ DRAMAT
Maj: 8% hematom hodowcy   ██████░░░ 

ALERT KWI: rozmowa z hodowcą + audyt farmy
```

### Tip 4: Marketing
Po wdrożeniu — **napisz artykuł** dla "Polskie Drobiarstwo" o tym że pierwszy w Polsce robisz AI forensic. **Darmowy marketing** + przyciągnie kolejnych klientów.
