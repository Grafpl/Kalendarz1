# 21. ⭐ Yield per Category Waterfall — PEŁNY PORADNIK

## Co to jest
**Wodospad uzysku** = wizualizacja **gdzie znika mięso** od żywej kury (100%) do gotowego produktu sprzedawanego klientowi. Każdy etap pokazuje **ile %** i **w PLN** tracicie.

## Wartość biznesowa
**Najbardziej strategiczna metryka w zakładzie** — pokazuje:
- Gdzie tracisz pieniądze na łańcuchu
- Które etapy są najgorsze
- Gdzie usprawnienie ma największy zwrot

Nie tyle **oszczędność** co **świadomość kosztów** — daje materiał do strategicznych decyzji wartych setki tysięcy.

---

## ANATOMIA WODOSPADU

```
1000 kg ŻYWIEC (cena zakupu, np. 5.20 zł/kg = 5200 zł)
   │
   │ -1.2% DOA (transport): 12 kg → ~62 zł straty
   ▼
988 kg żywego do uboju
   │
   │ -3% odrzuty ante-mortem (chore): 30 kg → ~155 zł
   ▼
958 kg dopuszczonych do uboju
   │
   │ -8% krew (wykrwawienie): 77 kg
   ▼
881 kg po wykrwawieniu
   │
   │ -8% pióra (skubanie): 70 kg
   ▼
811 kg tuszki z pióra
   │
   │ -10% wnętrzności (eviszeracja): 81 kg
   │   z tego: 1.5% jadalne (wątroba, serce, żołądek) → sprzedaż na rynki azjatyckie
   ▼
730 kg tuszki gotowe
   │
   │ -4% odrzuty wet. (ascites, cellulitis, GMD): 29 kg
   ▼
701 kg tuszki dopuszczone do sprzedaży
   │
   │ -2.5% drip loss (chłodzenie): 17.5 kg → strata wagi (bez wartości handlowej)
   ▼
683.5 kg tuszki po chłodzeniu
   │
   │ STRATY KROJENIA / PORCJOWANIA (jeśli sprzedajesz porcjowane):
   │ -10% kości (porcjowanie): 68 kg → trafia do KARMA
   │ -3% przekrwawione/wadliwe kawałki: 20 kg → klasa B, -30% ceny
   ▼
~595 kg jako kawałki "premium" + ~90 kg jako wartość mniejsza

PODSUMOWANIE:
- Wkład: 1000 kg × 5.20 zł = 5200 zł
- Sprzedaż premium: 595 kg × 12 zł = 7140 zł (cena fileta avg)
- Sprzedaż klasa B: 20 kg × 8 zł = 160 zł
- Sprzedaż mrożone: 50 kg × 6 zł = 300 zł
- Karma: 68 kg × 1.5 zł = 102 zł
- Odpady: 12 kg × 0 zł (koszt utylizacji)
- TOTAL przychód: ~7700 zł
- MARŻA BRUTTO: 7700 - 5200 = 2500 zł (48%)
```

---

## ARCHITEKTURA W ZPSP

### Dane już masz w HANDEL i LibraNet
- **Żywiec** (wejście): `listapartii.WagaCalkowita` (LibraNet)
- **DOA**: po wdrożeniu #2 — `listapartii.LiczbaPadlych`
- **Tuszka surowa**: `HM.MG` z `seria='sPWU'` (wytworzenie z żywca w HANDEL)
- **Tuszka klasy A/B**: po wdrożeniu #10 — `ClassificationLog`
- **Kawałki gotowe**: `HM.MG` z `seria='sPWP'` (krojenie)
- **Sprzedaż**: `HM.MG` z `seria='sWZ'` z cenami
- **Odpady, karma**: `HM.MG` ze specyficznymi katalogami towarów (67094, 65547)

### Co trzeba dorzucić
1. **Tabela `EtapWaterfall`** — definicja etapów + reguły agregacji
2. **Tabela `WaterfallSnapshot`** — agregaty per dzień/tydzień/miesiąc
3. **Serwis `WaterfallService`** — obliczenia
4. **Widok `WidokWodospad`** — wizualizacja

---

## SQL: pobieranie danych

