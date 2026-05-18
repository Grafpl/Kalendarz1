# 25. Analityka Pełna v2 — Stan magazynów (sub-tab w Bilansie)

> Dokumentuje rozbudowę modułu Analityka Pełna o nową sub-zakładkę "🏭 Stan magazynów" w karcie "📊 Bilans materiałowy".
> Iteracje refactoru: maj 2026 (~10 commitów).

---

## 1. Cel modułu

Pokazać **wizualnie i kompleksowo** stan produkcji w okresie:
- **Łańcuch produkcji** od żywca do klienta — z wydajnościami i stratami
- **Towary wyprodukowane** (tylko te które przeszły przez produkcję, nie wszystkie z magazynu)
- **Przepływy MM-** między magazynami w stylu Sankey
- Wszystko z **dokumentami Symfonii** w tooltipach

Główne pytania jakie odpowiada:
1. Ile żywca weszło, a ile produktu wyszło?
2. Jaka wydajność uboju i krojenia w okresie?
3. Gdzie poszły poszczególne towary?
4. Czy magazyny są wyzerowane (towar nie utknął)?
5. Które konkretne dokumenty wpłynęły na wynik?

---

## 2. Lokalizacja w UI

```
Menu → ANALITYKA PEŁNA → otwiera AnalitykaPelnaWindow
         ├── 📊 Plan
         ├── 📊 Realizacja
         ├── 📊 Bilans materiałowy  ← TUTAJ
         │     ├── (sub) 📊 Pozycje              ← klasyczna lista
         │     └── (sub) 🏭 Stan magazynów       ← NOWE!
         └── 📊 Wydajność
```

Kontroller: `AnalitykaPelna/Views/WidokWydajnosc.xaml(.cs)`. **TabItem** `📊 Bilans materiałowy` zawiera zagnieżdżony `TabControl` z 2 sub-tabami.

---

## 3. Struktura sub-taba "🏭 Stan magazynów"

```
ScrollViewer
└── StackPanel
    ├── [SEKCJA 1] Łańcuch produkcji    ← flow chain z arrows + wydajności
    ├── [SEKCJA 2] Towary wyprodukowane ← 3-kolumnowy grid kart ze zdjęciami
    └── [SEKCJA 3] Przepływy MM-         ← Sankey-style listing z paskami
```

(Hero KPI strip był sekcją 0, został USUNIĘTY na życzenie użytkownika — wskaźniki są teraz w strzałkach łańcucha.)

---

## 4. SEKCJA 1: Łańcuch produkcji

### 4.1. Wizualnie
5 kafelków (`FlowCardStyle`) ułożonych poziomo, połączonych 4 strzałkami (Border z LinearGradientBrush + Polygon).

```
┌──────────┐ ⚙85.2% ┌──────────┐ 🔪62.3% ┌──────────┐ 📦95.1% ┌──────────┐ 🚚100% ┌──────────┐
│🐔 ŻYWIEC │═══════►│⚙ UBÓJ    │═══════►│🔪 PRODUKC│═══════►│📦 DYSTRYB│═══════►│🚚 KLIENCI│
│500,000kg │strata  │478,000kg │do      │297,000kg │na      │282,000kg │do       │282,000kg │
│45 dok    │22k     │90 dok    │krojenia│180 dok   │dyst.   │230 dok   │klientów │890 dok   │
└──────────┘ (4.6%) └──────────┘        └──────────┘        └──────────┘         └──────────┘

                             ↘ Odgałęzienia z PRODUKCJI:
                               ❄ MROŹNIA  50,000 kg [16.8%]  12 dok
                               🌾 KARMA    8,000 kg [ 2.7%]   5 dok
                               🗑 ODPADY   2,000 kg [ 0.7%]   8 dok
```

### 4.2. Co pokazuje każdy kafelek
- **Ikona** etapu (🐔 / ⚙ / 🔪 / 📦 / 🚚)
- **Nazwa** etapu
- **kg** (Consolas Bold 22px)
- **Liczba dokumentów** Symfonii w okresie
- Hover: cień się pogłębia (`BlurRadius 8 → 18`, `ShadowDepth 3 → 5`)

