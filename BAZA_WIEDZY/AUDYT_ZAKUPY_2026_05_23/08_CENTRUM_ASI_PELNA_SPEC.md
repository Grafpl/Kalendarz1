# Część 8 (rozszerzenie) — Pełna spec techniczna Centrum Asi

> Rozszerzenie Części 3 — szczegóły implementacyjne dla Sera. Effort: 3-4 dni roboczych.

---

## 0. Cel + zasada

**Cel:** jedno okno otwierane rano + trzymane "na boku" cały dzień. Asia widzi 5 sekcji bez konieczności klikania w 6 innych kafelków.

**Zasada:** **read-mostly** (Asia patrzy, decyduje, klika 1× w wybrany temat → przekierowanie do dedykowanego okna). Centrum nie jest narzędziem do edycji — to kokpit.

---

## 1. Architektura

### 1.1 Pliki
```
Hodowcy/Centrum/
├── CentrumAsiWindow.xaml             (UI, 5 sekcji)
├── CentrumAsiWindow.xaml.cs          (code-behind, ~600 linii)
├── Models/
│   ├── CentrumAsiSnapshot.cs         (DTO agregat — wszystko co Centrum pokazuje)
│   ├── TerminItem.cs                 (sekcja Terminy)
│   ├── SkrzynkaItem.cs               (sekcja Skrzynka)
│   ├── TrendItem.cs                  (sekcja Trendy hodowców)
│   └── AuditItem.cs                  (sekcja Live audit)
├── Services/
│   ├── CentrumAsiService.cs          (orkiestracja Task.WhenAll 5 sekcji)
│   ├── TerminyDeadlineService.cs     (kontrakty wygasające + GUS)
│   ├── SkrzynkaService.cs            (wnioski + szkice + faktury Avilog)
│   ├── TrendyHodowcowEngine.cs       (engine alertów "3-i raz pod progiem")
│   └── LiveAuditPoller.cs            (re-use mechanizmu z Kalendarza)
└── Controls/
    ├── TerminyPanel.xaml             (sub-panel — sekcja Terminy)
    ├── CompliancePanel.xaml          (sub-panel — ARiMR)
    ├── SkrzynkaPanel.xaml            (sub-panel — Skrzynka)
    ├── TrendyPanel.xaml              (sub-panel — Trendy)
    └── AuditPanel.xaml               (sub-panel — Live)
```

### 1.2 Refresh strategy

| Co | Częstość | Mechanizm |
|---|---|---|
| Pełen refresh (5 sekcji) | Co 5 min lub F5 | `DispatcherTimer` |
| Live audit | Co 30 sek | `LiveAuditPoller` (re-use z `Zywiec/Kalendarz/Services/AuditLogService.cs`) |
| Compliance ARiMR | Co 15 min (drogo) | `DispatcherTimer` osobny |
| Cache TTL | 5 min | `MemoryCache` w `CentrumAsiService` |

---

## 2. CentrumAsiService — orchestrator

```csharp
// Hodowcy/Centrum/Services/CentrumAsiService.cs
using System;
using System.Threading.Tasks;
using Kalendarz1.Hodowcy.Centrum.Models;
using Kalendarz1.Kontrakty.Services;

namespace Kalendarz1.Hodowcy.Centrum.Services
{
    public class CentrumAsiService
    {
        private readonly TerminyDeadlineService _terminy = new();
        private readonly SkrzynkaService _skrzynka = new();
        private readonly TrendyHodowcowEngine _trendy = new();
        private readonly LiveAuditPoller _audit = new();
        private readonly KontraktyService _kontrakty = new();

        private DateTime _lastFullRefresh = DateTime.MinValue;
        private CentrumAsiSnapshot? _cached;
        private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

        public async Task<CentrumAsiSnapshot> GetSnapshotAsync(string userId, bool forceRefresh = false)
        {
            if (!forceRefresh && _cached != null && DateTime.Now - _lastFullRefresh < CacheTtl)
                return _cached;

            // Wszystko równolegle (4 niezależne źródła)
            var tTerminy    = _terminy.PobierzAsync(userId);
            var tSkrzynka   = _skrzynka.PobierzAsync(userId);
            var tTrendy     = _trendy.PobierzAsync();
            var tCompliance = _kontrakty.GetArimrComplianceAsync();

            await Task.WhenAll(tTerminy, tSkrzynka, tTrendy, tCompliance);

            _cached = new CentrumAsiSnapshot
            {
                WyliczonoKiedy = DateTime.Now,
                Terminy = tTerminy.Result,
                Skrzynka = tSkrzynka.Result,
                Trendy = tTrendy.Result,
                Compliance = tCompliance.Result
            };
            _lastFullRefresh = DateTime.Now;
            return _cached;
        }

        public Task<List<AuditItem>> PollNewAuditAsync(long sinceId)
            => _audit.PollAsync(sinceId);
    }
}
```

