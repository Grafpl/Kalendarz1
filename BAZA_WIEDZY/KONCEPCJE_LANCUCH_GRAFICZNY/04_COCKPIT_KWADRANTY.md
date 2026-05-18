# 🎛 Koncepcja 4: Cockpit 4-kwadrantowy

**Inspiracja**: Bloomberg Terminal, Datadog, Grafana, ProductionMonitor.io

## Idea
Cały widok jako **4 niezależne kwadranty**, każdy odpowiada na 1 pytanie. Operator skanuje wzrokiem F-pattern, zatrzymuje się przy potrzebnym.

## Mockup

```
┌──────────────────────────────┬──────────────────────────────────┐
│ ① CO PRZYJĘLIŚMY             │ ② CO WYPRODUKOWALIŚMY            │
│   (input KPI)                │   (output KPI + wydajność)        │
│                              │                                  │
│   🐔 ŻYWIEC                  │   ⚙ UBÓJ                          │
│   1 250 t                    │   1 065 t   85,2% wyd ✓          │
│   ↑ +12% vs poprz.           │   ↑ +5% vs poprz.                │
│                              │                                  │
│   👥 partii: 247             │   🔪 PROD                         │
│   🚛 transportów: 89         │     380 t   62,1% wyd ✓          │
│   śr. waga partii: 5,06 t    │   ↑ +3%                          │
│                              │                                  │
│   Sparkline 30d:             │   Sparkline 30d wyd uboju:       │
│   ▁▂▃▅▆▇█▇▅▃▂▁ ▁▂▃▅▆        │   85━━━━━━━━━━━━━━━━━━━━ (norma) │
│                              │   ──── linia rzeczywista          │
├──────────────────────────────┼──────────────────────────────────┤
│ ③ GDZIE TRAFIŁ TOWAR         │ ④ STATUS I ALERTY                │
│   (rozkład %)                │   (alarmy + akcje)               │
│                              │                                  │
│   📦 DYST  ████████ 720t 60% │   🚨 WYSOKIE ODPADY              │
│      ↳ Klienci   710t 99%    │     8,2% (norma 3-5%)            │
│   ❄ MROŹ  ███      150t 15% │     [Zobacz dokumenty]           │
│   🥓 MASAR  █       80t  8% │                                  │
│   🌾 KARMA  █       50t  5% │   ⚠ STAGNACJA MROŹ                │
│   🗑 ODPADY █       65t  6% │     Towar zalega >7 dni          │
│   📍 ZOSTAŁO█       80t  6% │     [Wyświetl listę]              │
│                              │                                  │
│   Donut:                     │   ✓ Wyd. uboju OK                │
│   ⚪⚫⚫⚫⚫⚫⚫⚪⚪⚪⚪⚪    │   ✓ Bilans masy OK                │
│   (visualization)            │   ✓ Sprzedaż OK                  │
└──────────────────────────────┴──────────────────────────────────┘
```

## Każdy kwadrant odpowiada na 1 pytanie:
| Kwadrant | Pytanie | Czas decyzji |
|----------|---------|:------------:|
| ① INPUT | "Ile mamy surowca?" | 2 sek |
| ② OUTPUT | "Jak nam idzie produkcja?" | 3 sek |
| ③ FLOW | "Gdzie poszedł produkt?" | 5 sek |
| ④ ALERTY | "Czy są problemy?" | 1 sek |

## Cechy
- **Każdy kwadrant ma 1 cel** — brak overlapu
- **Skanowalne F-pattern**: oko czyta ⓪→①→②→③→④
- **Kwadranty są niezależne** — można rozszerzyć/zminimalizować pojedynczo
- **Sparkline + delta** w każdym KPI (Stripe-style)
- **Alerty z akcjami** (linki do widoków szczegółowych)

## Pros
✅ Najszybsza decyzja — wiesz gdzie patrzeć
✅ Łatwa implementacja w WPF (4 cards w 2×2 grid)
✅ Skalowalne — można dodać 5-ty kwadrant na dole
✅ Sprawdza się w real-world enterprise dashboardach (Bloomberg/Datadog są tym znane)

## Cons
❌ Mniej "wow" — wygląda jak każdy dashboard
❌ Trzeba dobrze zaplanować co w którym kwadrancie
❌ Dla 4K mało wykorzystuje miejsca, dla 1080p może być ciasno

## Trudność: 3/5
~4-6h w WPF (4 cards z różnym contentem)

## Kiedy stosować
- **Codzienne narzędzie pracy** kierownika produkcji
- **Daily standup** — każdy patrzy 30 sek i wie status
- **Centrum monitorowania** (1 ekran dla 1 osoby)
