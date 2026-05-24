# 9. ⭐ Scalding Temperature Monitor — pełny poradnik

## Co to jest i po co
**Scalding** = parzenie tuszek w wannie z gorącą wodą po ogłuszeniu i wykrwawieniu, żeby rozluźnić pióra przed mechanicznym skubaniem.

**Dwa typy wg PDF Broiler Meat Signals (str. 98-105)**:
- **Low-temperature scalding** (50-52°C, ~3 min): zachowuje epidermis → skóra jasna, ładna, ale **wymaga** parzelnika z bardzo równym profilem temp
- **High-temperature scalding** (60-62°C, ~45 sek): zdejmuje epidermis → ciemniejsza skóra, łatwiejsze skubanie, krótsza, ale **wyższa absorpcja wody** (chłodzenie wodne, więcej bakterii)

**Problem dziś**:
- Operator ustawia raz, temperatura dryfuje (parownik się rozregulowuje, doprowadzenie wody za zimne)
- Za niska temp → niedoskubane → uszkodzona skóra przy mechanicznym skubaniu
- Za wysoka temp → ciemne tuszki, "parchment-like" plamy, klient odrzuca, **utrata 5-20% wartości**

## Wartość biznesowa
- Jeden incydent 4h pracy z złą temperaturą = **~3 partie × 50-100k = 150-300k PLN** strat
- Średnio 1-2 takie incydenty/rok = **150-600k PLN/rok zaoszczędzone**
- + jakość produktu spójna = mniej reklamacji
- + dokumentacja HACCP automatyczna (BRC v9 wymaga)

---

## CZĘŚĆ A: HARDWARE — co kupić i gdzie

### Opcja A1 (POLECANA): Czujnik PT1000 + konwerter Modbus + WiFi gateway

**Komponenty na 1 punkt pomiarowy (parzelnik):**

