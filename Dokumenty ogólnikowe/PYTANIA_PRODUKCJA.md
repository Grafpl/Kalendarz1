# PYTANIA PRODUKCYJNE — uporządkowane

> **Format:** Każda scena ma 3-4 proste pytania. Ty opowiadasz (voice-to-text), ja proponuję okno ZPSP. Ty mówisz „tak / nie / inaczej".
>
> **Twoje wcześniejsze odpowiedzi (sceny 1-4)** zostały uporządkowane w punkty — sprawdź czy zgadza się z tym co miałeś na myśli.
>
> **Sceny 5-16 są puste** — wypełniaj po kolei, dziennie po jednej-dwóch.

---

## SPIS SCEN

| # | Scena | Status |
|---|---|---|
| 1 | Świt — 3:30 do 6:00 | ✅ Odpowiedziane |
| 2 | Główna zmiana A — 6:00 do 13:30 | ✅ Odpowiedziane |
| 3 | Klasyfikacja A vs B | ✅ Odpowiedziane |
| 4 | Rozbiór i krojenie | ⚠️ Częściowo (czeka na szablon stanowisk) |
| 5 | Magazyn świeżych 65554 | ⏳ Do opowiedzenia |
| 6 | Rampa załadunkowa 65556 | ⏳ |
| 7 | Mroźnia | ⏳ |
| 8 | Anatomia jednego zamówienia | ⏳ |
| 9 | Anatomia jednej partii | ⏳ |
| 10 | Spotkanie 13:00 | ⏳ |
| 11 | Zmiana B — 14:00 do 21:00 | ⏳ |
| 12 | Stanowiska — kto na czym pracuje | ⏳ → szablon w `STANOWISKA_PRODUKCJI.md` |
| 13 | Komunikacja między działami | ⏳ |
| 14 | Sytuacje awaryjne | ⏳ |
| 15 | Frustracje codzienne | ⏳ |
| 16 | Idealna wizja | ⏳ |

---

## 🌅 SCENA 1 — ŚWIT: 3:30-6:00

### Pytania:
1. **Kto pierwszy w zakładzie i o której?**
2. **Od czego zaczyna pracę?**
3. **Skąd wie ile dziś ubijać?**
4. **Co z padłymi w transporcie?**

### ✅ Twoja odpowiedź (uporządkowana):

- **Pierwszy w zakładzie:** **Łukasz Collins** — dyrektor techniczny + kierownik uboju brudnej. Przygotowuje ubój.
- **Godzina startu:** **3:30** standardowo. **Latem 2:30** — bo upały, ludzie nie wytrzymaliby pracy w południe.
- **Pierwsza partia żywca:** ~3:00. **AVILOG** precyzyjnie planuje — samochód jeden po drugim, bez przerwy. Kurczaki **nie stoją na placu**.
- **Padłe w transporcie:** pracownik podczas rozładunku/zawieszania widzi martwego kurczaka → odkłada do **osobnego kontenera** → firma utylizująca odbiera.

### 💻 Moja propozycja okna ZPSP:

**„Start dnia — Kierownik Uboju"** (tablet 7", duże przyciski):
- Lewa strona: **lista aut żywca dziś** (z `HarmonogramDostaw`) — kolejność, hodowca, sztuki, waga
- Prawa: **plan produkcji** — automat: żywiec × 78% = tuszka, podział A/B/krojenie
- Dół: 3 duże przyciski: **„Linia START"** / **„Padłe: + N szt"** / **„Awaria — STOP"**
- Auto-log godzin (UNICARD)

### ✅ Twoja reakcja:
> **„Można spróbować tak zrobić jak najbardziej."**

---

## ☀️ SCENA 2 — GŁÓWNA ZMIANA A: 6:00-13:30

### Pytania:
1. **Kto czym zarządza?** (Kierownik Uboju, Kierownik Rozbioru, Anna Majczak)
2. **Co robi Justyna podczas zmiany?**
3. **Najczęstszy powód zatrzymania linii?**

### ✅ Twoja odpowiedź:

- **Łukasz Collins** = dyrektor techniczny + kierownik uboju brudnej (kurczak patroszony).
- **Kierownik Rozbioru** — pilnuje:
  - godzin pracowników
  - realizacji zamówień
  - zarządzania ludźmi
  - rozliczania produkcji
  - pojemników z folią
