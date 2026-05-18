# 26 — Moduł Zamówień v2 — kompletna wiedza techniczna

> **Status:** 2026-05-09. Ten dokument jest pełnym źródłem prawdy o module zamówień po refactorze przeprowadzonym w tej sesji.
>
> **Co tu jest:** architektura nowego okna zamówienia, system awatarów, integracja z Sage Symfonia (CDim_Handlowiec triggery), system propozycji notatek z auto-uczeniem, wszystkie gotchas, paths, wzorce kodu.
>
> **Po co:** w przyszłej rozmowie nie zaczynaj od zera. Wszystko co odkryliśmy o ContractorClassification, INSTEAD OF triggerach, UserAvatarManager, smart rankingu notatek — jest tu.

---

## 1. Stary vs nowy moduł zamówień

### Stary moduł (USUNIĘTY 2026-05-09)
- **Plik:** `Zamowienia/WidokZamowienia.cs` (.Designer.cs, .resx) — fizycznie usunięte z dysku.
- **Stack:** WinForms Form, ~1700+ linii code-behind, DataTable-driven.
- **Konstruktory:** `WidokZamowienia()`, `WidokZamowienia(int? id)`, `WidokZamowienia(string userId, int? id)`.
- **Funkcjonalność:** lista pozycji w DataGridView, edycja inline, zapisz/anuluj/duplikuj, snapshot pre-edit do `ZamowieniaMiesoSnapshot`, flagi `CzyZmodyfikowaneDla*`.

### Nowy moduł (aktualny)
- **Plik:** `Zamowienia/Views/NoweZamowienieTestWindow.xaml(.cs)` (~2400+ linii).
- **Stack:** WPF Window, code-behind (zgodnie z konwencją projektu — bez MVVM).
- **Layout:** 2-kolumnowy (lewy panel 320px sticky + prawy panel produkty *), bez headera, products attached do top.
- **Cechy:** kompaktowe karty produktów (Width=300), live-recalc kg↔poj↔pal, real avatars handlowca, 4-kolumnowy confirm overlay, smart suggestions notatek.

### Migracja wszystkich call-site'ów (8 miejsc)

Wszystkie `new WidokZamowienia(...)` zostały przekierowane na `new Kalendarz1.Zamowienia.Views.NoweZamowienieTestWindow(...)`:

| Plik:Linia | Tryb | Owner pattern |
|---|---|---|
| `Zamowienia/WidokZamowieniaPodsumowanie.cs:1094` | Nowe (null orderId) | `WindowInteropHelper.Owner = this.Handle` (WinForms→WPF) |
| `Zamowienia/WidokZamowieniaPodsumowanie.cs:1120` | Edytuj | jak wyżej |
| `Zamowienia/WidokZamowieniaPodsumowanie.cs:1999` | Edytuj (CellDoubleClick) | jak wyżej |
| `Transport/transport-editor.cs:3041` | Edytuj (WolneZamowienia) | `WindowInteropHelper.Owner = this.Handle` |
| `Transport/transport-editor.cs:3082` | Edytuj (Ladunki) | jak wyżej |
| `WPF/MainWindow.xaml.cs:1487` | Nowe (BtnNew_Click) | `win.Owner = this` |
| `WPF/MainWindow.xaml.cs:2480` | Edytuj | jak wyżej |
| `WPF/MainWindow.xaml.cs:3264` | Edytuj (DoubleClick) | jak wyżej |
| `WPF/HistoriaZmianWindow.xaml.cs:529` | Edytuj | jak wyżej |
| `WPF/DashboardWindow.xaml.cs:5565` | Edytuj | jak wyżej |
| `WPF/PanelFakturWindow.xaml.cs:417` | Nowe | jak wyżej |
| `WPF/PanelFakturWindow.xaml.cs:2249` | Edytuj | jak wyżej |

**Wzorzec WinForms → WPF dialog bridge:**
```csharp
var win = new Kalendarz1.Zamowienia.Views.NoweZamowienieTestWindow(userId, orderId);
new System.Windows.Interop.WindowInteropHelper(win) { Owner = this.Handle };
if (win.ShowDialog() == true) { ... }   // bool? zamiast DialogResult
```

**Usunięte:**
- `<Compile Update="WidokZamowienia.cs" />` z `Kalendarz1.csproj`.
- Wpis `WidokZamowienia` w `WindowIconHelper.cs` ikonografii.

---

## 2. NoweZamowienieTestWindow — architektura

### Konstruktory
```csharp
public NoweZamowienieTestWindow(string userId) : this(userId, null) { }
public NoweZamowienieTestWindow(string userId, int? orderId)
```

`_editOrderId` (`int?`) + `_isEditMode` (computed) sterują logiką edit vs new.

### Window_Loaded — paralelizacja (kluczowa optymalizacja)
```csharp
// 4 niezależne loady równolegle (różne DB, brak współdzielonego stanu)
var tHandlowcy = LoadUserHandlowcyAsync();   // LibraNet
var tKontr     = LoadKontrahenciAsync();      // Handel
var tObc       = LoadObciazeniaDniAsync();    // LibraNet
var tProd      = LoadProductsAsync();         // Handel
await Task.WhenAll(tHandlowcy, tKontr, tObc, tProd);

// Zależy od _kontrahenci
await LoadOstatnieZamowieniaAsync();

// UI render (bez obrazków)
RenderCustomers(); RenderDaysProd(); RenderDays(); RenderHours();
RenderProducts(); UpdateValidation(); UpdateTermDisplay(); RebuildCart();

// Edit-mode: nadpisz stan istniejącym zamówieniem
if (_isEditMode) await LoadExistingOrderAsync(_editOrderId.Value);

// Tło: ciężki BLOB image load. INPC odświeży bindings.
_ = LoadProductImagesAsync();
```

