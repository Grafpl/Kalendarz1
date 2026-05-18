# 📖 Koncepcja 5: Storytelling — narracyjne rozdziały

**Inspiracja**: NYTimes scrollytelling, Bloomberg Businessweek, Pudding.cool, Medium

## Idea
Zamiast pokazać wszystko na raz — **opowiedz historię**. Każdy rozdział = sekcja przewijana pionowo. Czytelnik scrolluje od początku do końca, "przeżywa" cały cykl produkcyjny. Świetne dla rozumienia procesu.

## Mockup

```
═══════════════════════════════════════════════════════════════════
║  Wtorek, 12.05.2026                                              ║
║                                                                  ║
║                       🐔                                         ║
║                                                                  ║
║              1 250 ton                                           ║
║                                                                  ║
║         Tyle żywego kurczaka przyjęliśmy dziś.                   ║
║         To +12% więcej niż w ten sam dzień tydzień temu.         ║
║                                                                  ║
║         247 partii od 89 hodowców.                               ║
║                                                                  ║
║                       ↓                                          ║
═══════════════════════════════════════════════════════════════════

═══════════════════════════════════════════════════════════════════
║                                                                  ║
║              CO Z TEGO POWSTAŁO?                                 ║
║                                                                  ║
║                    ⚙                                             ║
║                                                                  ║
║              1 065 ton produktu                                  ║
║                                                                  ║
║         85% — wydajność uboju (norma 85%)  ✓                     ║
║                                                                  ║
║         Pozostałe 15% to:                                        ║
║         • pióra (~7%)                                            ║
║         • krew (~3%)                                             ║
║         • woda i ubytek termiczny (~4%)                          ║
║         • jelita (~3%)                                           ║
║                                                                  ║
║                       ↓                                          ║
═══════════════════════════════════════════════════════════════════

═══════════════════════════════════════════════════════════════════
║                                                                  ║
║              GDZIE TO POSZŁO?                                    ║
║                                                                  ║
║          ┌──── 615 t (58%) → NA KROJENIE                         ║
║          │     ▼                                                 ║
║   1065 t ┤     🔪  62% wydajność  ✓                              ║
║          │     ▼                                                 ║
║          │     380 t elementów po krojeniu                       ║
║          │                                                       ║
║          └──── 450 t (42%) → sprzedaż jako CAŁE TUSZKI           ║
║                                                                  ║
║                       ↓                                          ║
═══════════════════════════════════════════════════════════════════

═══════════════════════════════════════════════════════════════════
║                                                                  ║
║              DO KOGO TRAFIŁO?                                    ║
║                                                                  ║
║        ████████████████ DYSTRYBUCJA   780 t (73%)                ║
║        ███              MROŹNIA       150 t (14%)                ║
║        █                MASARNIA       80 t  (8%)                ║
║        █                KARMA          50 t  (5%)                ║
║        █                ODPADY         65 t  (6%) ⚠              ║
║                                                                  ║
║         Z dystrybucji 99% trafiło do klientów (710 t).           ║
║                                                                  ║
║                       ↓                                          ║
═══════════════════════════════════════════════════════════════════

═══════════════════════════════════════════════════════════════════
║                                                                  ║
║              CO POSZŁO NIE TAK?                                  ║
║                                                                  ║
║         🚨 Odpady przekroczyły normę                             ║
║                                                                  ║
║              8,2% > 5% norma                                     ║
║                                                                  ║
║         To głównie z partii #1247 (hodowca: Kowalski).           ║
║         Klasa B wyższa niż średnia tygodnia.                     ║
║                                                                  ║
║         [Sprawdź dokumenty]  [Zobacz partię]                     ║
║                                                                  ║
║                       ↓                                          ║
═══════════════════════════════════════════════════════════════════

═══════════════════════════════════════════════════════════════════
║                                                                  ║
║              PODSUMOWANIE DNIA                                   ║
║                                                                  ║
║         ✓ Wydajność uboju 85,2% (norma)                          ║
║         ✓ Wydajność krojenia 62,1% (norma)                       ║
║         ⚠ Wysokie odpady (do sprawdzenia)                        ║
║         ✓ Rotacja DYST→KLIENCI 99% (świetna)                     ║
║         ✓ Bilans masy zamyka się                                 ║
║                                                                  ║
║                  [Drukuj raport]  [Wyślij email]                 ║
═══════════════════════════════════════════════════════════════════
```

## Cechy
- **Pełna szerokość** — każda sekcja zajmuje cały ekran
- **Centered text** — duża typografia (40-60pt nagłówki)
- **Plenty whitespace** — dużo oddechu
- **Subtle animations** — wjazd liczb z fade-in/slide-up
- **Strzałka ↓** wskazuje gdzie scrollować
- **Last screen** = call-to-action (drukuj/wyślij)

## Pros
✅ **Najlepsza edukacja** — uczy procesu (idealne dla nowego pracownika / audytora)
✅ Pamiętalne — czytelnik "przeżył" cykl
✅ Świetne jako **raport dzienny** (jeden scroll = full picture)
✅ Łatwe do udostępniania (PDF, email)

## Cons
❌ Powolne — 30-60 sek czytania na pełen scroll
❌ Niedobre do "quick check" (musisz przewinąć żeby znaleźć info)
❌ Mało użyteczne dla doświadczonych operatorów (już wiedzą co się dzieje)
❌ Wymaga pisarstwa — teksty narratywne (auto-generate z templates)

## Trudność: 2/5
~3-4h w WPF (StackPanel z sekcjami, scroll-snap)

## Kiedy stosować
- **Raport dzienny** drukowany / mailowany (PDF z sekcjami jako strony)
- **Onboarding** nowych pracowników i menedżerów
- **Audyt** — pokazujesz cały cykl od wejścia do wyjścia w 1 widoku
- **NIE** jako codzienne narzędzie dla doświadczonych
