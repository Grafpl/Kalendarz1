# 6. Umowa zakupu (ze ściągą: % ubytku, rozliczana waga)

**Kiedy tego używasz:** za każdym razem gdy zaczynasz współpracę z **nowym hodowcą** albo gdy odnawiasz umowę istniejącemu (typowo raz w roku, niedługo: raz na 3 lata pod ARiMR).
**Ile czasu zajmuje:** 15-20 minut (pierwsza umowa z Asią — 30 min, żeby zrozumieć).
**Wideo:** [link — Ser dorzuci nagranie]
**📘 Jak działa obecne okno umów (AS-IS):** [`../INSTRUKCJE_OBSLUGI/07_Umowy_i_Dokumenty.md`](../INSTRUKCJE_OBSLUGI/07_Umowy_i_Dokumenty.md) — opis dzisiejszego okna „Dokumenty i Umowy" (Sprawdzalka + statusy). **Ta instrukcja** = proces tworzenia umowy + ściąga % ubytku. **Docelowo** zastąpi je moduł Kontrakty Hodowców → [`../AUDYT_ZAKUPY_2026_05_23/04_MODUL_KONTRAKTY_SPEC.md`](../AUDYT_ZAKUPY_2026_05_23/04_MODUL_KONTRAKTY_SPEC.md).

> Magda — **TO NAJWAŻNIEJSZA INSTRUKCJA Z PUNKTU WIDZENIA SERA**. Umowy to teraz najsłabsza strona działu zakupu. **Pierwsze 3 umowy rób z Asią** (ona przejmuje strażnika kontraktów). Nigdy sama, dopóki nie poczujesz że ogarniasz % ubytku, rozliczaną wagę i termin płatności.

## Co musisz mieć przed startem
- [ ] **Hodowca** w bazie (jak nie, wniosek do Asi przez "Wnioski Zmian")
- [ ] **Szablon umowy** (Word `.docx`) od Asi — zawiera puste pola do wypełnienia
- [ ] **Ustalenia z hodowcą** spisane (% ubytku, cena, rynek, termin płatności)
- [ ] **Numer NIP i nr gospodarstwa** hodowcy (od Asi)
- [ ] Asia obok (pierwsze umowy)

---

## Część A — wypełnienie szablonu Word