**Wynik:** wall-clock = max(4 loadów) + ostatnie + render ~ 1-2s zamiast sumy ~6-8s.

### LoadProductsAsync — gotcha `HM.TW.katalog`
```csharp
// HM.TW.katalog jest INT (nie string!) → CAST przed czytaniem
const string sql = @"SELECT Id, Kod, CAST(katalog AS NVARCHAR(32)) AS Katalog
                     FROM [HANDEL].[HM].[TW]
                     WHERE katalog IN ('67095','67153')
                     ORDER BY Kod ASC";
// rd.GetString(2) by rzucił InvalidCastException — używaj rd["Katalog"]?.ToString()
```
**Połączenie 2 katalogów w 1 query** = 1 round-trip zamiast 2.

### LoadKontrahenciAsync — fold LimitAmount
Dodaliśmy `c.LimitAmount` do SELECT z `STContractors`, eliminując osobne query w `LoadOstatnieZamowieniaAsync`. Zmniejsza opóźnienie po wybraniu klienta.

### LoadProductImagesAsync — defer + INPC
- Wczytywany **fire-and-forget** po pierwszym renderze (`_ = LoadProductImagesAsync()`).
- `ProductVm.ImageSource`, `HasImageVisibility`, `PlaceholderVisibility` mają **INPC** — bindings odświeżają się automatycznie gdy obrazek dotrze.
- BitmapImage używa `DecodePixelWidth = 240` + `Freeze()` — gotowe do tła i UI thread.

### LoadExistingOrderAsync — wczytywanie zamówienia do edycji

Pełny flow przy `_isEditMode`:
1. Query `ZamowieniaMieso` (header): `KlientId`, `DataPrzyjazdu`, `Uwagi`, `TransportStatus`, `TrybE2`, `DataProdukcji?`.
2. Query `ZamowieniaMiesoTowar` (items): `KodTowaru`, `Ilosc`, `Cena`, `E2`, `Folia`, `Hallal`, `Strefa?`.
3. Set `_wybranaData`, `_wybranaGodzina`, `_dataProdukcji`.
4. Find customer w `_kontrahenci` przez ID, wywołaj `ApplySelectedCustomerAsync()`.
5. Set `TxtUwagi.Text`, `ChkWlasnyOdbior.IsChecked`, `LblGodzinaHeader`.
6. Czyść `_produkty` (set QtyKg=0, opcje=false), set tylko te z zamówienia.
7. `RecalcProductDisplay` na każdym z QtyKg>0.
8. Re-render: `RenderDays`, `RenderHours`, `RenderProducts`, `RebuildCart`, `UpdateTermDisplay`, `UpdateValidation`.

### UpdateValidation — gotcha edit-mode dla starych zamówień
```csharp
// W trybie edycji dopuszczamy przeszłe daty (zamówienie już istnieje)
bool hasTerm = _isEditMode || _wybranaData.Date >= DateTime.Today;
```
Bez tego edytując zamówienie z wczoraj, BtnSave był stale disabled.

### SaveOrderAsync — branch INSERT vs UPDATE z pełnym snapshotem

**INSERT (nowe):**
```sql
SELECT ISNULL(MAX(Id), 0) + 1 FROM dbo.ZamowieniaMieso  -- ręczne ID (nie IDENTITY!)
INSERT INTO dbo.ZamowieniaMieso (Id, DataZamowienia, ..., TransportStatus) VALUES (...)
INSERT INTO dbo.ZamowieniaMiesoTowar (...) VALUES (...) -- per pozycja
```

**UPDATE (edycja):**
```sql
UPDATE [dbo].[ZamowieniaMieso] SET
    DataZamowienia = @dz, DataPrzyjazdu = @dp, KlientId = @kid, Uwagi = @uw,
    KtoMod = @km, KiedyMod = SYSDATETIME(), LiczbaPojemnikow = @poj,
    LiczbaPalet = @pal, TrybE2 = @e2, TransportStatus = @ts,
    CzyZmodyfikowaneDlaFaktur = 1, DataOstatniejModyfikacji = SYSDATETIME(),
    ModyfikowalPrzez = @fullName,
    CzyZmodyfikowaneDlaMagazynu = 1,    -- jeśli kolumna istnieje
    CzyZmodyfikowaneDlaProdukcji = 1,   -- jeśli kolumna istnieje
    UwagiSnapshot = CASE WHEN UwagiSnapshot IS NULL THEN Uwagi ELSE UwagiSnapshot END
WHERE Id = @id
-- + Snapshot pre-edit do ZamowieniaMiesoSnapshot (typ='Realizacja', tylko jeśli nie istnieje)
-- + DELETE FROM ZamowieniaMiesoTowar WHERE ZamowienieId=@id
-- + INSERT pozycji (re-utworzenie)
```

`ZamowieniaMiesoSnapshot` jest **auto-tworzony** w transakcji jeśli nie istnieje — patrz code w `SaveOrderAsync` (`IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE name='ZamowieniaMiesoSnapshot' AND type='U')`).

`ColumnExistsAsync(cn, table, column)` — feature-detect dla kolumn opcjonalnych: `DataProdukcji`, `DataUboju`, `Strefa`, `CzyZmodyfikowaneDlaMagazynu`, `CzyZmodyfikowaneDlaProdukcji`, `UwagiSnapshot`.

