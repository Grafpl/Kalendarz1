# 🕸 Koncepcja 7: Network Graph (siatka połączeń)

**Inspiracja**: D3.js force-directed graphs, Neo4j Bloom, Gephi, social network analysis

## Idea
Każdy etap = **węzeł grafu**. Krawędzie = przepływy (grubość = kg). Force-directed layout sam się układa. Hover na węzeł podświetla połączenia.

## Mockup

```
                                            ╭───────╮
                                  ╭────────►│KLIENCI│
                                  │  720t   │ 720t  │
                                  │         ╰───────╯
                                  │
                  ╭───────╮  780t ╭───╨───╮
                  │ UBÓJ  │──────►│ DYST  │
                  │1 065t │       │ 780t  │
                  ╰───┬───╯       ╰───┬───╯
       1 250t       │ 615t          │
   ╭───────╮        │              │
   │ ŻYWIEC│────────╯              │
   │1 250t │   ╭───╨───╮            │
   ╰───────╯  ╲│ PROD  │────────────┤
              ╱│ 380t  │            │
              ╲╰───┬───╯            │
              ╱    │                │
                   │ 150t ╭───────╮ │
                   ├─────►│ MROŹ  │ │
                   │      │ 150t  │ │
                   │      ╰───────╯ │
                   │                │
                   │ 80t  ╭───────╮ │
                   ├─────►│MASAR  │ │
                   │      │  80t  │ │
                   │      ╰───────╯ │
                   │                │
                   │ 50t  ╭───────╮ │
                   ├─────►│KARMA  │ │
                   │      │  50t  │ │
                   │      ╰───────╯ │
                   │                │
                   │ 65t  ╭───────╮ │
                   └─────►│ODPADY │ │
                          │  65t  │ │
                          ╰───────╯ │
                                    │
                                   ╭╧═════╗
                                   │STRATA║   ← strata jako "drain"
                                   │ 185t║
                                   ╰══════╯

  Legenda:
  • Grubość strzałki = kg
  • Kolor węzła = kategoria (czerwony=żywiec/surowiec, zielony=produkt, niebieski=mag)
  • Hover węzła → wszystkie połączenia świecą, reszta szara
```

## Interaktywne
- **Force-directed layout** — węzły same się rozmieszczają (D3 spring physics)
- **Drag węzła** → ręczne przesuwanie (pinning)
- **Hover** → highlight related, reszta blakie
- **Zoom** scroll wheel — przybliż / oddal
- **Click węzła** → szczegóły, **click krawędzi** → szczegóły przepływu

## Pros
✅ Najbardziej **akademiczna** wizualizacja — dla data scientists
✅ Łatwo rozszerzyć — nowy węzeł = nowy magazyn / nowa partia
✅ Mocno wizualne — widać "kim jest hub" (PROD)
✅ Świetne do **analiz "co jeśli zniknie X"**

## Cons
❌ Trudna implementacja w WPF (brak D3.js — trzeba samemu force-directed algo)
❌ Mało precyzyjne — kg nie są dokładne, tylko proporcjonalne
❌ Trudne dla niewtajemniczonych ("co to za pajęczyna?")
❌ Trudne na małych ekranach

## Trudność: 5/5
- Force-directed physics (Verlet integration)
- Custom Path geometry per edge (Bezier z curve)
- ~30-50h implementacji od zera

## Kiedy stosować
- **Analytics deep-dive** — gdy chcesz zrozumieć **strukturę** przepływów
- **Demo techniczne** — robi wrażenie na data ludziach
- **NIE** dla codziennej operacji — za skomplikowane
- **Alternative**: użyć WebView + D3.js w HTML zamiast natywnego WPF