### Query bazowy (per dzień)
```sql
-- Bardzo uproszczony, w realu trzeba cross-DB query
DECLARE @Data DATE = '2026-05-23';

WITH 
ZywiecCTE AS (
    SELECT SUM(WagaCalkowita) AS WagaKg, SUM(LiczbaSztuk * CenaZakupu) AS WartoscZakupu
    FROM LibraNet.dbo.listapartii
    WHERE DataPrzyjecia = @Data
),
TuszkiSurowe AS (
    SELECT SUM(ABS(MZ.ilosc) * MZ.waga) AS WagaKg
    FROM HANDEL.HM.MG MG
    JOIN HANDEL.HM.MZ MZ ON MZ.dokid = MG.id
    WHERE CAST(MG.data AS DATE) = @Data
      AND MG.seria = 'sPWU'
      AND MG.anulowany = 0
),
TuszkiPoChlodzeniu AS (
    -- ...
),
KrojonePremium AS (
    SELECT SUM(ABS(MZ.ilosc) * MZ.waga) AS WagaKg, SUM(ABS(MZ.ilosc) * MZ.waga * MZ.cena) AS Wartosc
    FROM HANDEL.HM.MG MG
    JOIN HANDEL.HM.MZ MZ ON MZ.dokid = MG.id
    JOIN HANDEL.HM.TW TW ON TW.id = MZ.idtw
    WHERE CAST(MG.data AS DATE) = @Data
      AND MG.seria = 'sPWP'
      AND MG.anulowany = 0
      AND TW.katalog = 67095  -- Mięso świeże
)
SELECT 
    'ZYWIEC' AS Etap, ZywiecCTE.WagaKg, ZywiecCTE.WartoscZakupu AS Wartosc
FROM ZywiecCTE
UNION ALL
SELECT 'TUSZKA_SUROWA', TuszkiSurowe.WagaKg, NULL FROM TuszkiSurowe
-- ... itd
```

### Reguły kalkulacji strat
```sql
CREATE TABLE EtapWaterfall (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Kolejnosc INT NOT NULL,
    KodEtapu NVARCHAR(30) NOT NULL UNIQUE,
    NazwaEtapu NVARCHAR(100) NOT NULL,
    KategoriaStraty NVARCHAR(50) NULL,  -- np. 'PIORA', 'KOSCI', 'WET_REJECT'
    OczekiwanyProcStratyMin DECIMAL(5,2) NULL,
    OczekiwanyProcStratyMax DECIMAL(5,2) NULL,
    KolorWizualizacji NVARCHAR(7) NULL,  -- '#RRGGBB'
    OpisTechniczny NVARCHAR(500) NULL,
    SposobDochodu NVARCHAR(50) NULL  -- 'KARMA', 'ODPAD', 'UTYL', 'NIEUZYTECZNE'
);

INSERT INTO EtapWaterfall VALUES
(1, 'ZYWIEC', 'Żywiec przyjęty', NULL, NULL, NULL, '#3B82F6', 'Wejście wagi z PartiaDostawca', NULL),
(2, 'DOA', 'DOA transportowe', 'DEAD', 0.1, 0.5, '#EF4444', 'Padłe w transporcie', 'UTYL'),
(3, 'ANTE_MORTEM', 'Odrzuty ante-mortem', 'WET_REJECT', 1.0, 3.0, '#F59E0B', 'Chore tuszki rzucone przez wet.', 'UTYL'),
(4, 'KREW', 'Wykrwawienie', 'KREW', 6.0, 10.0, '#7C3AED', 'Krew z procesu wykrwawiania', 'NIEUZYTECZNE'),
(5, 'PIORA', 'Pióra', 'PIORA', 7.0, 9.0, '#84CC16', 'Pióra ze skubania', 'NIEUZYTECZNE'),
(6, 'WNETRZNOSCI', 'Wnętrzności', 'OFFAL', 8.0, 12.0, '#22C55E', 'Z czego 1.5-2% jadalne', 'KARMA/EKSPORT'),
(7, 'WET_POST', 'Odrzuty post-mortem', 'WET_REJECT', 2.0, 5.0, '#F59E0B', 'Ascites, cellulitis, GMD', 'UTYL'),
(8, 'DRIP_LOSS', 'Drip loss (chłodzenie)', 'CHILLING_DRIP', 1.5, 3.5, '#06B6D4', 'Wyciek wody przy chłodzeniu', 'NIEUZYTECZNE'),
(9, 'KOSCI_KROJENIE', 'Kości z krojenia', 'KOSCI', 8.0, 12.0, '#84CC16', 'Trafia do mączki kostnej / karmy', 'KARMA'),
(10, 'WADY_KROJENIE', 'Wady klasy B', 'KLASA_B', 2.0, 5.0, '#F59E0B', 'Hematomy, pop-out, GMD wykryte', 'KLASA_B'),
(11, 'PREMIUM', 'Klasa A premium', NULL, NULL, NULL, '#10B981', 'Filet, pierś, udo do klienta', 'SPRZEDAZ');
```