### Hot keys (PreviewKeyDown)
| Klawisz | Akcja |
|---|---|
| `Esc` | Anuluj / zamknij popup hotkeys |
| `Ctrl+S` | Zapisz (jeśli BtnSave.IsEnabled) |
| `Ctrl+F` | Focus + SelectAll na `TxtCustSearch` |
| `Ctrl+R` | Powtórz ostatnie zamówienie tego klienta |
| `Ctrl+1` | Katalog 🥩 Świeże (67095) |
| `Ctrl+2` | Katalog ❄ Mrożone (67153) |
| `Ctrl+N` | Wyczyść koszyk (z confirm) |
| `F1` | Pokaż/ukryj popup z listą hotkeyów |

Lista skrótów dostępna z `BtnHotkeysHelp` (ToggleButton) pod przyciskami Anuluj/Zapisz, otwiera Popup z grid'em.

### ConfirmOverlay — overlay potwierdzenia

- **Layout:** outer Border `MaxWidth=1500`, `Margin=30`, `VerticalAlignment=Stretch` (zamiast MaxHeight=800 — okno mogło być węższe od MinHeight, button cut).
- **Sticky bottom Grid:** `Grid.Row="0"` zawiera `ScrollViewer` ze wszystkim, `Grid.Row="1"` zawiera Anuluj+Zapisz przyciski. Przyciski **zawsze widoczne** niezależnie od liczby produktów.
- **Towary:** `UniformGrid Columns="4"` (zmienione z 2). Mniej rzędów, mniej scrollowania.
- **Tydzień:** `UniformGrid Columns="7"` (Pon-Nd) z markerami prod/odbior + godziną pod 🚚.

### Hidden proxies block
Sekcja `<Grid Visibility="Collapsed" Width="0" Height="0">` na końcu XAML zawiera puste kontrolki z legacy `x:Name` (np. `BtnTerminChip`, `ChipPalety`, `CustomerStrip`, `BtnStep2`). Nie usuwać — code-behind ich używa do unikania `NullReferenceException` przy starych referencjach. Każda nowa zmiana XAML nieusuwająca name z code-behind powinna być raczej tu dodana niż wywalona.

---

## 3. System awatarów — UserAvatarManager

### Lokalizacja
**Plik:** `UserAvatarManager.cs` (root projektu) — static class.

### Storage (network-first)
```csharp
private static readonly string NetworkAvatarsPath1 = @"\\192.168.0.170\Install\Prace Graficzne\Avatary";
private static readonly string NetworkAvatarsPath2 = @"\\192.168.0.171\Install\Prace Graficzne\Avatary";
```

- **Avatar lokalny** w `%AppData%/ZPSP/Avatars/{userId}.png` — używany jako **fallback** historyczny, ale aktywny kod **zawsze** szuka na sieciowym serwerze (170 priorytet, 171 backup).
- Rozszerzenia akceptowane: `.png`, `.jpg`, `.jpeg`, `.bmp` (przy zapisie zawsze jako `.png`).
- `ResizeAndCropToSquare(image, 128)` — zapisywany rozmiar 128×128.

### Public API
```csharp
bool HasAvatar(string userId)                              // true jeśli plik na network exists
Image? GetAvatar(string userId)                            // System.Drawing.Image, dispose obowiązkowo
Image? GetAvatarRounded(string userId, int size)           // 32, 36, 40, 64 itd. — okrągły
Image GenerateDefaultAvatar(string name, string seedId, int size)  // generuje gradient z inicjałami
bool SaveAvatar(string userId, string sourceImagePath)
bool SaveAvatar(string userId, Image image)
bool DeleteAvatar(string userId)
```

### Konwersja Image → BitmapSource (WPF)

UserAvatarManager zwraca `System.Drawing.Image` (GDI+). Aby użyć w WPF jako `ImageBrush`:

```csharp
private static System.Windows.Media.Imaging.BitmapSource ConvertToBitmapSource(System.Drawing.Image img)
{
    using var bmp = new System.Drawing.Bitmap(img);
    var hbm = bmp.GetHbitmap();
    try
    {
        var bs = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
            hbm, IntPtr.Zero, System.Windows.Int32Rect.Empty,
            System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
        bs.Freeze();
        return bs;
    }
    finally { DeleteObject(hbm); }   // P/Invoke gdi32.DeleteObject — KRYTYCZNE inaczej leak!
}
[System.Runtime.InteropServices.DllImport("gdi32.dll")]
private static extern bool DeleteObject(IntPtr hObject);
```

### Zastosowanie w nowym oknie zamówienia

**`_handlowiecAvatarCache`** (`Dictionary<string, BitmapSource>`) — cache per `HandlowiecName`, frozen, aby nie ładować z sieci wielokrotnie.

```csharp
private void EnsureHandlowiecAvatarCached(string handlowiec, int size = 64)
{
    if (_handlowiecAvatarCache.ContainsKey(handlowiec)) return;
    if (_handlowiecMapowanie.TryGetValue(handlowiec, out var uid))   // mapping nazwa→userId z UserHandlowcy
    {
        if (UserAvatarManager.HasAvatar(uid))
            using (var av = UserAvatarManager.GetAvatarRounded(uid, size))
                if (av != null) bmp = ConvertToBitmapSource(av);
        if (bmp == null)
            using (var defAv = UserAvatarManager.GenerateDefaultAvatar(handlowiec, uid, size))
                bmp = ConvertToBitmapSource(defAv);
    }
    // ...
}
```

