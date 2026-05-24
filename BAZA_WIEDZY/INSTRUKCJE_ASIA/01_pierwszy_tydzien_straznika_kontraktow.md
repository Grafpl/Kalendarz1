# Asia — pierwszy tydzień jako strażnik kontraktów

> Asia, nowa rola: **"kręgosłup działu zakupu"** — strażnik kontraktów hodowców. Ta instrukcja prowadzi Cię przez pierwsze 5 dni. Czytaj na bieżąco, odhacz co zrobione.

---

## Twój kontekst (czego się od Ciebie oczekuje)

- **Strażnik kontraktów** — wszystkie umowy z hodowcami przechodzą przez Ciebie
- **Rozliczenia zakupowe i sprzedażowe** — przejmujesz od Marleny
- **GUS R09** — raz w miesiącu do 8. dnia, **Sergiusz obiecał "jeden klik"** (Część 3 audytu)
- **W przyszłości:** monitoring wydajności żywca per hodowca
- **Magda od poniedziałku** — będzie się Ciebie pytać 5× dziennie. To jest OK, pomagaj.
- **Nie łapiesz za telefon do hodowców** — to robi Tereska (na razie) → Magda (w średnim terminie)
- **Jeździsz z Serem do prawniczki** — sprawy umów

---

## ⏱ PONIEDZIAŁEK 26.05 — wprowadzenie Magdy

### Rano (8:00–10:00)
- [ ] **Bądź obok Magdy przy pierwszym logowaniu** (8:30) — Ser to zrobi, Ty obserwujesz
- [ ] **Pierwsza kawa z Magdą** — pomóż przełamać stres "to ja, jestem Asia, dzwoń do mnie ZAWSZE jak masz wątpliwość ws. cen / umów / księgowości"

### Lunch (12:00–13:00)
- [ ] **Zjedź obiad z Magdą** — pierwsza okazja na konwersację bez stresu
- [ ] **Powiedz jej:** "W piątek razem zrobimy ZSRIR. Nie martw się, pokażę krok po kroku."

### Po południu (14:00–16:00)
- [ ] **Otwórz "📜 Kontrakty Hodowców"** — sprawdź czy moduł jest wdrożony (jeśli Ser zrobił Fazę 1 w weekend) lub czy nadal jest tylko stary "📑 Dokumenty i Umowy"
- [ ] **Wnioski o Zmiany** — sprawdź czy są pending od poprzedniego tygodnia. Zatwierdź / odrzuć.
- [ ] **Płatności** — przejrzyj listę zaległych, zaplanuj przelewy na wtorek-środę

### Wieczorem
- [ ] **15 min ze Sergiuszem** — debrief dnia, omów ewentualne problemy Magdy

---

## ⏱ WTOREK 27.05 — start nowej rutyny

### Rano (9:00)
- [ ] **Otwórz Centrum Asi** (jeśli wdrożone z Części 3) lub manual przegląd:
  - Wnioski o Zmiany (sprawdź pending)
  - Wstawienia bez potwierdzenia po 24h
  - Płatności zaległe > 7 dni
  - Najnowsze maile od hodowców

### W ciągu dnia
- [ ] **Bądź dostępna telefonicznie dla Magdy** (cytat Sera: "Magda będzie się stresować, odpowiadaj cierpliwie")
- [ ] Przelewy na hodowców (zgodnie z terminami umów)
- [ ] Faktury kosztowe (paszy, piskląt) jeśli przyszły

### Po południu
- [ ] **Z Magdą: instrukcja #11** (Wnioski o Zmiany) — pokaż jej workflow zatwierdzania z Twojej strony

---

## ⏱ ŚRODA 28.05 — kontrakty kluczowy temat

### Rano
- [ ] **Z Sergiuszem: przegląd modułu Kontrakty Hodowców** (Część 4 audytu, plik `04_MODUL_KONTRAKTY_SPEC.md`)
- [ ] **Wspólnie z nim:**
  - Zaakceptuj 7 scenariuszy użycia (S1-S7)
  - Zaakceptuj schemat SQL (`SQL/01_Kontrakty_v1_schema.sql`)
  - Zaakceptuj plan 3 faz wdrożenia

### W ciągu dnia
- [ ] **Excel inwentaryzacji umów** — zacznij ten plik:
  ```
  | Hodowca | NIP | Data podpisania | Data do | Typ | % ubytku | Cena | Ścieżka skanu |
  ```
- [ ] **Faza M1** (Część 4 audytu) — przeglądasz segregatory + foldery sieciowe, wpisujesz do Excela
- [ ] **Cel:** do końca tygodnia lista 50-100 umów

### Po południu
- [ ] **Z Magdą:** sprawdź czy poradziła sobie sama z 2-3 wstawieniami

---

## ⏱ CZWARTEK 29.05 — przygotowanie do ZSRIR

### Rano
- [ ] **Sprawdź dane do ZSRIR** za bieżący tydzień:
  - Faktury żywca w HANDEL od pn do śr (czy Tereska wpisała?)
  - Harmonogram dostaw w LibraNet (czy się zgadza?)
  - Specyfikacje wystawione w tygodniu

### W ciągu dnia
- [ ] **Excel inwentaryzacji umów** — kontynuacja

### Po południu
- [ ] **Z Magdą: pokaż jej Sprawozdania ZSRIR** — instrukcja #15, **z wyprzedzeniem**, żeby w piątek nie była zaskoczona

