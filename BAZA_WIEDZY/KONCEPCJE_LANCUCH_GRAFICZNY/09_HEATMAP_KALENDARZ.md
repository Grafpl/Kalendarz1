# 📅 Koncepcja 9: Heatmap Calendar (GitHub contributions style)

**Inspiracja**: GitHub contributions graph, Apple Health, Year in Pixels

## Idea
**12 miesięcy × 365 dni** jako siatka małych kwadratów. Kolor każdego dnia = wartość metryki (np. wydajność uboju). Od razu widać **wzorce sezonowe**, dni problemowe, dłuższe trendy.

## Mockup

```
┌─────────────────────────────────────────────────────────────────────────────────┐
│  📅 Łańcuch produkcji 2026 — heatmap roczny                                     │
│                                                                                 │
│  Metryka: [Wydajność uboju ▼]   Okres: [Cały rok ▼]   Min: 75% Max: 90%       │
│                                                                                 │
│       Sty  Lut  Mar  Kwi  Maj  Cze  Lip  Sie  Wrz  Paź  Lis  Gru               │
│  Pn  ░▒▒▓▓ ▒▓▓▓▓ ▓▓▓▒▒ ▓▓▓▓▓ ▓▓▓ . . . . . . . . . . . . . . . . . . .          │
│  Wt  ░▒▒▓▓ ▒▓▓▓▓ ▓▓▓▒▒ ▓▓▓▓▓ ▓▓▓ . . . . . . . . . . . . . . . . . . .          │
│  Śr  ░▒▒▓▓ ▒▓▓▓▓ ▓▓▓▒▒ ▓▓▓▓▓ ▓▓▒ . . . . . . . . . . . . . . . . . . .          │
│  Cz  ░▒▒▓▓ ▒▓▓▓▓ ▓▓▓▒▒ ▓▓▓▓▓ ▓▓▒ . . . . . . . . . . . . . . . . . . .          │
│  Pt  ▒▒▓▓▓ ▓▓▓▓█ ▓▓▒░░ ▓▓▓▓▓ ▓▓░ . . . . . . . . . . . . . . . . . . .          │
│  So  . . .  . . . . . . . . . . . . . . . . . . . . . . . . . . . . . .          │
│  Nd  . . .  . . . . . . . . . . . . . . . . . . . . . . . . . . . . . .          │
│                                                                                 │
│  Skala:  ░ <75%   ▒ 75-80%   ▓ 80-85% (norma)   █ >85% (świetnie)               │
│                                                                                 │
│                                                                                 │
│  📌 Dni o wartości <75% (poniżej normy):                                        │
│  • 04.01.2026 — 73% (po świątecznej przerwie)                                   │
│  • 12.03.2026 — 71% (awaria linii — 4h przestój)                                │
│  • 28.04.2026 — 70% (nowa partia od Kowalskiego, klasa B)                       │
│                                                                                 │
│  Hover dnia → tooltip ze szczegółami                                            │
│  Klik dnia → otwiera ten dzień w głównym widoku                                 │
└─────────────────────────────────────────────────────────────────────────────────┘
```

## Można pokazać wiele metryk (toggle):
- **Wydajność uboju %** (norma 85%)
- **Wydajność krojenia %** (norma 62%)
- **Odpady %** (norma <5% — niskie=zielone, wysokie=czerwone)
- **Sprzedaż t** (im więcej, tym ciemniejsze)
- **Liczba alarmów** (im więcej, tym czerwniejsze)
- **Razem przetworzono t**

## Pros
✅ **Najlepsze do wzorców czasowych** — sezonowość, dni tygodnia, święta
✅ Bardzo gęsta informacja w małej przestrzeni (365 dni na 1 ekranie)
✅ Łatwo zauważyć "ten tydzień jest gorszy" / "marzec był najlepszy"
✅ Świetne dla **rocznych raportów** dla zarządu
✅ Łatwe do zaimplementowania (~3-5h)

## Cons
❌ Wymaga dużo danych historycznych (sensowne dopiero po 6+ miesiącach)
❌ Skala kolorystyczna trudna do dobrego doboru
❌ Bezużyteczne dla **dzisiejszej decyzji** (tylko historia)
❌ Trudne dla daltonistów (gradient kolorów)

## Trudność: 2/5
- Grid z 365 prostokątami (`UniformGrid` lub `Canvas`)
- Konwerter wartość→kolor
- Tooltip per komórka
- ~3-5h w WPF

## Kiedy stosować
- **Roczny dashboard** — raport za rok dla zarządu / audytu
- **Detekcja wzorców** — "czy zima jest gorsza?"
- **Karta dodatkowa** obok głównego widoku (toggle)
- **Trend analysis** — porównać 2024 vs 2025 vs 2026

## Wariant: 4-week heatmap dla codziennej operacji
```
        Pn   Wt   Śr   Cz   Pt   So   Nd
W1     ▓▓▓  ▓▓▓  ▓▓░  ▓▓▓  ▓▓▓   .    .
W2     ▓▓▓  ▒▒▒  ▒░░  ▓▓▓  ▓▓▓   .    .   ← awaria w środku tygodnia
W3     ▓▓▓  ▓▓▓  ▓▓▓  ▓▓▓  ▓▓▓   .    .
W4     ▓▓▓  ▓▓▓  ▓▓▓  ▓▓▓  ▒▒░   .    .   ← piątek słabszy
```
Mniejsza siatka, pokazuje tylko ostatni miesiąc — bardziej praktyczne dla operacji.