**`ApplyHandlowiecAvatar(Border avatarBorder, TextBlock initialsText, string handlowiec)`** — ustawia `ImageBrush` na Background bordera:

```csharp
avatarBorder.Background = new ImageBrush(_handlowiecAvatarCache[handlowiec])
{
    Stretch = Stretch.UniformToFill
};
initialsText.Visibility = Visibility.Collapsed;   // schowaj inicjały bo mamy avatar
```

### Tabela `UserHandlowcy` (LibraNet)

**Klucz:** mapuje `HandlowiecName` (z Sage `CDim_Handlowiec_Val`) na `UserID` ZPSP-owy. Bez tego nie ma jak wczytać avatara dla handlowca z Symfonii.

```sql
LibraNet.dbo.UserHandlowcy:
  HandlowiecName NVARCHAR  -- np. "Justyna", "Maja", "Marcin K"
  UserID         NVARCHAR  -- np. "432143", "6521" — zgodne z App.UserID
```

**Manager:** `UserHandlowcyManager` (legacy) — `GetUserHandlowcy(userId)`, `AddHandlowiecToUser(userId, handlowiec, env)`, `RemoveHandlowiecFromUser`, `GetAvailableHandlowcy()`.

W nowym oknie zamówienia używamy bezpośrednio:
```csharp
_handlowiecMapowanie = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
// ...
SELECT HandlowiecName, UserID FROM UserHandlowcy
// → fill _handlowiecMapowanie[handlowiec] = userId
```

`_userHandlowcy` (`HashSet<string>`) — handlowcy aktualnego usera (do priorytetyzacji listy klientów: "moi klienci na górze").

---

## 4. Sage Symfonia — `SSCommon.ContractorClassification` (deep)

### Schema (zweryfikowana 2026-05-09)

```
HANDEL.SSCommon.ContractorClassification (USER_TABLE):
  Guid                                              uniqueidentifier  not null  PK?
  ElementId                                         int               not null  -- = STContractors.id
  st_last_modified                                  datetime          null
  st_shadowdata                                     varbinary(MAX)    null
  CDim_pojHM_6770_1                                 nvarchar(1000)    null      -- inny wymiar (pojemniki?)
  CDim_Blokuj#wysyłanie#powiadomień#o#płatnościach  bit               null
  CDim_Handlowiec                                   int               null      -- ⭐ FK do słownika wymiarów
  CDim_Handlowiec_Val                               nvarchar(1000)    null      -- denormalizowana wartość
  CDim_Kilometry                                    smallint          null
```

### 🚨 INSTEAD OF triggery (gotcha krytyczna!)

Tabela ma **3 triggery** Sage'a:
| Trigger | Typ | Skutek |
|---|---|---|
| `ContractorClassification_TH_IOI` | INSTEAD OF INSERT | Przejmuje INSERT, zapisuje swoją logiką |
| `ContractorClassification_TH_IOU` | INSTEAD OF UPDATE | **Klucz** — UPDATE jest przejmowany |
| `ContractorClassification_TH_AD`  | AFTER DELETE | Cleanup |

**Konsekwencja praktyczna:**
- `UPDATE ... SET CDim_Handlowiec_Val = 'Ania'` **NIE DZIAŁA** — trigger TH_IOU widzi że `CDim_Handlowiec` (FK) jest puste/niezmienione, i nadpisuje `_Val` przez JOIN do słownika wymiarów (NULL).
- Wynik: rowsAffected=1, wartość po UPDATE = '' (pusta).

### Strategia poprawnego UPSERT

```sql
-- 1. Pobierz CDim_Handlowiec (int FK) z istniejącego klienta z tym samym handlowcem
SELECT TOP 1 CDim_Handlowiec
FROM [HANDEL].[SSCommon].[ContractorClassification]
WHERE CDim_Handlowiec_Val = @nazwaHandlowca AND CDim_Handlowiec IS NOT NULL
-- np. dla "Ania" → 19985

-- 2. Update OBYDWU pól (trigger zaakceptuje bo FK jest valid)
IF EXISTS (SELECT 1 FROM [HANDEL].[SSCommon].[ContractorClassification] WHERE ElementId = @id)
    UPDATE [HANDEL].[SSCommon].[ContractorClassification]
       SET CDim_Handlowiec = @hid, CDim_Handlowiec_Val = @h
     WHERE ElementId = @id;
ELSE
    INSERT INTO [HANDEL].[SSCommon].[ContractorClassification]
        (Guid, ElementId, CDim_Handlowiec, CDim_Handlowiec_Val)
    VALUES (NEWID(), @id, @hid, @h);
```

**Limit:** można przypisać tylko handlowca, który **już istnieje** w słowniku (czyli przynajmniej jeden kontrahent ma go przypisanego). Tworzenie nowych elementów wymiaru w Sage Symfonia to operacja admin-konsolowa z dodatkowymi polami (kod, nazwa, hierarchia, kolejność) i nie da się tego bezpiecznie zrobić z aplikacji.

### Cache `_cachedKontrahenci` w `WPF/MainWindow.xaml.cs` — pułapka

```csharp
private Dictionary<int, (string Name, string Salesman)> _cachedKontrahenci = new();
private DateTime _cachedKontrahenciTime = DateTime.MinValue;
// 5-minute TTL — kontrahenci.Count > 0 && (Now - cachedKontrahenciTime).TotalMinutes < 5
```

**Pułapka:** po zmianie handlowca w Symfonii, `RefreshAllDataAsync()` używa cache i nie widzi zmiany.

