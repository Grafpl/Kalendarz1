# 🏭 Koncepcja 2: Fabryka — wizualna metafora (top-view)

**Inspiracja**: SCADA, MES dashboards, Schneider EcoStruxure, Siemens TIA Portal

## Idea
Widok rzutu z góry **rzeczywistej hali produkcyjnej**. Magazyny jako budynki/silosy z poziomem wypełnienia, taśmociągi jako linie z animowanym przepływem, kurczaki jako ikonki przesuwające się po linii. **Dosłowna metafora zakładu**.

## Mockup

```
┌────────────────────────────────────────────────────────────────────────────┐
│                                                                            │
│   🚛 PRZYJĘCIE                          ⚙ HALA UBOJU                       │
│   ┌───────────┐                         ┌──────────────────────────────┐  │
│   │ Brama 🐔  │═══════►═══════►═══════►│ ████████ Linia uboju ████████ │  │
│   │           │  taśmociąg żywca       │                              │  │
│   │  1 250 t  │                         │  85% wydajność  ⚠ obciążenie │  │
│   └───────────┘                         └──────────────┬───────────────┘  │
│                                                        ║                  │
│                                          (rozdział)    ║                  │
│                              ┌─────────────────────────╨─────────────┐    │
│                              ║ 58% na krojenie         42% bezpośr.  │    │
│                              ▼                                       ▼    │
│   🔪 KROJENIE                                          📦 SORTOWNIA      │
│   ┌──────────────────────┐                              ┌──────────────┐  │
│   │ ▓▓▓▓▓▓▓▓ tasak ▓▓▓▓▓ │═══════►                      │ Pakowanie    │  │
│   │  62% wyd ✓ norma     │                              │ tuszek       │  │
│   └──────────┬───────────┘                              └──────┬───────┘  │
│              ║                                                  ║         │
│   ┌──────────╨──────────┐                                       ║         │
│   ▼                     ▼                                       ║         │
│ Filet  Korpus  Skrzydło Pierś                                   ║         │
│  ║       ║        ║      ║                                      ║         │
│  ╚═══════╩════════╩══════╩══╗                                  ║         │
│                              ║                                  ║         │
│                              ▼                                  ▼         │
│  ┌────────┐  ┌────────┐  ┌────────┐  ┌────────┐  ┌────────────────┐      │
│  │ ❄ MROŹ │  │🥓MASAR │  │🌾KARMA │  │🗑 ODPADY│  │  📦 DYSTRYBUCJA │      │
│  │████░░░ │  │██░░░░░ │  │█░░░░░░ │  │█░░░░░░ │  │ ████████░░░░░  │      │
│  │ 150 t  │  │  80 t  │  │  50 t  │  │  65 t  │  │     780 t      │      │
│  │  ~78°C │  │        │  │        │  │  ⚠ 8% │  │   60% pełne    │      │
│  └────────┘  └────────┘  └────────┘  └────────┘  └────────┬───────┘      │
│                                                            ║              │
│                                                  🚛═══════►║═══════🚛     │
│                                                  Wysyłka do klientów      │
│                                                  720 t / dziś              │
└────────────────────────────────────────────────────────────────────────────┘

Status linii: 🟢 PRACUJE  | 🟡 OBCIĄŻONA  | 🔴 STOP
```

## Elementy interaktywne
- **Magazyny jako silosy** — animowany poziom wypełnienia (`fill: 0% → 100%`)
- **Taśmociągi** — pulsujące paski (CSS-style animation w WPF)
- **Ikonki kurczaków** — przesuwające się wzdłuż linii (Storyboard XAML)
- **Hover linii** → szczegóły wydajności + tooltip
- **Click magazynu** → otwiera widok tego magazynu
- **Alarmy** świecące czerwono nad zagrożonymi obszarami (np. ODPADY 8%)

## Pros
✅ Najbardziej "wow" wizualnie — wszyscy się zatrzymują
✅ Dosłownie odzwierciedla zakład (zarząd widzi swoją fabrykę)
✅ Intuicyjne dla operatorów hali — znają układ
✅ Świetne dla TV w sali konferencyjnej / hali

## Cons
❌ Bardzo trudne do zaimplementowania (~20-40h w WPF)
❌ Trudno utrzymać przy zmianach layoutu zakładu
❌ Może być przeładowane dla decyzji "ile kg poszło gdzie"
❌ Wymaga sporej powierzchni ekranu (min 1920×1080, najlepiej 4K)
❌ Animacje mogą rozpraszać przy poważnej pracy

## Trudność: 5/5
- Custom geometria (kształty magazynów, linii)
- Animations (przepływ na liniach, fill levels)
- Asset design (potrzebny grafik)

## Kiedy stosować
- **Wielki monitor w sali konferencyjnej / na hali** — pokazuje "live status"
- **Showroom dla klientów / audytorów** — robi wrażenie
- **Onboarding nowych pracowników** — uczą się układu zakładu wizualnie
- **NIE jako codzienne narzędzie pracy** — zbyt rozpraszające

## Pliki potrzebne
- SVG/XAML kształtów hali, taśmociągów, silosów
- Animations XAML Storyboards
- Heat maps overlay (gdzie problemy)