---

## SERWIS WaterfallService

**Plik**: `AnalitykaPelna/Services/WaterfallService.cs`

```csharp
public class WaterfallService
{
    public async Task<WaterfallData> CalculateAsync(DateTime od, DateTime doDate)
    {
        var data = new WaterfallData { Od = od, DoDate = doDate };
        
        // 1. Pobierz wszystkie etapy ze słownika
        var etapy = await LoadEtapyAsync();
        
        // 2. Oblicz wagi dla każdego etapu (cross-DB)
        var zywiec = await CalcZywiecAsync(od, doDate);          // LibraNet
        var doa = await CalcDOAAsync(od, doDate);                // LibraNet (po #2)
        var tuszkiSurowe = await CalcTuszkiSuroweAsync(od, doDate); // HANDEL (sPWU)
        var dripLoss = await CalcDripLossAsync(od, doDate);      // LibraNet (po #20)
        var krojonePremium = await CalcKrojonePremiumAsync(od, doDate); // HANDEL
        var karmaOdpady = await CalcKarmaOdpadyAsync(od, doDate); // HANDEL
        // ...

        // 3. Buduj wodospad: każdy etap z wagą i wartością
        data.Etapy = new List<WaterfallStep>
        {
            new WaterfallStep 
            { 
                Kod = "ZYWIEC", 
                Nazwa = "Żywiec przyjęty",
                WagaKg = zywiec.Waga, 
                WartoscPLN = zywiec.Wartosc,
                Kolor = "#3B82F6"
            },
            new WaterfallStep 
            { 
                Kod = "DOA",
                Nazwa = "DOA transportowe",
                StrataKg = doa.WagaPadlych,
                StrataPLN = doa.WagaPadlych * zywiec.CenaJednKg,
                ProcStrata = (doa.WagaPadlych / zywiec.Waga) * 100,
                Kolor = "#EF4444",
                Status = doa.WagaPadlych / zywiec.Waga * 100 > 0.5 ? "ALERT" : "OK"
            },
            // ... wszystkie pozostałe etapy
        };

        // 4. Oblicz końcowy yield
        data.WagaWejscia = zywiec.Waga;
        data.WagaWyjscia = krojonePremium.Waga;
        data.YieldProc = data.WagaWyjscia / data.WagaWejscia * 100;
        
        data.WartoscWejscia = zywiec.Wartosc;
        data.WartoscWyjscia = krojonePremium.Wartosc + dryfPozycji;
        data.MarzaPLN = data.WartoscWyjscia - data.WartoscWejscia;
        data.MarzaProc = data.MarzaPLN / data.WartoscWejscia * 100;

        return data;
    }
}

public class WaterfallData
{
    public DateTime Od { get; set; }
    public DateTime DoDate { get; set; }
    public List<WaterfallStep> Etapy { get; set; } = new();
    public double WagaWejscia { get; set; }
    public double WagaWyjscia { get; set; }
    public double YieldProc { get; set; }
    public double WartoscWejscia { get; set; }
    public double WartoscWyjscia { get; set; }
    public double MarzaPLN { get; set; }
    public double MarzaProc { get; set; }
}

public class WaterfallStep
{
    public string Kod { get; set; } = "";
    public string Nazwa { get; set; } = "";
    public double WagaKg { get; set; }
    public double StrataKg { get; set; }
    public double WartoscPLN { get; set; }
    public double StrataPLN { get; set; }
    public double ProcStrata { get; set; }
    public string Kolor { get; set; } = "";
    public string Status { get; set; } = "OK";  // OK, WARN, ALERT
    public string SposobDochodu { get; set; } = "";  // KARMA, ODPAD, UTYL
}
```

---

## UI — Widok wodospadu

