# Jak utworzyć szablon Word z bookmarkami (dla Asi + prawniczki)

## Co to są bookmarki (zakładki) w Wordzie

**Bookmark** to niewidoczna etykieta w tekście Worda. Generator (`WordTemplateService.cs`) szuka tych etykiet i podstawia wartości z bazy. Asia widzi gotowy dokument bez konieczności tykania kodu.

---

## Krok po kroku — utworzenie szablonu

### 1. Otwórz **plik tekstowy** `Umowa_ARIMR_3LAT_template.txt` w folderze `Szablony_Word/`.

To **wzorcowy tekst umowy** z markerami w stylu `{{bm_NumerKontraktu}}`. Asia + prawniczka:
- Czytają, korygują treść prawniczo
- **Zostawiają markery `{{bm_xxx}}`** w nienaruszonym stanie

### 2. Wklej cały tekst do **Microsoft Word** (nowy dokument).

### 3. Sformatuj wizualnie:
- Nagłówki, czcionki, marginesy — jak ma wyglądać po wydrukowaniu
- Logo Piórkowscy w nagłówku (jeśli chcesz)
- Stopka z datą wydruku i numerem strony

### 4. Zamień każdy marker `{{bm_xxx}}` na **bookmark Worda**:

#### Dla każdego markera:
1. **Zaznacz cały tekst markera** wraz z nawiasami, np. `{{bm_NumerKontraktu}}`
2. **Naciśnij Delete** (usuwa marker, kursor w jego miejscu)
3. Menu **Wstawianie → Zakładka** (skrót: `Ctrl+Shift+F5`)
4. W oknie wpisz **dokładnie tę samą nazwę** co marker (bez `{{}}`), np. `bm_NumerKontraktu`
5. Klik **Dodaj** → **Zamknij**

Powtórz dla wszystkich markerów (jest ich ~15).

### 5. Zapisz dokument jako:
```
\\192.168.0.170\Install\UmowyZakupu\_SZABLON\Umowa_ARIMR_3LAT.docx
```

### 6. Test: w ZPSP klik prawym na dowolny kontrakt → **"📄 Generuj Word"** → Word otwiera się z podstawionymi wartościami.

---

## Lista bookmarków do utworzenia w `Umowa_ARIMR_3LAT.docx`

| Bookmark | Co podstawia (przykład) |
|---|---|
| `bm_NumerKontraktu` | `1/27` |
| `bm_DataPodpisania` | `26 maja 2027` |
| `bm_NazwaHodowcy` | `Jan Kowalski` |
| `bm_AdresHodowcy` | `ul. Wiejska 12, 12-345 Wieś` |
| `bm_Nip` | `1234567890` |
| `bm_NrGospodarstwa` | `PL12345678` |
| `bm_ProcentUbytku` | `3,00 %` |
| `bm_TypCeny` | `wolnorynkowa` |
| `bm_Cena` | `7,50 zł/kg netto` |
| `bm_TerminPlatnosci` | `21 dni` |
| `bm_DataOd` | `1 czerwca 2027` |
| `bm_DataDo` | `31 maja 2030` (lub `na czas nieokreślony`) |
| `bm_OkresWypowiedzenia` | `90 dni` |
| `bm_NazwaPiorkowscy` | `Piórkowscy sp. z o.o.` (po 01.08.2026) |
| `bm_RozliczanaWaga` | `waga netto deklarowana przez Hodowcę` |

---

## Jak sprawdzić czy bookmarki są dobrze ustawione

1. W Wordzie: **Plik → Opcje → Zaawansowane** → przewiń do "Pokaż zawartość dokumentu" → **zaznacz "Pokaż zakładki"** → OK
2. Teraz każdy bookmark będzie widoczny jako **`[...]`** lub kursor wstawienia
3. Sprawdź czy są wszystkie z listy powyżej

---

## Tworzenie kolejnych szablonów (warianty)

Po przygotowaniu `Umowa_ARIMR_3LAT.docx`:

1. **`Umowa_Wieczna.docx`** — kopia + zmień zapisy: "czas nieokreślony", "wypowiedzenie 90 dni"
2. **`Umowa_Roczna.docx`** — kopia + zmień: "okres obowiązywania 12 miesięcy z możliwością przedłużenia"
3. **`Umowa_Spot.docx`** — kopia + zmień: "umowa jednorazowa, dotyczy dostawy z dnia bm_DataOd"

Wszystkie wgrać do tego samego folderu `_SZABLON\`.

---

## Co się dzieje gdy bookmark jest źle nazwany

- Generator po prostu **nie podstawi wartości** dla tego bookmarka — w wygenerowanym Wordzie zostanie pusty
- Asia poprawia w Wordzie ręcznie po wygenerowaniu (jednorazowo)
- Następnym razem — popraw bookmark w szablonie (dwuklik na zakładkę w panelu "Zakładki" → zmień nazwę)

---

## Co się dzieje gdy chcesz dodać nowe pole (np. "ilość minimalna sztuk/cykl")

1. Asia decyduje że potrzebne nowe pole
2. Dodaj **kolumnę w `dbo.Kontrakty`** (jest już `MinimalnaIlosc`) — sprawdź czy istnieje
3. W `WordTemplateService.BuildValuesFromKontrakt`:
   ```csharp
   ["bm_MinimalnaIlosc"] = k.MinimalnaIlosc?.ToString() ?? "(brak limitu)"
   ```
4. W szablonie Word dodaj bookmark `bm_MinimalnaIlosc` w odpowiednim miejscu
5. Build, test, gotowe

---

## ⚠️ Ważne uwagi

- **Bookmark NIE może mieć spacji ani polskich znaków** w nazwie — tylko `[a-zA-Z0-9_]`
- **Bookmark NIE może zaczynać się od cyfry**
- Word czasem **nie pokazuje bookmarków** które nie obejmują żadnego znaku — sprawdzaj przez Wstawianie → Zakładka → lista
- **Po pierwszym wygenerowaniu** Word w pliku `outputPath` ma dodane Runy z wartościami **PO** bookmarkach. To OK — Word renderuje poprawnie.
- **Nie usuwaj szablonu** po wygenerowaniu pojedynczego dokumentu — generator za każdym razem **kopiuje** szablon

---

*Wersja 1.0 • 24.05.2026 • Asia + prawniczka tworzą szablony, Ser oprogramowuje*
