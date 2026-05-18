# 🔴 Koncepcja 10: Live Mode — Real-Time Stream

**Inspiracja**: Trading dashboards (Bloomberg, TradingView), Twitch streamer dashboards, SCADA real-time

## Idea
Widok który **żyje na bieżąco**. Auto-refresh co 30-60 sek. Nowe dokumenty pulsują przy pojawieniu. Numery animują się gdy się zmieniają (count-up). Dla **monitorowania produkcji w czasie rzeczywistym** — np. monitor w hali lub w biurze kierownika.

## Mockup

```
┌────────────────────────────────────────────────────────────────────────────────┐
│  🔴 LIVE  12.05.2026  14:23:07         Auto-refresh: 30s [⏸]  [⛶ Full screen] │
├────────────────────────────────────────────────────────────────────────────────┤
│                                                                                │
│   DZIŚ — NA BIEŻĄCO                                                            │
│                                                                                │
│   🐔 ŻYWIEC PRZYJĘTY              ⚙ UBÓJ TERAZ                                │
│                                                                                │
│       847                              612                                     │
│       ton (od 6:00)                    ton (do 14:23)                          │
│                                                                                │
│       +12,5 t/godz ▲                  +8,4 t/godz                              │
│       ostatnia partia 14:18           ostatni dok 14:22                        │
│       🟢 Linia 1 PRACUJE              🟢 Linia 1 PRACUJE                        │
│                                                                                │
├────────────────────────────────────────────────────────────────────────────────┤
│                                                                                │
│   ⏱ OSTATNIE 5 MINUT                                                           │
│                                                                                │
│   14:22 → PWU 003421/26  ⚙ ubój     ▲ +1 480 kg     Kowalski Wlkp #1247       │
│   14:21 → MZ  001823/26  🚚 sprzedaż ▼ -2 100 kg     Drobimex sp. z o.o.       │
│   14:19 → PZ  002145/26  🐔 przyjęcie ▲ +5 200 kg    Kowalski Wlkp #1248       │
│   14:18 → PWP 003419/26  🔪 krojenie ▲ +860 kg     (Filet)                     │
│   14:17 → MM- 001712/26  ❄ na MROŹ  ▲ +600 kg                                  │
│                                                                                │
│   [↻ aktualizacja co 30 sek]                                                   │
│                                                                                │
├────────────────────────────────────────────────────────────────────────────────┤
│                                                                                │
│   📊 WSKAŹNIKI DZIŚ vs PLAN                                                    │
│                                                                                │
│        ŻYWIEC      UBÓJ        PROD       KLIENCI                              │
│       847/1200    612/1050   229/380    420/720                                │
│        70%        58%        60%        58%                                    │
│       ████░░░    ██████░    ███████   ████████                                 │
│                                                                                │
│                                                                                │
│   ⏰ Pozostało 3h 37min do końca dziennej zmiany                                │
│   Przy obecnym tempie: ~1 178 t ŻYW (98% planu)                                │
│                                                                                │
├────────────────────────────────────────────────────────────────────────────────┤
│                                                                                │
│   🚨 LIVE ALERTY                                                               │
│                                                                                │
│   ⚠ 14:18  Wyd. krojenia spadła z 64% na 58% (ostatnie 30 min)                 │
│   ⚠ 14:05  MROŹNIA przyjęła 200 kg więcej niż średnia godziny                  │
│                                                                                │
└────────────────────────────────────────────────────────────────────────────────┘
```

## Funkcje real-time
- **Auto-refresh co 30 sek** (configurable: 15s, 30s, 60s, off)
- **Count-up animacje** liczb (Storyboard animuje od starej do nowej wartości)
- **Pulse effect** gdy pojawia się nowy dokument (border miga na zielono)
- **Live feed** ostatnich 10 dokumentów (FIFO push)
- **Progress vs plan** — codzienna norma (z planu produkcji)
- **Forecast** "przy obecnym tempie: X ton"
- **Real-time alerty** — gdy metryka skoczyła w ciągu ostatnich 30 min

## Wymagania techniczne
- Backend: cache + delta loading (nie cały dzień co 30s, tylko zmiany od last_check)
- Optimistic UI: pokaż "Ładowanie..." na 1 sek przed odpowiedzią
- DispatcherTimer 30s w WPF
- WebSocket / SignalR byłoby ideałem (push), ale polling też wystarczy
- Dane z **planu produkcji** (dziennego targetu)

## Pros
✅ **WOW** — wygląda jak Bloomberg, robi wrażenie
✅ **Decyzje w trakcie zmiany** — kierownik widzi że coś się dzieje teraz
✅ Świetne na **TV w sali kierowniczej** / **monitorze obok biurka**
✅ Łatwe do zrozumienia ("liczby rosną")

## Cons
❌ Wymaga dziennego planu produkcji (jeszcze nie ma w systemie?)
❌ Backend musi być szybki (30s × każda godzina × każdy dzień)
❌ Auto-refresh denerwuje gdy chcesz coś przeczytać (preview problem)
❌ Bezużyteczne dla raportów (musisz "zamrozić" widok)
❌ Animations consume CPU/GPU

## Trudność: 4/5
- DispatcherTimer + delta query (~5h)
- Count-up animations (~3h)
- Pulse effects + live feed (~3h)
- Forecast logic (~2h)
- ~12-15h total

## Kiedy stosować
- **Monitor w hali / biurze** — kierownik w trakcie zmiany
- **Daily standup / TV** — wszyscy widzą stan dnia
- **Centrum dowodzenia** — gdy chcesz "uchwycić" produkcję na żywo
- **NIE** dla historii / raportów (frozen w danym momencie)

## Powiązany pomysł: "Plan dnia + odchylenie"
Plan = 1 200 t ŻYW na zmianę.
Live shows: "Jesteśmy o 14:23. Zrobiliśmy 847 t (70%). Pozostało 3h 37min na 353 t. Tempo: ~96 t/h — możliwe (potrzeba 96 t/h)."
**Czerwony alarm** gdy tempo spada poniżej tego co potrzeba do zrealizowania planu.
