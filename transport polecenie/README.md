# ZPSP — Edytor Kursu Transportowego (Wariant A)

## Struktura plików

```
zpsp-transport/
├── Theme/
│   ├── ZpspColors.cs          ← Paleta kolorów (WSZYSTKIE kolory w jednym miejscu)
│   └── ZpspFonts.cs           ← Definicje fontów
├── Models/
│   └── TransportModels.cs     ← Klasy: Order, CourseStop, TransportCourse,
│                                 Driver, Vehicle, CourseConflict
├── Controls/
│   ├── CapacityBarControl.cs  ← Pasek ładowności (custom ProgressBar)
│   ├── RoutePillsControl.cs   ← Wizualizacja trasy [START]→[KLIENT]→[POWRÓT]
│   └── ConflictPanelControl.cs← Panel alertów (wykryte konflikty)
├── Services/
│   └── ConflictDetectionService.cs ← Silnik wykrywania 14 typów konfliktów
├── KursEditorForm.cs          ← GŁÓWNA FORMA (layout + logika)
└── README.md                  ← Ten plik
```

## Layout Wariantu A (ASCII wireframe)

```
┌──────────────────────────────────────────────────────────────────────────┐
│                          TITLE BAR (system)                              │
├─────────────── 52% ──────────────┬──────────────── 48% ──────────────────┤
│  CIEMNY PANEL (#2B2D42)         │  JASNY PANEL (biały)                   │
│                                  │                                        │
│  ┌ HEADER KURSU ───────────────┐│  ┌ NAGŁÓWEK (#43A047 zielony) ───────┐│
│  │ KIEROWCA  [Radosław Czapla▾]││  │ 📋 ZAMÓWIENIA    [14 zam.]       ││
│  │ POJAZD    [EBR 08HY - 4 p▾]││  │        [🔍 Szukaj] [📅 14.02]   ││
│  │                              ││  └──────────────────────────────────┘│
│  │ DATA [14.02.2026]            ││  ┌ TABELA ZAMÓWIEŃ ─────────────────┐│
│  │ GODZ [06:00] → [18:00]      ││  │ ► 16.02 poniedziałek             ││
│  │                              ││  │ • O&M       11:00 14.8 533       ││
│  │ TRASA:                       ││  │ • Trzepałka 13:00 25.0 1000      ││
│  │ [🏭START]→[LOCIV]→[PODOLSKI]││  │ ● Damak     14:00 33.0 1320      ││
│  │          →[🏠POWRÓT]         ││  │ • Destan    14:00  6.7  240      ││
│  │                              ││  │ • ŁYSE      16:00 14.5  520      ││
│  │ ŁADOWNOŚĆ ▓▓▓▓▓▓▓▓▓▓ 536%  ││  │ • BOMAFAR   21:00  8.3  300      ││
│  │ 21.4/4 pal ⚠ PRZEŁADOWANE!  ││  │ • BATISTA   21:00 16.7  600      ││
│  │                              ││  │ • SMOLIŃSKI 21:00  9.4  340      ││
│  │ ⚠️ WYKRYTE PROBLEMY [4]     ││  │                                   ││
│  │ 🔴 Przeładowanie 536%       ││  │ ► 17.02 wtorek                    ││
│  │ 🔴 LOCIV - adres zagraniczny││  │ ● EUREKA    05:00  2.2   80      ││
│  │ 🟡 Kierowca po godzinach    ││  │ • Kaptan    06:00  8.3  300      ││
│  │ 🔵 Damak+Destan blisko      ││  │ ◆ Ladros    08:00 16.7  600      ││
│  │                              ││  │ ● RADDROB   08:00 33.0 1320      ││
│  │ Utworzył: Admin • Maja       ││  │ • TWÓJ M.   08:00  6.3  229      ││
│  └──────────────────────────────┘│  └──────────────────────────────────┘│
│  ┌ ŁADUNKI W KURSIE ──────────┐ │                                        │
│  │ 🚚 ŁADUNKI [2]  KOLEJN ▲▼ │ │                                        │
│  │ 1 LOCIV IMPEX  2.0  72    │ │                                        │
│  │ 2 PODOLSKI    19.4 700    │ │                                        │
│  │ Σ Palety: 21.4 • Poj: 772 │ │                                        │
│  └────────────────────────────┘ │  ┌──────────────────────────────────┐  │
│  [ANULUJ]        [✓ ZAPISZ KURS]│  │ ⬇ Dodaj zaznaczone do kursu (2) │  │
│                                  │  └──────────────────────────────────┘  │
└──────────────────────────────┴────────────────────────────────────────────┘
```

## Jak dodać do istniejącego projektu ZPSP

