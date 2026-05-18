# 📌 Koncepcja 6: Card Grid (Pinterest/Notion-style)

**Inspiracja**: Pinterest, Notion dashboard, Trello, Asana, Airtable galleries

## Idea
Każdy aspekt produkcji jako **niezależna karta** w masonry layout. Użytkownik może **dragować, ukrywać, dodawać własne karty**. Personalizowalne.

## Mockup

```
┌──────────────────────────────────────────────────────────────────────┐
│  🎛 Łańcuch produkcji              [+ Dodaj kartę]  [Zapisz layout] │
├──────────────────────────────────────────────────────────────────────┤
│                                                                      │
│  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────────┐  │
│  │ 🐔 ŻYWIEC       │  │ ⚙ WYD UBOJU     │  │ 🚨 ALERTY (2)        │  │
│  │                 │  │                 │  │                     │  │
│  │  1 250 t        │  │  85,2%          │  │ • Wysokie odpady    │  │
│  │  +12% ↑         │  │  ✓ NORMA        │  │   8% (norma 5%)     │  │
│  │                 │  │                 │  │                     │  │
│  │  247 partii     │  │  norma 85%      │  │ • Stagnacja MROŹ   │  │
│  │  ▁▂▃▅▆▇█        │  │  ▇▇▇━━━━━━━━━━  │  │   >7 dni           │  │
│  └─────────────────┘  └─────────────────┘  └─────────────────────┘  │
│                                                                      │
│  ┌────────────────────────────────────┐  ┌────────────────────────┐ │
│  │ 📦 ROZKŁAD WYJŚCIA                  │  │ 🔪 WYD KROJENIA         │ │
│  │                                     │  │                        │ │
│  │ DYST  ████████████ 60%             │  │   62,1%                │ │
│  │ MROŹ  ████          15%            │  │   ✓ NORMA              │ │
│  │ MASAR ██             8%            │  │                        │ │
│  │ KARMA █              5%            │  │   sRWP: 615 t          │ │
│  │ ODP   █              6% ⚠          │  │   sPWP: 380 t          │ │
│  │ ZOST  ██             6%            │  │                        │ │
│  └────────────────────────────────────┘  └────────────────────────┘ │
│                                                                      │
│  ┌────────────────────────┐  ┌──────────────────────────────────┐   │
│  │ 🥩 TOP TOWARY DZIŚ      │  │ 📊 WYDAJNOŚĆ — TREND 30 dni      │   │
│  │                        │  │                                  │   │
│  │ 1. Filet z piersi      │  │  85┤━━━━━━━━━━━━━━━━━━━━━ norma  │   │
│  │    142 t                │  │  82┤    ╭╮     ╭╮               │   │
│  │ 2. Tuszka 1100-1300     │  │  80┤━━╯ ╰━━━━━╯ ╰━━━━━━━━━━━━━━ │   │
│  │    98 t                 │  │     ↑                            │   │
│  │ 3. Skrzydło             │  │     awaria 27.04                 │   │
│  │    65 t                 │  │                                  │   │
│  │ ... [pełna lista]       │  └──────────────────────────────────┘   │
│  └────────────────────────┘                                          │
│                                                                      │
│  ┌────────────────────────┐  ┌─────────────────────────────────────┐│
│  │ 🚛 TRANSPORT           │  │ 🧮 BILANS MASY                       ││
│  │                        │  │                                     ││
│  │  720 t / 1 250 t       │  │   Żywiec 1 250 t                    ││
│  │  ███████░░░ 58%        │  │     = Ubój 1 065 t                  ││
│  │                        │  │     + Strata 185 t (14,8%)          ││
│  │  Wysłano klientom      │  │                                     ││
│  │  Pozostało: 530 t      │  │   ✓ Zamyka się                      ││
│  │                        │  │                                     ││
│  │  89 transportów        │  │                                     ││
│  └────────────────────────┘  └─────────────────────────────────────┘│
│                                                                      │
└──────────────────────────────────────────────────────────────────────┘
```

## Funkcje
- **Drag & drop** kart żeby zmienić layout
- **Resize** kart (drag corner)
- **Hide/Show** karty (klik X)
- **Bookmark presets** — każdy użytkownik zapisuje swój widok
- **+ Dodaj kartę** — biblioteka 20-30 widgetów do wyboru
- **Hover** → szczegóły, **click** → drill-down

## Każdy widget = oddzielny komponent
Można dodać widgety:
- Hodowcy top 5 dnia
- Kontrahenci top 5 dnia
- Klasy wagowe (chart)
- Mapa geograficzna klientów
- Magazyn X saldo
- Trend X-Y dni dla X metryki
- Tabela ostatnich dokumentów
- Mini Sankey
- Donut chart
- Kalendarz heatmap

## Pros
✅ **Personalizowalne** — każdy ma swój widok
✅ **Skalowalne** — łatwo dodać nowe widgety
✅ **Modułowe** — można reużywać widget w innych miejscach
✅ Dobrze rozumiane (Notion/Trello standard)
✅ Każda karta klikalna do drill-down

## Cons
❌ Brak narracyjnej spójności — chaos jak chcesz
❌ Implementacja drag&drop w WPF niełatwa (~10-15h)
❌ Wymaga bazy układów per user (DB)
❌ Może być przeładowane bez dyscypliny

## Trudność: 3/5 (basic) | 5/5 (full with drag-drop+save)
~6-8h dla grid bez personalizacji
~20-30h z drag/drop, resize, save layouts

## Kiedy stosować
- **Dla różnych ról** — kierownik PROD ma inny widok niż handlowiec
- **Power users** którzy chcą konfigurować
- **Długoterminowy projekt** — investing w jeden widok który rośnie
