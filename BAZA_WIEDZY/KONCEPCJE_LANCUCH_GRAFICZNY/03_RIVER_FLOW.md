# 🌊 Koncepcja 3: River Flow (rzeka materiału)

**Inspiracja**: D3.js water flows, biology cell diagrams, geographic flow maps

## Idea
Towar płynie jak **rzeka**. Główny nurt z ŻYWIEC dzieli się na coraz mniejsze strumienie. Każda "delta" pokazuje rozgałęzienia. Animowane fale subtelnie sugerują ruch.

## Mockup

```
            🐔
        ╭──────╮
        │ ŻYWIEC│
        │1 250t│
        ╰──┬───╯
           │
    ░░░░░░░│░░░░░░░  ← gradient strumienia (gruby u góry)
    ░░░░░░░│░░░░░░░
     ░░░░░░│░░░░░░     - strata uboju 15%
      ░░░░░│░░░░░       (parowanie wody/krwi)
       ░░░░│░░░░
           │
        ╭──┴───╮
        │ UBÓJ  │
        │1 065t│
        ╰──┬───╯
           │
     ─────┴─────  ← rozgałęzienie
    │           │
  ░░│░░░       │░░  ← cienki strumień (na krojenie)
   ░│░░         ░│░     - sprzedaż całych tuszek (42%)
    │ 58%       │ 42%
    │           │
 ╭──┴──╮     ╭──┴──╮
 │KROJ.│     │ BEZP.│
 │ 615t│     │ 450t│ ──┐
 ╰──┬──╯     ╰─────╯   │
    │                  │ (dołącza do DYST)
 ╭──┴──╮               │
 │PROD │ ───────────┐  │
 │ 380t│ 62% wyd ✓  │  │
 ╰──┬──╯            │  │
    │               │  │
 ╔══╧══════════════╧══╧══╗
 ║  PROD-DELTA               ║   ← delta rzeki (rozgałęzienia w lewo+prawo)
 ║                           ║
 ║   ┌──╨──┐                 ║
 ║   ▼     ▼ ▼ ▼ ▼ ▼         ║
 ║  DYST  MROŹ MASAR KARMA   ║
 ║  780t  150t  80t   50t    ║
 ║  ████░ ██░░ █░░   █░░     ║   ← szerokość strumienia = kg
 ║                           ║
 ╚═══════════════════════════╝
                  │
              ┌───┴───┐
              ▼       ▼
            KLIENCI  ODPADY
             720t     65t
            (99%)    (5% ⚠)
```

## Visual style
- **Gradient niebieski/zielony** — wszystko jest "wodą" płynącą
- **Animowane fale** subtle (10% opacity ripples)
- **Grubość strumienia** = kg (proporcjonalna)
- **Strata** = "parowanie" → unoszące się delikatne kropelki w bok
- **Delta** = rozgałęzienia symetryczne (jak ujście rzeki)

## Pros
✅ Naturalna metafora — "materiał płynie"
✅ Strata uboju jako "parowanie" jest piękne i intuicyjne
✅ Animacje subtelne, nie rozpraszają
✅ Świetne na duży monitor / projektor

## Cons
❌ Trudna implementacja w WPF (custom paths z animacją)
❌ Mniej precyzyjne niż Sankey (nieostre granice)
❌ Stylizacja może być zbyt "artystyczna" dla ERP
❌ Wymaga GPU dla płynnych animacji

## Trudność: 4/5
- Custom Path geometry (krzywe Bezier)
- Storyboards dla "fal"
- Computed widths per strumień

## Kiedy stosować
- **Hero screen** w sali konferencyjnej
- **Marketing** ("zobacz przepływ produkcji naszego zakładu")
- **NIE** dla codziennego użytku — zbyt estetyczne kosztem precyzji
