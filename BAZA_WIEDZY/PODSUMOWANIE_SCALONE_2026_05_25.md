# Scalone podsumowanie 2 sesji — 2026-05-25

> Połączenie dwóch równoległych sesji Claude Code pracujących na tym samym repo ZPSP.
> Sesja A = `cf4daf22` (Audyt Zaopatrzenia + Kontrakty). Sesja B = `d507b12b` (Audyt Broiler Meat Signals + instrukcje obsługi).

---

## 1. Co robiła każda sesja

### 🟢 Sesja A — Audyt "Zaopatrzenie i Zakup" + moduł Kontrakty Hodowców
**Kontekst kadrowy:** Paulina odeszła, Magda wchodzi, Tereska odchodzi, Asia → strażnik kontraktów. ARiMR IX.2026.

Wytworzyła:
- **`BAZA_WIEDZY/AUDYT_ZAKUPY_2026_05_23/`** — 10 dokumentów audytu (kafelki, wdrożenie Magdy, Centrum Asi, spec Kontraktów, quick wins weekend, komunikat zespół, refactor monstrów, Centrum Asi pełna spec, Pasza/Pisklęta)
- **`KOD_STARTOWY/`** — pełen moduł Kontrakty: 5 services C#, 4 okna WPF, SQL schema (6 tabel), generator Word OpenXML, migracja z Excela
- **`INSTRUKCJE_MAGDA/`** — 16 instrukcji + cheatsheet laminat
- **`INSTRUKCJE_ASIA/`** — 5 instrukcji (strażnik kontraktów, ZSRIR, GUS R09, dashboard ARiMR)
- **`INSTRUKCJE_TERESKA/`** — 2 (przekazanie 30 dni + inwentaryzacja wiedzy ukrytej)
- **`INSTRUKCJE_JUSTYNA/`** — 1 (HPAI/padłe procedura kryzysowa)

### 🔵 Sesja B — Audyt ZPSP vs książka "Broiler Meat Signals" + instrukcje obsługi
Przeczytała całą książkę branżową (196 str.) i zmapowała na kod.

Wytworzyła:
- **`BAZA_WIEDZY/AUDYT_BROILER_SIGNALS/`** — 13 plików: executive summary, inwentaryzacja 12 obszarów, **12 nowych funkcji** (FPD scorecard, stunning CCP, chilling curve, PM defects, traceability, Salmonella LIMS...), 8 ulepszeń, priorytetyzacja, BRC v9 mapping, SQL DDL (~17 tabel), słowniczek, dzień z życia (6 scenariuszy), **kurs drobiarstwa od zera**, przykłady życiowe per funkcja
- **`BAZA_WIEDZY/INSTRUKCJE_OBSLUGI/`** — 7 instrukcji "deep" (Cykle Wstawień, Lista Wstawień, Lista Partii, Reklamacje, Kalendarz Dostaw, Baza Hodowców, Umowy i Dokumenty)
- **Naprawa 2 bugów w kodzie produkcyjnym** (na końcu sesji):
  - Avilog menu kontekstowe nie otwierało okna → `WidokAvilogPlan` konstruktor owinięty try/catch
  - Cykle wstawień nie pokazywały dostaw → `Window_Loaded` w `WstawienieWindow.xaml.cs` owinięty try/catch

---

## 2. ⚠️ PRZECIĘCIA I KONFLIKTY (najważniejsze przy scalaniu)

