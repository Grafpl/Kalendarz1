# ZPSP.Sales - Modul Sprzedazy

## Opis

**ZPSP.Sales** to zrefaktoryzowany modul sprzedazy aplikacji ZPSP (Zajebisty Program Sergiusza Piorkowskiego). Modul obsluguje zarzadzanie zamowieniami, bilansowanie produkcji, transport i analityke dla ubojni drobiu przetwarzajacej ~70 000 kurczakow dziennie (200 ton).

## Architektura

Modul wykorzystuje wzorzec **MVVM** (Model-View-ViewModel) z pelna separacja warstw:

```
ZPSP.Sales/
├── Models/           # Klasy domenowe
├── ViewModels/       # Logika prezentacji
├── Views/            # Widoki XAML
├── Services/         # Logika biznesowa
├── Repositories/     # Dostep do danych
├── Infrastructure/   # DI, komendy, konfiguracja
└── SQL/              # Scentralizowane zapytania SQL
```

## Wymagania

- **.NET 8.0** lub nowszy
- **SQL Server 2019** lub nowszy
- **Windows 10/11** (WPF)
- Dostep sieciowy do baz danych:
  - LibraNet (192.168.0.109) - glowna baza operacyjna
  - Handel (192.168.0.112) - Sage Symfonia ERP
  - TransportPL (192.168.0.109) - logistyka

## Pakiety NuGet

```xml
<PackageReference Include="Microsoft.Data.SqlClient" Version="5.1.5" />
<PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="8.0.0" />
```

## Instalacja

1. Sklonuj repozytorium:
```bash
git clone https://github.com/organization/zpsp.git
```

2. Skonfiguruj connection strings w `DatabaseConnections.cs` lub zaladuj z pliku konfiguracyjnego:
```csharp
DatabaseConnections.Instance.LoadFromFile(@"C:\Config\connections.json");
```

3. Zbuduj projekt:
```bash
dotnet build ZPSP.Sales
```

## Konfiguracja

### Connection Strings

Domyslna konfiguracja znajduje sie w `Infrastructure/DatabaseConnections.cs`. W produkcji zalecane jest ladowanie z zewnetrznego pliku:

```json
{
  "LibraNet": "Server=192.168.0.109;Database=LibraNet;...",
  "Handel": "Server=192.168.0.112;Database=Handel;...",
  "TransportPL": "Server=192.168.0.109;Database=TransportPL;..."
}
```

### Dependency Injection

W `App.xaml.cs`:
```csharp
protected override void OnStartup(StartupEventArgs e)
{
    var services = new ServiceCollection();
    services.AddSalesModule();
    var serviceProvider = services.BuildServiceProvider();

    // Lub uzyj ServiceLocator dla starszego kodu
    ServiceLocator.Initialize(serviceProvider);
}
```

## Uzycie

### Tworzenie MainWindow z ViewModel:

```csharp
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow(IOrderService orderService,
                      IProductService productService,
                      ICacheService cacheService)
    {
        InitializeComponent();
        _viewModel = new MainViewModel(orderService, productService, cacheService);
        DataContext = _viewModel;

        Loaded += async (s, e) => await _viewModel.InitializeAsync();
    }
}
```

### Uzycie serwisow:

```csharp
// Pobierz zamowienia na dzien
var orders = await _orderService.GetOrdersForDateAsync(DateTime.Today);

// Pobierz agregacje produktow
var aggregations = await _productService.GetProductAggregationsAsync(DateTime.Today, useReleases: false);

// Uzyj cache
var customerName = await _cacheService.GetCustomerNameAsync(customerId);
```

## Glowne funkcjonalnosci

### Zarzadzanie zamowieniami
- Lista zamowien na wybrany dzien
- Filtrowanie po odbiorcy i produkcie
- Anulowanie i przywracanie zamowien
- Duplikowanie zamowien na inny dzien
- Historia zmian

### Bilansowanie produkcji
- Agregacje per produkt (plan, fakt, zamowienia, wydania)
- Stany magazynowe
- Bilans: (Fakt lub Plan) + Stan - (Zamowienia lub Wydania)
- Grupowanie produktow (scalanie)

### Dashboard
- Suma zamowien i wydan
- Liczba zamowien i klientow
- Pule Kurczaka A i B
- Wspolczynnik wydajnosci

## Optymalizacje SQL

1. **Batch loading** - eliminacja N+1 przez ladowanie wszystkich pozycji jednym zapytaniem
2. **Scentralizowane zapytania** - wszystkie SQL w `SQL/SqlQueries.cs`
3. **Parametryzacja** - ochrona przed SQL injection
4. **Cache** - centralne cachowanie kontrahentow, produktow, wydan

Przykladowe zoptymalizowane zapytanie:
```sql
-- Pobiera podsumowanie zamowien per produkt (jedno zapytanie zamiast N)
SELECT t.KodTowaru, SUM(t.Ilosc), COUNT(DISTINCT z.KlientId)
FROM ZamowieniaMiesoTowar t
JOIN ZamowieniaMieso z ON t.ZamowienieId = z.Id
WHERE z.DataUboju = @Day AND z.Status <> 'Anulowane'
GROUP BY t.KodTowaru
```

## Testowanie

```bash
dotnet test ZPSP.Sales.Tests
```

## Autorzy

- Sergiusz Piorkowski - glowny autor systemu
- Zespol IT Piorkowscy

## Licencja

Wlasnosc prywatna - wszelkie prawa zastrzezone.
