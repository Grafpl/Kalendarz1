# PROMPT — Przebudowa lewego panelu edycji kursu transportowego

## PROBLEM DO NAPRAWIENIA

Lewy ciemny panel (#2B2D42) ma ogromną pustą przestrzeń pomiędzy headerem (kierowca/pojazd/data) a tabelą ładunków na dole. Elementy są rozrzucone — header zajmuje tylko 20% panelu, tabela kolejności jest na samym dole, a 60% panelu w środku jest PUSTE.

## CEL

Przeprojektuj CAŁY lewy panel tak, żeby elementy były ułożone CIASNO jeden pod drugim bez pustych przestrzeni. Dodaj nowe elementy: Timeline (oś czasu), Capacity Bar (pasek ładowności), kompaktowe konflikty. Wszystko ma się ładnie mieścić bez scrollowania.

## SCREENSHOT OBECNEGO STANU

Obecny lewy panel wygląda tak (ŹLE):
```
┌──────────────────────────────────┐
│ KIEROWCA: [combo]  POJAZD:[combo]│  ← OK
│ DATA: [14.02] GODZ [06:00→18:00]│  ← OK
│ TRASA: "ABC Słupia 139a..."      │  ← OK ale brzydki TextBox
│                                  │
│ WYPEŁNIENIE: ████ 0%             │  ← brzydki, za prosty
│                                  │
│                                  │
│        (OGROMNA PUSTA            │  ← PROBLEM!
│         PRZESTRZEŃ               │  ← Tu nic nie ma!
│         ~400px pustki)           │  ← Zmarnowane miejsce!
│                                  │
│                                  │
│ KOLEJNOŚĆ: [▲][▼][Sortuj]       │  ← za nisko
│ ┌─────────────────────────────┐  │
│ │ 1 "ABC" Słupia  6.0  216   │  │  ← OK
│ └─────────────────────────────┘  │
└──────────────────────────────────┘
```

## DOCELOWY LAYOUT (DOBRZE):

Elementy ułożone CIASNO jeden pod drugim, bez żadnych pustych przestrzeni:

```
┌──────────────────────────────────────┐
│ SEKCJA A: NAGŁÓWEK KURSU (Auto height, ~110px)         │
│ ┌──────────────────────────────────┐ │
│ │ KIEROWCA [▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▾][+]│ │
│ │ POJAZD   [░░░░░░░░░░░░░░░░░░▾][+]│ │
│ │                                    │ │
│ │ DATA [15.02.2026]                  │ │
│ │ GODZINY [06:00]green → [18:00]purp│ │
│ └──────────────────────────────────┘ │
│ (separator 1px #3D3F5C)             │
│                                      │
│ SEKCJA B: TRASA jako pills (~40px)   │
│ ┌──────────────────────────────────┐ │
│ │ [🏭START]→[ABC Słupia]→[🏠POWRÓT]│ │
│ └──────────────────────────────────┘ │
│ (separator)                          │
│                                      │
│ SEKCJA C: CAPACITY BAR (~50px)       │
│ ┌──────────────────────────────────┐ │
│ │ ŁADOWNOŚĆ    [▓▓▓▓▓░░░░] 150%  │ │
│ │ 6.0 pal / 4 max • 216 poj      │ │
│ └──────────────────────────────────┘ │
│ (separator)                          │
│                                      │
│ SEKCJA D: TIMELINE - Oś czasu (~65px)│
│ ┌──────────────────────────────────┐ │
│ │ ⏱ OŚ CZASU KURSU                │ │
│ │ 6:00  8:00  10:00 12:00 14:00   │ │
│ │ [█ZAŁAD█][██JAZDA██][█ROZŁAD█]  │ │
│ │ ■Załadunek ■Jazda ■Rozładunek   │ │
│ └──────────────────────────────────┘ │
│ (separator)                          │
│                                      │
│ SEKCJA E: KONFLIKTY kompaktowe(~35px)│
│ ┌──────────────────────────────────┐ │
│ │ ⚠ KONFLIKTY [🔴1][🟡2] [Rozwiń▼]│ │
│ └──────────────────────────────────┘ │
│ (separator)                          │
│                                      │
│ SEKCJA F: ŁADUNKI W KURSIE (FILL!)  │
│ ┌──────────────────────────────────┐ │
│ │ 🚚 ŁADUNKI [1]  KOLEJN [▲▼Sort]│ │
│ │ ┌────────────────────────────┐   │ │
│ │ │ 1│ABC Słupia│6.0│216│96-128│   │ │
│ │ │ 2│Damak    │33.0│1320│...  │   │ │
│ │ │ ...                        │   │ │
│ │ └────────────────────────────┘   │ │
│ │ Σ Pal:39.0 • Poj:1536 • 7480kg │ │
│ └──────────────────────────────────┘ │
│                                      │
│ SEKCJA G: PRZYCISKI (~46px)          │
│ ┌──────────────────────────────────┐ │
│ │          [ANULUJ] [✓ ZAPISZ KURS]│ │
│ └──────────────────────────────────┘ │
└──────────────────────────────────────┘
```

## KLUCZOWA ZASADA LAYOUTU

**Użyj TableLayoutPanel z 7 wierszami w lewym panelu:**

```csharp
var leftLayout = new TableLayoutPanel
{
    Dock = DockStyle.Fill,
    ColumnCount = 1,
    RowCount = 7,
    BackColor = Color.FromArgb(43, 45, 66), // #2B2D42
    Margin = new Padding(0),
    Padding = new Padding(0),
};

// KRYTYCZNE: Sekcje A-E i G mają STAŁĄ wysokość (AutoSize),
// TYLKO sekcja F (ładunki) ma Fill — zajmuje RESZTĘ miejsca!
leftLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));     // A: Nagłówek kursu
leftLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));     // B: Trasa pills
leftLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));     // C: Capacity bar
leftLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));     // D: Timeline
leftLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));     // E: Konflikty kompakt
leftLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // F: ŁADUNKI (Fill!)
leftLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));     // G: Przyciski
```

**To jest NAJWAŻNIEJSZE. Dzięki temu:**
- Sekcje A-E mają dokładnie tyle wysokości ile potrzebują (AutoSize)
- Sekcja F (tabela ładunków) WYPEŁNIA resztę — nigdy nie ma pustki
- Sekcja G (przyciski) jest przyklejona do dołu

---

## PALETA KOLORÓW — użyj DOKŁADNIE tych wartości

```csharp
public static class ZpspColors
{
    // Ciemny panel
    public static readonly Color PanelDark       = Color.FromArgb(43, 45, 66);      // #2B2D42
    public static readonly Color PanelDarkAlt     = Color.FromArgb(50, 52, 80);      // #323450
    public static readonly Color PanelDarkBorder  = Color.FromArgb(61, 63, 92);      // #3D3F5C
    
    // Jasny panel  
    public static readonly Color PanelLight       = Color.White;
    public static readonly Color PanelLightAlt    = Color.FromArgb(248, 249, 252);   // #F8F9FC
    public static readonly Color PanelLightBorder = Color.FromArgb(226, 229, 239);   // #E2E5EF
    
    // Zielony (kierowca combo, przyciski, nagłówek zamówień)
    public static readonly Color Green     = Color.FromArgb(67, 160, 71);            // #43A047
    public static readonly Color GreenDark = Color.FromArgb(46, 125, 50);            // #2E7D32
    public static readonly Color GreenBg   = Color.FromArgb(232, 245, 233);          // #E8F5E9
    
    // Fioletowy (selekcja, godzina końca, sortuj)
    public static readonly Color Purple    = Color.FromArgb(123, 31, 162);           // #7B1FA2
    public static readonly Color PurpleRow = Color.FromArgb(232, 213, 245);          // #E8D5F5
    public static readonly Color PurpleBg  = Color.FromArgb(243, 229, 245);          // #F3E5F5
    
    // Pomarańczowy (palety, ostrzeżenia)
    public static readonly Color Orange    = Color.FromArgb(245, 124, 0);            // #F57C00
    public static readonly Color OrangeBg  = Color.FromArgb(255, 243, 224);          // #FFF3E0
    
    // Czerwony (przeładowanie, błędy)
    public static readonly Color Red       = Color.FromArgb(229, 57, 53);            // #E53935
    public static readonly Color RedDark   = Color.FromArgb(198, 40, 40);            // #C62828
    public static readonly Color RedBg     = Color.FromArgb(255, 235, 238);          // #FFEBEE
    
    // Niebieski (info, przyciski ▲▼)
    public static readonly Color Blue      = Color.FromArgb(30, 136, 229);           // #1E88E5
    public static readonly Color BlueBg    = Color.FromArgb(227, 242, 253);          // #E3F2FD
    
    // Tekst na ciemnym tle
    public static readonly Color TextWhite = Color.White;
    public static readonly Color TextLight = Color.FromArgb(200, 202, 216);          // #C8CAD8
    public static readonly Color TextMuted = Color.FromArgb(142, 144, 166);          // #8E90A6
    
    // Tekst na jasnym tle
    public static readonly Color TextDark   = Color.FromArgb(26, 28, 46);            // #1A1C2E
    public static readonly Color TextMedium = Color.FromArgb(85, 87, 112);           // #555770
    public static readonly Color TextGray   = Color.FromArgb(142, 144, 166);         // #8E90A6
}
```

---

## SEKCJA A: NAGŁÓWEK KURSU — szczegóły implementacji

Panel, Dock=Top wewnątrz wiersza 0, AutoSize=true, BackColor=PanelDark, Padding=(10,8,10,8).

**Wiersz 1 — Kierowca i Pojazd na jednej linii:**
```
[KIEROWCA:]label  [████████████▾]combo  [+]btn    [POJAZD:]label  [████████████▾]combo  [+]btn
```
- Użyj FlowLayoutPanel z WrapContents=false ALBO absolutne pozycje
- Label "KIEROWCA:" — Font Segoe UI 8pt Bold, ForeColor=TextMuted (#8E90A6)
- ComboBox kierowcy — Width=180, BackColor=Green (#43A047), ForeColor=White, Font=Segoe UI 11pt Bold, FlatStyle=Flat
- Button [+] — Size(26,26), BackColor=Blue (#1E88E5), ForeColor=White, Font=14pt Bold, FlatStyle=Flat, FlatAppearance.BorderSize=0
- Label "POJAZD:" — jak KIEROWCA
- ComboBox pojazdu — Width=180, BackColor=PanelDarkAlt (#323450), ForeColor=TextLight (#C8CAD8), border w PanelDarkBorder
- Button [+] — Size(26,26), BackColor=Green

**Wiersz 2 — Data i Godziny na jednej linii (pod kierowcą):**
```
[DATA:]label [15.02.2026]dtp   [GODZINY:]label [06:00]green [→]label [18:00]purple
```
- Wszystko w jednej linii, marginTop=6px
- DateTimePicker — Width=110, Format=Custom "dd.MM.yyyy"
- Godzina START — to może być DateTimePicker z ShowUpDown=true, CustomFormat="HH:mm"
  - ALE lepiej wizualnie: Label z BackColor=Green (#43A047), ForeColor=White, Font=13pt Bold, Padding(5,3,5,3), wyglądający jak pill. Klik otwiera TimePicker.
- Strzałka "→" — Label, ForeColor=TextMuted
- Godzina KONIEC — Label z BackColor=Purple (#7B1FA2), ForeColor=White, Font=13pt Bold

**Wiersz 3 — Metadata (pod datą):**
```
Utworzył: Administrator (15.02 08:48)  •  Handlowcy: [Maja]pill
```
- Font 8.5pt, ForeColor=TextMuted
- "Administrator" bold, ForeColor=TextLight  
- Pill [Maja] — BackColor=#E1BEE7, ForeColor=Purple, Font=8pt Bold, Padding(4,1,4,1), borderRadius=3

Całkowita wysokość sekcji A: ~90-100px.

---

## SEKCJA B: TRASA (Route Pills)

Panel, AutoSize=true, BackColor=PanelDarkAlt (#323450), border=1px PanelDarkBorder (#3D3F5C), 
Margin=(10,4,10,4), Padding=(6,4,6,4), borderRadius=6.

Wewnątrz FlowLayoutPanel z WrapContents=true:
```
[🏭 START]green → [ABC Słupia 139a (08:00)]purple → [🏠 POWRÓT]red
```

Pill START: BackColor=GreenDark (#2E7D32), ForeColor=White, Font=9pt Bold, Padding(6,2,6,2)
Pill klienta: BackColor=PurpleBg (#F3E5F5), ForeColor=Purple (#7B1FA2), border 1px #E1BEE7, Font=9pt Bold
Pill POWRÓT: BackColor=Red (#E53935), ForeColor=White
Strzałka "→": Label, ForeColor=TextMuted, Font=10pt

Trasa generowana automatycznie:
```csharp
var stopNames = course.Stops.OrderBy(s => s.Lp).Select(s => s.NazwaKlienta).ToArray();
routePills.SetRoute(stopNames); // automatycznie dodaje START i POWRÓT
```

Całkowita wysokość: ~36px.

---

## SEKCJA C: CAPACITY BAR (pasek ładowności)

Panel, AutoSize=true, BackColor=PanelDarkAlt, border, Margin=(10,4,10,4), Padding=(8,6,8,6).

Wiersz 1 (flex between):
```
ŁADOWNOŚĆ NACZEPY                     150% [⚠ PRZEŁADOWANE]pill
```
- "ŁADOWNOŚĆ NACZEPY" — Font 8pt Bold, TextMuted
- "150%" — Font 16pt Bold, kolor zależny od wartości:
  - 0-50%: Green
  - 50-80%: Orange  
  - 80-100%: Orange
  - >100%: Red
- Pill [⚠ PRZEŁADOWANE] — tylko gdy >100%, BackColor=RedBg, ForeColor=Red, Font=8pt Bold

Wiersz 2 — Sam pasek:
- Wysokość 12px, borderRadius=6
- Tło: szary #E0E0E0
- Wypełnienie (szerokość = min(procent, 100)% * szerokość paska):
  - 0-50%: Green (#43A047)
  - 50-80%: Orange (#FF9800)
  - 80-100%: Orange (#F57C00)
  - >100%: HATCHING — `HatchBrush(HatchStyle.ForwardDiagonal, Red, RedDark)` — czerwone ukośne paski
- Custom UserControl z OnPaint:
```csharp
protected override void OnPaint(PaintEventArgs e)
{
    var g = e.Graphics;
    g.SmoothingMode = SmoothingMode.AntiAlias;
    
    int barW = Width - 60; // zostawiam 60px na procent po prawej
    int barH = 12;
    int barY = 20; // pod labelem
    
    // Tło paska
    using var bgBrush = new SolidBrush(Color.FromArgb(224, 224, 224));
    g.FillRoundedRect(bgBrush, 0, barY, barW, barH, 6);
    
    // Wypełnienie
    float pct = Math.Min(_percent, 100f);
    int fillW = (int)(barW * pct / 100f);
    if (fillW > 0)
    {
        if (_percent > 100)
        {
            using var hatch = new HatchBrush(HatchStyle.ForwardDiagonal, 
                Color.FromArgb(229, 57, 53), Color.FromArgb(198, 40, 40));
            g.FillRoundedRect(hatch, 0, barY, fillW, barH, 6);
        }
        else
        {
            Color c = _percent > 80 ? Orange : _percent > 50 ? OrangeLight : Green;
            using var brush = new SolidBrush(c);
            g.FillRoundedRect(brush, 0, barY, fillW, barH, 6);
        }
    }
}
```

Wiersz 3 — Podsumowanie:
```
6.0 palet / 4 max  •  216 pojemników  •  2 400 kg
```
- Font 9pt, TextMuted, wartości bold: palety=Orange, pojemniki=Green, kg=TextLight

Całkowita wysokość: ~48px.

---

## SEKCJA D: TIMELINE (oś czasu kursu) ← NOWY ELEMENT

**To jest kluczowa nowa funkcjonalność!**

Custom UserControl `TimelineControl`, Height=65, BackColor=PanelDarkAlt, border, borderRadius=6, Margin=(10,4,10,4).

### Jak to wygląda:
```
⏱ OŚ CZASU KURSU                    Szac. powrót: ~09:30
 6:00   7:00   8:00   9:00  10:00  11:00  12:00
  |      |      |      |      |      |      |
  [█ZAŁADUNEK█][████JAZDA→ABC Słupia████][█ROZŁ█][██POWRÓT██]
  ■Załadunek  ■Jazda  ■Rozładunek  ■Powrót
```

### Logika obliczania segmentów:
```csharp
public void SetCourse(TransportCourse course)
{
    _segments.Clear();
    
    if (course.Stops.Count == 0) return;
    
    var startTime = course.GodzinaWyjazdu; // np. 06:00
    
    // 1. Załadunek w zakładzie: 30 min przed wyjazdem → wyjazd
    _segments.Add(new Segment
    {
        Start = startTime.Add(TimeSpan.FromMinutes(-30)),
        End = startTime,
        Label = "Załadunek",
        Color = GreenDark,     // #2E7D32
        Icon = "📦"
    });
    
    var currentTime = startTime;
    
    foreach (var stop in course.Stops.OrderBy(s => s.Lp))
    {
        // 2. Jazda do klienta
        var arrivalTime = stop.PlannedArrival ?? currentTime.Add(TimeSpan.FromHours(2));
        
        if (arrivalTime > currentTime)
        {
            _segments.Add(new Segment
            {
                Start = currentTime,
                End = arrivalTime,
                Label = $"Jazda → {stop.NazwaKlienta}",
                Color = Blue,     // #1E88E5
                Icon = "🚛"
            });
        }
        
        // 3. Rozładunek u klienta: 30 min
        var unloadEnd = arrivalTime.Add(TimeSpan.FromMinutes(30));
        _segments.Add(new Segment
        {
            Start = arrivalTime,
            End = unloadEnd,
            Label = stop.NazwaKlienta,
            Color = Purple,     // #7B1FA2
            Icon = "📦"
        });
        
        currentTime = unloadEnd;
    }
    
    // 4. Powrót — od ostatniego rozładunku do szacowanego powrotu
    // Szacuj czas powrotu = ostatni rozładunek + czas jazdy
    var estimatedReturn = currentTime.Add(TimeSpan.FromHours(2));
    _segments.Add(new Segment
    {
        Start = currentTime,
        End = estimatedReturn,
        Label = "Powrót",
        Color = Color.FromArgb(150, 229, 57, 53),  // Red z przezroczystością
        Icon = "🏠"
    });
    
    _estimatedReturn = estimatedReturn;
    Invalidate();
}
```

### Rysowanie (OnPaint):
```csharp
protected override void OnPaint(PaintEventArgs e)
{
    var g = e.Graphics;
    g.SmoothingMode = SmoothingMode.AntiAlias;
    g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
    
    // Oblicz zakres czasu (od pierwszego segmentu do ostatniego + margines)
    var minTime = _segments.Min(s => s.Start);
    var maxTime = _segments.Max(s => s.End);
    double totalMinutes = (maxTime - minTime).TotalMinutes;
    
    int leftPad = 8, rightPad = 8;
    int barArea = Width - leftPad - rightPad;
    int barY = 28;  // pod nagłówkiem
    int barH = 16;
    
    // --- Nagłówek ---
    using var titleFont = new Font("Segoe UI", 8f, FontStyle.Bold);
    using var titleBrush = new SolidBrush(TextMuted);
    g.DrawString("⏱ OŚ CZASU KURSU", titleFont, titleBrush, leftPad, 4);
    
    // Szacowany powrót po prawej
    if (_estimatedReturn.HasValue)
    {
        using var retBrush = new SolidBrush(
            _estimatedReturn > course.GodzinaPowrotu ? Red : Green);
        string retText = $"Powrót ~{_estimatedReturn:hh\\:mm}";
        var retSize = g.MeasureString(retText, titleFont);
        g.DrawString(retText, titleFont, retBrush, Width - rightPad - retSize.Width, 4);
    }
    
    // --- Linie godzin ---
    using var gridPen = new Pen(PanelDarkBorder, 1);
    using var hourFont = new Font("Segoe UI", 7f);
    using var hourBrush = new SolidBrush(TextMuted);
    
    // Rysuj pełne godziny
    for (int h = (int)minTime.TotalHours; h <= (int)maxTime.TotalHours + 1; h++)
    {
        double minutesFromStart = (h * 60) - minTime.TotalMinutes;
        int x = leftPad + (int)(barArea * minutesFromStart / totalMinutes);
        if (x >= leftPad && x <= leftPad + barArea)
        {
            g.DrawLine(gridPen, x, barY - 4, x, barY + barH + 2);
            g.DrawString($"{h}:00", hourFont, hourBrush, x - 10, barY - 14);
        }
    }
    
    // --- Segmenty ---
    foreach (var seg in _segments)
    {
        double startMin = (seg.Start - minTime).TotalMinutes;
        double endMin = (seg.End - minTime).TotalMinutes;
        
        int x1 = leftPad + (int)(barArea * startMin / totalMinutes);
        int x2 = leftPad + (int)(barArea * endMin / totalMinutes);
        int w = Math.Max(x2 - x1, 4); // minimum 4px szerokości
        
        var rect = new Rectangle(x1, barY, w, barH);
        using var brush = new SolidBrush(seg.Color);
        
        // Zaokrąglony prostokąt
        using var path = RoundedRect(rect, 3);
        g.FillPath(brush, path);
        
        // Tekst wewnątrz (jeśli się mieści)
        if (w > 40)
        {
            using var segFont = new Font("Segoe UI", 7f, FontStyle.Bold);
            string text = $"{seg.Icon} {seg.Label}";
            var textSize = g.MeasureString(text, segFont);
            if (textSize.Width < w - 4)
            {
                g.DrawString(text, segFont, Brushes.White,
                    x1 + (w - textSize.Width) / 2,
                    barY + (barH - textSize.Height) / 2);
            }
        }
    }
    
    // --- Legenda na dole ---
    int legendY = barY + barH + 6;
    using var legendFont = new Font("Segoe UI", 7.5f);
    using var legendBrush = new SolidBrush(TextLight);
    int lx = leftPad;
    foreach (var item in new[] {
        ("Załadunek", GreenDark), ("Jazda", Blue),
        ("Rozładunek", Purple), ("Powrót", Red) })
    {
        using var sqBrush = new SolidBrush(item.Item2);
        g.FillRectangle(sqBrush, lx, legendY + 2, 8, 8);
        g.DrawString(item.Item1, legendFont, legendBrush, lx + 11, legendY);
        lx += (int)g.MeasureString(item.Item1, legendFont).Width + 18;
    }
}
```

Całkowita wysokość: 65px (stała).

---

## SEKCJA E: KONFLIKTY — wersja kompaktowa

Panel, AutoSize=true, BackColor=PanelDarkAlt, border, borderRadius=6, Margin=(10,4,10,4).

### Domyślnie zwinięte (1 wiersz, ~32px):
```
⚠ KONFLIKTY  [🔴 1 błąd] [🟡 2 ostrz.] [🔵 1 info]     [Rozwiń ▼]
```

### Po kliknięciu "Rozwiń" — rozszerzone (~100px max, scrollowalne):
```
⚠ KONFLIKTY  [🔴 1 błąd] [🟡 2 ostrz.] [🔵 1 info]     [Zwiń ▲]
├─🔴 Przeładowanie naczepy: 6.0 palet / 4 max (150%)
├─🟡 Adres zagraniczny ABC Słupia — sprawdź CMR
└─🔵 Zamówienie Damak (05-555) ma zbliżony adres
```

**Implementacja:**
```csharp
// Pill badge z liczbą
private Label CreateCountBadge(int count, Color bgColor, string text)
{
    return new Label
    {
        Text = text,
        Font = new Font("Segoe UI", 8f, FontStyle.Bold),
        ForeColor = Color.White,
        BackColor = bgColor,
        AutoSize = true,
        Padding = new Padding(5, 1, 5, 1),
    };
}

// Wiersz konfliktu (1 linia)
private Panel CreateConflictRow(CourseConflict c)
{
    var row = new Panel { Height = 22, Dock = DockStyle.Top };
    // Lewy border kolorowy 3px
    row.Paint += (s, e) => {
        Color bc = c.Level == ConflictLevel.Error ? Red : 
                   c.Level == ConflictLevel.Warning ? Orange : Blue;
        using var pen = new Pen(bc, 3);
        e.Graphics.DrawLine(pen, 1, 0, 1, row.Height);
    };
    // Ikona + tekst
    var lbl = new Label {
        Text = $"{c.Icon} {c.Message}",
        Font = new Font("Segoe UI", 9f),
        ForeColor = TextLight,
        AutoSize = true,
        Location = new Point(8, 3),
    };
    row.Controls.Add(lbl);
    return row;
}
```

**Typy konfliktów do wykrywania (ConflictDetectionService):**

Stwórz serwis z metodą `List<CourseConflict> DetectAll(course, allOrders, allCourses)`:

```
ERROR (czerwone):
- NO_DRIVER: course.Kierowca == null
- NO_VEHICLE: course.Pojazd == null
- CAPACITY_OVERLOAD: SumaPalet > Pojazd.MaxPalet
- WEIGHT_OVERLOAD: SumaWagaKg + 7500 (tara) > Pojazd.DMC_Kg
- DRIVER_DOUBLE_BOOKING: inny kurs tego dnia z tym samym kierowcą i nachodzącym czasem
- VEHICLE_DOUBLE_BOOKING: inny kurs z tym samym pojazdem

WARNING (pomarańczowe):
- CAPACITY_HIGH: SumaPalet > 80% MaxPalet (ale < 100%)
- DRIVER_HOURS: (GodzinaPowrotu - GodzinaWyjazdu) > 12h
- DUPLICATE_CLIENT: klient z tego kursu jest też w innym kursie
- FOREIGN_ADDRESS: adres/uwagi zawierają: "Rumunia", "MUN.", "STR.", "Romania", "Deutschland"
- RETURN_LATE: PlannedArrival ostatniego stopu > GodzinaPowrotu

INFO (niebieskie):
- EMPTY_COURSE: brak ładunków
- NEARBY_ORDER: wolne zamówienie z tym samym prefiksem kodu pocztowego co ładunki w kursie
- MULTI_HANDLOWIEC: handlowcy.Count > 1
```

Wywołuj `DetectAll()` w `OnCourseChanged()` po każdej zmianie.

---

## SEKCJA F: ŁADUNKI W KURSIE (DataGridView ciemny motyw)

Ta sekcja WYPEŁNIA resztę panelu (RowStyle Percent 100%).

**Nagłówek:**
```
🚚 ŁADUNKI W KURSIE [2]       KOLEJNOŚĆ: [▲] [▼] [Sortuj]
```
- "🚚 ŁADUNKI W KURSIE" — Font 11pt Bold, White
- Pill [2] — BackColor=Green, White, Font=9pt Bold
- "KOLEJNOŚĆ:" — Font 8pt Bold, TextMuted
- [▲] [▼] — BackColor=Blue, Size(24,22), White
- [Sortuj] — BackColor=Purple, Padding(3,10,3,10), White, Font=9pt Bold

**DataGridView — ciemny styl:**
```csharp
dgvStops.EnableHeadersVisualStyles = false;
dgvStops.BackgroundColor = PanelDark;
dgvStops.GridColor = PanelDarkBorder;
dgvStops.BorderStyle = BorderStyle.None;
dgvStops.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
dgvStops.RowHeadersVisible = false;
dgvStops.AllowUserToAddRows = false;
dgvStops.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
dgvStops.RowTemplate.Height = 34;

dgvStops.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
{
    BackColor = PanelDarkBorder,
    ForeColor = TextMuted,
    Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
};
dgvStops.DefaultCellStyle = new DataGridViewCellStyle
{
    BackColor = PanelDark,
    ForeColor = TextLight,
    SelectionBackColor = Color.FromArgb(40, 123, 31, 162), // Purple 15% alpha
    SelectionForeColor = Color.White,
    Font = new Font("Segoe UI", 10f),
};
dgvStops.AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
{
    BackColor = PanelDarkAlt,
};
```

**Kolumny:**
| Kolumna | Width | Align | Font kolor |
|---------|-------|-------|------------|
| Lp. | 36 | Center | Green, 14pt Bold |
| Klient | Fill | Left | White, 10pt Bold |
| Palety | 55 | Right | Orange, 11pt Bold |
| Poj. | 55 | Right | Green, 10pt |
| Adres | 120 | Left | TextMuted, 9pt |
| Uwagi | 150 | Left | TextLight, 9pt |

**Podsumowanie pod tabelą:**
```
Σ Pal: 6.0  •  Σ Poj: 216  •  Σ Waga: 2 400 kg
```
Panel, Height=24, label z wartościami: palety=Orange(bold), pojemniki=Green(bold), kg=TextLight(bold), reszta=TextMuted.

**Interakcje:**
- Delete → usuń wybrany ładunek, przenumeruj Lp, wywołaj OnCourseChanged()
- ▲ → swap Lp wybranego z poprzednim, odśwież
- ▼ → swap Lp wybranego z następnym, odśwież
- Sortuj → sortuj po PlannedArrival, przenumeruj Lp

---

## SEKCJA G: PRZYCISKI

FlowLayoutPanel, FlowDirection=RightToLeft, Height=46, Padding=(10,6,10,6).

```
                                    [ANULUJ] [✓ ZAPISZ KURS]
```
- [✓ ZAPISZ KURS]:
  - Normalnie: BackColor=Green, gradient do GreenDark, White, Font=13pt Bold, Size(160,34), borderRadius=6
  - Gdy są Error-y: BackColor=Orange, tekst="⚠ ZAPISZ (z ostrzeżeniami)"
  - BoxShadow: maluj OnPaint z DrawRoundedRect pod spodem z Alpha
- [ANULUJ]:
  - Transparent bg, border 1px PanelDarkBorder, ForeColor=TextMuted, Font=10pt Bold, Size(90,34)

---

## LOGIKA OnCourseChanged()

Wywołuj po KAŻDEJ zmianie (dodanie/usunięcie ładunku, zmiana combo, zmiana godzin):

```csharp
private void OnCourseChanged()
{
    // 1. Przelicz sumy z course.Stops
    decimal sumPal = course.Stops.Sum(s => s.Palety);
    int sumPoj = course.Stops.Sum(s => s.Pojemniki);
    decimal sumKg = course.Stops.Sum(s => s.WagaKg);
    
    // 2. Capacity bar
    decimal maxPal = course.Pojazd?.MaxPalet ?? 4;
    capacityBar.SetCapacity(sumPal, maxPal);
    
    // 3. Route pills — automatycznie z ładunków
    var names = course.Stops.OrderBy(s => s.Lp)
        .Select(s => s.NazwaKlienta).ToArray();
    routePills.SetRoute(names);
    
    // 4. Timeline
    timeline.SetCourse(course);
    
    // 5. Konflikty
    var conflicts = conflictService.DetectAll(course, allOrders, allCourses);
    conflictPanel.SetConflicts(conflicts);
    
    // 6. Przycisk Zapisz
    bool hasErrors = conflicts.Any(c => c.Level == ConflictLevel.Error);
    btnSave.BackColor = hasErrors ? Orange : Green;
    btnSave.Text = hasErrors ? "⚠ ZAPISZ (z ostrzeżeniami)" : "✓ ZAPISZ KURS";
    
    // 7. Summary label
    lblSummary.Text = $"Σ Pal: {sumPal:F1}  •  Σ Poj: {sumPoj}  •  Σ Waga: {sumKg:F0} kg";
    
    // 8. Trasa tekstowa (dla starego pola, jeśli zostaje)
    course.TrasaOpis = string.Join(" → ", names);
}
```

---

## PRAWY PANEL — Zamówienia (bez zmian, ale popraw style)

Zachowaj obecną strukturę prawego panelu ale upewnij się że:
- Nagłówek jest zielony (#43A047)
- Wiersz zaznaczony ma fioletowe tło (#E8D5F5) i borderLeft 3px Purple
- Grupy dat mają kolorowe tła: poniedziałek=GreenBg, wtorek=OrangeBg, środa=BlueBg
- Priorytet to kolorowa kropka: Normal=Green, High=Red, Express=Purple, Low=gray
- Godzina w pill z fioletowym tłem PurpleBg i kolorze Purple
- Palety pomarańczowe bold

---

## GŁÓWNY LAYOUT — TableLayoutPanel 52/48

```csharp
var mainLayout = new TableLayoutPanel
{
    Dock = DockStyle.Fill,
    ColumnCount = 2,
    RowCount = 1,
    Padding = new Padding(0),
    Margin = new Padding(0),
    BackColor = Color.FromArgb(228, 230, 237), // szary jak tło okna
};
mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52f));
mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48f));
mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

// Lewy panel
var leftPanel = new Panel { Dock = DockStyle.Fill, BackColor = PanelDark };
var leftLayout = new TableLayoutPanel { /* 7 wierszy jak opisano */ };
leftPanel.Controls.Add(leftLayout);
mainLayout.Controls.Add(leftPanel, 0, 0);

// Prawy panel (zachowaj obecny z poprawionymi kolorami)
mainLayout.Controls.Add(rightPanel, 1, 0);

Controls.Add(mainLayout);
```

---

## WAŻNE — NIE RÓB TYCH BŁĘDÓW:

1. **NIE zostawiaj pustej przestrzeni** — sekcje muszą być ciasno jedna pod drugą
2. **NIE używaj Dock=Top dla tabeli ładunków** — tabela musi mieć Dock=Fill wewnątrz wiersza z Percent 100%
3. **NIE dawaj stałej wysokości temu co powinno być Fill** — tylko tabela ładunków jest Fill, reszta AutoSize
4. **NIE zapominaj o DoubleBuffered=true** na formie i custom kontrolkach
5. **NIE używaj Designera** — twórz kontrolki w kodzie w InitializeLayout()
6. **NIE zapominaj EnableHeadersVisualStyles=false** na DataGridView
7. **Timeline MUSI mieć stałą wysokość** (65px) — nie AutoSize bo to custom paint
8. **Conflict panel w trybie zwiniętym = 32px, rozwinięty = max 120px** — nie więcej bo zabierze miejsce ładunkom
9. **Font WSZĘDZIE = Segoe UI** — nie zmieniaj na inny

---

## KOLEJNOŚĆ TWORZENIA PLIKÓW

1. `Theme/ZpspColors.cs` — kolory
2. `Theme/ZpspFonts.cs` — fonty  
3. `Models/TransportModels.cs` — klasy danych
4. `Controls/CapacityBarControl.cs` — pasek ładowności
5. `Controls/RoutePillsControl.cs` — pills trasy
6. `Controls/TimelineControl.cs` — oś czasu Gantt ← NOWY!
7. `Controls/ConflictPanelControl.cs` — panel alertów kompaktowy
8. `Services/ConflictDetectionService.cs` — wykrywanie konfliktów
9. `KursEditorForm.cs` — główna forma z TableLayoutPanel 7 wierszy

Zrób to DOKŁADNIE jak opisano. Każdy kolor, font, rozmiar musi być taki jak w tym pliku. Testuj kompilację po każdym pliku.