---

## ⏱ PIĄTEK 30.05 — ZSRIR Z MAGDĄ

### Rano (9:00)
- [ ] **Powiedz Magdzie:** "Dziś o 14:00 robimy ZSRIR razem. Pierwszy raz pokażę całość, potem już Ty będziesz pomagać."

### 14:00 — ZSRIR z Magdą
- [ ] Otwórz kafelek "📊 Sprawozdania"
- [ ] **Krok po kroku z Magdą** (czytajcie razem instrukcję #15):
  - Wybór tygodnia
  - Sprawdzenie 3 źródeł (HANDEL, LibraNet, Specyfikacje)
  - Naprawa rozbieżności (jeśli są)
- [ ] Generuj tekst maila
- [ ] Wyślij z Outlooka

### 16:00 — koniec tygodnia
- [ ] Powiedz Magdzie: "Świetnie się trzymałaś. Następny piątek już sama spróbujesz, ja obok."
- [ ] Krótki email do Sera: "Magda OK po pierwszym tygodniu. Excel umów: 30/100 (kontynuacja przyszły tydzień)."

---

## 🎯 CELE NA PIERWSZY TYDZIEŃ (sprawdź w piątek wieczorem)

- [ ] Magda **3 dni samodzielnie wpisuje wstawienia** (#2)
- [ ] Magda **potwierdziła 5+ wstawień** sama (#3)
- [ ] Magda **wpisała 2+ wnioski o zmianę danych** i Ty je zatwierdziłaś (#11)
- [ ] **ZSRIR poszedł w piątek 16:00** (z Magdą)
- [ ] **30+ umów w Excelu** (pod migrację do `Kontrakty` w czerwcu)
- [ ] **Sergiusz dostał email** z podsumowaniem tygodnia

---

## 🚨 CZERWONE FLAGI — gdy widzisz, eskaluj do Sera

| Sygnał | Co to znaczy |
|---|---|
| Magda się nie pyta przez cały dzień | **Albo wie wszystko (mało prawdopodobne) albo się boi pytać** — proaktywnie zagadaj |
| Magda mówi "wszystko OK" ale wygląda zestresowana | Daj jej kawę, posiedź obok 15 min, niech opowie |
| Magda wpisała coś źle i ukryła | **Nie karz** — zachęć żeby zawsze meldowała. Sergiusz tak ustawił kulturę. |
| Hodowca dzwoni o coś dziwnego (groźba, agresja) | **Natychmiast Ty** + telefon do Sera |
| Compliance ARiMR widać że < 50% | **Natychmiast Ser + plan akcji** |
| Magda chce zrobić sama coś co wykracza poza jej kompetencje (nowy hodowca, zmiana ceny) | **Stop** — zrób z nią, wytłumacz dlaczego nie sama |

---

## 📞 Twoje telefony dyżurne

| Sytuacja | Kto Cię zastąpi (gdy jesteś nieobecna) |
|---|---|
| Wnioski o Zmiany | **Sergiusz** (chwilowo, do tygodnia) |
| Płatności hodowcom | **Sergiusz** lub przelej deadline'y na kolejny tydzień |
| ZSRIR (piątek 16:00) | **Sergiusz** (KONIECZNIE) |
| GUS R09 (8. dnia miesiąca) | **Sergiusz** (KONIECZNIE) |
| Pytania Magdy o księgowość | **Tereska** (lepiej) lub **Sergiusz** |

**Reguła:** jeśli wiesz że będziesz nieobecna > 1 dzień, **napisz to Sergiuszowi z wyprzedzeniem**.

---

## 🔧 Co masz dostępne (audyt Sera, maj 2026)

| Część audytu | Plik | Po co Tobie |
|---|---|---|
| **Część 3** — Centrum Asi | `BAZA_WIEDZY/AUDYT_ZAKUPY_2026_05_23/03_CENTRUM_ASI.md` | Twój przyszły kokpit, 8 widoków do wdrożenia |
| **Część 4** — Kontrakty Hodowców | `04_MODUL_KONTRAKTY_SPEC.md` | 13 dni roboczych Sera, deadline 01.08.2026 (sp. z o.o.) |
| **Część 5** — Quick wins | `05_QUICK_WINS_WEEKEND.md` | Co Ser zrobił w weekend dla Magdy |
| **Instrukcje Magdy** | `BAZA_WIEDZY/INSTRUKCJE_MAGDA/*` | 16 plików — czytaj te z którymi Magda potrzebuje pomocy |
| **SQL Kontrakty** | `AUDYT_ZAKUPY_2026_05_23/SQL/01_Kontrakty_v1_schema.sql` | Twoja akceptacja przed uruchomieniem |

---

## 💚 Słowo Sera (z komunikatu zespołowego)

> *"Asia jest właścicielką modułu Kontraktów od pierwszego dnia. Ja piszę kod, Ty decydujesz co działa biznesowo. Bez Twojej akceptacji nic nie idzie do produkcji."*

> *"Magda przechodzi przez Ciebie wszystkie pytania o ceny, umowy, hodowców. W pierwszym miesiącu odpowiadaj na każde pytanie z cierpliwością — Magda doceni, że nie ma głupich pytań."*

---

*Wersja 1.0 • 24.05.2026 • Twoja instrukcja Asia, aktualizujemy razem co tydzień*
