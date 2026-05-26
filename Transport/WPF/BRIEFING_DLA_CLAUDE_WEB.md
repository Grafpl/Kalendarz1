# Briefing dla Claude Web — moduł „Planowanie Transportu (WPF)" w aplikacji ZPSP

> **Twoja rola (Claude Web):** Przeczytaj ten dokument, zrozum stan projektu, a następnie:
> 1. Zaproponuj **przemyślany, fazowy plan** dokończenia i dopracowania modułu (oraz ścieżki, jak uczynić go oficjalną wersją zastępującą stary WinForms).
> 2. Na końcu napisz **bardzo dokładny, długi prompt dla Claude Code** (agenta pracującego bezpośrednio w repo), który poprowadzi dalsze prace krok po kroku.
> Pisz po polsku. Bądź konkretny i techniczny — Claude Code ma pełny dostęp do repo i baz, więc prompt ma go *kierować*, nie tłumaczyć podstaw od zera.

---

## 1. Kontekst projektu

- **Aplikacja:** „Kalendarz1" (wewn. **ZPSP**) — desktopowa **WPF .NET 8** (`net8.0-windows7.0`) dla zakładu drobiarskiego „Piórkowscy" (~258 mln obrotu, 200 t/dzień). Łączy Sage Symfonia (HANDEL), system wagowy LibraNet, RCP UNICARD i własne tabele.
- **Architektura (świadome decyzje):**
  - **Code-behind, NIE MVVM** — eventy w XAML, `x:Name` + bezpośredni dostęp w `*.xaml.cs`. (Wyjątek: nowe okna mogą używać lekkich wzorców, ale projekt celowo unika ViewModeli.)
  - **Connection stringi hardcoded** w klasach okien/serwisów (legacy pattern całego modułu transportu).
  - Menu główne to hybryda: `Menu.cs` (WinForms) odpala okna WPF i formularze WinForms.
- **4 instancje SQL Server:**
  - **HANDEL** `192.168.0.112` — Sage Symfonia (kontrahenci, faktury).
  - **LibraNet** `192.168.0.109` — wagi, **zamówienia mięsa**, operatorzy.
  - **TransportPL** `192.168.0.109` — moduł transportu (kursy, ładunki, kierowcy, pojazdy).
  - **UNISYSTEM** `192.168.0.23\SQLEXPRESS` — RCP/HR.
