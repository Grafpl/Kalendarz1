# 12 — ZPSP (Kalendarz1) — architektura programu

## Co to jest ZPSP

**ZPSP** = "Zajebisty Program Sergiusza Piórkowskiego" = `Kalendarz1.csproj`.

**Autorski system Sergiusza** (5 lat rozwoju, programowany sam) który łączy dane z 4 baz (Symfonia + LibraNet + UNISYSTEM + ZPSP własne tabele) w jedną aplikację desktopową.

**Cel:** zastąpić rozproszone narzędzia (Excel, karteczki, telefon) jednym oknem prawdy dla każdego działu firmy.

---

## Stack techniczny

| Warstwa | Technologia |
|---|---|
| **Język** | C# .NET 8 (`net8.0-windows7.0`) |
| **UI nowsze moduły** | WPF + DevExpress GridControl |
| **UI starsze moduły** | WinForms (legacy, sukcesywnie migrowane) |
| **Wzorzec** | Code-behind (głównie, bez MVVM w starszych) |
| **DI** | Brak (connection strings hardcoded w klasach) |
| **DB** | SQL Server (4 instancje — patrz `13_Bazy_danych.md`) |
| **Charts** | LiveCharts (LiveCharts.Defaults, LiveCharts.Wpf) |
| **Mapy** | GMap.NET |
| **Excel** | ClosedXML |
| **PDF** | iTextSharp / własne generatory |

**Repozytorium:**
- Lokalizacja: `C:\Users\PC\source\repos\Grafpl\Kalendarz1\`
- Branch główny: `master`
- Git user: `Grafpl`
- Czyste branche bo Sergiusz pracuje sam

---

## Pliki uruchomieniowe

| Plik | Funkcja |
|---|---|
| `App.xaml` / `App.xaml.cs` | Entry point WPF, `App.UserID`, `App.UserFullName` |
| `Menu1.xaml` | Login screen (WPF) |
| `Menu.cs` | Główne menu (WinForms, ~2000 linii) |

**Workflow startu:**
1. Login → `Menu1.xaml` → user wpisuje hasło
2. Po loginie → ustawia `App.UserID`, `App.UserFullName`
3. Otwiera `Menu` (WinForms) — pełny ekran z kafelkami modułów

---

## System menu (kafelki)

**Klasa konfiguracyjna:**
```csharp
class MenuItemConfig {
  string ModuleName;        // identyfikator (= klucz uprawnień)
  string DisplayName;       // tytuł wyświetlany
  string Description;
  Color Color;              // kolor kafelka
  Func<Form> FormFactory;   // jak utworzyć okno
  string IconText;          // emoji/tekst ikony
  string ShortTitle;
}
```

**Uprawnienia:**
```csharp
Dictionary<int, string> accessMap = {
  {16, "UstalanieTranportu"},
  {21, "AnalizaTygodniowa"},
  {46, "DashboardPrzychodu"},
  {55, "PozyskiwanieHodowcow"},
  {56, "KartotekaTowarow"},
  {57, "WidokFlota"},
  {58, "ListaPartii"},
  {59, "TransportZmiany"},
  // ... więcej w Menu.cs
};
```

**Kategorie kafelków (`Dictionary<string, List<MenuItemConfig>>`):**

| Kategoria | Co tam |
|---|---|
| **ZAOPATRZENIE I ZAKUPY** | Zakup żywca, hodowcy |
| **PRODUKCJA I MAGAZYN** | Lista partii, kartoteka towarów, krojenie |
| **OPAKOWANIA I TRANSPORT** | Transport, flota, mapa |
| **SPRZEDAŻ I CRM** | Handlowcy, klienci, oferty |
| **PLANOWANIE I ANALIZY** | Dashboard analityczny, dashboard przychodu, prognozy |
| **FINANSE I ZARZĄDZANIE** | Faktury, marża, raporty |
| **KADRY I HR** | Kontrola godzin, wnioski urlopowe |
| **ADMINISTRACJA SYSTEMU** | Uprawnienia, przypomnienia |

---

## Konwencje kodu

### Connection strings (hardcoded w klasach)
```csharp
private const string CONN_HANDEL =
  "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";

