# PROMPT DLA CLAUDE WEB — 12 wizualizacji HTML widoku PŁATNOŚCI

> Skopiuj CAŁOŚĆ poniżej (od linii `=== START PROMPTU ===`) do Claude Web.
> Oczekiwany wynik: kompletne, samodzielne pliki **HTML** z 12 zaprojektowanymi makietami
> (6 dla okna głównego + 6 dla okna szczegółów kontrahenta). To mają być WIZUALIZACJE
> koncepcji (mockupy), nie kod produkcyjny — wybiorę najlepsze, a potem przepiszemy je na WPF.

---

=== START PROMPTU ===

# ZADANIE: zaprojektuj i ZWIZUALIZUJ w HTML 12 koncepcji ekranu „Płatności klientów"

Jesteś senior product designerem + front-end developerem specjalizującym się w gęstych,
„operatorskich" dashboardach finansowych (typu trading desk / panel windykacyjny). Twoim
zadaniem jest zaprojektować i **w pełni zwizualizować w czystym HTML+CSS** dwanaście (12)
różnych koncepcji ekranu należności. Chcę realne, klikalne, dopracowane wizualnie makiety —
nie opisy, nie szkice. Poświęć na to dużo czasu i przemyśl każdą koncepcję osobno.

## KONTEKST BIZNESOWY (przeczytaj uważnie — to nie jest generyczny dashboard)

Prowadzę **zakład drobiarski** (ubój i przetwórstwo kurczaka): 258 mln zł obrotu rocznie,
~200 ton mięsa dziennie, sprzedaż do ~90–150 aktywnych odbiorców (sieci, hurt, sklepy,
masarnie, eksport). Codziennie rano patrzę na ekran „Płatności", żeby w 30 sekund wiedzieć:

1. **KTO mi wisi i ILE** — posortowane od najgorszych.
2. **JAK BARDZO TO PALI** — ile dni po terminie, czy kwota rośnie z dnia na dzień.
3. **CO MAM ZROBIĆ** — zadzwonić / wstrzymać kolejną dostawę / zablokować kredyt / odpuścić.

To jest narzędzie decyzyjne pod presją czasu, nie raport księgowy. Klient bez zapłaty =
ryzyko, że jutro wyślę mu kolejne 200 palet mięsa i nigdy nie zobaczę pieniędzy. Branża
mięsna ma cienkie marże (2–4%), więc jedno przeterminowane 300k potrafi zjeść zysk z całego
tygodnia.

## REALNE DANE (użyj ich w makietach — mają wyglądać prawdziwie)