### Wariant 1: Klasyczny wodospad (waterfall chart)
```
1000 kg ████████████████████████████████████████████████ ŻYWIEC
                                                          (5200 zł, 100%)
                                                  
 -12 kg ▌                                              DOA -1.2% (62 zł)
                                                  
 988 kg ███████████████████████████████████████████████  Po DOA

 -30 kg ▌▌                                             ANTE-MORTEM -3% (155 zł)
                                                  
 958 kg ██████████████████████████████████████████████  Dopuszczone

 -77 kg ▌▌▌▌▌                                          WYKRWAWIENIE -8% (400 zł)

 881 kg █████████████████████████████████████████      Po krwi
 -70 kg ▌▌▌▌                                           PIÓRA -8% (370 zł)

 811 kg ███████████████████████████████████████        Tuszka surowa
 -81 kg ▌▌▌▌▌                                          WNĘTRZNOŚCI -10% (420 zł, ale karma=130 zł)

 730 kg ████████████████████████████████████           Tuszka gotowa
 -29 kg ▌▌                                             WET ODRZUTY -4% (150 zł, utyl)

 701 kg ███████████████████████████████████            Po wet
 -18 kg ▌                                              DRIP LOSS -2.5% (94 zł)

 683 kg █████████████████████████████████              Po chłodzeniu
 -68 kg ▌▌▌▌                                           KOŚCI KROJENIE -10% (350 zł, karma=100 zł)
 -20 kg ▌                                              KLASA B -3% (104 zł, sprzedaż -30%)

 595 kg ███████████████████████████████                PREMIUM (7140 zł)
                                                       
═══════════════════════════════════════════════════
YIELD: 59.5%   MARŻA: 2500 zł (48%)
```

### Wariant 2: Sankey diagram
Bardziej "fancy" — strumienie wpływające do różnych destination'ów:
- 595 kg → SPRZEDAŻ PREMIUM
- 90 kg → SPRZEDAŻ KLASA B / MROŻONE  
- 68 kg → KARMA
- 81 kg → KARMA (offal) / EKSPORT (wątroba do Azji)
- 29 kg → UTYLIZACJA
- 100 kg → ODPADY (krew, pióra) → utylizacja

### Wariant 3: Tabela waterfall (eksport CSV)
```
Etap                  | Waga kg | Strata kg | % straty | Wartość PLN | Strata PLN | Status
ŻYWIEC                | 1000    |        0  |    -     |    5200     |     -      | -
DOA                   |  988    |       12  |   1.2%   |       -     |     62     | ⚠ HIGH
Ante-mortem           |  958    |       30  |   3.0%   |       -     |    155     | OK
Wykrwawienie          |  881    |       77  |   8.0%   |       -     |    400     | OK
Pióra                 |  811    |       70  |   8.0%   |       -     |    370     | OK
Wnętrzności           |  730    |       81  |  10.0%   |    130     |    290     | OK (karma)
Wet odrzuty           |  701    |       29  |   4.0%   |       -     |    150     | ⚠
Drip loss             |  683    |       18  |   2.5%   |       -     |     94     | OK
Kości krojenie        |  615    |       68  |  10.0%   |    100     |    250     | OK (karma)
Klasa B               |  595    |       20  |   3.0%   |    160     |     -      | OK
KOŃCOWY PREMIUM       |  595    |        -  |    -     |   7140     |     -      | ✓
─────────────────────────────────────────────────────────────────────────────────
MARŻA BRUTTO          |         |           |          |   2500     |   1771     | 48%
YIELD UZYSKU          |  59.5%  |          |          |            |            |
```

---

## XAML Widget Implementation

**Plik**: `AnalitykaPelna/Views/WidokWodospad.xaml`

