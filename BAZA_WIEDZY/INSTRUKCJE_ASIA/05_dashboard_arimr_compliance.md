# Asia — Dashboard ARiMR Compliance

> Twoje narzędzie nr 1 pod dotację ARiMR (IX.2026, do 10M PLN). Otwierasz codziennie rano + przed każdym spotkaniem ze Serem.

---

## Co to ARiMR Compliance

**ARiMR** = Agencja Restrukturyzacji i Modernizacji Rolnictwa, daje **dotację dla zakładów drobiarskich** pod warunkiem:
- **3-letnie kontrakty z hodowcami** na **minimum 50% surowca**
- **Utrzymane przez 5 lat** po zakończeniu inwestycji
- **Pisemna forma** + **rejestr** + **dowody dostaw zgodnie z kontraktami**

**Konsekwencja niespełnienia:** **zwrot dotacji** proporcjonalnie do % brakującego surowca.

**Twoja rola:** Strażnik tego procentu. **Reagujesz** gdy compliance spada poniżej 50%.

---

## Mockup widoku Dashboard ARiMR (planowany w Fazie 3)

```
╔══════════════════════════════════════════════════════════════════════╗
║  🎯 ARiMR COMPLIANCE — kontrakty 3-letnie                            ║
║  Stan na 26.05.2026 09:14 (ostatnie 12 miesięcy)                    ║
╠══════════════════════════════════════════════════════════════════════╣
║                                                                        ║
║                                                                        ║
║       ██████████████████████░░░░░░░░░░░░  67.4% ✅                   ║
║       0%                                       50%               100%║
║                                                                        ║
║   Wymagane minimum: 50%                                               ║
║   Aktualnie:        67.4%                                             ║
║   Margines:         +17.4 pp                                          ║
║   Status:           ✅ OK                                              ║
║                                                                        ║
║  ────────────────────────────────────────────────────────────────    ║
║                                                                        ║
║  📊 SZCZEGÓŁY                                                         ║
║                                                                        ║
║  Surowiec ogółem (ostatnie 12 mies.):     12 845 320 kg              ║
║  Surowiec pod ARiMR (3-letni):             8 657 510 kg              ║
║  Hodowców ogółem:                          137                        ║
║  Hodowców pod 3-letnim:                    42  (30.7%)                ║
║                                                                        ║
║  ────────────────────────────────────────────────────────────────    ║
║                                                                        ║
║  📈 TREND OSTATNICH 12 MIESIĘCY                                       ║
║                                                                        ║
║      70%┤                                          ●●●●●              ║
║         │                                  ●●●●●●●                    ║
║      60%┤                          ●●●●●●●                            ║
║         │                  ●●●●●●●                                    ║
║      50%┤━━━━━━━━━━━●●●●●━━━━━━━━━━━━━━━━━━━━━━━ (próg)              ║
║         │     ●●●●●                                                   ║
║      40%┤●●●●●                                                        ║
║         └────┬────┬────┬────┬────┬────┬────┬────┬────┬────           ║
║          maj  cze  lip  sie  wrz  paź  lis  gru  sty  lut             ║
║                                                                        ║
║  ────────────────────────────────────────────────────────────────    ║
║                                                                        ║
║  🟡 HODOWCY DO ZAKONTRAKTOWANIA (top 5)                              ║
║                                                                        ║
║  Hodowca         Dostaw   Kg/12m       Komentarz                      ║
║  ─────────────  ───────  ──────────  ──────────────────────────      ║
║  ABRAMOWICZ        12    480 000     stabilny, brak skarg            ║
║  CHOJNACKI         10    395 000     stabilny, brak skarg            ║
║  DĄBROWSKI          8    312 000     stabilny, brak skarg            ║
║  EJDYS              6    234 000     w ost. miesiącach +20%          ║
║  FRANEK             5    195 000     nowy, w trakcie probacji        ║
║                                            [Generuj propozycje umów]  ║
║                                                                        ║
║  ────────────────────────────────────────────────────────────────    ║
║                                                                        ║
║  ⚠️ KONTRAKTY KOŃCZĄCE SIĘ W NAJBLIŻSZYCH 6 MIESIĄCACH               ║
║                                                                        ║
║  Hodowca         Numer    Wygasa       Status      Akcja              ║
║  ─────────────  ───────  ──────────  ──────────  ───────────────    ║
║  KOWALSKI       1/24     15.07.2026  EXPIRING    Tereska: dzwoń pn  ║
║  NOWAK BIS      7/24     03.08.2026  EXPIRING    Asia: szkic         ║
║  JANKOWSKI      12/24    20.10.2026  ACTIVE      OK                  ║
║                                                                        ║
║  [📤 Export PDF dla audytu]    [⚙ Konfiguracja eskalacji]            ║
╚══════════════════════════════════════════════════════════════════════╝
```

