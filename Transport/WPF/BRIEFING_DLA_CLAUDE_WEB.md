# Briefing dla Claude Web — moduł „Planowanie Transportu (WPF)" w ZPSP (stan: 2026-05-26)

> **Twoja rola (Claude Web):** przeczytaj stan, zaproponuj **priorytetowy plan dalszych kroków** (funkcje + droga do uczynienia modułu oficjalnym zamiast WinForms), a na końcu napisz **bardzo dokładny, długi prompt dla Claude Code** (agent w repo) na 1–2 najbliższe etapy. Pisz po polsku, technicznie i konkretnie.

## 1. Kontekst
- Aplikacja „Kalendarz1" (ZPSP) — WPF .NET 8 dla zakładu drobiarskiego „Piórkowscy". Code-behind (NIE MVVM), conn-stringi hardcoded.
- 4 SQL Server: HANDEL 192.168.0.112 (Sage), LibraNet 192.168.0.109 (zamówienia), TransportPL 192.168.0.109 (transport), UNISYSTEM.
- KRYTYCZNE: HANDEL (.112) NIE jest cross-DB z .109 — łączymy w .NET. TransportPL↔LibraNet (.109) JOIN-y działają.
- Moduł to **piaskownica** `Transport/WPF/` (namespace `Kalendarz1.Transport.WPF`), kafelek accessMap[77], izolowany od produkcyjnego WinForms (`transport-panel-main.cs`, `transport-editor.cs`) i WPF Huba — docelowo go zastąpi.

## 2. Co JUŻ ZROBIONE (działa, skompilowane, wypchnięte na master)
**Funkcjonalność (pełna parzystość z WinForms):** okno planowania (lista kursów + wolne zamówienia), edytor tworzenia/modyfikacji kursu, dialogi (nowy kierowca/pojazd, prompt, szybki przydział). Drag&drop (wolne→kurs/ładunki), szybkie dodawanie, odbiór własny, filtr kursów, auto-odświeżanie 45s, cache nazw (klient/użytkownik/handlowiec), spójne statusy + auto-healing sierot.

