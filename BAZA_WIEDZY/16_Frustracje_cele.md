# 16 — Frustracje Sergiusza i wizja idealnego ZPSP

**Najważniejszy plik dla agenta** — bo to **język samego Sergiusza** (z PYTANIA_PRODUKCJA, voice-to-text). Te frustracje to lista tego, co realnie chciałby zmienić. Każda zmiana w ZPSP powinna być oceniana przez pryzmat: *"Czy to redukuje którąś z tych frustracji?"*

---

## Frustracje (cytaty Sergiusza, scena 15)

### Top 3 osobiste frustracje Sergiusza

1. **Kierownik zakładu (Justyna) nie jest tak zaangażowana jak myślałem**
   - Powinna chodzić po hali, sprawdzać kierowników, usprawniać procesy
   - W praktyce: zerka w kamery, nie wchodzi głębiej

2. **Partie kurczaka wychodzące z magazynu nie są rozliczane**
   - Magazynier wpisuje numery partii na papierowej WZ
   - Ilości "na oko" — brak prawdziwego rozliczenia

3. **Inwentaryzacje co 3 miesiące — "zawsze mówią że to przez produkcję"**
   - Stan ZPSP odbiega od fizycznego
   - Brak weryfikacji częstszej

### Frustracje technologiczne

4. **Brak skanerów RFID / kodów kreskowych**
   - Wszystko ręcznie
   - Skala 200t/dzień + 100+ klientów + 70 partii dziennie = niemożliwe ręcznie

5. **Brak czytników temperatury w czasie rzeczywistym**
   - Justyna mierzy 5x dziennie
   - Między pomiarami "ślepa plama"
   - Mroźnia ma -18 °C cel — gdyby skoczyło do -10 nikt nie zauważy zaraz

6. **Nie mogę obliczyć wydajności pracowników brudnej i czystej strefy**
   - Brak indywidualnych KPI
   - Nie wiadomo kto najlepiej / najgorzej pracuje

7. **Nie potrafię powiedzieć ile pracowników jest mi potrzebnych**
   - Brak modelu prognozy zatrudnienia
   - Sezonowe (lato, święta) skoki = trudność

### Frustracje organizacyjne

8. **Chciałbym aby kierownicy robili więcej spotkań beze mnie**
   - Sergiusz interweniuje w mikro-decyzje
   - Plan: Teresa + Justyna prowadzą spotkania 13:00

9. **Zamrażamy towar którego nie możemy sprzedać bo ciężko przewidzieć ile ostatecznie będzie towaru na koniec dnia**
   - Bilans dnia trudny do ustalenia w czasie rzeczywistym
   - Decyzja krojenie/mrożenie ad hoc

10. **Brak informacji o stanach rzeczywistych w magazynach na bieżąco**
    - Stany w ZPSP opóźnione (etykiety → ręczny wpis)
    - Bieżąca wiedza tylko fizycznie na hali

---

## Frustracje innych (w/g Sergiusza, scena 15)

### Teresa Jachymczak (sprzedaż + zakupy nieformalnie)
- Obciążenie: 2 działy nieformalnie
- Cel: awans formalny → Dyrektor Handlowy

### Justyna Chrostowska (jakość + de facto Dyr. Zakładu)
- *"Dobra ale oczekiwałem od niej więcej"*
- Sergiusz oczekuje **głębszego zaangażowania w operację**

### Pani Jola (handlowiec)
- *"Nie potrafi pracować w grupie bo pracowała 30 lat sama, ale dobrze sprzedaje"*
- Akceptacja: Jola zostaje, ale praca przez Anię jako pośrednika

### Łukasz Collins (kier. uboju brudnej)
- *"Dobrze obsługuje maszyny i produkcję brudną, ale nie mam nad nim kontroli i analiz"*
- Czyli: maszyny tak, KPI nie

---

## Idealna wizja Sergiusza (scena 16)

### 5:00 rano — pierwszy widok w ZPSP

> *"KTO jest na hali z pracowników, w której hali co robią. Ile mamy towaru. Ile mamy pojemników. Jaką mamy temperaturę. Jaki mamy dokładny towar z każdej partii i jaki rozmiar tuszki i z jakiej produkcji towar."*

**Tłumaczenie na komponenty:**
1. **Lista pracowników na hali** (z UNICARD): kto, w której strefie, od której godziny
2. **Stan magazynów per produkt** — kg per filet/ćwiartka/korpus/skrzydło/tuszka
3. **Liczba pojemników E2** (suma w użyciu + magazyn)
4. **Temperatury** wszystkich komór (3 mroźnie + chłodnia + szokówka + hala)
5. **Lista partii** dziś:
   - Numer partii
   - Hodowca
   - Sztuki
   - **Klasy wagowe** (% rozmiaru 6/7/8/9/10/11)
   - Z której produkcji (zmiana A/B)

### 9:00 idziesz na halę — widok na tablecie

(Brak konkretnej odpowiedzi — Sergiusz przerwał)

