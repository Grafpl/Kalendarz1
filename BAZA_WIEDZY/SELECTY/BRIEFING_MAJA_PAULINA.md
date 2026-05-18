# Briefing dla Claude web — decyzja o Mai, parytecie z Teresą, przesunięciu na zakup żywca

> Wklej **wraz z wynikami SQL** (HANDEL + LibraNet) do Claude w przeglądarce.
> Daje to pełen kontekst do oceny: czy podnieść Mai pensję z 7→10k PLN; czy parytet
> z Teresą (też 10k) jest fair; czy przesunięcie jej na zakup żywca po Paulinie ma sens.

---

## 1. KONTEKST FIRMY (Piórkowscy — ubojnia drobiu)

- **Skala:** 258M PLN obrotu rocznie, 200 ton produkcji dziennie, ~140-200 pracowników
- **Lokalizacja:** Koziołki (główny zakład) + Zgierz (masarnia, brat Marcin)
- **Branża:** drób — żywiec → ubój → krojenie → świeże/mrożone/wędliny → klienci B2B
- **Klienci:** 400+ w bazie HANDEL, 80-100 aktywnych dziennie, top 10 = ~70% wolumenu
- **Hodowcy:** 140+ aktywnych, 40-70 dostawców w danym kwartale, 1874 leadów w CRM
- **System ERP:** Sage Symfonia (HANDEL, 192.168.0.112) + własny ZPSP (LibraNet/TransportPL, 192.168.0.109)
- **Właściciel:** Sergiusz Piórkowski (programuje ZPSP sam, 5+ lat, klient-developer)

## 2. STRUKTURA ZESPOŁU SPRZEDAŻOWO-ZAKUPOWEGO

| Osoba | Stanowisko | Co robi | Pensja (znana) |
|---|---|---|---|
| **Pani Jola** | Senior handlowiec (30 lat firmy) | ~60% wolumenu firmy (Damak + Trzepałka). Używa karteczek, NIE czyta WhatsApp, monopol na top klientów | nieznana |
| **Maja** | Handlowiec (od 10.2025) | 65 klientów obecnie, 966 faktur, 25.8M obrotu (era Mai). Pozyskała 13 nowych klientów. Typ ESTJ — prosi o strukturę | 7000 → żąda 10000 |
| **Ania** | Handlowiec | Klienci eksportowi + pośrednik komunikacyjny dla Joli (czyta WhatsApp i wpisuje za Jolę). 5.6M obrotu | nieznana |
| **Radek** | Handlowiec | "Na końcu łańcucha pokarmowego" — dostaje resztki przy ucinaniu | nieznana |
| **Teresa Jachymczak** | Faktycznie obsługuje dział sprzedaży + zakupu (nieformalnie). Cel Sergiusza: awansować na Dyrektora Handlowego | 33.6M obrotu na 7 klientach (HHI 4621 — skrajna koncentracja!) | 6000+2000 → 10000 (parytet) |
| **Paulina** | Dział zakupów żywca | Negocjacje z hodowcami, harmonogramy dostaw, ceny. Konflikt z Teresą (04.2026). Rozważa odejście | nieznana |
| **Daniel** *(odszedł)* | Były handlowiec | Próbował wyciągnąć prowizję po odejściu — sprawa zamknięta. Część jego klientów przejęta przez Maję 10.2025 | — |
| **Dawid** *(odszedł)* | Były handlowiec | Razem z Danielem przed Maja 10.2025 | — |

## 3. SYTUACJA AKTUALNA

### Co się dzieje (maj 2026):
1. **Maja** dostała ofertę z zewnątrz na 9000 PLN. **Żąda od firmy 10000 PLN albo odejdzie.**
2. **Teresa** — jeśli Maja dostanie 10k, też pójdzie na 10k (parytet — Sergiusz nie może płacić Mai więcej niż Teresie).
3. **Paulina** może odejść (konflikt z Teresą). Plan: **przesunąć Maję częściowo na zakup żywca po Paulinie**.
4. **Maja musi:** (a) utrzymać 25.8M obrotu sprzedaży, (b) przejąć część operacji zakupowych, (c) rozwinąć portfel.

