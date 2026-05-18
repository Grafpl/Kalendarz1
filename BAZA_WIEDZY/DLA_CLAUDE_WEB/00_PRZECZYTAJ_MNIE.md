# Folder dla Claude Web — instrukcja użycia

**Cel:** rozmowa z Claude Web (lub ChatGPT/innym doradcą) o decyzji pensji Maja/Paulina/Teresa.

## Pliki w tym folderze (4)

| # | Plik | Co zawiera | Rozmiar |
|---|---|---|---|
| 01 | `01_BRIEF_GLOWNY.md` | **Pełna analiza SQL + rekomendacje + 5 pytań** | 17 KB |
| 02 | `02_Firma_skala.md` | Skala firmy (258M obrotu, 200t/dzień) | 7 KB |
| 03 | `03_Ludzie.md` | Kto jest kim (Maja, Paulina, Teresa, Jola, Daniel) | 6 KB |
| 04 | `04_Frustracje_cele_Sergiusza.md` | Co Sergiusza martwi, czego chce | 8 KB |

**Razem:** ~38 KB — bezpiecznie mieści się w jednym kontekście Claude Web.

## Jak użyć

### Wariant A — wszystko naraz (zalecane)

1. Otwórz claude.ai
2. Wklej zawartość wszystkich 4 plików w jednej wiadomości (oddziel separatorem `===` lub `---`)
3. Po wklejeniu dodaj prompt:

```
Jestem Sergiusz, właściciel firmy drobiarskiej (258M obrotu).
Powyżej masz pełną analizę SQL trzech moich ludzi pod decyzję pensji.

Chcę żebyś:
1. Przeczytał liczby krytycznie — wskaż gdzie moje rozumowanie ma luki
2. Odpowiedział na 5 pytań z sekcji 7 briefu
3. Powiedział co byś zrobił na moim miejscu w kolejności 30/60/90 dni

Nie zgadzaj się ze mną żeby być miłym. Wskaż gdzie się mylę.
```

### Wariant B — najpierw brief, kontekst później

1. Wklej tylko `01_BRIEF_GLOWNY.md`
2. Jeśli Claude prosi o kontekst — dosłaj `02`/`03`/`04` na żądanie

## Czego NIE wysyłać

- Pliki techniczne (`13_Bazy_danych.md`, `23_HANDEL_*.md`, `19_LibraNet_*.md`) — to dla developera, nie dla doradcy biznesowego
- Skrypty SQL z `SELECTY/` — Claude i tak ich nie uruchomi, brief ma wyniki
- Memory z `~/.claude/projects/...` — to dla Claude Code (lokalnego)
