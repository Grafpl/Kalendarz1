# 🎨 Koncepcje graficzne — Łańcuch Produkcji

Eksploracja **10 różnych pomysłów** na wizualizację łańcucha kurczak → ubój → krojenie → magazyny → klienci.

## Cele biznesowe (powtórzenie)
1. Ile żywca przyszło
2. Co powstało (tuszki / podroby / odpady)
3. Co poszło na krojenie vs bezpośrednio na sprzedaż
4. Wydajność uboju / krojenia vs normy
5. Czy wszystko wyszło z PROD
6. Ile zostało na magazynach (stagnacja?)
7. % udział wszystkich strumieni
8. Detekcja kradzieży / nieefektywności

## Aktualnie zaimplementowane: Linear/Stripe Dashboard
Czyste hero KPI + flow diagram + breakdown + alerty.
**Mocne strony**: typografia, hierarchia, czytelność
**Słabe strony**: numbers-heavy, brak wizualnej "magii"

---

## 📋 Tabela porównawcza koncepcji

| # | Koncepcja | Inspiracja | Trudność | "Wow" | Czytelność | Time-to-info |
|---|-----------|-----------|:--------:|:-----:|:----------:|:-----------:|
| 1 | **Sankey Flow** | Tableau, D3.js | 4/5 | 🔥🔥🔥🔥🔥 | ⭐⭐⭐⭐ | 5 sek |
| 2 | **Fabryka — top-view metafora** | SCADA, MES | 5/5 | 🔥🔥🔥🔥🔥 | ⭐⭐⭐ | 10 sek |
| 3 | **River Flow (rzeka)** | D3 water | 4/5 | 🔥🔥🔥🔥 | ⭐⭐⭐ | 7 sek |
| 4 | **Cockpit 4-kwadrantowy** | Bloomberg | 3/5 | 🔥🔥🔥 | ⭐⭐⭐⭐⭐ | 3 sek |
| 5 | **Storytelling pionowy** | NYTimes scrolly | 2/5 | 🔥🔥🔥🔥 | ⭐⭐⭐⭐⭐ | 30 sek (czytanie) |
| 6 | **Card Grid (Pinterest)** | Notion | 3/5 | 🔥🔥 | ⭐⭐⭐⭐ | 5 sek |
| 7 | **Network Graph** | D3 force | 5/5 | 🔥🔥🔥🔥🔥 | ⭐⭐ | 15 sek |
| 8 | **Spreadsheet/Tabela** | Airtable | 1/5 | 🔥 | ⭐⭐⭐⭐⭐ | 2 sek |
| 9 | **Heatmap kalendarz** | GitHub contributions | 2/5 | 🔥🔥🔥 | ⭐⭐⭐⭐ | 8 sek |
| 10 | **Live Mode Real-Time** | Trading screens | 4/5 | 🔥🔥🔥🔥 | ⭐⭐⭐ | live |

## 🏆 Moja rekomendacja: 2 koncepcje hybrydowe

### A) **Sankey** dla "co widzi szef"
Pełny wizualny przepływ — szybko widać proporcje i bottlenecki.

### B) **Spreadsheet** dla "co używa kierownik produkcji"
Każdy dzień jest wierszem, kolumny są etapami. Filterable, sortowable, copy-pasteable.

Te dwie razem (jako toggle) dają **pełne pokrycie** — wizualne dla decyzji + tabela dla pracy.

---

## Pliki koncepcji

- [01_SANKEY_FLOW.md](01_SANKEY_FLOW.md)
- [02_FABRYKA_METAFORA.md](02_FABRYKA_METAFORA.md)
- [03_RIVER_FLOW.md](03_RIVER_FLOW.md)
- [04_COCKPIT_KWADRANTY.md](04_COCKPIT_KWADRANTY.md)
- [05_STORYTELLING.md](05_STORYTELLING.md)
- [06_CARD_GRID.md](06_CARD_GRID.md)
- [07_NETWORK_GRAPH.md](07_NETWORK_GRAPH.md)
- [08_SPREADSHEET.md](08_SPREADSHEET.md)
- [09_HEATMAP_KALENDARZ.md](09_HEATMAP_KALENDARZ.md)
- [10_LIVE_REALTIME.md](10_LIVE_REALTIME.md)
