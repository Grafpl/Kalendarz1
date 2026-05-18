# CLAUDE.md — instrukcje dla AI w repo Kalendarz1 (ZPSP)

> Plik ładowany automatycznie przez Claude Code. Streszczenie konwencji + najważniejsze gotchas.
> Pełniejsze info: `BAZA_WIEDZY/`, w szczególności `13_Bazy_danych.md` i `22_Analityka_Pelna_modul.md`.

## 1. Czym jest projekt

**Kalendarz1** (wewnętrznie **ZPSP** = "Zajebisty Program Sergiusza Piórkowskiego") — WPF .NET 8.0 (target `net8.0-windows7.0`) dla zakładu drobiarskiego "Piórkowscy" (~258M obrotu, 200 t/dzień). Aplikacja produkcyjno-handlowa łącząca Sage Symfonia (HANDEL), system wagowy LibraNet, RCP UNICARD i własne tabele HR (ZPSP).

- Login: `Menu1.xaml` (WPF) → menu główne `Menu.cs` (WinForms, hybrydowe).
- Kontekst użytkownika: `App.UserID`, `App.UserFullName`.
- Permissions: `accessMap` dict (int → module name) + `userPermissions` dict.

## 2. Architektura (wysoki poziom)

```
Kalendarz1.exe
├── Menu.cs (WinForms główne menu)
├── 4 instancje SQL Server:
│   ├── HANDEL (192.168.0.112) — Sage Symfonia
│   ├── LibraNet (192.168.0.109) — wagi In0E, partie
│   ├── TransportPL (192.168.0.109) — moduł transportu
│   └── UNISYSTEM (192.168.0.23\SQLEXPRESS) — UNICARD RCP + HR
└── Moduły (po jednym folderze):
    ├── AnalitykaPelna/        # 4 widoki + dialog drill-down
    ├── KontrolaGodzin/        # HR/RCP, ~3100 linii w 1 oknie
    ├── Transport/             # 10+ plików, palety/kursy
    ├── Hodowcy/               # CRM hodowców
    ├── Flota/                 # kierowcy + pojazdy
    ├── Partie/                # Lista Partii V2 (lifecycle 10-state)
    ├── KartotekaTowarow/      # Article + Audit + Favorites
    ├── CentrumNagranAI/       # CCTV + Claude AI
    ├── MarketIntelligence/    # Tavily + intel_* tabele
    └── ZPSP.Sales / Handlowiec / Kartoteka / Reklamacje / ...
```

## 3. Konwencje kodu

### Hard rules
- **Code-behind, NIE MVVM** — projekt celowo bez ViewModeli (decyzja architektoniczna). Eventy w XAML, `x:Name` + bezpośredni dostęp w `*.xaml.cs`.
- **Connection strings hardcoded** w klasach okien (legacy) — nowe moduły używają `appsettings.json` przez serwisy konfiguracji (`AnalitykaConfig`, `IRZplusConfig`).
- **Nullable reference types ON** — wiele pre-existing CS8618 warnings, ignorować w nowym kodzie pisać `string Foo { get; set; } = ""` żeby nie generować nowych.
- **Style XAML:** używaj inline `Style="..."` lub `<Window.Resources>` lokalnie, unikaj globalnych Resources poza `App.xaml`.

### Tools w pierwszej kolejności
- `Read`, `Grep`, `Glob` — eksploracja
- `Edit` z `replace_all=false` (default) — zmiany punktowe
- `Bash` z `dotnet build Kalendarz1.csproj` — weryfikacja
- Build error MSB3027/MSB3021 (zablokowany exe) = aplikacja jest uruchomiona, **nie real compile error**

### Naming
- Polskie nazwy klas/metod/zmiennych są OK (`Pracownik`, `Hodowca`, `WydajnoscDzien`, `WazenieRekord`).
- x:Name w XAML: `dgPracownicy`, `txtKpiWazen`, `cbTowar`, `btnZastosuj`.
- Service classes: `*Service` (np. `WydajnoscService`, `RealizacjaService`).
- Models: `*Model` lub po prostu nazwa domeny (`PracownikModel`, `RealizacjaModels.cs` zawiera kilka klas).

## 4. SQL — główne gotchas

### LibraNet (SQL Server 2008 R2)
- **BRAK `TRY_CONVERT`** — używaj `CAST` + walidacja w .NET.
- Ograniczone funkcje okienkowe (LAG/LEAD słabe).
- Daty jako string: `cmd.Parameters.AddWithValue("@DataOd", f.DataOd.ToString("yyyy-MM-dd"))`.

### HANDEL (Sage Symfonia) — pełne gotchas

> Pełen schemat + przykłady → `BAZA_WIEDZY/23_HANDEL_Schema_Sage_Symfonia.md`.