- **Anna Majczak** — niejasne. Prawdopodobnie wspiera kierownika produkcji.
- **Justyna (dyrektor zakładu)** — **zerka w kamery, ale nie chodzi po hali wystarczająco.** Powinna obserwować kamery, chodzić po zakładzie, usprawniać procesy, sprawdzać kierowników. Nie do końca to robi. **(Pain point Sergiusza.)**
- **Linia zatrzymuje się głównie z powodu awarii.**

### ⚠️ KLUCZOWE: 2 programy poza ZPSP do zintegrowania
Sergiusz wspomniał dwa zewnętrzne programy które są krytyczne dla pomiarów:

1. **Program WAGI SELEKTYWNEJ:**
   - Sprawdza wagę kurczaka
   - Decyduje gdzie wybić (klasa wagowa 6/7/8/9...)
   - Jeśli odhaczona klasyfikacja wzrokowa B → wybicie do rozbieralni
   - **PROBLEM:** brak dostępu programistycznie
   - **POTENCJAŁ:** wiedzieć **realny % klasy A vs B per hodowca** — kluczowy KPI efektywności hodowcy
   - **AKCJA:** Sergiusz prosi o dostęp od dostawcy

2. **Program LICZNIKA TUSZEK:**
   - Liczy tuszki przechodzące przez linię
   - **PROBLEM:** brak dostępu
   - **POTENCJAŁ:** real-time tempo linii bez ręcznego wpisywania

### 💻 Moja propozycja okna ZPSP:

**„Hala LIVE"** — wielki monitor nad halą + tablet u Justyny:
- Centralny licznik **„Sztuk: 23 450 / 60 000"** + tempo bieżące
- Pasek postępu zmiany A (zielono/żółto/czerwono)
- Mini-wykres ostatnich 60 min (kiedy linia stała)
- **Po integracji wagi selektywnej:** % klasy A vs B per hodowca dnia
- Dla Justyny: ikonka kamer Hikvision (klika → live)

### Twoja reakcja:
> **„Dwa programy zewnętrzne — waga selektywna i licznik tuszek — muszę uzyskać do nich dostęp od dostawcy. To pozwoli sprawdzać efektywność każdego hodowcy. Na razie wszystko mam w Excelu, chcę żeby to było w bazie danych."**

**(Akcja dla Sergiusza:** poproś dostawców wagi selektywnej i licznika tuszek o dostęp do bazy / API. Dopóki nie ma — % klasy A vs B z hodowcy mierzymy ręcznie albo z modułu klasyfikacji w ZPSP.)**

---

## ⚖️ SCENA 3 — KLASYFIKACJA A vs B

### Pytania:
1. **Kto klasyfikuje i ile czasu na sztukę?**
2. **Co fizycznie robi z tuszką B?**
3. **Kto sprawdza klasyfikację (jakość)?**

### ✅ Twoja odpowiedź:

**Workflow klasyfikacji:**
1. Tuszka schodzi z **chłodzenia** → wchodzi na **produkcję czystą**
2. Tam **4-6 osób** wybija kurczaka na wannę
3. Z wanny przestawiają na zawieszanie (gdzie jest waga)
4. **Decyzja klasy wagowej** — system kieruje do odpowiedniego korytarza (15 kg netto pojemnik)
5. **Klasyfikacja wzrokowa A/B** — pracownik patrzy 1-2 sekundy
6. **Wszystkie wady** są klasyfikowane jako B (krwiak, złamania, żółć, czerwony filet, oparzenia, otwarte rany)
7. Jeśli B → **przesuwa wajchę w górę** na widelcu → program ważący wie że ma to nie uwzględniać → wybicie na końcu do **maszyny rozbierającej**

**Kontrola jakości:** Justyna i dział jakości — **brak procedur**, niejasne jak to robią.

### 💻 Moja propozycja okna ZPSP:

**Tablet przy klasyfikatorze** (wodoszczelny, duże przyciski):
- **A** — duży zielony przycisk
- **B** + powód: krwiak / złamanie / żółć / oparzenie / inne
- Auto-licznik dnia: A: 4 521 / B: 312 (z rozbiciem powodów)
- Alert godzinny: **„% klasy B rośnie 18% → 24% — sprawdź partię"**
- Ranking hodowców: kto dał najwięcej krwiaków/złamań w miesiącu