**Pełna przebudowa UI/UX (Etapy 1–5 + układ + edytor):**
- Wspólny język wizualny (`Theme/TransportWpfStyles.xaml`): akcent **turkus #00838F**, statusy = znaczenie (zielony/amber/czerwony), ikony **Segoe MDL2**, **karty z cieniem** zamiast ramek, skala typografii, styl pola `Input` (zaokrąglony, akcent przy focusie), przyciski Primary/Ghost/Icon.
- Lista kursów: **lewy pasek statusu 4px „bez czytania"** (pusty=czerwony, brak przydziału=amber, przeładowany=czerwony, OK=zielony), ikony przy kierowcy/pojeździe, metryki zgrupowane + mini-pasek wypełnienia, „Utworzył"→tooltip, avatary handlowców (zdjęcia z sieci).
- **Wolne zamówienia jako sidebar na CAŁĄ wysokość** (od góry do dołu), pogrupowane DNIAMI odbioru (nagłówek „📅 dd.MM ddd [N]"), karty kompaktowe; **drag&drop z ghostem + strefą „Upuść"** i podświetleniem celu.
- Górne paski skrócone i scalone w jedną kartę (nawigacja + akcje + statystyki).
- Edytor: lewa karta = formularz kursu (czysty rząd pól Kierowca/Pojazd/Data/Godziny + Trasa na pełną szerokość, pasek pakowania, ładunki) na całą wysokość; prawa karta = wolne na całą wysokość.

**Faza T — widok Timeline (Gantt) [NOWE 2026-05-27]:** drugi widok okna, przełącznik **Lista ↔ Timeline** (segmented `SegToggle` w górnej karcie, lazy-init — timeline nie ładuje się przy starcie). Oś czasu dnia: wiersze per kierowca (avatar+nazwisko+pojazd+liczba kursów po lewej, 170px), paski kursów pozycjonowane po godzinach wyjazd→powrót (`HourStart=6..HourEnd=22`, zoom 24–64 px/h, `RowHeight=50`). Pasek = 4-stanowa kolorystyka „bez czytania" (pusty/przeładowany=czerwony, brak przydziału=amber, OK=zielony) + badge %wypełnienia + tooltip (trasa/czas/KP/metryki/utworzył). **Wykrywanie konfliktów** (nakładające się kursy kierowcy → czerwona gruba ramka). **Linia „teraz"** (czerwona, tylko dziś, auto-update 60s). **Drag&drop wolnego zamówienia: 2 cele** — na pasek kursu (=przypisanie) lub na pusty obszar wiersza (=nowy kurs z preselekcją kierowcy + sugerowaną godziną zaokrągloną do 15 min, z potwierdzeniem). Synchroniczny scroll nagłówka/wierszy/kanwy. Pliki: `Views/TimelineDniaView.xaml(.cs)`, `Controls/KursBarControl.xaml(.cs)`, `Models/TimelineModels.cs` (`KursBar`, `KierowcaWierszTimeline`), metody serwisu `LoadKierowcyZKursamiAsync`/`WykryjKonflikty`/`ZapiszKursPrzeniesionyAsync`/`DodajWolneDoKursuAsync`/`UtworzKursIDodajAsync`. **Degradacja (świadoma):** bez dzwonka oczekujących zmian (wymaga Fazy 2) i bez kreskowania niedostępności kierowcy (brak kolumn nieobecności w DB).

**Pliki:** `PlanowanieTransportuWpfWindow`, `EdytorKursuWpfWindow`, `Services/TransportWpfService`, `Models/TransportWpfModels` + `Models/TimelineModels`, `Controls/AvatarControl` + `Controls/KursBarControl`, `Views/TimelineDniaView`, `Theme/TransportWpfStyles`, `WpfDragHelper` (+ `DragGhostAdorner`, `FmtWolne`), `Dialogs/` (4). Reuse: `TransportRepozytorium` (TransportPL).

## 3. Fundament — tabele/kolumny (Claude Code to zna)
- TransportPL: `Kurs`, `Ladunek` (KodKlienta='ZAM_{id}'), `Kierowca`, `Pojazd`, widok `vKursWypelnienie`. Repo `TransportRepozytorium` (UsunKursAsync sam zwalnia statusy).
- LibraNet: `ZamowieniaMieso` (DataPrzyjazdu=awizacja, DataUboju, TransportStatus ['Oczekuje'/'Przypisany'/'Wlasny'], TransportKursId, LiczbaPalet/Pojemnikow); `ZamowieniaMiesoTowar` (Ilosc=KG); `operators` (ID,Name); `UserHandlowcy` (HandlowiecName→UserID).
- HANDEL (osobne połączenie): `SSCommon.STContractors` (Shortcut), `ContractorClassification` (CDim_Handlowiec_Val), `STPostOfficeAddresses`.
- Filtr wolnych (jak stary): `CAST(DataUboju AS DATE)=@Data` + `TransportStatus NOT IN ('Przypisany','Wlasny')` + `TransportKursID IS NULL`; grupowanie po DataPrzyjazdu.Date.
- Avatary: `\\192.168.0.170(.171)\Install\Prace Graficzne\Avatary\{userId}.png` (UserAvatarManager.GetAvatarFilePathOrNull), fallback inicjały.

## 4. Reguły i pułapki (MUSZĄ być respektowane)
- **Spójność statusów:** przypisanie żyje w 2 bazach (LibraNet.TransportStatus/TransportKursId + TransportPL.Ladunek) — muszą być spójne. „Sierota" = status/KursId bez ładunku → znika z widoku. Rozwiązanie: `SyncStatusyKursuAsync` (status tylko dla faktycznych ładunków + auto-healing). Skrypt: `Transport/SQL/fix_orphan_transport_status.sql`.
- WPF: NIE `Run.Text` z bindingiem (TwoWay→błąd na read-only) — używać `TextBlock.Text`. Grupowane listy = `ListBox` + GroupStyle, NIE grupowany DataGrid. Pilnować szerokości kolumn (gwiazdka „Klient" nie może zniknąć).
- Build: `dotnet build Kalendarz1.csproj`; **MSB3027/MSB3021 = uruchomiona aplikacja, NIE błąd**. Cross-DB tylko w .NET. Po każdym etapie: build (0 błędów) + commit PL z `Co-Authored-By` + push. Nie ruszać WinForms.
- Profil usera (Sergiusz, właściciel-developer): pracuje iteracyjnie, ogląda na żywo, lubi wierność + estetykę (kompaktowość, czytelność, avatary, grupowanie dniami). Każdy etap kończyć buildem+commitem+push.

## 5. Co ZOSTAŁO / obszary do planu
1. **Edytor — live-refresh** (auto-aktualizacja zmienionych zamówień co ~10s, jak stary) + edycja inline pojemników/uwag.
2. **Wskaźnik „Alert"** dla kursów z oczekującymi zmianami (`TransportPL.TransportZmiany` + `TransportZmianyService`) — integracja z systemem akceptacji zmian.
3. **Comboboxy/DatePicker** — przeszablonowanie do stylu pól `Input` (zaokrąglenie, akcent przy focusie) dla pełnej spójności formularza.
4. **Mapa trasy** kursu (moduł MapaFloty/Webfleet — Webfleet na OAuth, GPS chwilowo nie działa).
5. **Ścieżka „oficjalna":** checklista parytetu 1:1, testy ręczne, przełączenie kafelka/menu, wycofanie starego WinForms, komunikat dla zespołu.
6. **Walidacje i odporność:** walidacja zapisu (godziny, brak pojazdu vs pakowanie), timeouty/wyjątki cross-DB, zachowanie zaznaczenia, wydajność przy dużej liczbie kursów/zamówień, mniejsze ekrany.
7. **Timeline — dociągnięcie z degradacji:** dzwonek/badge oczekujących zmian na pasku kursu (po Fazie 2 + `TransportZmianyService`), kreskowanie okien niedostępności kierowcy (wymaga kolumn nieobecności/urlopów w DB), drag paska kursu w poziomie = zmiana godzin (jest `ZapiszKursPrzeniesionyAsync`, brak UI), zwijanie pustych wierszy kierowców.
8. Brak test runnera (nie dodawać testów bez prośby).

## 6. TWOJE ZADANIE
1. **Priorytetowy plan** dalszych kroków (fazy: cel, zadania, pliki/tabele, ryzyka, kryteria „gotowe"); co da największą wartość i co blokuje „oficjalność".
2. **Bardzo dokładny, długi PROMPT dla Claude Code** na 1–2 pierwsze fazy: zakres+kolejność, konkretne pliki/metody/tabele, reguły (§4), kryteria akceptacji + weryfikacja, prośba o aktualizację notatek i commity PL. Pisz jak do doświadczonego programisty WPF/.NET z pełnym dostępem do repo i baz.
