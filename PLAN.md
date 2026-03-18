# Plan: Centralny System Zmian Zamówień (ZmianyZamowienDzialowe)

## Cel
Gdy handlowiec zmieni zamówienie w "Zamówienia Klientów", każdy dział (Produkcja, Transport, Rozbiór, Magazyn, Fakturowanie) musi zobaczyć i zaakceptować zmianę — z jednej centralnej tabeli.

## Stan obecny
| Dział | Mechanizm | Lokalizacja |
|-------|-----------|-------------|
| Produkcja | flaga `CzyZmodyfikowaneDlaProdukcji` + `ZamowieniaMiesoSnapshot` | LibraNet |
| Magazyn | flaga `CzyZmodyfikowaneDlaMagazynu` + snapshot | LibraNet |
| Fakturowanie | flaga `CzyZmodyfikowaneDlaFaktur` | LibraNet |
| Transport | osobna tabela `TransportZmiany` + `TransportOrderSnapshot` | TransportPL |
| Rozbiór | **BRAK** — nie czyta zamówień | - |

## Architektura nowego systemu

### Faza 1: SQL + Serwis centralny

**1a. Tabela `ZmianyZamowienDzialowe`** (LibraNet DB)
```sql
CREATE TABLE dbo.ZmianyZamowienDzialowe (
    Id                 INT IDENTITY(1,1) PRIMARY KEY,
    ZamowienieId       INT NOT NULL,
    KlientNazwa        NVARCHAR(200) NULL,
    Dzial              NVARCHAR(30) NOT NULL,  -- 'Produkcja','Transport','Magazyn','Rozbior','Fakturowanie'
    TypZmiany          NVARCHAR(50) NOT NULL,  -- 'Edycja','NoweZamowienie','Anulowanie','ZmianaIlosci',etc.
    PoleZmienione      NVARCHAR(100) NULL,     -- np. 'Ilosc', 'Folia', 'Uwagi'
    StareWartosc       NVARCHAR(MAX) NULL,
    NowaWartosc        NVARCHAR(MAX) NULL,
    OpisZmiany         NVARCHAR(500) NULL,     -- czytelny opis: "Filet 500→600 kg"
    StatusZmiany       NVARCHAR(20) NOT NULL DEFAULT 'Oczekuje',  -- 'Oczekuje','Zaakceptowano','Odrzucono'
    ZgloszonePrzez     NVARCHAR(100) NOT NULL,
    DataZgloszenia     DATETIME NOT NULL DEFAULT GETDATE(),
    ZaakceptowanePrzez NVARCHAR(100) NULL,
    DataAkceptacji     DATETIME NULL,
    Komentarz          NVARCHAR(500) NULL
);
CREATE INDEX IX_ZZD_Dzial_Status ON dbo.ZmianyZamowienDzialowe(Dzial, StatusZmiany);
CREATE INDEX IX_ZZD_Zamowienie ON dbo.ZmianyZamowienDzialowe(ZamowienieId);
```

**1b. Serwis `Services/ZmianyZamowienService.cs`** (static, wzorowany na `TransportZmianyService`)
- `EnsureTableAsync()` — auto-tworzy tabelę
- `ZarejestrujZmianeAsync(zamId, klientNazwa, typZmiany, pole, stare, nowe, opis, user)` — wstawia 5 wierszy (1 na dział)
- `GetPendingAsync(dzial)` — lista oczekujących zmian dla działu
- `GetPendingCountAsync(dzial)` / `GetPendingCount(dzial)` — synchroniczny count do badge
- `AkceptujAsync(id, user, komentarz?)` — akceptacja jednej zmiany
- `AkceptujWszystkieAsync(dzial, zamowienieId?, user)` — akceptacja zbiorcza
- `OdrzucAsync(id, user, komentarz?)` — odrzucenie

### Faza 2: Podpięcie pod edycję zamówień (źródło zmian)

**2a. `Zamowienia/WidokZamowienia.cs`** (~linia 1560-1640)
- Po UPDATE/INSERT zamówienia → wywołaj `ZmianyZamowienService.ZarejestrujZmianeAsync()` z typem zmiany i listą zmienionych pól
- Zachowaj istniejące flagi (`CzyZmodyfikowaneDla*`) dla backward compat — stopniowe usunięcie później