private const string CONN_LIBRA =
  "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";

private const string CONN_UNISYSTEM =
  "Server=192.168.0.23\\SQLEXPRESS;Database=UNISYSTEM;...";
```

**Uwaga:** to NIE jest dobrze (powinny być w konfigu), ale tak jest historycznie. Nie zmieniać bez wyraźnej zgody Sergiusza.

### Async/await
Nowsze moduły (Partie, Flota, Reklamacje) używają async. Starsze są synchroniczne.

### DevExpress conventions
- `LightweightCellEditor` NIE wspiera `FontWeight`
- `ColumnDefinition`/`RowDefinition` ambiguous — użyć `System.Windows.Controls.ColumnDefinition`

### Pre-existing warnings (ignorować)
- Wiele `CS8618` (nullable reference types)
- `NU1603/NU1701` (NuGet)

### Build issues
- **Running app locks .exe** → błędy `MSB3027/MSB3021` (NIE są błędami kompilacji)
- Należy zamknąć app przed buildem

---

## Liczba okien (na 2026-04)

**~71 okien WPF + WinForms** (z audytu OKNA_PRODUKCYJNE_PLAN.md).

Z czego:
- **Aktywnie używane:** ~25-30
- **Rzadko używane:** ~20
- **Duplikaty/legacy:** ~15-20
- **Niedokończone:** ~5

**Cel Sergiusza:** Skala do ~45 okien (37% redukcji).

---

## Główne moduły z ich modułami

### Lista Partii Ubojowych (V2)
- **Folder:** `Partie/`
- **DB:** LibraNet
- **Tabele:** `listapartii`, `PartiaDostawca`, `Out1A`, `In0E`, `FarmerCalc`, `Haccp`, `QC*`, `HarmonogramDostaw`, `PartiaAuditLog`, `PartiaStatus`, `QC_Normy`
- **Główne okno:** `ListaPartiiWindow` z 2 zakładkami: `ProdukcjaDzisWidok` + `WidokPartie`
- **Service:** `PartiaService` (~1200 linii)
- **Status V2 lifecycle:** PLANNED → IN_TRANSIT → AT_RAMP → VET_CHECK → APPROVED → IN_PRODUCTION → PROD_DONE → CLOSED → CLOSED_INCOMPLETE → REJECTED
- **Menu:** `accessMap[58]`, kategoria PRODUKCJA I MAGAZYN

### Kartoteka Towarów
- **Folder:** `KartotekaTowarow/`
- **DB:** LibraNet (Article + related)
- **V2 features:** Dashboard stats, AuditLog, Favorites, Quick-edit prices, Compare, Print card
- **Menu:** `accessMap[56]`, kategoria PRODUKCJA I MAGAZYN

### Transport (zob. `09_Transport.md`)

### Flota (zob. `09_Transport.md`)

### Pozyskiwanie Hodowców
- **Folder:** `Hodowcy/`
- **DB:** LibraNet (`Pozyskiwanie_Hodowcy`, `Pozyskiwanie_Aktywnosci`)
- **Skala:** 1874 hodowców z Excela
- **Menu:** `accessMap[55]`, kategoria ZAOPATRZENIE I ZAKUPY

### Zamówienia Klientów (NoweZamowienieTestWindow) — refactor 2026-05-09
- **Główne okno:** `Zamowienia/Views/NoweZamowienieTestWindow.xaml(.cs)` (~2400+ linii)
- **DB:** LibraNet (`ZamowieniaMieso`, `ZamowieniaMiesoTowar`, `UserHandlowcy`, `NotatkiSzablony`, `NotatkiUzycia`) + Handel (`STContractors`, `HM.TW`, `ContractorClassification`)
- **Konstruktory:** `(string userId)` lub `(string userId, int? orderId)` — drugi parametr przełącza w **edit mode**
- **Stary moduł:** `WidokZamowienia.cs` **USUNIĘTY** — ~12 call-site'ów zmigrowano (m.in. WPF MainWindow, PanelFakturWindow, transport-editor, HistoriaZmianWindow, WidokZamowieniaPodsumowanie, DashboardWindow)
- **Smart Suggestions notatek (`NotatkiService`):** auto-tworzy schemat, backfilluje TOP 30 z historii, ranking multiplikatywny (klient ×3, towary jaccard ×1.5, recency exp(-d/30), pin ×5)
- **Kontekstowe „Przypisz handlowca…":** w 2 oknach (WPF MainWindow + WidokZamowieniaPodsumowanie)
- **Pełna dokumentacja:** `BAZA_WIEDZY/26_Modul_Zamowien_v2.md`

### KontrolaGodzin (HR)
- **Plik:** `KontrolaGodzin.xaml.cs` (~3100+ linii, 20+ zakładek)
- **DB:** UNISYSTEM (UNICARD RCP) + ZPSP (HR_*)
- **Modele:** `PracownikModel`, `RejestracjaModel`, `NieobecnoscModel`, etc. (na dole .xaml.cs)
- **Schema HR_*:** `ZPSP_ModulKadrowy_Tabele.sql`
- **Menu:** kategoria KADRY I HR

### Reklamacje (zob. `11_Reklamacje_jakosc.md`)

### Dashboard Analityczny (`AnalizaTygodniowa`)
- **Plik:** `AnalizaTygodniowa/AnalizaTygodniowaWindow.xaml.cs` (~1020 linii)
- **DB:** Handel
- **Wykresy:** LiveCharts
- **Menu:** `accessMap[21]`, kategoria PLANOWANIE I ANALIZY (DisplayName "Dashboard Analityczny")

### Dashboard Przychodu Żywca LIVE
- **Plik:** `DashboardPrzychodu/Views/DashboardPrzychoduWindow.xaml.cs` (~2293 linii — duży refactor wskazany)
- **DB:** Handel + LibraNet
- **Pokazuje:** Plan vs rzeczywiste dostawy żywca + prognoza tuszek + klasy A/B
- **Menu:** `accessMap[46]`, kategoria PLANOWANIE I ANALIZY

### Analiza Przychodu Produkcji
- **Folder:** `AnalizaPrzychoduProdukcji/`
- **DB:** wyłącznie LibraNet (192.168.0.109) — **NIE czyta sprzedaży z Symfonia 112**
- **Pliki:**
  - `Services/PrzychodService.cs` — wszystkie zapytania SQL (jedyne źródło prawdy)
  - `Models/PrzychodModels.cs` — DTO (`Dokladamy`, `Niedowaga`, etc.)
  - `AnalizaPrzychoduWindow.xaml.cs` — UI, agregacje w pamięci, drill-down, LIVE
  - `ViewModels/AnalizaPrzychoduViewModel.cs` — bindings dla LiveCharts
- **Tabele:** `In0E` (rdzeń ważeń), `Article`, `PartiaDostawca`
- **6 zakładek + 5 kart KPI + Health Strip** w UI
- **Pełna dokumentacja:** `BAZA_WIEDZY/18_Analiza_przychodu_szczegoly.md`

---

## Helpers globalne

```csharp
WindowIconHelper.SetIcon(this);  // ustawienie ikony okna
FormatTimeSpan(TimeSpan)         // formatowanie godzin

