# AUDYT_BROILER_SIGNALS — porównanie ZPSP z książką (rozszerzone)

> Audyt wykonany **2026-05-23** na bazie:
> - **Broiler Meat Signals** (Roodbont 2020, ISBN 978-90-8740-330-0) — 196 stron — `BAZA_WIEDZY/Drobiarstwo/Broiler Meat Signals (VetBooks.ir).pdf`
> - Stan kodu ZPSP **commit master @ 2026-05-23** (a675e5f).
>
> Audyt **nie modyfikuje kodu produkcyjnego** — wszystko proponowane w nowych folderach `Stunning/`, `Patroszenie/`, `Chlodnia/`, `Higiena/`, `Compliance/`.

## Co tu jest (12 plików)

| Plik | Co | Dla kogo | Czas czytania |
|---|---|---|---|
| `00_EXECUTIVE_SUMMARY.md` | TL;DR na telefon | Sergiusz w 3 min | 3 min |
| `00_README.md` | Ten plik (mapa folderu) | każdy nowy | 2 min |
| `01_INWENTARYZACJA.md` | Co już mam vs 12 obszarów książki | techniczny | 5 min |
| `02_NOWE_FUNKCJE.md` | 12 funkcji: model danych + UI + KPI + $ | dev + Sergiusz | 25 min |
| `03_ULEPSZENIA.md` | 8 modyfikacji istniejących modułów | dev | 10 min |
| `04_PRIORYTYZACJA.md` | **DZIEŃ PO DNIU** — co robić w pon/wt/śr | Sergiusz operacyjnie | 8 min |
| `05_BRC_v9_MAPPING.md` | Mapping NF → BRC v9 / IFS v8 | audytor / Sergiusz pre-audit | 5 min |
| `06_SQL_DDL.sql` | **Gotowe DDL** (~17 tabel + 2 views) | dev — kopiuj-wklej | reference |
| `07_SLOWNICZEK.md` | **Wszystkie pojęcia po polsku** — FPD, CCP, BCO itp. | Sergiusz + Jola/Maja | 10 min |
| `08_DZIEN_Z_ZYCIA.md` | **6 scenariuszy** "dzień przed/po wdrożeniu" | każdy | 12 min |
| `09_MOCKUPY_UI.md` | **13 mockupów ASCII** — wyglądy ekranów | Sergiusz wyobrazić sobie | 10 min |
| `10_DROBIARSTWO_OD_PODSTAW.md` | **Kurs drobiarstwa dla Ciebie od zera** — historia kurczaka Zenka, 42 dni życia, 10 sygnałów na linii | Sergiusz — naucz się! | 30 min |
| `11_PRZYKLADY_ZYCIOWE.md` | **Konkretne przykłady dla każdej z 20 funkcji** — godziny, imiona, kwoty | Sergiusz — zobacz w praktyce | 25 min |

## Jak czytać — 4 ścieżki

### 📚 Ścieżka UCZENIA SIĘ (1h 20min — od zera, "nie znam drobiarstwa")
Idealna jeśli chcesz **naprawdę zrozumieć** zanim podejmiesz decyzje. Czytaj wieczorem z kawą.

1. `10_DROBIARSTWO_OD_PODSTAW.md` — historia kurczaka Zenka, anatomia uboju, sygnały na linii (30 min)
2. `07_SLOWNICZEK.md` — wszystkie pojęcia (10 min) — wracaj jak potrzebujesz
3. `11_PRZYKLADY_ZYCIOWE.md` — konkretne przykłady każdej funkcji (25 min)
4. `00_EXECUTIVE_SUMMARY.md` — utrwalenie (3 min)
5. `08_DZIEN_Z_ZYCIA.md` — scenariusze (12 min)

**Po tej ścieżce wiesz wszystko**. Możesz prowadzić mądrą rozmowę z weterynarzem, hodowcą, klientem, auditorem BRC.

### 🚀 Ścieżka EKSPRESOWA (15 min — jeśli masz mało czasu)
1. `00_EXECUTIVE_SUMMARY.md` (3 min)
2. `07_SLOWNICZEK.md` skanuj nagłówki (3 min)
3. `08_DZIEN_Z_ZYCIA.md` scenariusz 1+4+6 (9 min)

### 🎯 Ścieżka OPERACYJNA (50 min — Sergiusz na decyzję)
1. `00_EXECUTIVE_SUMMARY.md` (3 min)
2. `07_SLOWNICZEK.md` całość (10 min)
3. `11_PRZYKLADY_ZYCIOWE.md` — konkretne wartości funkcji (25 min)
4. `09_MOCKUPY_UI.md` przejrzeć (10 min)
5. `04_PRIORYTYZACJA.md` całość (8 min)
6. Plan działania na poniedziałek 🎯

