# 13. Waga przyjętego żywca nie zgadza się z wstawieniem

**Kiedy tego używasz:** po dostawie portier waży żywiec, a różnica między **deklaracją hodowcy** a **wagą u nas** jest większa niż typowy ubytek (>5%) — sygnał problemu.
**Ile czasu zajmuje:** 5-10 minut sprawdzenia + eskalacja jeśli serio.
**Wideo:** [link — Ser dorzuci nagranie]

> Magda — niewielkie różnice wag (2-3%) **są normalne** (ubytek transportowy). Powyżej 5% to czerwona flaga: może problem z paszą / chorobą / oszustwo wagowe. **Twoja rola: zauważyć, zweryfikować, eskalować.**

## Co musisz mieć przed startem
- [ ] Wstawienie hodowcy w systemie (z #2)
- [ ] **Dostawa już odebrana** (portier zważył, lekarz wet zważył klasyfikację)
- [ ] Kalkulator (do prostego liczenia procentów)

---

## Kroki

**1. W menu kategoria PRODUKCJA I MAGAZYN → kafelek "📋 Lista Partii Ubojowych" (lub w "📅 Kalendarz Dostaw Żywca" — pokazuje to samo).**
[SCREEN: kafelek "Lista Partii Ubojowych" zaznaczony]

**2. Wyszukaj partię po nazwie hodowcy lub dacie odbioru. Otwórz szczegóły partii (dwuklik).**
[SCREEN: lista partii z filtrem po hodowcy, jedna partia zaznaczona]

**3. **W szczegółach partii znajdź dwie kluczowe wagi:**

| Pole | Co znaczy | Skąd brać |
|---|---|---|
| **Waga deklarowana** (Farmer) | Co napisał hodowca | z wstawienia / WZ |
| **Waga rzeczywista** (ubojnia) | Co ważył nasz portier | z `In0E` |

[SCREEN: szczegóły partii z podświetlonymi polami Waga deklarowana i Waga rzeczywista]

**4. **Policz różnicę procentową** (kalkulator albo na palcach):**
```
Różnica % = (Waga deklarowana − Waga rzeczywista) / Waga deklarowana × 100%

Przykład:
Waga deklarowana: 10 000 kg
Waga rzeczywista: 9 400 kg
Różnica: (10 000 − 9 400) / 10 000 × 100% = 6%

🚨 6% > 5% = sygnał problemu, eskaluj.
```

**5. **Próg interpretacji (ściągawka):**

| Różnica % | Co to znaczy | Akcja |
|---|---|---|
| 0–3% | **Normalny ubytek transportowy** | OK, nic nie rób |
| 3–5% | **Zwiększony ubytek** (długi transport, ciepło) | Notatka w partii, info do Tereski |
| 5–10% | **Sygnał problemu** (waga hodowcy zawyżona albo żywiec chory) | Eskalacja: Justyna (jakość) + Asia |
| > 10% | **Czerwona flaga** (możliwe oszustwo, masowy padłe, choroba) | Natychmiast: Ser + Justyna + Łukasz |

**6. **Wpisz notatkę w partii** (pole notatek partii lub w specyfikacji):**
```
2026-05-26 — różnica wagi 6%.
Deklarowana: 10 000 kg, rzeczywista: 9 400 kg.
Powód do sprawdzenia (chory żywiec? długi transport? błąd ważenia hodowcy?).
Eskalowane do: Justyna (14:30), Asia (14:35).
```

**7. **Eskaluj zgodnie z progiem (krok 5).** Powiedz Justynie / Asi konkretnie ile %.**

**8. **Po decyzji szefostwa (Asia/Ser):**
- Płacimy wg **rzeczywistej wagi** (zwykle) — to standard
- ALBO wystawiamy **reklamację** do hodowcy (jeśli zawyżenie ewidentne)
- ALBO **negocjujemy** cenę za tę dostawę

---

## ⚠️ Najczęstsze problemy

- **"Hodowca dzwoni: 'nasza waga była 10 000 a u was pokazujecie 9 400'"** → Spokojnie: "Tak, mamy 6% różnicy. To powyżej typowego ubytku. Rozmawiamy o tym z naszą księgową, oddzwonimy z decyzją."
- **"Lekarz wet wpisał wysokie padłe (>5%)"** → To może tłumaczyć dużą różnicę wagi. Sprawdź `Panel Lekarza` → DeclI2-I5 dla tej partii. Jeśli zgadza się, normalne. Jeśli nie — eskaluj.
- **"Różnica zawsze taka sama dla tego hodowcy"** → Systematyczna nieuczciwość albo systematyczny problem (np. odwodnienie u tego hodowcy). **Asia musi to wiedzieć** — może decyzja cenowa lub rozstanie.
- **"Waga rzeczywista WYŻSZA niż deklarowana"** → To rzadkość. Sprawdź czy nie pomyłka w wadze portiera. Jeśli nie — hodowca **zaniżył** (rzadkie, ale się zdarza).
- **"Nie wiem jak otworzyć szczegóły partii"** → Tereska Ci pokaże. To moduł Lista Partii — duży, ale tutaj wystarczy jedna karta szczegółów.

---

## 📞 Do kogo dzwonić

| Sytuacja | Osoba |
|---|---|
| Różnica 3-5% (typowe) | Notatka, **Tereska** info |
| Różnica 5-10% (problem) | **Justyna** (jakość) + **Asia** |
| Różnica > 10% (krytyczne) | **Ser** + **Justyna** + **Łukasz** od razu |
| Hodowca dzwoni rozdzwoniony | Eskaluj do **Asi** |
| Reklamacja do hodowcy | **Asia** (księgowość) |
| Wstrzymanie płatności | **Asia** ZAWSZE |

---

## ✅ Skąd wiesz że zrobiłaś dobrze

- Notatka w partii **jest** (z liczbą % różnicy, datą, kontekstem).
- Właściwa osoba (Justyna / Asia / Ser) **dostała info** zgodnie z progiem.
- Hodowca nie został pominięty (jeśli dzwonił — oddzwoniliśmy z decyzją w ciągu doby).
- **Reklamacja** jest sformalizowana w Symfonii (jeśli Asia/Ser podjęli taką decyzję).

---

## 🔧 Czego ZPSP NIE obsługuje (dziś)

> **[BRAK W ZPSP — DO DODANIA]** Brak **automatycznego alertu** "różnica wagi > 5% dla partii X". Magda musi liczyć ręcznie.
>
> *Workaround na teraz:* kalkulator + dyscyplina sprawdzania każdej partii.
>
> *Planowane:* w `RaportyHodowcow` automatyczny ranking "hodowcy z systematyczną różnicą wagi > 3%" (Część 3 audytu, sekcja F). Engine alertów wykryje trend "3 raz z rzędu różnica > 5%" i wyśle do Asi.