App.UserID          // logged user ID
App.UserFullName    // logged user full name
```

---

## Powiadomienia

**ChangeNotificationPopup** (`Zywiec/Kalendarz/ChangeNotificationPopup.xaml.cs`):
- Pozycja: **prawy dolny róg** (`Top = workArea.Bottom - popupHeight - 8`)
- Brak górnego nagłówka — informacje w stopce
- Każdy wiersz ma mini-przycisk `➡` do konkretnej dostawy
- Slide animacja z prawej (X: 400→0, 0→400)
- Brak przycisku Cofnij

---

## Co Sergiusz lubi w ZPSP

**Mocne strony:**
- Listy partii z drilldown do ważeń, QC, HACCP
- Kalkulator krojenia 14A
- CRM hodowców (1874 leads)
- Transport — drag&drop ładowania
- Auto-import faktur korygujących z Symfonii

---

## Co Sergiusz chciałby zmienić (frustracje)

> *"Brak informacji o stanach rzeczywistych w magazynach na bieżąco i partii które idą do kogo."*

**Konkretne luki:**
1. **Brak Hala LIVE** — kto na hali, co robi, ile towaru, temperatury
2. **Brak rozliczenia partii per klient** — magazynier wpisuje "na oko"
3. **Brak prognozy ile pracowników potrzeba** dziennie
4. **Brak wydajności pracowników** brudnej i czystej strefy
5. **Mobile app dla Pani Joli / Justyny** — na hali tablet
6. **Real-time WAGO + RADWAG** — brak API od dostawców
7. **Czytniki temperatury** — fizyczne braki
8. **MroźniaDashboard** — okno dla Janka

---

## Architekturalne dylematy

### Duplikaty modułów (do konsolidacji)

1. **`ProdukcjaDzisWidok` + `WidokPartie`** — dwa widoki na te same dane
2. **`AnalizaTygodniowa` + `DashboardPrzychoduWindow`** — wykresy się przeplatają
3. **`FormReklamacja*` (4 okna)** — duplikaty CRUD

### Brak DI / abstrakcji

- Connection strings hardcoded
- Brak interfejsów dla testowania
- Code-behind heavy

**Plan:** Sergiusz pracuje sam, więc DI nie jest priorytetem. Większy zysk z UX modułów niż refactoringu architektury.

---

## Repozytorium kodu — struktura folderów (najważniejsze)

```
Kalendarz1/
├── Menu.cs, Menu1.xaml             ← Entry point
├── App.xaml.cs                     ← App.UserID
├── Partie/                         ← Lista partii V2
├── KartotekaTowarow/
├── Transport/                      ← Transport + edytor kursu
├── Flota/                          ← Kierowcy + pojazdy
├── MapaFloty/                      ← Real-time map
├── Hodowcy/                        ← CRM hodowców
├── Reklamacje/                     ← 4 okna reklamacji
├── AnalizaTygodniowa/              ← Dashboard analityczny
├── DashboardPrzychodu/             ← Przychód Żywca LIVE
├── KontrolaGodzin/                 ← HR
├── Zywiec/                         ← Kalendarz dostaw + specyfikacje
├── KrojenieMrozenie/               ← Kalkulator 14A
├── DOKUMENTY OGÓLNIKOWE/           ← Procedury, raporty (PDF/DOCX)
├── BAZA_WIEDZY/                    ← Ten folder!
└── obj/, bin/                      ← Build artifacts
```

---

## Build commands

```bash
# Build (zamknij app przedtem!)
dotnet build Kalendarz1.csproj -nologo -v:m

# Tylko sprawdź błędy
dotnet build Kalendarz1.csproj -nologo -v:m 2>&1 | grep -E "Liczba błędów" | tail -3
```

---

## Wskazówki dla nowej rozmowy

1. **Nie dotykaj kodu bez zgody Sergiusza** (nawet jeśli "quick win" wygląda kuszący)
2. **Najpierw rozmowa** — co user realnie robi w typowy dzień, czego brakuje
3. **Konkrety:** file:line, konkretne tabele, konkretne nazwy okien
4. **Nie rób refactoringu architektonicznego** bez wyraźnego briefu
5. **Sprawdź przed zaproponowaniem zmiany** — czy okno faktycznie istnieje w menu i czy user w nie wchodzi
