# Brief dla Claude (web) — moduł „Karta Klienta 360" w ZPSP

> Wklej to do claude.ai. Na końcu jest dokładna prośba: przemyśl plan i napisz prompt dla Claude Code (agenta pracującego w repo).

---

## 1. Kontekst projektu

**Kalendarz1** (wewn. „ZPSP") — aplikacja **WPF .NET 8.0** (`net8.0-windows7.0`) dla ubojni/zakładu drobiarskiego „Piórkowscy" (~258 mln PLN obrotu, 200 t/dzień). Łączy 4 bazy SQL Server:
- **HANDEL** (192.168.0.112) — Sage Symfonia (kontrahenci, faktury). Schemat `HM.*` + `SSCommon.*`.
- **LibraNet** (192.168.0.109) — system wagowy, **zamówienia mięsa** (`ZamowieniaMieso`, `ZamowieniaMiesoTowar`), własne tabele kartoteki klientów.
- **TransportPL** (192.168.0.109) — transport (kierowcy, pojazdy, kursy).
- UNISYSTEM — RCP/HR (nieistotne tutaj).

**Konwencje (twarde reguły repo):**
- **Code-behind, NIE MVVM** — eventy w XAML, `x:Name` + dostęp w `*.xaml.cs`. (Tak ma zostać.)
- Connection stringi hardcoded w klasach okien (legacy).
- Brak cross-DB JOIN — łączenie danych w .NET (LINQ in-memory).
- Nullable ON; nowy kod ma nie generować nowych CS8618.
- Uruchomiona aplikacja blokuje `Kalendarz1.exe` → błędy MSB3027/MSB3021 i przejściowe błędy `*_wpftmp` przy kompilacji XAML — to NIE są realne błędy kompilacji.

---

## 2. Co zbudowaliśmy (ostatnie sesje) — scalenie Customer360 + Kartoteka Odbiorców

Były **dwa kafelki robiące to samo**: „Kartoteka Odbiorców" (lista klientów + edycja) i „Customer 360" (karta analityczna). Scaliliśmy w jeden system:

- **Wejście:** menu „Kartoteka Odbiorców" (accessMap[26]) → lista wszystkich klientów (akordeon, 16 kolumn). **Dwuklik wiersza → otwiera `Customer360Window`** (bogata karta). Kafelek „Customer 360" usunięty z menu (accessMap[73] zostawiony — nie ruszamy indeksów permissions).
- Kartoteka ma też przycisk „📊 Pulpit Portfela" → `PulpitPortfelaWindow` (landing całego portfela).

### Karta klienta — `Customer360/Customer360Window.xaml(.cs)`
Pasek górny: wybór klienta, **🔎 filtr aktywnej tabeli**, **◀ ▶ nawigacja między klientami** (+ Ctrl+←/→), **ComboBox okresu** (Cała historia / 12M / 6M / 3M), **⚖ Porównaj**, **🕘 Ostatni** (ostatnio otwierani, plik w LOCALAPPDATA), **📥 Eksport** (aktywna tabela → CSV/Excel), **🐛 Debug**, **🔄 Odśwież**. Skróty: Esc zamyka, F5 odświeża. Overlay ładowania.

**11 zakładek pogrupowanych w 4 (zagnieżdżone TabControl):**
- **📊 Przegląd** (dashboard): hero ze **scoringiem A-F** (5 pasków składników: terminowość/historia/regularność/trend/limit) + **churn** + rekomendowany limit; chipy ryzyka (wykorzystanie limitu z paskiem, przeterminowane, dni od ost. zamówienia, reklamacje); KPI finansowe (Obrót 12M+YoY, Marża, Zamówienia, Limit/Do zapłaty); **wykres obrotu miesięcznego** (tooltips, linia średniej, podświetlony max, polskie miesiące, **klik słupka → szczegóły miesiąca**); Top 5 towarów ze zdjęciami; panel alertów (auto-sygnały).
- **💰 Sprzedaż**: Zamówienia · Faktury (z banerem zakresu dat + rozbiciem po latach) · **Weryfikacja** (poniżej) · Anulowane.
- **👤 Klient**: Dane (pełna edycja: kontakt, kategoria A/B/C/D, preferencje, trasa, notatki → zapis do LibraNet + log do historii) · Kontakty (CRUD wielu osób).
- **📈 Analiza**: Scoring (szczegóły) · Historia zmian (audit) · Transport (top kierowcy/pojazdy/trasy) · Asortyment (tabela + **wykres udziału % top 8 towarów**).

**Zakładka Weryfikacja (świeżo przeprojektowana, „inaczej"):** porównuje **zamówione (LibraNet) vs zafakturowane (HANDEL)**:
- Werdykt-hero: kółko realizacji % (zielone ~100% / żółte / czerwone), jednozdaniowy werdykt, paski zam→fak.
- Klikalne chipy filtrów: Wszystkie / ✂ Ucięte / ➕ Więcej / ⚠ Brak faktury / ✅ Zgodne (z licznikami).
- Lista pozycji per towar (renderowana, nie DataGrid): ikona+nazwa+status, podwójne paski (zam niebieski / fak zielony lub **czerwony gdy <95%**), różnica kg+zł, **sort: problemy na górze**.
- Wykres niedotrzymania miesięcznego (zamówione vs zafakturowane kg).
- **Weryfikacja liczy BEZ korekt** (tylko `FVS/FVR/FVZ`).

### `Customer360/PulpitPortfelaWindow` — landing całego portfela
6 KPI (klienci, obrót portfela 12M, suma przeterminowanych, liczba z przeterminowanymi, przekroczony limit, churn). 3 zakładki: 🚨 Alerty kredytowe · 📉 Churn (>60 dni bez faktury) · 🏆 Top klienci (+udział %). Kolorowanie wierszy wg ryzyka, dwuklik → karta, search, eksport, „🗺 Mapa klientów".

### Mapa — `Kartoteka/Features/Mapa/`
Auto-geokodowanie z adresu (Nominatim, 1 req/s) + naprawa zapisu współrzędnych na **UPSERT** (klienci bez wiersza w tabeli też się zapisują).

### Inne nowe pliki
`Customer360/Services/`: `Customer360Service` (główny agregator), `PortfelService`, `RecentClientsStore`. Okna: `SzczegolyMiesiacaDialog` (drill-down miesiąca), `PorownanieKlientowWindow` (2 klienci obok siebie, lepszy=zielony), `Customer360DiagWindow` (debugger z auto-werdyktem, kopiuj/zapis). Reuse: `KartotekaService`, `ScoringService`, `HistoriaZmianService`.

---

## 3. Kluczowe odkrycia o danych (gotchas — ważne!)

1. **Faktury sprzedaży = TYLKO `typ_dk IN ('FVS','FVR','FVZ')`** (potwierdzone w 5 modułach). Inne typy (FW, FVZ-warianty, PAR…) to nie sprzedaż klientowi.
2. **Korekty** dołączane przez `DK.iddokkoryg` (dokument korygowany), nie po nazwie typu. W **obrocie** je wliczamy, w **weryfikacji NIE**.
3. **`ZamowieniaMiesoTowar` (pozycje zamówień) istnieją dopiero od ~10/2025!** Nagłówki `ZamowieniaMieso` są od 01/2025, ale pozycje (ilość/cena) dopiero od października. Skutek: **obrót/marża liczone z zamówień×cena były zaniżone** (INNER JOIN wycinał wcześniejsze miesiące). **Przełączyliśmy „Obrót miesięczny" i KPI „Obrót 12M" na FAKTURY** (HANDEL `walbrutto`) — pełna historia.
4. **`ZamowieniaMieso.IdUser` to INT** — `ISNULL(IdUser,'') + reader.GetString()` rzuca cast Int32→String (łapane w catch → pusta lista). Użyć `CAST(... AS NVARCHAR)`.
5. HANDEL ma faktury od **2021-12-28** (4+ lata). LibraNet zamówienia od 01/2025.
6. `KlientId` (LibraNet) = `STContractors.Id` = `DK.khid` (HANDEL). Bezpośrednie dopasowanie.

---

## 4. Znane słabości / rzeczy niedokończone (do planu!)

1. **Marża 12M** wciąż liczona z zamówień×cena → tylko od 10/2025, **zaniżona i niespójna** z obrotem (z faktur). Marży nie da się policzyć z `HM.DP` (jest cena sprzedaży, brak kosztu). Trzeba zdecydować: ukryć/zastąpić (np. „śr. wartość faktury") albo dociągnąć koszt skądś.
2. **Scoring (`ScoringService.ObliczScoringAsync`) przelicza się i ZAPISUJE do bazy przy KAŻDYM otwarciu karty** — niepotrzebne obciążenie + zapis. Powinien być cache / „policz na żądanie".
3. **Porównanie 2 klientów** woła pełne `GetKpiAsync` (wiele zapytań) — może być wolne.
4. Grupowanie zakładek: Historia zmian wylądowała w „📈 Analiza" zamiast przy „👤 Klient" (zrobione tak, by uniknąć przenoszenia bloków XAML — logicznie pasuje do Klient).
5. **Eksport do PDF** całej karty — NIE zrobiony (QuestPDF jest w repo, użyty w module HDI).
6. Plik `Customer360Window` fizycznie został w folderze `Customer360/` (namespace `Kalendarz1.Customer360`), choć logicznie należy do Kartoteki — świadomie nie przenosiłem, by nie psuć referencji.
7. Brak testów (projekt nie ma test-runnera — i tak nie piszemy testów bez prośby).
8. Wiele wykresów to ręcznie rysowane słupki w `Grid` (code-behind), bez biblioteki — działa, ale rozjeżdża się przy bardzo wielu miesiącach; brak osi Y/siatki.
9. Wydajność: karta robi ~10 zapytań równolegle (`Task.WhenAll`) przy każdym otwarciu + 5 zakładek Kartoteki sekwencyjnie. Dla nawigacji ◀▶ między klientami to dużo.

---

## 5. Czego potrzebuję od Ciebie (Claude web)

1. **Przemyśl i zaproponuj uporządkowany plan**, jak zrobić ten moduł „dobrze" — priorytetyzowany (co najpierw, co potem), uwzględniając:
   - spójność metryk finansowych (obrót vs marża — co z marżą bez kosztu?),
   - wydajność (cache scoringu, mniej zapytań przy nawigacji, ewentualnie cache per-klient),
   - czytelność/UX (czy wykresy ręczne wystarczą, czy warto bibliotekę; co z eksportem PDF),
   - architekturę (czy warto wydzielić warstwę zapytań, skoro reszta repo jest code-behind — pamiętaj: NIE MVVM, connection stringi hardcoded, to celowe).
2. **Nie proponuj przepisywania na MVVM ani dużych zależności** — repo jest świadomie code-behind. Trzymaj się stylu.
3. **Napisz gotowy, precyzyjny prompt dla Claude Code** (agenta w repo), który:
   - rozbije pracę na małe, weryfikowalne kroki (każdy zakończony buildem),
   - wskaże konkretne pliki/metody do zmiany (te wymienione wyżej),
   - jasno określi decyzje (np. „marżę zastąp śr. wartością faktury z faktur" albo „dociągnij koszt z X"),
   - przypomni o gotchas z sekcji 3 (FVS/FVR/FVZ, korekty via iddokkoryg, ZamowieniaMiesoTowar od 10/25, IdUser INT),
   - uwzględni, że aplikacja bywa uruchomiona (build może nie podmienić exe — to normalne).

Zadaj mi pytania, jeśli czegoś brakuje (np. skąd wziąć koszt do marży, czy chcę bibliotekę wykresów, jaki priorytet). Potem wypluj finalny prompt dla Claude Code.
