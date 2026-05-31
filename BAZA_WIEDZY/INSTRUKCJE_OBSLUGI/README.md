# INSTRUKCJE_OBSLUGI — przewodniki krok po kroku dla użytkowników

> Tu trzymamy **instrukcje obsługi** poszczególnych modułów/okien ZPSP. Dla użytkowników (Justyna, Maja, Jola, Marcin, Sergiusz), nie dla deweloperów.

## Pliki

| Plik | Co | Główne okno w kodzie | Dla kogo |
|---|---|---|---|
| `01_Wstawienia_Kurczakow.md` | Edytor **jednego cyklu** wstawienia + dostawy + seria | `Zywiec/WstawieniaKurczaka/WstawienieWindow.xaml` | Justyna, Maja |
| `02_Lista_Wstawien.md` | **Lista** wszystkich cykli + przypomnienia + historia kontaktów | `Zywiec/WstawieniaKurczaka/WidokWstawienia.xaml` | Justyna, Maja |
| `03_Lista_Partii.md` | **Partie ubojowe** — pulpit dnia + lista historyczna + QC zamykanie | `Partie/Windows/ListaPartiiWindow.xaml` | Marcin, brygadziści |
| `04_Reklamacje.md` | **Reklamacje klientów** + auto-import korekt z Symfonii | `Reklamacje/Views/FormPanelReklamacjiWindow.xaml` | Jola |
| `05_Kalendarz_Dostaw_Zywca.md` | **Tygodniowy planner dostaw** — kto przyjeżdża kiedy + status | `Zywiec/Kalendarz/WidokKalendarzaWPF.xaml` | Justyna, Maja |
| `06_Baza_Hodowcow.md` | **CRM hodowców** — lista + Karta 360° + Wizard + Mapa + Duplikaty | `Hodowcy/PozyskiwanieHodowcowWindow.xaml` | Maja, Sergiusz |
| `07_Umowy_i_Dokumenty.md` | **Umowy zakupu** (Sprawdzalka + UmowyForm) + **Dokumenty handlowe** (drill-down z raportów) | `WPF/SprawdzalkaUmowWindow.xaml` + `SzczegolyDokumentuWindow.xaml` | Asia, Sergiusz |

## Mapa modułów ↔ ludzi ↔ instrukcji

```
              ╔══════════════════════════════════════════════╗
              ║         CYKL ŻYCIA PRODUKTU + CRM           ║
              ╚══════════════════════════════════════════════╝

   🐔 BAZA HODOWCÓW (instr. 06)  ⟵⟶  ASIA: Umowy (instr. 07)
   ├─ Maja: codzienne dzwonienie         ├─ Sprawdzalka Umów
   ├─ Karta 360° per hodowca             ├─ UmowyForm (DOCX gen.)
   └─ Mapa GPS + Duplikaty               └─ Status: Utworz./Wysł./Otrz.
        │
        │ (hodowca zostaje "Zdaje" = aktywny)
        ↓
   📅 KALENDARZ DOSTAW (instr. 05)  ← Justyna codziennie 06:30
   ├─ Tygodniowy planner tabeli
   ├─ Statusy kolorowe (zielony/żółty/itd.)
   └─ Ranking hodowców (sidebar)
        │
        │ (z dostawy w kalendarzu rodzi się cykl)
        ↓
   📋 CYKL WSTAWIENIA (instr. 01-02)  ← Justyna, Maja
   ├─ 01: edytor jednego cyklu + dostawy + seria
   └─ 02: lista wszystkich cykli + przypomnienia
        │
        │ ~42 dni rośnie...
        ↓
   🚛 Transport → 🏭 Ubój
        │
        ↓
   🏭 LISTA PARTII (instr. 03)  ← Marcin
   ├─ Produkcja Dzis (pulpit dnia)
   ├─ Lista Partii (historia)
   └─ QC Checklist przy zamykaniu
        │
        ↓
   📦 Pakowanie → 🚚 Klient
        │
        ├──→ 📑 DOKUMENTY (instr. 07)  ← Sergiusz, księgowość
        │    └─ drill-down z raportów Sprawozdania
        │
        └──→ 📋 REKLAMACJE (instr. 04)  ← Jola
             ├─ Telefon klienta lub
             └─ Auto-import korekty z Symfonii
```

### Wymiar CRM/zakupy (poprzeczny)

```
🐔 BAZA HODOWCÓW    ⟷    📑 UMOWY ZAKUPU    ⟷    📅 KALENDARZ DOSTAW
(instr. 06)              (instr. 07 część 1)        (instr. 05)
   Maja                       Asia                       Justyna
```

## Konwencja pisania instrukcji

- **Po polsku**, prostym językiem.
- Bez żargonu programistycznego (lub z wyjaśnieniem).
- Każda instrukcja ma sekcje:
  1. **Po co to okno** (1-2 zdania)
  2. **Tryby / 2 widoki** (jeśli są)
  3. **Anatomia okna** (ASCII mockup)
  4. **Krok po kroku** (typowy scenariusz)
  5. **Funkcje zaawansowane**
  6. **Co znaczy która opcja**
  7. **Typowy dzień użytkownika** (scenariusz z godzinami)
  8. **FAQ** (typowe pytania)
  9. **Mapa działań** (skróty do najczęstszych zadań)
  10. **Skróty klawiszowe**
  11. **Co dalej** (linki do innych instrukcji)
