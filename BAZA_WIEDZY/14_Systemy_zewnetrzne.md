# 14 — Systemy zewnętrzne (poza ZPSP)

**Krytyczne dla zrozumienia pain points Sergiusza.** Brak integracji z 2 z tych systemów (WAGO + RADWAG) blokuje kluczowe KPI.

---

## 1. Sage Symfonia Handel

**Funkcja:** ERP główne — faktury, magazyny, kontrahenci, KSeF.

**Dostęp ZPSP:** Pełny SQL (192.168.0.112, baza HANDEL, user `sa`).

**Status:** Działa, używane intensywnie. ZPSP integruje się przez SQL queries i zapisy bezpośrednio do tabel HANDEL.

**Auto-import korekt:** ZPSP czyta FKS/FKSB/FWK z Symfonii i tworzy z nich rekordy w bazie reklamacji (= 75% reklamacji w ZPSP).

---

## 2. Sage Symfonia Production (KUPIONY, nigdy nie wdrożony!)

**Funkcja:** Moduł produkcji Symfonii — receptury, zlecenia, BOM, koszty produkcji.

**Status:** **87 tabel `MF.Production*` w bazie HANDEL — wszystkie PUSTE.**
**Powód:** Sergiusz kupił, ale nigdy nie wdrożył.

**Implikacja:**
- Wszystko co produkcja → musi działać przez ZPSP
- ZPSP wykonuje rolę MES (Manufacturing Execution System) zamiast Symfonia Production

**Rozważyć:** Albo wdrożyć Symfonia Production (i przenieść część logiki tam), albo pełna alokacja roli MES w ZPSP.

---

## 3. WAGO selektywna ⚠️ BRAK API

**Funkcja:** Waga + sortownik na linii ubojowej.
- Sprawdza wagę każdej tuszki
- Klasyfikuje wg **klas wagowych** (rozmiar 6/7/8/9/10/11 = liczba sztuk w pojemniku 15 kg)
- Otrzymuje sygnał A/B z wajchy klasyfikatora wzrokowego
- Kieruje tuszkę do odpowiedniego korytarza / pojemnika

**Status integracji ZPSP:** **BRAK DOSTĘPU** programistycznego.

**Skutki:**
- ZPSP **nie zna realnej klasy wagowej** per partia
- ZPSP **nie zna realnego % klasy A vs B** per partia per hodowca
- Sergiusz zabiega o dostęp od dostawcy WAGO

**Pain point Sergiusza:**
> *"Brak dostępu programistycznie. Potencjał: wiedzieć realny % klasy A vs B per hodowca — kluczowy KPI efektywności hodowcy. Akcja: Sergiusz prosi o dostęp od dostawcy."*

**Workaround obecny:** Excel (ręcznie wpisywane przez Sergiusza po dniu).

---

## 4. Licznik tuszek ⚠️ BRAK API

**Funkcja:** Liczy tuszki przechodzące przez linię (czujnik fotoelektryczny lub podobny).

**Status integracji ZPSP:** **BRAK DOSTĘPU**.

**Skutki:**
- ZPSP **nie zna real-time tempa linii** bez ręcznego wpisywania
- "Sztuk dziś" jest przybliżona (z plan × 0.78 + wagi samochodowe)

**Pain point Sergiusza:**
> *"Brak dostępu. Potencjał: real-time tempo linii bez ręcznego wpisywania. Akcja: Sergiusz prosi o dostęp od dostawcy."*

---

## 5. RADWAG (wagi platformowe + paletowe) ⚠️ BRAK API

**Funkcja:** Wagi przemysłowe w czystej strefie:
- **Waga paletowa** — całe palety z tuszką
- **Waga platformowa** — pojemniki 15 kg E2 z elementami

**Status integracji ZPSP:** **BRAK API**.

**Workaround:** Operator drukuje **etykietę** po zważeniu, etykieta z wagą + datą + nr partii. Etykieta wprowadzana ręcznie do ZPSP.

**Implikacja:**
- ZPSP **nie zna real-time stanu magazynu** per pojemnik
- Wszystko z opóźnieniem (etykieta → ręczny wpis)

**Cel:** Integracja RADWAG z ZPSP (do rozmowy z dostawcą).

---

## 6. AVILOG

**Funkcja:** Wewnętrzny system planowania odbioru żywca od hodowców.
- Sergiusz/Paulina wpisują kontrakty hodowców
- Planuje precyzyjnie godziny odbioru — samochód po samochodzie, bez przerwy
- Cel: kurczaki **nie stoją na placu** (stres = strata wagi + jakość)

**Status integracji ZPSP:** Częściowa.
- Pliki ZPSP: `Wstawienie.cs`, `WidokAvilog.cs`, `WidokAvilogPlan.cs`
- Tabele integracyjne (do potwierdzenia w `Wstawienie.Designer.cs`)

---

## 7. UNICARD (RCP — Rejestrator Czasu Pracy)

**Funkcja:** System kontroli czasu pracy (czytniki kart przy wejściu).
- Pracownicy "biją się kartą" przy wejściu i wyjściu
- DB: UNISYSTEM (192.168.0.23\SQLEXPRESS)
- Widok: `V_RCINE_EMPLOYEES`

