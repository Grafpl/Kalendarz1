# 14. Lekarz wet wpisał padłe > 5% — co robić

**Kiedy tego używasz:** Panel Lekarza pokazuje że w partii padłe (DeclI2) > 5% — **sygnał alarmowy** (choroba? błąd hodowcy? błąd transportu?).
**Ile czasu zajmuje:** 5-15 min reakcji + eskalacja.
**Wideo:** [link — Ser dorzuci nagranie]

> Magda — **padłe > 5% to nie jest "no, zdarza się"**. Może oznaczać masową chorobę (HPAI = ptasia grypa!), błąd hodowcy (zła pasza, woda), problem transportowy (godziny w upale). **Twoja rola: zauważyć, eskalować — natychmiast.**

## Co musisz mieć przed startem
- [ ] **Lekarz wet już wpisał** liczbę padłych (sprawdzasz po fakcie)
- [ ] Numery telefonów: **Justyna** (jakość), **Ser** (decyzje), **Łukasz** (produkcja)
- [ ] Dane partii (hodowca, data dostawy, liczba sztuk)

---

## Kroki

**1. Otwórz "🩺 Panel Lekarza" (kategoria ZAOPATRZENIE I ZAKUPY) lub szczegóły partii.**
[SCREEN: Panel Lekarza z gridem dostaw i kolumnami Padłe / CH / NW / ZM]

**2. **Sprawdź wartości:**

| Skrót | Pełna nazwa | Próg alarmu |
|---|---|---|
| **Padłe** | Sztuki padłe w transporcie/przy odbiorze | > 5% sztuk |
| **CH** | Choroby (ogólnie) | > 2% sztuk |
| **NW** | Niezakaźne | > 3% sztuk |
| **ZM** | **Zakaźne** | > 0.5% sztuk lub jakiekolwiek przy podejrzeniu HPAI |
| **Łapki** (klasa B) | Klasyfikacja niższa | > 10% sztuk |

**3. **Policz procent:**
```
% padłych = liczba padłych / liczba sztuk × 100%

Przykład:
Sztuki: 30 000
Padłe: 1 800
% padłych = 1 800 / 30 000 × 100% = 6%

🚨 6% > 5% = ALARM
```

**4. **Reakcja zależnie od progu:**

### Padłe 3–5%
- **Notatka** w partii: dokładne %, data, kontekst (pogoda, transport).
- Info do **Tereski** mailem / telefonicznie.
- Sprawdź **trend** w `RaportyHodowcow` — czy hodowca ma to regularnie? Jeśli tak — Asia.

### Padłe 5–10%
- **Natychmiast Justyna** (jakość) — może chcieć osobiście sprawdzić partię.
- **Asia** — informuje hodowcę o problemie, sprawdza umowę pod kątem rozliczenia.
- Notatka w partii + **flagujesz hodowcę** w Bazie Hodowców (pole notatki).

### Padłe > 10% lub jakikolwiek ZM (zakaźny)
- **NATYCHMIAST telefon do Justyny + Sera + Łukasza** — równolegle.
- Justyna kontaktuje **lekarza wet powiatowego** (HPAI to obowiązek zgłoszenia).
- **Wstrzymujemy ubój tej partii** dopóki Justyna nie zdecyduje.
- **Zabezpieczamy próbki** (Justyna wie procedurę).

[SCREEN: Panel Lekarza z partią > 10% padłych zaznaczoną czerwoną ramką, alertem]

**5. **Notatka w partii** musi zawierać:**
```
2026-05-26 14:30 — ALARM padłe 6% (1 800/30 000).
Hodowca: KOWALSKI, dostawa 26.05 06:30.
Eskalowane: Justyna (14:32), Asia (14:35).
Decyzja Justyny: [...]
```

**6. **Po decyzji szefostwa:**
- Płacimy wg standardowej procedury (zwykle, jeśli to nie HPAI/ZM).
- Reklamacja do hodowcy.
- Wstrzymanie przyjęcia kolejnej partii od tego hodowcy (jeśli systematyka).
- W przypadku HPAI/ZM — pełna procedura kryzysowa (Ser/Justyna decydują).

---

## ⚠️ Najczęstsze problemy

- **"Lekarz wet sam zauważył, sam dzwonił do Sera"** → Świetnie. Twoja rola: **zrobić notatkę w partii** + sprawdzić czy hodowca dostał info.
- **"Padłe 4.8% — czy to próg czy nie?"** → **Powyżej 4% już sprawdzaj kontekst.** Pogoda upalna? Długi transport? Nowy hodowca? Wytrenuj sobie nos, ale w razie wątpliwości — **eskaluj do Tereski**.
- **"Hodowca dzwoni: 'tak, my widzieliśmy że umierają w transporcie'"** → Notatka! To dowód że hodowca wiedział i nie wstrzymał. **Asia musi to wiedzieć** (rozliczenie).
- **"Wszyscy hodowcy mają w tym tygodniu 5-7% padłych"** → Sygnał systemowy: **pogoda, problem branżowy, choroba w okolicy**. Justyna + Ser. Sprawdź czy nie HPAI w regionie (komunikaty Inspektoratu Weterynaryjnego).
- **"Padłe 12% i hodowca nie odbiera telefonu"** → Krytyczne. **Ser sam zadzwoni** lub każe Łukaszowi pojechać.

---

## 📞 Do kogo dzwonić — TABELA REAKCJI

| % padłych / sytuacja | **Pierwsza osoba** | Druga |
|---|---|---|
| 3-5% (zwiększone) | **Tereska** (info) | — |
| 5-10% (problem) | **Justyna** (jakość) | Asia |
| > 10% (krytyczne) | **Justyna + Ser + Łukasz** równolegle | wszyscy |
| **Jakikolwiek ZM** (zakaźny) | **Justyna NATYCHMIAST** | Ser, lekarz wet powiatowy |
| **Podejrzenie HPAI** (ptasia grypa) | **Justyna** + **Ser** + **PIWet** | (procedura kryzysowa) |
| Hodowca nie odbiera | **Ser** (decyzja czy jechać) | — |
| Reklamacja finansowa | **Asia** | — |

---

## ✅ Skąd wiesz że zrobiłaś dobrze

- W partii jest **notatka z dokładnym %** + eskalacja.
- Właściwa osoba dostała info **w czasie reakcji** (3-5% — w ciągu dnia, > 10% — w ciągu 5 minut).
- W razie ZM/HPAI — Justyna powiedziała **"OK, mam, dzwonię do PIWet"**.
- Hodowca **nie został zignorowany** — wie że temat trafił do Justyny/Asi.

---

## 🚨 ZASADA ŻELAZNA

> **Wątpisz?** → **Eskaluj.** Lepiej 10× za dużo niż raz za mało. Justyna woli 10 fałszywych alarmów niż jedną przegapioną epidemię HPAI.

> **Padłe > 5%** = **zawsze ktoś inny musi to wiedzieć w ciągu godziny.** Nigdy sama nie podejmuj decyzji o tym co z partią.

---

## 🔧 Czego ZPSP NIE obsługuje (dziś)

> **[BRAK W ZPSP — DO DODANIA]** Brak **automatycznego alertu** "padłe > 5%" → email/push do Justyny + Sera. Magda musi sama liczyć i eskalować.
>
> *Workaround na teraz:* dyscyplina sprawdzania + tabela progów do laminacji.
>
> *Planowane:* w `Panel Lekarza` po zapisaniu wartości — jeśli > 5%, automatyczny **alert push w ZPSP** do Justyny + email. Engine alertów w `RaportyHodowcow` wykrywa systematyczne problemy (Część 3 audytu, sekcja F).