### ✅ Twoja reakcja:
> **„Można spróbować. Poprawi się najwyżej."**

---

## 🥩 SCENA 4 — ROZBIÓR I KROJENIE

### Pytania:
1. **Jak działa linia rozbioru?** (od tuszki B do filetu)
2. **Norma per pracownik?** (wydajność)
3. **Kto decyduje co produkować?** (mielone, polędwiczki, tuba)

### ⚠️ Twoja odpowiedź (częściowa — czeka na szablon stanowisk):

**Workflow rozbioru:**
1. **Korpus z filetem razem** wchodzą do maszyny
2. Maszyna rozdziela: **filet** + **korpus**
3. **Korpus → bezpośrednio do pojemnika**
4. **Filet → ręczne czyszczenie** (usuwanie balonów, zakrwawionych miejsc)

**Etykietowanie:** terminale na **wagach platformowych** — produkt zważony → drukuje etykieta.

**Wagi:** tuszka jest na **wadze paletowej**.

**Norma per pracownik:** Sergiusz mówi że może dać przez **SQL query z bazy**.

**Mielone, polędwiczki, tuba — kto decyduje:** **NIE WIE**, musi się dowiedzieć.

**Skrawki/odpady (skórki, kości):** **NIE WIE**, prosi przypomnienie.

**Czystość pojemników E2:** **NIE WIE kto sprawdza**, pyta czy powinno być sprawdzane.

### 📋 Akcja: szablon stanowisk
Sergiusz prosi szablon do wypełnienia w wolnych chwilach. **Stworzony plik:**
**`Dokumenty ogólnikowe/STANOWISKA_PRODUKCJI.md`** — wypełnij w poniedziałek na hali.

### 💻 Moja propozycja okna ZPSP:

**„Rozbiór dnia"** — duży monitor w pomieszczeniu krojenia:
- Lewa: plan dnia (Filet I: 5800 kg, Ćwiartka: 6600, Skrzydło: 1700, Korpus: 4500)
- Środek: postęp realny vs plan (paski kolorowe)
- Prawa: per-pracownik tempo (kg/h fileta z RFID przy wadze)
- Decyzja Dyrektora w trakcie zmiany ("więcej skrzydła") → broadcast na Teams

### ✅ Twoja reakcja:
> **„Tak."**

---

## 📦 SCENA 5 — MAGAZYN ŚWIEŻYCH 65554

### Pytania:
1. **Jak fizycznie wygląda magazyn?** (regały? palety? ile mieści?)
2. **Co się dzieje z towarem niesprzedanym?** (po ilu godzinach do mroźni)
3. **Kto sprawdza temperaturę i FIFO?**

> **Twoja opowieść (voice-to-text OK):**
> 
>
>
>

### 💻 Moja propozycja okna ZPSP:

**„Magazyn świeżych — stan LIVE"**:
- Mapa pomieszczenia 2D — gdzie palety stoją, kolor wieku (zielony 0-12h, żółty 12-24h, czerwony 24h+)
- Lista FIFO (najstarszy → najnowszy)
- Alert: „Paleta 234 jest 28h — wydaj jako pierwsza albo decyzja mrożenia"
- Saldo w czasie rzeczywistym: kg tuszki A / B / fileta / ćwiartki / korpusu

### Twoja reakcja:
> 

---

## 🚛 SCENA 6 — RAMPA ZAŁADUNKOWA 65556

### Pytania:
1. **Jak klient awizuje przyjazd?** (telefon, kamera, slot booking)
2. **Magazynier kompletuje wg czego?** (papier, ZPSP, lista)
3. **Co jeśli czegoś brakuje na rampie?**

> **Twoja opowieść:**
> 
>
>
>

### 💻 Moja propozycja okna ZPSP:

**„Rampa — Magazynier"** (tablet wodoszczelny przy rampie):
- Lista aut na dziś + status (wjeżdża / na rampie / wyjechał)
- Po wyborze klienta: pozycje do załadowania + checkbox „skompletowane"
- Brakuje? Klik „BRAK" + powód → auto-alert do handlowca
- Auto-WZ + plomba + podpis kierowcy na ekranie