- **KRYTYCZNE ograniczenie cross-DB:** HANDEL (.112) **NIE jest** dostępny z .109 (brak linked server). **Żadnych cross-DB JOIN do HANDEL** — dane łączymy w .NET (LINQ in-memory). TransportPL ↔ LibraNet (ta sama instancja .109) JOIN-y działają.
- **Login/użytkownik:** `App.UserID`, `App.UserFullName`. Admin (`App.IsAdmin` lub login „11111") widzi wszystkie kafelki niezależnie od uprawnień.

---

## 2. Co zbudowaliśmy — moduł „Transport WPF" (piaskownica → docelowo oficjalna wersja)

**Cel biznesowy:** nowa, czysto-WPF wersja **planowania transportu** (przegląd kursów + edytor tworzenia/modyfikacji kursu + pula wolnych zamówień), **izolowana** od starego WinForms, którą docelowo zastąpimy produkcję. User pracuje iteracyjnie i chce, żeby było „bardzo dobrze" wizualnie i funkcjonalnie, **wiernie do starego, ale ładniej**.

### 2.1. Lokalizacja i pliki (`Transport/WPF/`, namespace `Kalendarz1.Transport.WPF`)
- `PlanowanieTransportuWpfWindow.xaml(.cs)` — **główne okno**: lista kursów (lewo) + panel WOLNE ZAMÓWIENIA (prawo).
- `EdytorKursuWpfWindow.xaml(.cs)` — **edytor kursu** (nowy/edycja): nagłówek + ładunki + wolne zamówienia + pasek pakowania.
- `Services/TransportWpfService.cs` — warstwa danych (opakowuje `TransportRepozytorium`, dokłada cross-DB do LibraNet/HANDEL, cache, synchronizacja statusów).
- `Models/TransportWpfModels.cs` — `WolneZamowienieWpf`, `LadunekWierszWpf` (INotifyPropertyChanged), `ZamowienieNazwaInfo`.
- `Controls/AvatarControl.xaml(.cs)` — okrągły avatar (zdjęcie z sieci lub inicjały).
- `Theme/TransportWpfStyles.xaml` — wspólny motyw siatek (nagłówki/komórki/wiersze).
- `Dialogs/` — `NowyKierowcaWpfDialog`, `NowyPojazdWpfDialog`, `TekstPromptDialog` (uniwersalny input), `SzybkiPrzydzialDialog` (kierowca+pojazd).
- `WpfDragHelper.cs` — helpery drag&drop + `GrupujKolekcje` (CollectionView grouping).
- Kafelek w menu: `Menu.cs` → `accessMap[77] = "TransportWPF"`, kategoria OPAKOWANIA I TRANSPORT, otwiera `PlanowanieTransportuWpfWindow`. **Stary WinForms (`TransportMainFormImproved`) i WPF Hub (`TransportHubWindow`) pozostają nietknięte.**

### 2.2. Okno planowania — co zawiera i jak wygląda
- **Pasek górny:** tytuł, nawigacja datą (◀ DatePicker ▶, „Dziś"), przyciski: 🚚 Nowy kurs / 📝 Edytuj / 🗑 Usuń / 🔄 / ⟳ Auto (auto-odświeżanie 45 s).
- **Pasek KPI:** Kursów / Z kierowcą / Wymaga przydziału / Suma palet / ⚠ Wolne (bez kursu) + **filtr kursów** (szukaj trasa/kierowca/pojazd + checkbox „tylko wymagające uwagi").
- **Lewa kolumna — lista kursów (DataGrid), wiernie do starego WinForms, z avatarami:**
  kolumny: **Wyj. (godz.) · Kierowca · Pojazd · Trasa · Pal · Poj · KG · Wyp.% · Utworzył (avatar+nazwa+data) · Handl. (do 3 nakładających się avatarów + skrót)**. „⚠ BRAK" pomarańczowy gdy brak kierowcy/pojazdu; Wyp.% kolorowany (zielony/pomarańcz/czerwony); **kolor wiersza**: pusty kurs = czerwonawy, brak przydziału = żółty. (Kolumnę „Status" usunięto na życzenie usera.)
  Menu kontekstowe: Edytuj / Szybki przydział / Usuń. Dwuklik = edycja. Skróty: Insert=nowy, Delete=usuń, Enter=edytuj, F5=odśwież.
  **Dawniej był** dolny panel master-detail „Ładunki kursu" — **usunięty na życzenie usera**.
- **Prawa kolumna — WOLNE ZAMÓWIENIA (ListBox z kartami, pogrupowane DNIAMI):**
  - Nagłówek grupy: `📅 dd.MM ddd  [N] zam.` (dzień odbioru/awizacji, kultura pl-PL, chronologicznie).
  - **Karta zamówienia (kompaktowa, rozdzielona separatorem):** `[avatar handlowca] Klient (bold) + „Ubój dd.MM ddd · handlowiec"  |  🕐 godzina + data awizacji ; pigułki [N poj] [N,N pal]`.
  - Toolbar: szukaj (klient/handlowiec), przełącznik **Ubój/Odbiór** (po której dacie filtrować dzień), 🔄.
  - Akcja: zaznacz kurs (lewo) + zamówienia (prawo) → **➕ Dodaj do kursu** (lub dwuklik / **drag&drop** karty na wiersz kursu). Menu kontekstowe: Dodaj / 🚗 Odbiór własny / Odśwież.
  - Stany puste: „📭 Brak kursów…" / „✅ Brak wolnych zamówień…".

### 2.3. Edytor kursu — co zawiera
- **Nagłówek:** Kierowca (ComboBox editable + szukaj + ＋ nowy), Pojazd (＋ nowy), Data, Godziny wyjazd→powrót, Trasa (+ „auto" z nazw klientów), **info bar** (Utworzył/Zmienił), **pasek pakowania palet** (36 E2/paleta, kolor wg %).
- **Ładunki w kursie (DataGrid):** kolejność (▲▼ / Sortuj po awizacji), menu kontekstowe (edytuj uwagi / góra-dół / usuń), edycja uwag (TekstPromptDialog), **drag&drop**: z wolnych → ładunki (Copy) i reorder (Move) z podświetlaniem celu.
- **Wolne zamówienia (ListBox z kartami, jak w planowaniu).**
- **Zapis:** Dodaj/Aktualizuj/Usuń ładunki + **`SyncStatusyKursuAsync`** (spójne statusy + auto-healing — patrz §4.3).

---

## 3. Fundament techniczny — bazy, tabele, kolumny (Claude Code to zna, ale i Ty musisz)

### 3.1. TransportPL (.109) — przez `TransportRepozytorium` (reuse w całości, UI-niezależne)
- `dbo.Kurs` (KursID, DataKursu, KierowcaID?, PojazdID?, Trasa, GodzWyjazdu?, GodzPowrotu?, Status, PlanE2NaPalete, UtworzonoUTC/Utworzyl, ZmienionoUTC/Zmienil) + widok `vKursWypelnienie`.
- `dbo.Ladunek` (LadunekID, KursID, Kolejnosc, **KodKlienta = "ZAM_{id}"**, PojemnikiE2, PaletyH1?, PlanE2NaPaleteOverride?, TrybE2, Uwagi).
- `dbo.Kierowca` (KierowcaID, Imie, Nazwisko, Telefon, Aktywny), `dbo.Pojazd` (PojazdID, Rejestracja, Marka, Model, PaletyH1=33 default, Aktywny).
- Metody repo: PobierzKursyPoDacieAsync, PobierzKursAsync, DodajKursAsync, AktualizujNaglowekKursuAsync, **UsunKursAsync (sam przywraca statusy zamówień)**, PobierzLadunkiAsync, PobierzLadunkiDlaKursowAsync, DodajLadunekAsync, AktualizujLadunekAsync, UsunLadunekAsync, RenumerujLadunkiAsync, ObliczPakowanieZLadunkow, Pobierz/Dodaj Kierowcow/Pojazdy.

### 3.2. LibraNet (.109) — zamówienia + operatorzy + mapowanie handlowców
- `dbo.ZamowieniaMieso` (Id, KlientId, **DataPrzyjazdu** = awizacja/dzień odbioru, **DataUboju**, DataZamowienia, LiczbaPalet, LiczbaPojemnikow, TrybE2, **TransportStatus** ['Oczekuje'/'Przypisany'/'Wlasny'], **TransportKursId**, Status).
- `dbo.ZamowieniaMiesoTowar` (ZamowienieId, Ilosc) — **suma Ilosc = KG zamówienia**.
- `dbo.operators` (ID, Name) — userId → pełna nazwa (do podpisu „Utworzył" i avatara).
- `dbo.UserHandlowcy` (HandlowiecName, UserID) — **mapowanie nazwy handlowca → userId** (do zdjęcia avatara handlowca).

### 3.3. HANDEL (.112) — TYLKO osobnym połączeniem (NIE cross-DB)
- `SSCommon.STContractors` (Id, **Shortcut** = nazwa klienta), `SSCommon.ContractorClassification` (ElementId, **CDim_Handlowiec_Val** = nazwa handlowca), `SSCommon.STPostOfficeAddresses` (adres domyślny).
- Łączenie: zamówienie → KlientId → (osobne zapytanie do HANDEL) → nazwa/handlowiec/adres. Wynik scalany w .NET.

### 3.4. Filtr „wolnych zamówień" — WIERNIE jak stary `transport-panel-main.cs`
```sql
WHERE CAST(DataUboju AS DATE) = @Data        -- JEDEN dzień (po uboju; toggle Odbiór→DataPrzyjazdu)
  AND ISNULL(Status,'Nowe') NOT IN ('Anulowane')
  AND ISNULL(TransportStatus,'Oczekuje') NOT IN ('Przypisany','Wlasny')
  AND TransportKursID IS NULL
```
Grupowanie w UI po `DataPrzyjazdu.Date` (dzień odbioru).

---

## 4. Reguły domenowe i pułapki (MUSZĄ być respektowane)

### 4.1. Pakowanie palet
- 36 E2/paleta (lub override per ładunek), pojemność pojazdu = `Pojazd.PaletyH1` (domyślnie 33). Wyp.% = palety_potrzebne / pojemność. Realny zakres ważenia palety to osobny temat (Analityka), tu liczymy z `PojemnikiE2`.

### 4.2. Avatary
- Zdjęcia PNG na sieci: `\\192.168.0.170\Install\Prace Graficzne\Avatary\{userId}.png` (oraz `.171`). Helper: `UserAvatarManager.GetAvatarFilePathOrNull(userId)`. Brak pliku → inicjały na kolorowym kółku (8 deterministycznych kolorów z hasha ID). `AvatarControl` cache'uje ścieżki i obrazy statycznie.
- Handlowiec: nazwa (`CDim_Handlowiec_Val`) → `UserHandlowcy` → userId → zdjęcie.

### 4.3. SPÓJNOŚĆ STATUSÓW (najważniejsza pułapka — „sieroty")
- Przypisanie zamówienia do kursu żyje w **DWÓCH bazach**: `LibraNet.ZamowieniaMieso` (TransportStatus + TransportKursId) **i** `TransportPL.Ladunek` (KodKlienta='ZAM_{id}'). **Muszą być spójne.**
- **Sierota** = status 'Przypisany' i/lub TransportKursId ustawiony, ale BRAK rekordu Ladunek → zamówienie „znika" (niewidoczne w kursie i w puli wolnych). Realny incydent: klienci „Cezar"/„Trzepałka".
- Naprawione: `SyncStatusyKursuAsync(kursId, zamIdyWKursie, user)` ustawia 'Przypisany' tylko dla zamówień faktycznie w `_ladunki` **i robi auto-healing**: każde zamówienie wskazujące na ten kurs (TransportKursId=kursId), którego nie ma w ładunkach, jest zwalniane na 'Oczekuje'. Skrypt diagnostyczny: `Transport/SQL/fix_orphan_transport_status.sql`. (Commity przyczyny/auto-healingu: `6a326bf`, `e59126c`, `8860d9d`.)

### 4.4. Pułapki WPF napotkane (żeby plan ich unikał)
- `Run.Text="{Binding ...}"` próbuje TwoWay → wyjątek na właściwościach read-only. **Używać `TextBlock.Text`**, nie `Run` z bindingiem.
- **Grupowany `DataGrid`** z customowym szablonem komórek renderował puste wiersze → przepisaliśmy wolne na **`ListBox` + GroupStyle** (niezawodne). Wniosek: do grupowanych list używać ListBox/ItemsControl, nie DataGrid.
- Kolumny stałej szerokości „zjadały" kolumnę gwiazdkową (Klient) → „nic nie widać". Pilnować budżetu szerokości; dzień trzymać w nagłówku grupy, nie w kolumnach.
- Merged ResourceDictionary: `Source="/Kalendarz1;component/Transport/WPF/Theme/TransportWpfStyles.xaml"`.
- Build: `dotnet build Kalendarz1.csproj`. **MSB3027/MSB3021 = aplikacja uruchomiona (exe zablokowany), NIE błąd kompilacji.** Wiele pre-existing CS8618/NU1603 warnings — ignorować.

---

## 5. Parytet ze starym WinForms (źródło prawdy do odtworzenia)
- `Transport/transport-panel-main.cs` (4962 linie) — stary panel: lista kursów + wolne zamówienia grupowane dniami + pulsujący „Alert", podsumowanie, avatary malowane ręcznie.
- `Transport/transport-editor.cs` (3969 linii) — stary edytor: ładunki, palety, drag&drop, live-update, dialogi +kierowca/+pojazd.
- Odtworzyliśmy: kolumny, avatary (Utworzył + Handlowcy), kolory wierszy, grupowanie wolnych po dniach, kolumny wolnych, filtr wolnych. **NIE odtworzono jeszcze:** kolumna „Alert" (pulsujące powiadomienie o oczekujących zmianach z `TransportZmiany`), pełny live-refresh, podsumowanie dolne (mamy KPI górne).

---

## 6. Stan: co DZIAŁA, co ZOSTAŁO, znane braki/ryzyka
**Działa (skompilowane, 0 błędów CS/MC; ostatnie commity do `19e49df`):** lista kursów z avatarami, wolne pogrupowane dniami (karty), drag&drop (wolne↔kursy/ładunki), szybkie dodawanie, szybki przydział, odbiór własny, filtr kursów, auto-refresh, cache nazw klientów/użytkowników/handlowców, spójne statusy + auto-healing, dialogi nowy kierowca/pojazd, edycja uwag, sortowanie, pakowanie.

**Zostało / do przemyślenia (zakres dla Twojego planu):**
1. **Edytor — live-refresh** (auto-aktualizacja zmienionych zamówień co ~10 s, jak stary) + edycja inline pojemników.
2. **Kolumna/wskaźnik „Alert"** dla kursów z oczekującymi zmianami (`TransportPL.TransportZmiany` + `TransportZmianyService`) — integracja z istniejącym systemem akceptacji zmian.
3. **Mapa trasy** kursu (jest osobny moduł MapaFloty/Webfleet — Webfleet przeszedł na OAuth 2.0 i wymaga poświadczeń; GPS chwilowo nie działa).
4. **Ścieżka „uczynienia oficjalną":** parytet 1:1 ze starym, checklista testów, przełączenie kafelka/menu, wycofanie starego WinForms, migracja ewentualnych różnic w danych, szkolenie/komunikat.
5. **Walidacje i odporność:** walidacja zapisu (godziny, brak pojazdu vs pakowanie), obsługa wyjątków/timeoutów cross-DB, zachowanie zaznaczenia po odświeżaniu, wydajność przy dużej liczbie zamówień/kursów.
6. **Spójność wizualna** całego okna (lista kursów też ma sporo kolumn — na mniejszych ekranach może być ciasno).
7. **Brak testów** (projekt nie ma test runnera — nie dodawać bez prośby).

---

## 7. Profil i preferencje usera (ważne dla tonu planu i prompta)
- User = **Sergiusz**, właściciel, sam programuje ZPSP (klient-developer). Pracuje **bardzo iteracyjnie**, ogląda efekt na żywo (zrzuty), oczekuje **wierności staremu + lepszej estetyki**, mówi krótko („zrób lepiej", „bardzo źle", „nic nie widać"). Lubi: kompaktowość, czytelność, rozdzielenie sekcji, prawdziwe avatary, grupowanie dniami.
- Każda zmiana powinna kończyć się **buildem (0 błędów) i commitem + push** (po polsku, z `Co-Authored-By`). Izolacja od WinForms musi być utrzymana, dopóki nie zdecydujemy o przełączeniu.
- Repo to Git (branch `master`), Windows, PowerShell. Aplikacja często uruchomiona (MSB3027 ≠ błąd).

---

## 8. TWOJE ZADANIE (Claude Web)
1. **Plan fazowy** (np. Faza A: dokończenie funkcji edytora i live-refresh; Faza B: integracja Alert/TransportZmiany; Faza C: parytet + przełączenie na oficjalną; Faza D: dopieszczenie UX/wydajność/walidacje). Dla każdej fazy: cel, konkretne zadania, pliki/tabele, ryzyka, kryteria „gotowe".
2. **Priorytetyzacja** — co da największą wartość najszybciej, co jest blokujące dla „oficjalności".
3. **Bardzo dokładny, długi PROMPT dla Claude Code** na pierwszą (lub 1–2) fazę, zawierający:
   - jednoznaczny zakres i kolejność kroków,
   - konkretne pliki/metody/tabele do tknięcia,
   - reguły, których musi przestrzegać (izolacja, spójność statusów, ListBox-zamiast-DataGrid do grup, brak Run-binding, cross-DB tylko w .NET, build+commit+push po każdym etapie, nie ruszać starego WinForms),
   - kryteria akceptacji + jak zweryfikować (build, ewentualnie ręczny test na żywo),
   - prośbę o aktualizację pamięci/notatek i sensowne commit messages.

> Napisz prompt tak, jakbyś instruował doświadczonego programistę WPF/.NET, który ma pełny dostęp do repo i baz. Ma być **długi, precyzyjny i samowystarczalny**.
