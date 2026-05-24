# 16. Pierwszy dzień miesiąca — checklista zakupowa

**Kiedy tego używasz:** **w każdy 1. dzień miesiąca** (lub pierwszy roboczy dzień jeśli weekend). To dzień zamknięcia poprzedniego miesiąca + start nowego.
**Ile czasu zajmuje:** 1-2 godziny rano (zwykle 9:00–11:00).
**Wideo:** [link — Ser dorzuci nagranie]

> Magda — pierwszy dzień miesiąca to **dzień Asi** (GUS R09, rozliczenia, kontrola compliance). **Twoja rola: zebrać dane, oddać Asi, pomóc gdzie trzeba.** Nie próbuj sama wysyłać GUS-u — Asia ma 8 dni na to (do 8. dnia miesiąca).

## Co musisz mieć przed startem
- [ ] **Asia obok** lub dostępna telefonicznie
- [ ] Wszystkie wstawienia poprzedniego miesiąca wpisane (#2)
- [ ] Wszystkie potwierdzenia (#3)
- [ ] Wszystkie faktury żywca w Symfonii
- [ ] Karteczka z dzisiejszą datą (do notatek)

---

## Checklist pierwszego dnia miesiąca

### ⏱ 9:00 — Inwentaryzacja zamknięcia poprzedniego miesiąca

- [ ] **Wszystkie wstawienia poprzedniego miesiąca potwierdzone?**
  - Otwórz "🐣 Cykle Wstawień" → filtr "data od / do" = poprzedni miesiąc
  - Sprawdź panel "do potwierdzenia" (po prawej) — **musi być 0** dla wstawień z poprzedniego miesiąca
  - Jeśli >0: pilnie zadzwoń do tych hodowców, potwierdź (#3)

- [ ] **Wszystkie dostawy odebrane fizycznie?**
  - Otwórz "📅 Kalendarz Dostaw Żywca" → poprzedni miesiąc
  - Czy są dostawy planowane ale nie odebrane? Wyjaśnij z Tereską

- [ ] **Wszystkie faktury żywca w Symfonii?**
  - Otwórz Symfonię → Zakupy → filtr po dacie (poprzedni miesiąc) + dostawca = hodowcy
  - Porównaj z listą dostaw z Kalendarza
  - Brakuje? Tereska wpisuje natychmiast — to blokuje GUS R09 Asi

### ⏱ 10:00 — Przygotowanie danych dla Asi (GUS R09)

- [ ] **W menu kategoria PRODUKCJA I MAGAZYN → kafelek "📊 Sprawozdania GUS"** (Asia ma to robić)
- [ ] Sprawdź wspólnie z Asią dane wiersza w14 (Brojlery):
  - r1 = sztuki za miesiąc (suma z `FarmerCalc.LumQnt`)
  - r2 = waga żywa farmerska (`NettoFarmWeight`)
  - r3 = waga poubojowa brutto (`NettoWeight`)
  - r4 = waga handlowa netto (Asia koryguje r3 o ubytki)
  - r5 = wartość zł (suma faktur z HANDEL)
[SCREEN: okno R09U z 5 wartościami zaznaczonymi]

- [ ] Drill-down (dwuklik wiersza) → lista wszystkich partii za miesiąc. Sprawdź czy nie ma anomalii (sztuki = 0, waga = 0).

### ⏱ 11:00 — Compliance ARiMR (gdy moduł Kontrakty wdrożony)

> **[DOSTĘPNE OD: Q3 2026 — patrz Część 4 audytu]**

- [ ] **W menu kategoria ZAOPATRZENIE I ZAKUPY → kafelek "🎯 Dashboard ARiMR Compliance"**
- [ ] Sprawdź % surowca pod 3-letnim kontraktem za poprzedni miesiąc
  - Cel: ≥ 50%
  - Jeśli < 50% → **Asia ma czerwony alert**, ty pomagasz przy listach hodowców do zakontraktowania

### ⏱ Cały dzień — bieżąca obsługa

- [ ] Telefony od hodowców o płatność za poprzedni miesiąc
  - "Zapłacimy do X (termin z umowy)" + sprawdź w "💵 Rozliczenia z Hodowcami"
  - Asia robi przelewy zwykle do 5. dnia miesiąca

- [ ] Wpisywanie nowych wstawień (jak codziennie #2)

- [ ] Nowe specyfikacje na nowy miesiąc (#7)

### ⏱ Przed końcem dnia

- [ ] **Notatka w karcie zespołu:** *"1. miesiąca: zamknięcie zakup OK. R09 gotowe / w toku. Compliance ARiMR XX%."* — żeby Ser i Asia wiedzieli na rano.

---

## ⚠️ Najczęstsze problemy

- **"Brakuje faktur żywca w Symfonii"** → Tereska wpisuje — to blokuje wszystko inne (R09, rozliczenia).
- **"R09U pokazuje sztuki = 0"** → Pewnie brak danych w `FarmerCalc` za poprzedni miesiąc. Sprawdź czy lekarz wet i portier wpisywali dane. Dzwoń do Sera (sprawdzi bazę).
- **"Hodowca dzwoni o płatność a Asia robi GUS"** → Powiedz hodowcy: "Asia jest w trakcie zamknięcia miesiąca, oddzwonimy do końca dnia." I oddzwonisz.
- **"Compliance ARiMR < 50%"** → Asia ma alarm. **Twoja rola: pomóc Asi przygotować listę propozycji kontraktów dla 5-10 hodowców spotowych.** Tereska/Magda dzwonią do nich w tygodniu.
- **"Pierwszy roboczy dzień to nie jest 1., tylko 2./3. (weekend)"** → Robisz dokładnie to samo, tylko z opóźnieniem. Asia i tak ma 8 dni na R09.

---

## 📞 Do kogo dzwonić

| Problem | Osoba |
|---|---|
| Brak faktur w Symfonii | **Tereska** |
| Compliance ARiMR < 50% | **Asia** + **Ser** |
| Hodowca dzwoni o płatność a Asia zajęta | Ty: "Oddzwonimy do końca dnia" + zapisz na liście |
| Anomalia w R09U (sztuki = 0) | **Ser** (techniczne) |
| Dzień jest weekendem | Przeniesione na pierwszy roboczy |

---

## ✅ Skąd wiesz że zrobiłaś dobrze

- Wszystkie wstawienia poprzedniego miesiąca **mają status: potwierdzone**.
- Tereska potwierdziła że **wszystkie faktury żywca są w Symfonii**.
- Asia ma **pełne dane do R09U** (nie musi gonić za informacjami).
- Compliance ARiMR widoczny + akcja jeśli potrzebna.
- **Notatka dla zespołu** poszła.

---

## 🔧 Pierwszy-dnia-miesiąca rytm dla całej firmy

| Dzień | Kto | Co |
|---|---|---|
| **1.** | Magda | Inwentaryzacja zamknięcia poprzedniego miesiąca (powyższy checklist) |
| **1.** | Asia | Start GUS R09 (ma do 8.) |
| **1.** | Asia | Rozliczenia zakupowe poprzedniego miesiąca (przelewy) |
| **1-5.** | Asia | Płatności hodowcom (zgodnie z terminem umów) |
| **5.** | Ser | Sprawdza compliance ARiMR + decyzje strategiczne |
| **8.** | Asia | **Deadline GUS R09** — wysyłka do Portalu Sprawozdawczego |
| **10.** | Asia | Raport zamknięcia miesiąca dla Sera |

---

## 🔧 Czego ZPSP NIE obsługuje (dziś)

> **[BRAK W ZPSP — DO DODANIA]** Brak **automatycznego checklistu pierwszego dnia miesiąca** w UI Magdy.
>
> *Workaround na teraz:* ta strona wydrukowana na biurku + powieszona na ścianie. Magda odhacza ręcznie.
>
> *Planowane:* w **Dashboard "Quick check Magda"** (Część 2 audytu, sekcja E) — sekcja "1. dnia miesiąca pokazuje się specjalny checklist". Asia + Ser to widzą.
