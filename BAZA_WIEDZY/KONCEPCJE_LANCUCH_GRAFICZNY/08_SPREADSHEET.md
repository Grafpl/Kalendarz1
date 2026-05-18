# 📊 Koncepcja 8: Spreadsheet — tabela operacyjna

**Inspiracja**: Excel, Airtable, Notion tables, SAP S/4HANA Fiori

## Idea
**Tabela. Po prostu tabela.** Każdy wiersz = dzień (lub partia). Kolumny = wszystkie metryki. Filterable, sortowable, copy-pasteable. **Dla profesjonalistów** którzy chcą cyfr a nie kolorów.

## Mockup

```
┌─────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┐
│  📊 Łańcuch produkcji — operacyjny                              [📥 Eksport CSV]  [📋 Kopiuj]  [🔍 Filter]  [⚙ Kolumny]  [F5]   │
├─────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┤
│  Data    │Żywiec │Ubój  │Wyd.U │RWP   │PWP   │Wyd.K │DYST  │KLIEN│MROŹ │MASAR│KARMA│ODP  │Zost.│Bilans│Alarmy│ Status        │
│          │  (t)  │ (t)  │  %   │ (t)  │ (t)  │  %   │ (t)  │ (t) │ (t) │ (t) │ (t) │ (t) │ (t) │  %   │      │               │
├──────────┼───────┼──────┼──────┼──────┼──────┼──────┼──────┼─────┼─────┼─────┼─────┼─────┼─────┼──────┼──────┼───────────────┤
│ 12.05.26 │ 1 250 │ 1 065│ 85.2 │  615 │  380 │ 62.1 │  780 │ 710 │ 150 │  80 │  50 │  65 │  80 │ 14.8 │  2   │ ⚠ Odpady ↑    │
│ 11.05.26 │ 1 180 │   980│ 83.1 │  590 │  365 │ 61.9 │  700 │ 690 │ 120 │  75 │  45 │  40 │ 100 │ 16.9 │  1   │ ⚠ Wyd.U ↓     │
│ 10.05.26 │ 1 300 │ 1 105│ 85.0 │  640 │  395 │ 61.7 │  820 │ 720 │ 160 │  90 │  55 │  50 │  60 │ 15.0 │  0   │ ✓             │
│ 09.05.26 │ 1 220 │ 1 050│ 86.1 │  605 │  378 │ 62.5 │  790 │ 750 │ 140 │  85 │  48 │  45 │  72 │ 13.9 │  0   │ ✓             │
│ 08.05.26 │ 1 200 │ 1 020│ 85.0 │  600 │  370 │ 61.7 │  780 │ 720 │ 130 │  80 │  50 │  48 │  82 │ 15.0 │  0   │ ✓             │
│ 07.05.26 │ 1 180 │   985│ 83.5 │  580 │  362 │ 62.4 │  720 │ 695 │ 135 │  78 │  46 │  62 │  88 │ 16.5 │  1   │ ⚠ Odp ↑       │
│ 06.05.26 │ 1 150 │   970│ 84.3 │  570 │  355 │ 62.3 │  710 │ 680 │ 125 │  72 │  44 │  45 │  79 │ 15.7 │  0   │ ✓             │
├──────────┼───────┼──────┼──────┼──────┼──────┼──────┼──────┼─────┼─────┼─────┼─────┼─────┼─────┼──────┼──────┼───────────────┤
│ 7 dni    │ 8 480 │ 7 175│ 84.6 │ 4 200│ 2 605│ 62.0 │ 5 300│4 965│ 960 │ 560 │ 338 │ 355 │ 561 │ 15.4 │  4   │ 4 alarmy/tyg  │
│ Σ        │       │      │      │      │      │      │      │     │     │     │     │     │     │      │      │               │
├──────────┼───────┼──────┼──────┼──────┼──────┼──────┼──────┼─────┼─────┼─────┼─────┼─────┼─────┼──────┼──────┼───────────────┤
│ ŚREDNIA  │ 1 211 │ 1 025│ 84.6 │  600 │  372 │ 62.0 │  757 │ 709 │ 137 │  80 │  48 │  51 │  80 │ 15.4 │      │               │
│ NORMA    │       │      │ 85.0 │      │      │ 62.0 │      │     │     │     │     │  5% │     │ ≤22  │      │               │
│ Δ vs N   │       │      │ -0.4 │      │      │  0.0 │      │     │     │     │     │ +1% │     │ -7%  │      │               │
└──────────┴───────┴──────┴──────┴──────┴──────┴──────┴──────┴─────┴─────┴─────┴─────┴─────┴─────┴──────┴──────┴───────────────┘

🔍 Filter: Data: [06.05 - 12.05]  Tylko alerty: ☐  Wybierz kolumny: ☑ wszystkie

[Klik wiersza] → szczegóły dnia z dokumentami i kontrahentami
[Klik kolumny] → sortowanie po niej, drugi klik = malejąco
[Klik komórki z ⚠] → otwiera dialog z konkretnym problemem
```

## Funkcje
- **Sortowanie** po dowolnej kolumnie
- **Filterowanie** (data, alerty, kontrahent, towar)
- **Conditional formatting** — wartości poza normą czerwone, OK zielone
- **Footer agregat** — Σ tygodnia, średnia, norma, Δ
- **Heatmap kolory** w komórkach % (low=red, high=green)
- **Eksport CSV** / kopiuj do schowka
- **Klik wiersza** → drill-down do dnia (modal)
- **Konfiguracja kolumn** — pokaż/ukryj

## Pros
✅ **Najszybsze** do skanowania (eksperci robią to w 2 sek)
✅ Najprostsze do **eksportu / analizy w Excelu**
✅ Łatwa implementacja w WPF (DataGrid)
✅ Wszystkie liczby widoczne — brak chowania
✅ Można **porównać dni / partie** wzrokowo

## Cons
❌ "Suche" — bez emocji, bez wow
❌ Wymaga znajomości metryk (skróty Wyd.U, RWP itp.)
❌ Mało wizualne dla zarządu
❌ Trudne na małych ekranach (~12+ kolumn)

## Trudność: 1/5
- WPF DataGrid out-of-the-box
- ~2-3h dla wersji z conditional formatting + footer

## Kiedy stosować
- **Codzienne narzędzie** dla kierownika produkcji / księgowości
- **Audyt** — auditor woli tabelę niż wykres
- **Eksport do Excela** — kopiuj-wklej dla raportu
- **Power users** którzy nie lubią "ozdóbek"
- **PARALLEL z wizualnym widokiem** — toggle "Tabela / Wykres"
