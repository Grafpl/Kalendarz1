# Część 2 — Rekomendacje pod wdrożenie Magdy (poniedziałek 26.05.2026)

**Profil Magdy:** zna firmę od lat, pracowała w innym dziale, **stresuje się** wchodząc w zakup. Codziennie dotknie: wstawienia, potwierdzenia, AviLog, umowy zakupu, faktury hodowców, specyfikacje. W średnim terminie (po odejściu Tereski) — telefony do hodowców i negocjacje.

**Otoczenie wspierające:** Tereska (jeszcze jest), Asia (kolega z biurka), Ser (programista-CEO). Magda nie zostaje sama.

**Zasada:** każda decyzja UX patrzy "co Magda zobaczy w pierwszym tygodniu" — nie "jak to powinno działać docelowo".

---

## A. Top 5 kafelków Magdy — co uprościć w tygodniu 1

### A.1 🐣 Cykle Wstawień (`WidokWstawienia.xaml.cs`)

| Co zrobić | Effort | Pilność |
|---|---|---|
| Banner pod tabelą: *"💡 Dzień 0 to dzień gdy hodowca odebrał pisklęta. System sam wyliczy datę uboju (35-42 dni)"* | 1h | **PILNE** |
| Tooltip na kolumnie "Sztuki" przy hoverze: *"Liczba sztuk wstawionych. Typowy kurnik 25 000 - 40 000"* | 30 min | **PILNE** |
| **Walidator** liczby sztuk: 0 < sztuki ≤ 250 000 (poza tym warning "Czy na pewno?") | 1h | **PILNE** |
| **Walidator** wagi piskląt: 30 g ≤ waga ≤ 60 g (poza tym warning) | 30 min | średnie |
| Field "Potwierdzenie hodowcy": dropdown [Brak / SMS / Mail / WhatsApp / Telefon] + textbox "Numer/link" + przycisk "📎 Załącz screenshot" | 4h | **PILNE pod ARiMR** |
| Filter na liście "tylko bez potwierdzenia" — pomaga Magdzie nadgonić zaległości | 1h | średnie |

**Cel:** Magda widzi czego się trzyma (banner), nie wpisze 100k sztuk przez pomyłkę, i ma miejsce na dowód.

---

### A.2 📅 Kalendarz Dostaw Żywca (`WidokKalendarzaWPF.xaml.cs` — 9,6k linii ⚠️)

| Co zrobić | Effort | Pilność |
|---|---|---|
| **Legenda kolorów** w nagłówku okna (dziś użytkownik domyśla się sam) | 30 min | **PILNE** |
| Tooltip po hoverze na komórce dnia: *"Hodowca X, 5 aut, 18 000 sztuk, godz. 6:30 — kliknij dwukrotnie żeby otworzyć"* | 2h | **PILNE** |
| Banner ostrzegawczy gdy Magda chce edytować dostawę z dnia wczorajszego/przeszłego: *"⚠️ Edytujesz historyczną dostawę. Wymagana zgoda Asi"* | 2h | średnie |
| Mini-statusbar dolny: *"Załadowano: 47 dostaw | Ostatni refresh: 14:23 | Live: ●"* — Magda wie że jest na bieżąco | 1h | niska |
| **Naprawić TODO drukowania PDF** (linia 11247) — Magda dzwoni do hodowcy i chce wysłać wydruk | 4h | wysoka |

**Cel:** Magda nie boi się klikać. Wie co jest co.

---

### A.3 📋 Specyfikacje Surowca (`WidokSpecyfikacje.xaml.cs` — 16,2k linii ⚠️⚠️⚠️)

**Reguła:** **NIE TYKAĆ KODU.** 16k linii = każda zmiana to ruletka. Wszystko co dla Magdy — robimy *obok*.

| Co zrobić | Effort | Pilność |
|---|---|---|
| **Quick action button** w pasku: *"📋 Skopiuj z poprzedniej dla tego hodowcy"* — clone last spec, otwórz w edycji | 4h | **PILNE** |
| **Banner kontekstowy** w nagłówku: *"💡 Wybierz hodowcę → system pokaże specyfikację z ostatniego miesiąca. Edytujesz tylko co się zmieniło."* | 30 min | **PILNE** |
| Walidator: cena > 0, zakres wagowy `low < high`, hodowca z listy `DOSTAWCY` | 2h | wysoka |
| Wyłączyć przycisk "Wyślij email" gdy Outlook nie jest zainstalowany (lub fallback do `mailto:` z PDF w załączniku) | 3h | średnie |

