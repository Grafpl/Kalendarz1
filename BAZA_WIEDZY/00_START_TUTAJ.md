# BAZA WIEDZY — Sergiusz Piórkowski + Ubojnia Drobiu Piórkowscy + ZPSP

**Po co istnieje ten folder:** Sergiusz pracuje z Claude Code codziennie, ale każda nowa rozmowa zaczyna się bez kontekstu. Ten folder = kompletna baza wiedzy, którą każda nowa sesja może wczytać żeby od razu wiedzieć kim jest user, jak działa firma i jak działa ZPSP.

**Status:** 2026-05-03 — pierwsza wersja po 6 miesiącach pracy z Sergiuszem. Zawiera fakty z dokumentów (procedury 01-08, raporty, wywiady), z rozmów (Fireflies), z kodu ZPSP, z bezpośrednich odpowiedzi Sergiusza (PYTANIA_PRODUKCJA, voice-to-text).

---

## Jak czytać tę bazę

**Pierwsza rozmowa o ZPSP / firmie:**
1. Przeczytaj `01_Sergiusz_profil.md` — kim jest user
2. Przeczytaj `02_Firma_skala.md` — co to za firma
3. Przeczytaj `12_ZPSP_program.md` — co to za program

**Pytanie konkretne (np. "co to magazyn 65554"):** od razu skacz do `24_Magazyny_i_Lancuch_Produkcji.md`.

**Pisanie SQL na HANDEL/Symfonię:** najpierw przeczytaj `23_HANDEL_Schema_Sage_Symfonia.md` (gotchas: anulowany, MM- i khdzial, brak słownika magazynów, ABS(ilosc)).

**Modyfikacje w "Bilans materiałowy" / "Stan magazynów":** `25_Analityka_Pelna_v2_StanMagazynow.md` ma pełną strukturę.

**Modyfikacje w nowym oknie zamówienia / przypisanie handlowca / awatary / sugestie notatek:** `26_Modul_Zamowien_v2.md` zawiera wszystko (architektura, ContractorClassification triggery, UserAvatarManager, NotatkiService smart ranking).

**Modyfikacje w "Pokaż ceny" / analityka cen żywca / Kontrakty vs Wolny rynek / YoY:** `27_WidokCenWszystkich_modul.md` zawiera wszystko (11 zakładek, klasyfikacja TypCeny, layout Kontrakty, dialog dostaw, dane HarmonogramDostaw).

**Konkretna zmiana w kodzie:** zacznij od `12_ZPSP_program.md` żeby zrozumieć architekturę, potem otwórz odpowiedni moduł w kodzie.

---

## Spis plików

