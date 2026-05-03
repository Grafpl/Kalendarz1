# 06 — Hala produkcji (ubój, klasyfikacja, rozbiór)

## Układ hali

```
[Wejście żywca z hodowców]
         │
         ▼
┌─────────────────┐
│  BRUDNA STREFA  │  ← Łukasz Collins, ~3:30-13:00
│ - Rozładunek    │     4-6 osób
│ - Zawieszanie   │
│ - Patroszenie   │     Meyn Mountaineer 2015 → IX 2026 Meyn Maestro
│ - WAGA + klasy  │     WAGO selektywna (klasy 6/7/8/9/10/11)
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│   CHŁODZENIE    │  ← przejście między brudną a czystą
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│  CZYSTA STREFA  │  ← klasyfikacja A/B + rozbiór
│ - Klasyfikacja  │     Wybijanie z linii do wanny
│ - Zawieszanie   │     4-6 osób, 1-2 sek/sztuka
│ - Korytarze     │     Klasy wagowe rozdzielają do pojemników 15 kg
│ - Filet/korpus  │     Maszyna rozbierająca
│ - Krojenie      │     Wagi platformowe + drukarka etykiet
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│ MAGAZYN ŚWIEŻYCH│  ← bez regałów, palety na podłodze
│   (65554)       │     FIFO, magazynierzy
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│    WYDAWKA      │  ← 2 rampy załadunkowe
│   (65556)       │     Magazynier kompletuje wg "Panel magazyniera"
└─────────────────┘
```

---

## Brudna strefa (Łukasz Collins)

**Start:** 3:30 (lato 2:30) z dostawą żywca AVILOG.

**Kroki:**
1. Rozładunek aut z żywcem
2. Zawieszanie kurczaków na haku linii (4-6 osób)
3. Oszołomienie (gazem CO₂ lub elektrycznie)
4. Wykrwawianie
5. Parzenie (skóra zmiękczana)
6. Skubanie (usuwanie piór)
7. **Patroszenie** — Meyn Mountaineer (2015), IX 2026 → Meyn Maestro
8. Mycie wewnątrz
9. **Chłodzenie** (przejście między strefami)

**Padłe w transporcie:** pracownicy podczas rozładunku znajdują martwe kurczaki → osobny kontener → firma utylizująca odbiera.

---

## WAGO selektywna (klasy wagowe)

**KRYTYCZNE: brak dostępu API do tego systemu. Sergiusz prosi dostawcę o API.**

**Co robi WAGO:**
- Sprawdza wagę każdej tuszki
- Rozdziela do **klas wagowych** — czyli **liczby sztuk w pojemniku 15 kg netto**:

| Klasa wagowa | Liczba sztuk w pojemniku 15kg | Ciężar 1 sztuki | Komentarz |
|---|---|---|---|
| **5** | 5 sztuk | ~3.0 kg | Bardzo duży kurczak |
| **6** | 6 sztuk | ~2.5 kg | **Idealna klasa** |
| **7** | 7 sztuk | ~2.14 kg | **Idealna klasa** |
| **8** | 8 sztuk | ~1.875 kg | Średni |
| **9** | 9 sztuk | ~1.67 kg | Średni |
| **10** | 10 sztuk | ~1.5 kg | Mniejszy |
| **11** | 11 sztuk | ~1.36 kg | Mały |
| **12** | 12 sztuk | ~1.25 kg | Najmniejszy w użyciu |
| **0** | (brak / mix) | — | Zapomnienie operatora lub mix klas w pojemniku |

Pole DB: `LibraNet.dbo.In0E.QntInCont` — tylko dla `ArticleID = '40'` (Kurczak A).

**Workflow:**
1. Pracownik na zawieszaniu klasyfikuje wzrokowo A vs B (wajcha)
2. WAGA czyta wagę + klasę wagową
3. **Wajcha do góry = "B"** → WAGA wie że nie sortuje, jedzie do maszyny rozbierającej
4. Klasa A → korytarz wg wagi → pojemnik 15 kg

**Pain point:** Brak danych z WAGO oznacza że **% klasy A vs B per hodowca** jest niemierzalny w ZPSP. Sergiusz mierzyłby to ręcznie albo z osobnego modułu klasyfikacji.

---

## Klasyfikacja A/B (4-6 osób, czysta strefa, zawieszanie)

