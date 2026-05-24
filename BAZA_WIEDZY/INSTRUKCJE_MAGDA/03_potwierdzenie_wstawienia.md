# 3. Potwierdzenie wstawienia po telefonie od hodowcy

**Kiedy tego używasz:** gdy hodowca dzwoni / pisze SMS / mail "potwierdzam wstawienie z dnia X" — przed tym wstawienie czeka u Ciebie jako "do potwierdzenia".
**Ile czasu zajmuje:** 30 sekund (gdy data się zgadza) lub 1-2 minuty (gdy trzeba zmienić datę).
**Wideo:** [link — Ser dorzuci nagranie]

> Magda — **bez potwierdzenia kalendarz dostaw nie wie czy planu się trzymamy**. Potwierdzenie jest też **dowodem pod kontrole ARiMR** (jest na liście "muszą być zarchiwizowane"). Zawsze zachowaj screenshot SMS-a / maila.

## Co musisz mieć przed startem
- [ ] Wstawienie **już wpisane** w systemie (instrukcja #2)
- [ ] **Dowód** — SMS / mail / WhatsApp screenshot (na Twoim telefonie albo Outlook)
- [ ] Pewność że **data się zgadza** (jeśli nie — zobacz krok 5)

---

## Kroki

**1. W menu głównym otwórz "🐣 Cykle Wstawień". W oknie głównym po lewej stronie zobaczysz dużą listę "📋 Lista Wstawień", a po prawej węższy panel "⏰ Przypomnienia" z licznikiem "🔔 X" (czerwony) — to liczba wstawień do potwierdzenia.**
[SCREEN: główne okno Cykli Wstawień z zaznaczonym czerwonym licznikiem 🔔 i pasującym panelem "do potwierdzenia"]

**2. W panelu "do potwierdzenia" znajdź wstawienie hodowcy który właśnie zadzwonił. Kliknij **prawym** klawiszem na wiersz.**
[SCREEN: prawy klik na wierszu w gridzie "do potwierdzenia", widoczne menu kontekstowe z opcjami "✅ Potwierdź wstawienie", "📅 Potwierdź i zmień datę", "➕ Dodaj numer hodowcy"]

**3. Wybierz opcję zależnie od sytuacji:**
- **Hodowca potwierdza dokładnie tę datę co wpisałaś** → kliknij **"✅ Potwierdź wstawienie"** → pojawi się okienko "Czy na pewno chcesz potwierdzić to wstawienie?" → kliknij **"Tak"**.
- **Hodowca podaje inną datę** (np. wpisałaś 24.05 ale faktycznie wstawili 25.05) → kliknij **"📅 Potwierdź i zmień datę"** → otworzy się dialog z polem daty → poprawiasz datę → klik **OK**.

**4. Wpisz dowód w notatce wstawienia. Zamknij menu kontekstowe, znajdź to samo wstawienie na głównej liście "📋 Lista Wstawień" (po lewej) → kliknij prawym → "✏️ Edytuj wstawienie".** Otworzy się okno wstawienia. Na dole, w żółtym pasku, jest pole **"📝 Notatki"**. Dopisz **datę i sposób potwierdzenia**, np.:
```
Potwierdzenie SMS 24.05 14:10. Screenshot: Potwierdzenia_2026/KOWALSKI_2026-05-24.png
```
[SCREEN: pole "📝 Notatki" w dolnym pasku z przykładową notatką]
Kliknij **"💾 Zapisz"**.

**5. Zarchiwizuj screenshot dowodu w folderze sieciowym.** Otwórz Eksplorator Windows, idź do `\\192.168.0.170\Public\Potwierdzenia_Wstawien\2026\`. Wrzuć screenshot SMS-a / kopia maila / WhatsApp screenshot. **Nazewnictwo:** `NAZWA_HODOWCY_yyyy-MM-dd.png` (przykład: `KOWALSKI_2026-05-24.png`).
[SCREEN: okno Eksploratora Windows otwarte na folderze sieciowym Potwierdzenia_Wstawien\2026 z przykładowymi plikami]

**6. Wstawienie znika z panelu "do potwierdzenia". W liczniku "🔔" zobaczysz o 1 mniej.**

---

## ⚠️ Najczęstsze problemy

- **Hodowca podaje datę inną niż wstawiona "o tydzień wcześniej"** → To częste. Użyj "📅 Potwierdź i zmień datę". Jeśli różnica > 7 dni — dzwoń do Tereski/Sera, może to wstawienie do **innego** cyklu/innej partii.
- **Hodowca mówi "nie wstawialiśmy w ogóle"** → Zostaw wstawienie nie potwierdzone, **dzwoń do Tereski**. Możliwe że ktoś źle wpisał (np. zamiana hodowców o podobnych nazwiskach).
- **Nie ma kogo potwierdzać — lista pusta** → Super, **nie szukaj problemu** ;). Sprawdź licznik "🔔" — jeśli 0, jesteś na bieżąco.
- **Już potwierdziłam ale chcę cofnąć** → Na liście głównej "📋 Lista Wstawień" znajdź wstawienie → prawy klik → **"↩️ Cofnij potwierdzenie"** (pomarańczowa opcja).
- **Folder sieciowy nie otwiera się** → VPN / sieć nie działa. Zapisz screenshot tymczasowo na pulpicie, wrzuć później (do końca dnia).
- **System nie waliduje czy notatka jest pusta** — możesz zapisać bez notatki. **Nie rób tego.** ARiMR potem nie pomoże.

---

## 📞 Do kogo dzwonić

| Problem | Osoba |
|---|---|
| Hodowca twierdzi że nie wstawiał | **Tereska** (kontekst hodowcy) |
| Data hodowcy różni się o tydzień + | **Tereska** lub **Ser** |
| Folder sieciowy nie otwiera się | **Edyta** (IT), tymczasowo zapisz na pulpicie |
| Cofnęłam potwierdzenie ale zniknęło z panelu i nie wraca | **Ser** |
| ARiMR pyta o dowód którego nie mam | **Asia** (jako pierwsza) → potem Ser |

---

## ✅ Skąd wiesz że zrobiłaś dobrze

- Wstawienie **zniknęło z panelu "do potwierdzenia"** po prawej.
- Licznik "🔔" zmniejszył się o 1.
- Otwierając wstawienie (na liście głównej, prawy → edytuj) widzisz **swoją notatkę w polu "Notatki"**.
- W folderze `\\192.168.0.170\Public\Potwierdzenia_Wstawien\2026\` **leży plik z prawidłową nazwą**.

---

## 🔧 Mała ściągawka — przy każdym potwierdzeniu

**3 rzeczy które MUSZĄ się zgadzać:**
1. **Hodowca = ten z telefonu** (nie zamień przez podobne nazwisko)
2. **Data = ta którą hodowca podaje** (jeśli inna — zmień)
3. **Notatka + screenshot** (bez tego nie ma dowodu pod ARiMR)

---

## 🔧 Czego ZPSP NIE obsługuje (dziś)

> **[BRAK W ZPSP — DO DODANIA]** Nie ma flow "załącz screenshot SMS-a bezpośrednio do wstawienia" — załącznik trzeba ręcznie kopiować do folderu sieciowego. Notatka tekstowa to wszystko co ZPSP zapisuje.
>
> *Workaround na teraz:* folder sieciowy `\\192.168.0.170\Public\Potwierdzenia_Wstawien\` + dyscyplina nazewnictwa `HODOWCA_yyyy-MM-dd.png`. Powiedz Serowi gdy zaczyna się robić bałagan.
>
> *Planowane:* przycisk "📎 Załącz screenshot" w oknie wstawienia, automatyczne nazewnictwo + indeks. Ser dorobi w czerwcu (Część 2 audytu, punkt A.1.6).