### Co Maja realnie robi (z danych HANDEL):
- **29 klientów aktywnych** (era Mai od 10.2025), 13 nowych pozyskanych
- **Mix portfela:** TOP 5 = 52% udziału. SMOLIŃSKI (22%), BATISTA (8%), MEST FOOD Estonia (8%), LAVERNA (8%), TWÓJ MARKET (7%)
- **HHI 918** — najlepszy/najzdrowszy portfel w firmie (Jola 1679, Teresa 4621, Ania 1721)
- **Marża vs benchmark: -27k PLN na 25.8M = -0.1%** — sprzedaje praktycznie po średniej firmy
- **Mix kategorii:** 71% świeże / 28% mrożone (mrożone wyższa marża)
- **Eksport ~30%** portfolio (Estonia, Belgia, Holandia, Szwecja, Dania, Rumunia)
- **MAT TEAM** — jedyny krytyczny dłużnik (230k PLN, 530 dni przeterminowania)

### Co Paulina obecnie robi (zakup żywca):
- ~50 dostawców rocznie, ~52000 ton żywca/rok
- Wartość zakupu ~210M PLN/rok (4-5.20 PLN/kg × ~50000t)
- Negocjuje kontrakty (50% kontrakt 4.40-5.23 zł/kg / 50% wolny rynek 4.00 zł/kg)
- Prowadzi CRM hodowców (Pozyskiwanie_Hodowcy: 1874 leadów, statusy: Nowy/Skontaktowany/Zainteresowany/Próbne/Stały/Odrzucony)
- Planuje harmonogram dostaw (HarmonogramDostaw — co tydzień ~50-70 dostaw, AVILOG planuje precyzyjnie auto po aucie)
- Reklamacje na partie hodowców (ReklamacjePartie — krwiaki, złamania, klasy B)
- Rozliczenia z hodowcami (FarmerCalc)

## 4. PROCEDURY OBOWIĄZUJĄCE (do uwzględnienia w decyzji)

Z `PROCEDURY_01_HANDLOWCY_V4_FINAL.docx`:
- **Polityka cen:** świeże ustala handlowiec → raport Zarządowi; mrożone ustala WYŁĄCZNIE Zarząd; rabaty WYŁĄCZNIE Zarząd
- **Bufor:** firma utrzymuje 5-6 ton dziennie. **Piątek: bufor musi spaść do 0** (towar nie przeżyje weekendu)
- **Ucinanie:** każdy klient dostaje TEN SAM PROCENT (np. 80%). VIP-owanie ZAKAZANE
- **Deadliny:** zamówienia do 10:00 (priorytet), 14:00 (deadline twardy)
- **CRM statusy leadów (klienci):** Nowy → Skontaktowany → Zainteresowany → Próbne → Stały → Odrzucony
- **SLA reklamacji:** odpowiedź do 15:00 tego dnia, zamknięcie 48h
- **Limity kredytowe:** ustala ubezpieczyciel, ZPSP blokuje po przekroczeniu lub 14 dniach przeterminowania

Z `PROCEDURY_07_JAKOSC.docx`:
- Justyna kontroluje halę 7-9-11-13-15 (5 obchodów dziennie)
- Reklamacje workflow: zgłoszenie → klasyfikacja → badanie → konsultacja → decyzja → odpowiedź → zamknięcie 48h → CAPA

## 5. RAPORTY FIREFLIES (rozmowy nagrane)

Dostęp przez MCP — `mcp__claude_ai_Fireflies__*`. Konkretne pliki w `Dokumenty ogólnikowe/`:

| Rozmowa | Z kim | O czym |
|---|---|---|
| `Rozmowa-z-Justyn-...c5aa1476.docx` (90KB) | Justyna Chrostowska | Kontrola jakości, procedury, BRC/IFS |
| `Rozmowa-z-Ilon-Transport-...93e8d80d.docx` (48KB) | Ilona | Transport, procedury kierowców |
| `Rozmowa-z-S-awomir-Ga-ek-...edfc8ece.docx` (37KB) | Sławomir Gałek | Kierowca |
| `Rozmowa-z-kierowc-Rados-aw-...4e42f1ed.docx` (58KB) | Radosław Kołodziejczyk | Kierowca |
| `Spotkanie-z-Panem-Jankiem-...5617a3fe.docx` (48KB) | Pan Janek | Procedury (kontekst nieznany agentowi) |
| `Feb-25/27-...docx`, `Mar-23-...docx` | (bez tytułu) | Spotkania luty/marzec 2025 |

**Brak nagranej rozmowy bezpośrednio z Mają lub Pauliną** — Sergiusz nagrywa głównie z dyrektorami i kierowcami.

## 6. STRUKTURA PROGRAMU ZPSP (Kalendarz1)

System modułowy z `accessMap` (kontrola uprawnień przez int → moduł):

| accessMap | Moduł | Co | Z baz |
|---|---|---|---|
| 16 | UstalanieTransportu | Planowanie kursów + palety | TransportPL + LibraNet + Handel |
| 55 | Pozyskiwanie hodowców | CRM hodowców (1874 leadów) | LibraNet |
| 56 | Kartoteka towarów | Article + Audit + Favorites | LibraNet |
| 57 | Flota | Kierowcy + Pojazdy + Przypisania | LibraNet |
| 58 | Lista Partii V2 | Partie ubojowe (10-state lifecycle) | LibraNet |
| 59 | Transport Zmiany | Workflow akceptacji zmian kursów | TransportPL |
| 67 | Centrum Nagrań AI | CCTV + Claude AI rerank | NVR + Claude API |
| (TBD) | **Analityka Pełna** | Cross-DB: 4 widoki Plan/Realizacja/Bilans/Wydajność | HANDEL + LibraNet |
| (TBD) | **Handlowiec Dashboard** | 11 zakładek: sprzedaż, top klienci, ceny, mrożone vs świeże, płatności | HANDEL |

## 7. KLUCZOWE DANE DO WKLEJENIA (z SQL)

Po uruchomieniu **analiza_maja_HANDEL.sql** wklej do Claude web:
- **K1** (scorecard fakturowy: wszyscy handlowcy)
- **T.1** (super-scorecard: 12 metryk per handlowiec)
- **L.2** (era Daniela vs era Mai: czy Maja rozwija portfel)
- **L.3** (TOP 10 klientów rosnących pod Mają)
- **L.4** (TOP 10 spadających — red flagi)
- **L.5** (klienci straceni — odeszli z firmy)
- **L.6** (klienci czysto nowi pod Mają)
- **D.2** (suma marży Mai vs benchmark)
- **D.3** + **D.4** (gdzie traci/zarabia na cenie)
- **F.1** (frekwencja — sygnalizacja klientów uciekających)
- **C.1** + **C.2** (nowi/przejęci/utraceni)
- **O.1** + **O.3** (eksport vs krajowy)
- **B.4** (Pareto: ile klientów = 80% obrotu)

Po uruchomieniu **analiza_paulina_ZAKUP.sql** (dla kontekstu czego Maja by przejęła):
- **G.1** (skala działania Pauliny: ton żywca, hodowców, wartość zakupu)
- **A.2** (kontrakt vs wolny rynek)
- **A.4** (TOP 30 dostawców — z kim Paulina pracuje)
- **B.1** (lejek CRM hodowców)
- **A.5** (kontrakty na przyszłość — do realizacji)