**Co to klasa B?** Wszystkie wady wzrokowe:
- **Krwiak** (czerwony filet)
- **Złamanie** (kurczak źle złapany w hodowli, kość widoczna)
- **Żółć** (nieusunięta torebka żółciowa)
- **Czerwony filet** (wybroczyny mięśnia piersiowego)
- **Oparzenia** (skóra przy parzeniu)
- **Otwarte rany**
- **Inne**

**Czas decyzji:** 1-2 sekundy na sztukę.

**Klasa B jedzie do końca linii → maszyny rozbierającej** (filet + korpus rozdzielane).

---

## Rozbiór (czysta strefa)

**Workflow:**
1. **Korpus z filetem razem** wchodzą do maszyny
2. Maszyna rozdziela: **filet** + **korpus**
3. **Korpus → bezpośrednio do pojemnika** (15 kg E2)
4. **Filet → ręczne czyszczenie** (4-6 osób):
   - Usuwanie balonów
   - Usuwanie zakrwawionych miejsc
5. **Filet do pojemnika** + waga + etykieta

**Wagi w czystej strefie:**
- **Waga paletowa** — dla całych tuszek na palecie
- **Waga platformowa** — dla pojemników z elementami (15 kg E2)
- Terminale przy każdej wadze drukują **etykietę** po zważeniu

---

## Współczynniki uzysku (z modułu Krojenie 14A)

**Hardcoded w `PokazKrojenieMrozenie.cs`:**

| Element | Uzysk z tuszki |
|---|---|
| **Filet** | **29.5%** |
| **Ćwiartka** | **33.4%** |
| **Korpus** | **22.7%** |
| **Skrzydło** | **8.7%** |
| Pozostałe (skórki, kości, odpady) | ~5.7% |

**Przelicznik żywiec → tuszka:** **~78%** (Dyrektor Zakładu używa rano przy planowaniu).

---

## Norma per pracownik

**Status:** Sergiusz mówi że można wyciągnąć z bazy SQL. Nie jest jeszcze w ZPSP jako standardowy KPI.

**Sprawdzenie:** zapytanie SQL z `Wazenia` lub podobnej tabeli (do potwierdzenia w `13_Bazy_danych.md`).

---

## Niejasne (do potwierdzenia)

1. **Kto decyduje co produkować** (mielone, polędwiczki, tuba)?
   - Sergiusz: NIE WIE, musi się dowiedzieć
2. **Skrawki / odpady (skórki, kości)** — co się z nimi dzieje?
   - Sergiusz: NIE WIE, prosi przypomnienie
3. **Czystość pojemników E2** — kto sprawdza?
   - Sergiusz: NIE WIE, pyta czy powinno być sprawdzane
4. **Anna Majczak** — rola?
   - Sergiusz: NIE WIE
5. **Inwentaryzacja co 3 mies.** — sygnał problemu (powinno być częściej)
6. **Brak czytników temperatury w czasie rzeczywistym** (frustracja Sergiusza)

---

## Pain points produkcji (z odpowiedzi Sergiusza)

> *"Wkurza mnie to, że partie kurczaka które wychodzą z magazynu nie są rozliczane."*

> *"Wkurza mnie to, że inwentaryzacje są robione co 3 miesiące i zawsze mówią że to przez produkcję."*

> *"Wkurza mnie to, że nie mamy skanerów."*

> *"Wkurza mnie to, że nie mam czytników temperatury."*

> *"Wkurza mnie to, że nie mogę obliczyć wydajności pracowników brudnej i czystej strefy."*

> *"Wkurza mnie to, że nie potrafię powiedzieć ile pracowników jest mi potrzebnych."*

---

## Idealna wizja (Sergiusz, scena 16 z PYTANIA_PRODUKCJA)

**5:00 rano — pierwszy widok w ZPSP:**
> *"KTO jest na hali z pracowników, w której hali co robią. Ile mamy towaru. Ile mamy pojemników. Jaką mamy temperaturę. Jaki mamy dokładny towar z każdej partii i jaki rozmiar tuszki i z jakiej produkcji towar."*

**To brzmi jak Cockpit / Hala LIVE** — jeden ekran z:
- Mapą hali z punktami pracowników (z UNICARD)
- Stan magazynów (kg per produkt per partia)
- Liczba pojemników E2 (sprawdzić kto ma kalkulator)
- Temperatury (jeszcze brak czytników!)
- Klasy wagowe per partia

**Status:** Brak takiego okna. Najbliżej jest `DashboardPrzychoduWindow` (Przychód Żywca LIVE), ale pokazuje tylko stronę dostaw, nie hali.