### 1. Skopiuj pliki
Skopiuj foldery `Theme/`, `Models/`, `Controls/`, `Services/` do projektu.
Zmień namespace z `ZpspTransport` na swój (np. `ZPSP.Transport`).

### 2. Dodaj using-i
```csharp
using ZpspTransport.Theme;
using ZpspTransport.Models;
using ZpspTransport.Controls;
using ZpspTransport.Services;
```

### 3. Integracja z istniejącym kodem
W `KursEditorForm.cs` podmień `LoadSampleData()` na prawdziwe dane z bazy:
```csharp
private void LoadRealData(int courseId)
{
    _drivers = _db.Drivers.Where(d => d.IsActive).ToList();
    _vehicles = _db.Vehicles.Where(v => v.IsAvailable).ToList();
    _allOrders = _db.Orders.Where(o => !o.IsAssigned || o.AssignedCourseId == courseId).ToList();
    _course = _db.Courses.Include(c => c.Stops).FirstOrDefault(c => c.Id == courseId) ?? new();
    _allCourses = _db.Courses.Where(c => c.Id != courseId && c.DataWyjazdu.Date == _course.DataWyjazdu.Date).ToList();
}
```

### 4. Zapis do bazy
W metodzie `SaveCourse()` zamień `// TODO` na:
```csharp
_db.Courses.Update(_course);
_db.SaveChanges();
```

## 14 typów wykrywanych konfliktów

| # | Kod | Poziom | Opis |
|---|-----|--------|------|
| 1 | NO_DRIVER | 🔴 Error | Brak kierowcy |
| 2 | NO_VEHICLE | 🔴 Error | Brak pojazdu |
| 3 | CAPACITY_OVERLOAD | 🔴 Error | Przeładowanie palet >100% |
| 4 | WEIGHT_OVERLOAD | 🔴 Error | Przekroczenie DMC (waga) |
| 5 | DRIVER_DOUBLE_BOOKING | 🔴 Error | Kierowca w 2 kursach naraz |
| 6 | VEHICLE_DOUBLE_BOOKING | 🔴 Error | Pojazd w 2 kursach naraz |
| 7 | CAPACITY_HIGH | 🟡 Warning | Naczepa >80% |
| 8 | WEIGHT_HIGH | 🟡 Warning | Waga >80% DMC |
| 9 | DRIVER_HOURS | 🟡 Warning | Czas pracy >12h |
| 10 | DUPLICATE_CLIENT | 🟡 Warning | Ten sam klient w 2 kursach |
| 11 | FOREIGN_ADDRESS | 🟡 Warning | Adres zagraniczny (CMR) |
| 12 | TIME_ORDER | 🟡 Warning | Odwrócona kolejność godzin |
| 13 | RETURN_LATE | 🟡 Warning | Powrót po godzinach |
| 14 | EMPTY_COURSE | 🔵 Info | Pusty kurs |
| 15 | SINGLE_STOP_LOW | 🔵 Info | 1 przystanek, mało towaru |
| 16 | NEARBY_ORDER | 🔵 Info | Blisko zamówienie nieprzypisane |
| 17 | MULTI_HANDLOWIEC | 🔵 Info | Wielu handlowców w kursie |
| 18 | TIME_TIGHT | 🔵 Info | <30 min między przystankami |

## Paleta kolorów (skrót)

| Element | Kolor | HEX | C# |
|---------|-------|-----|-----|
| Ciemny panel | Charcoal | #2B2D42 | Color.FromArgb(43,45,66) |
| Zielony accent | Green | #43A047 | Color.FromArgb(67,160,71) |
| Fioletowy accent | Purple | #7B1FA2 | Color.FromArgb(123,31,162) |
| Zaznaczony wiersz | Lavender | #E8D5F5 | Color.FromArgb(232,213,245) |
| Pomarańczowy | Orange | #F57C00 | Color.FromArgb(245,124,0) |
| Czerwony alarm | Red | #E53935 | Color.FromArgb(229,57,53) |
| Niebieski info | Blue | #1E88E5 | Color.FromArgb(30,136,229) |

## Skróty klawiszowe (do implementacji)

| Klawisz | Akcja |
|---------|-------|
| Enter / DblClick | Dodaj zamówienie do kursu |
| Delete | Usuń ładunek z kursu |
| Ctrl+S | Zapisz kurs |
| Ctrl+Z | Cofnij |
| ↑↓ | Nawigacja |
| Alt+↑↓ | Zmień kolejność |
| Spacja | Zaznacz zamówienie |
| Ctrl+F | Szukaj klienta |
| F5 | Odśwież zamówienia |