---

## 3. TerminyDeadlineService — terminy z 2 źródeł

```csharp
// Hodowcy/Centrum/Services/TerminyDeadlineService.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Kalendarz1.Hodowcy.Centrum.Models;

namespace Kalendarz1.Hodowcy.Centrum.Services
{
    public class TerminyDeadlineService
    {
        private const string ConnLibra =
            "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";

        public async Task<List<TerminItem>> PobierzAsync(string userId)
        {
            var list = new List<TerminItem>();

            // 1. GUS R09 (szkice — re-use z R09USzkice gdy wdrożone)
            list.AddRange(await PobierzGusAsync());

            // 2. Kontrakty wygasające (top 10)
            list.AddRange(await PobierzKontraktyWygasajaceAsync());

            // 3. ZSRIR (piątek bieżący tydzień)
            var dayOfWeek = (int)DateTime.Today.DayOfWeek;
            if (dayOfWeek >= 1 && dayOfWeek <= 5) // pn-pt
            {
                var dniDoPiatku = 5 - dayOfWeek;
                list.Add(new TerminItem
                {
                    Typ = "ZSRIR",
                    Tytul = $"ZSRIR — sprawozdanie tygodniowe",
                    DataDeadline = DateTime.Today.AddDays(dniDoPiatku),
                    Severity = dniDoPiatku <= 1 ? "WARN" : "INFO",
                    Akcja = "Otwórz Sprawozdania"
                });
            }

            // Sortuj rosnąco po deadline
            list.Sort((a, b) => a.DataDeadline.CompareTo(b.DataDeadline));
            return list;
        }

        private async Task<List<TerminItem>> PobierzGusAsync()
        {
            var list = new List<TerminItem>();
            // Aktualny miesiąc — deadline 8. następnego
            var teraz = DateTime.Today;
            var deadline = new DateTime(teraz.Year, teraz.Month, 1).AddMonths(1).AddDays(7);
            var dni = (deadline - teraz).Days;

            // Sprawdź czy R09 za poprzedni miesiąc już wysłany
            const string sql = @"
SELECT TOP 1 Status, GeneratedAt FROM dbo.GusSubmissions
WHERE TypFormularza = 'R-09U' AND Rok = @Rok AND Miesiac = @Miesiac
ORDER BY GeneratedAt DESC;";

            var poprzedniMiesiac = teraz.AddMonths(-1);
            using var conn = new SqlConnection(ConnLibra);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Rok", poprzedniMiesiac.Year);
            cmd.Parameters.AddWithValue("@Miesiac", poprzedniMiesiac.Month);
            using var rdr = await cmd.ExecuteReaderAsync();
            bool sent = false;
            if (await rdr.ReadAsync())
                sent = (rdr["Status"] as string) == "Sent";

            list.Add(new TerminItem
            {
                Typ = "GUS_R09",
                Tytul = $"GUS R-09U za {poprzedniMiesiac:MMMM yyyy}",
                DataDeadline = deadline,
                Severity = sent ? "OK" : (dni <= 1 ? "CRIT" : dni <= 3 ? "WARN" : "INFO"),
                Akcja = sent ? "✅ Wysłane" : "Otwórz R09",
                Status = sent ? "Wysłane" : "Do wysłania"
            });
            return list;
        }

        private async Task<List<TerminItem>> PobierzKontraktyWygasajaceAsync()
        {
            const string sql = @"
SELECT TOP 10 Id, NumerKontraktu, NazwaHodowcySnapshot, DataObowiazujeDo
FROM dbo.Kontrakty
WHERE Status IN ('ACTIVE','EXPIRING')
  AND DataObowiazujeDo IS NOT NULL
  AND DataObowiazujeDo <= DATEADD(MONTH, 6, GETDATE())
ORDER BY DataObowiazujeDo ASC;";

            var list = new List<TerminItem>();
            using var conn = new SqlConnection(ConnLibra);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(sql, conn);
            using var rdr = await cmd.ExecuteReaderAsync();
            while (await rdr.ReadAsync())
            {
                var d = (DateTime)rdr["DataObowiazujeDo"];
                var dni = (d - DateTime.Today).Days;
                list.Add(new TerminItem
                {
                    Typ = "KONTRAKT",
                    Tytul = $"Kontrakt {rdr["NumerKontraktu"]} — {rdr["NazwaHodowcySnapshot"]}",
                    DataDeadline = d,
                    Severity = dni <= 7 ? "CRIT" : dni <= 30 ? "WARN" : "INFO",
                    Akcja = "Otwórz w Kontraktach",
                    KontraktId = (int)rdr["Id"]
                });
            }
            return list;
        }
    }
}
```