**2b. `WPF/MainWindow.xaml.cs`** (metoda `SaveOrderItemChangeAsync` ~linia 7940)
- Po zapisie pozycji → wywołaj `ZmianyZamowienService.ZarejestrujZmianeAsync()` z detalami (produkt, pole, stare→nowe)
- Zachowaj istniejący zapis do `HistoriaZmianZamowien` — centralny serwis to DODATEK, nie zamiennik audytu

### Faza 3: Konsumenci — odczyt i akceptacja zmian

**3a. `ProdukcjaPanel.xaml.cs`** — Dział "Produkcja"
- W `LoadOrdersAsync()` → doczytaj pending count z `ZmianyZamowienService.GetPendingCount("Produkcja")`
- Wyświetl badge/ikonę na zamówieniach z oczekującymi zmianami
- Przycisk "Akceptuj zmianę" → `ZmianyZamowienService.AkceptujWszystkieAsync("Produkcja", zamId, user)`
- Zachowaj istniejący mechanizm snapshot-diff (nie kasujemy go) — centralny system dodaje notification layer

**3b. `Transport/TransportZmianyService.cs`** — Dział "Transport"
- Transport już ma działający system. Podejście: **bridge** — przy `DetectNewOrdersAsync()` oprócz wpisu do `TransportZmiany` → wpisz też do centralnej tabeli z `Dzial='Transport'`
- Alternatywnie: sam centralny serwis wykrywa zmiany dla transportu

**3c. Rozbiór (nowe!)** — Dział "Rozbior"
- `PokazKrojenieMrozenie.cs` jest czysto kalkulacyjny — NIE modyfikujemy go
- Zamiast tego: dodajemy do `PokazKrojenieMrozenie` prosty panel "Oczekujące zmiany zamówień" (banner/lista)
- Albo: nowa zakładka/sekcja z pending zmianami + przycisk akceptuj
- Minimum: badge na kafelku w Menu.cs (tak jak Transport ma badge)

**3d. `MagazynPanel.xaml.cs`** — Dział "Magazyn"
- Analogicznie do Produkcji — doczytaj pending count, wyświetl badge, przycisk akceptuj

**3e. `PanelFakturWindow.xaml.cs`** — Dział "Fakturowanie"
- Analogicznie — pending count, badge, akceptuj

### Faza 4: Badge'e na kafelkach menu

**`Menu.cs`** — rozszerzenie systemu badge'y
- Nowy timer `_zmianyBadgeTimer` (co 60s) odpytuje `ZmianyZamowienService.GetPendingCount()` per dział
- Badge na kafelkach: Panel Produkcji, Kalkulacja Rozbioru, Planowanie Transportu, Panel Magazyniera, Panel Faktur

## Kolejność implementacji
1. SQL tabela + `ZmianyZamowienService.cs` (Faza 1)
2. Hook w `WidokZamowienia.cs` + `MainWindow.xaml.cs` (Faza 2)
3. Badge'e w `Menu.cs` (Faza 4 — szybki wizualny efekt)
4. Akceptacja w ProdukcjaPanel + MagazynPanel (Faza 3a, 3d)
5. Panel zmian w PokazKrojenieMrozenie (Faza 3c)
6. Bridge z Transport (Faza 3b)
7. Akceptacja w PanelFaktur (Faza 3e)

## Pliki do utworzenia
- `SQL/CreateZmianyZamowienDzialowe.sql` — skrypt SQL
- `Services/ZmianyZamowienService.cs` — centralny serwis

## Pliki do zmodyfikowania
- `Zamowienia/WidokZamowienia.cs` — hook po edycji/nowym zamówieniu
- `WPF/MainWindow.xaml.cs` — hook po inline edycji pozycji
- `Menu.cs` — badge timer + badge labels na kafelkach
- `ProdukcjaPanel.xaml.cs` — odczyt + akceptacja z centralnej tabeli
- `PokazKrojenieMrozenie.cs` — dodanie panelu oczekujących zmian
- `Magazyn/Panel/MagazynPanel.xaml.cs` — odczyt + akceptacja
- `WPF/PanelFakturWindow.xaml.cs` — odczyt + akceptacja
- `Transport/TransportZmianyService.cs` — bridge do centralnej tabeli (opcjonalnie)
