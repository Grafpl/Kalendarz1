# Instrukcja: Umowy i Dokumenty — deep

> **Dla kogo**: Asia (umowy zakupu), Sergiusz (kontrola), księgowość (dokumenty).
> **Co robi**:
> - **Umowy** = śledzenie cyklu życia umów zakupu z hodowcami (3 flagi) + edytor z generatorem DOCX.
> - **Dokumenty** = analiza faktur handlowych z Symfonii (drill-down z raportów + HHI + diagnoza).
> **Pliki kodu**: `WPF/SprawdzalkaUmowWindow.xaml`, `UmowyForm.cs`, `SzczegolyDokumentuWindow.xaml`, `Sprawozdania/Views/DokumentyDrillDownDialog.xaml`.

---

# CZĘŚĆ I: UMOWY

## 1. Dwa okna

| Okno | Plik | Status | Po co |
|---|---|---|---|
| **Sprawdzalka Umów** | `WPF/SprawdzalkaUmowWindow.xaml` (WPF) | aktywny | lista + status, codzienna kontrola |
| **UmowyForm** | `UmowyForm.cs` (WinForms) | **aktywny** | edycja umowy, sugestie, generator DOCX |
| ~~SprawdzalkaUmow~~ | `SprawdzalkaUmow.cs` (WinForms) | legacy (nieużywany) | — |

---

## 2. Sprawdzalka Umów — anatomia

```
┌────────────────────────────────────────────────────────────────────┐
│ 📑 Sprawdzalka Umów                                                │
│ Łącznie: 147 z 200 | ⚠ Przeterminowane: 12 | ⏰ Dziś: 8           │
│ ✅ Kompletne: 47 | 🤝 Pośrednicy: 3                                │
│ [🔍 Szukaj] [📅Dziś][➡Jutro][📆Tydzień][⚠Spóźnione][👤Moje]       │
│ [☑ Tylko niekompletne] [☐ Archiwalne >6 mies]                     │
├────────────────────────────────────────────────────────────────────┤
│ GROUPED BY DataOdbioru:                                            │
│ ▼ 15 maja 2026 (środa) — 4 dostawy                                │
│   Dostawca | ☑Utw | ☑Wys | ☑Otrz | ☑Pośr | Aut | Szt | Waga ...  │
│   Wojtek   |  ☑   |  ☑   |  ☑    |  ☐    |  2  |10560|  2.1  ...   │
│            | Asia | MS   | Asia  |       |                        │
└────────────────────────────────────────────────────────────────────┘
```

### Statystyki górne (formuły)

| Statystyka | Formuła |
|---|---|
| **Przeterminowane** | `!IsKompletna && !IsPosrednik && DataOdbioru < Today` |
| **Dziś do zamknięcia** | `!IsKompletna && !IsPosrednik && DataOdbioru == Today` |
| **Kompletne** | `Utworzone && Wysłane && Otrzymane` |
| **Pośrednicy** | `IsPosrednik` (liczą się jako kompletne) |

### 12 kolumn

Dostawca (badge statusu) · ☑ Utworzona · ☑ Wysłana · ☑ Otrzymana · ☑ Pośrednik (niebieski) · Aut · Sztuki · Waga · Szt/poj · **Utworzył** (avatar + imię + data) · **Wysłał** · **Otrzymał**.

> Checkboxy Utw/Wys/Otrz są **disabled** gdy IsPosrednik=true.

### Grupowanie