- `MG.data` to `datetime` — zawsze `CAST(MG.data AS DATE)` przy `BETWEEN`.
- **`MG.anulowany = 0` ZAWSZE w WHERE** (Sage zostawia anulowane!).
- **`MG.khdzial` repurposowane dla MM-/MM+** — trzyma ID magazynu docelowego (sMM-) lub źródłowego (sMM+). `sMM-` i `sMM+` to OSOBNE dokumenty, więc subquery na siblingach `MZ` NIE DZIAŁA.
- **`HM.MZ.kod` może się duplikować** w wielolinijnych dokumentach — STRING_AGG potrzebuje CTE z pre-agregacją per `MG.id`.
- **`HM.MZ.ilosc` ma niespójne znaki** — zawsze `ABS(MZ.ilosc)`, kierunek wyciągaj z `MG.seria`.
- **NIE MA tabeli słownikowej magazynów w HANDEL!** Sage trzyma nazwy w UI/konfiguracji. Parsuj sufiksy kodu MM+/MM- (np. `"0001/22/MM-/M. PROD"` → magazyn=65554 = "M. PROD"). Implementacja: `MagazynyHelper.LoadFromDatabaseAsync()`.
- `HM.MZ.ProductionLineID` PUSTE — moduł produkcji nigdy nie wdrożony.
- `MF.Production*` (87 tabel) PUSTE — to samo.
- `STRING_AGG` działa (SQL 2017+). LibraNet — używaj `STUFF + FOR XML PATH('')`.
- **Magazyny** (real names): 65555=M.UBOJ, 65554=M.PROD, 65556=M.DYST, 65552=M.MROŹ, 65562=M.MASAR, 65547=KARMA, 65551=M.ODPA, 65559=Mag.opak. (pełna mapa → `24_Magazyny_i_Lancuch_Produkcji.md`).
- **Katalogi towarów**: 65882=Żywiec, 67094=Odpady, 67095=Mięso świeże, 67104=Mięso inne, 67153=Mrożone.

### Cross-DB
- **Nie ma cross-DB JOIN-ów.** Łączenie po stronie .NET (LINQ in-memory).
- `CommandTimeout = 60` na agregowanych query.

### `In0E` (LibraNet) — ważenia palet
- `QntInCont` = klasa wagowa drobiu (1-12), **nie ilość**:
  - 4–7 = Duży kurczak (~14-16 kg/szt, 36/paleta)
  - 8–12 = Mały kurczak (~5-7 kg/szt)
  - 1-3, >12 = anomalie, odrzucamy
- Realny zakres `ActWeight` (waga palety): **500–600 kg**. Poza tym to anulacje/błędy.
- `P1` = klucz partii → JOIN z `dbo.PartiaDostawca.Partia`.
- `Godzina` to `varchar` "HH:mm:ss", nie `time`.

## 5. Moduł "Analityka Pełna" (najważniejszy aktywny moduł)

Pełna dokumentacja: `BAZA_WIEDZY/22_Analityka_Pelna_modul.md`.
**Stan magazynów (sub-tab w Bilansie)**: `BAZA_WIEDZY/25_Analityka_Pelna_v2_StanMagazynow.md`.

### Quick reference
- Główne okno: `AnalitykaPelna/AnalitykaPelnaWindow.xaml` (TabControl Plan/Realizacja/Bilans/Wydajność).
- Wspólny pasek filtrów: `AnalitykaPelna/Controls/FiltryPasek` (kompakt + rozwijany "▼ Więcej" + presety jako dropdown).
- 4 service'y: `PrognozaService`, `BilansService`, `RealizacjaService`, `WydajnoscService`.
- Dialog drill-down: `Windows/SzczegolyKlasyDialog` (double-click klasy → szczegóły wszystkich ważeń).
- LiveCharts.Wpf 0.9.7 (gotcha: NaN crash, użyj `0.0` + label guard).

### Bilans materiałowy — 2 sub-zakładki (NOWE 2026-05-09)
- **📊 Pozycje** — klasyczna lista pozycji z grupowaniem po etap, kolumny kg/% bazy/dokumenty
- **🏭 Stan magazynów** — wizualizacja production-focused:
  1. **Łańcuch produkcji** (5 kafelków + 4 strzałki z wydajnościami w %)
  2. **Towary wyprodukowane** (3-kolumnowy grid kart ze zdjęciami z `Assets/Towary/`)
  3. **Przepływy MM-** (Sankey-style z paskami proporcjonalnymi do kg)

### Konwertery WPF (`AnalitykaPelna/Services/Konwertery.cs`)
- `KategoriaKolorConverter` — kategoria towaru → kolor
- `WydajnoscKolorConverter` — % wydajności → kolor (czerwony/żółty/zielony)
- `EtapTloConverter` / `EtapKolorConverter` — etap bilansu
- `HexToBrushConverter` — string hex → SolidColorBrush (dynamiczne kolory)
- `SafeImagePathConverter` — string path → BitmapImage (defensywnie, with file existence check)
- `BoolToVisibilityConverter` / `BoolToVisibilityInverseConverter`

### Zdjęcia towarów
Folder: `Assets/Towary/{kod}.{jpg|png|jpeg|webp}`. Pliki kopiowane przez `.csproj` z `CopyToOutputDirectory="PreserveNewest"`. Brak pliku → fallback na ikonę kategorii.