---

## 4. TrendyHodowcowEngine — alert "3-i raz pod progiem"

```csharp
// Hodowcy/Centrum/Services/TrendyHodowcowEngine.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Kalendarz1.Hodowcy.Centrum.Models;

namespace Kalendarz1.Hodowcy.Centrum.Services
{
    public class TrendyHodowcowEngine
    {
        private const string ConnLibra =
            "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";

        // Konfigurowalne progi (powinno iść do tabeli AlertyKonfig w Q3)
        private const decimal ProgPadlych = 5.0m;
        private const decimal ProgRoznicyWagi = 5.0m;
        private const int LiczbaPodRzadem = 3; // ile cykli z rzędu pod progiem

        public async Task<List<TrendItem>> PobierzAsync()
        {
            var list = new List<TrendItem>();

            // Pobierz ostatnie 6 cykli per hodowca, sprawdź czy 3 ostatnie były pod progiem
            const string sql = @"
WITH OstatnieDostaw AS (
    SELECT
        Dostawca,
        CalcDate,
        CAST(LumDieAtAr * 100.0 / NULLIF(LumQnt, 0) AS DECIMAL(5,2)) AS PadlePr,
        ROW_NUMBER() OVER (PARTITION BY Dostawca ORDER BY CalcDate DESC) AS Rn
    FROM dbo.FarmerCalc
    WHERE LumQnt > 0
)
SELECT Dostawca, COUNT(*) AS LiczbaPodRzadem, MAX(PadlePr) AS Max, AVG(PadlePr) AS Avg
FROM OstatnieDostaw
WHERE Rn <= 3 AND PadlePr > @Prog
GROUP BY Dostawca
HAVING COUNT(*) >= @MinLiczba;";

            using var conn = new SqlConnection(ConnLibra);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 60 };
            cmd.Parameters.AddWithValue("@Prog", ProgPadlych);
            cmd.Parameters.AddWithValue("@MinLiczba", LiczbaPodRzadem);
            using var rdr = await cmd.ExecuteReaderAsync();
            while (await rdr.ReadAsync())
            {
                list.Add(new TrendItem
                {
                    Hodowca = rdr["Dostawca"].ToString() ?? "?",
                    Metryka = "Padłe %",
                    Severity = "WARN",
                    Komunikat = $"3-i raz z rzędu padłe > {ProgPadlych}% (max: {rdr["Max"]:F1}%, śr.: {rdr["Avg"]:F1}%)",
                    ProponowanaAkcja = "Telefon Justyny + przegląd paszy / weterynarz"
                });
            }
            return list;
        }
    }
}
```

---

## 5. XAML — CentrumAsiWindow.xaml (kluczowe fragmenty)

