# 2. Wpisanie nowego wstawienia hodowcy

**Kiedy tego używasz:** za każdym razem gdy hodowca dzwoni "wstawiliśmy pisklęta dziś / wczoraj / pojutrze".
**Ile czasu zajmuje:** 1-2 minuty na hodowcę.
**Wideo:** [link — Ser dorzuci nagranie]
**📘 Pełny opis okna (wszystkie funkcje, seria, kalendarz, formuły):** [`../INSTRUKCJE_OBSLUGI/01_Wstawienia_Kurczakow.md`](../INSTRUKCJE_OBSLUGI/01_Wstawienia_Kurczakow.md) — czytaj gdy chcesz zrozumieć **całe** okno, nie tylko swój krok. Ta instrukcja = „co klikasz krok po kroku", tamta = „jak działa cała maszyna".

> Magda — to jeden z **dwóch najczęstszych Twoich kroków** (drugi to potwierdzenie, instrukcja #3). Dopóki nie wpiszesz wstawienia, kalendarz dostaw nie wie kiedy odbierzemy żywiec. **Nikt nie stoi za Tobą — pytaj zawsze gdy masz wątpliwość.**

## Co musisz mieć przed startem
- [ ] **Imię/nazwisko hodowcy** (z listy, jest w bazie)
- [ ] **Data wstawienia** — kiedy hodowca odebrał pisklęta (dzień 0)
- [ ] **Liczba sztuk** — ile piskląt wstawił (typowo 25 000 – 40 000)
- [ ] **Dowód** — SMS, mail albo screenshot WhatsApp (do instrukcji #3)

---

## Kroki

**1. W menu głównym kliknij kafelek "🐣 Cykle Wstawień" (kategoria ZAOPATRZENIE I ZAKUPY, druga pozycja od góry).**
[SCREEN: menu główne z zaznaczonym kafelkiem "Cykle Wstawień" w zielonej kategorii]
Otworzy się okno z listą wszystkich aktywnych wstawień.

**2. Sprawdź najpierw czy to wstawienie nie jest już wpisane.** W pasku górnym widzisz pole "🔍" — wpisz nazwisko hodowcy. Jeśli pojawia się ten sam dostawca z taką samą datą — **nie dodawaj drugi raz**, otwórz istniejący przez dwuklik (to instrukcja #3).
[SCREEN: górny pasek z polem 🔍 i wpisanym przykładowo "Kowalski", lista przefiltrowana]
Możesz też zaznaczyć "📅 Tylko przyszłe" (zielony chip) żeby zobaczyć tylko aktywne cykle.

**3. Kliknij zielony przycisk "➕ Dodaj" w prawym górnym rogu listy (skrót: Ctrl+N).**
[SCREEN: przycisk "➕ Dodaj" zaznaczony strzałką, obok skrótów klawiszowych "Ctrl+N Dodaj | Del Usuń | Ctrl+F Szukaj | F5 Odśwież"]
Otworzy się okno **"🐔 Wstawienia Kurczaków"**. U góry zobaczysz "Tryb: Nowe" na zielono — to się musi zgadzać.

**4. W górnym pasku okna znajdź listę "👨‍🌾" obok ikony rolnika — to wybór dostawcy. Kliknij i zacznij wpisywać nazwisko, lista się przefiltruje.** Wybierz właściwego hodowcę.
[SCREEN: górny pasek z ComboBox dostawcy otwartym i podświetlonym jednym z hodowców z listy]
Jeśli hodowcy nie ma na liście — **nie dodawaj nowego sama**. Zadzwoń do Sera/Asi, ktoś musi go najpierw założyć w Bazie Hodowców.

**5. W białej karcie poniżej (środek okna) wypełnij dwa kluczowe pola:**
- **📅 Data** — kliknij i wybierz datę gdy hodowca odebrał pisklęta (dzień 0).
- **🐣 Sztuk** — wpisz liczbę piskląt (zielono podświetlone pole).
[SCREEN: środkowa karta "JEDNO WSTAWIENIE" z polami 📅 Data, 🐣 Sztuk wypełnione, oraz read-only 📊 Po 3% upadku, 📈 Suma, ✅ Różnica]
Pozostałe trzy pola (Po 3% upadku, Suma, Różnica) **liczą się same** — nie tykaj ich.

**6. Niżej zobaczysz kartę "🚚 Szablon Dostaw" — system sam zaplanował kiedy odbierzemy żywiec.** Jeśli wszystko wygląda OK (data ok. 35-42 dni po wstawieniu) — **nie zmieniaj nic**. Jeśli hodowca podał inne ustalenia (np. odbieramy w 2 turach, inne dni) — kliknij "➕ Dostawa" żeby dodać lub edytuj istniejące wiersze.
[SCREEN: karta "Szablon Dostaw" z listą zaplanowanych dostaw, kolumny # | Doba | Data | Dni | Waga | Szt/poj | Mnóż | Sztuki | Auta | Auto Wyl.]
**Magda — to jest miejsce gdzie Tereska Ci pomoże w pierwszych dniach.** Domyślnie 264 sztuki na paletę × ilość palet × auta = sztuki.

**7. Na dole w żółtym pasku jest pole "📝 Notatki" — wpisz dowód potwierdzenia.** Przykład: *"Hodowca potwierdził SMS-em 24.05 o 14:10"* albo *"Mail z Outlook, temat 'Wstawienie 24.05'"*. To zostaje w systemie.
[SCREEN: dolny pasek na żółto z polem 📝 Notatki i przykładową notatką "Hodowca potwierdził SMS-em 24.05 o 14:10"]

**8. Kliknij zielony przycisk "💾 Zapisz" w prawym dolnym rogu.**
[SCREEN: zielony przycisk "💾 Zapisz" zaznaczony strzałką]
Okno zamknie się, na liście pojawi się Twoje wstawienie + zobaczysz krótki toast "Sukces".

---

## ⚠️ Najczęstsze problemy

- **"Hodowca nie ma na liście dostawców"** → Nie dodawaj sama. Dzwoń do Asi (zakłada nowego w `DostawcyCR` jako wniosek o zmianę).
- **"Wpisałam 100 000 sztuk i system się nie zatrzymał"** → System dziś **nie waliduje liczby sztuk** — wpisałaś za dużo, sprawdź dwa razy zanim klikniesz Zapisz. Typowy kurnik: 25-40 tys.
- **"Szablon Dostaw pokazuje dziwne daty"** → Sprawdź pole "📅 Data" w karcie wyżej — czy data wstawienia jest poprawna. System liczy "data + 35-42 dni".
- **"Zapisałam i widzę dwa wstawienia tego samego hodowcy"** → Otwórz to drugie przez prawy klik → "🗑️ Usuń wstawienie" (potwierdź). Albo dzwoń do Sera jeśli się boisz.
- **"Po Zapisz okno nie zamknęło się"** → Pewnie brakuje obowiązkowego pola. Sprawdź czy dostawca i data są wybrane, czy sztuki > 0.
- **"Pomyliłam datę po Zapisz"** → Wróć do listy, kliknij na wstawienie prawym → "📅 Zmień datę wstawienia". Albo: prawy → "✏️ Edytuj wstawienie" → otwiera całe okno do poprawienia.

---

## 📞 Do kogo dzwonić

| Problem | Osoba |
|---|---|
| Hodowca nie ma na liście | **Asia** (zakłada wniosek) |
| Nie wiem ile sztuk wpisać / "Szablon Dostaw" mnie przeraża | **Tereska** (pierwsze 2-3 wstawienia z Tobą) |
| Wpisałam za dużo / źle / nie idzie zapisać | **Tereska** lub **Ser** |
| ZPSP się powiesił / okno zamarło | **Ser** |
| Hodowca dzwoni i nie wiem czy to nowe czy edycja | **Tereska** (kontekst hodowcy) |

---

## ✅ Skąd wiesz że zrobiłaś dobrze

- Na liście "📋 Lista Wstawień" pojawił się **nowy wiersz z nazwiskiem dostawcy i datą** którą wpisałaś.
- Pokazał się **zielony toast** "Sukces" w dolnym prawym rogu (znika po ~3 sek).
- Otwierając w kalendarzu dostaw (instrukcja #4) zobaczysz **zaplanowane dostawy** za 35-42 dni.

---

## 🔧 Mała ściągawka — pamiętaj

| Pole | Co znaczy | Skąd brać |
|---|---|---|
| **Dostawca** | Hodowca który odbiera pisklęta | Z listy (już w bazie) |
| **Data wstawienia** | Dzień 0 — kiedy pisklęta przyjechały do hodowcy | Hodowca w telefonie |
| **Sztuk** | Liczba piskląt wstawionych do kurnika | Hodowca (faktura z wylęgarni) |
| **Po 3% upadku** | Ile sztuk zostanie po naturalnym upadku piskląt | System wyliczy |
| **Szablon Dostaw** | Plan odbiorów żywca (gdzie/kiedy/ile aut) | System proponuje, Ty korygujesz |
| **Notatki** | Dowód że hodowca potwierdził | **OBOWIĄZKOWE** — Asia patrzy pod ARiMR |

---

## 🔗 Następny krok

Po wstawieniu — **idź do instrukcji #3** żeby załączyć dowód potwierdzenia (SMS / mail / WhatsApp screenshot). Pole "Notatki" to **minimum** — ARiMR może zapytać o pełny dowód.

> *Planowane:* okno "Załącz potwierdzenie" z możliwością wrzucenia screenshota SMS/maila bezpośrednio do wstawienia. Ser to dorobi w czerwcu (Część 2 audytu, punkt A.1).