### 🛠 Ścieżka DEWELOPERSKA (2h — programista do implementacji)
1. `00_EXECUTIVE_SUMMARY.md` (3 min)
2. `01_INWENTARYZACJA.md` (5 min)
3. `02_NOWE_FUNKCJE.md` — szczegóły 12 funkcji (25 min)
4. `03_ULEPSZENIA.md` (10 min)
5. `09_MOCKUPY_UI.md` (10 min) — żeby wiedzieć jak ma wyglądać
6. `06_SQL_DDL.sql` — reference do kopiowania (reference)
7. `04_PRIORYTYZACJA.md` — plan tygodniowy QW1-5 (8 min)
8. `05_BRC_v9_MAPPING.md` — kontekst dlaczego to ważne (5 min)

## Konwencje

- **Prefix tabel SQL**: `BS_` (Broiler Signals).
- **NF##** = Nowa Funkcja (#1-12, jeden per obszar książki).
- **U##** = Ulepszenie istniejącego (8 propozycji).
- **QW##** = Quick Win (do tygodnia, dev-only, zerowy CAPEX).
- **ST##** = Strategic (perspektywa roku, zwykle CAPEX 10-30 tys.).
- **AR##** = ARiMR-fundable (do wniosku IX.2026, większe budżety).
- **M##** = Mockup UI (numerowane w `09_MOCKUPY_UI.md`).
- **Wszystkie kwoty PLN** przy założeniu: **70k ptaków/dzień × 250 dni = 17.5 mln ptaków/rok**, średnia waga karkasu 2 kg = 35 000 t/rok, cena tuszki 12 zł/kg.

## Słowniczek pojęć w skrócie

Pełen słowniczek w `07_SLOWNICZEK.md`. Tu top 8 które muszą być oczywiste:

| Pojęcie | Po polsku w 1 zdaniu |
|---|---|
| **FPD** | Zapalenie podeszew łap (mokra ściółka u hodowcy). |
| **DOA** | Martwy przy rozładunku (max 0.5%, średnia 0.2%). |
| **CCP** | Krytyczny Punkt Kontrolny w HACCP — musi być **elektronicznie** monitorowany (BRC v9). |
| **PM** | Post Mortem inspection (weterynaryjna kontrola po uboju). |
| **Polyserositis** | Zapalenie błon surowiczych — top powód odrzutu PM w EU. |
| **Ascites** | Wodobrzusze (mięso ciemne, brzuch obrzęknięty). |
| **MAP** | Pakowanie w atmosferze modyfikowanej (CO2/N2) — shelf life 30-60 dni. |
| **Drip loss** | Ubytek wody z mięsa po pakowaniu — koszt 8.4M zł/rok dla Ciebie. |

## Cyfry do zapamiętania

- **17.5 mln ptaków/rok** Twojego wolumenu.
- **35 000 ton/rok** mięsa.
- **~318 mln zł obrotu** (z briefingu firmowego).
- **~216 tys. zł** łączny CAPEX wszystkich 12 nowych funkcji.
- **~16 mln zł/rok** bezpośrednia wartość operacyjna nowych funkcji.
- **~130 mln zł** chronionych obrotów (dostęp do retail UE — BRC).
- **<1 rok** średni okres zwrotu większości funkcji.

## Co dalej

1. Czytaj zgodnie ze ścieżką (EKSPRESOWA / OPERACYJNA / DEWELOPERSKA).
2. Wybierz QW01 do realizacji w nadchodzącym tygodniu.
3. Wyślij email do dostawcy linii (Marel/Foodmate) — `04_PRIORYTYZACJA.md` ostatnia sekcja.
4. Skonsultuj wniosek ARiMR z księgową (Jola lub kancelaria) do końca lipca.

## Źródła wiedzy w repo

**Wcześniejsze (kontekstowe):**
- `BAZA_WIEDZY/30_POMYSLY/00_INDEX_I_ROADMAPA.md` — 30 wcześniejszych pomysłów Broiler Signals
- `BAZA_WIEDZY/30_POMYSLY/DEEP_DIVE_19_Cold_Chain.md`
- `BAZA_WIEDZY/30_POMYSLY/DEEP_DIVE_22_Traceability.md`
- `BAZA_WIEDZY/30_POMYSLY/DEEP_DIVE_12_Forensic.md`
- `CLAUDE.md` — instrukcje projektu (sekcje 4-5 ważne)

**Wykorzystane w syntezie audytu:**
- Książka jako źródło "co powinno się mierzyć"
- Inwentaryzacja kodu jako "co już mamy"
- Audyt branżowy 2026-05-11 (z memory) jako kontekst sektorowy

## Wersjonowanie audytu

- **v1.0** — 2026-05-23 — pierwsza wersja: 8 plików (Executive, README, Inwentaryzacja, Nowe Funkcje, Ulepszenia, Prioryzacja, BRC mapping, SQL DDL)
- **v1.1** — 2026-05-23 — rozszerzenie: dodanie Słowniczka, Dnia z życia, Mockupów UI; rozbudowa Prioryzacji o plan tygodniowy
- **v1.2** — 2026-05-23 — **kurs drobiarstwa od zera** + **przykłady życiowe** dla każdej funkcji (12 plików łącznie). Ścieżka UCZENIA SIĘ dla osób, które chcą się **naprawdę nauczyć** zanim zdecydują.