Po `DataOdbioru` — banner z gradient (DZIŚ = #FFF5DC→#FFE3A1), chip dnia tygodnia (zielony #5C8A3A), pełna data, liczba dostaw.

### Filtry (chips)

| Chip | Działanie |
|---|---|
| 📅 Dziś | DataOdbioru == Today |
| ➡ Jutro | == Today+1 |
| 📆 Tydzień | bieżący tydzień |
| ⚠ Spóźnione | `< Today && !IsKompletna && !IsPosrednik` |
| 👤 Moje | KtoUtw/Wysl/Otrzym == UserID |

Checkboxy: "Tylko niekompletne", "Archiwalne (>6 mies)" (przeładowuje większy zakres).

🔍 Szukaj: debounce 250ms, w Dostawca/KtoUtw/KtoWysl/KtoOtrzym/DataOdbioru.

---

## 3. Workflow umowy — 3 flagi

```
NIE ISTNIEJE
  → (Asia: UmowyForm, generuje DOCX, akceptuje)
☑ UTWORZONA  [tracking: KtoUtw, KiedyUtw]
  → (Asia: wysyła hodowcy mailem/pocztą)
☑ WYSŁANA    [tracking: KtoWysl, KiedyWysl]
  → (hodowca: podpisuje, odsyła skan)
☑ OTRZYMANA  [tracking: KtoOtrzym, KiedyOtrzm]
= KOMPLETNA ✅ (wszystkie 3)
```

**POŚREDNIK** = dostawa od pośrednika bez umowy. Checkboxy disabled, liczy się jako kompletna, niebieski #3578D9.

### Co dzieje się gdy klikasz checkbox (Flag_Click)

1. Pobranie wartości (Utworzone/Wysłane/Otrzymane/Posrednik).
2. **Potwierdzenie TYLKO gdy COFASZ** (true→false): "Cofnąć 'X' dla {Dostawca}?".
3. SQL UPDATE:
   ```sql
   UPDATE HarmonogramDostaw SET {col}=@new,
     Kto{Kol}=CASE WHEN ISNULL(Kto{Kol},0)=0 THEN @uid ELSE Kto{Kol} END,
     Kiedy{Kol}=CASE WHEN Kiedy{Kol} IS NULL THEN GETDATE() ELSE Kiedy{Kol} END
   WHERE Lp=@lp
   ```
   (Kto/Kiedy zapisują się **tylko raz** — pierwsza osoba.)
4. Audit log: `(Id, Column, OldValue, At)`.
5. Refresh UI + recompute derived.

### Ctrl+Z (30 sekund)

```
if ((Now - _lastToggle.At).TotalSeconds > 30) → "starsze niż 30s, niedostępne"
else → przywróć OldValue
```

### PPM menu

- ✏ Edytuj umowę (UmowyForm).
- 📁 Pokaż plik umowy (Explorer).
- 📜 Pokaż historię zmian (AuditHistoryDialog).
- 📋 Skopiuj LP.

### "Pokaż plik umowy"

Root: `\\192.168.0.170\Install\UmowyZakupu`. Algorytm: exact match `Umowa Zakupu {Dostawca} {D}-{M}-{Y}.docx` → wildcard `*{Dostawca}*.docx` → najstarszy. Otwiera Explorer z `/select`.

### Avatary

Async (Background priority), batche po 25 wierszy, cache `_avatarCache`, Freeze().

---

## 4. UmowyForm — edytor (Libra ↔ Symfonia)

```
┌─────────────────────────────────────────────────────────────────────┐
│ LEWA — LIBRA (hodowca)         │ PRAWA — SYMFONIA (handlowy)        │
│ LP [HarmonogramDostaw ▼]       │ Dostawca Symfonia [▼]             │
│ Dostawca1, IDLibra             │ IDLibraS                          │
│ Adres, NIP, REGON, PESEL,      │ NIPS, REGONS, PESELS, AdresS      │
│ Phone, Email, NrGosp(AnimNo)   │ [Powiąż] (zapisz mapowanie)       │
│ Data Odbioru → Data Podpisania │                                   │
│ (DataOdbioru-2 dni, pomija weekend)                                │
├─────────────────────────────────────────────────────────────────────┤
│ 📅 Specyfikacje z dnia DataOdbioru (wszyscy dostawcy, aktualny żółty)│
│ 💡 SUGEROWANE WARUNKI (z 10 ostatnich dostaw):                      │
│ [💰 Typ ceny: Wolnorynkowa 6/10] [➕ Dodatek: 0.50zł 5/10]          │
│ [📉 Ubytek: 3.5% 7/10] [⚖ Czyja waga: HODOWCA 8/10]                │
│ [🐔 PiK: TAK→Odbiorcę 6/10]                                         │
│ [✓ ZASTOSUJ WSZYSTKIE SUGESTIE]                                     │
└─────────────────────────────────────────────────────────────────────┘
   [Anuluj] [Zapisz (generuje DOCX)]
```

### Sugerowane warunki — algorytm

TOP 10 FarmerCalc dla dostawcy (`WHERE Price>0 ORDER BY CalcDate DESC`). Dla każdego pola: `GroupBy().OrderByDescending(COUNT()).First()`.

5 kart:
1. **Typ ceny** (indigo) — klik → drill-down historia 50 dostaw.
2. **Dodatek** (zielony) — zaokrąglony, zakres min-max.
3. **Ubytek** (pomarańczowy) — %, zakres.
4. **Czyja waga** (fiolet) — **pochodna**: Ubytek>0 → "Hodowca", =0 → "Ubojnia".
5. **PiK** (czerwony) — IncDeadConf: TRUE → "Odbiorcę", FALSE → "Sprzedającego".

### Auto-mapowanie Libra ↔ Symfonia

Zmiana Dostawca1 → `SELECT IdSymf FROM Dostawcy WHERE Name=@name`. Jeśli znajdzie → ustaw comboBoxDostawcaS. "Zapisz mapowanie" → `UPDATE Dostawcy.IdSymf`.

### Generator DOCX (OpenXML)

Template: `\\192.168.0.170\Install\UmowyZakupu\UmowaZakupu.docx`. Output: `Umowa Zakupu {Dostawca} {D}-{M}-{Y}.docx`.

**Placeholdery** (wszystkie):
`[NAZWA]` `[AdresHodowcy]` `[KodPocztowyHodowcy]` `[NIP]` `[WAGA]` `[DataZawarciaUmowy]` `[AdresKurnika]` `[KodPocztowyKurnika]` `[SZTUKI]` `[DataOdbioru]` `[CzyjaWaga]` `[Dodatek]` `[Obciążenie]` `[Ubytek]` `[Cena]` `[PaszaPisklak]` `[Odeslanie]` `[Rolnik]`.

- `[Cena]` → buduje zdanie wg typu ceny (wolna/rolnicza/ministerialna/łączona) + dodatek.
- `[Ubytek]` → "Pomniejszona o X% ubytków transportowych".
- `[PaszaPisklak]` → " + 0.03 zł/kg" lub "".
- `[Rolnik]` → "nie jest rolnikiem ryczałtowym" / "jest rolnikiem".

### Cache (30 min)
`_hodowcyCache` ('Dane hodowców$' z LibraNet), `_kontrahenciCache` (TOP 1 dok/kontrahent z Handlu). Debounce historii 300ms.

---

# CZĘŚĆ II: DOKUMENTY

## 5. Czym są dokumenty

**NIE** dokumenty firmy (certyfikaty BRC) — to **transakcje handlowe z Symfonii** (HM.DK + HM.DP):

| Typ | Co | Kolor (drill-down) |
|---|---|---|
| **FVS** | Faktura sprzedaży | 🟢 #DCFCE7 |
| **FKS** | Korekta sprzedaży | 🟡 #FEF3C7 |
| **PWU** | Przyjęcie z uboju | 🔵 #DBEAFE |
| **PWP** | Przyjęcie z produkcji | 🟣 #EDE9FE |
| **PWK** | Przyjęcie korygujące | 🩷 #FCE7F3 |

---

## 6. DokumentyDrillDownDialog — drill-down z raportów

Wejście: z **Sprawozdania → P02** → klik w liczbę dokumentów.

Parametry: pkwiu, typ (SprzedazMc/SprzedazYtd/ProdukcjaMc/ProdukcjaYtd), rok, miesiąc.

```
┌─────────────────────────────────────────────────────────────────┐
│ Dokumenty: Kurczak żywy | Marzec 2026   [🔍] [📋 Kopiuj][💾 CSV]│
│ DAILY STRIP: ▁▂▃▅▇█▇▅▃▂▁ kg/dzień (sprzedaż niebieski/prod zielony)│
│ # | Data | Typ(pill) | Nr | Kontrahent | KodTowaru | Kg | Wartość│
│ 1 | 01.03| 🟢FVS    | FV/...| Karmar | F-001 | 1200 | 8250        │
│ 2 | 02.03| 🟡FKS    | KOR/..| ZPC    | ...   | -50  | -345        │
│ Stat: 245 dok. · 1450 pozycji · 280k kg · 1.8M zł                │
└─────────────────────────────────────────────────────────────────┘
```

- **Daily strip** — agregacja kg/dzień, wysokość proporcjonalna, tooltip data+kg.
- Filtruj: KodTowaru/NazwaTowaru/Kontrahent/Nr.
- Kopiuj (TSV), CSV (semicolon, UTF-8 BOM).
- Klik dokumentu → SzczegolyDokumentuWindow.

---

## 7. SzczegolyDokumentuWindow — analiza faktury

```
┌─────────────────────────────────────────────────────────────────┐
│ 📄 FV/2026/03/001                                                │
│ Data: 15.05 | Termin: 14.06 | Kontrahent: Karmar | Handlowiec: MS│
│ Netto: 8250 | Brutto: 8910 | Zapłacono: 8910 | Status: ✓Zapłacone│
├─────────────────────────────────────────────────────────────────┤
│ Lp | Kod | Towar | Kg | Cena | Wartość | Udział%                 │
│  1 |F-001| Filet | 450| 12.00| 5400    | 65%                     │
│  2 |S-002| Skrzyd| 200| 8.00 | 1600    | 19%                     │
├─────────────────────────────────────────────────────────────────┤
│ Razem: 850 kg, 8250 zł | Pozycji: 4 | Śr cena: 9.70             │
│ 🥇 Filet 5400 (65%) 🥈 Skrzydła 1600 (19%) 🥉 Nogi 900 (11%)    │
│ HHI: 49% (umiarkowana koncentracja)                             │
│ Rozpiętość cen: 6.00-12.00 zł/kg                                │
│ Diagnoza: 💡 Bogaty asortyment, faktura zrównoważona            │
└─────────────────────────────────────────────────────────────────┘
```

### Status płatności (z DniDoTerminu)

| Warunek | Wyświetla |
|---|---|
| (Brutto - Zapłacono) ≤ 0.01 | ✓ Zapłacone |
| Termin > dziś | "{dni} dni" (niebieski) |
| DniDoTerminu == 0 | ⚠ Termin dziś (pomarańczowy) |
| DniDoTerminu < 0 | ⚠ Po terminie (-X dni) (czerwony) |

### HHI (Herfindahl-Hirschman Index)

`HHI = SUM[(udział)²] × 100` — koncentracja wartości:
- **>50%** → ⚠ Wysoka koncentracja (dominuje kilka pozycji) — czerwony.
- **25-50%** → ✓ Umiarkowana — pomarańczowy.
- **<25%** → ✓ Równomierny rozkład — zielony.

> 💡 Wysokie HHI = ryzyko (jak klient odmówi 1 pozycji, faktura znika).

### Auto-diagnoza

```
pozycji==1 → "Tylko jedna pozycja"
hhi>50 → "Zdominowana przez główną pozycję"
pozycji>10 → "Bogaty asortyment"
srednia>500 → "Pozycje o wysokiej wartości"
else → "Standardowy zestaw"
```

---

## 8. Auto-import / synchronizacja

- **Umowy**: brak auto-sync. Ręczne w UmowyForm. "Pokaż plik" → UNC path sieciowy.
- **Dokumenty**: źródło HM.DK + HM.DP, live query (bez cache, świeże dane).

---

## 9. Typowy dzień

### Asia (umowy, 09:00)
```
09:00  Sprawdzalka → filtr "Spóźnione" → 12 niedokończonych.
09:05  Wójcik (dostawa 5 dni temu, 0 checkboxów):
       PPM → Edytuj → UmowyForm → wybierz LP → "Zastosuj sugerowane" → Zapisz (DOCX).
       Drukuje, podpisuje, skanuje, wysyła mailem.
       Zaznacza ☑ Utworzona + ☑ Wysłana.
09:30  Krzysiek (wysłana, czeka na podpis):
       Dzwoni → "odesłałem wczoraj" → znajduje skan w mailu → ☑ Otrzymana = kompletna ✅
10:00  Filtr "Dziś" → 3 dostawy, wszystkie 3 checkboxy ✅
10:30  Nowy hodowca → "Nowa umowa" → UmowyForm → brak historii → wpisuje ręcznie → DOCX.
```

### Sergiusz (dokumenty, początek miesiąca)
```
1. Sprawozdania → P02 → raport miesięczny.
2. Top 5 wyrobów → drill-down dokumentów.
3. Per dokument: HHI, top pozycja, status płatności.
```

---

## 10. FAQ

### Umowy
**P: Hodowca odmawia podpisu?** → Edytuj → odznacz Utworzona + notatka w historii.
**P: Gdzie szablon umowy?** → `\\192.168.0.170\Install\UmowyZakupu\UmowaZakupu.docx`. Zmiany = konsultacja z prawnikiem!
**P: Nowy placeholder?** → System zna tylko zdefiniowane. Nowy = zmiana w UmowyForm.cs (dev).
**P: Sugerowane warunki — kto wprowadza?** → System uczy się z 10 ostatnich umów. Nikt.
**P: Ctrl+Z nie działa?** → Po 30s zamrożone. Admin może cofnąć.
**P: "Spóźnione"?** → DataOdbioru minęła, brak 3 checkboxów.

### Dokumenty
**P: Otworzyć dokumenty bez raportu?** → Nie. Tylko drill-down z Sprawozdania P02.
**P: PDF faktury?** → ZPSP nie przechowuje. Pobierz z Symfonii.
**P: HHI=90%?** → Faktura zdominowana 1 pozycją. Ryzykowne.
**P: Brak niektórych dokumentów?** → Filtruje wg PKWIU z raportu.

---

## 11. Podsumowanie

| Moduł | Po co | Tech | Stan |
|---|---|---|---|
| SprawdzalkaUmowWindow | Status dostaw + flagi | WPF | aktywny |
| UmowyForm | Edycja + sugestie + DOCX | WinForms | aktywny |
| SzczegolyDokumentuWindow | Analiza faktury + HHI | WPF | aktywny |
| DokumentyDrillDownDialog | Drill-down P02 + daily strip | WPF | aktywny |
| SprawdzalkaUmow | — | WinForms | legacy |

> ⚠ Umowy = **proces fizyczny** (Word, podpis, skan), system śledzi status. Dokumenty = **dane z Symfonii**, system czyta i analizuje. Żadnego ZPSP nie generuje sam (poza DOCX umowy z szablonu).

---

## 12. Co dalej

- **Baza hodowców** (skąd hodowcy) → `06_Baza_Hodowcow.md`.
- **Kalendarz dostaw** (skąd LP HarmonogramDostaw) → `05_Kalendarz_Dostaw_Zywca.md`.
- **Reklamacje** (korekty faktur) → `04_Reklamacje.md`.
- **Sprawozdania P02** (skąd drill-down) → *TODO instrukcja*.