**Fix:** po UPSERT zawsze:
```csharp
_cachedKontrahenci.Clear();
_cachedKontrahenciTime = DateTime.MinValue;
await RefreshAllDataAsync();
```

### JOIN konwencja kontrahent ↔ klasyfikacja

**Standard projektu (~50 miejsc):**
```sql
LEFT JOIN [HANDEL].[SSCommon].[ContractorClassification] WYM ON c.Id = WYM.ElementId
-- albo (równoważnie, inne aliasy):
LEFT JOIN [HANDEL].[SSCommon].[ContractorClassification] WYM ON DK.khid = WYM.ElementId
```

**`ElementId = STContractors.id` (= klient ID).** NIE używaj `MainElement` (to inna konwencja Sage'a, nie ta w naszej instancji).

---

## 5. Context menu „Przypisz handlowca…"

Funkcja zmiany handlowca w 2 oknach (różne tech stacki):

### A. WPF: `WPF/MainWindow.xaml`
**Otwierane z:** Menu → Zamówienia Klientów → kliknij prawym myszki na zamówienie.
- XAML: `<MenuItem x:Name="menuPrzypiszHandlowca" Header="👤 Przypisz handlowca…" Click="MenuPrzypiszHandlowca_Click"/>` w `contextMenuOrders`.
- Handler: `WPF/MainWindow.xaml.cs:MenuPrzypiszHandlowca_Click`.
- Dialog: `WPF/PrzypiszHandlowcaWpfDialog.cs` (programowy WPF Window z editable ComboBox).

### B. WinForms: `Zamowienia/WidokZamowieniaPodsumowanie.cs`
**Otwierane z:** stary widok zamówień. Mniej używany, ale zachowany dla zgodności.
- Menu item dodany w `UtworzMenuKontekstowe()` między „Notatka" a „Historia zmian".
- Handler: `PrzypiszHandlowcaAsync()`.
- Dialog: `PrzypiszHandlowcaDialog` (zagnieżdżona klasa WinForms Form).

### Diagnostyka schematu (in-app)
W `MenuPrzypiszHandlowca_Click` przy nieudanym zapisie wywołuje się `PokazDiagnostykeContractorClassificationAsync(klientId, nowy, verified, rowsAffected)`:
1. Typ obiektu (TABLE/VIEW)
2. Wszystkie kolumny + `is_computed`, `is_nullable`
3. Triggery (`type_desc`, `is_instead_of_trigger`)
4. Liczba wierszy dla `ElementId`
5. Pełny wiersz dla tego klienta
6. Wzorcowy „działający" wiersz dla tego handlowca

Dump trafia do `TextBox` w nowym Window (Consolas, czytelnie), automatycznie kopiowany do Clipboard. To narzędzie pozwala szybko zdiagnozować zmianę schematu Symfonii w przyszłości.

---

## 6. System propozycji notatek (Smart Suggestions v2)

### Tabele (LibraNet, auto-create na pierwszym uruchomieniu)

```sql
-- 1. Szablony tworzone przez handlowców + auto-backfill z historii
CREATE TABLE dbo.NotatkiSzablony (
    Id INT IDENTITY PRIMARY KEY,
    Tekst NVARCHAR(500) NOT NULL,
    Kategoria NVARCHAR(40) NULL,             -- Cięcie/Kaliber/Transport/Jakość/Pakowanie/Inne
    Zakres NVARCHAR(20) NOT NULL DEFAULT 'Globalny',  -- Globalny/PerKlient/PerHandlowiec
    KlientId INT NULL,                        -- gdy Zakres='PerKlient'
    UserId NVARCHAR(50) NULL,                 -- gdy Zakres='PerHandlowiec'
    Pinowane BIT NOT NULL DEFAULT 0,
    LiczbaUzyc INT NOT NULL DEFAULT 0,
    OstatnieUzycie DATETIME NULL,
    UtworzonoPrzez NVARCHAR(50) NULL,
    UtworzonoTsmp DATETIME NOT NULL DEFAULT GETDATE(),
    Aktywne BIT NOT NULL DEFAULT 1
);
CREATE INDEX IX_NotatkiSzablony_Zakres ON dbo.NotatkiSzablony(Zakres, Aktywne);
CREATE INDEX IX_NotatkiSzablony_Klient ON dbo.NotatkiSzablony(KlientId, Aktywne);
CREATE INDEX IX_NotatkiSzablony_User   ON dbo.NotatkiSzablony(UserId, Aktywne);

-- 2. Log użyć (auto-learning + analytics)
CREATE TABLE dbo.NotatkiUzycia (
    Id INT IDENTITY PRIMARY KEY,
    Tekst NVARCHAR(500) NOT NULL,
    KlientId INT NULL,
    UserId NVARCHAR(50) NULL,
    Akcja NVARCHAR(20) NOT NULL,              -- 'Wstawiona' (klik chip) | 'Wpisana' (zapis zamówienia)
    TowaryKody NVARCHAR(500) NULL,            -- comma-separated
    SzablonId INT NULL,                       -- gdy klik na szablon
    DataAkcji DATETIME NOT NULL DEFAULT GETDATE()
);
CREATE INDEX IX_NotatkiUzycia_Tekst  ON dbo.NotatkiUzycia(Tekst);
CREATE INDEX IX_NotatkiUzycia_Klient ON dbo.NotatkiUzycia(KlientId);
```

### Auto-backfill (przy pustej `NotatkiSzablony`)
Jednorazowo importuje TOP 30 najpopularniejszych notatek z `ZamowieniaMieso.Uwagi` ostatnich 6 miesięcy (≥5 powtórzeń) jako globalne aktywne szablony. Handlowiec od pierwszego uruchomienia ma działające propozycje.

### `NotatkiService` (`Zamowienia/Services/NotatkiService.cs`)

```csharp
public class NotatkiService
{
    public async Task EnsureSchemaAsync()                                       // DDL + backfill
    public async Task<List<SuggestionVm>> GetSuggestionsAsync(
        int klientId, string userId, IEnumerable<int> kodyTowarowWKoszyku, int maxResults = 18)
    public async Task<int> SaveTemplateAsync(
        string tekst, string kategoria, string zakres,
        int? klientId, string userId, bool pinowane, string utworzonyPrzez)
    public async Task LogUsageAsync(
        string tekst, int? klientId, string userId, string akcja,
        IEnumerable<int> towary, int? szablonId)
}
```

### Smart ranking (kluczowy algorytm)

Wynik **multiplikatywny** — każdy czynnik ≥1.0:

| Czynnik | Boost | Komentarz |
|---|---:|---|
| Pin | ×5 | Przypięte zawsze na górze |
| Zakres `PerKlient` | ×4 | Maksymalnie celne |
| Zakres `PerHandlowiec` | ×2.5 | Mój własny |
| Klient match (≥1 raz w historii) | ×3 + log boost | Najsilniejszy sygnał z historii |
| User match | ×1.5 | Ten sam handlowiec już używał |
| Recency | ×(1 + 2·exp(-dni/30)) | Eksponencjalny zanik (30 dni waga 1, 60 dni 0.4) |
| Frequency | ×(1 + log(1+count)/log(50)) | Cap na 50 użyć |
| Towary (Jaccard) | ×(1 + 1.5·jaccard) | Max ×2.5 przy idealnym dopasowaniu koszyka |

**Algorytm szczegółowo:**

```
score = 1.0
if Pinned: score *= 5.0
if Zakres == PerKlient: score *= 4.0
elif Zakres == PerHandlowiec: score *= 2.5
if OstatnieUzycie: score *= 1.0 + 2.0 * exp(-days_since/30)
score *= 1.0 + log(1 + LiczbaUzyc) / log(50)
# Sygnały z historii:
if dlaKlienta > 0: score *= 1.5 + log(1+dlaKlienta)/log(20)
if dlaUsera > 0: score *= 1.2
score *= 1.0 + exp(-days_since_last_history/30)
# Towary (boost po prelim score, dla TOP 50 kandydatów):
if jaccard(koszyk, historiaTowarow) > 0:
    score *= 1.0 + 1.5 * jaccard
```

### Kolory chipów per źródło

| Source | Icon | Background | Border |
|---|---|---|---|
| Pin | 📌 | `#FFF8DC` | `#E0B040` |
| Towary (jaccard match) | 🛒 | `#FFEFD5` | `#FFB347` |
| Szablon (z `NotatkiSzablony`) | ⭐ | `#E8F1DC` | `#C5DDA8` |
| Z historii (z `ZamowieniaMieso.Uwagi`) | 📋 | `#F0F4F8` | `#CBD5E0` |

### UI — gdzie jest co

- **Chipy:** `ListNoteSuggestions` (ItemsControl + WrapPanel) pod `TxtUwagi`. Display = "ICON tekst (do 50 znaków + …)".
- **Tooltip:** `[Source · użyć: N] · Kategoria\nPełny tekst notatki`.
- **Klik chipa:** wstawia do `TxtUwagi`. Jeśli pole puste → set, jeśli ma tekst → append `" / "` (separator wybrany przez Sergiusza zamiast `\n`).
- **Tracking:** `LogUsageAsync(Akcja='Wstawiona')` + auto-bump `LiczbaUzyc`/`OstatnieUzycie` szablonu jeśli `SzablonId.HasValue`.
- **Re-ranking live:** `RebuildCart()` woła `ScheduleSuggestionsReload()` (DispatcherTimer 800ms debounce) → `LoadNoteSuggestionsAsync()` re-pobiera z aktualnym koszykiem.
- **Wpisana ręcznie:** przy zapisie zamówienia (`BtnConfirmSave_Click` → po `SaveOrderAsync`) loguje `Akcja='Wpisana'` z towarami w koszyku. **Sygnał** że istniejące propozycje nie wystarczyły — przyszła analiza może auto-promować powtarzające się.

### Dialogs

**`Zamowienia/Views/ZapiszSzablonNotatkiDialog.cs`** (programowy, bez XAML):
- Pola: TextBox tekst, ComboBox kategoria, 3× RadioButton zakres (Globalny/PerKlient/PerHandlowiec), CheckBox pin.
- PerKlient disabled gdy klient niewybrany.
- PerHandlowiec disabled gdy userId pusty.

**`Zamowienia/Views/ZarzadzanieSzablonamiNotatekWindow.cs`** (programowy):
- Otwierany z przycisku „⚙" obok „💾 Zapisz".
- DataGrid z kolumnami: Id, 📌, Tekst (TwoWay), Kategoria (combo), Zakres (combo), KlientId, UserId, Użyć (read-only), Ostatnio (read-only), Aktywne (checkbox).
- Filter: TextBox + 2× ComboBox (kategoria, zakres).
- Akcje: 💾 Zapisz zmiany (UPDATE wszystkich wierszy), 🗑 Usuń zaznaczone (DELETE z confirm).

---

## 7. Pożyteczne SQL queries (referencyjne)

### Top 30 najczęstszych notatek (analiza historyczna)
```sql
SELECT TOP 30
    LTRIM(RTRIM(z.Uwagi)) AS Notatka,
    COUNT(*) AS Powtorzen,
    COUNT(DISTINCT z.KlientId) AS RoznychKlientow,
    COUNT(DISTINCT z.IdUser) AS RoznychHandlowcow,
    MIN(z.DataZamowienia) AS PierwszeUzycie,
    MAX(z.DataZamowienia) AS OstatnieUzycie
FROM dbo.ZamowieniaMieso z
WHERE z.Uwagi IS NOT NULL
  AND LTRIM(RTRIM(z.Uwagi)) <> ''
  AND ISNULL(z.Status, '') NOT IN ('Anulowane', 'Anulowano')
  AND LEN(LTRIM(RTRIM(z.Uwagi))) BETWEEN 3 AND 200
GROUP BY LTRIM(RTRIM(z.Uwagi))
HAVING COUNT(*) >= 2
ORDER BY Powtorzen DESC, OstatnieUzycie DESC;
```

### Pełny historyczny dump (notatki + co zamówili)
```sql
SELECT
    z.Id, z.DataZamowienia, z.DataPrzyjazdu, z.KlientId,
    z.IdUser AS Handlowiec, z.LiczbaPalet, z.LiczbaPojemnikow, z.TrybE2,
    z.TransportStatus, LEN(z.Uwagi) AS DlugoscNotatki, z.Uwagi AS Notatka,
    -- Pozycje jako jedna linia: "KOD x ILOSC kg [E2/Folia/Hallal/Strefa]; ..."
    STUFF((
        SELECT '; ' + CAST(zt.KodTowaru AS NVARCHAR(20))
             + ' x ' + LTRIM(STR(CAST(zt.Ilosc AS DECIMAL(10,1)), 10, 1)) + 'kg'
             + CASE WHEN ISNULL(zt.E2,0)=1 THEN ' E2' ELSE '' END
             + CASE WHEN ISNULL(zt.Folia,0)=1 THEN ' Folia' ELSE '' END
             + CASE WHEN ISNULL(zt.Hallal,0)=1 THEN ' Hallal' ELSE '' END
             + CASE WHEN ISNULL(zt.Strefa,0)=1 THEN ' Strefa' ELSE '' END
        FROM dbo.ZamowieniaMiesoTowar zt
        WHERE zt.ZamowienieId = z.Id AND zt.Ilosc > 0
        ORDER BY zt.Ilosc DESC
        FOR XML PATH(''), TYPE
    ).value('.', 'NVARCHAR(MAX)'), 1, 2, '') AS Pozycje
FROM dbo.ZamowieniaMieso z
WHERE z.Uwagi IS NOT NULL AND LTRIM(RTRIM(z.Uwagi)) <> ''
  AND ISNULL(z.Status, '') NOT IN ('Anulowane', 'Anulowano')
ORDER BY z.DataZamowienia DESC;
```

### Korelacja notatka × towar (kontekstowe sugestie)
```sql
WITH NotItems AS (
    SELECT LTRIM(RTRIM(z.Uwagi)) AS Notatka, zt.KodTowaru, zt.Ilosc
    FROM dbo.ZamowieniaMieso z
    INNER JOIN dbo.ZamowieniaMiesoTowar zt ON zt.ZamowienieId = z.Id
    WHERE z.Uwagi IS NOT NULL AND LTRIM(RTRIM(z.Uwagi)) <> ''
      AND zt.Ilosc > 0
      AND ISNULL(z.Status, '') NOT IN ('Anulowane', 'Anulowano')
)
SELECT Notatka, KodTowaru,
       COUNT(*) AS WspolnePowtorzenia,
       SUM(Ilosc) AS LacznieKg
FROM NotItems
GROUP BY Notatka, KodTowaru
HAVING COUNT(*) >= 3
ORDER BY Notatka, WspolnePowtorzenia DESC;
```

### Wszyscy handlowcy w Symfonii
```sql
SELECT DISTINCT CDim_Handlowiec_Val, CDim_Handlowiec
FROM [HANDEL].[SSCommon].[ContractorClassification]
WHERE CDim_Handlowiec_Val IS NOT NULL
ORDER BY CDim_Handlowiec_Val;
```

### Sprawdź mapowanie handlowiec → user
```sql
SELECT HandlowiecName, UserID FROM LibraNet.dbo.UserHandlowcy ORDER BY HandlowiecName;
```

---

## 8. Best practices wynikające z sesji

### Kompatybilność z LibraNet (SQL 2008 R2)
- ❌ `STRING_SPLIT` → użyj `CHARINDEX` w pętli lub `XML PATH`
- ❌ `STRING_AGG` → `STUFF + FOR XML PATH('')`
- ❌ `TRY_CONVERT` → `CONVERT` + walidacja .NET
- ❌ `JSON_*` → przetwarzaj w .NET
- ✅ `LEN`, `LTRIM/RTRIM`, `CASE WHEN`, `EXISTS`, `STR`, `FOR XML PATH('')` działają

### Cross-DB query
**Connection string decyduje** który serwer/DB jest „home". Notacja `[HANDEL].[SSCommon].[STContractors]` w query odpalonym przez `_connHandel` (192.168.0.112/Handel) odnosi się do tego samego serwera/bazy — `HANDEL` to nazwa bazy. Działa **tylko** gdy odpalasz query na właściwym connection. Cross-server JOIN-y (LibraNet ↔ Handel) wymagałyby linked server — **nie używaj**, łącz dane w .NET.

### Polish quotes w C# string literals
```csharp
// ❌ Złe — `"` (U+201D right double quote) terminuje string
var msg = "Wykres pokazuje 'dzień roku" (1‑365)";

// ✅ Bezpiecznie — pojedyncze cudzysłowy
var msg = "Wykres pokazuje 'dzień roku' (1-365)";

// ✅ Albo escape
var msg = "Wykres pokazuje \"dzień roku\" (1-365)";
```
**Pre-existing bug w `WidokCenWszystkich.cs:3349`** — naprawiony 2026-05-09.

### MSB3027/MSB3021 (file lock)
**ZAWSZE pre-existing**, nie real compile error. Aplikacja jest uruchomiona, blokuje `bin/Debug/.../Kalendarz1.exe`. Zamknij appkę i rebuilduj. Zignoruj błąd przy weryfikacji buildem — sprawdzaj tylko `error CS*`.

### INPC dla VM-ów dziedziczących
```csharp
public class ProductVm : INotifyPropertyChanged
{
    private ImageSource? _imageSource;
    public ImageSource? ImageSource
    {
        get => _imageSource;
        set { _imageSource = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ImageSource))); }
    }
    // ... podobnie HasImageVisibility, PlaceholderVisibility
    public event PropertyChangedEventHandler? PropertyChanged;
}
```
Dzięki temu **deferred BLOB load** może uzupełnić obrazek w istniejącym renderze bez re-render (kosztownego).

### WPF auto-detect kolumn (feature flag)
```csharp
private async Task<bool> ColumnExistsAsync(SqlConnection cn, string table, string column) =>
    await new SqlCommand($"SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('{table}') AND name='{column}'", cn)
        .ExecuteScalarAsync() != null;