**Cel:** Magda nie pisze specyfikacji od zera. 99% przypadków = "to samo co poprzednio + nowa data".

---

### A.4 💵 Rozliczenia z Hodowcami / Płatności (`Platnosci.cs` — WinForms legacy)

| Co zrobić | Effort | Pilność |
|---|---|---|
| **Read-only banner w nagłówku:** *"❗ Płatności pochodzą z Symfonii — tu tylko podgląd. Zmiany robi księgowa (RB/MSS)."* | 30 min | **PILNE** |
| **Filtr "tylko zaległe"** (chip button) — Magda klika i widzi 5 hodowców do kogo dzwonić | 2h | **PILNE** |
| Kolumna "Telefon hodowcy" + przycisk "📞" obok każdego wiersza zaległego — Magda dzwoni nie wychodząc z okna | 3h | wysoka |
| **Pole notatki przy hodowcy**: *"Hodowca obiecał zapłatę do 2026-05-27 (rozmawiała Magda)"* — zapis do nowej tabelki `PlatnosciNotatki` | 4h | średnie |
| Komunikat błędu gdy SQL fail (dziś silent fail) | 1h | wysoka |

**Cel:** Magda nie panikuje gdy hodowca dzwoni "kiedy mi zapłacicie?" — widzi listę, dzwoni, notuje, zamyka.

---

### A.5 🚛 AviLog (Matryca + Rozliczenia jako 1 grupa)

| Co zrobić | Effort | Pilność |
|---|---|---|
| **Banner w Matrycy:** *"💡 To co tu wpiszesz, leci do 'Rozliczenia Avilog' (tygodniowe zestawienie kosztów). Sprawdzaj raz w tygodniu razem z Asią."* — Magda widzi sens danych | 1h | **PILNE** |
| Walidacja: pojazd + kierowca obowiązkowe (dziś można zapisać puste) | 2h | wysoka |
| **W Rozliczeniach Avilog dodać "Refresh z Matrycy"** — przycisk który ładuje świeże dane z `HarmonogramDostaw` (dziś userzy nie wiedzą czy widzą stare/nowe) | 2h | wysoka |
| Loading indicator (dziś Asia myśli że okno się zawiesiło) | 1h | wysoka |
| **CSV z BOM** (polskie znaki) | 30 min | niska |

**Cel:** Magda rozumie *po co* wpisuje dane do Matrycy. Asia wreszcie nie mówi *"wpisuję ale nie mam korzyści"*.

---

## B. Walidacje / ściągi w UI — pełna lista pod Magdę

### B.1 Walidacje numeryczne (powtarzający się grzech 15/15 kafelków)

Wprowadzić **wspólny WPF ValidationRule** (np. `Validators/NumericRangeRule.cs`) i zastosować punktowo:

```csharp
// Pseudokod — przykład ranges per pole
new NumericRangeRule { Min = 0, Max = 250_000, Warning = "Typowy kurnik 25-40 tys. Wprowadziłaś {value}." }
```

| Kafelek | Pole | Min | Max | Co robić poza zakresem |
|---|---|---:|---:|---|
| Wstawienia | Sztuki | 1 | 250 000 | Warning dialog "Czy na pewno?" |
| Wstawienia | Waga pisklęta (g) | 30 | 60 | Warning |
| Portiernia | Brutto (kg) | 1 000 | 45 000 | **Blok** (max ciężarówka) |
| Portiernia | Tara (kg) | 5 000 | 20 000 | Warning |
| Portiernia | Brutto > Tara | — | — | **Blok** |
| Lekarz wet | Padłe, CH, NW, ZM | 0 | (Sztuki z wstawienia) | Warning |
| Specyfikacje | Cena (zł/kg) | 0,01 | 50,00 | Warning |
| Specyfikacje | Zakres wagi low < high | — | — | **Blok** |
| AviLog | Kg na kursie | 1 | 30 000 | Warning |
| Płatności | (read-only, brak walidacji) | — | — | — |

### B.2 Ściągi kontekstowe (info-bannery w XAML)

Wszystkie jako jeden styl `InfoBanner` w `App.xaml` — wystarczy raz zaprojektować, zastosować wszędzie:

```xml
<!-- Wystarczy w App.xaml -->
<Style x:Key="InfoBanner" TargetType="Border">
  <Setter Property="Background" Value="#E3F2FD"/>
  <Setter Property="BorderBrush" Value="#1976D2"/>
  <Setter Property="BorderThickness" Value="0,0,0,2"/>
  <Setter Property="Padding" Value="12,8"/>
</Style>
```

Lista bannerów do założenia (per kafelek):

| Kafelek | Treść bannera |
|---|---|
| Wstawienia | *"💡 Dzień 0 = data odebrania piskląt. System wyliczy datę uboju. Zawsze pytaj o potwierdzenie SMS/mail."* |
| Kalendarz | *"💡 Dwuklik na komórce = otwórz dostawę. Prawy klik = menu. Niebieski = pod kontraktem (gdy moduł Kontrakty wdrożony)."* |
| Specyfikacje | *"💡 Najpierw wybierz hodowcę → zobaczysz poprzednią specyfikację → kliknij '📋 Skopiuj' → edytuj co się zmieniło → 📧 wyślij."* |
| Płatności | *"❗ Tylko podgląd. Zmiany w fakturach robi księgowa. Tu możesz tylko zanotować obietnicę zapłaty."* |
| Matryca AviLog | *"💡 Wprowadź płachtę z Avilog → dane idą do Rozliczeń tygodniowych. Sprawdzaj z Asią w piątek."* |
| Baza Hodowców | *"💡 ~140 aktywnych hodowców. Leady (potencjalni) są w osobnym kafelku 'Pozyskiwanie Hodowców'."* |
| Pozyskiwanie Hodowców | *"💡 Tu są tylko **leady** (1874 potencjalnych). Aktywni dostawcy → kafelek 'Baza Hodowców'."* |
| Wnioski Zmian | *"💡 Wpiszesz wniosek → Asia zatwierdza/odrzuca. Nie zmieniaj bezpośrednio Bazy Hodowców."* |

### B.3 Pytania pułapki (placeholdery w polach)

Małe rzecz, duża wartość — Magda widzi co ma wpisać:

| Pole | Placeholder |
|---|---|
| Wstawienia → Notatka | *"np. Pisklęta odebrane 6:00, hodowca potwierdził SMS-em o 8:14"* |
| Specyfikacje → Komentarz | *"np. Tygodniowo, max 5 aut, czwartek-piątek"* |
| Wnioski Zmian → Powód | *"np. Hodowca zadzwonił 2026-05-23, zmienił NIP po przekształceniu w sp. z o.o."* |
| Płatności → Notatka | *"np. Hodowca obiecał zapłatę do 2026-05-30"* |

---

## C. Automatyzacje — priority order (effort vs wartość)

Ser obiecał: *"rozliczenia, pasze, pisklaki, listy płac"*. Plus to co Magda realnie odciąży.

### C.1 RANK A — wdrożyć w 2 tygodnie (do ~10.06)

| # | Co zautomatyzować | Czas | Wartość | Komentarz |
|---|---|---|---|---|
| **A1** | **Auto-szkic specyfikacji** — przycisk "Skopiuj z poprzedniej dla hodowcy X" | 1 dzień | ⭐⭐⭐⭐⭐ | Magda 80% przypadków = clone+edit |
| **A2** | **Auto-fill ZSRIR sprawozdanie** — przygotowane do wysyłki w piątek 16:00, Asia/Magda klikają tylko "Wyślij" | 2 dni | ⭐⭐⭐⭐ | Memory: BAZA_WIEDZY/29_ZSRIR_Integracja.md już opisuje |
| **A3** | **Auto-link wstawienie → planowana data dostawy** — sprawdzić czy działa, jeśli nie — dorobić | 4h | ⭐⭐⭐⭐ | Jeśli już działa, dodać banner "system zaplanował dostawę na 2026-07-05" |
| **A4** | **Auto-alarm "wstawienie bez potwierdzenia po 24h"** — banner na ekranie Magdy + email do Asi | 1 dzień | ⭐⭐⭐⭐ | Pod ARiMR ważne |
| **A5** | **Ukryć martwy kafelek "Pasza i Pisklęta"** | 15 min | ⭐⭐⭐ | Quick win, mniej zamieszania |

### C.2 RANK B — wdrożyć w 1 miesiąc (do ~25.06)