| # | Plik | Co zawiera |
|---|---|---|
| 00 | START_TUTAJ.md | Ten plik — przewodnik |
| 01 | Sergiusz_profil.md | Kim jest właściciel, 18+ ról, preferencje, stack |
| 02 | Firma_skala.md | Liczby (258M/200t/dzień), lokalizacje, struktura |
| 03 | Ludzie.md | Wszyscy z imion: Łukasz, Justyna, Jola, Maja, Janek… |
| 04 | Klienci_dostawcy.md | Top klienci B2B, hodowcy, pasze |
| 05 | Dzien_pracy.md | Plan dnia 3:30→21:00, kto co robi i o której |
| 06 | Hala_produkcja.md | Brudna/czysta strefa, klasy A/B, klasy wagowe 6-11 |
| 07 | Magazyn_mroznia.md | 65554/65556, FIFO, szokówka, polibloki |
| 08 | Sprzedaz_ceny.md | Polityka cen, bilans, bufor 5-6t, ucinanie |
| 09 | Transport.md | Flota 12 aut + 13 kierowców, AVILOG |
| 10 | Procedury_zasady.md | 5 zasad operacyjnych z procedur 01-08 |
| 11 | Reklamacje_jakosc.md | Workflow, 75% to auto-import korekt Symfonii |
| 12 | ZPSP_program.md | Architektura, 71 okien, .NET 8 WPF/WinForms |
| 13 | Bazy_danych.md | UNISYSTEM, HANDEL, LibraNet, ZPSP, magazyny, numer partii |
| 14 | Systemy_zewnetrzne.md | WAGO selektywna, RADWAG, Symfonia, AVILOG, UNICARD |
| 15 | Inwestycje_ryzyka.md | Meyn IX 2026, fotowoltaika, HPAI, Mercosur |
| 16 | Frustracje_cele.md | Co Sergiusza wkurza + idealna wizja systemu |
| 17 | Slownik_skrotow.md | sPWU, RWP, WZ, PZ, FVS, FKS, ZPSP, BCC |
| 18 | Analiza_przychodu_szczegoly.md | **Tabela `In0E`, dekoder partii, klasy 5-12, operatorzy** — pełen szczegół modułu Analiza Przychodu Produkcji |
| 19 | LibraNet_audyt_uzycia.md | **Audyt: ~65 tabel + 15 widoków + 8 SP** używanych przez ZPSP. Po działach + relacje + WHERE klauzule |
| 21 | PYTANIA_PRODUKCYJNE.md | **118 pomysłów programów produkcyjnych** podzielonych po działach (A-K) — do rozmowy |
| 22 | Analityka_Pelna_modul.md | Moduł Analityka Pełna — 4 widoki + dialog drill-down |
| 23 | **HANDEL_Schema_Sage_Symfonia.md** | **HM.MG/MZ/TW pełna schema** — kolumny, gotchas, polimorfizm, brak słownika magazynów |
| 24 | **Magazyny_i_Lancuch_Produkcji.md** | **14 magazynów** (M.UBOJ/M.PROD/M.DYST...) + flow produkcji + wydajności + normy |
| 25 | **Analityka_Pelna_v2_StanMagazynow.md** | **Sub-tab "Stan magazynów"** w Bilansie — flow chain + towary z zdjęciami + Sankey MM- |
| 26 | **Modul_Zamowien_v2.md** | **Pełen refactor modułu zamówień (2026-05-09)** — NoweZamowienieTestWindow, UserAvatarManager, ContractorClassification triggery, smart suggestions notatek, NotatkiSzablony+NotatkiUzycia |
| 27 | **WidokCenWszystkich_modul.md** | **Moduł "Pokaż ceny" — 11 zakładek analityki cen żywca** (Dane/Wykresy/YoY/Kontrakty/Pasze/Klienci). Pełny SQL Kontrakty, definicja TypCeny, kolumny HarmonogramDostaw, dialog dostaw |
| _ | zrodla.md | Lista oryginalnych dokumentów (docx, pdf) |
| _ | AUDYT_KODU_SQL.md | Audyt zapytań SQL w kodzie ZPSP |
| _ | **SELECTY/** | **Folder z 20+ plikami `.sql`** + `WYNIKI.md` na wklejki — eksploracja LibraNet/HANDEL |

---

## Złote zasady pracy z Sergiuszem (krytyczne!)

1. **NIE dotykaj kodu bez wyraźnej zgody.** Sergiusz wolał kilka razy najpierw porozmawiać niż widzieć od razu zmiany.
2. **NIE rób "quick wins" w 5 oknach naraz** — szczególnie gdy user nawet w nie nie wchodzi.
3. **Najpierw zrozum jego dzień** — co realnie klika rano, kiedy odpala program, co robi przez telefon zamiast w ZPSP.
4. **Pisz konkretami** (file:line) — Sergiusz czyta kod, więc poda mu te lokalizacje.
5. **Gdy mówi głosem (voice-to-text):** literówki, brak interpunkcji — ale **bardzo gęste w treści**. Czytaj uważnie 2x.
6. **Sergiusz programuje sam** — to klient-developer, nie tylko klient. Może odpowiedzieć "zrobię sam".

---

## Co jeszcze warto wiedzieć

- **Adres głównej ubojni:** Koziołki 40, 95-061 Dmosin, woj. łódzkie (51.9148, 19.8089)
- **Druga lokalizacja:** Zgierz (masarnia Marcina Piórkowskiego — wspólnik)
- **Rok założenia:** 1996 przez Jerzego Piórkowskiego (dziadek Sergiusza)
- **Email Sergiusza:** sergiusz.piorko@gmail.com (Fireflies tu zapisuje nagrania)
- **Repo ZPSP:** `C:\Users\PC\source\repos\Grafpl\Kalendarz1\` — branch `master`
- **Memory Claude'a:** `C:\Users\PC\.claude\projects\C--Users-PC-source-repos-Grafpl-Kalendarz1\memory\`