### Twoja reakcja:
> 

---

## ❄️ SCENA 7 — MROŹNIA

### Pytania:
1. **Kto fizycznie przenosi towar do mroźni?**
2. **3 mroźnie + szokówka — co gdzie idzie?**
3. **Jak długo typowo towar leży w mroźni?**

> **Twoja opowieść:**
> 
>
>
>

### 💻 Moja propozycja okna ZPSP:

**„Mroźnia — kierownik mroźni"**:
- Mapa 3D komór: kolorowe sektory wieku (0-30 / 30-90 / 90-180 / >180 dni)
- „Do mrożenia dziś" (z 13:00 spotkania)
- „Do wydania jutro" — automat FIFO
- Alert: „Partia 25034 leży 270 dni — sprawdź"

### Twoja reakcja:
> 

---

## 📞 SCENA 8 — ANATOMIA JEDNEGO ZAMÓWIENIA

### Pytania:
1. **Jak Damak składa zamówienie i kto je wpisuje?**
2. **Kiedy informacja dochodzi do produkcji?**
3. **Kto wystawia fakturę i kiedy?**

> **Twoja opowieść (cały cykl, jeden konkretny przykład):**
> 
>
>
>

### 💻 Moja propozycja okna ZPSP:

**„Zamówienie — workflow"** — wszyscy widzą ten sam dokument:
- Status pipe: **Nowe → Potwierdzone → W produkcji → W magazynie → Załadowane → Faktura → Opłacone**
- Każdy etap: kto, kiedy, co zrobione
- Auto-alert do osób w każdym etapie (Teams)

### Twoja reakcja:
> 

---

## 🐣 SCENA 9 — ANATOMIA JEDNEJ PARTII

**Sytuacja:** 35 dni temu wstawiono pisklęta. Dziś ubój. Za 5 dni reklamacja "filet czerwony".

### Pytania:
1. **Czy partia ma śledzony cykl 35 dni hodowli w ZPSP?**
2. **Po reklamacji — czy łatwo znajdziecie hodowcę partii?**
3. **Czy hodowca dostaje feedback o jakości?**

> **Twoja opowieść:**
> 
>
>
>

### 💻 Moja propozycja okna ZPSP:

**„Partia — pełen cykl" (traceability)**:
- Reklamacja → automat: która partia → który hodowca → kiedy ubita → kiedy wydana
- Auto-update rankingu hodowcy (3 reklamacje → ranking spada)
- Alert do działu zakupów: „Stróżewski 3 reklamacje — rozmowa"

### Twoja reakcja:
> 

---

## 🕐 SCENA 10 — SPOTKANIE 13:00 (decyzja krojenie/mrożenie)

### Pytania:
1. **Gdzie i jak długo?**
2. **Kto decyduje?** (Sergiusz, Justyna, konsensus)
3. **Po decyzji — jak info dochodzi do hali?**

> **Twoja opowieść:**
> 
>
>
>

### 💻 Moja propozycja okna ZPSP:

**„Spotkanie 13:00"** — wspólne na monitorze (Teams ekran-share):
- Bilans dnia (produkcja vs sprzedaż)
- Kalkulator decyzji 14A (3 scenariusze: tuszka / krojenie / mrożenie)
- Po decyzji → broadcast Teams do wszystkich

### Twoja reakcja:
> 

---

## 🌆 SCENA 11 — ZMIANA B (14:00-21:00)

### Pytania:
1. **Czy zmiana B kontynuuje ubój czy tylko sprząta?**
2. **Kto jest kierownikiem II zmiany?**
3. **Załadunki popołudniowe (Bomafar, Publimar) — kto obsługuje?**

> **Twoja opowieść:**
> 
>
>
>

### 💻 Moja propozycja okna ZPSP:

**„Zmiana B — Kierownik II Zmiany"**:
- Lista zadań: sprzątanie, mycie pojemników, załadunki, inwentaryzacja
- Checklist: kto, kiedy, status
- Raport końca zmiany B: 5 punktów → auto-mail do Sergiusza+Justyny

### Twoja reakcja:
> 

---

## 👷 SCENA 12 — STANOWISKA NA HALI

