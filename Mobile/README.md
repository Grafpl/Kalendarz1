# Kalendarz Zamówień - Aplikacja Mobilna Android

Mobilna wersja systemu zarządzania zamówieniami dla telefonów Android, zbudowana w .NET MAUI.

## Funkcjonalności

- **Przegląd zamówień** - lista zamówień na wybrany dzień z filtrowaniem
- **Szczegóły zamówienia** - pełne informacje o zamówieniu i towarach
- **Statystyki dnia** - podsumowanie ilościowe zamówień
- **Nawigacja po datach** - łatwe przełączanie między dniami
- **Wyszukiwanie** - filtrowanie po kliencie i statusie
- **Tryb offline** - dane demonstracyjne gdy brak połączenia z API

## Wymagania

### Do kompilacji
- .NET 8 SDK
- .NET MAUI workload (`dotnet workload install maui`)
- Android SDK (API 21+)
- Visual Studio 2022 lub VS Code z rozszerzeniem MAUI

### Do uruchomienia
- Android 5.0 (Lollipop) lub nowszy
- Połączenie sieciowe do serwera API

## Struktura projektu

```
Mobile/
├── App.xaml(.cs)           # Główna aplikacja i zasoby
├── AppShell.xaml(.cs)      # Nawigacja Shell
├── MauiProgram.cs          # Konfiguracja DI
├── KalendarzMobile.csproj  # Plik projektu
│
├── Models/                 # Modele danych
│   ├── Zamowienie.cs       # Zamówienia i towary
│   └── Kontrahent.cs       # Kontrahenci i filtry
│
├── ViewModels/             # MVVM ViewModels
│   ├── BaseViewModel.cs
│   ├── ZamowieniaListViewModel.cs
│   ├── ZamowienieDetailViewModel.cs
│   └── SettingsViewModel.cs
│
├── Views/                  # Widoki XAML
│   ├── ZamowieniaListPage.xaml(.cs)
│   ├── ZamowienieDetailPage.xaml(.cs)
│   └── SettingsPage.xaml(.cs)
│
├── Services/               # Serwisy
│   ├── IZamowieniaService.cs
│   ├── ZamowieniaService.cs
│   └── ISettingsService.cs
│
├── Converters/             # Konwertery XAML
│   └── ValueConverters.cs
│
└── Platforms/Android/      # Konfiguracja Android
    ├── AndroidManifest.xml
    ├── MainActivity.cs
    └── MainApplication.cs
```

## Kompilacja

### 1. Zainstaluj MAUI workload
```bash
dotnet workload install maui
```

### 2. Przywróć pakiety
```bash
cd Mobile
dotnet restore
```

### 3. Zbuduj aplikację
```bash
# Debug
dotnet build -f net8.0-android

# Release APK
dotnet publish -f net8.0-android -c Release
```

### 4. Zainstaluj na urządzeniu
```bash
adb install bin/Release/net8.0-android/com.kalendarz.mobile-Signed.apk
```

## Uruchomienie API Backend

Aplikacja mobilna wymaga uruchomionego serwera API do komunikacji z bazą danych.

```bash
cd Mobile.Api
dotnet run
```

API będzie dostępne pod adresem: `http://192.168.0.109:5000`

### Endpointy API

| Endpoint | Opis |
|----------|------|
| `GET /api/zamowienia` | Lista zamówień z filtrowaniem |
| `GET /api/zamowienia/{id}` | Szczegóły zamówienia |
| `GET /api/kontrahenci` | Lista kontrahentów |
| `GET /api/statystyki/{data}` | Statystyki dnia |
| `GET /api/health` | Health check |

## Konfiguracja

### Adres API
W aplikacji mobilnej przejdź do **Ustawienia** i zmień adres API na adres serwera.

### Połączenia bazodanowe
W pliku `Mobile.Api/appsettings.json` skonfiguruj połączenia do baz danych:
- `LibraNet` - baza zamówień
- `Handel` - baza kontrahentów i towarów

## Rozwój

### Dodawanie nowych funkcji
1. Utwórz Model w `Models/`
2. Dodaj metody do `IZamowieniaService` i `ZamowieniaService`
3. Stwórz ViewModel w `ViewModels/`
4. Stwórz widok XAML w `Views/`
5. Zarejestruj w `MauiProgram.cs`
6. Dodaj trasę w `AppShell.xaml.cs`

### Testowanie
Aplikacja posiada wbudowane dane demonstracyjne, które są używane gdy:
- Brak połączenia z API
- Tryb offline
- Testowanie bez bazy danych

## Architektura

```
┌─────────────────────┐
│   Android Device    │
│  ┌───────────────┐  │
│  │  MAUI App     │  │
│  │  (Views +     │  │
│  │   ViewModels) │  │
│  └───────┬───────┘  │
└──────────│──────────┘
           │ HTTP/JSON
           ▼
┌──────────────────────┐
│    REST API          │
│  (ASP.NET Core)      │
│  Mobile.Api/         │
└──────────┬───────────┘
           │ SQL
           ▼
┌──────────────────────┐
│   SQL Server         │
│  ┌────────┐ ┌──────┐ │
│  │LibraNet│ │Handel│ │
│  └────────┘ └──────┘ │
└──────────────────────┘
```

## Technologie

- **.NET 8** - platforma
- **.NET MAUI** - framework UI cross-platform
- **CommunityToolkit.Mvvm** - MVVM helpers
- **System.Text.Json** - serializacja JSON
- **ASP.NET Core** - REST API
- **Microsoft.Data.SqlClient** - połączenia SQL Server

## Licencja

Własnościowe - Piórkowscy © 2024