| # | Co zautomatyzować | Czas | Wartość | Komentarz |
|---|---|---|---|---|
| **B1** | **GUS R09 auto-gen** — Ser obiecał Asi "jedno kliknięcie" | 3-5 dni | ⭐⭐⭐⭐⭐ | Pełna Część 3 |
| **B2** | **Import SMS/Email potwierdzenia** (przeciągnij screenshot na ekran) | 2-3 dni | ⭐⭐⭐⭐ | OCR opcjonalnie |
| **B3** | **Dashboard ARiMR Compliance** — % surowca pod kontraktem | 2 dni | ⭐⭐⭐⭐⭐ | Część 4 |
| **B4** | **Rozliczenia AviLog auto-refresh z Matrycy** — przycisk "Synchronizuj" | 4h | ⭐⭐⭐ | |
| **B5** | **Notification badge** na kafelku "Wnioski Zmian" gdy są pending | 4h | ⭐⭐ | Asia widzi 3 do zatwierdzenia |

### C.3 RANK C — Q3 2026 (większe projekty)

| # | Co zautomatyzować | Czas | Wartość |
|---|---|---|---|
| **C1** | **Moduł Pasza i Pisklęta** — placeholder do faktycznej implementacji | 2-3 tygodnie | ⭐⭐⭐⭐ |
| **C2** | **Lista płac integration** — automatyczne kalkulacje | 3-4 tygodnie | ⭐⭐⭐ |
| **C3** | **Refactor Specyfikacji** (16k linii → modular) | 4-6 tygodni | ⭐⭐⭐⭐ |
| **C4** | **Refactor Kalendarza** (9,6k linii → MVVM partial) | 4 tygodnie | ⭐⭐⭐ |
| **C5** | **Scalenie 2 baz hodowców** (`DOSTAWCY` + `Pozyskiwanie_Hodowcy`) → jedna z flagą `IsLead/IsActive` | 2 tygodnie | ⭐⭐⭐⭐ |

---

## D. Lista 1-stronnicowych instrukcji per kafelek (do napisania w osobnym kroku)

Spis tematów (po jednej stronie, screenshoty + bullet pointy, **Magda powinna umieć przeczytać przy poniedziałkowej kawie**):

### D.1 Kategoria "PIERWSZE KROKI"
1. ✏️ **"Pierwszy poniedziałek Magdy"** — orientacja w menu + co kliknąć żeby się zorientować
2. ✏️ **"Telefony hodowców i WhatsApp — kogo zapytać, jak się przedstawić"**
3. ✏️ **"Eskalacja: kogo pytać o co"** (Tereska, Asia, Ser, Justyna)
4. ✏️ **"Słowniczek skrótów"** (DEK, NW, CH, ZM, PNA, FK, FV, MM-, MM+, IRZplus, ZSRIR)

### D.2 Kategoria "CODZIENNE OPERACJE"
5. ✏️ **"Jak wpisać nowe wstawienie"** — z polem potwierdzenia (krok po kroku)
6. ✏️ **"Jak sprawdzić kalendarz dostaw na dziś / jutro / tydzień"**
7. ✏️ **"Jak wygenerować specyfikację z szablonu poprzedniej"**
8. ✏️ **"Co kliknąć w panelu AviLog gdy przychodzi płachta"**
9. ✏️ **"Co robić gdy hodowca dzwoni o płatność"** — flow telefoniczny

### D.3 Kategoria "WYJĄTKI"
10. ✏️ **"Co robić gdy hodowca prosi o zmianę danych"** (wniosek do Asi)
11. ✏️ **"Co robić gdy hodowca chce odejść / zerwać współpracę"**
12. ✏️ **"Co robić gdy hodowca przysyła pisklęta w innej liczbie niż umówiono"**
13. ✏️ **"Co robić gdy waga przyjętego żywca nie zgadza się z wstawieniem"**
14. ✏️ **"Co robić gdy lekarz wet wpisał padłe > 5% — kogo informować"**

### D.4 Kategoria "TYGODNIOWE / MIESIĘCZNE"
15. ✏️ **"Piątek 16:00 — sprawdzenie sprawozdania ZSRIR z Asią"**
16. ✏️ **"Pierwszy dzień miesiąca — checklist zakupowy"**

**Razem: 16 instrukcji, ~16 stron**. Da się napisać 8 w weekend (priorytet D.1 + D.2), pozostałe w następnych 2 tygodniach.

**Rekomendowane miejsce trzymania:** `BAZA_WIEDZY/INSTRUKCJE_MAGDA/` + drukowane na blacie Magdy w segregatorze.

---

## E. "Quick check przy starcie zmiany" — ekran startowy Magdy

Pomysł na **prosty dashboard** który Magda otwiera rano i widzi 1 spojrzeniem:

```
┌─────────────────────────────────────────────────────────────────┐
│  🌅 Dzień dobry Magda • Poniedziałek 26.05.2026 • 7:42         │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  📋 DO ZROBIENIA DZIŚ                                            │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │ ⚠️  3 wstawienia z piątku — BRAK POTWIERDZENIA           │  │
│  │     → Klikni żeby zobaczyć i dzwonić do hodowców         │  │
│  │                                                            │  │
│  │ 📞  5 hodowców z zaległymi płatnościami >7 dni            │  │
│  │     → Najstarsze: KOWALSKI 2026-05-12 (14 dni)           │  │
│  │                                                            │  │
│  │ 📋  Specyfikacja na dziś dla NOWAK BIS (godz. 9:00)      │  │
│  │     → Klikni "Skopiuj z poprzedniej + wyślij"            │  │
│  │                                                            │  │
│  │ 🚛  Płachta AviLog z piątku NIEWPISANA                   │  │
│  │     → Wpisz przed 12:00, Asia robi rozliczenia o 14:00  │  │
│  └──────────────────────────────────────────────────────────┘  │
│                                                                  │
│  📊 KALENDARZ DZIŚ                                               │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │  6:00  KOWALSKI    5 aut, 18 000 szt    Pojechał ✅      │  │
│  │  9:00  NOWAK BIS   3 aut, 12 000 szt    Planowane ⏳     │  │
│  │ 13:00  JANKOWSKI   4 aut, 16 000 szt    Planowane ⏳     │  │
│  └──────────────────────────────────────────────────────────┘  │
│                                                                  │
│  💬 OD ZESPOŁU                                                   │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │  @Tereska wczoraj 16:00: "Hodowca CYBULSKI dzwoni o      │  │
│  │   zmianę terminu na piątek — wpisz wniosek do Asi"      │  │
│  └──────────────────────────────────────────────────────────┘  │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

**Effort:** 2-3 dni (re-use `WPF/DashboardWindow.xaml.cs` — już 5,6k linii dashboardu, dodać "tryb Magda").
**Wartość:** ⭐⭐⭐⭐⭐ — Magda otwiera ZPSP rano i wie co robić bez pytania.

---

## F. CHECKLIST przed-poniedziałkowy dla Sera

Minimum jakie powinien zrobić w weekend (szczegóły w **Części 5**):

- [ ] Ukryć martwy kafelek "Zakup Paszy i Piskląt" (15 min)
- [ ] Dodać 8 info-bannerów do top 5 kafelków (4-6h)
- [ ] Walidator sztuk wstawienia 0-250k (1h)
- [ ] Banner read-only w Płatnościach (30 min)
- [ ] Filter "tylko bez potwierdzenia" w Wstawieniach (1h)
- [ ] Napisać instrukcje D.1 (4 stron orientacyjnych) (3h)
- [ ] Wydrukować + segregator na biurko Magdy

**Razem ~12h weekendowej pracy** — realne do soboty + niedzieli.

---

## 📌 PODSUMOWANIE CZĘŚCI 2

| Akcja | Effort | Pilność | Termin |
|---|---|---|---|
| **A. UX uproszczenia top 5 kafelków** | ~30h | wysoka | 2 tyg. |
| **B. Walidacje numeryczne + bannery** | ~15h | wysoka | weekend + 1 tydz. |
| **C. Automatyzacje RANK A** (5 funkcji) | ~5 dni | wysoka | do 10.06 |
| **C. Automatyzacje RANK B** (5 funkcji) | ~10 dni | średnia | do 25.06 |
| **D. Instrukcje 1-stronicowe** | 16 dokumentów | wysoka | weekend (8) + 2 tyg. (8) |
| **E. Dashboard "Quick check Magda"** | 2-3 dni | średnia | do 10.06 |
| **F. Weekend pre-flight** | ~12h | **KRYTYCZNE** | sob+niedz. |

**Reguła #1:** Magda nie powinna w pierwszy poniedziałek zobaczyć żadnego okna bez bannera kontekstowego.
**Reguła #2:** Każda automatyzacja RANK A musi mieć "opt-out" — Magda może wyłączyć i zrobić ręcznie (zaufanie do systemu nie ma być wymuszone).
**Reguła #3:** Nie tykać 5 plików-monstrów (Specyfikacje, Kalendarz, Wstawienia, Portier, Pozyskiwanie) bez code-review Sera.
