# 4. Wczytanie i wysłanie planu transportu (AviLog)

**Kiedy tego używasz:** gdy dostajesz **płachtę AviLog** (PDF z firmy AviLog z planem przewozów żywca) — typowo 1× dziennie / 2-3× tygodniowo.
**Ile czasu zajmuje:** 3-5 minut (jeśli wszystko się mapuje automatycznie) / 10 min (gdy musisz ręcznie poprawiać kierowców).
**Wideo:** [link — Ser dorzuci nagranie]

> Magda — AviLog to firma która **wozi żywiec** od hodowców do nas. Płachta = plan na dzień: kto którym ciągnikiem, do którego hodowcy, ile aut. To co tu wpiszesz, później idzie do **Rozliczeń AviLog** (Asia robi je w piątek). Cytat Asi: *"wpisuję dane ale nie mamy korzyści"* — Twoim zadaniem **nie jest** czuć się winną, tylko zrobić to co dziś działa.

## Co musisz mieć przed startem
- [ ] **PDF lub Excel od AviLogu** w mailu / na pulpicie
- [ ] Wstawienia hodowców (z #2) **już w systemie** — inaczej Matryca nie zmapuje
- [ ] Hodowca i kierowca muszą być w bazie (jeśli nowy kierowca — dzwoń do Tereski)

---

## Kroki — wczytanie płachty

**1. W menu głównym, kategoria ZAOPATRZENIE I ZAKUPY → kliknij kafelek "🚛 Matryca Transportu".**
[SCREEN: menu główne, kafelek "Matryca Transportu" zaznaczony]
Otworzy się pełnoekranowe okno "Matryca Transport — Planowanie dostaw żywca".

**2. W górnym pasku narzędzi znajdź zielone przyciski importu. Wybierz źródło:**
- PDF od AviLog → **"📄 IMPORT PDF"**
- Excel od AviLog → **"📊 IMPORT EXCEL"**
[SCREEN: górny pasek narzędzi Matrycy z przyciskami "IMPORT PDF", "IMPORT EXCEL", "WCZYTAJ Z BAZY", "ZAPISZ DO BAZY"]

**3. Otworzy się okno importu z mapowaniem. Po lewej zobaczysz kolumny z AviLogu (np. "KIEROWCA", "HODOWCA", "CIĄGNIK", "NACZEPA"), po prawej kolumny BAZA — które trzeba zmapować.** System spróbuje sam (przycisk **"Auto-mapuj"** jeśli widoczny) — sprawdź czy mapowanie wygląda OK.
[SCREEN: okno ImportAvilogWindow z dwoma kolumnami: lewo "AVILOG", prawo "BAZA", ComboBoxy do wyboru]

**4. Jeśli któryś kierowca / hodowca nie został rozpoznany (czerwona kropka / puste pole) — wybierz ręcznie z listy w ComboBox.** Jeśli kogoś nie ma w bazie — **zatrzymaj się i zadzwoń do Tereski** (musi go najpierw założyć).
[SCREEN: wiersz z czerwonym tłem i otwartą listą ComboBox z hodowcami]

**5. Kliknij przycisk "IMPORTUJ DO MATRYCY".** Dane przeniosą się do głównego DataGrid w Matrycy.
[SCREEN: główny DataGrid Matrycy wypełniony wierszami po imporcie]

**6. Sprawdź wiersze szybkim okiem — czy auta są przypisane, godziny się zgadzają. Jeśli widzisz oczywiste błędy (np. ten sam kierowca w 2 miejscach o tej samej godzinie) — popraw klikając w komórkę.**

**7. Kliknij **"ZAPISZ DO BAZY"** (zielony przycisk w górnym pasku) żeby plan zapisał się trwale.**
[SCREEN: przycisk "ZAPISZ DO BAZY" zaznaczony strzałką]
Bez kliknięcia "ZAPISZ" — zmiany znikną przy zamknięciu okna!

---

## Kroki — wysłanie planu z powrotem do AviLogu

> **[BRAK W ZPSP — DO DODANIA]** ZPSP **nie ma wbudowanego przycisku "Wyślij do AviLog"**. Plan dla AviLogu wysyłasz emailem ręcznie.

**Workaround:**
1. Po zapisie do bazy → kliknij prawym na DataGrid → **"Eksportuj do Excela"** (jeśli widoczne) lub zrób screenshot Snipping Tool.
2. Otwórz Outlook → nowy mail → adres AviLogu (zapytaj Tereskę) → załącz Excel / wklej screenshot.
3. Temat: `Plan transportu YYYY-MM-DD` (np. `Plan transportu 2026-05-26`).
4. Treść: krótko "W załączeniu plan na jutro / pojutrze".

---

## ⚠️ Najczęstsze problemy

- **"PDF nie chce się otworzyć"** → Pewnie format AviLog się zmienił. Spróbuj IMPORT EXCEL (poproś AviLog o Excela). Jeśli ani to nie idzie — **dzwoń do Sera** (parser do PDF jest w pliku `AvilogPdfParser.cs`, czasem trzeba poprawić).
- **"Kierowca/hodowca nie ma na liście podczas mapowania"** → Tereska musi go założyć w bazie kierowców / hodowców. **Nie dodawaj sama.**
- **"Zaimportowałam, ale w głównym DataGrid nic się nie pokazało"** → Sprawdź czy nie zapomniałaś kliknąć "IMPORTUJ DO MATRYCY" w oknie importu. Powtórz kroki 2-5.
- **"Zapomniałam ZAPISZ DO BAZY, zamknęłam okno"** → Zmiany przepadły. Musisz **wczytać PDF jeszcze raz**. Następnym razem zapamiętaj: ZAPISZ na końcu.
- **"To samo co wczoraj"** → Kliknij **"🗄️ WCZYTAJ Z BAZY"** żeby zobaczyć poprzedni plan, edytuj, ZAPISZ.

---

## 📞 Do kogo dzwonić

| Problem | Osoba |
|---|---|
| Nowy kierowca / hodowca / pojazd nie istnieje w bazie | **Tereska** (zakłada) |
| Parser PDF nie czyta płachty | **Ser** (zmiana parsera) |
| Nie wiem na jaki email AviLog | **Tereska** lub **Asia** |
| Płachta przyszła w innym formacie | **Asia** (decyzja czy nowy format akceptujemy) |
| Excel/PDF nie chce się załadować | **Ser** lub **Edyta** (IT) |

---

## ✅ Skąd wiesz że zrobiłaś dobrze

- W głównym DataGrid Matrycy widać **wiersze z dzisiejszą / jutrzejszą datą** odpowiadające płachcie.
- Wszystkie wiersze mają wypełnionych **hodowcę, kierowcę, ciągnik, godzinę**.
- Po kliknięciu "ZAPISZ DO BAZY" pojawił się komunikat sukcesu (toast lub MessageBox).
- W Rozliczeniach Avilog (kafelek obok) Asia w piątek **widzi te same kursy** — jeśli tak, wpisałaś OK.

---

## 🔧 Czego ZPSP NIE obsługuje (dziś)

> **[BRAK W ZPSP — DO DODANIA]** Brak przycisku "Wyślij plan do AviLogu" (email). Magda kopiuje ręcznie do Outlooka.
>
> *Workaround na teraz:* eksport DataGrid do Excela / screenshot + ręczny mail.
>
> *Planowane:* automatyczna wysyłka maila z Excelem na adres AviLogu po kliknięciu jednego przycisku (Część 2 audytu, RANK B).
