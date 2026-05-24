# Część 1 — Audyt 16 kafelków modułu "Zaopatrzenie i Zakup"

**Źródło:** `Menu.cs` linie 1480–1556 (definicja kategorii) + `_moduleAccessOrder` linie 1300–1315 (accessMap)
**Werdykt sumaryczny:** 14 działa, 1 placeholder (Pasza/Pisklęta), 1 dubluje funkcję innego kafelka (Pozyskiwanie ↔ Hodowcy)

---

## Legenda werdyktu per kafelek

- 🟢 **DOBRY** — działa, używany, sensowny
- 🟡 **DOBRY ALE NIEDOKOŃCZONY** — solidna baza, brakuje warstwy wykorzystania danych / UX dla nowego użytkownika
- 🟠 **DUBLUJE / WYMAGA SCALENIA** — pokrywa się z innym kafelkiem, łatwo się pogubić
- 🔴 **MARTWY** — placeholder lub kafelek bez wartości
- 🩹 **DZIAŁA ALE WYMAGA HIGIENY** — funkcja OK, monstrualny code-behind / hardcode / brak walidacji

---

## Skrót — czego dotykają wszystkie kafelki (mapa DB)

```
LibraNet (192.168.0.109)  ←  RDZEŃ MODUŁU (12 z 15 działających kafelków)
  ├─ DOSTAWCY                  → kartoteka hodowców (~140 aktywnych)
  ├─ Pozyskiwanie_Hodowcy      → CRM leadów (1874 importowanych z Excela)
  ├─ HarmonogramDostaw         → kalendarz dostaw żywca
  ├─ WstawieniaKurczakow       → cykle wstawień (start cyklu hodowlanego)
  ├─ PartiaDostawca            → mapowanie partia↔hodowca
  ├─ In0E / Out1A              → ważenia
  ├─ FarmerCalc                → rozliczenia per dostawa (Padłe, CH, NW, ZM, Łapki)
  ├─ DostawcyCR / DostawcyCRItem → wnioski o zmiany danych hodowcy
  ├─ PriceType                 → słownik typu ceny
  └─ Notatki / AuditLog        → live audit + @mentions

HANDEL (192.168.0.112)    ←  Faktury, płatności
  ├─ HM.MG / HM.MZ / HM.DK     → faktury zakupu od hodowców
  └─ FK / FV                   → korekty + faktury

TransportPL (192.168.0.109) ←  Pojazdy / kierowcy do Matrycy Avilog
  └─ Pojazd, Kierowca, Kurs
```

---

# 📋 KAFELKI — RAPORT SZCZEGÓŁOWY

---

### 1. 🧑‍🌾 Baza Hodowców (`DaneHodowcy`)

| Pole | Wartość |
|---|---|
| **Plik** | `WidokKontrahenci.cs` — **982 linie** |
| **Typ UI** | WinForms (legacy) |
| **DB** | LibraNet → `DOSTAWCY`, `HarmonogramDostaw`, `FarmerCalc` |
| **accessMap** | `[00]` |

**Co robi:** Kartoteka ~140 aktywnych dostawców żywca: dane kontaktowe, historia dostaw, oceny (recenzja/rekomendacja), filtry typu ceny.

**Kto używa:**
- **Tereska** (najczęściej) — sprawdza kontakt do hodowcy przed telefonem
- **Asia** (rzadziej) — wyszukuje hodowcę przy rozliczeniach
- **Magda** (od poniedziałku) — będzie potrzebować codziennie