```xml
<Window x:Class="Kalendarz1.Hodowcy.Centrum.CentrumAsiWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="🏠 Centrum Asi — Strażnik Kontraktów"
        Height="900" Width="1400"
        WindowStartupLocation="CenterScreen"
        Background="#F5F7FA"
        Topmost="{Binding ElementName=chkOnTop, Path=IsChecked}">

    <DockPanel>
        <!-- HEADER -->
        <Border DockPanel.Dock="Top" Background="#5C8A3A" Padding="14,8">
            <Grid>
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="*"/>
                    <ColumnDefinition Width="Auto"/>
                </Grid.ColumnDefinitions>
                <TextBlock Grid.Column="0" Foreground="White" FontSize="18" FontWeight="Bold">
                    🏠 CENTRUM ASI — Strażnik Kontraktów
                </TextBlock>
                <StackPanel Grid.Column="1" Orientation="Horizontal">
                    <TextBlock x:Name="txtLastRefresh" Foreground="#E8F5E9" FontSize="11"
                               VerticalAlignment="Center" Margin="0,0,12,0"/>
                    <CheckBox x:Name="chkOnTop" Content="Always on top" Foreground="White"
                              FontSize="11" VerticalAlignment="Center" Margin="0,0,8,0"/>
                    <Button Content="🔄 F5" Padding="10,4" FontSize="11" Click="BtnRefresh_Click"/>
                    <Button Content="⛶ Mini" Padding="10,4" FontSize="11" Click="BtnMini_Click" Margin="4,0"/>
                </StackPanel>
            </Grid>
        </Border>

        <!-- BANNER POWODY -->
        <Border DockPanel.Dock="Top" Background="#E8F5E9" Padding="14,6">
            <TextBlock Foreground="#2E7D32" FontSize="11">
                💡 Otwieraj rano + trzymaj "Always on top" cały dzień.
                Pełna instrukcja: INSTRUKCJE_ASIA/02_rytm_tygodniowy.md
            </TextBlock>
        </Border>

        <!-- SCROLLABLE CONTENT (5 sekcji) -->
        <ScrollViewer VerticalScrollBarVisibility="Auto">
            <StackPanel Margin="14,12">

                <!-- SEKCJA 1: TERMINY -->
                <Border Background="White" CornerRadius="6" Padding="14" Margin="0,0,0,12">
                    <StackPanel>
                        <TextBlock Text="⏰ TERMINY (top priority)" FontSize="14" FontWeight="Bold"
                                   Foreground="#37474F" Margin="0,0,0,8"/>
                        <ItemsControl x:Name="lstTerminy">
                            <ItemsControl.ItemTemplate>
                                <DataTemplate>
                                    <Border Background="{Binding TloPoSeverity}"
                                            BorderBrush="{Binding RamkaPoSeverity}"
                                            BorderThickness="0,0,0,1" Padding="10,8" Margin="0,2">
                                        <Grid>
                                            <Grid.ColumnDefinitions>
                                                <ColumnDefinition Width="*"/>
                                                <ColumnDefinition Width="Auto"/>
                                                <ColumnDefinition Width="Auto"/>
                                            </Grid.ColumnDefinitions>
                                            <StackPanel Grid.Column="0">
                                                <TextBlock Text="{Binding Tytul}" FontWeight="SemiBold" FontSize="12"/>
                                                <TextBlock Text="{Binding Komunikat}" FontSize="11" Foreground="#607D8B"/>
                                            </StackPanel>
                                            <TextBlock Grid.Column="1" Text="{Binding DniText}" FontSize="11"
                                                       VerticalAlignment="Center" Margin="0,0,12,0"/>
                                            <Button Grid.Column="2" Content="{Binding Akcja}"
                                                    FontSize="11" Padding="8,4"
                                                    Click="BtnTerminAkcja_Click" Tag="{Binding}"/>
                                        </Grid>
                                    </Border>
                                </DataTemplate>
                            </ItemsControl.ItemTemplate>
                        </ItemsControl>
                    </StackPanel>
                </Border>

                <!-- SEKCJA 2: ARiMR COMPLIANCE -->
                <Border Background="White" CornerRadius="6" Padding="14" Margin="0,0,0,12">
                    <StackPanel>
                        <TextBlock Text="🎯 ARiMR COMPLIANCE (live)" FontSize="14" FontWeight="Bold"
                                   Foreground="#37474F" Margin="0,0,0,8"/>
                        <Grid>
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width="*"/>
                                <ColumnDefinition Width="Auto"/>
                            </Grid.ColumnDefinitions>
                            <StackPanel Grid.Column="0">
                                <ProgressBar x:Name="prgCompliance" Height="24" Minimum="0" Maximum="100"
                                             Value="0" Foreground="#4CAF50"/>
                                <TextBlock x:Name="txtComplianceLabel" Margin="0,4,0,0" FontSize="12"/>
                            </StackPanel>
                            <Button Grid.Column="1" Content="📊 Dashboard" FontSize="11" Padding="10,6"
                                    Click="BtnDashboard_Click" Margin="14,0,0,0"/>
                        </Grid>
                    </StackPanel>
                </Border>

                <!-- SEKCJA 3: SKRZYNKA ASI -->
                <Border Background="White" CornerRadius="6" Padding="14" Margin="0,0,0,12">
                    <StackPanel>
                        <TextBlock Text="📥 SKRZYNKA ASI (do akcji)" FontSize="14" FontWeight="Bold"
                                   Foreground="#37474F" Margin="0,0,0,8"/>
                        <ItemsControl x:Name="lstSkrzynka"/>
                    </StackPanel>
                </Border>

                <!-- SEKCJA 4: TRENDY HODOWCÓW -->
                <Border Background="White" CornerRadius="6" Padding="14" Margin="0,0,0,12">
                    <StackPanel>
                        <TextBlock Text="📈 TRENDY HODOWCÓW (ostatnie 4 tyg.)" FontSize="14" FontWeight="Bold"
                                   Foreground="#37474F" Margin="0,0,0,8"/>
                        <ItemsControl x:Name="lstTrendy"/>
                    </StackPanel>
                </Border>

                <!-- SEKCJA 5: LIVE AUDIT -->
                <Border Background="White" CornerRadius="6" Padding="14">
                    <StackPanel>
                        <TextBlock Text="💬 LIVE AUDIT (cross-module, ostatnie 24h)"
                                   FontSize="14" FontWeight="Bold" Foreground="#37474F" Margin="0,0,0,8"/>
                        <ItemsControl x:Name="lstAudit"/>
                    </StackPanel>
                </Border>

            </StackPanel>
        </ScrollViewer>
    </DockPanel>
</Window>
```