```
Pozwala dodawać kolumny stopniowo (DataProdukcji, Strefa, UwagiSnapshot, CzyZmodyfikowaneDla*) bez breaking change.

### Sticky bottom buttons w overlay
```xml
<Border MaxWidth="1500" Margin="30" VerticalAlignment="Stretch">
  <Grid>
    <Grid.RowDefinitions>
      <RowDefinition Height="*"/>
      <RowDefinition Height="Auto"/>
    </Grid.RowDefinitions>
    <ScrollViewer Grid.Row="0">...</ScrollViewer>
    <Border Grid.Row="1" BorderThickness="0,1,0,0" Padding="0,12,0,0">
      <!-- Anuluj/Zapisz buttons -->
    </Border>
  </Grid>
</Border>
```
Buttony **zawsze widoczne** niezależnie od liczby produktów w scrollu.

---

## 9. Ścieżki plików (referencja szybka)

### Nowy moduł zamówień
- `Zamowienia/Views/NoweZamowienieTestWindow.xaml(.cs)` — główne okno
- `Zamowienia/Services/NotatkiService.cs` — smart suggestions service
- `Zamowienia/Views/ZapiszSzablonNotatkiDialog.cs` — dialog zapisu szablonu
- `Zamowienia/Views/ZarzadzanieSzablonamiNotatekWindow.cs` — okno zarządzania szablonami
- `Zamowienia/WidokZamowieniaPodsumowanie.cs` — lista zamówień (WinForms, niezmieniony, kontekstowe „Przypisz handlowca…")

### WPF "Zamówienia Klientów" (otwierane z menu głównego)
- `WPF/MainWindow.xaml(.cs)` — główne okno (otwierane przez `Menu.cs` MenuItemConfig)
- `WPF/PrzypiszHandlowcaWpfDialog.cs` — dialog WPF dla zmiany handlowca

### Avatar / handlowiec / mapping
- `UserAvatarManager.cs` — static, root projektu
- `UserHandlowcyManager.cs` — manager mappingu handlowiec→userId

### Stary moduł (USUNIĘTY)
- ~~`Zamowienia/WidokZamowienia.cs`~~
- ~~`Zamowienia/WidokZamowienia.Designer.cs`~~
- ~~`Zamowienia/WidokZamowienia.resx`~~

---

## 10. Otwarte tematy / TODO

1. **Auto-promocja powtarzających się ręcznych notatek** — analiza `NotatkiUzycia` typ='Wpisana' + auto-add do `NotatkiSzablony` jeśli ≥3 wystąpienia bez dopasowanego szablonu.
2. **Per-customer dashboardy notatek** — który klient ma najbardziej skomplikowane wymagania (top dłuższych notatek).
3. **Eksport szablonów** — share między instancjami ZPSP (np. JSON do clipboard).
4. **OCR z notatek skanowanych** — gdy przyjdzie zamówienie mailem/faxem.
5. **Linked server LibraNet ↔ Handel** — eliminacja .NET-side JOIN-ów dla dużych zapytań analitycznych. Wymaga koordynacji z adminem SQL.
6. **Migracja LibraNet do SQL 2017+** — odzyska TRY_CONVERT, STRING_SPLIT, JSON. Zmniejszy ilość workaround-ów.

---

**Last updated:** 2026-05-09 — pełna sesja refactoru modułu zamówień.
**Autor wpisu:** Claude (po zakończeniu sesji z Sergiuszem).
**Źródła:**
- bezpośrednie testy w SSMS / app
- diagnostyka `ContractorClassification` 2026-05-09 (TH_IOI/TH_IOU/TH_AD)
- analiza historii `ZamowieniaMieso.Uwagi` (top 30 + per klient + per handlowiec + korelacja z towarami)
- code review wszystkich 12 call-site'ów `WidokZamowienia` przed migracją
