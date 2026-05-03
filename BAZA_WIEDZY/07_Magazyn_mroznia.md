# 07 — Magazyny i mroźnia

## Magazyn świeżych (65554) — magazyn dystrybucji

**Lokalizacja fizyczna:** Po hali produkcji czystej, przed wydawką.
**Bez regałów** — palety stoją bezpośrednio na podłodze. Sergiusz nie zna dokładnej liczby palet jakie się mieszczą (do sprawdzenia).

**Co tam idzie:**
- Tuszki świeże po klasyfikacji (klasa A)
- Elementy po krojeniu (filet, ćwiartka, korpus, skrzydło)
- W pojemnikach E2 15 kg lub na paletach H1

**FIFO bezwzględne:**
- Najstarszy najbliżej wyjścia
- Magazynierzy pilnują FIFO
- Bez wyjątków: *"Klient woli świeże" → odpowiedź: "wydaję wg FIFO"*

**Kontrola:**
- **Dział jakości** sprawdza temperaturę
- **Magazynierzy** pilnują FIFO

**Co jeśli niesprzedane:**
1. **Tuszka** → krojenie (rozbiór na elementy, zwiększa szansę sprzedaży)
2. **Pokrojone elementy** → mroźnia (jeśli też nie idą)
3. **Mrożone** → po 1-6 miesiącach taniej do klienta (-18% wartości)

---

## Wydawka (65556) — rampa załadunkowa

**2 rampy załadunkowe.**

**Workflow wydania:**
1. Handlowcy (6:00-8:00) zbierają zamówienia
2. Wpisują datę produkcji + datę i godzinę awizacji klienta
3. Logistyk układa transport
4. Magazynier widzi w **Panelu Magazyniera** zamówienia w kolejności wyjazdu
5. Tworzy wydanie (RWP/sWZ), kompletuje towar
6. Brak czegoś → ładuje co ma + poprawia wydanie ostateczne (wpisuje faktyczną ilość)
7. Auto wyjeżdża

**Brak skanerów RFID/kodów kreskowych** — pain point Sergiusza:
- Magazynier wpisuje numery partii ręcznie na papierowej WZ
- Ilości "na oko" — nie ma faktycznego rozliczenia ile z której partii poszło do auta

---

## Magazyny mrożone (65562) — 3 mroźnie + szokówka + chłodnia

### Komory chłodnicze
**3 mroźnie + 1 chłodnia + 1 szokówka.**

**Temperatury:**
- Chłodnia: ok. +2°C do +4°C
- Szokówka: zamrażanie szybkie (-30°C i niżej)
- Mroźnie: **min -18°C, cel -20°C**

**Brak czytników temperatury w czasie rzeczywistym** — frustracja Sergiusza.

---

### Workflow mrożenia (Janek Matusiak)

**Kiedy:**
- W **piątki** Janek przychodzi rano 4-5 (jeśli elementy się nie sprzedały na koniec tygodnia)
- W innych dniach: po decyzji 13:00 (gdy jest)

**Kroki:**
1. Bierze towar z pojemników 15 kg E2
2. **Przeważa na 10 kg** — lepiej się zamraża (większa powierzchnia, szybsze przemarznięcie)
3. Wkłada do **szokówki**
4. **Po 24h** wyjmuje
5. Wybija towar z E2:
   - Na **karton** (paczkowane, dla klientów detalicznych)
   - Na **paletę w polibloach** (dla klientów hurtowych)
6. Trafia do mroźni właściwej (jednej z 3)

---

### FIFO w mroźni
- Najstarszy najbliżej wyjścia
- Wydanie TYLKO za zgodą Dyrektora z wpisem ZPSP

### Inwentaryzacja
- **Codziennie:** temperatury (czytane przez kogoś — Janek? jakość?)
- **Co tydzień:** pełna fizyczna inwentaryzacja
- **Realnie (Sergiusz):** *"Inwentaryzacje są robione co 3 miesiące i zawsze mówią że to przez produkcję"* — pain point

### Norma straty wagi
- **≤2% wagi** w cyklu in→out (wsadu vs wydania)
- Strata >2% → raport i dochodzenie

### Czas leżakowania w mroźni
- **1-6 miesięcy** typowo
- Po tym: sprzedaż (-18% wartości od ceny świeżej)
- Eksport (przez pośredników) — głównie mrożone

---

## Tabela magazynów HANDEL (Symfonia)

| Symbol | Nazwa | Typ dokumentów | Co tam |
|---|---|---|---|
| **65554** | Świeże po uboju | sPWU, PWP, RWP, sPZ | Tuszki + elementy świeże |
| **65556** | Wydania | sWZ, sWZ-W, sWZK | Wydania do klientów (rampa) |
| **65552** | Drugi magazyn produkcji | (rzadziej) | (uzupełniający) |
| **65547** | Paczkowane | sPPK | Towar paczkowany (detaliczny) |
| **65562** | Mrożonki | sPPM | Towar mrożony |
| **65559** | Pomocniczy | różne | Pomocniczy |

**Pasze (osobne):** kategoria **65883**, jednostka tona, przyjęcie od TASOMIX/De Heus/Ekoplon.

---

## Pojemniki E2

**Standard:** 15 kg netto (waga towaru w pojemniku).
**Materiał:** plastikowy, składany.
**Cykl:** użycie → mycie → ponowne użycie.

**Myjka pojemników:**
- Osobne stanowisko (PROCEDURY_08_MYJKA)
- FIFO obowiązuje też tutaj — najstarszy pojemnik z brudnych pierwszy do mycia
- Czystość krytyczna dla BRC/IFS

**Czy jest sprawdzanie czystości?** Sergiusz: NIE WIE, pyta czy powinno być sprawdzane.

---

## Polibloki (mroźnia)

**Definicja:** Karton zewnętrzny + worek foliowy w środku, z zamrożonym towarem (10 kg).

**Workflow:**
1. Towar 10 kg po szokówce → wsypywany do worka
2. Worek włożony do kartonu/poliblok
3. Karton zaklejany, etykietowany
4. Paleta z polibloami w mroźni właściwej

---

## Pain points magazyn/mroźnia (z odpowiedzi Sergiusza)

> *"Brak informacji o stanach rzeczywistych w magazynach na bieżąco i partii które idą do kogo."*

> *"Wkurza mnie to, że partie kurczaka które wychodzą z magazynu nie są rozliczane."*

> *"Wkurza mnie to, że zamrażamy towar którego nie możemy sprzedać bo ciężko przewidzieć ile ostatecznie będzie towaru na koniec dnia."*

> *"Wkurza mnie to, że nie mogę dokładnie rozliczyć produkcji i magazynu."*

---

## Pomysł: MroźniaDashboard

**Status:** brak okna w ZPSP. Pomysł Sergiusza:

**„Mroźnia — Kierownik mroźni"** (tablet u Janka):
- Mapa 3D komór: kolorowe sektory wieku (0-30 / 30-90 / 90-180 / >180 dni)
- "Do mrożenia dziś" (z 13:00 spotkania)
- "Do wydania jutro" — automat FIFO
- Alert: *"Partia 25034 leży 270 dni — sprawdź"*

**Reakcja Sergiusza:** "OK"