```xml
<UserControl x:Class="Kalendarz1.AnalitykaPelna.Views.WidokWodospad">
    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>

        <!-- Pasek filtrów -->
        <controls:FiltryPasek Grid.Row="0" x:Name="filtryPasek"/>

        <!-- KPI w nagłówku -->
        <UniformGrid Grid.Row="1" Rows="1" Columns="4" Margin="0,12">
            <Border Background="#3B82F6" CornerRadius="8" Margin="4" Padding="12">
                <StackPanel>
                    <TextBlock Text="WEJŚCIE" Foreground="White" FontSize="11"/>
                    <TextBlock Text="{Binding WagaWejscia, StringFormat={}{0:N0} kg}" 
                               Foreground="White" FontSize="20" FontWeight="Bold"/>
                </StackPanel>
            </Border>
            <Border Background="#10B981" CornerRadius="8" Margin="4" Padding="12">
                <StackPanel>
                    <TextBlock Text="WYJŚCIE PREMIUM" Foreground="White" FontSize="11"/>
                    <TextBlock Text="{Binding WagaWyjscia, StringFormat={}{0:N0} kg}" 
                               Foreground="White" FontSize="20" FontWeight="Bold"/>
                </StackPanel>
            </Border>
            <Border Background="#7C3AED" CornerRadius="8" Margin="4" Padding="12">
                <StackPanel>
                    <TextBlock Text="YIELD" Foreground="White" FontSize="11"/>
                    <TextBlock Text="{Binding YieldProc, StringFormat={}{0:F1}%}" 
                               Foreground="White" FontSize="20" FontWeight="Bold"/>
                </StackPanel>
            </Border>
            <Border Background="#F59E0B" CornerRadius="8" Margin="4" Padding="12">
                <StackPanel>
                    <TextBlock Text="MARŻA BRUTTO" Foreground="White" FontSize="11"/>
                    <TextBlock Text="{Binding MarzaPLN, StringFormat={}{0:N0} zł ({0:P1})}" 
                               Foreground="White" FontSize="20" FontWeight="Bold"/>
                </StackPanel>
            </Border>
        </UniformGrid>

        <!-- Wodospad -->
        <ScrollViewer Grid.Row="2" VerticalScrollBarVisibility="Auto">
            <ItemsControl ItemsSource="{Binding Etapy}">
                <ItemsControl.ItemTemplate>
                    <DataTemplate>
                        <Grid Margin="0,4" Height="36">
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width="200"/>
                                <ColumnDefinition Width="*"/>
                                <ColumnDefinition Width="150"/>
                            </Grid.ColumnDefinitions>

                            <!-- Nazwa etapu -->
                            <TextBlock Grid.Column="0" Text="{Binding Nazwa}" 
                                       VerticalAlignment="Center" FontWeight="SemiBold"/>

                            <!-- Pasek proporcjonalny -->
                            <Border Grid.Column="1" 
                                    Background="{Binding Kolor}"
                                    Width="{Binding ProporcjaWagi}"
                                    HorizontalAlignment="Left"
                                    CornerRadius="4">
                                <TextBlock Text="{Binding LabelKg}" 
                                           Foreground="White" 
                                           Margin="8,0"
                                           VerticalAlignment="Center"/>
                            </Border>

                            <!-- Wartość PLN + status -->
                            <StackPanel Grid.Column="2" Orientation="Horizontal" 
                                        HorizontalAlignment="Right" VerticalAlignment="Center">
                                <TextBlock Text="{Binding WartoscDisplay}" Margin="4,0"/>
                                <TextBlock Text="{Binding StatusIcon}" FontSize="14"/>
                            </StackPanel>
                        </Grid>
                    </DataTemplate>
                </ItemsControl.ItemTemplate>
            </ItemsControl>
        </ScrollViewer>

        <!-- Footer z akcjami -->
        <StackPanel Grid.Row="3" Orientation="Horizontal" HorizontalAlignment="Right" Margin="0,12">
            <Button Content="📥 Eksport CSV" Click="EksportCSV_Click" Margin="4,0" Padding="12,6"/>
            <Button Content="📄 Eksport PDF" Click="EksportPDF_Click" Margin="4,0" Padding="12,6"/>
            <Button Content="📊 Porównaj okresy" Click="Porownaj_Click" Margin="4,0" Padding="12,6"/>
            <Button Content="🎯 Sankey" Click="Sankey_Click" Margin="4,0" Padding="12,6"/>
        </StackPanel>
    </Grid>
</UserControl>
```

---

## DRILL-DOWN — klik etapu pokazuje szczegóły

