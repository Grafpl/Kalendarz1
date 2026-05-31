# Gdzie wpiąć wiedzę z "Broiler Meat Signals" → moduł Reklamacji

**Pytanie Sergiusza:** *„Dział reklamacji / dział jakości gdzie reklamacje są — może tam dać tę wiedzę?"*

**Odpowiedź: TAK — to idealne miejsce, i tańsze niż myślisz, bo połowa instalacji już jest w kodzie.**

---

## Dlaczego AKURAT moduł reklamacji (a nie nowy moduł)

Książka mówi: *„meat quality = chain quality"* — każda wada to **sygnał wskazujący przyczynę w górę łańcucha**. Reklamacja klienta to **ostatni i najtwardszy sygnał** — klient zapłacił i odrzucił. Jeśli skategoryzujesz reklamacje wg taksonomii książki i połączysz z partią/hodowcą/procesem, **dział reklamacji staje się układem nerwowym jakości całej firmy.**

I — co kluczowe — **Twój moduł `Reklamacje/` już ma 80% potrzebnej instalacji:**

| Co już masz w kodzie | Gdzie | Po co przy taksonomii wad |
|---|---|---|
| **Reklamacja → Partia** | tabela `ReklamacjePartie` + `PartiaViewModel` (NumerPartii, Dostawca) | **Już łączysz reklamację z partią i hodowcą!** |
| **Źródło zgłoszenia** | `ZrodloZgloszenia` (Klient/Kierowca/Jakość/Handlowiec/Symfonia) | wiesz kto zgłosił |
| **Zdjęcia** | `ReklamacjeZdjecia` + miniatury inline | dowód wizualny wady |
| **Decyzja jakości** | pole `DecyzjaJakosci` | Justyna już ocenia |
| **SLA dwa zegary** | jakość 24h + rozwiązanie 7 dni | terminowość |
| **Statystyki** | `StatystykiReklamacjiWindow` (910 linii) | gotowa warstwa analityczna |

**Czego brakuje — DOKŁADNIE jednej rzeczy:** ustrukturyzowanej **kategorii wady** (`KategoriaWady`). Dziś masz tylko `Opis` (wolny tekst) + `TypReklamacji` — a `TypReklamacji` jest zdominowane przez „Faktura korygująca". To dlatego audyt Broiler Signals napisał *„reklamacje działają, ale 75% to szum z korekt faktur"* — **prawdziwe wady jakościowe toną w korektach księgowych.**

---

## Co konkretnie dodać — taksonomia wad z książki