### Kolory klasy drobiowej (używaj konsekwentnie)
- Duży kurczak (4–7): niebieski `#2563EB`/`#1E40AF`
- Mały kurczak (8–12): pomarańczowy `#F97316`/`#9A3412`
- Razem (suma 4–12): fioletowy `#7C3AED`/`#5B21B6`

### Histogram wagi palety
- Zawsze filtruj do **500–600 kg** (poza zakresem to anulacje).
- Oś X dynamicznie dopasowuje się do faktycznych danych (jeśli wszystko w 540–558 → pokazujemy tylko 540–550).
- Bin co 10 kg.
- Anomalie ukryte, ale liczba widoczna w tytule osi X.

## 6. Moduły aktywne (skrót dla orientacji)

| Moduł | Folder | accessMap | Główne okno | DB |
|---|---|---|---|---|
| Analityka Pełna | `AnalitykaPelna/` | (TBD) | AnalitykaPelnaWindow | HANDEL + LibraNet |
| Kontrola Godzin | (root + folders) | — | KontrolaGodzin (~3100 linii) | UNISYSTEM + ZPSP |
| Transport | `Transport/` | 16, 59 | TransportMainFormImproved | TransportPL + LibraNet + Handel |
| Hodowcy | `Hodowcy/` | 55 | PozyskiwanieHodowcowWindow | LibraNet |
| Flota | `Flota/` | 57 | (UserControl WidokFlota) | LibraNet |
| Lista Partii V2 | `Partie/` | 58 | ListaPartiiWindow | LibraNet |
| Kartoteka Towarów | `KartotekaTowarow/` | 56 | KartotekaTowarowWindow | LibraNet |
| Centrum Nagrań AI | `CentrumNagranAI/` | 67 | (CCTV + Claude) | sekrety w `%LOCALAPPDATA%` |

## 7. Build & runtime

- `dotnet build Kalendarz1.csproj` — weryfikacja kompilacji.
- Aplikacja uruchomiona blokuje `bin/Debug/.../Kalendarz1.exe` → MSB3027/MSB3021 (NIE real compile error).
- Wiele NU1603/NU1701 (NuGet) i CS8618 (nullable) warnings — pre-existing, ignorować.
- DevExpress (Lista Partii): `LightweightCellEditor` nie wspiera FontWeight; ColumnDefinition/RowDefinition kolizje namespace — używaj `System.Windows.Controls.ColumnDefinition`.

## 8. Czego NIE robić

- ❌ Nie wprowadzaj MVVM/ViewModeli — projekt celowo code-behind.
- ❌ Nie pisz testów jednostkowych — projekt nie ma test runnera (jeśli user nie poprosi).
- ❌ Nie usuwaj/zmieniaj nazw `accessMap` indeksów — łamie permissions w prod.
- ❌ Nie commituj `appsettings.json` z prawdziwymi sekretami — sprawdź `.gitignore`.
- ❌ Nie używaj cross-DB JOIN — łącz dane w .NET.
- ❌ Nie zakładaj że `In0E.QntInCont` to ilość — to klasa wagowa drobiu (1-12).

## 9. Skróty mentalne dla typowych zadań

- **Dodanie nowego widoku do Analityki Pełnej:**
  1. Nowy `WidokX.xaml(.cs)` w `AnalitykaPelna/Views/`.
  2. Nowy service `XService` w `AnalitykaPelna/Services/`.
  3. Modele w `AnalitykaPelna/Models/`.
  4. Wpięcie `<TabItem>` w `AnalitykaPelnaWindow.xaml`.
  5. Subskrypcja `FiltryZastosowane` z `filtryPasek`.

- **Nowy moduł w menu głównym:**
  1. Nowy folder `NazwaModulu/` z głównym oknem.
  2. `accessMap[N] = "NazwaModulu"` w `Menu.cs`.
  3. `MenuItemConfig` z FormFactory.
  4. Permissions w bazie (jeśli trzeba).

- **Nowy raport SQL z LibraNet:**
  1. Pamiętaj o SQL Server 2008 R2 (brak TRY_CONVERT).
  2. Daty jako string `yyyy-MM-dd`.
  3. `CommandTimeout = 60` na większych query.
  4. Przed produkcją sprawdź indeksy (patrz `BAZA_WIEDZY/SELECTY/REKOMENDACJE_INDEKSY_ANALITYKA.sql`).

---

**Aktualizacja:** 2026-05-09 — duża aktualizacja po refactorze "Stan magazynów" w Bilansie:
- Sekcja 4 (HANDEL gotchas) rozszerzona o 7 nowych odkryć (anulowany, khdzial dla MM-, brak słownika magazynów, ABS(ilosc), STRING_AGG)
- Sekcja 5 (Analityka Pełna) zaktualizowana o sub-zakładkę Stan magazynów + konwertery + zdjęcia
- Dodano linki do nowych plików BAZA_WIEDZY/23, 24, 25.