### ✅ KONFLIKT 1: Dwa równoległe foldery instrukcji tego samego okna — ROZWIĄZANY 2026-05-25
> Zlinkowano oba foldery: w `INSTRUKCJE_MAGDA/00_INDEKS.md` dodano mapę „zadanie ↔ pełny opis okna" + linki w plikach #02/#06; w `INSTRUKCJE_OBSLUGI/README.md` dodano sekcję role-based + notę kadrową (Maja→Magda/Asia). Podział: OBSLUGI = referencja okna, MAGDA = krok po kroku. Komplementarne.
| Okno | Sesja A | Sesja B |
|---|---|---|
| Cykle Wstawień | `INSTRUKCJE_MAGDA/02_nowe_wstawienie.md` | `INSTRUKCJE_OBSLUGI/01_Wstawienia_Kurczakow.md` (deep, 23 sekcje) |
| Baza Hodowców | (wzmianka w #1, #10) | `INSTRUKCJE_OBSLUGI/06_Baza_Hodowcow.md` (deep CRM) |
| Kalendarz Dostaw | (wzmianka) | `INSTRUKCJE_OBSLUGI/05_Kalendarz_Dostaw_Zywca.md` (deep) |
| Umowy/Dokumenty | `INSTRUKCJE_MAGDA/06_umowa_zakupu.md` (proces + Word) | `INSTRUKCJE_OBSLUGI/07_Umowy_Dokumenty.md` (opis okna AS-IS) |

**Problem:** Magda dostanie 2 różne instrukcje tego samego ekranu. Trzeba zdecydować jedną kanoniczną strukturę.

**Rekomendacja:**
- `INSTRUKCJE_OBSLUGI/` = **referencja techniczna "jak działa okno"** (deep, wszystkie funkcje) — dla każdego
- `INSTRUKCJE_MAGDA/` = **role-based "co robisz krok po kroku"** (operacyjne, pod stres nowej osoby) — dla Magdy
- W instrukcjach Magdy **dodać link** "pełny opis funkcji → INSTRUKCJE_OBSLUGI/XX". Nie usuwać żadnego — są komplementarne (zadanie vs referencja).

### 🔴 KONFLIKT 2: Umowy — AS-IS vs TO-BE
- Sesja B `07_Umowy_Dokumenty.md` opisuje **istniejące** okno `SprawdzalkaUmowWindow` (status binarny per dostawa).
- Sesja A `04_MODUL_KONTRAKTY_SPEC.md` + kod = **nowy** moduł Kontrakty (rejestr, numeracja, ARiMR dashboard).
- **Scalenie:** instrukcja B = stan dzisiejszy; moduł A = docelowy. W obu dopisać wzajemny link + notkę "moduł Kontrakty zastąpi/uzupełni to w Q3 2026".

### 🟡 KONFLIKT 3: ARiMR — wspólny deadline, dwa plany inwestycyjne
- Sesja A: dashboard compliance pod **3-letnie kontrakty na 50% surowca** (deadline IX.2026).
- Sesja B: lista funkcji **"ARiMR-fundable"** do IX.2026 (FPD scorecard, traceability, chilling itp.).
- **To ta sama dotacja (do 10 mln) i ten sam deadline.** Powinny być **jednym wnioskiem inwestycyjnym** — Asia (kontrakty) + inwestycje technologiczne (funkcje B). Scalić w jeden plan ARiMR.

### 🟡 KONFLIKT 4: Ten sam plik kodu dotykany przez obie sesje
- `Zywiec/WstawieniaKurczaka/WstawienieWindow.xaml.cs`:
  - **Sesja B faktycznie EDYTOWAŁA** (try/catch w Window_Loaded — naprawa buga "nie pokazuje dostaw").
  - **Sesja A planowała** QW6 (walidator sztuk 0-250k) w tym samym pliku — jeszcze nie wykonane.
- **Akcja:** quick win QW6 z sesji A nakładać **na już naprawioną wersję** z sesji B (sprawdzić git diff przed dodaniem walidatora).

### 🟢 KOMPLEMENTARNE (bez konfliktu — wzmacniają się)
- **Jakość/padłe:** Sesja A `INSTRUKCJE_JUSTYNA/01` (flow operacyjny HPAI) + Sesja B funkcje PM defects/DOA/FPD scorecard (głębia produkcyjna). Razem = pełny obraz.
- **Edukacja:** Sesja A cheatsheet/słowniczek Magdy + Sesja B kurs drobiarstwa od zera + słowniczek FPD/CCP. Razem = ścieżka nauki od zera.
- **Reklamacje:** tylko sesja B (`04_Reklamacje.md`) — uzupełnia lukę sesji A.

---

## 3. Mapa CAŁEGO `BAZA_WIEDZY/` po obu sesjach

```
BAZA_WIEDZY/
├── AUDYT_ZAKUPY_2026_05_23/        [Sesja A] 10 dok + KOD_STARTOWY + SQL + Migracja + Szablony_Word
├── AUDYT_BROILER_SIGNALS/          [Sesja B] 13 plików (audyt książki + 12 funkcji + kurs)
├── INSTRUKCJE_MAGDA/               [Sesja A] 16 + cheatsheet (role-based, operacyjne)
├── INSTRUKCJE_ASIA/                [Sesja A] 5 (strażnik kontraktów)
├── INSTRUKCJE_TERESKA/             [Sesja A] 2 (przekazanie wiedzy)
├── INSTRUKCJE_JUSTYNA/             [Sesja A] 1 (HPAI/padłe)
├── INSTRUKCJE_OBSLUGI/             [Sesja B] 7 deep (referencja "jak działa okno")
└── PODSUMOWANIE_SCALONE_2026_05_25.md  [ten plik]
```

---

## 4. Zunifikowany plan następnych kroków (obie sesje razem)

### Weekend (przed Magdą 26.05)
1. **[A]** 9 quick winów (ukryć Pasza, walidatory, bannery) — **UWAGA: QW6 nakładać na naprawiony przez [B] WstawienieWindow.xaml.cs**
2. **[A]** Uruchomić SQL `01_Kontrakty_v1_schema.sql`
3. **[A]** Konta Magdy (ZPSP + Symfonia), folder potwierdzeń
4. **[B]** Zweryfikować że naprawy bugów (Avilog + dostawy wstawień) są w prod i działają
5. **Scalić instrukcje:** w `INSTRUKCJE_MAGDA/` dodać linki do deep wersji w `INSTRUKCJE_OBSLUGI/`

### Czerwiec
6. **[A]** Moduł Kontrakty Faza 1-3 (13 dni)
7. **[B]** Quick wins z Broiler Signals (FPD scorecard, DOA tracking — ≤1 tydz. każdy)
8. **Scalić plan ARiMR:** Asia (kontrakty 50%) + inwestycje technologiczne (funkcje B) = jeden wniosek do IX.2026

### Q3 2026
9. **[A]** Centrum Asi (5 dni) + Pasza/Pisklęta (9-12 dni)
10. **[B]** Strategic funkcje Broiler (traceability, Salmonella LIMS, chilling curve)
11. **[A]** Refactor 5 plików-monstrów (14 tyg.) — **w tym `WstawienieWindow` którego dotykały obie sesje**

### Do IX.2026 (ARiMR)
12. **Wspólny wniosek ARiMR** = kontrakty 3-letnie (50% surowca, dashboard A) + inwestycje BRC/jakość (funkcje B) + mapping BRC v9 (B)

---

## 5. Jedno zdanie podsumowania

> **Sesja A zbudowała "dział zakupu pod Magdę i Asię + system kontraktów ARiMR". Sesja B zbudowała "modernizację produkcyjno-jakościową wg światowej książki branżowej + komplet instrukcji obsługi okien". Nakładają się na instrukcjach (Wstawienia/Hodowcy/Kalendarz/Umowy) i na pliku WstawienieWindow.xaml.cs — trzeba zlinkować instrukcje (role-based ↔ referencja) i połączyć oba w jeden wniosek ARiMR do IX.2026.**

---

*Scalono: 2026-05-25 z sesji cf4daf22 (Zaopatrzenie+Kontrakty) + d507b12b (Broiler Signals). Plik referencyjny — nie kompilowany.*
