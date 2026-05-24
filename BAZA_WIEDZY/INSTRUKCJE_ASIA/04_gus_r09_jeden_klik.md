# Asia — GUS R09 w jednym kliknięciu (po wdrożeniu auto-szkicu)

> Po wdrożeniu Części 3 audytu (sekcja E — auto-szkic R09) — Twój flow R09 skraca się z 10 klików do 3. Ta instrukcja: jak działa nowy flow + jak się przygotować.

---

## Co się zmienia (przed vs po)

### Przed (dziś — flow ręczny, ~10 minut)
1. Asia pamięta że 1. dnia miesiąca trzeba zrobić R09 (do 8.)
2. Menu → **PRODUKCJA I MAGAZYN** (sic!) → Sprawozdania GUS → R09U
3. Wybierz miesiąc/rok → F5 (Load)
4. Drill-down → przejrzyj partie
5. Korekta r4 (ubytki) ręcznie
6. Walidator
7. Generate XML
8. Login Portal Sprawozdawczy GUS (raport.stat.gov.pl)
9. Upload XML
10. Mark as Sent w ZPSP

### Po (Q3 2026 — flow zautomatyzowany, ~3 minuty)
1. **1. dnia miesiąca o 4:00** system sam generuje szkic (Windows Scheduled Task)
2. **Asia o 9:00** otwiera **Centrum Asi** — sekcja Skrzynka pokazuje "📊 R09 [miesiąc] gotowy do przejrzenia"
3. **Klik** → otwiera się R09UWindow z wczytanym szkicem + walidacja OK + auto-suggest r4
4. Asia przegląda, korzysta z auto-suggest (lub poprawia r4), klik **"📤 Wyślij"**
5. Otwiera się Portal Sprawozdawczy GUS w przeglądarce + kopia XML w schowku
6. Asia: login → paste → submit
7. Wraca → klik **"Wysłałam"** w ZPSP

**Skrócenie:** 7 minut, 3 klików w ZPSP zamiast 10.

---

## Co Asia widzi w "Centrum Asi" rano

```
📥 SKRZYNKA ASI
┌──────────────────────────────────────────────────────────┐
│ 📊 R09 maj — auto-szkic gotowy do przejrzenia            │
│    Wygenerowany: 01.06.2026 04:00 przez SYSTEM           │
│    Walidacja: ✅ OK                                       │
│    Auto-suggest r4: 98.2% × r3 = 9 543 200 kg            │
│    [Klikni: otwórz w R09UWindow]                          │
│                                                            │
│ Status: ⏳ Do przejrzenia — wyślij do 08.06.2026          │
│ Deadline za 7 dni                                          │
└──────────────────────────────────────────────────────────┘
```

---

## Krok po kroku — flow nowy

### Krok 1: Centrum Asi pokazuje alert

Rano 1. czerwca otwierasz Centrum Asi (kafelek "🏠 Centrum Asi" — kategoria ZAOPATRZENIE I ZAKUPY).

W sekcji "📥 Skrzynka Asi" widzisz pierwszy wiersz:
> 📊 **R09 maj — auto-szkic gotowy do przejrzenia**

### Krok 2: Klik → otwiera R09UWindow

Po kliknięciu:
1. Otwiera się `R09UWindow` z **wczytanym szkicem** (nie musisz klikać Load / wybierać miesiąca).
2. W gridzie widać pozycję **Brojlery (w14)** z 5 kolumnami wypełnionymi:
   - r1 (sztuki)
   - r2 (waga żywa farmerska)
   - r3 (waga poubojowa brutto)
   - **r4 (waga handlowa netto) — auto-sugerowane!**
   - r5 (wartość zł)
3. Status w nagłówku: **"✅ Walidacja OK — gotowe do wysłania"**

### Krok 3: Sprawdź dane (1-2 min)

- [ ] **Suma sztuk** — sensowna? (~500k-1M w skali miesiąca)
- [ ] **Suma wagi** — sensowna? (~1-2M kg)
- [ ] **Auto-suggest r4** — sprawdź czy procent ubytków (np. 1.8%) zgadza się z realiami miesiąca
- [ ] **Drill-down** (dwuklik wiersza) → lista partii — jakieś anomalie (sztuki=0, waga=0)?

**Jeśli wszystko OK** → idź do kroku 4.
**Jeśli coś nie tak** → popraw r4 ręcznie + dopisz notatkę przed wysłaniem.

### Krok 4: Wyślij