Klik na "WET ODRZUTY" → modal z:
- Lista konkretnych odrzuconych tuszek (z #11 Digital inspection)
- Per typ wady: 12 ascites, 8 cellulitis, 9 inne
- Per partia: które partie miały najgorsze wyniki
- Per hodowca: ranking

Klik na "DRIP LOSS" → modal z:
- Krzywa chłodzenia (z #18)
- Korelacja: czy drip loss koreluje z odchyleniami krzywej?

Klik na "KLASA B" → modal z:
- Top 10 typów wad
- Wartość każdej kategorii w PLN
- Sugestie naprawy (np. "60% wad to pop-outy → sprawdź skubarki")

---

## PORÓWNANIA OKRESÓW

```
Maj 2026 vs Kwiecień 2026:
                       Kwi     Maj    Δ      Trend
DOA:                  0.18%  0.21%  +0.03% ⚠
Wet odrzuty:           3.8%   4.2%  +0.4%  ⚠
Drip loss:             2.4%   2.6%  +0.2%  ⚠
Klasa B:               4.1%   4.8%  +0.7%  ⚠ 
Yield premium:        61.2%  59.8%  -1.4%  ⚠⚠

MAJ JEST GORSZY! Sprawdź:
- Czy zmiana hodowców?
- Czy zmiana temp chłodzenia?
- Czy nowy operator?
```

---

## SCENARIUSZE BIZNESOWE

### Scenariusz 1: Pytanie strategiczne
**Pytanie**: "Czy warto kupić nowe spin chiller za 200k zł?"
**Bez systemu**: szacujesz "no pewnie tak"
**Z systemem**: widzisz że drip loss = 18 kg/1000 kg żywca = 2.5%. Spin chiller redukuje to do 1.5% = oszczędność 10 kg × cena fileta 12 zł = 120 zł/1000 kg. Przy 200 ton/dzień = 24k zł/dzień × 250 dni = **6M zł/rok**. ROI = 1.5 miesiąca. **DECYZJA: KUP**.

### Scenariusz 2: Negocjacja z hodowcą
**Sytuacja**: Kowalski podnosi cenę z 5.20 na 5.40 zł/kg
**Bez systemu**: rozważasz, ale brakuje argumentów
**Z systemem**: pokazujesz Kowalskiemu jego waterfall:
- Twój yield z innych hodowców: 60%
- Yield z Kowalskiego: 56% (wadliwe kości, drip loss wyższy)
- "Twoje kurczaki kosztują mnie efektywnie 5.20 / 0.56 = 9.30 zł/kg gotowego mięsa"
- "Średnio od konkurentów: 5.20 / 0.60 = 8.67 zł/kg gotowego mięsa"
- "Twoja podwyżka 4% × twój gorszy yield = realnie kosztuje mnie 8%"
- **Decyzja Kowalski**: poprawia warunki lub spada w cenie

### Scenariusz 3: Pokazanie wartości szefowi
Drukujesz waterfall na A3, pokazujesz na zebraniu:
- "Tracimy 18 kg/ton na drip loss, to 360 t/rok = 4.3M PLN potencjału"
- "Tracimy 30 kg/ton na klasie B, to 600 t/rok = 7.2M PLN potencjału"
- "TOTAL potencjał poprawy: ~15M PLN/rok jak zoptymalizujemy 3 obszary"

---

## INTEGRACJA Z EXISTING ZPSP

### Reuse z AnalitykaPelna
- `WydajnoscService` ma już dane HANDEL — extend o nowe queries
- `BilansService` ma już framework — wodospad to nowy widok
- `FiltryPasek` reuse 1:1

### Nowy widok w `AnalitykaPelnaWindow`
```xml
<TabItem Header="💧 Wodospad">
    <views:WidokWodospad/>
</TabItem>
```

---

## CZAS IMPLEMENTACJI

| Etap | Czas |
|---|---|
| Tabele `EtapWaterfall` + seed | 4h |
| SQL queries cross-DB (zywiec+tuszki+drip+sprzedaż) | 12h |
| WaterfallService | 16h |
| UI WidokWodospad + KPI + barwa | 16h |
| Drill-down dialogi (klik etapu) | 16h |
| Porównania okresów | 8h |
| Eksport CSV/PDF | 8h |
| **RAZEM** | **~80h** |

**Bez nowego sprzętu** — to czysto software, dane już są w bazach.

**MVP w tydzień**: pierwsze 5 etapów bez drill-down — już daje WOW.

---

## DODATKOWO: Per partia / per hodowca

### Tabela waterfall agregowany per hodowca (miesięczny)
```
HODOWCA: KOWALSKI — MAJ 2026 (12 partii, 18 ton żywca)
─────────────────────────────────────────────────────
Etap                   kg    % straty    PLN strata
ZYWIEC               18 000   -          93 600
DOA                     54   0.3%          280  ⚠
Wet odrzuty             720   4.0%        3 740
Drip loss               450   2.5%        2 340
Klasa B                 360   2.0%        1 870
PREMIUM              10 800   60.0%       129 600
─────────────────────────────────────────────────────
YIELD: 60%  |  MARŻA: 36 000 zł  |  Ranking: 8/24 hodowców
```

To są dane do twardych negocjacji.
