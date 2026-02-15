# PROMPT DLA CLAUDE CODE — Przebudowa okna edycji kursu transportowego

## KONTEKST PROJEKTU

Przerabiam okno `KursEditorForm` w aplikacji WinForms (.NET 8, C#). 
To okno służy do planowania kursów transportowych w firmie przetwórstwa drobiu.
Logistyk widzi listę zamówień i przypisuje je do kursów (kierowca + pojazd + trasa).

Obecne okno jest standardowym WinForms z szarymi panelami. Chcę je przerobić na nowoczesny ciemny/jasny motyw z kolorowymi akcentami i dodatkowymi funkcjonalnościami.

---

## DOCELOWY LAYOUT — WARIANT A (Classic Improved)

Okno dzieli się na 2 główne kolumny:
- **LEWA KOLUMNA (52% szerokości)** — ciemne tło `#2B2D42` — dane kursu + ładunki
- **PRAWA KOLUMNA (48% szerokości)** — białe tło `#FFFFFF` — lista zamówień do przypisania

```
┌──────────────────────────────────────────────────────────────────────────┐
│  📦 Edycja kursu transportowego                           [_][□][X]     │
├─────────────── 52% ──────────────┬──────────────── 48% ──────────────────┤
│  CIEMNY PANEL (#2B2D42)          │  JASNY PANEL (biały #FFFFFF)          │
│                                  │                                        │
│  ┌ HEADER KURSU ───────────────┐ │  ┌ NAGŁÓWEK ZIELONY (#43A047) ──────┐│
│  │ KIEROWCA [combo, zielony bg]│ │  │ 📋 ZAMÓWIENIA  [14 zam.]         ││
│  │ [+] POJAZD [combo, ciemny]  │ │  │ [Ubój|Odbiór] [🔍 Szukaj] [📅] ││
│  │ [+]                         │ │  └──────────────────────────────────┘│
│  │                             │ │                                       │
│  │ DATA [14.02.2026]           │ │  Nagłówki kolumn (sticky):            │
│  │ GODZ [06:00]green→[18:00]pur│ │  [●] Odbiór  Godz.  Pal. Poj. Klient│
│  │                             │ │                                       │
│  │ TRASA (route pills):       │ │  ► 16.02 poniedziałek [zielone tło]  │
│  │ [🏭START]→[LOCIV]→[PODOLSKI]│ │  • O&M       11:00  14.8  533 ...   │
│  │ →[🏠POWRÓT]                 │ │  ● Damak     14:00  33.0  1320 ...  │
│  │                             │ │  (● = czerwona kropka = high prio)   │
│  │ ┌ ŁADOWNOŚĆ ──────────────┐ │ │  ...więcej zamówień...               │
│  │ │ ▓▓▓▓▓▓▓▓▓▓▓▓▓▓ 536%    │ │ │                                       │
│  │ │ 21.4/4 pal ⚠PRZEŁADOW.  │ │ │  ► 17.02 wtorek [pomarańczowe tło]  │
│  │ └─────────────────────────┘ │ │  ● EUREKA    05:00   2.2   80 ...   │
│  │                             │ │  ◆ Ladros    08:00  16.7  600 ...   │
│  │ ┌ ⏱️ OŚ CZASU KURSU ─────┐ │ │  (◆ = fiolet = express)              │
│  │ │ [Gantt bar: 06:00→20:30]│ │ │  ...więcej zamówień...               │
│  │ │ załad→jazda→rozład→jazda │ │ │                                       │
│  │ │ →rozład→powrót           │ │ │                                       │
│  │ └─────────────────────────┘ │ │                                       │
│  │                             │ │                                       │
│  │ ┌ ⚠ KONFLIKTY (kompakt) ──┐│ │                                       │
│  │ │ 🔴2 🟡2 🔵2  [Rozwiń ▼]││ │                                       │
│  │ │ Przeładowanie 536%       ││ │                                       │
│  │ │ Adres zagraniczny CMR    ││ │                                       │
│  │ └─────────────────────────┘│ │                                       │
│  │                             │ │                                       │
│  │ Utworzył: Admin • Maja      │ │                                       │
│  └─────────────────────────────┘ │                                       │
│                                  │                                       │
│  ┌ 🚚 ŁADUNKI W KURSIE [2] ──┐ │                                       │
│  │ KOLEJNOŚĆ: [▲][▼][Sortuj]  │ │                                       │
│  │                             │ │                                       │
│  │ 1│LOCIV IMPEX│2.0│72│Rum...│ │                                       │
│  │ 2│PODOLSKI  │19.4│700│Lut..│ │                                       │
│  │                             │ │                                       │
│  │ Σ Pal:21.4 • Poj:772 • 4t │ │                                       │
│  └─────────────────────────────┘ │                                       │
│                                  │                                       │
│  ┌ ZAKŁADKI DOLNE ────────────┐ │  ┌──────────────────────────────────┐ │
│  │[Kurs]  [Historia] [Szablony]│ │  │ ⬇ Dodaj zaznaczone do kursu (2) │ │
│  │[Koszty/Waga]                │ │  └──────────────────────────────────┘ │
│  └─────────────────────────────┘ │                                       │
│                                  │                                       │
│  [ANULUJ]        [✓ ZAPISZ KURS] │                                       │
└──────────────────────────────────┴───────────────────────────────────────┘
```

---

## DOKŁADNA PALETA KOLORÓW

Stwórz klasę statyczną `ZpspColors` z tymi kolorami (wszystkie jako `Color.FromArgb`):

### Ciemny panel (lewy):
```csharp
PanelDark       = Color.FromArgb(43, 45, 66);      // #2B2D42 — główne tło
PanelDarkAlt    = Color.FromArgb(50, 52, 80);      // #323450 — inputy, combobox
PanelDarkBorder = Color.FromArgb(61, 63, 92);      // #3D3F5C — obramowania
PanelDarkHover  = Color.FromArgb(58, 60, 88);      // #3A3C58 — hover
```

### Jasny panel (prawy):
```csharp
PanelLight       = Color.White;                      // #FFFFFF
PanelLightAlt    = Color.FromArgb(248, 249, 252);   // #F8F9FC — zebra wiersz
PanelLightBorder = Color.FromArgb(226, 229, 239);   // #E2E5EF — linie
PanelLightHover  = Color.FromArgb(240, 242, 250);   // #F0F2FA
```

### Zielone (primary — przyciski, nagłówki, combo kierowcy):
```csharp
Green     = Color.FromArgb(67, 160, 71);     // #43A047
GreenDark = Color.FromArgb(46, 125, 50);     // #2E7D32
GreenBg   = Color.FromArgb(232, 245, 233);   // #E8F5E9 — tło grupy dat pon.
GreenBg2  = Color.FromArgb(200, 230, 201);   // #C8E6C9
```

### Fioletowe (selekcja, godzina końca, Sortuj):
```csharp
Purple    = Color.FromArgb(123, 31, 162);    // #7B1FA2
PurpleRow = Color.FromArgb(232, 213, 245);   // #E8D5F5 — zaznaczony wiersz
PurpleBg  = Color.FromArgb(243, 229, 245);   // #F3E5F5 — pill godziny
PurpleBg2 = Color.FromArgb(225, 190, 231);   // #E1BEE7 — border pill
```

### Pozostałe akcenty:
```csharp
Orange    = Color.FromArgb(245, 124, 0);     // #F57C00 — palety, ostrzeżenia
OrangeBg  = Color.FromArgb(255, 243, 224);   // #FFF3E0 — tło grupy dat wt.
Red       = Color.FromArgb(229, 57, 53);     // #E53935 — przeładowanie, błędy
RedDark   = Color.FromArgb(198, 40, 40);     // #C62828 — hatching
RedBg     = Color.FromArgb(255, 235, 238);   // #FFEBEE
Blue      = Color.FromArgb(30, 136, 229);    // #1E88E5 — przyciski ▲▼, info
BlueBg    = Color.FromArgb(227, 242, 253);   // #E3F2FD
Cyan      = Color.FromArgb(0, 172, 193);     // #00ACC1
```

### Tekst na ciemnym tle:
```csharp
TextWhite = Color.White;                              // nagłówki, klienci
TextLight = Color.FromArgb(200, 202, 216);   // #C8CAD8 — wartości
TextMuted = Color.FromArgb(142, 144, 166);   // #8E90A6 — labele, etykiety
```

### Tekst na jasnym tle:
```csharp
TextDark   = Color.FromArgb(26, 28, 46);    // #1A1C2E — klienci
TextMedium = Color.FromArgb(85, 87, 112);   // #555770 — wartości
TextGray   = Color.FromArgb(142, 144, 166); // #8E90A6 — adresy, daty
TextFaint  = Color.FromArgb(176, 179, 197); // #B0B3C5 — disabled
```

---

## SZCZEGÓŁOWY OPIS KAŻDEGO ELEMENTU UI

### 1. HEADER KURSU (lewy panel, góra)

**Wiersz 1 — Kierowca + Pojazd:**
- Label "KIEROWCA" — font Segoe UI 8pt Bold, kolor `TextMuted`, uppercase, letterSpacing
- ComboBox kierowcy — BackColor=`Green` (#43A047), ForeColor=White, font Segoe UI 11pt Bold, DropDownStyle=DropDownList, borderRadius=6 (custom paint)
- Przycisk [+] — kwadrat 28x28, BackColor=`Blue`, białe "+", borderRadius=6
- Label "POJAZD" — jak label KIEROWCA
- ComboBox pojazdu — BackColor=`PanelDarkAlt`, ForeColor=`TextLight`, border 1px `PanelDarkBorder`
- Przycisk [+] — 28x28, BackColor=`Green`

**Wiersz 2 — Data + Godziny:**
- Label "DATA" — jak wyżej
- DateTimePicker — format "dd.MM.yyyy"
- Label "GODZINY" — jak wyżej  
- Godzina START — wyświetlana w spanku/labelu z BackColor=`Green`, kolor biały, font 13pt Bold, padding 5px 12px, borderRadius=6
- Strzałka "→" — Label, kolor `TextMuted`
- Godzina KONIEC — jak start ale BackColor=`Purple` (#7B1FA2)
- Używaj DateTimePicker z ShowUpDown=true, CustomFormat="HH:mm"

**Wiersz 3 — Trasa (Route Pills):**
- Label "TRASA" — jak wyżej
- Kontener trasy — FlowLayoutPanel, BackColor=`PanelDarkAlt`, border 1px `PanelDarkBorder`, borderRadius=6, padding 6px
- Wewnątrz pills:
  - [🏭 START] — BackColor=`GreenDark`, biały tekst, font 9pt Bold, padding 2px 8px, borderRadius=4
  - Strzałka "→" — Label, kolor `TextMuted`
  - [LOCIV IMPEX (RO) 08:00] — BackColor=`PurpleBg` (#F3E5F5), kolor `Purple`, border 1px `PurpleBg2`
  - ...kolejne przystanki...
  - [🏠 POWRÓT] — BackColor=`Red`, biały tekst
- Trasa budowana automatycznie z listy ładunków w kursie

**Wiersz 4 — Capacity Bar (ładowność naczepy):**
- Kontener — BackColor=`PanelDarkAlt`, border, borderRadius=6, padding 8px
- Wiersz nagłówka: "ŁADOWNOŚĆ NACZEPY" (muted) ←→ "536%" (Red, 16pt Bold) + pill [⚠ PRZEŁADOWANE] (RedBg/Red)
- Custom ProgressBar:
  - Tło: szary `#E0E0E0`, borderRadius=height/2
  - Wypełnienie:
    - 0-50%: `Green`
    - 50-80%: `OrangeLight`
    - 80-100%: `Orange`
    - >100%: czerwony hatching — `HatchBrush(HatchStyle.ForwardDiagonal, Red, RedDark)`
  - Wysokość: 12px
- Pod paskiem: "21.4 palet / 4 max • 772 pojemników • 4 104 kg" (TextMuted, 9pt)

### 2. OŚ CZASU KURSU (Timeline) — W GŁÓWNEJ ZAKŁADCE

Wizualny pasek Gantta pokazujący co kierowca robi w każdej godzinie.

**Implementacja WinForms:**
- Panel, BackColor=`PanelDarkAlt`, border, borderRadius=6, height ~70px
- Nagłówek: "⏱️ OŚ CZASU KURSU" (TextMuted, 8pt Bold)
- Oś godzin: od godziny wyjazdu do godziny powrotu +2h margines
  - Każda pełna godzina = pionowa linia `PanelDarkBorder` + label "HH:00" (7pt, TextMuted)
- Segmenty (custom paint OnPaint):
  - Załadunek: `GreenDark`, ikona "📦"
  - Jazda: `Blue` z przezroczystością, tekst "Jazda → [klient] (~Xkm)"
  - Rozładunek: `Purple`, ikona "📦" lub "🇷🇴"
  - Powrót: `Red` z przezroczystością, tekst "← Powrót"
  - Każdy segment: borderRadius=3, wysokość 14px, wewnątrz tekst 7pt Bold biały
- Marker "TERAZ": czerwona pionowa linia 2px, label "TERAZ" nad nią (Red, 7pt Bold)
- Legenda pod osią: małe kolorowe kwadraty 8x8 + etykiety (8pt, TextLight)
- Czasy jazdy obliczaj szacunkowo: ładowanie przystanków z godzinami, dystans między nimi podziel na średnią prędkość 60km/h

**Dane wejściowe:**
```
Oblicz na podstawie:
- course.GodzinaWyjazdu (np. 06:00)
- course.GodzinaPowrotu (np. 18:00)  
- course.Stops[].PlannedArrival (np. 08:00, 18:00)
- Szacunkowy czas rozładunku: 30 min na przystanek
- Reszta czasu = jazda
```

### 3. KONFLIKTY — KOMPAKTOWA WERSJA W GŁÓWNEJ ZAKŁADCE

Nie zajmuje dużo miejsca. Kompaktowy pasek z podsumowaniem + rozwijalna lista.

**Implementacja:**
- Panel, BackColor=`PanelDarkAlt`, border, borderRadius=6, height domyślnie ~40px (zwinięty) lub ~120px (rozwinięty)
- Wiersz podsumowania (zawsze widoczny):
  - "⚠ KONFLIKTY" (TextWhite, 10pt Bold)
  - Pill [🔴 2 błędy] (Red bg, white text)
  - Pill [🟡 2 ostrz.] (Orange bg)
  - Pill [🔵 2 info] (Blue bg)
  - Przycisk [Rozwiń ▼] / [Zwiń ▲] po prawej
- Po rozwinięciu: lista alertów (każdy 1 linijka):
  - Kolor border-left 3px: Red/Orange/Blue zależnie od poziomu
  - Tło: RedBg/OrangeBg/BlueBg
  - Ikona + treść (10pt, TextDark)
  - Klik na alert → dodatkowe szczegóły pod spodem

**14 typów konfliktów do wykrywania (stwórz ConflictDetectionService):**

```
BŁĘDY (Error — czerwone):
1. NO_DRIVER — Brak kierowcy
2. NO_VEHICLE — Brak pojazdu  
3. CAPACITY_OVERLOAD — Przeładowanie palet >100%
4. WEIGHT_OVERLOAD — Przekroczenie DMC (waga towaru + tara > DMC pojazdu)
5. DRIVER_DOUBLE_BOOKING — Kierowca przypisany do innego kursu w tym samym czasie
6. VEHICLE_DOUBLE_BOOKING — Pojazd w 2 kursach naraz

OSTRZEŻENIA (Warning — pomarańczowe):
7. CAPACITY_HIGH — Naczepa >80% (ale jeszcze nie przeładowana)
8. WEIGHT_HIGH — Waga >80% DMC
9. DRIVER_HOURS — Czas pracy kierowcy >12h (godzina powrotu - godzina wyjazdu)
10. DUPLICATE_CLIENT — Ten sam klient w tym i innym kursie tego dnia
11. FOREIGN_ADDRESS — Adres zagraniczny (szukaj słów: Rumunia, MUN., STR., Deutschland itp.) → potrzebne CMR
12. RETURN_LATE — Ostatni przystanek po godzinie powrotu

INFO (niebieskie):
13. NEARBY_ORDER — Zamówienie nieprzypisane z tego samego regionu (pierwsze 2 cyfry kodu pocztowego)
14. MULTI_HANDLOWIEC — Zamówienia od wielu handlowców w jednym kursie

Wywołuj DetectAll() po KAŻDEJ zmianie: dodanie/usunięcie ładunku, zmiana kierowcy, zmiana pojazdu, zmiana godzin.
```

### 4. TABELA ŁADUNKÓW W KURSIE (lewy panel, środek)

**Nagłówek sekcji:**
- "🚚 ŁADUNKI W KURSIE" (White, 12pt Bold) + pill z liczbą [2] (Green bg, white)
- Po prawej: "KOLEJNOŚĆ:" (TextMuted, 8pt Bold) + przyciski [▲] [▼] (Blue bg, 24x24) + [Sortuj] (Purple bg, padding 3px 10px)

**DataGridView — ciemny motyw:**
```
EnableHeadersVisualStyles = false
BackgroundColor = PanelDark
GridColor = PanelDarkBorder
BorderStyle = None
CellBorderStyle = SingleHorizontal
RowHeadersVisible = false
AllowUserToAddRows = false
SelectionMode = FullRowSelect
RowHeight = 36

ColumnHeadersDefaultCellStyle:
  BackColor = PanelDarkBorder (#3D3F5C)
  ForeColor = TextMuted (#8E90A6)
  Font = Segoe UI 8.5pt Bold

DefaultCellStyle:
  BackColor = PanelDark (#2B2D42)
  ForeColor = TextLight (#C8CAD8)
  SelectionBackColor = Purple z 33% alpha
  SelectionForeColor = White

AlternatingRowsDefaultCellStyle:
  BackColor = PanelDarkAlt (#323450)
```

**Kolumny ładunków:**
| Kolumna | Szerokość | Font | Kolor |
|---------|-----------|------|-------|
| Lp. | 40px, center | 14pt Bold | Green |
| Klient | 160px | 10pt Bold | White |
| Data uboju | 90px | 10pt | TextLight |
| Palety | 65px, right | 11pt Bold | OrangeLight |
| Poj. | 65px, right | 10pt | Green |
| Adres | Fill | 10pt | TextMuted |
| Uwagi | 180px | 10pt | TextLight |

**Podsumowanie pod tabelą:**
- Panel, height=28, BackColor=`PanelDark`
- "Σ Palety: **21.4** • Σ Pojemniki: **772** • Σ Waga: **4 104** kg"
- Wartości Bold w kolorach: palety=Orange, pojemniki=Green, waga=TextLight

**Interakcje ładunków:**
- Delete na klawiaturze → usuwa ładunek z kursu
- ▲▼ → zmienia kolejność (swap Lp z sąsiadem)
- Sortuj → sortuje wg PlannedArrival

### 5. ZAKŁADKI DOLNE (pod ładunkami, nad przyciskami)

TabControl lub własny panel z przyciskami zakładek. 4 zakładki:

#### Zakładka "Kurs" (domyślna — pusta, bo info jest wyżej)
Wyświetla dodatkowe info o kursie: uwagi, notatki dla kierowcy, dokumenty.

#### Zakładka "📜 Historia"
Pokazuje historię dostaw do wybranego klienta (kliknij ładunek w tabeli → pokaż historię tego klienta).

**Zawartość:**
- Nagłówek: "[nazwa klienta] — ostatnie 5 dostaw" (PurpleLight, 10pt Bold)
- Tabela z kolumnami: Data | Kierowca | Palety | Godz. | Uwagi
  - Tekst w kolorach: Data=TextLight, Kierowca=White, Palety=Orange, Godz=PurpleLight
  - Uwagi "OK" = Green, "Spóźnienie"/"Reklamacja" = Red
- Podsumowanie: "📊 Śr. zamówienie: 19.1 pal • Preferowany kierowca: Czapla (3/5) • Okno: 16:30-18:00" (Blue bg, 9pt)

**Źródło danych:** zapytanie do bazy o ostatnie dostawy do tego klienta, wyciągnij kierowcę, palety, godzinę, uwagi.

#### Zakładka "📋 Szablony"
Zapisane szablony kursów (częste trasy).

**Zawartość:**
- 3 karty obok siebie (FlowLayoutPanel):
  - Każda karta: BackColor=`PanelDarkAlt`, border, borderRadius=6, borderTop 3px solid [kolor trasy]
  - Nazwa: "Trasa Warszawa" (10pt Bold, kolor trasy)
  - Trasa: "O&M → Damak → Destan → Trzepałka" (8pt, TextLight)
  - Pills: [📅 3x/tydz] [🚛 Czapla] (PanelDarkBorder bg, TextLight)
  - Przycisk [Użyj →] (BackColor=kolor trasy, biały, 8pt Bold)
- Kolory tras: Warszawa=Green, Południe=Purple, Export=Orange
- Klik "Użyj" → wypełnia combo kierowcy, pojazdu i dodaje ładunki z szablonu

#### Zakładka "💰 Koszty/Waga"
2 panele obok siebie (50/50):

**Panel lewy — Kalkulacja kosztów:**
- BackColor=`PanelDarkAlt`, border, borderRadius=6
- "💰 KALKULACJA KOSZTÓW" (TextMuted, 8pt Bold)
- Grid 2x2 kafelków:
  - "Dystans" → "680 km" (Blue, 13pt Bold)
  - "Paliwo ~" → "204 L" (Orange)
  - "Koszt" → "1 224 zł" (Red)
  - "Czas" → "~14h" (Purple)
- Pod spodem:
  - "Wartość towaru: **18 450 zł**" (Green, 10pt Bold)
  - "Koszt/kg: **0.30 zł/kg**" (Orange, 10pt Bold)

**Panel prawy — Waga na osiach:**
- "⚖️ WAGA NA OSIACH" (TextMuted, 8pt Bold)
- 3 słupki (bar chart, custom paint):
  - Oś 1 (przód): 1200/3000 kg → Green
  - Oś 2 (środek): 1800/3000 kg → Orange  
  - Oś 3 (tył): 1104/3000 kg → Green
  - Słupki: gradient od koloru 44% na górze do pełnego na dole
- Pod słupkami: "DMC: 4 104 / 18 000 kg ✓ OK" (Green bg, 9pt Bold, GreenLight)

**Obliczenia:**
```
Dystans: suma dystansów między przystankami (z zewnętrznej tabeli odległości lub szacunkowo)
Paliwo: dystans * 30L/100km (średnie zużycie ciężarówki)
Koszt paliwa: paliwo * 6.00 zł/L
Czas: dystans / 60 km/h (średnia prędkość) + 30min na każdy rozładunek
Wartość towaru: suma cen zamówień w kursie
Koszt/kg: koszt paliwa / suma wagi
Waga na osiach: równomierny rozkład wagi towaru na 3 osie
```

### 6. PANEL ZAMÓWIEŃ (prawy — jasny)

**Nagłówek zielony:**
- Panel, Height=38, BackColor=`Green` (#43A047)
- "📋 ZAMÓWIENIA" (White, 12pt Bold)
- Pill [14 zam.] (semi-transparent white bg)
- Po prawej: toggle [Ubój|Odbiór], searchbox [🔍 Szukaj], date picker [📅 14.02], przycisk [Dziś]

**Nagłówki kolumn (sticky):**
- BackColor=`PanelLightAlt`, borderBottom 2px `PanelLightBorder`
- Font 8.5pt Bold, TextGray, uppercase
- Kolumny: [priorytet kropka] | Odbiór | Godz. | Palety | Poj. | Klient | Adres

**Grupowanie po dacie odbioru:**
- Wiersz grupy: pełna szerokość, padding 5px 10px
  - Poniedziałek: BackColor=`GreenBg`, borderLeft 3px `Green`, tekst "► 16.02 poniedziałek" (GreenDark, 10pt Bold) + "8 zamówień" (TextGray)
  - Wtorek: BackColor=`OrangeBg`, borderLeft 3px `Orange`, tekst "► 17.02 wtorek" (Orange, 10pt Bold)
  - Środa: BackColor=`BlueBg`, borderLeft 3px `Blue`
  - itd.

**Wiersz zamówienia:**
- Zebra: co drugi wiersz `PanelLightAlt`
- Zaznaczony: BackColor=`PurpleRow` (#E8D5F5), borderLeft 3px `Purple`
- Priorytet (pierwsza kolumna, 28px):
  - Normal: zielona kropka 8x8 (`Green`)
  - High: czerwona kropka 8x8 (`Red`) + boxShadow glow
  - Express: fioletowa kropka 8x8 (`Purple`) + boxShadow glow
  - Low: szara kropka (`TextFaint`)
- Godzina: pill z BackColor=`PurpleBg`, kolor `Purple`, border `PurpleBg2`, font 9pt Bold
- Palety: `Orange`, font 11pt Bold, right-aligned
- Klient: `TextDark`, font 10pt Bold, maxWidth z ellipsis
- Adres: `TextGray`, font 9pt, maxWidth z ellipsis

**Footer — Dodaj zaznaczone:**
- Panel, Height=44, BackColor=`GreenBg`
- Button na pełną szerokość: "⬇ Dodaj zaznaczone do kursu (X)" 
  - BackColor=`Green`, White, 11pt Bold, borderRadius=6
  - X = liczba zaznaczonych zamówień
  - Po kliknięciu: dodaje wszystkie zaznaczone zamówienia jako ładunki do kursu

**Interakcje zamówień:**
- Klik na wiersz → zaznacz/odznacz (toggle PurpleRow)
- Double-click → od razu dodaj do kursu (bez zaznaczania)
- Ctrl+klik → multi-select
- Ctrl+F → focus na searchbox
- Szukaj → filtruj po nazwie klienta lub adresie

### 7. PRZYCISKI (lewy panel, sam dół)

- Panel, Height=50, BackColor=`PanelDark`, borderTop 1px `PanelDarkBorder`
- FlowDirection=RightToLeft:
  - [✓ ZAPISZ KURS] — BackColor=gradient(`Green` → `GreenDark`), White, 13pt Bold, padding 8px 32px, borderRadius=6, boxShadow `Green` 44% alpha
    - Jeśli są Error-y w konfliktach → BackColor=`Orange`, tekst "⚠ ZAPISZ KURS (z ostrzeżeniami)"
    - Klik + errory → MessageBox.YesNo z listą błędów
  - [ANULUJ] — transparent bg, border 1px `PanelDarkBorder`, TextMuted, 11pt Bold

---

## SKRÓTY KLAWISZOWE

Zaimplementuj w KeyDown formy (KeyPreview=true):

| Klawisz | Akcja |
|---------|-------|
| Enter lub Double-click | Dodaj zamówienie do kursu |
| Delete | Usuń wybrany ładunek z kursu |
| Ctrl+S | Zapisz kurs |
| Ctrl+Z | Cofnij ostatnią zmianę (undo stack) |
| ↑↓ | Nawigacja po tabeli (domyślne DGV) |
| Alt+↑ | Przesuń ładunek w górę |
| Alt+↓ | Przesuń ładunek w dół |
| Ctrl+F | Focus na searchbox zamówień |
| F5 | Odśwież listę zamówień z bazy |

---

## LOGIKA PO KAŻDEJ ZMIANIE

Za każdym razem gdy zmienia się cokolwiek w kursie (dodanie/usunięcie ładunku, zmiana kierowcy/pojazdu/godzin), wywołaj:

```csharp
private void OnCourseChanged()
{
    // 1. Przelicz sumy
    RefreshSummary(); // palety, pojemniki, waga
    
    // 2. Zaktualizuj capacity bar
    capacityBar.SetCapacity(course.SumaPalet, course.Pojazd?.MaxPalet ?? 4);
    
    // 3. Zaktualizuj route pills
    routePills.SetRoute(course.Stops.OrderBy(s => s.Lp).Select(s => s.NazwaKlienta).ToArray());
    
    // 4. Zaktualizuj timeline
    timeline.SetCourse(course);
    
    // 5. Wykryj konflikty
    var conflicts = conflictService.DetectAll(course, allOrders, allCourses);
    conflictPanel.SetConflicts(conflicts);
    
    // 6. Zmień wygląd przycisku Zapisz
    bool hasErrors = conflicts.Any(c => c.Level == ConflictLevel.Error);
    btnSave.BackColor = hasErrors ? ZpspColors.Orange : ZpspColors.Green;
    btnSave.Text = hasErrors ? "⚠ ZAPISZ KURS (z ostrzeżeniami)" : "✓ ZAPISZ KURS";
    
    // 7. Odśwież listę zamówień (oznacz przypisane)
    RefreshOrdersGrid();
}
```

---

## STRUKTURA PLIKÓW DO STWORZENIA

```
Theme/
  ZpspColors.cs         — Wszystkie kolory jako static readonly Color
  ZpspFonts.cs          — Wszystkie fonty jako static readonly Font

Models/
  TransportModels.cs    — Order, CourseStop, TransportCourse, Driver, Vehicle, CourseConflict
                          + enumy: OrderPriority, StopStatus, ConflictLevel

Controls/
  CapacityBarControl.cs     — Custom ProgressBar z hatching
  RoutePillsControl.cs      — FlowLayoutPanel z kolorowymi pills
  ConflictPanelControl.cs   — Panel alertów (kompaktowy + rozwijalny)
  TimelineControl.cs        — Gantt chart osi czasu kursu
  AxleWeightControl.cs      — Wizualizacja wagi na osiach (3 słupki)

Services/
  ConflictDetectionService.cs — Silnik 14 typów konfliktów

KursEditorForm.cs           — Główna forma z layoutem Wariant A
KursEditorForm.Designer.cs  — Designer (jeśli potrzebujesz, ale lepiej kodowo)
```

---

## WAŻNE ZASADY IMPLEMENTACJI

1. **DoubleBuffered = true** na formie i wszystkich custom kontrolkach (unikaj migotania)
2. **Nie używaj Designer.cs** do layoutu — twórz kontrolki w kodzie (łatwiej zarządzać)
3. **TableLayoutPanel** do głównego podziału 52/48 — `ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52f))`
4. **Zaokrąglone rogi** — custom paint z `GraphicsPath.AddArc()`:
```csharp
private static GraphicsPath RoundedRect(Rectangle bounds, int radius)
{
    var path = new GraphicsPath();
    int d = radius * 2;
    path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
    path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
    path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
    path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
    path.CloseFigure();
    return path;
}
```
5. **DataGridView** — `EnableHeadersVisualStyles = false` żeby custom kolory nagłówków działały
6. **SmoothingMode.AntiAlias** + **TextRenderingHint.ClearTypeGridFit** w OnPaint
7. **SuspendLayout/ResumeLayout** przy masowych zmianach kontrolek
8. **Minimum Size** formy: 1200x700, domyślnie 1500x900

---

## PODSUMOWANIE ZAKŁADEK

| Zakładka | Gdzie | Co zawiera |
|----------|-------|------------|
| **Główna** | Lewy panel góra | Header + Route Pills + Capacity Bar + **Timeline** + **Konflikty kompaktowe** |
| **📜 Historia** | Tab pod ładunkami | Ostatnie 5 dostaw do wybranego klienta z tabeli ładunków |
| **📋 Szablony** | Tab pod ładunkami | 3 karty z częstymi trasami + przycisk "Użyj" |
| **💰 Koszty/Waga** | Tab pod ładunkami | Kalkulacja kosztów + wizualizacja wagi na osiach |

---

Zrób to dokładnie jak opisano. Nie pomijaj żadnego szczegółu kolorów, fontów, rozmiarów. Każdy element ma mieć dokładnie te kolory które podałem. Testuj czy kompiluje się bez błędów. Font wszędzie Segoe UI.
