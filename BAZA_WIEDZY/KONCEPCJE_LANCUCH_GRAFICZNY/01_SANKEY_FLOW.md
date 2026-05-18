# 🌊 Koncepcja 1: Sankey Flow Diagram

**Inspiracja**: Tableau, D3.js Sankey, Wikipedia Sankey examples, Google Material Flow

## Idea
Jeden duży, czytelny diagram przepływu masy. **Grubość pasów = kg**. Kolory rozróżniają strumienie. Hover pokazuje szczegóły. Industry standard dla "where does material flow?".

## Mockup

```
                              ┌──────────┐
                              │ KLIENCI  │
                              │  720 t   │
                          ╔═══╪══════════╪═══╗
                          ║72%║          ║   ║
                          ║   ║          ║   ║
┌──────────┐  ╔══════════╗║   ║ ┌──────╗ ║   ║
│ ŻYWIEC   │  ║          ║║   ║ │ DYST │ ║   ║
│ 1 250 t  │══╣  UBÓJ    ║╠═══╝ │ 780 t│═╝   ║
│          │  ║ 1 065 t  ║║     │      │     ║
└──────────┘  ║          ║║     └──────┘     ║
              ╚══════════╝║                  ║
                  ║       ║  ┌──────┐        ║
                  ║       ╠══│ MROŹ │        ║
                  ║       ║  │ 150t │        ║
                  ║       ║  └──────┘        ║
                  ║       ║                  ║
            ┌─────╨───┐   ║  ┌──────┐        ║
            │ STRATA  │   ╠══│MASAR │        ║
            │ 185 t   │   ║  │ 80t  │        ║
            │(pióra/  │   ║  └──────┘        ║
            │ krew/woda)│  ║                  ║
            └─────────┘   ║  ┌──────┐        ║
                          ╠══│KARMA │        ║
                          ║  │ 50t  │        ║
                          ║  └──────┘        ║
                          ║                  ║
                          ║  ┌──────┐        ║
                          ╚══│ODPADY│        ║
                             │ 65t  │        ║
                             └──────┘        ║
                                             ║
                                       ┌─────╨────┐
                                       │ZOSTAŁO  │
                                       │ 80 t    │
                                       └─────────┘
```

## Cechy
- **Pasy proporcjonalne** — od razu widać "DYST to większość, ODPADY śladowo"
- **Łuk Bézier** między węzłami — bardziej miękki niż prosta linia
- **Etykieta z kg + %** na każdym pasie (gdy mieści się)
- **Hover** → highlight tego pasa + tooltip z dokładnymi liczbami
- **Click** węzła → szczegóły etapu (dialog)
- Kolory: węzły kolorowe (zgodne z resztą systemu), pasy w odcieniach pochodnej

## Implementacja w WPF
- `Path` z `PathGeometry` + `BezierSegment` dla każdego strumienia
- Geometria liczona w code-behind (każdy strumień = osobny Path)
- Canvas z absolute positioning węzłów
- ~300-400 linii kodu (geometria + interakcje)

## Pros
✅ Industry standard — wszyscy rozumieją natychmiast
✅ Skala wzrokowa = skala biznesowa (oko widzi proporcje)
✅ Jeden ekran = pełen łańcuch
✅ Świetne dla decyzji "gdzie idzie najwięcej?"

## Cons
❌ Trudne przy wielu źródłach (>10 strumieni robi się gęsto)
❌ Wymaga sporo logiki geometrycznej w WPF (brak gotowej biblioteki)
❌ Trudno pokazać szczegóły (towary, kontrahenci) bez click
❌ Skoki w skali — jeśli ŻYWIEC=1000t a ODPADY=10t, pasek odpadów znika

## Trudność: 4/5
~6-8h implementacji w WPF od zera (Path geometria + animacje + interakcje)

## Kiedy stosować
- **Dashboard dla zarządu** — quick "co się dzieje w produkcji"
- **Prezentacje** — łatwo zinterpretować dla niewtajemniczonych
- **Audyt zewnętrzny** — pokazujesz BRC/IFS auditor i od razu rozumie