**Bolączki:**
- Legacy WinForms (~1000 linii w jednym pliku, mieszanka SQL + event handlery + business logic)
- Hardcoded connection string z plaintext hasłem
- Brak permission check — każdy może edytować dane każdego hodowcy
- Brak historii zmian na poziomie pola (kto zmienił NIP/adres, kiedy) — dopiero `DostawcyCR` (kafelek #15) ma audit, ale tylko dla wniosków
- Paginacja 2000 sztywno

**Czego brakuje (perspektywa Magdy/Asi):**
- **Linki krzyżowe** — z karty hodowcy nie kliknę do jego: wstawień, specyfikacji, ostatniej faktury, otwartych płatności, aktywnych umów
- **Tag "kontraktowy" vs "spotowy"** — kluczowe pod ARiMR (kto już jest pod 3-letnim kontraktem)
- **Telefony bezpośrednie z karty** — Magda boi się dzwonić, dobrze byłoby mieć przycisk "📞 zadzwoń" + auto-zapis do log rozmów

**Werdykt:** 🩹 **DZIAŁA ALE WYMAGA HIGIENY** — funkcjonalność OK, ale legacy WinForms + brak linków krzyżowych. **Pod Magdę nie wystarczy.**

---

### 2. 🐣 Cykle Wstawień (`WstawieniaHodowcy`)

| Pole | Wartość |
|---|---|
| **Plik** | `Zywiec/WstawieniaKurczaka/WidokWstawienia.xaml.cs` — **5333 linie** |
| **Typ UI** | WPF (kompletny rebuild) |
| **DB** | LibraNet → `WstawieniaKurczakow`, `HarmonogramDostaw`, `FarmerCalc`, `DostawaFeedback`, `AuditLog` |
| **accessMap** | `[02]` |

**Co robi:** **Rdzeń biznesowy modułu zakupu** — rejestracja dat wstawienia piskląt (dzień 0), automatyczne obliczanie terminu uboju (dzień 35/42), notatki z @mentions, live audit notifications. Cache dostaw TTL 60s, refresh co 15s.

**Kto używa:**
- **Tereska** (codziennie) — odbiera telefony "wstawiliśmy pisklaki" i wpisuje
- **Paulina** (przed odejściem) — robiła to samo
- **Magda** (od poniedziałku) — **TO BĘDZIE JEJ GŁÓWNY EKRAN**
- **Ser** — pilnuje aby Tereska/Paulina wpisały na czas

**Bolączki:**
- **Monstrualne code-behind** (5,3k linii) — 6 timerów, ręczne cache, race conditions na shared state
- Logika tooltipów rozsmarowana po 40 liniach (`_tooltipCloseTime` hack)
- Brak Observable / MVVM — każdy refresh wywołuje imperatywne UpdateUI
- Avatary z `static` cache bez locka — możliwe race conditions

**Czego brakuje (perspektywa Magdy/Asi):**
- **Potwierdzenia wstawień** — nie ma flow "hodowca przysłał potwierdzenie SMS/mail/WhatsApp → załącz do wpisu" (dziś papierowo, w głowie Tereski)
- **Alert "brakujące potwierdzenie"** — wstawienie wpisane, ale brak dowodu (kluczowe pod ARiMR i kontrakty!)
- **Walidacja zakresów** — można wpisać 10 milionów piskląt, system to przyjmie
- **Wskazówka dla nowego użytkownika** — Magda nie wie kiedy klikać "Nowe wstawienie" vs "Edytuj istniejące"

**Werdykt:** 🟡 **DOBRY ALE NIEDOKOŃCZONY** — działa od strony danych, ale **brakuje warstwy "dowód potwierdzenia"** (mail/SMS/WhatsApp screenshot przy wpisie). Pod ARiMR to wymóg.

---

### 3. 📅 Kalendarz Dostaw Żywca (`TerminyDostawyZywca`)

| Pole | Wartość |
|---|---|
| **Plik** | `Zywiec/Kalendarz/WidokKalendarzaWPF.xaml.cs` — **9631 linii** ⚠️ |
| **Typ UI** | WPF |
| **DB** | LibraNet + HANDEL (2 conn stringi) → `HarmonogramDostaw`, `WstawieniaKurczakow`, `PartiaDostawca`, `In0E`, `Notatki` |
| **accessMap** | `[03]` |

**Co robi:** Interaktywny kalendarz miesięczny planowania dostaw żywca, integracja z partiami ubojowymi, live audit, ranking dostaw, SMS, mention threading.

**Kto używa:**
- **Tereska** (codziennie) — kuje terminy z hodowcami i wpisuje
- **Ser** (codziennie, rano) — sprawdza plan dnia/tygodnia
- **Asia** (sporadycznie) — sprawdza historię w sporach z hodowcami
- **Magda** (od poniedziałku) — kolejny ekran do nauki

**Bolączki:**
- **NAJWIĘKSZY plik w module — 9,6k linii** (drugi po Specyfikacjach w całym repo)
- **6 timerów równolegle** (`_refreshTimer`, `_priceTimer`, `_surveyTimer`, `_countdownTimer`, `_liveWatchTimer`, `_mentionsPollTimer`) bez cancellation tokens
- **`// TODO: Wywołaj drukowanie PDF` na linii 11247** — eksport do druku nieskończony
- Magic numbers wszędzie (15 sek, 60 sek, limit 100 wstawień)
- Live audit ID śledzony przez `_lastSeenAuditId` bez transakcji → możliwe duplikaty/pominięcia notifikacji

**Czego brakuje (perspektywa Magdy/Asi):**
- **Wizualizacja "pod kontraktem vs spot"** — Asia jako strażnik kontraktów musi widzieć w kalendarzu które dostawy są na 3-letniej umowie
- **Alert wyprzedzający** — "za 2 dni mija termin dostawy, brak potwierdzenia od hodowcy"
- **Onboarding dla Magdy** — brak overlay'a "kliknij tu żeby dodać dostawę"

**Werdykt:** 🩹 **DZIAŁA ALE WYMAGA HIGIENY** — kafelek kluczowy, ale 9,6k linii to dług techniczny. Pod Magdę **dodać legendę kolorów** + tooltip "co kliknąć żeby zrobić X".

---

### 4. 🚛 Matryca Transportu / AviLog (`PlachtyAviloga`)

| Pole | Wartość |
|---|---|
| **Plik** | `Zywiec/MatrycaTransport/WidokMatrycaWPF.xaml.cs` — **2883 linie** + parserzy PDF/Excel (~1550 linii) |
| **Typ UI** | WPF (drag&drop) |
| **DB** | LibraNet + TransportPL → `HarmonogramDostaw`, `Kierowca`, `Pojazd`, `Naczepa`, SMS history |
| **accessMap** | `[04]` |

**Co robi:** Drag&drop matryca planowania tras transportu żywca — kto którym ciągnikiem do którego hodowcy. Import z PDF/Excela (płachty Aviloga), SMS do kierowców, eksport.

**Kto używa:**
- **Asia** — wpisuje dane z płachty Avilog (cytat usera: *"wpisuje dane ale nie mamy korzyści"*)
- **Tereska** — okazjonalnie

**Bolączki:**
- **TO JEST KAFELEK "WYRZUCONA PRACA"** — Asia poświęca czas na wpisywanie, ale dane są dla niej **ślepym zaułkiem** — nie wracają nigdzie w postaci raportu/decyzji/oszczędności
- Import PDF/Excel działa, ale wymaga ręcznego nadzoru i poprawek
- Brak walidacji — można dodać pojazd bez kierowcy
- 2 conn stringi hardcoded

**Czego brakuje (perspektywa Asi):**
- **Sens biznesowy danych** — co miałby Sergiusz/Asia z tego zrobić? Możliwe: rozliczenie Avilog vs własne auta, optymalizacja tras, raport kosztu transportu per hodowca, kalkulacja "czy bardziej opłaca nam się wozić swoim ciągnikiem"
- **Linkowanie z RozliczeniaAvilog** (kafelek #8) — dane z Matrycy powinny zasilać Rozliczenia, a nie być osobnym silosem

**Werdykt:** 🟡 **DOBRY ALE NIEDOKOŃCZONY** — funkcja ok, ale **brakuje warstwy wykorzystania**. Razem z kafelkiem #8 do scalenia/przemyślenia ścieżki użytkownika.

---

### 5. ⚖️ Panel Portiera (`PanelPortiera`)

| Pole | Wartość |
|---|---|
| **Plik** | `Portiernia/PanelPortiera.xaml.cs` — **4231 linii** |
| **Typ UI** | WPF touchscreen-optimized |
| **DB** | LibraNet → `HarmonogramDostaw` (wagi: Brutto/Tara), `FarmerCalc` |
| **accessMap** | (sprawdzić) |

**Co robi:** Dotykowy panel przy bramie — portier wpisuje wagę brutto i tara dostawy żywca. Trzy zakładki: dostawy AviLog, ODPADY, zwykłe. Klawiatura numeryczna, PIN ochrona, auto-logout 5 min, kamera CCTV.

**Kto używa:**
- **Portierzy** (zmiany 24/7)
- **Ser** — sporadycznie

**Bolączki:**
- ⚠️ **PIN HARDCODED `"1994"` w kodzie** (linia 64: `const string EXIT_PIN`) — security issue. Każdy portier zna.
- Hardcoded dane firmy w kodzie (NIP, REGON, adres) — gdy będzie sp. z o.o. (01.08.2026) **trzeba edytować kod i przekompilować**
- Hardcoded `C:\Windows\Media\chimes.wav` dla dźwięków
- Brak warning przed auto-logout (portier może stracić wpisane dane)

**Czego brakuje:**
- **Konfigurowalne dane firmy** — pod transformację w sp. z o.o.
- **PIN per portier** — dziś jeden PIN dla wszystkich = brak audit trail
- **Walidacja** — można wpisać ujemną wagę

**Werdykt:** 🩹 **DZIAŁA ALE WYMAGA HIGIENY** — pilna sprawa security (PIN) + dane firmy do sp. z o.o.

---

### 6. 🩺 Panel Lekarza Wet. (`PanelLekarza`)

| Pole | Wartość |
|---|---|
| **Plik** | `Portiernia/PanelLekarza.xaml.cs` — **1378 linii** |
| **Typ UI** | WPF |
| **DB** | LibraNet → `HarmonogramDostaw`, `FarmerCalc.DeclI2-I5, Lapki` |
| **accessMap** | (sprawdzić) |

**Co robi:** Wet wpisuje wyniki badań dostawy: padłe (Padłe), choroby (CH), niezakaźne (NW), zakaźne (ZM), łapki (klasa B). Keyboard (1-4 numpad), auto-refresh 5 min.

**Kto używa:**
- **Lekarz wet** (codziennie rano przy odbiorach)
- **Asia** (w przyszłości) — monitoring wydajności żywca per hodowca

**Bolączki:**
- Brak walidacji liczb (można ujemne, bardzo duże)
- Enum aktywnego pola bez persistencji — restart resetuje
- Brak obsługi duplikatów — jeśli wstawienia są wielokrotne, niejednoznaczny SELECT
- Hardcoded conn string

**Czego brakuje (perspektywa Asi):**
- **Trend per hodowca** — kafelek wpisuje dane, ale do raportu trzeba iść do RaportyHodowcow (#13) — brak skrótu
- **Eksport do dashboardu Asi** — jeśli Asia ma monitorować wydajność, dane lekarza są kluczowym wejściem

**Werdykt:** 🟢 **DOBRY** — prosty CRUD, działa. **Do dopisania pod Asię w Części 3** (raport "padłe per hodowca" jako trend).

---

### 7. 📋 Specyfikacja Surowca (`Specyfikacje`)

| Pole | Wartość |
|---|---|
| **Plik** | `Zywiec/WidokSpecyfikacji/WidokSpecyfikacje.xaml.cs` — **16255 linii** ⚠️⚠️⚠️ |
| **Typ UI** | WPF + Outlook interop + iTextSharp PDF |
| **DB** | LibraNet + sieć (PDF na `\\192.168.0.170\Public\Przel\`) |
| **accessMap** | `[06]` |

**Co robi:** Definiowanie parametrów jakościowych surowca per dostawca, ceny (wolnyrynek/rolnicza/ministerialna/łączona), zakresy wagowe, generowanie PDF specyfikacji, wysyłka emailem przez Outlook. Mapowanie dostawców na pośredników.

**Kto używa:**
- **Tereska** + **Paulina** (była) — generowały specyfikacje przed dostawami
- **Magda** (od poniedziałku) — przejmuje generowanie

**Bolączki:**
- **NAJWIĘKSZY PLIK W REPO — 16,2k linii** — niemal niemożliwe do bezpiecznej zmiany bez regresji
- Outlook interop hardcoded → wymaga **MS Office na każdej maszynie** (instalacja + licencja)
- iTextSharp (legacy, EOL'd) — security advisories
- Network share PDF hardcoded `\\192.168.0.170\Public\Przel\` — brak fallback
- **TODO `Implementacja eksportu do Symfonii`** na linii ~9794
- Wiele static fields bez locka

**Czego brakuje (perspektywa Magdy):**
- **Szablonów** — każda specyfikacja generowana ad hoc, brak "kopiuj z poprzedniej dla tego hodowcy"
- **Walidacji "specyfikacja ↔ kontrakt"** — pod kontraktem hodowcy są warunki (% ubytku, termin płatności) — system nie sprawdza zgodności specyfikacji ze stanem umowy
- **Onboarding** — Magda nie wie kiedy edytować specyfikację a kiedy tworzyć nową

**Werdykt:** 🩹 **DZIAŁA ALE WYMAGA HIGIENY** — krytyczny dług techniczny (16k linii). **Pod Magdę: nie tykać kodu, dodać tylko 1-stronnicową instrukcję** "kiedy specyfikacja, kiedy edycja, kiedy mail".

---

### 8. 🚛 Rozliczenia Avilog (`RozliczeniaAvilog`)

| Pole | Wartość |
|---|---|
| **Plik** | `Avilog/Views/RozliczeniaAvilogWindow.xaml.cs` — **576 linii** |
| **Typ UI** | WPF |
| **DB** | LibraNet (Avilog-specific tables) |
| **accessMap** | (sprawdzić) |

**Co robi:** Tygodniowe zestawienia transportu żywca dla Avilog. Kalkulacja kosztu (stawka × kg, domyślnie 0,119 zł), eksport CSV.

**Kto używa:**
- **Asia** (raz w tygodniu) — wystawia rozliczenie do Avilog

**Bolączki:**
- Hardcoded default stawka `0.119m` (powinna z DB / konfigu)
- Brak walidacji zakresu dat (od > do)
- Brak loading indicator → Asia myśli że się zawiesiło
- CSV bez BOM → polskie znaki w Excelu się rozsypują

**Czego brakuje:**
- **Linkowanie z Matrycą Avilog (#4)** — dane z Matrycy powinny zasilać Rozliczenia
- **Historia stawek** — zmiana 0,119 → 0,125 powinna być datowana, nie nadpisywać

**Werdykt:** 🟢 **DOBRY** dla obecnego flow Asi, **drobne fixy** (BOM, walidacja dat, loading).

---

### 9. 📑 Dokumenty i Umowy (`DokumentyZakupu`)

| Pole | Wartość |
|---|---|
| **Plik** | `WPF/SprawdzalkaUmowWindow.xaml.cs` — **939 linii** |
| **Typ UI** | WPF |
| **DB** | LibraNet (`HarmonogramDostawRepository`) + sieć `\\192.168.0.170\Install\UmowyZakupu` |
| **accessMap** | `[05]` |

**Co robi:** **TO JEST DZISIEJSZE "MIEJSCE NA UMOWY"** — DataGrid z dostawami i statusami umów (Aktywna/Wygasła/Oczekiwanie). Chip filters, search, undo 10s, CSV export.

**Kto używa:**
- **Asia** (sporadycznie) — sprawdza status
- **Tereska** — jak ma czas

**Bolączki:**
- **TO JEST FUNDAMENT KTÓRY TRZEBA ZASTĄPIĆ MODUŁEM KONTRAKTY (Część 4)** — obecny widok mówi tylko "Aktywna/Wygasła", ale **nie ma rejestru umów**, **nie ma numeracji**, **nie ma skanów PDF**, **nie ma alertów wyprzedzających**
- Network path hardcoded — jeśli VPN padnie, kafelek pada
- Brak permission check — każdy może oznaczyć umowę jako aktywną
- Avatar cache bez thread safety

**Czego brakuje (KRYTYCZNIE — to jest cały sens Części 4):**
- **Rejestr kontraktów z numeracją** (1/27, 2/27, ...)
- **Statusy 3-letnich kontraktów ARiMR**
- **Załączniki PDF skan podpisanej umowy**
- **Daty od-do + ile wstawień objętych**
- **Alerty 3 miesiące przed końcem**
- **Dashboard "% surowca pod kontraktem"**

**Werdykt:** 🟡 **DOBRY ALE NIEDOKOŃCZONY** — to jest **MVP kontraktów do rozbudowy w pełny moduł (Część 4)**. Nie wyrzucać — rozbudować.

---

### 10. 📊 Sprawozdania ZSRIR (`Sprawozdania`)

| Pole | Wartość |
|---|---|
| **Plik** | `WPF/SprawozdaniaWindow.xaml.cs` — **913 linii** |
| **Typ UI** | WPF |
| **DB** | HANDEL + LibraNet (multi-source aggregation) |
| **accessMap** | (sprawdzić) |

**Co robi:** Tygodniowe sprawozdania zakupu kurczaka rzeźnego (3 źródła: HANDEL faktury, LibraNet harmonogram, Specyfikacja PDF). Tekst maila + wysyłka do ZSRIR (zsrir.minrol.gov.pl). Skróty F11/F12/F5/Ctrl+S.

**Kto używa:**
- **Asia** (cotygodniowo) — wysyła do ministerstwa
- **Ser** — weryfikuje

**Bolączki:**
- Magic numbers — typ faktur `7`, `8` bez komentarza
- Brak error handling przy `SqlConnection.Open()` → UI hang
- Email template hardcoded w C# → zmiana = recompile
- 2 conn stringi hardcoded
- CSV bez BOM

**Czego brakuje:**
- **GUS R09 jako siostrzany flow** — Asia robi GUS R09 raz w miesiącu (memory: deadline do 8.); Ser obiecał "jedno kliknięcie". Dziś **nie ma kafelka GUS R09** w module Zakup (jest tylko GUS w Produkcji — SprawozdaniaGus dla P-02). Część 3 to zaprojektuje.

**Werdykt:** 🟢 **DOBRY** dla ZSRIR. **Do dodania siostrzany flow R09 w Centrum Asi** (Część 3).

---

### 11. 💵 Rozliczenia z Hodowcami / Płatności (`PlatnosciHodowcy`)

| Pole | Wartość |
|---|---|
| **Plik** | `Platnosci.cs` — **636 linii** |
| **Typ UI** | WinForms (legacy) |
| **DB** | HANDEL → faktury (FV, FK), płatności |
| **accessMap** | (sprawdzić) |

**Co robi:** 2 DataGridView — szczegóły płatności + summary per hodowca. Filtry tekst, "Pokaż wszystkich", kolorowanie przeterminowań, async load z cancellation.

**Kto używa:**
- **Asia** (codziennie/co kilka dni) — kontrola płatności
- **Tereska** — gdy hodowca dzwoni "kiedy mi zapłacicie"
- **Magda** (od poniedziałku) — będzie używać przy telefonach
- **Ser** — kontrola needed-action

**Bolączki:**
- WinForms legacy
- Brak proper async/await — możliwe nullref przy cancel
- Brak paging → spowolnienia z dużymi zbiorami
- Magic number `3` (Top3List)
- Brak komunikatu o błędzie ładowania → silent fail

**Czego brakuje:**
- **Otwarte faktury hodowcy z karty hodowcy** — Magda nie powinna otwierać 2 okien (Baza Hodowców + Płatności) żeby zobaczyć "ile się komu należy"
- **Alert "termin płatności minął"** — pod telefon od hodowcy Magda musi mieć kontekst

**Werdykt:** 🩹 **DZIAŁA ALE WYMAGA HIGIENY** — legacy WinForms, ale dla Magdy najpilniejsze to **link "płatności" z karty hodowcy**.

---

### 12. 🌾 Zakup Paszy i Piskląt (`ZakupPaszyPisklak`)

| Pole | Wartość |
|---|---|
| **Plik** | **NIE ISTNIEJE** |
| **Typ UI** | — |
| **DB** | — |
| **accessMap** | `[01]` (drugi w kolejności! historyczny dług) |

**Co robi:** Nic. `Menu.cs:1540` ma `null` jako FormFactory. Menu sprawdza `if (config.FormFactory != null)` (linia 2242) → kliknięcie **nic nie robi**, ale **kafelek wisi w UI**.

**Kto używa:** Nikt — bo nic nie robi. **Sergiusz obiecał ten moduł** (cytat usera: *"Ser obiecał automatyzację rozliczeń, pasze, pisklaki, listy płac"*).

**Bolączki:**
- **MARTWY KAFELEK od dawna** — zajmuje miejsce w UI, dezorientuje
- Druga oś surowca (pasza, pisklęta) **w ogóle nie śledzone w ZPSP** — Ser nie widzi kosztów

**Czego brakuje:**
- **WSZYSTKO** — od schematu DB (`PaszaDostawa`, `PisklakDostawa`) po UI
- Integracja z dostawcami pasz (TASOMIX, De Heus, Ekoplon) i wylęgarniami

**Werdykt:** 🔴 **MARTWY** — albo **wdrożyć w Q3 2026** (osobny projekt, nie tematy Magdy/ARiMR), albo **ukryć kafelek** żeby Magda się nie pytała "a co to jest". **Quick win na weekend → ukrycie kafelka** (Część 5).

---

### 13. 📊 Statystyki Hodowców (`RaportyHodowcow`)

| Pole | Wartość |
|---|---|
| **Plik** | `Zywiec/RaportyStatystyki/RaportyStatystykiWindow.xaml.cs` — **861 linii** |
| **Typ UI** | WPF |
| **DB** | LibraNet → `Dostawcy`, `HarmonogramDostaw`, `FarmerCalc` |
| **accessMap** | (sprawdzić) |

**Co robi:** Wydajność, jakość, terminowość dostaw per hodowca. Ranking + progi (yellow/red) dla: różnica wag, opaszenie, padłe, konfiskaty. PDF export.

**Kto używa:**
- **Ser** (okazjonalnie) — przy decyzjach o cenach
- **Asia** (w przyszłości) — monitoring wydajności żywca per hodowca (memory: *"w przyszłości monitoring wydajności żywca per hodowca"*)

**Bolączki:**
- Statyczne progi w kodzie (`ProgRoznicaWagZolty`, `ProgOpasienieZolty`) — UI ich nie edytuje
- Brak transaction isolation → race conditions z UpdateProgi
- Brak walidacji zakresu dat
- iTextSharp legacy

**Czego brakuje (perspektywa Asi):**
- **Linkowanie z Kartoteką Hodowcy** — z karty hodowcy nie ma "Pokaż jego ranking"
- **Eksport do PDF dla audytu ARiMR** — w razie kontroli z ARiMR trzeba szybko pokazać "od kogo kupujemy ile, jak wydajnie"

**Werdykt:** 🟢 **DOBRY** baseline, **do rozbudowy pod Centrum Asi** w Części 3.

---

### 14. 🐔 Pozyskiwanie Hodowców (`PozyskiwanieHodowcow`)

| Pole | Wartość |
|---|---|
| **Plik** | `Hodowcy/PozyskiwanieHodowcowWindow.xaml.cs` — **3312 linii** |
| **Typ UI** | WPF |
| **DB** | LibraNet → `Pozyskiwanie_Hodowcy` (1874 leadów), `Pozyskiwanie_Aktywnosci` |
| **accessMap** | `[55]` (zgodnie z memory) |

**Co robi:** CRM leadów — 1874 hodowców z Excela. Statusy: Nowy → Skontaktowany → Zainteresowany → Próbne dostawy → Stały → Odrzucony. 8 szablonów rozmów, mapowanie wojewódzkie po PNA, avatary.

**Kto używa:**
- **Ser** — przeszukuje leadów, dzwoni nowych
- **Tereska/Asia** — sporadycznie

**Bolączki:**
- 🟠 **DUBLUJE się funkcjonalnie z DaneHodowcy (#1)** — są **2 osobne tabele hodowców**: `DOSTAWCY` (aktywni ~140) i `Pozyskiwanie_Hodowcy` (leady ~1874). To częste źródło chaosu w głowie nowego użytkownika. **Magda zobaczy 2 kafelki "Hodowcy" i nie będzie wiedzieć którego użyć.**
- 8 szablonów rozmów hardcoded
- Mapowanie wojewódzkie hardcoded (duży słownik PNA → woj.)
- Brak pagingu — 1874 rekordów na raz w UI
- Brak walidacji email/telefon

**Czego brakuje:**
- **Konwersja "lead → aktywny dostawca"** — jeden klik "ten hodowca jest gotowy, zacznij współpracę" → automatyczne dodanie do `DOSTAWCY` (dziś robi się ręcznie + duplikaty)
- **UI do edycji szablonów rozmów**

**Werdykt:** 🟠 **DUBLUJE FUNKCJĘ** — pod Magdę: **przemianować na "Pozyskiwanie (leady)"** + dodać label kontekstowy "Aktywni hodowcy są w kafelku 'Baza Hodowców'". W Q3 2026 — **scalić obie tabele jeden master + flaga `IsLead/IsActive`**.

---

### 15. 📝 Wnioski o Zmiany (`ZmianyUHodowcow`)

| Pole | Wartość |
|---|---|
| **Plik** | `Hodowcy/AdminChangeRequestsWindow.xaml.cs` — **575 linii** |
| **Typ UI** | WPF |
| **DB** | LibraNet → `DostawcyCR` (workflow change requests), `DostawcyCRItem` (delta pól) |
| **accessMap** | (sprawdzić) |

**Co robi:** Approval flow zmian danych hodowcy — kto zaproponował, co, kiedy, kto zatwierdził. DataGrid + szczegóły delta + status combo + search + F5/Esc shortcuts.

**Kto używa:**
- **Asia** (sporadycznie) — zatwierdza wnioski Tereski/Magdy
- **Ser** — okazjonalnie

**Bolączki:**
- ⚠️ **SQL injection risk** — dynamiczne budowanie WHERE przez StringBuilder (linie ~63-75), brak parameterized queries
- Brak transaction — approve/reject może zostawić inconsistent state (header zaakceptowany, delta nie)
- Brak audit log na poziomie kto/kiedy zatwierdził (kolumny są w DB, ale GUI ich nie wypełnia konsekwentnie)
- Brak null handling na DataGrid selection

**Czego brakuje:**
- **Notyfikacja Asi że ma wniosek do zatwierdzenia** — dziś trzeba pamiętać żeby otworzyć kafelek
- **"Zatwierdź wszystkie z tego batcha"** — bulk approval

**Werdykt:** 🩹 **DZIAŁA ALE WYMAGA HIGIENY** — bezpieczeństwo (SQL injection) + dodać notification badge na kafelku ile pending.

---

## ⚠️ DODATKOWE OBSERWACJE

### Czego BRAKUJE w kategorii (nie ma takich kafelków!)

| Brakujący kafelek | Komu potrzebny | Dlaczego |
|---|---|---|
| **🌾 Pasza i Pisklęta** | Magda, Ser | Druga oś surowca, dziś `null` placeholder |
| **📜 Kontrakty Hodowców** | Asia (główny user), Ser, ARiMR | **CAŁY POWÓD CZĘŚCI 4** |
| **📞 Log rozmów z hodowcami** | Magda, Tereska, Asia | Dziś notatki w `Pozyskiwanie_Aktywnosci` tylko dla leadów; brakuje dla aktywnych z `DOSTAWCY` |
| **📊 GUS R09 (zakup)** | Asia | Robi co miesiąc do 8., dziś ręcznie poza ZPSP |
| **🎯 Dashboard ARiMR Compliance** | Asia, Ser | % surowca pod 3-letnim kontraktem, alarm <50% |
| **🏠 Centrum Asi (kokpit)** | Asia | Jeden widok: alerty, terminy umów, R09, audyt |

---

### Kafelki KRYTYCZNE pod Magdę (priorytet poniedziałek)

W kolejności bólu:
1. 🐣 **Cykle Wstawień** — codzienna główna praca
2. 📅 **Kalendarz Dostaw** — codziennie sprawdza/edytuje
3. 📋 **Specyfikacje** — generuje przy każdej dostawie
4. 💵 **Płatności** — telefony "kiedy mi zapłacicie"
5. 🚛 **AviLog** (Matryca + Rozliczenia) — wpisuje płachty

**Wszystkie 5 — Magda dotknie w pierwszy poniedziałek.**

---

### Kafelki ZBĘDNE / DO UKRYCIA / DO SCALENIA

| Kafelek | Werdykt | Akcja |
|---|---|---|
| 🌾 ZakupPaszyPisklak | MARTWY (null factory) | **Ukryć z UI** do czasu implementacji (Q3 2026) |
| 🐔 Pozyskiwanie Hodowców | DUBLUJE Baza Hodowców | **Przemianować** + label "Tylko leady, aktywni w Baza Hodowców" |
| 🚛 Matryca AviLog + 🚛 Rozliczenia AviLog | OSOBNE ALE POWIĄZANE | **Scalić w 1 grupę** "AviLog (transport)" z 2 zakładkami |

---

### Kafelki DOBRE ALE NIEDOKOŃCZONE (rozbudowa = niska cena, wysoka wartość)

| Kafelek | Brakująca warstwa | Wartość biznesowa |
|---|---|---|
| 🐣 Cykle Wstawień | Załącznik dowodu (mail/SMS/WhatsApp screenshot) | **Wymóg ARiMR + spór z hodowcą** |
| 📅 Kalendarz Dostaw | Kolor "pod kontraktem vs spot" | **Asia widzi 1 spojrzeniem co kontraktowe** |
| 🚛 AviLog | Sens biznesowy danych (raport kosztu/optymalizacja) | **Asia przestaje "wpisywać w pustkę"** |
| 📑 Dokumenty i Umowy | Numeracja / skany PDF / alerty | **PUNKT WYJŚCIA dla modułu Kontrakty (Część 4)** |
| 💵 Płatności | Link z karty hodowcy | **Magda nie otwiera 2 okien** |
| 📊 Statystyki Hodowców | Eksport ARiMR-ready PDF | **W razie kontroli — 1 klik** |

---

### Wspólne grzechy techniczne (powtarzają się w 15/15 kafelków)

1. **Hardcoded connection stringi z plaintext passwordami** — pełna lista: Menu.cs sekcja `connectionString`, każde okno ma swoje
2. **Brak structured logging** — tylko `Debug.WriteLine` (jeśli w ogóle)
3. **Mega code-behind** — 5 z 15 kafelków >2,5k linii w jednym pliku
4. **Brak walidacji wejść numerycznych** — można wszędzie ujemne wagi/ilości/ceny
5. **Brak permission check per okno** — `userPermissions` sprawdzane tylko przy kliknięciu kafelka, ale po otwarciu okna każdy może wszystko edytować

---

## 📌 PODSUMOWANIE CZĘŚCI 1

| Werdykt | Liczba kafelków | Akcja |
|---|---:|---|
| 🟢 DOBRY | 3 | Zostawić, drobne fixy |
| 🩹 DZIAŁA ALE WYMAGA HIGIENY | 6 | Refactor w Q3 (priorytet wg długu) |
| 🟡 DOBRY ALE NIEDOKOŃCZONY | 4 | **Dorobić warstwę wartości** (UX, walidacje, linki) |
| 🟠 DUBLUJE FUNKCJĘ | 1 | Scalić/przemianować |
| 🔴 MARTWY | 1 | Ukryć/wdrożyć |
| **RAZEM** | **15** (+ 1 brakujący "Kontrakty") | |

**Najbardziej krytyczne 3 ruchy:**
1. **Ukryć "Zakup Paszy i Piskląt"** (martwy kafelek myli Magdę) — Część 5
2. **Dorobić "potwierdzenia wstawień"** (dowód = wymóg ARiMR) — Część 4
3. **Zbudować moduł Kontrakty Hodowców** (Asia + ARiMR + sp. z o.o.) — Część 4