---

## 6. Mini-mode (Alt+M)

Gdy Asia pracuje w innym oknie, Centrum zwija się do paska:

```csharp
private void BtnMini_Click(object sender, RoutedEventArgs e)
{
    // Zmiana rozmiaru okna na pasek + ukrycie zawartości
    this.Width = 380;
    this.Height = 80;
    this.Top = SystemParameters.WorkArea.Top + 4;
    this.Left = SystemParameters.WorkArea.Right - 384;
    // pokaż tylko nagłówek + liczbę alertów
}
```

W tym trybie pokazuje tylko: `"3 terminy dziś, 2 alerty ARiMR ⚠"`.

---

## 7. Krytyczne notyfikacje przy logowaniu

W `Menu1.xaml.cs` po `LoginButton_Click` (krok 4 — po sukcesie):

```csharp
// Asia + Ser dostają alert przy logowaniu jeśli są CRIT alerty
if (App.UserID is "asia" or "ser") // → poprawić na faktyczne ID
{
    var alerts = await new KontraktyAlertService_GetCriticalAsync(App.UserID);
    if (alerts.Count > 0)
    {
        var dlg = new KrytyczneAlertyWindow(alerts) { Owner = this };
        if (dlg.ShowDialog() != true)
            return; // nie pozwól wejść do menu
    }
}
```

---

## 8. Permissions

W `Menu.cs` nowy kafelek:

```csharp
new MenuItemConfig("CentrumAsi", "Centrum Asi",
    "Kokpit strażnika kontraktów — terminy, ARiMR, skrzynka, trendy, audit",
    Color.FromArgb(45, 110, 25), // ciemny zielony
    () => new Kalendarz1.Hodowcy.Centrum.CentrumAsiWindow(), "🏠", "Centrum"),
```

W `_moduleAccessOrder` na końcu: `"CentrumAsi"`.

W `UserPermissions`:
- Asia → HasAccess = true
- Ser → HasAccess = true
- Justyna → HasAccess = true (do podglądu compliance)
- Magda → **false** w pierwszych 3 miesiącach (potem Asia może przyznać)

---

## 9. Effort breakdown

| Co | Effort |
|---|---|
| 5 DTO + Snapshot model | 2h |
| `CentrumAsiService` orchestrator | 4h |
| `TerminyDeadlineService` | 4h |
| `SkrzynkaService` | 4h |
| `TrendyHodowcowEngine` (z SQL window functions) | 6h |
| `LiveAuditPoller` (re-use z Kalendarza) | 2h |
| `CentrumAsiWindow.xaml` (5 sekcji + DataTemplates) | 6h |
| `CentrumAsiWindow.xaml.cs` (event handlery + refresh) | 4h |
| Mini-mode | 2h |
| Krytyczne notyfikacje przy logowaniu | 2h |
| Permissions + integracja z menu | 1h |
| **Razem** | **~37h = ~5 dni roboczych Sera** |

---

## 10. Roadmap

| Tydzień | Co |
|---|---|
| **Czerwiec 1-2** | DTO + 4 services + Snapshot |
| **Czerwiec 3** | XAML + bindings + DataTemplates |
| **Czerwiec 4** | LiveAuditPoller + Mini-mode |
| **Lipiec 1** | Krytyczne notyfikacje + permissions |
| **Lipiec 2** | Testy z Asią + iteracje |
| **Lipiec 3+** | Produkcja, monitoring użycia |

**Asia używa od początku lipca 2026** — wcześniej niż pełny moduł Kontrakty (Faza 1 Kontraktów = czerwiec, ale szczegółowe widoki to lipiec-sierpień).

---

*Wersja 1.0 • 24.05.2026 • Pełna spec techniczna dla Sera. Rozwinięcie Części 3.*