**Pomysł agenta (do potwierdzenia z Sergiuszem):**
- Tempo linii (sztuk/h aktualne vs plan)
- Klasy A/B per partia bieżąca (ranking hodowców na żywo)
- Alerty (% B rośnie, temperatura skoczyła, awaria linii)

### 19:00 wracasz do domu — laptop?

**Pomysł:**
- Aplikacja mailem podsumowuje dzień (5 KPI)
- Sergiusz czyta na telefonie 2 minuty
- Klika ✓ "OK" lub eskaluje konkretną sprawę

---

## Mechanizm "Zgłoś frustrację" (pomysł Sergiusza)

> Każde okno ZPSP ma ikonkę 🤬 "Zgłoś frustrację":
> - Pracownik klika → opisuje co się nie podoba
> - Tygodniowy raport dla Sergiusza
> - Filtrowane: per okno, per pracownik, per dzień

**Sergiusz reakcja:** "OK".

**Status:** Pomysł, nie zaimplementowany.

---

## Lista konkretnych okien które Sergiusz chciałby mieć

(Z PYTANIA_PRODUKCJA, sceny 1-16)

| # | Nazwa okna | Status | Priorytet |
|---|---|---|---|
| 1 | **Start dnia — Kierownik Uboju** (tablet brudna strefa) | Brak | Wysoki |
| 2 | **Hala LIVE** (monitor nad halą) | Brak | Wysoki |
| 3 | **Tablet klasyfikatora A/B** (czysta strefa) | Brak | Średni (czeka na WAGO) |
| 4 | **Rozbiór dnia** (monitor w pomieszczeniu krojenia) | Brak | Wysoki |
| 5 | **Magazyn świeżych — stan LIVE** (rampa) | Brak | Wysoki |
| 6 | **Rampa — Magazynier** (tablet wodoszczelny) | Brak | Wysoki |
| 7 | **Mroźnia — Kierownik mroźni** (Janek) | Brak | Średni |
| 8 | **Zamówienie — workflow** (status pipe) | Częściowo | Średni |
| 9 | **Partia — pełen cykl** (traceability) | Częściowo (Lista Partii V2) | Wysoki |
| 10 | **Spotkanie 13:00** (kalkulator decyzji) | Brak (są spotkania!) | Średni |
| 11 | **Zmiana B — Kierownik II Zmiany** | Brak | Niski |
| 12 | **System alertów Teams** | Brak | Średni |
| 13 | **Tryb awaryjny** (incydent → checklist) | Brak | Niski |
| 14 | **Cockpit Idealny** (1 ekran 4K) | Brak | Wizja |

**Reakcja Sergiusza na każdą propozycję:** *"Można spróbować. Najwyżej poprawimy."* (czyli zielone światło, ale bez sztywnych zobowiązań).

---

## Pomysły Sergiusza co do roli Pani Joli (scena 13)

> *"Czytają (WhatsApp) oprócz Pani Joli. Ania jest pośrednikiem Pani Joli. Czyta wiadomości i wpisuje Pani Joli zamówienia."*

**Akceptacja stanu obecnego:** Jola pisze jak chce, Ania pośredniczy. Nie wymuszać przejścia na ZPSP **dla Joli**.

**Implikacja:** Architektura **musi obsługiwać oba tryby**:
- ZPSP-first (Maja, Ania, Teresa, Radek)
- Karteczka → Ania → ZPSP (Jola)

---

## Pomysły dla Sergiusza — gdzie ZPSP daje **przewagę** vs konkurencja

1. **Ranking hodowców per partia** (% klasy B + reklamacje)
   - Konkurencja nie ma — wszyscy patrzą tylko na ilość/cenę
2. **Real-time bilans** (przychód + stany - zamówienia + bufor)
   - Konkurencja decyduje "na oko" — my mamy dane
3. **Prognoza zatrudnienia** na bazie planowanej produkcji
   - Konkurencja = ile na zmianie etatowych. Nas — model: 1 osoba na X kg/h
4. **Mobile dla Pani Joli** (mimo wszystko) — Ania wprowadza, Jola tylko zatwierdza ✓
5. **Auto-alert do hodowców** *"Twoja ostatnia partia 22% klasy B — rozmowa"*

---

## Cytat Sergiusza — co go napędza

> *"Wkurza mnie [długa lista]. Chcę żeby ZPSP rozwiązywał te rzeczy. Każde nowe okno = mniejsza frustracja."*

**Zasada agenta:** Zanim zaproponujesz cokolwiek nowego — **sprawdź, którą z 10 frustracji powyżej rozwiązuje**. Jeśli żadnej, odłóż pomysł.

---

## Cytat Sergiusza — co go nie wkurza (akcepuje)

- Sergiusz **akceptuje** że Pani Jola jest jak jest (akceptacja faktu)
- Sergiusz **akceptuje** że ZPSP jest niedoskonały (sam pisze, sam zmienia)
- Sergiusz **akceptuje** że Mercosur i HPAI są ryzykami (przygotowuje się)

**Lekcja:** Nie wszystko musi być rozwiązane. Niektóre rzeczy = po prostu fakty życia firmy.