Jedna nowa kolumna `KategoriaWady` + słownik oparty na rozdziałach książki. **Każda kategoria od razu wskazuje etap łańcucha gdzie powstała** (to jest cała magia „signals"):

| Kategoria wady (KategoriaWady) | Rozdz. książki | Etap-przyczyna (auto-sugestia) | Twój realny przypadek |
|---|---|---|---|
| 💧 **Mokry / drip loss / woda** | 8 | Chłodzenie (ślimak / chiller) | **„mokry kurczak" — rozmowa #4!** |
| 🪵 **Wooden breast / white striping** | 7 | Genetyka + waga >2,5 kg (ferma) | straty fileta #12 |
| 🩸 **Krwiaki / blood spots** | 7 | Ogłuszanie (za wysokie V/mA) lub łapanie | |
| 🦴 **Złamania / pop-out** | 7 | Łapanie / transport / skubarka | |
| 🔪 **Rozerwania / zadrapania skóry** | 6 | Skubarka (plucking) | |
| 🟣 **Złe wykrwawienie (sino-fiolet)** | 7 | Ogłuszanie / cięcie | |
| 🌡️ **Za ciepły / łańcuch chłodniczy** | 8 | Chłodzenie / transport (opóźnienia 2-3h #25) | chiller 3-4°C #8 |
| 🦠 **Salmonella / Campylobacter** | 3+9 | Higiena / bioasekuracja | 4 rozmowy o salmonelli |
| 🧪 **Kontaminacja / ciało obce** | 9 | Higiena linii | |
| ⚖️ **Klasa B / kalibracja / waga** | 8 | Grading | klasa B real-time #47 |
| 📦 **Opakowanie (folia, karton)** | 8 | Pakowanie | folia mokra (#4: „karton rozwalany") |
| 🧾 **Korekta faktury (NIE jakość)** | — | Księgowość | **oddziel to od reszty!** |

**Kluczowy ruch:** ostatnia kategoria („Korekta faktury") **wyciąga 75% szumu** z reszty — wtedy widzisz CZYSTE reklamacje jakościowe.

---

## Co to odblokowuje (czego dziś NIE możesz zrobić)

Bo reklamacja jest już połączona z partią + hodowcą (tabela `ReklamacjePartie`), po dodaniu kategorii wady **Twoje istniejące Statystyki Reklamacji** od razu odpowiedzą na pytania, na które dziś nie ma odpowiedzi:

1. **„Mokry kurczak — ile reklamacji w tym kwartale? trend?"** → koniec z „Mohamed mówi", masz liczbę
2. **„Z których partii/hodowców najwięcej wad mokrości?"** → bo partia jest już podpięta
3. **„Czy reklamacje 'za ciepły' wzrosły od uruchomienia nowego chillera?"** → walidacja inwestycji #8
4. **„Który hodowca generuje najwięcej krwiaków / złamań?"** → scorecard hodowcy (książka rozdz. 3)
5. **„% reklamacji jakościowych vs korekty"** → realny KPI jakości pod BRC v9

To **dokładnie domyka pętlę z poprzedniej analizy** — zamiast dowiadywać się o „mokrym kurczaku" od agenta Halal po fakcie, widzisz to z własnych danych w czasie rzeczywistym.

---

## Implementacja — mała, bo grunt już położony

### Faza 1 — taksonomia (1-2 dni)
1. **SQL:** `ALTER TABLE Reklamacje ADD KategoriaWady VARCHAR(40) NULL` + tabela słownika `ReklamacjeKategorieWad` (Id, Nazwa, RozdzialKsiazki, EtapPrzyczyna, Aktywna)
2. **Model:** dodać `KategoriaWady` do `ReklamacjaItem` (jak istniejące pola)
3. **UI:** ComboBox „Kategoria wady" w `FormReklamacjaWindow` + `FormRozpatrzenieWindow` (Justyna wybiera przy rozpatrywaniu)
4. **Filtr/badge** kategorii w `FormPanelReklamacjiWindow` (jak istniejące chipy źródła)

### Faza 2 — analityka „signal → przyczyna" (2 dni)
5. W `StatystykiReklamacjiWindow` dodać:
   - rozkład wad wg kategorii (wykres)
   - **wady per hodowca/partia** (JOIN przez `ReklamacjePartie` — już jest!)
   - trend kategorii w czasie (czy chiller pomógł?)
6. **Auto-sugestia etapu-przyczyny** — gdy Justyna wybierze „Mokry/drip loss", system podpowiada „sprawdź: ślimak / czas w chillerze / temp" (z książki)

### Faza 3 — pętla zwrotna do zakupu (opcjonalnie, łączy z modułem Kontrakty)
7. Wady przypisane do hodowcy → zasilają **scorecard hodowcy** (Część 3 audytu zakupu, Centrum Asi → „trendy hodowców")
8. Hodowca z systematycznymi wadami → sygnał przy odnowieniu kontraktu

**Razem Faza 1+2: ~3-4 dni Sera.** Niski koszt, bo nie budujesz modułu od zera — dokładasz jedną kolumnę + analitykę do dojrzałego modułu (13,5k linii już działa).

---

## Dlaczego to lepsze niż „PM defects tablet" z audytu Broiler Signals

Audyt sesji B proponował osobny tablet PM defects na linii (drogie, 2 mies, hardware). **Reklamacje to tańszy punkt startu tej samej idei:**
- PM defects tablet = łapiesz wadę na linii (wcześnie, ale wymaga sprzętu + obsady)
- Reklamacje z taksonomią = łapiesz wadę u klienta (później, ale **za darmo, na istniejącym module, dziś**)

**Zacznij od reklamacji** (3-4 dni, 0 hardware) → gdy zobaczysz które wady dominują, **wtedy** zdecydujesz czy warto PM defects tablet na te konkretne. Dane z reklamacji uzasadnią (lub nie) większą inwestycję. To jest „look-think-act" zastosowane do samej decyzji inwestycyjnej.

---

## Jedno zdanie

> **Tak — wpięcie taksonomii wad z książki w moduł Reklamacji to najtańszy, najszybszy i najmądrzejszy sposób uruchomienia filozofii „signals" w ZPSP, bo Twój moduł reklamacji JUŻ łączy reklamację z partią i hodowcą — brakuje dosłownie jednej kolumny (KategoriaWady), żeby zamienić dział reklamacji w układ nerwowy jakości całej firmy.**

---

*Oparte na: analizie kodu `Reklamacje/` (model 637 linii, tabele Reklamacje/ReklamacjePartie/Zdjecia/Historia), książce "Broiler Meat Signals" rozdz. 6-9, analizie `ANALIZA_KSIAZKA_x_FIREFLIES_x_INTERNET.md`. 2026-05-25.*
