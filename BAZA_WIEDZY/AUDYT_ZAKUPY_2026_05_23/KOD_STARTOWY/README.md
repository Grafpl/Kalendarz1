# Kod startowy modułu Kontrakty Hodowców

Pliki gotowe do skopiowania do projektu po uruchomieniu `SQL/01_Kontrakty_v1_schema.sql`.

## Struktura plików (jak skopiować do projektu)

```
Kalendarz1/
└── Kontrakty/                        ← NOWY folder
    ├── Models/
    │   └── KontraktDto.cs            ← 4 DTO + 1 helper
    ├── Services/
    │   ├── KontraktyService.cs       ← CRUD + numeracja + audit
    │   ├── WordTemplateService.cs    ← OpenXML bookmark replace
    │   └── KontraktyAlertService.cs  ← nocny job alertów
    └── Windows/
        ├── KontraktyListaWindow.xaml      ← główne okno (gotowe)
        ├── KontraktyListaWindow.xaml.cs   ← szkielet (CRUD + Word działa)
        ├── KontraktyEditorWindow.xaml     ← TODO Faza 1 (CRUD edycji)
        ├── KontraktyDetailsWindow.xaml    ← TODO Faza 2 (3 zakładki)
        └── DashboardArimrWindow.xaml      ← TODO Faza 3
```

## Co działa "od ręki" po skopiowaniu

✅ **`KontraktyService`** — pełne CRUD, numeracja (`sp_KontraktyNastepnyNumer`), audit log, compliance ARiMR
✅ **`WordTemplateService`** — generacja Word z szablonu + bookmarki + helper `BuildValuesFromKontrakt`
✅ **`KontraktyAlertService`** — nocny job alertów (uruchom z `Kalendarz1.exe --kontrakty-check`)
✅ **`KontraktyListaWindow`** — lista, filtry, search, generacja Worda, zmiany statusu, wypowiedzenie

## Co jest TODO (oznaczone w kodzie)

⏳ `KontraktyEditorWindow` — formularz edycji (Faza 1, ~1 dzień)
⏳ `KontraktyDetailsWindow` — szczegóły + 3 zakładki (Faza 2, ~1 dzień)
⏳ `DashboardArimrWindow` — wykres + lista hodowców do zakontraktowania (Faza 3, ~1 dzień)
⏳ `BtnDodajSkan_Click` — OpenFileDialog → kopiowanie PDF do UNC (Faza 2, ~4h)
⏳ `WyslijEmailAsync` w AlertService — Outlook interop (Faza 3, ~4h)
⏳ Dialog wypowiedzenia z powodem + datami (Faza 2, ~2h)

## Plan integracji do projektu (krok po kroku)

### Krok 1 — przygotowanie (5 min)

1. Uruchom `SQL/01_Kontrakty_v1_schema.sql` na LibraNet
2. Zweryfikuj że tabele istnieją:
   ```sql
   SELECT name FROM sys.tables WHERE name LIKE 'Kontrakty%';
   ```

### Krok 2 — skopiowanie plików do projektu (10 min)

```powershell
# Z PowerShella w katalogu Kalendarz1
$src  = "BAZA_WIEDZY\AUDYT_ZAKUPY_2026_05_23\KOD_STARTOWY"
$dest = "Kontrakty"

New-Item -ItemType Directory -Force -Path "$dest\Models", "$dest\Services", "$dest\Windows"
Copy-Item "$src\Models\*.cs"   "$dest\Models\"
Copy-Item "$src\Services\*.cs" "$dest\Services\"
Copy-Item "$src\Windows\*.xaml*" "$dest\Windows\"
```

### Krok 3 — dodanie do `.csproj` (jeśli MSBuild nie wykryje automatycznie)

Zwykle `<Compile Include="**\*.cs" />` w SDK-style projects już to robi. Jeśli nie:

```xml
<ItemGroup>
  <Page Update="Kontrakty\Windows\KontraktyListaWindow.xaml">
    <Generator>MSBuild:Compile</Generator>
    <SubType>Designer</SubType>
  </Page>
</ItemGroup>
```

### Krok 4 — wpięcie do Menu.cs (5 min)

W `Menu.cs` w kategorii `ZAOPATRZENIE I ZAKUPY` (linia ~1481) dodaj **jako drugi kafelek** (po Bazie Hodowców):

```csharp
new MenuItemConfig("KontraktyHodowcow", "Kontrakty Hodowców",
    "Rejestr kontraktów + dashboard ARiMR compliance + generator umów Word",
    Color.FromArgb(120, 190, 130), // jasny zielony, między Bazą Hodowców a Wstawieniami
    () => new Kalendarz1.Kontrakty.Windows.KontraktyListaWindow(), "📜", "Kontrakty"),
```

W `_moduleAccessOrder` (linia ~1300) dodaj na końcu:

```csharp
/* XX */ "KontraktyHodowcow",
```
(XX = kolejny numer; NIE wsuwaj w środek — łamie permissions)

### Krok 5 — build + test (10 min)

```powershell
dotnet build Kalendarz1.csproj
```

