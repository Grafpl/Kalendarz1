# 11. Hodowca prosi o zmianę danych (NIP, adres, telefon, konto)

**Kiedy tego używasz:** hodowca dzwoni / pisze: "zmieniłem NIP / adres / konto bankowe / telefon" — zwykle 1-2× w tygodniu w całej bazie.
**Ile czasu zajmuje:** 3-5 minut na wniosek.
**Wideo:** [link — Ser dorzuci nagranie]

> Magda — **NIGDY nie zmieniaj danych hodowcy bezpośrednio.** Zawsze przez wniosek do Asi (workflow zatwierdzania). Powód: ARiMR i Symfonia muszą być w sync, jeden ruch w bok = miesiące szukania błędu.

## Co musisz mieć przed startem
- [ ] Hodowca w bazie (z `DOSTAWCY`)
- [ ] **Dowód zmiany** — mail, SMS, skan dokumentu (NIP zmienia się np. przy przekształceniu sp. z o.o.)
- [ ] Numer telefonu z którego dzwonił (do notatki)

---

## Kroki

**1. W menu kategoria ZAOPATRZENIE I ZAKUPY → kafelek "📝 Wnioski o Zmiany".**
[SCREEN: menu główne z kafelkiem "Wnioski o Zmiany" zaznaczonym]

**2. Otworzy się okno z DataGrid wniosków. Kliknij "➕ Nowy wniosek" (jeśli widoczny) lub menu kontekstowe → "Dodaj wniosek".**
[SCREEN: DataGrid wniosków z górnym przyciskiem "Nowy wniosek"]

**3. Wybierz hodowcę z listy (ComboBox).**

**4. W polu **Powód** wpisz konkretnie co się zmieniło, np.:**
```
Hodowca Kowalski Sp. z o.o. (były Jan Kowalski) zadzwonił 26.05.2026 14:23.
Przekształcenie firmy 01.06.2026 — nowy NIP 1234567890.
Dowód: mail z 25.05.2026 (folder _Wnioski_Zmian).
```
[SCREEN: formularz wniosku z wypełnionym polem Powód]

**5. **Dodaj pozycje zmiany** (każde pole które się zmienia = osobna pozycja):**

| FieldName | OldValue | NewValue |
|---|---|---|
| NIP | (stara wartość — system pobiera z bazy) | 1234567890 |
| Nazwa | Jan Kowalski | Kowalski Sp. z o.o. |
| Telefon | (jeśli zmienia, podaj) | 600 700 800 |

**6. Załącz dowód:** wrzuć mail/SMS do `\\192.168.0.170\Public\Wnioski_Zmian_Hodowcow\2026\` z nazwą `KOWALSKI_2026-05-26.png` lub `.eml`.

**7. Status wniosku domyślnie: `Proposed`. Kliknij "Zapisz".**

**8. Wniosek trafia do Asi — ona zatwierdza/odrzuca. Ty już nic nie robisz, dopóki Asia nie zapyta.**

---

## ⚠️ Najczęstsze problemy

- **"Hodowca chce żebym natychmiast zmieniła"** → Spokojnie wytłumacz: "Zmiana musi przejść przez naszą księgową (Asia). Wpiszę dziś, zatwierdzimy w ciągu doby." Nigdy nie zmieniaj na słowo.
- **"Nie wiem co wpisać w `OldValue` — system już mi pokazuje"** → OK, użyj tego co system podpowiada. Jeśli puste — wpisz "brak" lub "nieznane".
- **"Hodowca dał skan dokumentu zmiany NIP"** → Załącz skan do folderu sieciowego, w notatce wniosku wpisz ścieżkę.
- **"Zmiana to numer konta bankowego"** → SUPER ostrożnie. Asia ZAWSZE weryfikuje telefonicznie (oddzwania na hodowcę z numeru z bazy, nie z nowego SMS-a). Wpisz wniosek, ale dopisz `⚠️ WERYFIKACJA NUMERU KONTA PRZEZ TELEFON`.
- **"Wniosek odrzucony przez Asię — i co teraz?"** → Asia napisze powód odrzucenia. Skontaktuj się z hodowcą, wyjaśnij co brakuje, zbierz dane, złóż nowy wniosek.

---

## 📞 Do kogo dzwonić

| Problem | Osoba |
|---|---|
| Hodowca dzwoni o zmianę konta bankowego | **Asia** ZAWSZE — telefon kontrolny |
| Zmiana to przekształcenie firmy / sp. z o.o. | **Asia** + dokumenty (KRS, NIP, REGON) |
| Hodowca podaje sprzeczne dane | **Tereska** lub **Asia** (kontekst) |
| Wniosek się nie zapisał | **Ser** |
| Asia nieobecna a hodowca naciska | "Wpiszę wniosek, Asia oddzwoni najpóźniej jutro." |

---

## ✅ Skąd wiesz że zrobiłaś dobrze

- W oknie "Wnioski o Zmiany" widać **wiersz z dzisiejszą datą**, statusem **Proposed**, Twoim imieniem.
- W folderze `\\192.168.0.170\Public\Wnioski_Zmian_Hodowcow\2026\` leży **załącznik z dowodem**.
- Asia w ciągu 24h zmienia status na **Zdecydowany** (zaakceptowany lub odrzucony).
- Po akceptacji dane hodowcy są zmienione w **bazie + Symfonii**.

---

## 🔧 Czego ZPSP NIE obsługuje (dziś)

> **[BRAK W ZPSP — DO DODANIA]** Asia musi pamiętać żeby zaglądać do "Wnioski o Zmiany" sama — brak notyfikacji / badge na kafelku.
>
> *Workaround na teraz:* Asia robi to rano każdego dnia jako rutyna.
>
> *Planowane:* notification badge na kafelku Wnioski Zmian (Część 3 audytu, D8) — Asia widzi "5 do zatwierdzenia" bez otwierania okna.