Po uruchomieniu **analiza_maja_LIBRANET.sql** (dane operacyjne):
- **K2** (scorecard zamówieniowy)
- **G.1-G.3** (zamówienia per miesiąc, średni czas planowania)
- **I.1** (różnice zamówienie→wydanie — precyzja obietnic)
- **H.2** (reklamacje per handlowiec)
- **G.5** (mix pakowania E2/Folia/Hallal)

## 8. PYTANIA DO CLAUDE WEB

Sugerowany prompt:

> Jestem właścicielem ubojni drobiu Piórkowscy (~258M PLN obrotu, 200 t/dzień).
> Mam handlowca Maję która od 10.2025 (8 miesięcy) generuje 25.8M obrotu na 29
> klientach, w tym 13 pozyskanych samodzielnie. Żąda 10k PLN (z 7k), ma ofertę 9k
> z zewnątrz. Jeśli dostanie 10k, Teresa (33.6M obrotu na 7 klientach) też wejdzie
> na 10k (parytet). Dodatkowo planuję częściowo przesunąć Maję na zakup żywca po
> Paulinie (która kupuje ~52000t/rok wartości ~210M PLN).
>
> Oto twarde dane:
> [WKLEJ TUTAJ K1 + T1 + L1-L6 + D2/D3/D4 + reszta]
>
> Odpowiedz na:
> 1. Czy 10k dla Mai jest uzasadnione patrząc na te dane?
> 2. Czy Maja faktycznie rozwija portfel ex-Daniela/Dawida czy tylko nie zepsuła?
> 3. Czy parytet z Teresą jest fair? Teresa robi większy obrót ale na 7 klientach
>    (HHI 4621 = uzależnienie). Maja zbudowała 29 klientów w pół roku.
> 4. Czy Maja jest w stanie częściowo przejąć rolę Pauliny?
>    Jakich kompetencji potrzebuje? Co musi się nauczyć od Pauliny?
> 5. Jakie 5 KPI postawić Mai na Q3/Q4 2026 jeśli zostaje na 10k?
> 6. Jakie ryzyka kadrowe widzisz? Co zrobić jeśli odejdzie?
> 7. Praktyczny plan rozwoju Mai na 12 miesięcy: co utrzymać + co dodać + jak mierzyć.

## 9. CZEGO BRAKUJE w danych (świadome luki)

- **Pensje innych handlowców** — nie zna ich Claude, podaj jak negocjacjach
- **Konkretne marże produktów** — `DP.kosztAproksymowany` jest niewiarygodny w HANDEL; marża liczona top-down (cena_sprzedaży − cena_żywca / uzysk_%)
- **Aktywność CRM Mai w LibraNet** — bo nie wiemy czy Maja loguje notatki/przypomnienia czy nie (raporty J.1-J.5 to pokażą)
- **Czas pracy Mai** — UNICARD ma jej godziny ale tego nie ciągnęliśmy do tej analizy
- **Cykl decyzyjny Mai** — czy poprawia własne zamówienia często (G.6/G.7 to mierzy)

## 10. PLIKI ŹRÓDŁOWE (dla weryfikacji)

- Folder skryptów: `BAZA_WIEDZY/SELECTY/`
  - `analiza_maja_HANDEL.sql` (sprzedaż Mai)
  - `analiza_maja_LIBRANET.sql` (operacyjne)
  - `analiza_paulina_ZAKUP.sql` (zakup żywca)
  - `eksploracja_HANDEL_v2.sql` (pełna inwentaryzacja kolumn HANDEL)
  - `eksploracja_LIBRANET_v2.sql` (pełna inwentaryzacja LibraNet)
- Dokumentacja firmowa: `BAZA_WIEDZY/01-27_*.md`
- Procedury (oryginały): `Dokumenty ogólnikowe/PROCEDURY_*_V4_FINAL.docx`
- Rozmowy Fireflies: `Dokumenty ogólnikowe/Rozmowa-*.docx`