| Komponent | Producent | Model | Cena | Gdzie kupić |
|---|---|---|---|---|
| Czujnik PT1000 zanurzeniowy | Czaki Thermo | TP-211 (sonda 200mm, klasa A, gwint G1/2") | 250-350 zł | tme.eu, atest.com.pl |
| Konwerter PT→Modbus RTU | Elsotec | ELT-2C lub równoważny (4 kanały) | 400-600 zł | tme.eu |
| Konwerter Modbus RTU → TCP/IP | USR-IOT | USR-TCP232-410S | 250-400 zł | botland.com.pl |
| Zasilacz 24V DIN | Mean Well | DR-15-24 | 100 zł | tme.eu |
| Obudowa IP65 + dławiki | Spelsberg | TK PS 1813-12 | 80-150 zł | hurtownie elektryczne |
| Kabel ekranowany 2x0.5mm² (LiYCY) | dowolny | 25m | 100 zł | hurtownia |
| Kabel ethernet S/FTP CAT6 (50m) | dowolny | | 80 zł | hurtownia |
| Drobne (uszczelki, dławiki, koryta) | | | 200 zł | |

**Suma na 1 punkt: ~1300-2000 zł**

**Plan**: 4 punkty pomiarowe (wejście parzelnika, środek 1/3, środek 2/3, wyjście) = **~6-8 tys zł sprzętu**

### Opcja A2 (PROSTA, gorsza): Termometr WiFi z zewnętrzną sondą

| Komponent | Cena |
|---|---|
| Elitech RC-5+ WiFi z sondą zewnętrzną | 350-450 zł/szt |
| 4 sztuki | ~1500 zł |

**Wady opcji A2**:
- Ograniczona dokładność (±0.5°C zamiast ±0.1°C)
- Brak ekranowania - zakłócenia przy silnikach
- Bateria - musisz wymieniać co 6-12 mies.
- Nie certyfikowane dla HACCP w niektórych audytach
- Trudniejsza integracja z C# (WiFi REST API zamiast bezpośredni Modbus)

### Opcja A3 (NAJLEPSZA dla nowej inwestycji): IFM TN-7530 lub Endress+Hauser

Czujniki przemysłowe z certyfikatem FDA/EHEDG (dla branży mięsnej):
- IFM TN7530: ~1500 zł/szt (4 = 6000 zł), bezpośrednio IO-Link → Modbus
- Endress+Hauser TM412: ~2500 zł/szt, top quality, 10 lat życia

**Kiedy A3**: jak planujesz remont parzelnika i tak.

---

## CZĘŚĆ B: INSTALACJA — krok po kroku

### Krok 1: Wybór punktów pomiarowych
```
            Wejście tuszek →
   ┌─────────────────────────────────────────┐
   │ ●─────────●─────────────●─────────●     │  ← parzelnik
   │ A         B             C         D     │
   └─────────────────────────────────────────┘
            Wyjście tuszek →
```
- **A**: Wejście (gdzie tuszka po raz pierwszy styka się z wodą)
- **B**: 1/3 długości
- **C**: 2/3 długości
- **D**: Wyjście (najczęściej najzimniejszy punkt!)

### Krok 2: Montaż czujników mechaniczny
1. **WYŁĄCZ parzelnik** + LOTO (Lock Out Tag Out — wpis do księgi BHP)
2. **Opróżnij wodę** i odczekaj 30 min aż ostygnie do <40°C
3. W boku parzelnika nawierć otwory M16 — **potrzebujesz mechanika z uprawnieniami** (boki parzelnika to stal nierdzewna 2-3mm, wiertło HSS-Co + olej)
4. Wkręć gwint redukcyjny G1/2" → M16 z uszczelką
5. **Uszczelnij** taśmą teflonową (PTFE) + uszczelka EPDM
6. Wsuń sondę PT1000 tak, żeby końcówka była w **środku** strumienia wody, nie na ściance (sonda 200mm to standard)
7. Sprawdź szczelność po napełnieniu

⚠️ **WAŻNE**:
- Sondy muszą mieć obudowę **IP67 lub wyższą** (stała wilgoć)
- Sondy klasy A (dokładność ±0.15°C) — klasa B (±0.3°C) za mała
- **NIE** używaj sond na klipsa zewnętrznego - mierzą temp ścianki, nie wody
- **NIE** stosuj sond zbyt długich — interferują z linią transportu tuszek

### Krok 3: Okablowanie elektryczne

```
[Sonda PT1000 #A] ──LiYCY 2x0.5──┐
[Sonda PT1000 #B] ──LiYCY 2x0.5──┤
[Sonda PT1000 #C] ──LiYCY 2x0.5──┤
[Sonda PT1000 #D] ──LiYCY 2x0.5──┤
                                  ▼
                          [Konwerter ELT-2C, 4 kanały]
                                  │
                                  │ Modbus RTU (RS485, 9600 baud)
                                  ▼
                          [USR-TCP232-410S]
                                  │
                                  │ Ethernet S/FTP CAT6
                                  ▼
                       [Switch sieciowy fabryczny]
                                  │
                                  ▼
                          [Serwer ZPSP / aplikacja WPF]
```

**Zasady kabelowania (CRITICAL)**:
- **Ekran** kabla LiYCY uziem **z jednej strony** (przy konwerterze, NIE przy sondzie) — inaczej pętla masy, zakłócenia
- Trasa kabli **z dala od silników skubarki** (min 30cm)
- Jeśli musisz krzyżować silnik - **krzyżuj pod 90°**
- Kable w korytkach metalowych - dodatkowy ekran
- **Długość kabla od sondy do konwertera: max 50m** (powyżej zauważalny spadek napięcia)
- Zasilacz 24V w osobnym przedziale szafy (separacja od sygnału)

### Krok 4: Konfiguracja Modbus RTU/TCP

**Konwerter ELT-2C** (typowa konfiguracja fabryczna):
- Adres Modbus: 1
- Baudrate: 9600
- Format: 8N1 (8 bit, no parity, 1 stop)
- Rejestry holding: 40001 (kanał 1), 40002 (kanał 2), 40003 (kanał 3), 40004 (kanał 4)
- Format danych: int16, skala ×10 (np. 525 = 52.5°C)

**USR-TCP232-410S** (panel web):
1. Podłącz komputer kablem Ethernet bezpośrednio
2. Komputer IP: 192.168.0.7 (domyślnie urządzenie 192.168.0.7)
3. Otwórz http://192.168.0.7
4. Ustaw IP statyczne **192.168.0.150** (uzgodnij z administratorem sieci)
5. Tryb pracy: **TCP Server**
6. Port: **502** (standard Modbus TCP)
7. UART baudrate: **9600**
8. UART format: **8N1**
9. Save + Reboot

### Krok 5: Test komunikacji
**Tool**: QModMaster (darmowy, https://sourceforge.net/projects/qmodmaster/)
1. Connect → Modbus TCP
2. Host: 192.168.0.150, Port: 502
3. Slave ID: 1
4. Function: 03 (Read Holding Registers)
5. Start Address: 0 (jeśli zero-based) lub 1 (jeśli 1-based — sprawdź dokumentację)
6. Quantity: 4
7. Send

Powinieneś zobaczyć 4 wartości typu 525, 524, 523, 522 = 52.5, 52.4, 52.3, 52.2°C.

---

## CZĘŚĆ C: SOFTWARE — kod C# WPF

### Pakiet NuGet
```xml
<PackageReference Include="NModbus" Version="3.0.81" />
<PackageReference Include="Microsoft.Extensions.Hosting" Version="8.0.0" />
```

### Serwis odczytu temperatury
**Plik**: `Produkcja/Services/ScaldingMonitorService.cs`

```csharp
using NModbus;
using System.Net.Sockets;
using System.Data.SqlClient;

namespace Kalendarz1.Produkcja.Services;

public class ScaldingMonitorService : IDisposable
{
    private const string CONN_LIBRA = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=...;TrustServerCertificate=true";

    private readonly string _modbusIp;
    private readonly int _modbusPort;
    private readonly byte _slaveId;

    private TcpClient? _client;
    private IModbusMaster? _master;

    public ScaldingMonitorService(string ip = "192.168.0.150", int port = 502, byte slaveId = 1)
    {
        _modbusIp = ip;
        _modbusPort = port;
        _slaveId = slaveId;
    }

    public async Task ConnectAsync()
    {
        _client = new TcpClient();
        await _client.ConnectAsync(_modbusIp, _modbusPort);
        var factory = new ModbusFactory();
        _master = factory.CreateMaster(_client);
    }

    /// Odczyt temp z PT1000 przez ELT-2C.
    /// Konwerter zwraca int16 ze skalą x10 (525 = 52.5°C).
    /// Adresy rejestrów typowo 0-3 dla 4 kanałów.
    public async Task<double[]> ReadTemperaturesAsync(ushort startReg = 0, ushort count = 4)
    {
        if (_master is null) await ConnectAsync();
        var raw = await _master!.ReadHoldingRegistersAsync(_slaveId, startReg, count);
        return raw.Select(r => (short)r / 10.0).ToArray();
    }

    public async Task LogToDatabase(double[] temps, int? partiaId = null)
    {
        using var cn = new SqlConnection(CONN_LIBRA);
        await cn.OpenAsync();
        const string sql = @"
            INSERT INTO ScaldingTempLog (PomiarDateTime, PartiaId, TempA, TempB, TempC, TempD)
            VALUES (@dt, @pid, @a, @b, @c, @d)";
        using var cmd = new SqlCommand(sql, cn);
        cmd.Parameters.AddWithValue("@dt", DateTime.Now);
        cmd.Parameters.AddWithValue("@pid", (object?)partiaId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@a", temps[0]);
        cmd.Parameters.AddWithValue("@b", temps[1]);
        cmd.Parameters.AddWithValue("@c", temps[2]);
        cmd.Parameters.AddWithValue("@d", temps[3]);
        await cmd.ExecuteNonQueryAsync();
    }

    public void Dispose()
    {
        _master?.Dispose();
        _client?.Dispose();
    }
}
```

### Tabele bazy
```sql
-- LibraNet
CREATE TABLE ScaldingTempLog (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    PomiarDateTime DATETIME NOT NULL,
    PartiaId INT NULL,
    TempA DECIMAL(5,2) NULL,
    TempB DECIMAL(5,2) NULL,
    TempC DECIMAL(5,2) NULL,
    TempD DECIMAL(5,2) NULL
);
CREATE INDEX IX_ScaldingTempLog_DateTime ON ScaldingTempLog(PomiarDateTime);
CREATE INDEX IX_ScaldingTempLog_Partia ON ScaldingTempLog(PartiaId) WHERE PartiaId IS NOT NULL;

CREATE TABLE ScaldingNormy (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    TypScaldingu NVARCHAR(20) NOT NULL,
    TempMin DECIMAL(5,2) NOT NULL,
    TempMax DECIMAL(5,2) NOT NULL,
    OpisUwagi NVARCHAR(500) NULL
);

INSERT INTO ScaldingNormy VALUES
('LOW_TEMP', 50.0, 52.5, 'Zachowuje epidermis - jasna skóra'),
('HIGH_TEMP', 59.0, 62.5, 'Zdejmuje epidermis - latwiejsze skubanie');

CREATE TABLE ScaldingAlerts (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    AlertDateTime DATETIME NOT NULL,
    TypAlertu NVARCHAR(30) NOT NULL,
    TempZmierzona DECIMAL(5,2) NULL,
    TempOczekiwana DECIMAL(5,2) NULL,
    PunktPomiaru CHAR(1) NULL,
    PartiaId INT NULL,
    PotwierdzonyPrzez NVARCHAR(50) NULL,
    PotwierdzonyDateTime DATETIME NULL,
    PrzyczynaPodana NVARCHAR(500) NULL
);
```

### Worker w tle (co 30 sek)
**Plik**: `Produkcja/Services/ScaldingBackgroundWorker.cs`

```csharp
public class ScaldingBackgroundWorker : BackgroundService
{
    private readonly ScaldingMonitorService _monitor = new();
    private const double TEMP_MIN_ABSOLUTE = 49.0;
    private const double TEMP_MAX_ABSOLUTE = 63.0;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await _monitor.ConnectAsync();
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var temps = await _monitor.ReadTemperaturesAsync();
                var currentPartia = await GetCurrentProductionPartiaIdAsync();
                await _monitor.LogToDatabase(temps, currentPartia);

                for (int i = 0; i < temps.Length; i++)
                {
                    char punkt = (char)('A' + i);
                    if (temps[i] < TEMP_MIN_ABSOLUTE || temps[i] > TEMP_MAX_ABSOLUTE)
                    {
                        await SaveAlert(punkt, temps[i], currentPartia);
                        BroadcastAlert($"Temperatura punktu {punkt}: {temps[i]:F1}°C poza normą!");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Scalding monitor failure");
                // backoff + reconnect
                await Task.Delay(TimeSpan.FromSeconds(5), ct);
                try { await _monitor.ConnectAsync(); } catch { }
            }
            await Task.Delay(TimeSpan.FromSeconds(30), ct);
        }
    }

    // ...GetCurrentProductionPartiaIdAsync, SaveAlert, BroadcastAlert
}
```

### UI Widget w ProdukcjaDzisWidok
**Plik**: `Produkcja/Views/ScaldingWidget.xaml`

```xml
<UserControl x:Class="Kalendarz1.Produkcja.Views.ScaldingWidget"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:lvc="clr-namespace:LiveCharts.Wpf;assembly=LiveCharts.Wpf">
    <Border CornerRadius="8" Background="#F8FAFC" Padding="16">
        <Grid>
            <Grid.RowDefinitions>
                <RowDefinition Height="Auto"/>
                <RowDefinition Height="*"/>
                <RowDefinition Height="Auto"/>
            </Grid.RowDefinitions>

            <TextBlock Grid.Row="0" FontWeight="Bold" FontSize="16">
                🔥 PARZELNIK
                <Run Text="{Binding StatusGlobal}" Foreground="{Binding StatusColor}"/>
            </TextBlock>

            <UniformGrid Grid.Row="1" Rows="1" Columns="4" Margin="0,12">
                <Border Background="{Binding TempAColor}" CornerRadius="6" Margin="4" Padding="8">
                    <StackPanel>
                        <TextBlock Text="A (wejście)" FontSize="10" HorizontalAlignment="Center"/>
                        <TextBlock Text="{Binding TempA, StringFormat={}{0:F1}°C}"
                                   FontSize="24" FontWeight="Bold" HorizontalAlignment="Center"/>
                    </StackPanel>
                </Border>
                <Border Background="{Binding TempBColor}" CornerRadius="6" Margin="4" Padding="8">
                    <StackPanel>
                        <TextBlock Text="B (1/3)" FontSize="10" HorizontalAlignment="Center"/>
                        <TextBlock Text="{Binding TempB, StringFormat={}{0:F1}°C}"
                                   FontSize="24" FontWeight="Bold" HorizontalAlignment="Center"/>
                    </StackPanel>
                </Border>
                <Border Background="{Binding TempCColor}" CornerRadius="6" Margin="4" Padding="8">
                    <StackPanel>
                        <TextBlock Text="C (2/3)" FontSize="10" HorizontalAlignment="Center"/>
                        <TextBlock Text="{Binding TempC, StringFormat={}{0:F1}°C}"
                                   FontSize="24" FontWeight="Bold" HorizontalAlignment="Center"/>
                    </StackPanel>
                </Border>
                <Border Background="{Binding TempDColor}" CornerRadius="6" Margin="4" Padding="8">
                    <StackPanel>
                        <TextBlock Text="D (wyjście)" FontSize="10" HorizontalAlignment="Center"/>
                        <TextBlock Text="{Binding TempD, StringFormat={}{0:F1}°C}"
                                   FontSize="24" FontWeight="Bold" HorizontalAlignment="Center"/>
                    </StackPanel>
                </Border>
            </UniformGrid>

            <!-- Mini-wykres ostatnich 60 minut, 4 linie -->
            <lvc:CartesianChart Grid.Row="2" Height="100" Series="{Binding Series60min}">
                <lvc:CartesianChart.AxisY>
                    <lvc:Axis MinValue="48" MaxValue="64" Title="°C"/>
                </lvc:CartesianChart.AxisY>
            </lvc:CartesianChart>
        </Grid>
    </Border>
</UserControl>
```

---

## CZĘŚĆ D: HACCP i dokumentacja

### Auto-raport dzienny PDF
- Wygenerowany o 22:00 automatycznie
- Zawiera: min/max/avg per godzina dla każdego z 4 punktów
- Lista alarmów + przyczyn (operator wpisuje "awaria pompy 14:00-14:30")
- Wykres temperatury cały dzień
- Pieczątka cyfrowa BRC compliance

### Eksport CSV dla auditora BRC
- One-click w widoku ScaldingHistory
- Range dat configurable
- Format zgodny z BRC v9 issue 9 sekcja 4.7

### Tabela operatorów z dostępem do ustawień
- Tylko mistrz produkcji + brygadzista mogą zmienić wartości progowe
- Każda zmiana zapisana w `AuditLog` z user/timestamp

---

## CZĘŚĆ E: Czas implementacji + budżet

| Pozycja | Czas | Koszt |
|---|---|---|
| Hardware (4 czujniki PT1000 + konwertery + obudowa) | — | **7000-9000 zł** |
| Mechanik (wiercenie, montaż sond) | 0.5 dnia | 800-1200 zł |
| Elektryk (kable, szafa, konwertery) | 1 dzień | 1500-2000 zł |
| Software (kod C# WPF, baza, UI) | 16-24h pracy programisty | — (robisz sam) |
| Testy + kalibracja | 8h | — |
| **RAZEM** | **~3 dni** | **~10-12 tys zł** |

### ROI
- Zwrot inwestycji po **1 incydencie zaoszczędzonym**
- Średnio 1-2 incydenty/rok = **ROI w 6-12 mies.**

---

## CZĘŚĆ F: Ryzyka i gotchas

⚠️ **Sondy w parze 50°C+ mają krótszy żywot (3-5 lat)**, planuj wymianę co 4 lata
⚠️ **Wkurzenie operatora** "patrzysz mi na ręce" — sprzedaj jako "system ostrzega Was, nie kontroluje"
⚠️ **Awaria sieci ethernet = brak danych** → dorzuć **lokalny buffer SQLite** w aplikacji, synchronizacja po reconnect
⚠️ **Kalibracja**: raz na 6 mies. sprawdź sondy z certyfikowanym termometrem referencyjnym (np. Fluke 1551A — wypożyczalnia ~200 zł)
⚠️ **Mycie CIP** (Clean In Place) parzelnika z gorącym ługiem — czujniki muszą być EHEDG-compliant (Hygienic Equipment Design Group)
⚠️ **Awaria czujnika** = pokaże nierealne wartości (np. -50°C lub 999°C) → dodaj walidację: wartości <0 lub >100 ignoruj, sygnalizuj alert "Czujnik X uszkodzony"