---

## Jak czytać Dashboard

### Pasek główny (% compliance)
- **≥ 50%** → zielone OK, możesz oddychać spokojnie
- **45-50%** → żółty WARN, eskaluj do Sera, planuj rozmowy z 5+ hodowcami
- **< 45%** → czerwony CRIT, **natychmiastowa akcja**, Sergiusz alert + plan w 24h

### Trend ostatnich 12 miesięcy
- **Rośnie** = strategia działa, rób więcej tego co teraz
- **Stabilny** = OK, ale czujny
- **Spada** = ALARM — nawet jeśli wartość bezwzględna OK, trend pokazuje że coś się zmienia
  - Wygasają stare kontrakty bez przedłużenia?
  - Hodowcy spotowi rosną w wolumenie?
  - Nowi hodowcy nie idą w 3-letnie?

### Hodowcy do zakontraktowania
- **Top 5** hodowców **spotowych** (bez kontraktu) **z najwyższym wolumenem**
- Twoja akcja:
  - **2-3 z listy/tydzień** → propozycja kontraktu (Magda/Tereska dzwonią)
  - **Klikni "Generuj propozycje umów"** → tworzy szkice DRAFT dla wybranych z auto-wypełnionymi polami
  - Sergiusz akceptuje warunki → Word → wysyłka → SIGNED

### Kontrakty wygasające w 6 miesięcy
- **Asia automatycznie dostaje alerty** 90/30/7 dni przed wygaśnięciem (w Centrum Asi)
- **W Dashboard** widzisz to wszystko razem
- Akcja: zaplanować przedłużenia z wyprzedzeniem 1-2 miesięcy

---

## Codzienna rutyna z Dashboard

### Rano (5 min)
- [ ] Otwórz Dashboard ARiMR
- [ ] **Sprawdź % compliance** — czy nie spadło?
- [ ] **Sprawdź sekcję "Wygasające"** — czy nowe pozycje?
- [ ] **Akcja na dziś:** wybierz 1-2 rzeczy (przedłużenie, propozycja nowego kontraktu, rozmowa z hodowcą)