Klik **"📤 Wyślij"** (nowy przycisk, jak Faza 3 wdrożona):
1. System **kopiuje XML do schowka** (Ctrl+C zrobione za Ciebie).
2. **Otwiera w przeglądarce** Portal Sprawozdawczy: `https://raport.stat.gov.pl/`.
3. ZPSP minimalizuje się.

### Krok 5: Portal Sprawozdawczy GUS

W przeglądarce:
1. **Zaloguj się** (Twoje konto Portalu Sprawozdawczego — Ser zakłada raz, używasz cały rok).
2. Znajdź formularz **R-09U** (zakup żywca).
3. **Upload XML** — przycisk "Wczytaj plik" → wybierz **"Wklej ze schowka"** (lub jeśli portal nie obsługuje paste, **wklej do Notatnika → zapisz jako .xml → uploaduj**).
4. **Sprawdź** czy GUS akceptuje (komunikat "Wczytano poprawnie").
5. **Wyślij** w portalu (przycisk "Zatwierdź" / "Wyślij").

### Krok 6: Potwierdzenie w ZPSP

Wróć do ZPSP → R09UWindow nadal otwarte → **klik "Wysłałam"**:
1. System zapisuje `R09USzkice.StatusSzkic = 'SentByUser'`, `SentByUser = Asia`, `SentAt = teraz`.
2. Alert znika z Centrum Asi.
3. Historia (kafelek "Historia") pokazuje nową pozycję.

**Koniec. Następny R09 — za miesiąc, automatyczny szkic znowu czeka.**

---

## Co robić gdy auto-szkic nie powstał

Może się zdarzyć (Windows Task nie wystartował, serwer offline, błąd w danych). W Centrum Asi nie ma alertu rano 1-2 dnia.

**Plan B:**

1. Otwórz **R09UWindow** ręcznie (jak dziś — kategoria PRODUKCJA → Sprawozdania GUS → R09U).
2. Wybierz miesiąc → F5 → przejrzyj.
3. Generate XML → Wyślij ręcznie (jak w starym flow).
4. **Zgłoś Serowi** że auto-job nie wystartował — niech sprawdzi log `C:\ZPSP\logs\kontrakty-job.log` (analogicznie auto-job R09).

---

## Co Sergiusz musi zrobić żeby ten flow działał

**Effort: 3 dni roboczych Sera** (Część 3 audytu, sekcja E):

1. Nowa tabela `R09USzkice` w LibraNet (4h)
2. Service `R09UAutoService.GenerujSzkicAsync()` (1 dzień)
3. Windows Scheduled Task `Kalendarz1.exe --gus-r09-szkic` (30 min)
4. Integracja z Centrum Asi (sekcja Skrzynka) (4h)
5. Auto-suggest r4 = r3 × (1 - ubytki%) (4h)
6. Eskalacja email -7d / -3d / -1d / 0 (4h)
7. Skrót w kategorii ZAOPATRZENIE I ZAKUPY (30 min)

---

## ⚠️ Co pozostaje ręczne (nie da się zautomatyzować)

- **Login + upload XML do Portalu Sprawozdawczego GUS** — Portal **nie ma API publicznego** (sprawdziłem). Asia musi ręcznie.
- **Decyzja o korekcie r4** — auto-suggest to tylko podpowiedź; Asia ma ostatnie słowo.
- **Reakcja na błędy walidacji** — jeśli GUS odrzuca XML, Asia poprawia ręcznie w R09UWindow.

---

## ✅ Skąd wiesz że zrobiłaś dobrze

- W **Historii Sprawozdań GUS** widać pozycję z dzisiejszą datą, status "Sent", Twoim imieniem.
- W **Portalu Sprawozdawczym GUS** historia pokazuje zaakceptowane sprawozdanie.
- **W Centrum Asi** alert R09 zniknął.
- **W kolejnym miesiącu** auto-szkic znowu powstał (sprawdź 1. dnia miesiąca o 9:00).

---

## 🎯 Cel długoterminowy

**Po 6 miesiącach automatyzacji:**
- Asia spędza na R09 **5 minut/miesiąc** (zamiast 30-60 minut dziś)
- Brak ryzyka przeoczenia deadline (auto-alerty -7/-3/-1d)
- Brak ryzyka błędu liczbowego (walidator zatrzymuje przed wysyłką)
- Historia wszystkich sprawozdań w jednym miejscu (audit pod kontrolę IJHARS)

---

*Wersja 1.0 • 24.05.2026 • Asia, gdy auto-szkic wdrożony przeczytaj jeszcze raz i odhacz że flow działa zgodnie ze spec*