### 📋 Akcja: Wypełnij w wolnym czasie szablon
**`STANOWISKA_PRODUKCJI.md`** — przygotowany dla Ciebie. Idziesz po hali, wypełniasz każde stanowisko (nazwa, ile osób, co robią).

> **Po wypełnieniu szablonu** — ja narysuję mapę hali i propozycje gdzie jakie tablety/ekrany.

---

## 💬 SCENA 13 — KOMUNIKACJA MIĘDZY DZIAŁAMI

### Pytania:
1. **Anulacja zamówienia 11:00 — jak info idzie do produkcji?**
2. **Awaria patroszarki — kto kogo woła?**
3. **WhatsApp grupy — czy wszyscy czytają, czy info ginie?**

> **Twoja opowieść:**
> 
>
>
>

### 💻 Moja propozycja okna ZPSP:

**System alertów Teams**:
- „Anulacja >500 kg" → #produkcja + #magazyn + #zarząd
- „Awaria linii — STOP 5 min" → SMS do Sergiusza
- „% klasy B >25%" → #jakosc + Justyna
- „Reklamacja >5 000 zł" → Sergiusz osobiście

### Twoja reakcja:
> 

---

## 🚨 SCENA 14 — SYTUACJE AWARYJNE

### Pytania (4 typowe sytuacje, opisz każdą krótko):
1. **Ostatnia awaria linii** — co, ile, kto naprawił?
2. **Ostatnia anulacja kluczowego klienta** — jak zareagowaliście?
3. **Newcastle 12 km luty 2026** — co zrobiliście operacyjnie?
4. **Wypadek pracownika** — był? procedura BHP?

> **Twoja opowieść:**
> 
>
>
>

### 💻 Moja propozycja okna ZPSP:

**„Tryb awaryjny"**:
- Klik „🚨 INCYDENT" → wybór typu (awaria/anulacja/HPAI/BHP)
- Auto-checklist: co robić, kto powiadomiony
- Po incydencie: raport → CAPA register

### Twoja reakcja:
> 

---

## 😤 SCENA 15 — FRUSTRACJE CODZIENNE

### Pytania (top 3 z każdej grupy):
1. **Twoje top 3 — co Cię dziś wkurza?**
2. **Top 3 Justyny / kierowników**
3. **Mała frustracja kosztowna** (np. „30 min dziennie szukamy palety")

> **Twoja opowieść:**
> 
>
>
>

### 💻 Moja propozycja:

**Mechanizm „Zgłoś frustrację"** — ikonka „🤬" w każdym oknie ZPSP. Pracownik klika, opisuje, wraca do bazy. Tygodniowy raport dla Sergiusza.

### Twoja reakcja:
> 

---

## 🌟 SCENA 16 — IDEALNA WIZJA

### Pytania:
1. **5:00 rano — co jest pierwszą rzeczą którą widzisz w ZPSP?**
2. **9:00 idziesz na halę — jaki widok masz na tablecie?**
3. **19:00 wracasz do domu — czy zabierasz laptop?**

> **Twoja opowieść (długa, marzenie):**
> 
>
>
>

### 💻 Moja propozycja:

**Cockpit Idealny** — 1 ekran 4K na ścianie + mobile:
- 5:00: 6 KPI dziś + alerty + plan dnia
- 9:00: tablet z 3 ekranami (tempo / zmiany / klasa A vs B)
- 19:00: aplikacja mailem podsumowuje dzień, ja czytam 2 min

### Twoja reakcja:
> 

---

# 📌 NASTĘPNE KROKI

1. **Odpowiadaj na sceny 5-16** — sekcjami, dziennie po jednej-dwóch
2. **Wypełnij `STANOWISKA_PRODUKCJI.md`** — w poniedziałek na hali (notuj voice-to-text)
3. **Akcje dla Ciebie:**
   - Zapytaj dostawcę WAGI SELEKTYWNEJ o dostęp do bazy
   - Zapytaj dostawcę LICZNIKA TUSZEK o dostęp do bazy
   - Doprecyzuj rolę Anny Majczak z Justyną
   - Doprecyzuj kto decyduje o mielonym/polędwiczkach/tubie
   - Doprecyzuj procedury jakości Klaudii Osińskiej

**Po Twoich opowieściach:** napiszę „Mapa procesu Twojej produkcji" + „15 okien ZPSP rozplanowane" + „Plan migracji starych okien".