### W ciągu dnia
- [ ] **Klikni "Generuj propozycje umów"** dla wybranych hodowców (Magda/Tereska dzwonią z propozycją)
- [ ] **Przedłuż umowy wygasające w najbliższych 4 tygodniach** (instrukcja Magdy #6 — scenariusz S2)

### Wieczorem (przed wyjściem)
- [ ] Sprawdź czy dziś przybyło kontraktów ACTIVE (klik "Statystyki dnia")
- [ ] Notatka do Sera jeśli były duże zmiany

---

## Co robić gdy compliance spada poniżej progu

### Compliance 45-50% (żółty WARN)

**Tygodniowy plan:**
1. Lista top 10 hodowców spotowych (już generuje Dashboard)
2. Magda/Tereska dzwonią do każdego z propozycją kontraktu 3-letniego
3. Asia generuje szkice DRAFT
4. **Cel: 5 podpisanych kontraktów w 4 tygodnie** = wzrost compliance o ~2-3 pp

### Compliance < 45% (czerwony CRIT)

**Natychmiastowa akcja:**
1. **W tym samym dniu**: Email do Sera z planem
2. **W ciągu 48h**: spotkanie Asi + Ser + Tereska + Justyna — strategia
3. **Możliwości:**
   - Renegocjacja z hodowcami spotowymi (lepsza cena za 3-letni kontrakt)
   - Pozyskanie nowych hodowców (z modułu Pozyskiwanie Hodowców — Sergiusz)
   - Tymczasowe zwiększenie wolumenu pod 3-letnich (jeśli mają moce)

### Compliance < 40% (krytyczny — ARiMR zagrożone)

- **Stan alertowy** firmy
- Sergiusz dzwoni do prawniczki
- Ocena ryzyka zwrotu dotacji + plan komunikacji z ARiMR
- **Dashboard powinien zablokować zwykłe wyjście z aplikacji** (alert wymagający potwierdzenia)

---

## Export PDF dla audytu ARiMR

**Kiedy używać:**
- ARiMR żąda dokumentacji
- Wewnętrzna kontrola Sergiusza
- Spotkanie z prawniczką
- Aplikacja o dotację (IX.2026)

**Co zawiera PDF (generowany przez iTextSharp):**
1. **Strona tytułowa** — Piórkowscy, data, podpis Asi
2. **Snapshot Dashboard** — % compliance, trend, liczby
3. **Lista wszystkich kontraktów ARiMR** (status ACTIVE/EXPIRING):
   - Numer, hodowca, NIP, daty, % ubytku, cena, dostawy w okresie
4. **Lista załączników** (z `KontraktyZalaczniki`) — ścieżki UNC do skanów PDF
5. **Audit log** — kto, kiedy zmieniał kluczowe pola
6. **Sygnatura** — data, godzina, użytkownik generujący

**Wygenerowany PDF → Asia drukuje → dołącza do dokumentacji ARiMR.**

---

## Jak Sergiusz to buduje (Faza 3 — Część 4 audytu)

**Effort: 2 dni roboczych:**

1. **`DashboardArimrWindow.xaml`** — UI z LiveCharts (już używane w AnalitykaPelna):
   - ProgressBar z % compliance
   - Wykres trendu (linia ostatnich 12 miesięcy)
   - Lista top 5 hodowców (DataGrid)
   - Lista wygasających (DataGrid)
2. **`DashboardArimrService`** — pobiera `v_ArimrCompliance` + historię (szybkie zapytania)
3. **`ExportArimrPdfService`** — iTextSharp generuje PDF z snapshotem
4. **Integracja z Centrum Asi** — sekcja "ARiMR Compliance" pokazuje skrót

---

## ⚠️ Co Dashboard NIE pokazuje (świadome ograniczenia)

- **Nie pokazuje compliance per okres rolny** (jak ARiMR liczy) — tylko ostatnie 12 miesięcy
- **Nie generuje formalnego wniosku ARiMR** (Asia musi sama wypełnić formularze ARiMR)
- **Nie weryfikuje** czy kontrakty są **prawidłowo notarialnie poświadczone** (jeśli ARiMR tego wymaga — sprawdź z prawniczką)
- **Snapshot dnia** ≠ pełny audit ARiMR — to narzędzie operacyjne, nie zastępuje papierowej dokumentacji

---

## ✅ Mierniki sukcesu po 3 miesiącach

- [ ] Compliance stabilnie > 55% (margines 5+ pp)
- [ ] Brak kontraktów wygasłych bez przedłużenia
- [ ] Asia generuje 3-5 propozycji umów / tydzień (z listy Dashboardu)
- [ ] Tereska/Magda umieją zaproponować 3-letni kontrakt hodowcy (skrypt rozmowy zaakceptowany)
- [ ] PDF audytu jest aktualny (Asia generuje co miesiąc, dokumentacja archiwum)

---

*Wersja 1.0 • 24.05.2026 • Asia — kontrakty to Twój budżet władzy w firmie. Strażnik = decyduje.*
