# 18. ⭐ Chilling Curve Monitor — PEŁNY PORADNIK

## Co to jest
**Chilling** = schładzanie tuszek od ~40°C (temp ciała żywego ptaka) do <4°C (cold storage) w ciągu max 6h (wymóg EU 92-116).

**Krzywa idealna** wg PDF Broiler Meat Signals str. 152-153:
> "Rule of thumb: temperatura tuszki w °C halvuje się w każdej 1/4 czasu chłodzenia"

```
Czas:     0%   25%   50%   75%   100%
Temp:    40°C  20°C  10°C  5°C   2.5°C
```

## Problem dziś
- Chłodnia działa ciągle, ale **nikt nie sprawdza krzywej**
- Co kilka godzin ktoś sprawdza temperaturę "na oko" (termometr ręczny)
- Brak dokumentacji = problem przy audycie BRC v9
- Niezauważona awaria (np. kompresor 1h nie pracuje) = ryzyko mikrobiologiczne dla całej partii

## Wartość
- **Bezpieczeństwo**: zapobieganie incydentowi mikrobiologicznemu (recall = 2-5 mln PLN)
- **Jakość**: optymalna krzywa = mniej drip loss (= więcej kg do sprzedaży)
- **Compliance**: dokumentacja HACCP/BRC v9 (sek. 4.7, 4.8)
- **Oszczędność energii**: niedoschłodzone → marnujesz prąd; przechłodzone → marnujesz prąd
- **Wartość roczna: ~200-400 tys PLN** (mix oszczędności + uniknięcia incydentu)

---

## ARCHITEKTURA

### Co mierzysz
1. **Temperatura powietrza w chłodni** (5-10 punktów na różnych wysokościach)
2. **Temperatura rdzenia tuszki** (sonda wbijana w 1-2 tuszki testowe co partia)
3. **Wilgotność powietrza** (wpływa na drip loss)
4. **Prędkość przepływu powietrza** (opcjonalnie, dla zaawansowanych)
5. **Czas każdej tuszki w chłodni** (wejście → wyjście)

### Hardware (zalecam **rozszerzyć** setup z #9 i #19)