**Integracja ZPSP:** Pełna.
- Moduł `KontrolaGodzin.xaml.cs` (3100+ linii, 20+ zakładek)
- HR rozszerzenia w tabelach `HR_*` (urlopy, wnioski)

**Pomysł:** Pokazać na "Hala LIVE" KTO jest na hali (wczytane z UNICARD).

---

## 8. WebFleet (TomTom — tracking floty)

**Funkcja:** GPS tracking pojazdów + wgląd w status kierowców.

**API:** Tak, dostępne.

**Integracja ZPSP:** Tak — `MapaFloty/`:
- `FleetAlertService` — alerty (off-route, opóźnienie)
- `KursMonitorService` — real-time monitoring
- `TransportMapWindow` — mapa z pozycjami

---

## 9. Hikvision (kamery przemysłowe)

**Funkcja:** Kamery na hali, w magazynie, na rampie. RTSP streaming.

**Status:** Justyna ogląda — "zerka w kamery" zamiast chodzić po hali (pain point).

**Integracja ZPSP:** Pomysł — ikona w "Hala LIVE" otwierająca live RTSP.

**Plik:** Brak na razie. Do dodania.

---

## 10. Fireflies (transkrypcje spotkań)

**Funkcja:** Nagrywanie spotkań + automatyczne transkrypcje + Action Items.

**Email organizator:** sergiusz.piorko@gmail.com

**Integracja:** Claude Code MCP (`mcp__claude_ai_Fireflies__*`):
- `fireflies_get_transcripts` — lista nagrań
- `fireflies_get_transcript` — szczegółowa transkrypcja
- `fireflies_get_summary` — podsumowanie + action items
- `fireflies_search` — wyszukiwanie tematu w nagraniach

**Sergiusz nagrywa:**
- Rozmowy z kierowcami (Gałek, Kołodziejczyk)
- Spotkania z Justyną (jakość)
- Spotkania z Marciem (wspólnik)
- Wewnętrzne spotkania zarządu

---

## 11. Microsoft 365 / Teams (planowane)

**Funkcja:** Email + kanały komunikacji + dokumenty + spotkania.

**Status:** Planowane, obecnie email Gmail + WhatsApp.

**Plan migracji:**
- WhatsApp grupy → Teams kanały (#sprzedaz, #produkcja, #logistyka, #jakosc, #zarzad)
- Spotkania: Teams zamiast WhatsApp połączeń
- Dokumenty: SharePoint zamiast lokalnych folderów

**Integracja z ZPSP:** Pomysł — alerty kierowane do konkretnych kanałów Teams (np. anulacja >500 kg → #produkcja + #magazyn).

---

## 12. KSeF (Krajowy System e-Faktur)

**Funkcja:** Państwowy system fakturowania (obowiązkowy od 2026).

**Integracja:** Przez Symfonię Handel (Sage prowadzi adapter).

**Status:** Sergiusz to ogarnia jako "Compliance".

---

## 13. IRZplus (Identyfikacja i Rejestracja Zwierząt)

**Funkcja:** Państwowy rejestr zwierząt rzeźnych — obowiązkowy przy przyjęciu żywca.

**Status:** Sergiusz to ogarnia jako "Compliance".

---

## 14. Power BI (Sergiusz, lokalnie)

**Pliki:** `Sprzedaz3.pbix`, `marza.pbix` w `Dokumenty ogólnikowe/`.

**Funkcja:** Analizy post-factum (sprzedaż, marża).

**Integracja z ZPSP:** Brak — Sergiusz używa Power BI **obok** ZPSP, nie zamiast.

---

## Zewnętrzni dostawcy oprogramowania

| Dostawca | Co | Status |
|---|---|---|
| **Sage** | Symfonia Handel | Aktywny |
| **Sage** | Symfonia Production | Kupiony, nieaktywny |
| **DevExpress** | UI komponenty | Licencja ~1100 USD/rok |
| **WAGO** | Waga selektywna | Brak API (Sergiusz prosi) |
| **RADWAG** | Wagi platformowe | Brak API (do rozmowy) |
| **TomTom WebFleet** | Tracking floty | API aktywne |
| **Hikvision** | Kamery | RTSP, brak głębszej integracji |
| **UNICARD** | RCP | SQL view dostępny |
| **AVILOG** | Planowanie żywca | Wewnętrzne dane firmy |

---

## Pomysły integracji (po kolei priorytetu)

1. **WAGO selektywna** — kluczowe (klasy wagowe + A/B per partia per hodowca)
2. **Licznik tuszek** — real-time tempo linii
3. **RADWAG** — automatyczne wpisy do ZPSP zamiast etykiet
4. **Hikvision** — szybki podgląd kamer w ZPSP
5. **Czytniki temperatury** — fizyczne instalacje + integracja
6. **Skanery RFID/kody kreskowe** — rozliczenie partii per klient
7. **M365 / Teams** — alerty z ZPSP do kanałów

**Strategia:** Skup się na #1-3 (najwięcej zysku biznesowego) zanim ruszysz architekturę.
