# 5. SMS do hodowcy z godziną załadunku

**Kiedy tego używasz:** dzień przed odbiorem żywca — wysłać hodowcy SMS z godziną załadunku i danymi ciągnika.
**Ile czasu zajmuje:** 1 minuta na hodowcę.
**Wideo:** [link — Ser dorzuci nagranie]

> Magda — w ZPSP jest przycisk SMS, ale **on nie wysyła SMS-a sam**. Generuje treść, kopiuje do schowka (Ctrl+C), Ty potem **wklejasz w SMS Desktop / telefonie** i wysyłasz. To workaround na teraz (Ser obiecał pełną automatyzację).

## Co musisz mieć przed startem
- [ ] **Plan transportu z dnia / dnia jutrzejszego wczytany** (instrukcja #4)
- [ ] **Numer telefonu hodowcy** w bazie (jeśli brak — dodaj przed wysyłką)
- [ ] Otwarta aplikacja do wysyłania SMS (**SMS Desktop**, **WhatsApp Web**, albo wyciągasz telefon)

---

## Kroki — wysłanie SMS-a

**1. Otwórz "🚛 Matryca Transportu" (jak w #4). Plan musi być widoczny w głównym DataGrid.**
[SCREEN: główny DataGrid Matrycy z wczytanym planem na jutro]

**2. Sprawdź czy każdy hodowca ma uzupełniony numer telefonu (kolumna "Telefon" w gridzie). Jeśli pusto — kliknij komórkę, wpisz numer w formacie `+48 600 700 800` lub `600 700 800`.**
[SCREEN: kolumna "Telefon" z przykładowymi numerami, jedna komórka pusta zaznaczona czerwoną ramką]
**Bez numeru SMS nie skopiuje się do schowka** — system wyrzuci komunikat.

**3. Wybierz hodowcę dla którego wysyłasz SMS (kliknij wiersz). Następnie w górnym pasku narzędzi znajdź dwa przyciski:**
- **"SMS WSZYSTKIE"** — kopiuje treść SMS-a do **wszystkich aut** tego hodowcy w jednej wiadomości
- **"SMS TO AUTO"** — kopiuje treść tylko dla **zaznaczonego auta**
[SCREEN: przyciski "SMS WSZYSTKIE" i "SMS TO AUTO" w górnym pasku zaznaczone strzałką]
Standardowo używaj **"SMS WSZYSTKIE"**.

**4. Po kliknięciu przycisku **treść SMS-a kopiuje się automatycznie do schowka** (jak Ctrl+C). Zobaczysz krótki komunikat "Skopiowano do schowka".**
[SCREEN: toast / MessageBox z napisem "Treść SMS skopiowana do schowka"]
Przykładowa treść:
```
Piorkowscy z 27 na 28 listopada (piątek):
Załadunek godz.14:30
ciągnik:DW1234 naczepa:NA5678
śr.waga:1850.00kg
```

**5. Otwórz "SMS Desktop" (skrót na pulpicie) albo aplikację którą wysyłasz SMS-y. Stwórz nową wiadomość.**
[SCREEN: okno SMS Desktop / inne narzędzie z polem nowego SMS-a]

**6. Wpisz numer hodowcy (z gridu w Matrycy — możesz skopiować Ctrl+C / Ctrl+V).**

**7. W polu treści wciśnij **Ctrl+V** — treść SMS-a wklei się z schowka.**

**8. Sprawdź czy wszystko się zgadza (godzina, ciągnik, naczepa, waga) → kliknij "Wyślij" w SMS Desktop.**

---

## Kroki — dla wielu hodowców pod rząd

Powtórz kroki 3-8 dla każdego hodowcy. **Tip:** szybciej idzie jeśli najpierw wszystkim wkleisz w SMS Desktop jako szkice, potem hurtem wyślesz.

---

## ⚠️ Najczęstsze problemy

- **"Kliknęłam SMS WSZYSTKIE i nic się nie skopiowało"** → Sprawdź czy hodowca ma uzupełniony **numer telefonu**. Bez numeru system nie kopiuje.
- **"W SMS-ie jest dziwna data / godzina"** → Wracaj do planu w Matrycy, popraw godzinę w wierszu, ZAPISZ DO BAZY, dopiero potem klikaj SMS.
- **"Skopiowała się tylko część"** → Złe ustawienia schowka. Spróbuj jeszcze raz albo wklej do Notatnika, sprawdź, potem do SMS Desktop.
- **"Nie wiem na jaki numer wysłać"** → Sprawdź kolumnę "Telefon" w gridzie. Jeśli pusta — zadzwoń do Tereski / Asi po numer, wpisz, zapisz, potem SMS.
- **"SMS wysłałam, ale hodowca twierdzi że nie dostał"** → Sprawdź czy w SMS Desktop pokazuje "Dostarczono". Zadzwoń do hodowcy, potwierdź ustnie.

---

## 📞 Do kogo dzwonić

| Problem | Osoba |
|---|---|
| Brak numeru telefonu hodowcy w gridzie | **Tereska** lub **Asia** (mają telefonię) |
| SMS Desktop nie działa | **Edyta** (IT) |
| Nie wiem jak edytować treść SMS-a (chcę dodać własną notatkę) | Po wklejeniu w SMS Desktop **edytuj swobodnie** przed wysłaniem |
| Hodowca skarży się że SMS-y są mylące | **Asia** (zarządza komunikacją) |

---

## ✅ Skąd wiesz że zrobiłaś dobrze

- Po kliknięciu "SMS WSZYSTKIE" pojawił się **komunikat o skopiowaniu**.
- W SMS Desktop / WhatsApp **treść wklejona poprawnie** (godzina, ciągnik, waga).
- W SMS Desktop widać **"Dostarczono"** lub "✓✓" przy wiadomości.
- Hodowca **nie dzwoni z pytaniem "kiedy przyjeżdżacie"** — dobry znak.

---

## 🔧 Czego ZPSP NIE obsługuje (dziś)

> **[BRAK W ZPSP — DO DODANIA]** ZPSP **nie wysyła SMS-ów automatycznie**. Ma serwis `SmsService.cs` przygotowany pod 3 bramki (SMSAPI.pl, SMSCenter, SerwerSMS), ale **brak skonfigurowanego klucza API** w pliku konfiguracji. SMS kopiuje się do schowka, Magda wkleja ręcznie.
>
> *Workaround na teraz:* schowek + SMS Desktop / WhatsApp / telefon.
>
> *Planowane:* skonfigurowanie SMSAPI.pl (Ser zamawia konto), wtedy "SMS WSZYSTKIE" będzie po prostu wysyłać SMS. Czas: ~1 dzień pracy Sera + miesiąc abonament SMSAPI ~50 zł.