#### Czujniki powietrza (5-10 sztuk)
- **Tinytag Plus 2 TGP-4017** — datalogger z wbudowaną sondą — ~800 zł/szt
- Lub: PT1000 + konwerter Modbus (jak w #9) — wybór taniej dla większej skali
- **Termohigrometr** (wilgotność + temp): Tinytag TV-4500 — 1200 zł/szt

#### Czujniki rdzeniowe (przebijanie tuszki)
- Wireless probe Tinytag TGP-4500 z sondą iglicową — 1500 zł
- Wbijanie ~5cm w środek piersi 1-2 tuszek testowych z każdej partii
- Bezprzewodowa transmisja do gateway w chłodni

#### Gateway / centralka
- Tinytag Cloud Connect Gateway (Wi-Fi/Ethernet) — 2000 zł
- Lub: własna stacja Raspberry Pi z RTL-SDR + receivers (~500 zł, ale wymaga wiedzy)

#### Rekomendacja
**Pakiet startowy**: 6 czujników powietrza + 2 rdzeniowe + 1 gateway = **~8000-10000 zł**
**Pakiet pełny** (audytowo): 10 powietrza + 4 rdzeniowe + 2 gateway = **~16000-20000 zł**

---

## MONTAŻ

### Krok 1: Mapa punktów pomiarowych w chłodni
```
Plan chłodni z góry (przykład 10m × 5m):
                                                       
   +-----+-----+-----+-----+-----+
   | A1  |     | A2  |     | A3  |   ← wieża 1 (góra)
   +-----+-----+-----+-----+-----+
   |     |     |     |     |     |
   +-----+-----+-----+-----+-----+
   | B1  |     | B2  |     | B3  |   ← środek
   +-----+-----+-----+-----+-----+
   |     |     |     |     |     |
   +-----+-----+-----+-----+-----+
   | C1  |     | C2  |     | C3  |   ← dół
   +-----+-----+-----+-----+-----+
        ↑                     ↑
     wlot powietrza    wylot/wentylator
```

### Krok 2: Montaż czujników powietrza
- **Wysokość**: 3 poziomy (góra/środek/dół) — nierównomierność pionowa to typowy problem
- **NIE** mocuj na ścianie zimnej (mierzysz ścianę nie powietrze) — użyj wysięgnika
- Czujnik 30-50 cm od ściany
- Z dala od dyszy wentylatora (50+ cm) — inaczej local cold spot

### Krok 3: Czujniki rdzeniowe (workflow)
- **PRZED** wejściem partii w chłodnię operator wbija sondę w 1-2 tuszki testowe (środek piersi, ~5cm głęboko)
- Tuszki testowe **odpowiednio oznakowane** (taśma kolorowa)
- Wędrują przez chłodnię z resztą partii
- Bezprzewodowo logują temperaturę co 1 min
- **PO** wyjściu operator usuwa sondę

### Krok 4: Kalibracja
- Raz na 6 mies. weryfikuj z certyfikowanym termometrem Fluke
- Zerowanie wszystkich czujników w lodzie (0°C ± 0.1)
- Zapisz protokół kalibracji (audyt BRC wymaga)

---

## DATABASE SCHEMA

```sql
-- LibraNet
CREATE TABLE ChillingProbe (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Kod NVARCHAR(20) NOT NULL UNIQUE,  -- 'A1', 'A2', ... 'C3', 'CORE_1', 'CORE_2'
    Typ NVARCHAR(20) NOT NULL,  -- 'AIR', 'CORE', 'HUMIDITY'
    PozycjaOpis NVARCHAR(200) NULL,
    PoziomXYZ NVARCHAR(50) NULL,  -- 'GORA/LEWO', 'SRODEK/CENTRUM'
    KalibracjaData DATE NULL,
    Aktywny BIT NOT NULL DEFAULT 1
);

CREATE TABLE ChillingMeasurement (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    ProbeId INT NOT NULL FOREIGN KEY REFERENCES ChillingProbe(Id),
    PomiarDateTime DATETIME NOT NULL,
    Wartosc DECIMAL(6,2) NOT NULL,  -- temp °C lub wilgotność %
    PartiaId INT NULL  -- powiązanie z partią (dla CORE probes)
);
CREATE INDEX IX_ChillingMeas_Probe_DateTime ON ChillingMeasurement(ProbeId, PomiarDateTime);
CREATE INDEX IX_ChillingMeas_Partia ON ChillingMeasurement(PartiaId) WHERE PartiaId IS NOT NULL;

CREATE TABLE ChillingSession (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    PartiaId INT NOT NULL,
    StartDateTime DATETIME NOT NULL,
    EndDateTime DATETIME NULL,
    StartTemp DECIMAL(5,2) NULL,
    EndTemp DECIMAL(5,2) NULL,
    DurationMin INT NULL,
    StatusFinal NVARCHAR(20) NULL,  -- 'OK', 'POZA_NORMA', 'BLAD'
    UwagiOperatora NVARCHAR(500) NULL
);

CREATE TABLE ChillingNormy (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    KategoriaTuszki NVARCHAR(30) NOT NULL,
    MaxCzasH DECIMAL(4,2) NOT NULL DEFAULT 6.0,
    MaxTempEnd DECIMAL(5,2) NOT NULL DEFAULT 4.0,
    OpisUwagi NVARCHAR(500) NULL
);

INSERT INTO ChillingNormy VALUES
('TUSZKA_CALA', 6.0, 4.0, 'Standardowa tuszka 1.5-2kg'),
('PIERS', 4.0, 4.0, 'Same piersi, szybsza krzywa'),
('SKRZYDLA', 3.0, 4.0, 'Same skrzydła'),
('PORCJONOWANE', 3.0, 4.0, 'Porcjowane przed chłodzeniem');
```

---

## KOD C# — Serwis monitoringu

**Plik**: `Produkcja/Services/ChillingMonitorService.cs`

```csharp
public class ChillingMonitorService
{
    private readonly string _connString;

    public async Task<List<ChillingSnapshot>> GetCurrentSnapshotAsync()
    {
        // Pobierz ostatnie pomiary wszystkich aktywnych sond
        const string sql = @"
            SELECT p.Id, p.Kod, p.Typ, p.PozycjaOpis,
                   m.PomiarDateTime, m.Wartosc, m.PartiaId
            FROM ChillingProbe p
            OUTER APPLY (
                SELECT TOP 1 PomiarDateTime, Wartosc, PartiaId
                FROM ChillingMeasurement
                WHERE ProbeId = p.Id
                ORDER BY PomiarDateTime DESC
            ) m
            WHERE p.Aktywny = 1";
        // ...
    }

    public async Task<ChillingCurve> GetCurveForPartiaAsync(int partiaId)
    {
        const string sql = @"
            SELECT m.PomiarDateTime, AVG(m.Wartosc) AS AvgCore
            FROM ChillingMeasurement m
            JOIN ChillingProbe p ON p.Id = m.ProbeId
            WHERE p.Typ = 'CORE' 
              AND m.PartiaId = @PartiaId
            GROUP BY m.PomiarDateTime
            ORDER BY m.PomiarDateTime";

        // Build curve points
        // Compare to ideal curve
        // Calculate deviations
        // ...
    }

    public AnalysisResult AnalyzeCurve(List<CurvePoint> actual, CurvePoint ideal)
    {
        var result = new AnalysisResult();
        
        // Sprawdzenie czasu całkowitego
        var totalMinutes = (actual.Last().DateTime - actual.First().DateTime).TotalMinutes;
        if (totalMinutes > 360)  // 6h
            result.Issues.Add($"Czas chłodzenia przekroczony: {totalMinutes/60:F1}h > 6h");
        
        // Sprawdzenie temp końcowej
        if (actual.Last().Temp > 4.0)
            result.Issues.Add($"Temp końcowa za wysoka: {actual.Last().Temp:F1}°C > 4°C");
        
        // Sprawdzenie krzywej w 4 checkpointach (25%, 50%, 75%, 100%)
        for (int q = 1; q <= 4; q++)
        {
            var idealAtQ = 40 / Math.Pow(2, q);  // 20, 10, 5, 2.5
            var pointAtQ = InterpolateAt(actual, q * 0.25);
            var deviation = pointAtQ - idealAtQ;
            if (Math.Abs(deviation) > 3)
                result.Issues.Add($"Odchylenie w {q*25}%: temp={pointAtQ:F1}°C, ideał={idealAtQ:F1}°C");
        }
        
        // Sprawdzenie nagłych skoków
        for (int i = 1; i < actual.Count; i++)
        {
            var deltaC = actual[i].Temp - actual[i-1].Temp;
            var deltaMin = (actual[i].DateTime - actual[i-1].DateTime).TotalMinutes;
            if (deltaMin > 0 && Math.Abs(deltaC/deltaMin) > 1.5)  // skok >1.5°C/min
                result.Issues.Add($"Nagły skok temp o {deltaC:F1}°C w {deltaMin:F0} min - awaria?");
        }
        
        return result;
    }
}

public class ChillingCurve
{
    public int PartiaId { get; set; }
    public List<CurvePoint> ActualPoints { get; set; } = new();
    public List<CurvePoint> IdealPoints { get; set; } = new();
    public AnalysisResult Analysis { get; set; } = new();
}

public class CurvePoint
{
    public DateTime DateTime { get; set; }
    public double Temp { get; set; }
}

public class AnalysisResult
{
    public List<string> Issues { get; set; } = new();
    public bool IsCompliant => !Issues.Any();
}
```

---

## UI — Widok krzywej

**Plik**: `Produkcja/Views/ChillingCurveWidok.xaml`

```xml
<UserControl xmlns:lvc="clr-namespace:LiveCharts.Wpf;assembly=LiveCharts.Wpf">
    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>

        <!-- Header z wyborem partii -->
        <StackPanel Grid.Row="0" Orientation="Horizontal">
            <ComboBox ItemsSource="{Binding Partie}" 
                      SelectedItem="{Binding WybranaPartia}"
                      DisplayMemberPath="NazwaWyswietlana"
                      Width="300"/>
            <Button Content="Załaduj krzywą" Click="Zaladuj_Click" Margin="8,0"/>
            <Button Content="Eksport PDF" Click="EksportPDF_Click" Margin="8,0"/>
        </StackPanel>

        <!-- Główny wykres krzywych: ideal vs actual -->
        <lvc:CartesianChart Grid.Row="1" Series="{Binding Series}" LegendLocation="Top">
            <lvc:CartesianChart.AxisX>
                <lvc:Axis Title="Czas (minuty)" MinValue="0"/>
            </lvc:CartesianChart.AxisX>
            <lvc:CartesianChart.AxisY>
                <lvc:Axis Title="Temperatura (°C)" MinValue="0" MaxValue="42">
                    <lvc:Axis.Sections>
                        <lvc:AxisSection Value="4" SectionWidth="0" 
                                         Stroke="Red" StrokeThickness="2"
                                         StrokeDashArray="4"
                                         Label="LIMIT 4°C"/>
                    </lvc:Axis.Sections>
                </lvc:Axis>
            </lvc:CartesianChart.AxisY>
        </lvc:CartesianChart>

        <!-- Wyniki analizy -->
        <Border Grid.Row="2" Background="{Binding StatusBackground}" Padding="12">
            <StackPanel>
                <TextBlock Text="{Binding StatusText}" FontWeight="Bold" FontSize="16"/>
                <ItemsControl ItemsSource="{Binding Issues}">
                    <ItemsControl.ItemTemplate>
                        <DataTemplate>
                            <TextBlock Text="{Binding}" Foreground="DarkRed"/>
                        </DataTemplate>
                    </ItemsControl.ItemTemplate>
                </ItemsControl>
            </StackPanel>
        </Border>
    </Grid>
</UserControl>
```

### Code-behind
```csharp
public partial class ChillingCurveWidok : UserControl
{
    private readonly ChillingMonitorService _service = new();

    public async void Zaladuj_Click(object sender, RoutedEventArgs e)
    {
        var partia = vm.WybranaPartia;
        var curve = await _service.GetCurveForPartiaAsync(partia.Id);
        
        // Buduj wykres
        var actualValues = new ChartValues<ObservablePoint>();
        var minutes = 0.0;
        foreach (var pt in curve.ActualPoints)
        {
            actualValues.Add(new ObservablePoint(minutes, pt.Temp));
            minutes = (pt.DateTime - curve.ActualPoints[0].DateTime).TotalMinutes;
        }

        var idealValues = new ChartValues<ObservablePoint>();
        var totalMin = (curve.ActualPoints.Last().DateTime - curve.ActualPoints[0].DateTime).TotalMinutes;
        for (int q = 0; q <= 100; q += 5)
        {
            var t = totalMin * q / 100.0;
            var pct = q / 100.0;
            // Ideal: temp halves per quarter
            var ideal = 40 * Math.Pow(0.5, pct * 4);
            idealValues.Add(new ObservablePoint(t, ideal));
        }

        vm.Series = new SeriesCollection
        {
            new LineSeries 
            { 
                Title = "Rzeczywista", 
                Values = actualValues, 
                Stroke = Brushes.Blue, 
                Fill = Brushes.Transparent,
                PointGeometry = DefaultGeometries.Circle,
                PointGeometrySize = 5
            },
            new LineSeries 
            { 
                Title = "Idealna (PDF Broiler)", 
                Values = idealValues, 
                Stroke = Brushes.Green,
                StrokeDashArray = new DoubleCollection { 5, 3 },
                Fill = Brushes.Transparent,
                PointGeometry = null
            }
        };
        
        vm.Issues = new ObservableCollection<string>(curve.Analysis.Issues);
        vm.StatusText = curve.Analysis.IsCompliant ? "✓ KRZYWA POPRAWNA" : "⚠ NIEPRAWIDŁOWOŚCI";
        vm.StatusBackground = curve.Analysis.IsCompliant 
            ? Brushes.LightGreen 
            : Brushes.LightYellow;
    }
}
```

---

## DASHBOARD LIVE — Stan aktualny chłodni

```
┌──────────────────────────────────────────────────────────┐
│  ❄ CHŁODNIA 1 — STAN LIVE  14:23                         │
├──────────────────────────────────────────────────────────┤
│                                                          │
│  WIEŻA GÓRA:    A1: 2.8°C  A2: 2.5°C  A3: 3.1°C ⚠       │
│  WIEŻA ŚRODEK:  B1: 1.8°C  B2: 2.0°C  B3: 2.4°C         │
│  WIEŻA DÓŁ:     C1: 1.5°C  C2: 1.6°C  C3: 1.9°C         │
│                                                          │
│  ŚREDNIA:       2.2°C (NORMA <4°C ✓)                    │
│  WILGOTNOŚĆ:    82% RH (NORMA 80-90% ✓)                 │
│                                                          │
│  CORE PROBES (tuszki testowe):                          │
│  Partia #1247 (Kowalski):  Sonda A: 4.2°C  Sonda B: 4.8°C │
│  Wszedł 12:30 (105 min temu)  ETA <4°C: ~25 min         │
│                                                          │
│  ⚠ ALERT: Wieża górna A3 lekko podwyższona, możliwy     │
│           lokalny problem z izolacją lub wylotem powietrza │
└──────────────────────────────────────────────────────────┘
```

---

## RAPORTY HISTORYCZNE

### Raport 1: Per partia
- Krzywa actual vs ideal
- Lista odchyleń
- Status compliance
- Eksport PDF dla audytu BRC

### Raport 2: Per dzień
- Wszystkie partie dnia
- Czy któraś przekroczyła czas?
- Średnia drip loss (jeśli mierzona) — korelacja z odchyleniami krzywej

### Raport 3: Per miesiąc
- Trend incydentów (ile krzywych poza normą)
- Heatmap: które wieże / pozycje są problemowe
- Sugestie remontowe (np. "A3 ma chronicznie problem — sprawdź izolację")

---

## INTEGRACJA Z #19 (Cold Chain HACCP)
- Te same sondy obsługują OBYDWA punkty (chłodzenie + magazyn po chłodzeniu)
- Wspólna tabela `CCP_Pomiar` → różne `PunktCCP` (chłodzenie vs storage)
- Wspólny dashboard "Cold Chain"

## CZAS IMPLEMENTACJI

| Etap | Czas | Koszt |
|---|---|---|
| Hardware: 6 czujników powietrza + 2 rdzeniowe + gateway | — | 8-10 tys zł |
| Montaż | 2 dni elektryka | 1500-2000 zł |
| Software: serwis + UI + raporty | 32-40h | — |
| Integracja z #19 | 8h | — |
| Testy + kalibracja | 1 tydzień | — |
| **RAZEM** | **~5-6 dni** | **~10-12 tys zł** |