### 4.3. Strzałki (sankey-style)
Każda strzałka pokazuje:
- **Badge z procentem wydajności** (kolorowane wg normy):
  - ŻYWIEC → UBÓJ: % wyd. uboju (norma 80%, zielony ≥80%, żółty 70-80%, czerwony <70%)
  - UBÓJ → PRODUKCJA: % wyd. krojenia (norma 55%)
  - PRODUKCJA → DYSTRYBUCJA: % przepływu na DYST
  - DYSTRYBUCJA → KLIENCI: % sprzedaży
- **Pasek strzałki** (LinearGradientBrush #94A3B8 → #CBD5E1, 80×14px, DropShadow)
- **Polygon grot** (16×28px)
- **Etykieta opisowa** pod strzałką (np. "478,000 kg • strata 22,000 kg (4.6%)")

### 4.4. Odgałęzienia
3 chipy w `Border` (z kolorami M.MROŹ/KARMA/ODPADY), każda pokazuje:
- Ikonę + nazwę
- kg
- Procent (badge w kolorze etapu)
- Liczbę dokumentów

### 4.5. SQL i model
**Plik**: `WydajnoscService.LoadFlowChainAsync(FiltryAnaliz)`.

```sql
-- 8 zapytań UNION ALL, każde agreguje 1 etap
SELECT 'ZYWIEC', SUM(ABS(MZ.ilosc)), COUNT(DISTINCT MG.id)
FROM HM.MG MG JOIN HM.MZ MZ ON MZ.super = MG.id JOIN HM.TW TW ...
WHERE MG.seria = 'sPZ' AND TW.katalog = 65882
UNION ALL
SELECT 'UBOJ', SUM(...), COUNT(...)  WHERE MG.seria IN ('PWU','sPWU')
UNION ALL
SELECT 'PRODUKCJA', ...                WHERE MG.seria IN ('PWP','sPWP')
...
```

**Model**: `FlowChainSummary` w `StanMagazynuModels.cs` z polami wyliczanymi:
- `WydajnoscUbojuProc` = `Uboj.Kg / Zywiec.Kg × 100`
- `WydajnoscKrojeniaProc` = `Produkcja.Kg / Uboj.Kg × 100`
- `ProcDoMrozniProc`, `ProcDoKarmyProc`, `ProcDoOdpadowProc`
- `StratyUbojuKg`, `StratyUbojuProc`
- `WydajnoscUbojuStatus` ("✓ OK" / "⚠ Niska" / "❌ Bardzo niska")
- `WydajnoscUbojuKolor` (#10B981 / #F59E0B / #DC2626)

---

## 5. SEKCJA 2: Towary wyprodukowane

### 5.1. Filtr "tylko produkcyjne"
**Kluczowy detal**: pokazujemy **tylko towary które kiedykolwiek wystąpiły w PWU/PWP/PPM/PPK** — czyli faktycznie wyprodukowane na produkcji (nie wszystkie z magazynu).

SQL:
```sql
WITH AllOps AS (
    SELECT MZ.kod AS KodPoz, TW.nazwa AS NazwaTw, TW.katalog,
           MG.seria, MG.id AS DokId, MG.kod AS DokKod,
           ABS(MZ.ilosc) AS Kg,
           CASE
               WHEN MG.seria IN ('PWU','sPWU','PWP','sPWP','PPM','sPPM','PPK','sPPK') THEN 'PRODUKCJA'
               WHEN MG.seria IN ('WZ','sWZ','WZ-W','sWZ-W','WZK','sWZK') THEN 'SPRZEDAZ'
               WHEN MG.seria IN ('RWP','sRWP','RWU','sRWU','RPM','sRPM','RPK','sRPK') THEN 'ZUZYCIE'
               ELSE 'INNE'
           END AS Typ
    FROM HM.MZ MZ
    INNER JOIN HM.MG MG ON MG.id = MZ.super
    INNER JOIN HM.TW TW ON TW.id = MZ.idtw
    WHERE MG.anulowany = 0 AND MG.data BETWEEN @DataOd AND @DataDo
),
ProdukcyjnePozycje AS (
    SELECT DISTINCT KodPoz, NazwaTw, Katalog FROM AllOps WHERE Typ = 'PRODUKCJA'
),
PerPozycjaTyp AS (
    SELECT A.KodPoz, A.NazwaTw, A.Katalog, A.Typ,
           SUM(A.Kg) AS Kg,
           COUNT(DISTINCT A.DokId) AS LiczbaDok,
           STRING_AGG(CAST(A.DokKod AS NVARCHAR(MAX)), ', ') WITHIN GROUP (ORDER BY A.DokId) AS NumeryDok
    FROM AllOps A
    INNER JOIN ProdukcyjnePozycje P
        ON P.KodPoz = A.KodPoz AND P.NazwaTw = A.NazwaTw AND P.Katalog = A.Katalog
    WHERE A.Typ IN ('PRODUKCJA', 'SPRZEDAZ', 'ZUZYCIE')
    GROUP BY A.KodPoz, A.NazwaTw, A.Katalog, A.Typ
)
SELECT KodPoz, NazwaTw, Katalog,
       SUM(CASE WHEN Typ = 'PRODUKCJA' THEN Kg ELSE 0 END) AS WyprodukowanoKg,
       SUM(CASE WHEN Typ = 'PRODUKCJA' THEN LiczbaDok ELSE 0 END) AS DokProd,
       MAX(CASE WHEN Typ = 'PRODUKCJA' THEN NumeryDok END) AS NumProd,
       SUM(CASE WHEN Typ = 'SPRZEDAZ' THEN Kg ELSE 0 END) AS SprzedanoKg,
       ...
FROM PerPozycjaTyp
GROUP BY KodPoz, NazwaTw, Katalog;
```

### 5.2. Layout — 3-kolumnowy grid kart

`UniformGrid Columns="3" Rows="0"` — automatycznie układa karty w 3 kolumny.

**Karta** (`MinHeight=220`):
```
┌─────────────────────────────────┐
│  [Image lub ikona kategorii]    │ ← 100px wysokości
│  [badge kategoria]   [badge stat]│ ← w rogach
├─────────────────────────────────┤
│  Kurczak A                       │ ← Bold 14
│  Kurczak A 1.6kg+...             │ ← nazwa nazwa towaru z TW
├─────────────────────────────────┤
│ ⬇250k │ 🔪5k │ ⬆245k             │ ← 3 mini-boxes side-by-side
│ 90 dok│ 8 dok│ 230 dok          │
├─────────────────────────────────┤
│  SALDO              ≈ 0 kg       │ ← Footer z saldo
└─────────────────────────────────┘
```

### 5.3. Zdjęcia towarów

**Mechanizm**:
- Folder: `Assets/Towary/{kod}.{jpg|png|jpeg|webp}`
- Sanityzacja `kod` przez `Path.GetInvalidFileNameChars()` przed użyciem jako nazwa pliku
- W kodzie: `WydajnoscService.ZnajdzZdjecieTowaru(string kod)` zwraca pełną ścieżkę albo null

**Konwerter**: `SafeImagePathConverter` w `Konwertery.cs`
```csharp
public object? Convert(object value, ...)
{
    string? path = value as string;
    if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
    var bmp = new BitmapImage();
    bmp.BeginInit();
    bmp.CacheOption = BitmapCacheOption.OnLoad;
    bmp.UriSource = new Uri(path);
    bmp.DecodePixelHeight = 200;  // optymalizacja pamięci
    bmp.EndInit();
    bmp.Freeze();
    return bmp;
}
```

**XAML pattern z fallbackiem**:
```xml
<Grid Height="100">
    <!-- Fallback: gradient kategorii + duża ikona -->
    <Border Background="{Binding KategoriaKolor, Converter={StaticResource HexToBrush}}" Opacity="0.18"/>
    <TextBlock Text="{Binding KategoriaIkonaPelna}" FontSize="56" Opacity="0.5"/>
    <!-- Real image (overrides if exists) -->
    <Image Source="{Binding ZdjecieSciezka, Converter={StaticResource SafeImage}}"
           Stretch="UniformToFill"/>
</Grid>
```

**.csproj** kopiuje pliki do output:
```xml
<None Include="Assets\Towary\*.jpg;Assets\Towary\*.jpeg;Assets\Towary\*.png;Assets\Towary\*.webp">
  <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
</None>
```

### 5.4. Filtry
- **`chkTowaryTylkoZSaldem`** — pokaż tylko towary z saldem > 5% (te które się NIE bilansują)
- **`cbTowaryKategoria`** — ComboBox wyboru kategorii (Wszystkie / Mięso / Mrożone / Odpady)

### 5.5. Status badge
`TowarProdukcyjny.Status`:
- `✓ Bilansuje` (saldo < 5%) → zielony
- `⚠ Saldo` (5-20%) → żółty
- `❌ Duże saldo` (> 20%) → czerwony

`Saldo = Wyprodukowano - Zużyto - Sprzedano`

---

## 6. SEKCJA 3: Przepływy MM-

### 6.1. SQL
`WydajnoscService.LoadPrzeplywyMagazynowAsync(FiltryAnaliz)`:
```sql
WITH PerDok AS (
    SELECT MZ.magazyn AS MagZ, MG.khdzial AS MagDo, MG.id AS DokId,
           SUM(ABS(MZ.ilosc)) AS DokKg
    FROM HM.MG MG
    INNER JOIN HM.MZ MZ ON MZ.super = MG.id
    WHERE MG.seria IN ('MM-','sMM-')
      AND MG.anulowany = 0
      AND MZ.magazyn IS NOT NULL
      AND MG.khdzial IS NOT NULL AND MG.khdzial <> 0
      AND MG.data BETWEEN @DataOd AND @DataDo
    GROUP BY MZ.magazyn, MG.khdzial, MG.id
)
SELECT MagZ, MagDo, SUM(DokKg) AS Kg, COUNT(*) AS LiczbaDok
FROM PerDok
GROUP BY MagZ, MagDo
ORDER BY SUM(DokKg) DESC;
```

### 6.2. UI
Każdy wiersz = jeden kierunek przepływu:
```
[M.UBOJ]   →   [M.PROD]    ████████████████████████   478,066 kg   230 dok.
[M.PROD]   →   [M.DYST]    ████████████████████       282,000 kg   185 dok.
[M.PROD]   →   [M.MROŹ]    ███████                     50,000 kg    12 dok.
[M.PROD]   →   [KARMA]     █                            8,000 kg     5 dok.
[M.UBOJ]   →   [M.DYST]    ████████                    62,000 kg    34 dok.
```

- **Border żółty** dla magazynu źródłowego (#FEF3C7 / #F59E0B)
- **Border niebieski** dla magazynu docelowego (#DBEAFE / #2563EB)
- **Strzałka** Polygon (#7C3AED)
- **Pasek proporcjonalny** do kg (max 280px) z fioletową poświatą `DropShadowEffect`
- **Liczba dokumentów** w lawendowym chip
- **ToolTip** zawiera `numery_dokumentów`

---

## 7. Pliki źródłowe

### 7.1. Modele
- `AnalitykaPelna/Models/StanMagazynuModels.cs` — `StanMagazynu`, `StanMagazynuSeria`, `PrzeplywMagazynow`, `TowarProdukcyjny`, `TowarPrzeplyw`, `FlowChainSummary`, `FlowChainNode`, `SeriaSymfoniaHelper`

### 7.2. Service
- `AnalitykaPelna/Services/WydajnoscService.cs` — metody:
  - `LoadStanMagazynowAsync` — agregaty per magazyn (przychód/rozchód/saldo)
  - `LoadPrzeplywyMagazynowAsync` — przepływy MM-
  - `LoadTowaryProdukcjiAsync` — towary z produkcji + lifecycle
  - `LoadFlowChainAsync` — agregaty łańcucha (Żywiec/Uboj/Prod/...)
  - `ZnajdzZdjecieTowaru` — szuka pliku w Assets/Towary/

### 7.3. Helper
- `AnalitykaPelna/Services/MagazynyHelper.cs` — słownik magazynów + DB refresh + appsettings override

### 7.4. Konwertery
- `AnalitykaPelna/Services/Konwertery.cs`:
  - `KategoriaKolorConverter`
  - `WydajnoscKolorConverter`
  - `EtapTloConverter` / `EtapKolorConverter`
  - `GrupaPodsumowanieConverter`
  - `HexToBrushConverter` (NOWE)
  - `SafeImagePathConverter` (NOWE)
  - `BoolToVisibilityConverter` / `BoolToVisibilityInverseConverter` (NOWE)

### 7.5. View
- `AnalitykaPelna/Views/WidokWydajnosc.xaml(.cs)` — TabControl + 4 sub-zakładki + nowy "Stan magazynów"

### 7.6. Style w `UserControl.Resources`
- `FlowCardStyle` — kafelki łańcucha z hover effect
- `TowarCardStyle` — karty towarów z hover
- `KategoriaToggleStyle` — toggle filtrowania kategorii
- `SectionHeaderStyle` — nagłówki sekcji

---

## 8. Zmiany w iteracjach

### v1 (2026-05-08): "Bilans materiałowy" pierwsza wersja
- Sub-tab Pozycje (lista wszystkich operacji w okresie)
- Komparator (towar vs baza)
- KPI strip etap-by-etap

### v2 (2026-05-09): Dodano "Stan magazynów"
- Pierwsza wersja: kafelki magazynów (wszystkie), przepływy MM-, tabela rozkładu
- **Reakcja użytkownika**: "źle, chcę tylko towary z produkcji + strzałki + dokumenty"

### v3 (2026-05-09): Production-focused refactor
- Usunięto kafelki "wszystkie magazyny"
- Dodano łańcuch produkcji (5 kafelków + 4 strzałki + 3 odgałęzienia)
- Dodano "Towary wyprodukowane" (lista poziomych kart)
- Dokumenty na każdej operacji (tooltip)

### v4 (2026-05-09): Wydajności i normy
- Wydajność uboju + krojenia obliczane i wyświetlane na strzałkach
- Status badges (✓ OK / ⚠ Niska / ❌)
- HERO KPI strip (czarny pasek na górze) z 6 wskaźnikami

### v5 (2026-05-09): Polishing
- Hover effects (FlowCardStyle, TowarCardStyle)
- Spójne nagłówki sekcji
- Linear gradient na hero strip
- Lepsze cienie

### v6 (2026-05-09): Final layout
- USUNIĘTO HERO KPI strip (na życzenie — info redundantne ze strzałek)
- Towary 3-kolumnowy grid (bardziej kompaktowe)
- **ZDJĘCIA towarów** z `Assets/Towary/{kod}.jpg`
- Większe karty łańcucha (160px min)
- Sankey-style strzałki z gradient + DropShadow
- Bigger procent badges (12px Bold)

---

## 9. Future improvements (TODO)

- [ ] Animation on data load (FadeIn / SlideIn)
- [ ] Export do PDF dla Stan magazynów
- [ ] Drill-down: kliknięcie kafelka → szczegóły dokumentów dla tego etapu
- [ ] Per-product timeline (jak zmieniała się produkcja Tuszki A w okresie)
- [ ] Mapowanie kolorów do typowych klientów (B2B chains)
- [ ] Comparison mode: ten okres vs. poprzedni
- [ ] Heatmap dnia: który dzień / godzina najbardziej produktywne
- [ ] Drag & drop zdjęć w Assets/Towary/ z UI

---

**Aktualizacja**: 2026-05-09 — dokument utworzony po finalnym refactorze v6.
**Powiązane**: 23_HANDEL_Schema_Sage_Symfonia.md, 24_Magazyny_i_Lancuch_Produkcji.md, 22_Analityka_Pelna_modul.md.