**1. Skopiuj szablon umowy z folderu `\\192.168.0.170\Install\UmowyZakupu\_SZABLON\` na swój pulpit.** Nazwa pliku: `Umowa_TEMPLATE_2026.docx` (ostatnia wersja).
[SCREEN: Eksplorator Windows w folderze _SZABLON z plikiem szablonu]
**Nigdy nie edytuj szablonu w jego miejscu** — zawsze kopiuj, potem edytuj kopię.

**2. Otwórz kopię w Wordzie. Zmień nazwę pliku na schemat: `Umowa_NAZWISKO_yyyy-MM-dd.docx` (np. `Umowa_Kowalski_2026-05-26.docx`).**

**3. Wypełnij pola w szablonie. Asia pokaże Ci 6 pól które ZAWSZE musisz wypełnić:**

| Pole | Co wpisać | Skąd brać |
|---|---|---|
| **Hodowca** | imię, nazwisko, adres | z kartoteki ZPSP |
| **NIP** | NIP gospodarstwa | dokument hodowcy |
| **Nr gospodarstwa** | nr ewidencyjny ARiMR | dokument hodowcy |
| **% ubytku** | typowo **2.5% – 3%** | ustalenie z hodowcą |
| **Rynek (typ ceny)** | wolnorynkowy / rolniczy / ministerialny | ustalenie z Serem |
| **Termin płatności** | typowo **21 dni** od dostawy | ustalenie z hodowcą |

[SCREEN: Word otwarty na szablonie umowy z podświetlonymi 6 polami do wypełnienia]

**4. Zapisz Word (Ctrl+S). NIE drukuj jeszcze — najpierw przeczyta Asia.**

**5. Wyślij plik Asi mailem albo na Teams. Asia sprawdza, potem akceptuje / poprawia.**

**6. Po akceptacji — drukujesz w 2 egzemplarzach + podpisuje Ser (właściciel) + wysyłasz hodowcy do podpisu.**

**7. Gdy hodowca odeśle podpisany skan / oryginał — kopiujesz do folderu sieciowego (patrz część C niżej).**

---

## Część B — wpisanie statusu umowy w ZPSP

**1. W menu głównym kategoria ZAOPATRZENIE I ZAKUPY → kafelek "📑 Dokumenty i Umowy".**
[SCREEN: kafelek "Dokumenty i Umowy" zaznaczony]

**2. Otworzy się okno z listą umów. Kliknij **"📑 Nowa umowa"** (zielony przycisk w górnym pasku).**
[SCREEN: górny pasek z przyciskiem "Nowa umowa" zaznaczony]

**3. W formularzu wybierz hodowcę (lista) + daty + zaznacz checkbox statusu:**
- **UTWORZONA** — gdy stworzyłaś Word i Asia zaakceptowała
- **WYSŁANA** — gdy wysłałaś hodowcy do podpisu
- **OTRZYMANA** — gdy hodowca odesłał podpisany dokument
- **POŚREDNIK** — zaznacz tylko jeśli umowa idzie przez pośrednika (sytuacja rzadka)
[SCREEN: formularz nowej umowy z checkboxami statusów: UTWORZONA, WYSŁANA, OTRZYMANA, POŚREDNIK]

**4. Kliknij **"Zapisz"**.** Wpis pojawia się na liście.

**5. Po każdej zmianie statusu (np. dzisiaj wysłałam, jutro hodowca odesłał) → znajdź umowę na liście, kliknij **"✏ Edytuj"**, **zaznacz/odznacz** odpowiedni checkbox, **Zapisz**.**

---

## Część C — archiwum PDF skanu umowy

**1. Po otrzymaniu skanu podpisanej umowy od hodowcy — zapisz PDF/skan w folderze sieciowym:**
```
\\192.168.0.170\Install\UmowyZakupu\2026\
```
**Nazewnictwo:** `Umowa_NAZWISKO_yyyy-MM-dd.pdf` (taki sam schemat jak Word).

**2. W ZPSP, na liście umów, kliknij prawym na wierszu → **"📁 Pokaż plik umowy w Eksploratorze"** żeby się upewnić że plik jest na miejscu.**
[SCREEN: menu kontekstowe z opcją "📁 Pokaż plik umowy w Eksploratorze" zaznaczone]

---

## 📋 Ściąga — % ubytku, rozliczana waga, ceny

| Termin | Co znaczy | Typowo |
|---|---|---|
| **% ubytku** | Strata wagi między ważeniem u hodowcy a u nas (transport, odwodnienie) | 2.5 – 3.5% (więcej = krzywdzi hodowcę, mniej = krzywdzi nas) |
| **Waga deklarowana** (Farmer) | Waga którą ważył hodowca | Pisze hodowca |
| **Waga rozliczana** | Waga × (1 - %ubytku) | **To po niej płacimy** |
| **Cena (rynek)** | Stawka zł/kg, zależnie od umowy | Ser ustala (wolnorynkowa = ryzyko, rolnicza = stabilna, ministerialna = baseline) |
| **Termin płatności** | Ile dni od dostawy do przelewu | 7 / 14 / 21 / 30 dni (typowo 21) |

**Przykład rozliczenia:**
- Hodowca ważył: **10 000 kg**
- % ubytku w umowie: **3%**
- Waga rozliczana: 10 000 × (1 - 0.03) = **9 700 kg**
- Cena: **7.50 zł/kg**
- Do zapłaty: 9 700 × 7.50 = **72 750 zł netto**

---

## ⚠️ Najczęstsze problemy

- **"Nie wiem jaki % ubytku zaproponować"** → Asia / Ser. Standardowo dla nowego hodowcy zaczynamy od **3%**.
- **"Hodowca chce 1% — czy to OK?"** → **NIE.** 1% = my dopłacamy do dostawy. **Zatrzymaj się, dzwoń do Sera.**
- **"Hodowca chce termin płatności 60 dni"** → Asia/Ser. Standard to 21 dni. Dłużej tylko za zgodą.
- **"Zapomniałam zaznaczyć checkbox WYSŁANA"** → Edytuj umowę w ZPSP, zaznacz, zapisz. Audyt sprawdzi.
- **"Hodowca podpisał ale nie chce odsyłać oryginału"** → Skan PDF wystarczy do folderu sieciowego. **Oryginał musi przyjść w ciągu miesiąca.** Asia kontroluje.

---

## 📞 Do kogo dzwonić

| Problem | Osoba |
|---|---|
| **Jakiekolwiek pytanie o treść umowy** (% ubytku, cena, rynek) | **Asia** ZAWSZE (jeździ ze Serem do prawniczki) |
| Hodowca dyktuje warunki które są dziwne | **Asia** → **Ser** |
| Skan umowy nie chce zapisać do folderu | **Edyta** (IT) |
| Hodowca nie odsyła podpisanej umowy 2+ tygodnie | **Tereska** → telefon → **Asia** |
| Hodowca pyta o "kontrakt 3-letni ARiMR" | **Asia** (to jej obszar, ważne pod dotację) |

---

## ✅ Skąd wiesz że zrobiłaś dobrze

- W oknie "Dokumenty i Umowy" widać **wiersz z hodowcą i 3 zaznaczonymi checkboxami** (UTWORZONA, WYSŁANA, OTRZYMANA).
- W folderze `\\192.168.0.170\Install\UmowyZakupu\2026\` leży PDF z podpisem hodowcy.
- Asia powiedziała **"OK, akceptuję"** na ostatnim widoku.
- Hodowca zaczyna dostarczać żywiec na ustalonych warunkach (% ubytku, termin płatności).

---

## 🔧 Czego ZPSP NIE obsługuje (dziś)

> **[BRAK W ZPSP — DO DODANIA]** — to **najważniejsza dziura w module zakupu**:
> - Nie ma **generatora umów** w ZPSP — szablon Word ręcznie.
> - Nie ma **rejestru kontraktów z numeracją** (1/27, 2/27...) — checkboxy to wszystko.
> - Nie ma **alertu "umowa wygasa za 3 mies."** — Asia musi pilnować ręcznie.
> - Nie ma **dashboardu ARiMR Compliance** (% surowca pod 3-letnim kontraktem).
>
> *Workaround na teraz:* Word ręcznie + skan w folderze + checkboxy w ZPSP + Asia trzyma wszystko w głowie.
>
> *Planowane:* **pełny moduł "Kontrakty Hodowców"** (Część 4 audytu Sera). Generator Word, numeracja, alerty, dashboard. Effort ~2-3 tygodnie pracy Sera, wdrożenie do końca lipca 2026 (pod ARiMR + sp. z o.o.).