Zagregowane (stan „dziś"):
- **Do zapłaty razem: 18 032 779 zł** (1173 otwarte faktury, 93 klientów z saldem)
- **W terminie: 11 343 166 zł**
- **Przeterminowane: 6 689 613 zł** (≈ 37% należności)
- Aging przeterminowanych (kwota / liczba faktur):
  - 1–7 dni: 2 140 000 zł / 31 fv
  - 8–14 dni: 1 760 000 zł / 24 fv
  - 15–21 dni: 1 410 000 zł / 18 fv
  - 21+ dni: 1 379 613 zł / 22 fv

Przykładowi klienci (wymyśl 12–18 wierszy w podobnej skali; nazwy realistyczne dla
polskiego rynku mięsnego — sieci, hurtownie, masarnie, np. „MAKRO Cash & Carry",
„Hurt-Drob Sp. z o.o.", „Masarnia Wiejska Kowalski", „Bidfood Polska", „Eksport DE — Geflügel
GmbH"). Każdy klient:
- Nazwa, opiekun-handlowiec (Maja / Justyna / Marcin / Nieprzypisany), limit kredytowy,
  do zapłaty, w terminie, przeterminowane, max dni po terminie, liczba faktur,
  % wykorzystania limitu, czy limit przekroczony.
- Rozrzuć tak, żeby było widać różne sytuacje: idealny płatnik (0 przeterminowanych),
  chronicznie spóźniony (41 dni, 280k), świeżo po terminie (3 dni, drobna kwota), gruba ryba
  blisko limitu (limit 2M, do zapłaty 1,9M), eksporter EUR, klient „Nieprzypisany".

Dla okna szczegółów wymyśl dla jednego klienta listę ~15 faktur (numery typu „0123/26/FVS",
daty, terminy, kwota brutto, rozliczone, do zapłaty, dni po terminie, status) + „rytm
płatnika": średnio płaci po 19 dniach, ostatnia wpłata 02.06, opóźnień w 12M: 7,
suma zapłacona 12M: 4,2M.

## CO MASZ WYPRODUKOWAĆ

### Część A — 6 koncepcji OKNA GŁÓWNEGO (lista wszystkich klientów z saldem)
### Część B — 6 koncepcji OKNA SZCZEGÓŁÓW (jeden klient, otwierany dwuklikiem)

Łącznie **12 w pełni wyrenderowanych makiet HTML**. Każda koncepcja ma być wyraźnie inna
filozoficznie, nie kosmetycznie. Poniżej kierunki — rozwiń je, dodaj swoje:

**Okno główne — 6 różnych podejść (przykładowe kierunki, możesz ulepszyć):**
1. **„Triage windykacyjny"** — jak skrzynka mailowa: lista wierszy sortowana po pilności,
   kolorowy lewy pasek (czerwony/pomarańcz/zielony), kwota-bohater po prawej, akcje w hover.
2. **„Heatmap / aging matrix"** — klienci w wierszach, koszyki dni (1-7/8-14/15-21/21+)
   w kolumnach, komórki wypełnione kolorem wg kwoty. Od razu widać gdzie się pali.
3. **„Karty ryzyka (kanban)"** — kolumny: Do telefonu / Pilne / Krytyczne / Blokada,
   klienci jako karty przeciągalne wizualnie, z kwotą i dniami.
4. **„Big numbers + drill"** — minimalizm: 3 ogromne liczby na górze, pod spodem jedna
   czysta lista TYLKO przeterminowanych, reszta zwinięta.
5. **„Portfel handlowca"** — pogrupowane po opiekunie (Maja / Justyna...), każdy handlowiec
   to sekcja z własną sumą ryzyka — do rozliczania ludzi z ich klientów.
6. **„Dashboard sygnalizator"** — fokus na TREND: przy każdym kliencie mini-sparkline salda
   z 6 dni + strzałka rośnie/maleje, żeby łapać pogarszających się ZANIM przekroczą limit.

**Okno szczegółów — 6 różnych podejść:**
1. **„Karta windykacyjna"** — nagłówek z 3 liczbami + duży przycisk „Kopiuj wezwanie",
   lista faktur posortowana: przeterminowane → w terminie → rozliczone (zwinięte).
2. **„Oś czasu płatnika"** — timeline: faktury i wpłaty na osi czasu, widać rytm i opóźnienia.
3. **„Profil ryzyka"** — scoring klienta, % terminowości, wykorzystanie limitu jako gauge,
   rekomendacja akcji wygenerowana z danych („limit 99% + 7 opóźnień → rozważ blokadę").
4. **„Czysta tabela 2-sekcyjna"** — góra: KPI w jednym zdaniu, dół: jedna gęsta tabela faktur
   z filtrami-chipami (wszystkie / po terminie / opłacone).
5. **„Rozmowa z klientem"** — układ pod telefon: po lewej „co powiedzieć" (lista
   przeterminowanych z numerami i kwotami gotowa do dyktowania), po prawej kontekst/historia.
6. **„Cashflow klienta"** — porównanie: ile nam płaci vs ile bierze towaru, czy saldo rośnie
   przez miesiące, prognoza kiedy spłaci przy obecnym tempie.

## WYMAGANIA WIZUALNE (to ma być ŁADNE i gotowe do oceny)

- **Dark theme**, nowoczesny, gęsty ale czytelny. Sugerowana paleta (możesz dopracować):
  tła `#0F1419` / `#1A1D21` / `#1A2332`, karty `#243344`, obramowania `#2D3A4F`,
  tekst główny `#E2E8F0`, wyciszony `#8B949E`, zielony `#34D399`, czerwony `#EF4444`/`#FF6B6B`,
  pomarańcz `#F97316`, żółty `#FCD34D`, fiolet/akcent `#A78BFA`, niebieski `#38BDF8`.
- Font systemowy (Segoe UI / -apple-system / Inter). Kluczowe liczby DUŻE (24–40px),
  nigdy nie schodź poniżej 12px dla treści.
- **Czysty HTML + CSS** (może być trochę vanilla JS dla chipów-filtrów, hover, przełączania
  zakładek — ale bez bibliotek). **ŻADNYCH frameworków UI, ŻADNEGO DevExpress, żadnego
  Bootstrap/Tailwind CDN jeśli się da — pisz własny CSS.** Sparkline'y i gauge rysuj jako
  inline SVG, nie biblioteki wykresów.
- Realne liczby (te z sekcji DANE), formatowanie polskie: `18 032 779 zł`, daty `DD.MM.YYYY`.
- Mikro-interakcje: hover na wierszu, kolorowe statusy, badge przekroczonego limitu, ikony
  (użyj emoji lub inline SVG). Pokaż stany: idealny płatnik vs krytyczny dłużnik.
- Każda makieta responsywna do ~1600×900 (na tym pracują użytkownicy), ale niech ładnie
  wygląda też zmniejszona.

## FORMAT DOSTARCZENIA (ważne)

Dostarcz to jako **artefakty HTML**. Preferowana struktura:
- Jeden plik **`platnosci_glowne.html`** zawierający wszystkie 6 koncepcji okna głównego,
  przełączanych górną belką zakładek („Koncepcja 1 · Triage", „Koncepcja 2 · Heatmap"…).
- Jeden plik **`platnosci_szczegoly.html`** z 6 koncepcjami okna szczegółów, też w zakładkach.
- (Jeśli wolisz — możesz dać 12 osobnych artefaktów, ale wtedy ponumeruj jasno.)
- Na górze każdej koncepcji **mały opisowy pasek** (2–3 zdania): nazwa koncepcji, dla kogo
  / kiedy najlepsza, co jest jej mocną stroną i kompromisem. To mi pomoże wybrać.

Pracuj DOKŁADNIE i NIE upraszczaj na siłę — chcę zobaczyć 12 naprawdę różnych, dopracowanych
wizji, każda wypełniona realistycznymi danymi, tak żebym mógł je porównać obok siebie i
wskazać palcem: „ta główna + ta szczegółowa". Po wygenerowaniu krótko podsumuj, którą parę
TY byś wybrał dla zapracowanego właściciela zakładu i dlaczego.

=== KONIEC PROMPTU ===

---

## Notatka dla mnie (Sergiusz) — co dalej

Po wygenerowaniu makiet w Claude Web wybierz 1 koncepcję głównego okna + 1 szczegółów.
Wklej wybrane HTML-e tutaj do Claude Code — przepiszę je na WPF (code-behind, bez MVVM,
bez DevExpress) i podepnę pod istniejące, już naprawione pobieranie danych
(`FetchPlatnosciAsync` / `KontrahentPlatnosciWindow`). Modele danych gotowe:
`PlatnoscRow`, `AgingData`, `FakturaRow`.