- **Mockupy ASCII** jeśli pomocne.
- **Imiona przykładowe** konsekwentne w całej dokumentacji firmy:
  - Wojtek Nowak — hodowca dobry (TOP 25%)
  - Mazur Kazimierz — hodowca słaby (BOTTOM 10%)
  - Marek Borowski — kierowca dobry
  - Stasiek — kierowca też
  - Janusz Mikulski — kierowca słaby (DOA wysokie)
  - Justyna — rampa, planowanie dostaw
  - Janek (dr Mikulski) — weterynarz PM
  - Jola — reklamacje, klienci
  - Maja — zaopatrzenie, hodowcy
  - Marcin — kierownik produkcji
  - Adam — operator linii
  - Marian — brygadzista

## Co jeszcze do napisania (TODO)

| Plik (planowany) | Moduł | Priorytet |
|---|---|---|
| `08_Poranny_Briefing.md` | MarketIntelligence/ (kokpit CEO) | Średni |
| `09_Mapa_Floty.md` | MapaFloty/ + Flota/ (kierowcy + pojazdy) | Średni |
| `10_Transport.md` | Transport/ (planowanie kursów) | Wysoki |
| `11_Analityka_Pelna.md` | AnalitykaPelna/ (Plan/Realizacja/Bilans/Wydajność) | Średni |
| `12_Kartoteka_Towarow.md` | KartotekaTowarow/ | Niski |
| `13_Kontrola_Godzin.md` | HR + RCP (3100 linii!) | Wysoki dla HR |
| `14_Panel_Faktur.md` | WPF/PanelFakturWindow.xaml | Średni |
| `15_Centrum_Nagran_AI.md` | CentrumNagranAI/ (CCTV + Claude) | Niski |
| `16_Sprawozdania_P02.md` | Sprawozdania/ (raporty + drill-down dokumentów) | Średni |

## Jak dodać kolejną instrukcję

1. Stwórz nowy plik `NN_NazwaModulu.md` (kolejny numer).
2. Dodaj wpis w tabeli **Pliki** wyżej.
3. Trzymaj się szablonu istniejącej instrukcji (najlepiej `04_Reklamacje.md` — najbardziej dojrzała).
4. Pamiętaj: ten plik czyta **użytkownik**, nie programista. Bez żargonu.
5. **Zacznij od researchu**: użyj agenta `Explore` lub przeczytaj XAML + code-behind głównych plików modułu.
6. **Imiona spójne** z resztą dokumentacji (lista wyżej).
7. **Mockupy ASCII** dla 2-4 kluczowych ekranów.
8. **Sekcja "Co dalej"** z linkami.

## 🔗 Instrukcje role-based (druga sesja — dział zakupu pod nowych ludzi)

> **WAŻNE — aktualizacja kadrowa (2026-05-25):** te instrukcje pisano gdy zaopatrzenie prowadziła „Maja". Tymczasem **dział zakupu przejmuje Magda + Asia** (Paulina odeszła, Tereska odchodzi). Opisy okien tutaj są poprawne technicznie — ale **operacyjne instrukcje „krok po kroku" dla nowych ludzi** są w osobnych folderach:

| Folder | Dla kogo | Co |
|---|---|---|
| [`../INSTRUKCJE_MAGDA/`](../INSTRUKCJE_MAGDA/00_INDEKS.md) | **Magda** (nowa w zakupie) | 16 instrukcji „co robisz krok po kroku" + cheatsheet |
| [`../INSTRUKCJE_ASIA/`](../INSTRUKCJE_ASIA/00_INDEKS.md) | **Asia** (strażnik kontraktów) | 5 instrukcji: kontrakty, ZSRIR, GUS R09, dashboard ARiMR |
| [`../INSTRUKCJE_TERESKA/`](../INSTRUKCJE_TERESKA/01_przekazanie_magdzie_30_dni.md) | **Tereska** (odchodzi) | przekazanie wiedzy 30 dni |
| [`../INSTRUKCJE_JUSTYNA/`](../INSTRUKCJE_JUSTYNA/01_padle_hpai_procedura_kryzysowa.md) | **Justyna** (jakość) | HPAI/padłe procedura kryzysowa |

**Podział ról folderów:**
- **`INSTRUKCJE_OBSLUGI/`** (ten) = referencja techniczna „jak działa okno" — dla każdego, pełne pokrycie funkcji.
- **`INSTRUKCJE_MAGDA/` itd.** = „co konkretna osoba robi krok po kroku" — operacyjne, role-based.
- Te same okna są w obu, ale z różnej perspektywy — **komplementarne, nie duplikaty.**

**Mapa nakładających się okien** (ten folder ↔ INSTRUKCJE_MAGDA):
- `01_Wstawienia_Kurczakow.md` ↔ `../INSTRUKCJE_MAGDA/02_nowe_wstawienie.md` + `03_potwierdzenie_wstawienia.md`
- `05_Kalendarz_Dostaw_Zywca.md` ↔ kontekst w instrukcjach Magdy #2/#4
- `06_Baza_Hodowcow.md` ↔ kontekst w #1/#10/#11
- `07_Umowy_i_Dokumenty.md` ↔ `../INSTRUKCJE_MAGDA/06_umowa_zakupu.md` (+ docelowy moduł Kontrakty)

## Powiązane dokumenty

- **Audyt techniczny i biznesowy** modułów → `BAZA_WIEDZY/AUDYT_BROILER_SIGNALS/` (13 plików).
- **Audyt działu zakupu + moduł Kontrakty** → `BAZA_WIEDZY/AUDYT_ZAKUPY_2026_05_23/` (druga sesja, 2026-05-25).
- **Scalone podsumowanie obu sesji** → `BAZA_WIEDZY/PODSUMOWANIE_SCALONE_2026_05_25.md`.
- **Architektura kodu** → `CLAUDE.md` w korzeniu repo.
- **SQL i schemat baz** → `BAZA_WIEDZY/13_Bazy_danych.md`.