Uruchom, zaloguj się, otwórz kafelek "📜 Kontrakty Hodowców". Powinien pokazać pustą listę (jeszcze nie ma rekordów).

### Krok 6 — test ręczny (10 min)

1. Klik **➕ Nowy kontrakt** → MessageBox "TODO Faza 1" (oczekiwane, brak edytora)
2. Wstaw testowy rekord przez SQL:
   ```sql
   DECLARE @id INT;
   DECLARE @num VARCHAR(20), @lp INT;
   EXEC dbo.sp_KontraktyNastepnyNumer @Rok = 2027, @NumerOut = @num OUTPUT, @LpOut = @lp OUTPUT;

   INSERT INTO dbo.Kontrakty (NumerKontraktu, Rok, LpRoku, DostawcaId, TypKontraktu,
     Status, DataObowiazujeOd, DataObowiazujeDo,
     ProcentUbytku, TypCeny, Cena, TerminPlatnosciDni,
     NazwaHodowcySnapshot, NipSnapshot, NrGospodarstwaSnapshot,
     LiczySieDoArimr, PartiaPiorkowscy,
     UtworzylUserId)
   VALUES (@num, 2027, @lp, 12345, 'ARIMR_3LAT',
     'ACTIVE', '2027-01-01', '2030-01-01',
     3.00, 'wolnorynkowa', 7.50, 21,
     'TEST Hodowca', '1234567890', 'PL12345678',
     1, 'PIORKOWSCY',
     'test');
   ```
3. F5 w UI → wiersz pojawia się
4. Klik prawym → "📄 Generuj Word" → BŁĄD (bo brak szablonu w `_SZABLON\`) — to OK, to dowód że generator próbuje pracować
5. Wrzuć testowy szablon `.docx` (pusty) do `\\192.168.0.170\Install\UmowyZakupu\_SZABLON\Umowa_ARIMR_3LAT.docx` → ponów → Word się otworzy

### Krok 7 — Windows Scheduled Task (5 min)

```powershell
# W PowerShell jako Administrator
$action  = New-ScheduledTaskAction -Execute "C:\Program Files\Kalendarz1\Kalendarz1.exe" -Argument "--kontrakty-check"
$trigger = New-ScheduledTaskTrigger -Daily -At 2am
Register-ScheduledTask -TaskName "ZPSP Kontrakty Check" -Action $action -Trigger $trigger -Description "Nocny job alertów wygasania kontraktów"
```

**Dodaj obsługę `--kontrakty-check` w `App.xaml.cs`:**

```csharp
// App.xaml.cs OnStartup
protected override async void OnStartup(StartupEventArgs e)
{
    if (e.Args.Contains("--kontrakty-check"))
    {
        var svc = new Kalendarz1.Kontrakty.Services.KontraktyAlertService();
        var result = await svc.GenerujAlertyAsync();
        // log do EventLog albo do pliku
        System.IO.File.AppendAllText(
            @"C:\ZPSP\logs\kontrakty-job.log",
            $"{DateTime.Now:s} {result}\n");
        Shutdown(0);
        return;
    }
    base.OnStartup(e);
}
```

## Co dalej (Faza 2-3 — przewodnik)

| Co | Plik | Effort | Co dokładnie |
|---|---|---|---|
| **F1.1 KontraktyEditorWindow** | nowy | 1 dzień | Formularz: ComboBox hodowcy z DOSTAWCY, daty, ubytek, cena, typ; przycisk "Zapisz" → `_svc.CreateAsync/UpdateAsync` |
| **F2.1 Szablon Word `Umowa_ARIMR_3LAT.docx`** | nowy | ~1h (Asia) | Tekst umowy z bookmarkami `bm_*` (patrz `Szablony_Word/`) |
| **F2.2 KontraktyDetailsWindow** | nowy | 1 dzień | 3 zakładki: Podstawowe / Załączniki PDF / Audit log |
| **F2.3 BtnDodajSkan_Click** | uzupełnić | 4h | `OpenFileDialog` → `File.Copy` → `INSERT KontraktyZalaczniki` |
| **F3.1 DashboardArimrWindow** | nowy | 1 dzień | Wykres ProcentArimr (LiveCharts) + lista propozycji + lista wygasających |
| **F3.2 Export PDF audytu** | nowy | 1 dzień | iTextSharp — snapshot + lista kontraktów `LiczySieDoArimr=1` |
| **F3.3 WyslijEmailAsync** | uzupełnić | 4h | Outlook interop (jak w `WidokSpecyfikacje`) |

## Bezpieczeństwo / RODO / sanity checks

- ✅ Snapshot danych hodowcy (`NipSnapshot` etc.) chroni przed mutacjami w `DOSTAWCY` po podpisaniu umowy
- ✅ Audit log automatyczny dla wszystkich zmian statusów
- ✅ FK do `DOSTAWCY` z `ON DELETE` brak (chroni przed kasowaniem hodowcy z aktywną umową)
- ⚠️ Permissions na poziomie okna — TODO Faza 1 (dodać check `App.UserID` przeciw `accessMap`)
- ⚠️ Backup folderu `\\192.168.0.170\Install\UmowyZakupu\` — koniecznie ustawić w Edycie IT (codzienne)
