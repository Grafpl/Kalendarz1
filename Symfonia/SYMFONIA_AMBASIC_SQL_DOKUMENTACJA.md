# SYMFONIA HANDEL + AMBASIC + SQL — KOMPLETNA DOKUMENTACJA TECHNICZNA

> **Cel dokumentu:** Pełna baza wiedzy o integracji LibraNet ↔ Symfonia Handel
> przez skrypty AMBasic i bezpośrednie operacje SQL. Dokument przeznaczony do
> użytku przez Claude Code (i ludzi) jako referencja przy modyfikacji,
> rozbudowie i debugowaniu systemu.
>
> **Kontekst biznesowy:** Zakład przetwórstwa mięsa (drób). Integruje system
> zarządzania ubojami i zamówieniami (LibraNet, MS SQL Server) z systemem
> ERP/księgowym (Symfonia Handel) przez skrypty AMBasic, eliminując ręczne
> wprowadzanie dokumentów PZ, FVR, FVZ, RWU, WZ, FVS.
>
> **Zasada bezpieczeństwa:** Wszystkie dokumenty tworzone są w trybie
> **buforowym** (`bufor = 1`) — operator weryfikuje je w Symfonii przed
> zatwierdzeniem.

---

## SPIS TREŚCI

1. [Architektura systemu](#1-architektura-systemu)
2. [Środowisko i parametry połączeń](#2-środowisko-i-parametry-połączeń)
3. [AMBasic — język](#3-ambasic--język)
4. [AMBasic — formularze GUI](#4-ambasic--formularze-gui)
5. [AMBasic — połączenia z bazą (ADO)](#5-ambasic--połączenia-z-bazą-ado)
6. [AMBasic — IORec (tworzenie dokumentów)](#6-ambasic--iorec-tworzenie-dokumentów)
7. [AMBasic — funkcje wbudowane i NIEISTNIEJĄCE](#7-ambasic--funkcje-wbudowane-i-nieistniejące)
8. [Pułapki i błędy AMBasic — kompletna lista](#8-pułapki-i-błędy-ambasic--kompletna-lista)
9. [Struktura bazy LibraNet](#9-struktura-bazy-libranet)
10. [Struktura bazy HANDEL (Symfonia)](#10-struktura-bazy-handel-symfonia)
11. [Mapowanie LibraNet ↔ Symfonia](#11-mapowanie-libranet--symfonia)
12. [Workflow eksportu zakupów (PZ + FVR/FVZ + RWU)](#12-workflow-eksportu-zakupów-pz--fvrfvz--rwu)
13. [Workflow eksportu sprzedaży (WZ + FVS)](#13-workflow-eksportu-sprzedaży-wz--fvs)
14. [Sprawdzone wzorce kodu](#14-sprawdzone-wzorce-kodu)
15. [Zatwierdzanie dokumentów i operacje SQL](#15-zatwierdzanie-dokumentów-i-operacje-sql)
16. [Powiązania dokumentów (PZ ↔ FV)](#16-powiązania-dokumentów-pz--fv)
17. [Konwencje i zasady pisania nowych skryptów](#17-konwencje-i-zasady-pisania-nowych-skryptów)
18. [Diagnostyka i debugging](#18-diagnostyka-i-debugging)
19. [Słowniki i kody](#19-słowniki-i-kody)

---

# 1. Architektura systemu

## 1.1 Komponenty

```
┌────────────────────────────────────────────────────────────────────┐
│                  ŚRODOWISKO PRODUKCYJNE                            │
│                                                                    │
│  ┌─────────────────────┐         ┌─────────────────────┐           │
│  │   LibraNet (SQL)    │         │   HANDEL (SQL)      │           │
│  │   192.168.0.109     │         │   192.168.0.109     │           │
│  │                     │         │                     │           │
│  │  - dbo.FarmerCalc   │         │  - HM.DK            │           │
│  │  - dbo.Dostawcy     │         │  - HM.MG            │           │
│  │  - dbo.Zamowienia*  │         │  - HM.TW            │           │
│  │  - dbo.Odbiorcy     │         │  - HM.DP            │           │
│  └──────────┬──────────┘         │  - SSCommon.        │           │
│             │                    │      STContractors  │           │
│             │                    │  - dr.DocumentsLinks│           │
│             │                    └──────────▲──────────┘           │
│             │                               │                      │
│             │   ┌───────────────────────────┘                      │
│             │   │                                                  │
│  ┌──────────▼───▼─────────────────────────────┐                    │
│  │       SYMFONIA HANDEL (klient)             │                    │
│  │                                            │                    │
│  │  Menu: Procedury → Raporty z menu kartotek │                    │
│  │                                            │                    │
│  │  Skrypty AMBasic (.sc):                    │                    │
│  │  - ExportPZLibraNet_v37+ (zakupy)          │                    │
│  │  - WZ.sc (sprzedaż)                        │                    │
│  │                                            │                    │
│  │  IORec → importMg() / ImportZK() /         │                    │
│  │          ImportSP()                        │                    │
│  └────────────────────────────────────────────┘                    │
│                                                                    │
│  ┌────────────────────────────────────────────┐                    │
│  │       Kalendarz1 ZPSP (C# WPF)             │                    │
│  │                                            │                    │
│  │  - WidokSpecyfikacje (zakupy)              │                    │
│  │  - Panel Faktur (sprzedaż)                 │                    │
│  │  - MapowanieDostawcowWindow                │                    │
│  │                                            │                    │
│  │  Łączy się BEZPOŚREDNIO z LibraNet i       │                    │
│  │  Symfonia (HANDEL) przez SqlClient         │                    │
│  └────────────────────────────────────────────┘                    │
└────────────────────────────────────────────────────────────────────┘
```

## 1.2 Przepływ danych — schemat ogólny

**Zakupy (od hodowców):**

```
LibraNet.dbo.FarmerCalc  ──┐
   (dostawy z dnia)         │
                            ▼
                 Skrypt AMBasic ExportPZLibraNet
                            │
                            ├─► Symfonia: PZ   (importMg)
                            ├─► Symfonia: FVR/FVZ (ImportZK)
                            └─► Symfonia: RWU (importMg)
                            │
                            ▼
              UPDATE FarmerCalc SET
                Symfonia=1,
                SymfoniaIdFV=...,
                SymfoniaNrFV=...
```

**Sprzedaż (do odbiorców):**

```
LibraNet.dbo.ZamowieniaMieso  ──┐
   (status: Zrealizowane/Wydano) │
   (CzyZafakturowane = 0)         │
                                  ▼
                  Skrypt AMBasic WZ.sc
                                  │
                                  ├─► Symfonia: WZ  (importMg)  [opcjonalnie]
                                  └─► Symfonia: FVS (ImportSP)
                                  │
                                  ▼
              UPDATE ZamowieniaMieso SET
                CzyZafakturowane=1,
                NumerFaktury=...
```

## 1.3 Zasady projektowe

1. **Tryb buforowy obowiązkowy** — `bufor = 1` przy tworzeniu. Operator
   weryfikuje dokumenty przed zatwierdzeniem. Nigdy nie tworzymy dokumentów
   od razu zatwierdzonych.
2. **Mapowanie po IdSymf, nie po NIP** — NIP może być pusty, mieć myślniki
   itp. Pole `IdSymf` w tabelach LibraNet jest źródłem prawdy.
3. **Idempotentność** — flagi `Symfonia`, `CzyZafakturowane` zapobiegają
   podwójnemu eksportowi. Skrypt zawsze pyta lub filtruje rekordy
   nieprzetworzone.
4. **Brak operacji destrukcyjnych** — skrypt nie usuwa danych w LibraNet
   ani Symfonii, jedynie tworzy dokumenty i aktualizuje flagi.
5. **Logowanie kluczowych zdarzeń** — ID dokumentu Symfonii, numer faktury,
   data, użytkownik (jeśli możliwe).

---

# 2. Środowisko i parametry połączeń

## 2.1 Serwer SQL

| Parametr   | Wartość             |
|------------|---------------------|
| Host       | `192.168.0.109`     |
| Silnik     | Microsoft SQL Server |
| Bazy       | `LibraNet`, `HANDEL`|
| Login      | `pronova`           |
| Hasło      | `pronova`           |

> **UWAGA:** Hasło w plikach .bas jest zapisane w jawnej formie. Jest to
> akceptowalne dla skryptów uruchamianych w sieci wewnętrznej, ale nigdy
> nie publikuj tych plików publicznie ani nie commituj ich do publicznego
> repozytorium bez podmiany.

## 2.2 Connection stringi

### Dla AMBasic (przez ADODB)

**Wariant SQLOLEDB (zalecany — szybszy, stabilniejszy):**

```basic
String gConnStr
gConnStr = "Provider=SQLOLEDB;Data Source=192.168.0.109;Initial Catalog=LibraNet;User ID=pronova;Password=pronova"
```

**Wariant MSDASQL (działa, ale starszy — zostaw w razie problemów):**

```basic
String gConnStr
gConnStr = using "Provider=MSDASQL.1;Extended Properties=DRIVER=SQL Server;SERVER=%s;UID=%s;PWD=%s;DATABASE=%s", mySrv, myUsr, myPwd, myDb
```

**Wariant ODBC bezpośredni (używany w niektórych starszych wersjach):**

```basic
String connectionString
connectionString = using "driver=SQL Server;server=%s;database=%s;Uid=%s;Pwd=%s;", gServerLibraNet, gDatabaseLibraNet, gUserLibraNet, gPassLibraNet
```

### Dla aplikacji C# (Kalendarz1 ZPSP)

```csharp
// LibraNet
private string connectionString =
    "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";

// Symfonia (HANDEL)
private string symfoniaConnectionString =
    "Server=192.168.0.109;Database=HANDEL;User Id=pronova;Password=pronova;TrustServerCertificate=True";
```

## 2.3 Wewnętrzne połączenie do Symfonii (z poziomu skryptu w Symfonii)

W skrypcie AMBasic uruchamianym **wewnątrz Symfonii** połączenie do bazy
HANDEL (Symfonii) **nie wymaga connection stringa** — Symfonia udostępnia
wbudowane połączenie:

```basic
Dispatch conSymf
conSymf = getAdoConnection()    ' zwraca otwarte połączenie do bazy HANDEL
```

> **WAŻNE:** Połączenia z `getAdoConnection()` **NIE WOLNO ZAMYKAĆ**
> przez `con.close()` — należy do systemu, jego zamknięcie powoduje
> błędy w dalszej części pracy Symfonii.

---

# 3. AMBasic — język

## 3.1 Nagłówek skryptu (pierwsza linia)

Każdy skrypt umieszczany w drzewie procedur Symfonii ma nagłówek
w pierwszej linii. Format:

```basic
//"NazwaPliku.sc","Opis w menu","\Ścieżka\W\Drzewie\Menu",flaga,wersja,typ
```

Przykłady:

```basic
//"ExportPZLibraNet.sc","Eksport PZ z LibraNet","\Procedury\Raporty z menu kartotek\Magazyn",0,1.0.0,SYSTEM
//"WZ.sc","WZ - Eksport faktur","\Procedury",0,1.0.0,SYSTEM
```

Pola:
- **NazwaPliku.sc** — nazwa pliku w katalogu skryptów Symfonii
- **Opis w menu** — etykieta widoczna w drzewie procedur
- **Ścieżka** — gałąź drzewa (`\Procedury`, `\Procedury\Raporty z menu kartotek\Magazyn`, itd.)
- **flaga** — zwykle `0`
- **wersja** — np. `1.0.0`
- **typ** — `SYSTEM` (dostępny dla wszystkich) lub inny

## 3.2 Komentarze

```basic
// Komentarz jednoliniowy (preferowany)
' Komentarz jednoliniowy (alternatywny — działa, ale rzadziej używany)
```

Brak komentarzy blokowych `/* */`.

## 3.3 Typy danych — KRYTYCZNE!

| Typ        | Użycie                         | Uwagi                                        |
|------------|--------------------------------|----------------------------------------------|
| `String`   | Tekst                          | Konkatenacja przez `+`                       |
| `int`      | Liczba całkowita 32-bit        | Format `%d`                                  |
| `long`     | Liczba całkowita 64-bit        | Format `%l`                                  |
| `float`    | Liczba zmiennoprzecinkowa      | Format `%.2f`                                |
| **`double`** | **❌ NIE ISTNIEJE!**         | Próba użycia → błąd kompilacji. Używaj `float`. |
| **`bool`** | **❌ NIE ISTNIEJE!**            | Używaj `int` (0/1).                          |
| `Date`     | Data                           | Metody `today()`, `addDays()`, `toStr()`     |
| `Dispatch` | Obiekt COM/ActiveX             | Dla ADO (Connection, Recordset, Command)     |
| `IORec`    | Obiekt do importu dokumentów   | **Musi być deklarowany W FUNKCJI, nie globalnie!** |

### Deklaracja zmiennych

```basic
String sNazwa
int nLiczba
long nDuzaLiczba
float fUlamek
Date dData
Dispatch oCon
IORec dokPz                  // tylko wewnątrz funkcji!
```

### Inicjalizacja przy deklaracji (działa dla typów prostych)

```basic
String mySrv = "192.168.0.109"
int dniTerminu = 35
float fStawkaVAT = 23.0
```

### Przypisanie wartości

```basic
sNazwa = "Test"
nLiczba = 42
fUlamek = 3.14
```

## 3.4 Operatory

### Porównania

| Operator | Znaczenie         |
|----------|-------------------|
| `==`     | Równe             |
| `!=`     | Różne             |
| `<`      | Mniejsze          |
| `>`      | Większe           |
| `<=`     | Mniejsze lub równe|
| `>=`     | Większe lub równe |

> **UWAGA:** Pojedyncze `=` to **przypisanie**, nie porównanie!

### Logiczne

| Operator | Znaczenie  |
|----------|------------|
| `AND`    | I logiczne |
| `OR`     | Lub        |
| `!`      | Negacja    |

```basic
While !rs.EOF AND nLicznik < 100
    // ...
Wend
```

### Arytmetyczne

`+` (dodawanie/konkatenacja), `-`, `*`, `/`

### Konkatenacja stringów

Dwa sposoby — oba działają:

```basic
// 1. Operator +
sWynik = "Hello " + sImie + "!"

// 2. Funkcja using (formatowanie w stylu printf)
sWynik = using "Hello %s, masz %d lat", sImie, nWiek
```

Specyfikatory formatu w `using`:
- `%s` — string
- `%d` — int
- `%l` — long
- `%.2f` — float z 2 miejscami po przecinku
- `%05d` — int z paddingiem zerami do 5 znaków

## 3.5 Struktury kontrolne

### IF / ELSE

```basic
if warunek then
    // kod
endif

if warunek then
    // kod 1
else
    // kod 2
endif

// Zagnieżdżone — używaj kolejnych if/else, NIE elseif
if a == 1 then
    // ...
else
    if a == 2 then
        // ...
    else
        // ...
    endif
endif
```

> **PUŁAPKA:** `elseif` lub `else if` w jednej linii — niepewne wsparcie.
> Bezpieczniej zagnieżdżać.

### WHILE

```basic
While !rs.EOF
    sWartosc = rs.fields("kolumna").value
    rs.moveNext()
Wend
```

### FOR (jeśli wspierane w Twojej wersji)

```basic
for i = 1 to 10
    // kod
next
```

> Niektóre wersje AMBasic mają problemy z `for` — używaj `while` jako bezpieczniejszą opcję.

### Brak `continue` / `break`

```basic
// ❌ NIE DZIAŁA:
While !rs.EOF
    if pomin then
        continue          // BŁĄD
    endif
    rs.moveNext()
Wend

// ✅ POPRAWNIE — przez warunek:
While !rs.EOF
    if !pomin then
        // przetwarzanie
    endif
    rs.moveNext()
Wend
```

## 3.6 Funkcje (sub)

Każda funkcja musi mieć **typ zwracany**. Brak typu → traktowane jak procedura
i kompilator może protestować w określonych kontekstach.

### Składnia

```basic
TYPRETURNY sub NazwaFunkcji(TypP1 pParam1, TypP2 pParam2)
    // deklaracje lokalne
    String lokalna
    int wynik

    // kod

    // zwrot wartości — przypisanie do nazwy funkcji
    NazwaFunkcji = wynik
endsub
```

### Przykłady

```basic
int sub Dodaj(int pA, int pB)
    int suma
    suma = pA + pB
    Dodaj = suma
endsub

String sub PobierzNazwe(int pId)
    String nazwa
    nazwa = "test"
    PobierzNazwe = nazwa
endsub

// "Procedura" — w praktyce sub zwracający int (np. 1 = OK)
int sub WykonajCos()
    // kod
    WykonajCos = 1
endsub
```

### Wywołanie

```basic
int x
x = Dodaj(3, 5)               // OK

// Wywołanie bez użycia wyniku — ZAWSZE przypisz do zmiennej
int tmp
tmp = WykonajCos()            // OK

// ❌ Niektóre wersje AMBasic mają problem z bezpośrednim wywołaniem
//    bez przypisania, jeśli funkcja jest nie-int. Bezpiecznie: zawsze przypisuj.
```

## 3.7 Zmienne globalne

Deklarowane na samym początku skryptu, **poza** wszelkimi funkcjami.
Dostępne wewnątrz funkcji bez przekazywania jako parametr.

```basic
String gConnStr               // konwencja: prefiks g dla globalnych
int gLicznik
float gSumaKg

int sub Dodaj(int pA)
    gLicznik = gLicznik + 1   // dostęp z funkcji
    Dodaj = pA + 1
endsub
```

> **WYJĄTEK:** `IORec` — choć technicznie można deklarować globalnie, jest
> to źródło bardzo dziwnych błędów (stan obiektu zostaje między wywołaniami).
> **ZAWSZE deklaruj `IORec` wewnątrz funkcji.**

## 3.8 Daty

```basic
Date d
d.today()                    // ustaw na dzisiejszą datę
d.addDays(30)                // dodaj 30 dni
String s = d.toStr()         // konwersja na string "YYYY-MM-DD"
```

W setField wartości daty zawsze przekazujemy jako String w formacie ISO:

```basic
dokPz.setField("dataWystawienia", "2026-01-20")
dokPz.setField("dataWystawienia", d.toStr())
```

---

# 4. AMBasic — formularze GUI

## 4.1 Struktura formularza

```basic
int wynik
form "Tytuł okna", szerokość, wysokość
    ground R, G, B                            // tło RGB
    Text "etykieta", x, y, w, h               // tekst statyczny
    Edit "etykieta", zmienna, x, y, w, h      // pole tekstowe
    Datedit "etykieta", zmienna, x, y, w, h   // pole daty
    Checkbox "etykieta", zmiennaInt, x, y, w, h   // checkbox
    button "&Etykieta", x, y, w, h, wartoscZwrotna
wynik = ExecForm
```

`ExecForm` zwraca wartość przycisku, który został kliknięty.

## 4.2 Tytuł formularza

### ❌ NIE DZIAŁA — dynamiczny tytuł w `using`

```basic
form using "Eksport %s", sData, 400, 100        // BŁĄD!
```

### ✅ DZIAŁA — przypisz do zmiennej, użyj zmiennej

```basic
String sTytul
sTytul = using "Eksport %s", sData
form sTytul, 400, 100
    // ...
```

## 4.3 Kontrolka `ground` (kolor tła)

```basic
ground R, G, B               // każda składowa 0-255
```

Wartości używane w skryptach produkcyjnych:
- `ground 80, 200, 200` — bladocyjan (główny formularz eksportu)
- `ground 100, 180, 220` — błękitny (formularze wyboru)
- `ground 200, 200, 200` — szary (komunikaty)
- `ground 60, 120, 180` — granatowy (nagłówki dużych okien)
- `ground 240, 240, 240` — jasnoszary (lista zamówień)

## 4.4 `Text` — tekst statyczny

```basic
Text "treść", x, y, szerokość, wysokość
```

### Dynamiczny tekst — najpierw przypisz

```basic
String sLinia
sLinia = using "Liczba: %d", n
Text sLinia, 20, 50, 400, 22       // OK

// Bezpośrednie zmiennej też działa:
Text sLinia, 20, 50, 400, 22       // OK
```

### Pusta linia (separator)

```basic
Text " ", 20, 100, 400, 10         // pusta linia
```

## 4.5 `Edit` — pole tekstowe

```basic
String sWartosc
Edit "Etykieta:", sWartosc, x, y, szerokość, wysokość
```

Po `ExecForm` w zmiennej `sWartosc` jest wpisana przez użytkownika wartość.

## 4.6 `Datedit` — pole daty

```basic
String datap
Datedit "Data:", datap, x, y, szerokość, wysokość
```

Wynik w zmiennej `datap` w formacie `YYYY-MM-DD`.

## 4.7 `Checkbox` — pole zaznaczenia

```basic
int gSel1
gSel1 = 1                                       // domyślnie zaznaczony
Checkbox "Etykieta:", gSel1, x, y, szerokość, wysokość
```

Po `ExecForm`: `gSel1 == 1` jeśli zaznaczone, `0` jeśli nie.

> **PUŁAPKA — checkboxy zwracają wartość TYLKO jeśli formularz został zamknięty
> przyciskiem `>= 2`.** Anulowanie nie aktualizuje wartości checkboxów.

## 4.8 `button` — przycisk

```basic
button "&Etykieta", x, y, szerokość, wysokość, wartoscZwrotna
```

`&` przed literą — skrót klawiaturowy (Alt+litera).

### ❌ KRYTYCZNA PUŁAPKA: wartość `1` jest zarezerwowana!

W AMBasic Symfonii **wartość zwrotna `1` zachowuje się jak Anuluj/zamknięcie**
formularza — przycisk z wartością 1 nie działa zgodnie z oczekiwaniami.

```basic
// ❌ ŹLE — drugi przycisk nie zadziała
button "TAK",  20, 50, 100, 30, 1     // BŁĄD!
button "NIE", 130, 50, 100, 30, 2

// ✅ DOBRZE — używaj wartości >= 2 dla "OK", -1 dla "Anuluj"
button "TAK",  20, 50, 100, 30, 2
button "NIE", 130, 50, 100, 30, 3
button "&Anuluj", 240, 50, 100, 30, -1
```

### Konwencje wartości

| Wartość | Użycie typowe                        |
|---------|--------------------------------------|
| `2`     | Główny przycisk akcji (OK / Eksportuj) |
| `3`     | Drugi wybór (alternatywa)            |
| `4`, `5`, ... | Kolejne opcje                  |
| `-1`    | Anuluj                               |
| **`1`** | **❌ NIE UŻYWAĆ!**                   |

## 4.9 Wynik `ExecForm`

```basic
int wynik
wynik = ExecForm

if wynik < 1 then
    Error ""                  // przerwanie skryptu (pusty komunikat)
endif

if wynik == 2 then
    // główna akcja
endif
```

`Error ""` z pustym stringiem przerywa skrypt bez wyświetlania komunikatu.

## 4.10 Pełny przykład formularza wejściowego

```basic
String datap
int wynik_form
int bIgnorujFlage

form "EKSPORT PZ Z LIBRANET", 450, 170
    ground 80, 200, 200
    Datedit "Data uboju:", datap, 150, 20, 150, 22
    Text "Dla każdego dostawcy: PZ + Faktura VAT RR/FVZ", 20, 50, 400, 20
    Text "Na końcu: RWU z sumą kg (PZ-FV powiązane)",   20, 70, 400, 20
    button "&Eksportuj NOWE",          20, 110, 130, 35, 2
    button "Eksportuj &WSZYSTKIE",    160, 110, 150, 35, 3
    button "&Anuluj",                  320, 110, 100, 35, -1
wynik_form = ExecForm

if wynik_form < 1 then
    Error ""
endif

if wynik_form == 2 then
    bIgnorujFlage = 0
else
    bIgnorujFlage = 1
endif
```

## 4.11 Lista pozycji z checkboxami (wzorzec)

Dla list o ograniczonej liczbie elementów (np. max 15) używamy stałych
zmiennych globalnych — nie ma dynamicznych kontrolek.

```basic
// Globalne — etykiety i stany checkboxów
String gZam1, gZam2, gZam3 /* ... */, gZam15
int gSel1, gSel2, gSel3 /* ... */, gSel15

// Wypełnianie z bazy (pętla while ze zliczaniem)
nr = 0
while !rs.EOF and nr < 15
    nr = nr + 1
    String linia
    linia = using "%2d. %s | %s kg", nr, klient, suma
    if nr == 1 then
        gZam1 = linia
        gSel1 = 1
    endif
    if nr == 2 then
        gZam2 = linia
        gSel2 = 1
    endif
    // ...
    rs.moveNext()
wend

// Formularz
form "Wybierz zamówienia", 800, 550
    ground 240, 240, 240
    Text "Zaznacz zamówienia do eksportu:", 20, 20, 760, 25
    Checkbox gZam1, gSel1, 20,  60, 760, 22
    Checkbox gZam2, gSel2, 20,  85, 760, 22
    Checkbox gZam3, gSel3, 20, 110, 760, 22
    // ...
    Checkbox gZam15, gSel15, 20, 410, 760, 22
    button "&EKSPORTUJ ZAZNACZONE", 250, 470, 200, 50, 2
    button "&Anuluj", 480, 470, 150, 50, -1
wynik = ExecForm
```

## 4.12 Rozmiary referencyjne formularzy używanych w produkcji

| Skrypt                         | Rozmiar  | Cel                      |
|--------------------------------|----------|--------------------------|
| ExportPZLibraNet — wejście     | 450×170  | wybór daty + opcji       |
| ExportPZLibraNet — wybór FVR/FVZ | 350×100 | dialog typu faktury      |
| WZ.sc — wejście                | 800×550  | duże okno z instrukcją   |
| WZ.sc — lista zamówień         | 950×850  | checkboxy 15 zamówień    |

---

# 5. AMBasic — połączenia z bazą (ADO)

## 5.1 Dwa rodzaje połączeń

| Typ                | Cel                | Otwieranie                       | Zamykanie  |
|--------------------|--------------------|----------------------------------|------------|
| Wbudowane Symfonia | baza HANDEL        | `getAdoConnection()`             | **NIE!**   |
| Zewnętrzne ODBC/OLEDB | baza LibraNet i inne | `createObject("ADODB.Connection")` + `.open()` | `con.close()` |

## 5.2 Połączenie do Symfonii (z poziomu Symfonii)

```basic
Dispatch conSymf
conSymf = getAdoConnection()

// Recordset
Dispatch rsSymf
rsSymf = "ADODB.Recordset"
rsSymf.cursorType = 1
rsSymf.lockType = 1

rsSymf.open("SELECT id, kod FROM HM.TW WHERE kod = 'KURCZAK'", conSymf)

if !rsSymf.EOF then
    String kod = rsSymf.fields("kod").value
endif

rsSymf.close()
// NIE zamykamy conSymf — należy do systemu!
```

## 5.3 Połączenie do LibraNet (zewnętrzne)

```basic
Dispatch con
con = createObject("ADODB.Connection")
con.connectionString = "Provider=SQLOLEDB;Data Source=192.168.0.109;Initial Catalog=LibraNet;User ID=pronova;Password=pronova"
con.open()

Dispatch rs
rs = "ADODB.Recordset"
rs.cursorType = 1                   // adOpenKeyset (1)
rs.lockType = 1                     // adLockReadOnly (1)
rs.open("SELECT * FROM dbo.FarmerCalc WHERE CalcDate = '2026-01-20'", con)

While !rs.EOF
    String s = rs.fields("CarLp").value
    rs.moveNext()
Wend

rs.close()
con.close()
```

## 5.4 Tworzenie obiektu Recordset — DWIE SKŁADNIE

```basic
// Składnia 1 — przez przypisanie stringa (działa, prosta)
Dispatch rs
rs = "ADODB.Recordset"

// Składnia 2 — przez createObject (też działa)
Dispatch rs
rs = createObject("ADODB.Recordset")
```

Obydwie działają. W produkcyjnych skryptach częściej używamy formy 1.

## 5.5 Parametry kursora

```basic
rs.cursorType = 1     // adOpenKeyset — szybki, do iteracji
rs.lockType = 1       // adLockReadOnly — tylko odczyt
```

Inne wartości:
- `cursorType = 0` (adOpenForwardOnly) — najszybszy, ale tylko `moveNext`
- `cursorType = 3` (adOpenStatic) — snapshot, dobre dla raportów

> **UWAGA:** Niektóre wersje AMBasic mają problem z `cursorType = 3`
> w połączeniu z `getAdoConnection()` — używaj `1` jako bezpieczny default.

## 5.6 Pobieranie wartości z pola

```basic
String s = rs.fields("nazwa_kolumny").value
int n = rs.fields("id").value
float f = rs.fields("cena").value
```

> **Konwersje:** AMBasic robi konwersję automatycznie, ale dla bezpieczeństwa
> w SQL używaj `CAST(... AS VARCHAR(...))` jeśli chcesz uniknąć problemów
> z formatowaniem.

## 5.7 Obsługa NULL w SQL — KRYTYCZNE

Pole NULL wczytane do `String` w AMBasic powoduje **błąd**. Zawsze chroń się
funkcją `ISNULL()` w SQL:

```sql
-- ❌ Ryzykowne:
SELECT NIP FROM dbo.Dostawcy

-- ✅ Bezpieczne:
SELECT ISNULL(NIP, '') AS NIP FROM dbo.Dostawcy
SELECT ISNULL(CAST(IdSymf AS VARCHAR(20)), '0') AS IdSymf FROM dbo.Dostawcy
```

## 5.8 Wykonanie INSERT/UPDATE/DELETE (bez zwracania danych)

```basic
sub UpdateFlagi(String pData, long pIdFV, String pNrFV)
    Dispatch conUpd
    String sql

    conUpd = createObject("ADODB.Connection")
    conUpd.connectionString = gConnStr
    conUpd.open()

    sql = "UPDATE dbo.FarmerCalc SET Symfonia = 1, "
    sql = sql + "SymfoniaIdFV = " + using "%d", pIdFV + ", "
    sql = sql + "SymfoniaNrFV = '" + pNrFV + "' "
    sql = sql + "WHERE CalcDate = '" + pData + "'"

    conUpd.execute(sql)
    conUpd.close()
endsub
```

## 5.9 Eskejpowanie apostrofów w SQL

W AMBasic nie ma wbudowanej funkcji `Replace`. Dla bezpieczeństwa używaj
parametryzacji (Command + Parameters), lub dla prostych przypadków
sprawdź dane wcześniej:

```basic
// Jeśli wiadomo że nie ma apostrofów (np. ID, daty):
sql = "WHERE Id = " + sId

// Dla tekstu o nieznanej zawartości — bezpieczniej iść przez Command:
Dispatch cmd
cmd = createObject("ADODB.Command")
cmd.ActiveConnection = con
cmd.CommandText = "UPDATE Tab SET Nazwa = ? WHERE Id = ?"
// ... ustaw parametry
cmd.Execute()
```

W praktyce w naszych skryptach większość pól jest typu numerycznego lub
sprawdzonego, więc konkatenacja stringów jest akceptowalna.

---

# 6. AMBasic — IORec (tworzenie dokumentów)

## 6.1 Czym jest IORec

`IORec` to specjalny typ AMBasic do **importu dokumentów do Symfonii**.
Mapuje się na wewnętrzny obiekt Symfonii — pozwala wypełnić nagłówek,
dane kontrahenta, pozycje, a następnie zaimportować do bazy jako prawdziwy
dokument widoczny w GUI Symfonii.

## 6.2 Złota zasada: `IORec` deklarowany W FUNKCJI

```basic
// ❌ ŹLE — IORec globalnie powoduje dziwne błędy stanu między wywołaniami
IORec dokGlob

int sub TworzPZ()
    dokGlob.setField("typDk", "PZ")    // może nie działać
    // ...
endsub

// ✅ DOBRZE — IORec lokalnie w każdej funkcji
int sub TworzPZ()
    IORec dokPz                        // świeży obiekt za każdym razem
    dokPz.setField("typDk", "PZ")
    // ...
endsub
```

## 6.3 Trzy funkcje importu — według typu dokumentu

| Funkcja        | Typ dokumentu                | Tabela docelowa      |
|----------------|------------------------------|----------------------|
| `importMg()`   | PZ, WZ, PW, RW, RWU, MM      | `HM.MG`              |
| `ImportZK()`   | FVZ (zakup), FVR (rolnik)    | `HM.DK` (typ_dk = 202)|
| `ImportSP()`   | FVS (sprzedaż), PAR, KFS     | `HM.DK` (typ_dk = 0) |

> **BARDZO WAŻNE:** Mieszanie funkcji importu z typem dokumentu = błąd!
> Nigdy nie wywołuj `importMg()` na fakturze i odwrotnie.

### Składnia wywołania

```basic
// Magazynowy
long idMg
idMg = dokPz.importMg()

// Zakupowy (UWAGA wielkość liter!)
long idZk
idZk = ImportZK(dokFv)              // funkcja globalna z parametrem!

// Sprzedażowy
long idSp
idSp = ImportSP(dokFvs)             // funkcja globalna z parametrem!
```

> **Niuans:** `importMg()` to **metoda obiektu** (`dok.importMg()`), natomiast
> `ImportZK(dok)` i `ImportSP(dok)` to **funkcje globalne** przyjmujące
> obiekt jako parametr. Łatwo się pomylić!

## 6.4 Zwracana wartość

- `> 0` — sukces, zwraca **ID nowo utworzonego dokumentu**
- `0` lub fałsz — błąd

```basic
long idDok
idDok = dokPz.importMg()

if idDok > 0 then
    message using "Utworzono PZ id=%d", idDok
else
    message "Błąd tworzenia PZ"
endif
```

## 6.5 Pola nagłówka dokumentu

Wszystkie pola ustawiamy przez `setField`. Wartości **zawsze jako String**,
nawet liczby (Symfonia sama konwertuje).

| Pole                  | Opis                              | Przykład wartości       |
|-----------------------|-----------------------------------|-------------------------|
| `typDk`               | Typ dokumentu                     | `"PZ"`, `"FVR"`, `"FVS"`|
| `seria`               | Seria numeracji                   | `"sPZ"`, `"sFVR"`       |
| `dataWystawienia`     | Data wystawienia                  | `"2026-01-20"`          |
| `dataOperacji`        | Data operacji                     | `"2026-01-20"`          |
| `termin`              | Termin płatności                  | `"2026-02-24"`          |
| `dataDokumentuObcego` | Data dokumentu obcego (z dnia)    | `"2026-01-20"`          |
| `dataZakupu`          | Data zakupu                       | `"2026-01-20"`          |
| `dzial`               | Dział / magazyn                   | `"M. PROD"`             |
| `opis`                | Opis dokumentu                    | `"Import LibraNet"`     |
| `bufor`               | Czy w buforze (1 = tak)           | `"1"` (zalecane!)       |
| `kod_obcy`            | Numer obcy (z LibraNet)           | `"LN/2026-01-20/773"`   |

### Przykład kompletny

```basic
IORec dokPz
Date termin

termin.today()
termin.addDays(35)

dokPz.setField("typDk",                "PZ")
dokPz.setField("seria",                "sPZ")
dokPz.setField("dataWystawienia",      "2026-01-20")
dokPz.setField("dataOperacji",         "2026-01-20")
dokPz.setField("dataDokumentuObcego",  "2026-01-20")
dokPz.setField("dataZakupu",           "2026-01-20")
dokPz.setField("termin",               termin.toStr())
dokPz.setField("dzial",                "M. PROD")
dokPz.setField("opis",                 "LibraNet Wodzyński Stanisław")
dokPz.setField("bufor",                "1")
```

## 6.6 Sekcja `daneKh` — kontrahent

Trzy sposoby identyfikacji kontrahenta — wybór jeden:

```basic
// Opcja A — przez ID (najpewniejsze, wymaga znajomości IdSymf)
dokPz.beginSection("daneKh")
    dokPz.setField("khId", "5209")              // ID z SSCommon.STContractors
dokPz.endSection()

// Opcja B — przez Kod (skrót kontrahenta)
dokPz.beginSection("daneKh")
    dokPz.setField("khKod", "WODZYNSKI")
dokPz.endSection()

// Opcja C — przez NIP (nie zalecane — NIP może być pusty)
dokPz.beginSection("daneKh")
    dokPz.setField("khNip", "1234567890")
dokPz.endSection()
```

> **Zalecenie:** Używaj `khId` (lub `khKod`). NIP jest zawodny — w bazie
> LibraNet wielu hodowców ma puste pole NIP, choć są poprawnie zmapowani
> przez `IdSymf`.

## 6.7 Sekcja `Pozycja dokumentu` — pozycje

Każda pozycja w osobnej parze `beginSection` / `endSection`:

```basic
dokPz.beginSection("Pozycja dokumentu")
    dokPz.setField("kod",   "Kurczak żywy -8")     // kod towaru z HM.TW.kod
    dokPz.setField("ilosc", "1234.56")             // ilość
    dokPz.setField("cena",  "4.50")                // cena jednostkowa
    dokPz.setField("opis",  "Dostawa LibraNet")    // opcjonalnie
    dokPz.setField("jm",    "kg")                  // opcjonalnie (zwykle z kartoteki)
    dokPz.setField("vat",   "5")                   // opcjonalnie
dokPz.endSection()

// Druga pozycja:
dokPz.beginSection("Pozycja dokumentu")
    dokPz.setField("kod",   "Kurczak żywy -7")
    dokPz.setField("ilosc", "987.65")
    dokPz.setField("cena",  "4.20")
dokPz.endSection()
```

## 6.8 Identyfikacja towaru — przez `kod`

Kluczowe: pole `kod` musi **dokładnie** odpowiadać polu `kod` w tabeli
`HM.TW`. Polskie znaki, wielkość liter i spacje muszą się zgadzać!

### Przykład problemu z naszej historii

W kartotece Symfonii: `Kurczak żywy -7` (BEZ spacji przed cyfrą)

Skrypt wstawiał: `Kurczak żywy - 7` (Z spacją) → pozycja nie była dodawana
do dokumentu, faktura wychodziła pusta.

**Rozwiązanie:** zawsze sprawdź dokładny zapis w `HM.TW.kod` przed
zakodowaniem stałej w skrypcie:

```sql
SELECT id, kod, nazwa FROM HM.TW WHERE nazwa LIKE '%Kurczak%żywy%'
```

## 6.9 Mapowanie ID towaru → kod

W LibraNet `KodTowaru` w `dbo.ZamowieniaMiesoTowar` to często **ID liczbowe**
z `HM.TW.ID`, nie tekstowy kod. Należy zrobić lookup:

```basic
String sub PobierzKodTowaru(String pIdTw)
    Dispatch conS
    Dispatch rsS
    String sql
    String wynik

    conS = getAdoConnection()
    rsS = "ADODB.Recordset"
    rsS.cursorType = 1
    rsS.lockType = 1

    sql = "SELECT ISNULL(kod, '') AS kod FROM HM.TW WHERE id = " + pIdTw
    rsS.open(sql, conS)

    if rsS.EOF then
        wynik = ""
    else
        wynik = rsS.fields("kod").value
    endif
    rsS.close()

    PobierzKodTowaru = wynik
endsub
```

## 6.10 Pełny przykład — utworzenie PZ

```basic
int sub UtworzPZ(String pCustomerGID, String pIdSymf, String pDostawca, String pData)
    IORec dokPz
    Date termin
    long idMg
    Dispatch conPos, rsPos
    String sqlPoz, sWaga, sCena
    int nPoz

    termin.today()
    termin.addDays(35)

    dokPz.setField("typDk",            "PZ")
    dokPz.setField("seria",            "sPZ")
    dokPz.setField("dataWystawienia",  pData)
    dokPz.setField("dataOperacji",     pData)
    dokPz.setField("termin",           termin.toStr())
    dokPz.setField("dzial",            "M. PROD")
    dokPz.setField("opis",             using "LibraNet %s", pDostawca)
    dokPz.setField("bufor",            "1")

    dokPz.beginSection("daneKh")
        dokPz.setField("khId", pIdSymf)
    dokPz.endSection()

    // Pozycje z LibraNet
    conPos = createObject("ADODB.Connection")
    conPos.connectionString = gConnStr
    conPos.open()

    rsPos = "ADODB.Recordset"
    rsPos.cursorType = 1
    rsPos.lockType = 1

    sqlPoz = "SELECT "
    sqlPoz = sqlPoz + "  CAST(COALESCE(PayWgt, NettoFarmWeight, 0) AS VARCHAR(50)) AS Waga, "
    sqlPoz = sqlPoz + "  CAST(ISNULL(Price, 0) AS VARCHAR(50))                     AS Cena, "
    sqlPoz = sqlPoz + "  CarLp "
    sqlPoz = sqlPoz + "FROM dbo.FarmerCalc "
    sqlPoz = sqlPoz + "WHERE CalcDate = '" + pData + "' "
    sqlPoz = sqlPoz + "  AND LTRIM(RTRIM(CustomerGID)) = '" + pCustomerGID + "'"
    rsPos.open(sqlPoz, conPos)

    nPoz = 0
    While !rsPos.EOF
        sWaga = rsPos.fields("Waga").value
        sCena = rsPos.fields("Cena").value

        dokPz.beginSection("Pozycja dokumentu")
            dokPz.setField("kod",   "Kurczak żywy -8")
            dokPz.setField("ilosc", sWaga)
            dokPz.setField("cena",  sCena)
        dokPz.endSection()

        nPoz = nPoz + 1
        rsPos.moveNext()
    Wend

    rsPos.close()
    conPos.close()

    if nPoz > 0 then
        idMg = dokPz.importMg()
        if idMg > 0 then
            UtworzPZ = 1
        else
            UtworzPZ = 0
        endif
    else
        UtworzPZ = 0
    endif
endsub
```

## 6.11 Tworzenie FVR / FVZ — różnice

```basic
IORec dokFv

// FVR (rolnik ryczałtowy)
dokFv.setField("typDk", "FVR")
dokFv.setField("seria", "sFVR")
// (reszta tak samo jak PZ — daty, kontrahent, pozycje)
// Pozycja dla FVR często ma inny kod towaru:
//   PZ:  "Kurczak żywy -7"
//   FVR: "Kurczak żywy -7"  (ten sam — od rolnika)
// vs FVZ: "Kurczak żywy -8"  (od vatowca)

long idZk
idZk = ImportZK(dokFv)            // UWAGA: funkcja globalna, NIE metoda

// FVZ (faktura VAT zakupu od vatowca)
dokFv.setField("typDk", "FVZ")
dokFv.setField("seria", "sFVZ")
// kod towaru: "Kurczak żywy -8"
```

## 6.12 Tworzenie FVS (faktura sprzedaży)

```basic
IORec dokFvs
long idSp

dokFvs.setField("typDk",           "FVS")
dokFvs.setField("seria",           "sFVS")
dokFvs.setField("dataWystawienia", datap)
dokFvs.setField("dataOperacji",    datap)
dokFvs.setField("bufor",           "1")
dokFvs.setField("opis",            "LibraNet zam #" + sZamId)

dokFvs.beginSection("daneKh")
    dokFvs.setField("khId", sKlientId)        // KlientId z ZamowieniaMieso
dokFvs.endSection()

// Pętla po pozycjach z ZamowieniaMiesoTowar
While !rsPoz.EOF
    String sIdTw = rsPoz.fields("KodTowaru").value      // to ID liczbowe
    String sIlosc = rsPoz.fields("Ilosc").value
    String sCena = rsPoz.fields("Cena").value
    String sKodTw = PobierzKodTowaru(sIdTw)             // lookup do HM.TW.kod

    if sKodTw != "" then
        dokFvs.beginSection("Pozycja dokumentu")
            dokFvs.setField("kod",   sKodTw)
            dokFvs.setField("ilosc", sIlosc)
            dokFvs.setField("cena",  sCena)
        dokFvs.endSection()
    endif

    rsPoz.moveNext()
Wend

idSp = ImportSP(dokFvs)            // funkcja globalna
```

## 6.13 Tworzenie RWU (rozchód wewnętrzny — usługa)

RWU to dokument magazynowy (jak PZ), używamy `importMg()`:

```basic
IORec dokRwu
long idMg

dokRwu.setField("typDk",           "RWU")
dokRwu.setField("seria",           "sRWU")
dokRwu.setField("dataWystawienia", datap)
dokRwu.setField("dataOperacji",    datap)
dokRwu.setField("dzial",           "M. PROD")
dokRwu.setField("opis",            using "RWU dla daty %s", datap)
dokRwu.setField("bufor",           "1")

// Brak daneKh — RWU nie ma kontrahenta zewnętrznego

dokRwu.beginSection("Pozycja dokumentu")
    dokRwu.setField("kod",   "Kurczak żywy -8")
    dokRwu.setField("ilosc", using "%.2f", gSumaKg)
    dokRwu.setField("cena",  "0")
dokRwu.endSection()

idMg = dokRwu.importMg()
```

---

# 7. AMBasic — funkcje wbudowane i NIEISTNIEJĄCE

## 7.1 Co ISTNIEJE

### Systemowe

```basic
getAdoConnection()              // wbudowane połączenie do Symfonii
createObject("ADODB.Connection") // tworzenie obiektu COM
noOutput()                       // wyłącza standardowe wyjście (cichy tryb)
ExecForm                         // uruchomienie formularza
Error "komunikat"                // przerwanie skryptu z komunikatem (pusty = bez)
message "tekst"                  // okno komunikatu (zwykła informacja)
message using "format %s", arg   // sformatowany komunikat
```

### Tekstowe

```basic
using "format %s %d", str, num   // sformatowanie stringa (działa też w form, message)
val("123.45")                    // konwersja string → float
```

### Daty

```basic
Date d
d.today()                        // ustaw na dziś
d.addDays(n)                     // dodaj n dni
String s = d.toStr()             // → "YYYY-MM-DD"
```

### Importu

```basic
IORec dok
dok.setField("nazwa", "wartosc")
dok.beginSection("nazwa_sekcji")
dok.endSection()
dok.importMg()                   // metoda — magazynowy
ImportZK(dok)                    // funkcja globalna — zakupowy
ImportSP(dok)                    // funkcja globalna — sprzedażowy
```

## 7.2 Co NIE ISTNIEJE — kluczowa lista

```basic
// ❌ NIE MA TYCH FUNKCJI w AMBasic Symfonia Handel:
yesNo(...)                      // brak
msgBox(...)                     // brak
inputBox(...)                   // brak
chr(13), chr(10), chr(...)      // brak — nie ma sposobu na nowe linie w stringach
trim(...)                       // brak — używaj LTRIM/RTRIM w SQL
ltrim(...) / rtrim(...)         // brak (jako funkcje AMBasic)
len(...)                        // brak — używaj LEN() w SQL
mid(...) / left(...) / right(...) // brak — używaj SUBSTRING w SQL
ucase(...) / lcase(...)         // brak — używaj UPPER/LOWER w SQL
replace(...)                    // brak — używaj REPLACE w SQL
instr(...)                      // brak — używaj CHARINDEX/PATINDEX w SQL
isNull(...)                     // brak — używaj ISNULL w SQL
asc(...)                        // brak
str(...)                        // brak (jako funkcja) — używaj `using "%d", n`
cstr(...)                       // brak
```

## 7.3 Strategie obejścia braków

### Brak msgBox / yesNo → użyj `form` z dwoma przyciskami

```basic
int sub Zapytaj(String pPytanie)
    int wynik
    form "Pytanie", 400, 150
        ground 100, 180, 220
        Text pPytanie, 20, 20, 360, 40
        button "&Tak",  50, 90, 120, 40, 2
        button "&Nie", 230, 90, 120, 40, 3
    wynik = ExecForm

    if wynik == 2 then
        Zapytaj = 1                 // TAK
    else
        Zapytaj = 0                 // NIE / Anulowano
    endif
endsub
```

### Brak chr() → wieloliniowy tekst przez wiele kontrolek `Text`

```basic
// ❌ NIE DZIAŁA:
String s
s = "Linia 1" + chr(13) + chr(10) + "Linia 2"

// ✅ DZIAŁA — kilka oddzielnych Text w form:
form "Komunikat", 400, 150
    Text "Linia 1", 20, 20, 360, 22
    Text "Linia 2", 20, 45, 360, 22
    Text "Linia 3", 20, 70, 360, 22
    button "OK", 150, 100, 100, 35, 2
ExecForm
```

### Brak trim() → ramka SQL

```basic
sql = "WHERE LTRIM(RTRIM(CustomerGID)) = '" + sId + "'"
```

### Brak left/right/substring → SQL

```basic
sql = "SELECT LEFT(kod, 5) AS prefix FROM HM.TW WHERE id = " + sId
```

---

# 8. Pułapki i błędy AMBasic — kompletna lista

## 8.1 Lista wszystkich znanych pułapek

| #  | Pułapka                                                  | Skutek                                  | Rozwiązanie                                                |
|----|----------------------------------------------------------|------------------------------------------|------------------------------------------------------------|
| 1  | Wartość `1` w button                                     | Przycisk zachowuje się jak Anuluj        | Używaj wartości `>= 2` dla akcji, `-1` dla Anuluj          |
| 2  | Typ `double`                                             | Błąd kompilacji                          | Używaj `float`                                             |
| 3  | Typ `bool`                                               | Błąd kompilacji                          | Używaj `int` (0/1)                                         |
| 4  | `IORec` deklarowany globalnie                            | Stan między wywołaniami, dziwne błędy    | Zawsze deklaruj IORec wewnątrz funkcji                     |
| 5  | `yesNo()`, `msgBox()`, `chr()`                           | Funkcja nie istnieje                     | Użyj `form` z przyciskami (zob. wzorzec 7.3)               |
| 6  | `importMg()` na fakturze (FVZ/FVR/FVS)                   | Dokument nie powstaje lub błąd           | Faktury → `ImportZK()` (zakup), `ImportSP()` (sprzedaż)    |
| 7  | Zamknięcie `getAdoConnection()` przez `con.close()`      | Błędy w dalszej pracy Symfonii           | NIE zamykać tego połączenia                                |
| 8  | Polskie znaki / spacje w `kod` towaru niezgodne z HM.TW  | Pozycja nie dodaje się do dokumentu      | Sprawdź dokładnie w `HM.TW.kod` przed kodowaniem stałej    |
| 9  | NULL w polu wczytanym do String                          | Błąd wykonania                           | `ISNULL(pole, '')` w SQL                                   |
| 10 | Dynamiczny `using` w `form "..."`                        | Tytuł pusty lub błąd                     | Najpierw przypisz do zmiennej, potem `form sTytul, ...`    |
| 11 | `elseif` lub `else if`                                   | Niepewne wsparcie w niektórych wersjach  | Zagnieżdżaj if/else                                        |
| 12 | `continue`, `break` w pętli                              | Nie istnieje                             | Steruj przez warunki                                       |
| 13 | Cudzysłowy w SQL (apostrof w danych)                     | Błąd składni SQL                         | Używaj parametrów Command lub sprawdź dane wcześniej       |
| 14 | Brak typu zwracanego w `sub`                             | Niektóre wersje protestują               | Zawsze deklaruj `int sub` / `String sub`                   |
| 15 | Wywołanie `sub` bez przypisania wyniku                   | Czasem błąd (zależy od wersji)           | `int tmp; tmp = MojaFunkcja()`                             |
| 16 | `cursorType = 3` z `getAdoConnection()`                  | Czasem błędy                             | Używaj `cursorType = 1`                                    |
| 17 | Konwersja int → String w konkatenacji                    | Czasem nieoczekiwane wyniki              | Używaj `using "%d", n` zamiast `+ n`                       |
| 18 | Aktualizacja `bufor=0` w HM.DK dla PZ                    | Nie działa — PZ jest w HM.MG!            | Zatwierdź odpowiednią tabelę: PZ/RWU → HM.MG, FV → HM.DK   |
| 19 | Mieszanie `ImportZK` (metoda) vs funkcja                 | Błąd                                     | `ImportZK(dok)` to FUNKCJA globalna, nie metoda obiektu    |
| 20 | Przycisk OK jako wartość `1`                             | jw. — działa jak Anuluj                  | Wartość `2` dla głównego przycisku                         |

## 8.2 Komunikat błędu — diagnostyka

| Komunikat                                | Prawdopodobna przyczyna                               |
|------------------------------------------|-------------------------------------------------------|
| `Invalid column name 'X'`                | Zła nazwa kolumny SQL — sprawdź case i schemat        |
| `Cannot find object 'HM.MG'`             | Nieprawidłowy schemat lub baza                        |
| `Type mismatch`                          | Próba użycia nieistniejącego typu (np. `double`)      |
| Skrypt kończy się bez efektu             | Przycisk z wartością `1` lub `Error ""` zamknął skrypt |
| Pozycja nie pojawia się na dokumencie    | Kod towaru niezgodny z `HM.TW.kod`                    |
| Faktura tworzy się, ale jest pusta       | Brak `daneKh` lub niezmapowany kontrahent             |
| Dokument w buforze, nie chce się zatw.   | Próba aktualizacji złej tabeli (HM.DK vs HM.MG)       |

---

# 9. Struktura bazy LibraNet

## 9.1 Połączenie

| Parametr | Wartość             |
|----------|---------------------|
| Host     | `192.168.0.109`     |
| Baza     | `LibraNet`          |
| Schemat  | `dbo`               |

## 9.2 Tabela `dbo.FarmerCalc` — specyfikacje dostaw od hodowców

Najważniejsza tabela źródłowa dla eksportu PZ/FVR/FVZ.

| Kolumna             | Typ           | Opis                                              |
|---------------------|---------------|---------------------------------------------------|
| `ID`                | int (PK)      | Identyfikator specyfikacji                        |
| `CalcDate`          | datetime      | Data uboju                                        |
| `CarLp`             | int           | Numer samochodu / pozycja                         |
| `CustomerGID`       | varchar       | ID hodowcy → `Dostawcy.ID`                        |
| `PayWgt`            | float         | Waga do zapłaty (kg)                              |
| `NettoFarmWeight`   | float         | Waga netto z farmy                                |
| `NettoWeight`       | float         | Waga netto (po obróbce)                           |
| `Price`             | float         | Cena za kg                                        |
| `Symfonia`          | int / bit     | Flaga eksportu (0 = nie wyeksportowane, 1 = tak)  |
| `SymfoniaIdFV`      | int           | ID utworzonej faktury w Symfonii (HM.DK.id)       |
| `SymfoniaNrFV`      | varchar       | Numer faktury w Symfonii (np. "0001/26/FVR")      |

> **Konwencja eksportu wagi:** preferowane jest pole `PayWgt`. Gdy puste,
> fallback do `NettoFarmWeight`, dalej `NettoWeight`:
> ```sql
> COALESCE(PayWgt, NettoFarmWeight, NettoWeight, 0)
> ```

## 9.3 Tabela `dbo.Dostawcy` — hodowcy/dostawcy

| Kolumna       | Typ      | Opis                                              |
|---------------|----------|---------------------------------------------------|
| `ID`          | varchar  | Identyfikator (= `FarmerCalc.CustomerGID`)        |
| `ShortName`   | varchar  | Nazwa skrócona                                    |
| `NIP`         | varchar  | NIP (może być pusty!)                             |
| `IdSymf`      | int      | **ID kontrahenta w Symfonii (`STContractors.Id`)** |

> **Pole `IdSymf` jest kluczem mapowania.** Jeśli puste — dostawca nie jest
> zmapowany i nie da się go wyeksportować.

## 9.4 Tabela `dbo.ZamowieniaMieso` — nagłówki zamówień sprzedaży

| Kolumna                       | Typ        | Opis                                          |
|-------------------------------|------------|-----------------------------------------------|
| `Id`                          | int (PK)   | ID zamówienia                                 |
| `DataZamowienia`              | datetime   | Data złożenia                                 |
| `KlientId`                    | int        | **ID klienta = `STContractors.Id` (mapowanie 1:1)** |
| `Uwagi`                       | varchar    | Uwagi do zamówienia                           |
| `Status`                      | varchar    | `Nowe`, `W realizacji`, `Zrealizowane`, `Wydano`, `Anulowane` |
| `DataUtworzenia`              | datetime   | Kiedy utworzono                               |
| `RejAuta`, `RejNaczepy`       | varchar    | Numery rejestracyjne                          |
| `Kierowca`                    | varchar    | Kierowca                                      |
| `DataWyjazdu`, `DataPrzyjazdu`, `DataPowrotu` | datetime | Daty transportu             |
| `IdUser`                      | int        | Użytkownik tworzący                           |
| `LiczbaPojemnikow`, `LiczbaPalet` | int / float | Logistyka                                |
| `TrybE2`                      | bit        | Tryb E2                                       |
| `TransportKursID`             | int        | FK do tabeli kursów (TransportPL)             |
| `TransportStatus`             | varchar    | Np. "Oczekuje"                                |
| `DataProdukcji`               | date       |                                               |
| `DataUboju`                   | date       | **Kluczowa dla eksportu**                     |
| `DataWydania`                 | datetime   |                                               |
| `KtoWydal`                    | int        |                                               |
| `CzyZrealizowane`             | bit        |                                               |
| `CzyWydane`                   | bit        |                                               |
| `Waluta`                      | varchar    | "PLN", ...                                    |
| `ProcentRealizacji`           | float      |                                               |
| `CzyZafakturowane`            | bit        | **Flaga eksportu — 0 = do faktury, 1 = już**  |
| `NumerFaktury`                | varchar    | Numer FVS po eksporcie                        |
| `CzyZmodyfikowaneDlaFaktur`   | bit        | Modyfikacje wymagające ponownej oceny         |

## 9.5 Tabela `dbo.ZamowieniaMiesoTowar` — pozycje zamówień

| Kolumna             | Typ      | Opis                                              |
|---------------------|----------|---------------------------------------------------|
| `Id`                | int (PK) | ID pozycji                                        |
| `ZamowienieId`      | int      | FK → `ZamowieniaMieso.Id`                         |
| `KodTowaru`         | varchar  | **Wbrew nazwie — to ID liczbowe z `HM.TW.ID`**    |
| `Ilosc`             | varchar  | Ilość (zapisana jako string!)                     |
| `Cena`              | varchar  | Cena (string, może mieć przecinek polski)         |
| `Pojemniki`         | int      |                                                   |
| `Palety`            | float    |                                                   |
| `E2`, `Folia`, `Hallal` | bit  | Cechy                                             |
| `IloscZrealizowana` | float    |                                                   |
| `PowodBraku`        | varchar  |                                                   |

> **Konwersja ceny w C# (CultureInfo!):**
> ```csharp
> decimal.TryParse(cena.Replace(",", "."), NumberStyles.Any,
>                  CultureInfo.InvariantCulture, out cenaDecimal);
> ```

## 9.6 Tabela `dbo.HistoriaZmianZamowien` — historia zmian

| Kolumna           | Typ      | Opis                            |
|-------------------|----------|---------------------------------|
| `ZamowienieId`    | int      | FK do zamówienia                |
| `TypZmiany`       | varchar  | `EDYCJA`, `UTWORZENIE`, ...     |
| `OpisZmiany`      | varchar  | Opis                            |
| `UzytkownikNazwa` | varchar  | Kto zmienił                     |
| `DataZmiany`      | datetime | Kiedy                           |

## 9.7 Tabela `dbo.Odbiorcy` — odbiorcy/klienci (lokalni w LibraNet)

(Pomocnicza — najważniejsze mapowanie idzie przez `ZamowieniaMieso.KlientId`
do `SSCommon.STContractors.Id`.)

## 9.8 Inne tabele zauważone w schemacie

`KonfiguracjaProduktow`, `Kontrahenci`, `MapowanieHandlowcow`,
`OcenyDostawcow`, `OdbiorcyKurczaka`, `Oferty`, `Oferty_Pozycje`,
`StanyMagazynowe`, `TransportTrip`, `TransportTripOrder`,
`UmowyKontraktacji`, `WstawieniaKurczakow`, `ZamowieniaMiesoSnapshot`,
`ZamowieniaSzablony`, `ZamowieniaSzablonyPozycje`, `vZamowieniaTransport`
(widok), `vw_TransportTripWithOrders` (widok).

---

# 10. Struktura bazy HANDEL (Symfonia)

## 10.1 Przegląd schematów

| Schemat       | Zawartość                                              |
|---------------|--------------------------------------------------------|
| `HM`          | Główne tabele handlowe (dokumenty, towary, magazyny)   |
| `SSCommon`    | Kartoteki współdzielone (kontrahenci, klasyfikacja)    |
| `dr`          | Dane relacyjne / powiązania dokumentów                 |
| `FK`          | Finansowo-księgowe (synchronizacja z księgowością)     |
| `MF`          | Manufacturing (produkcja)                              |

## 10.2 `HM.DK` — dokumenty HANDLOWE (faktury, KP, KW, paragony)

> **KLUCZOWE:** Tabela `HM.DK` zawiera **tylko dokumenty handlowe**:
> faktury (FVS, FVZ, FVR, FW), korekty (KFS, KFZ, FKS, FKSB), faktury
> RR (FVRR), itp. **Dokumenty magazynowe (PZ, WZ, RWU, MM) są w `HM.MG`!**

### Najważniejsze kolumny

| Kolumna       | Typ              | Opis                                                  |
|---------------|------------------|-------------------------------------------------------|
| `id`          | int (PK)         | ID dokumentu                                          |
| `flag`        | smallint         | Flagi bitowe (np. zaksięgowane)                       |
| `aktywny`     | bit              | Aktywny / anulowany                                   |
| `subtyp`      | smallint         | Podtyp                                                |
| `typ`         | smallint         | Liczbowy kod typu (`0` = sprzedaż FVS, `202` = zakup) |
| `kod`         | varchar          | **Numer dokumentu** np. `"0001/26/FVR"`               |
| `seria`       | varchar          | Seria                                                 |
| `serianr`     | int              | Numer w serii                                         |
| `nazwa`       | varchar          |                                                       |
| `data`        | datetime         | Data wystawienia                                      |
| `datasp`      | datetime         | Data sprzedaży / operacji                             |
| `datawplywu`  | datetime         |                                                       |
| `opis`        | varchar          | Opis (nasz `setField("opis", ...)`)                   |
| `khid`        | int              | **ID kontrahenta** (= `STContractors.Id`)             |
| `khadid`      | int              | ID adresu                                             |
| `odid`, `odadid`| int            | Odbiorca i adres odbiorcy                             |
| `netto`, `vat`| float            | Wartości                                              |
| `typ_dk`      | varchar          | **Tekstowy typ:** `PZ`, `FVR`, `FVZ`, `FVS`, `WZ`, `RWU`, ... |
| `iddokkoryg`  | int              | ID dokumentu korygowanego (dla korekt)                |
| `magazyn`     | int              | Magazyn                                               |
| `formaplatn`  | int              | Forma płatności                                       |
| `bufor`       | smallint         | **0 = zatwierdzony, 1 = w buforze**                   |
| `anulowany`   | smallint         |                                                       |
| `wystawil`    | int              |                                                       |
| `createdBy`, `createdDate`     | int / datetime |                                          |
| `modifiedBy`, `modifiedDate`   | int / datetime |                                          |
| `walNetto`, `walBrutto`        | float          | Wartości w walucie                       |
| `eFaktura`, `statusKsef`       | smallint / int |                                          |
| `guid`                         | uniqueidentifier |                                        |
| `splitPayment`                 | bit            |                                          |
| `jpk_v7`                       | varchar        | Status JPK                               |

### Kody `typ` (pole numeryczne)

| Wartość | Znaczenie               |
|---------|-------------------------|
| `0`     | Dokument sprzedaży (FVS, FW, KFS, ...) |
| `202`   | Dokument zakupu (FVR, FVZ, FVRR, ...) |

> **Praktyka:** filtruj raczej po `typ_dk` (tekstowe) — czytelniej.

### Przykładowe rekordy zaobserwowane

```
id      kod                bufor  typ
902129  FVRR/0036/26       1      202
902107  0459/26/FVS        1      0
902101  0454/26/FVS        0      0
902098  75/26/FW           0      0
902089  18/26/FKSB         0      0
902088  0015/26/FKS        0      0
```

## 10.3 `HM.MG` — dokumenty MAGAZYNOWE (PZ, WZ, RWU, MM, PW, RW)

Struktura podobna do `HM.DK`, ale **tylko dokumenty magazynowe**.

### Kolumny

| Kolumna       | Typ              | Opis                                                  |
|---------------|------------------|-------------------------------------------------------|
| `id`          | int (PK)         | ID dokumentu MG                                       |
| `flag`        | smallint         | Flagi bitowe                                          |
| `aktywny`     | bit              |                                                       |
| `subtyp`      | smallint         |                                                       |
| `typ`         | smallint         | Liczbowy typ                                          |
| `kod`         | varchar          | **Numer dokumentu** np. `"PZ/0123/26"`, `"RWU/0001"`  |
| `seria`       | varchar          |                                                       |
| `serianr`     | int              |                                                       |
| `nazwa`       | varchar          |                                                       |
| `data`        | datetime         |                                                       |
| `datasp`      | datetime         |                                                       |
| `opis`        | varchar          |                                                       |
| `khid`, `khadid` | int           | Kontrahent (dla PZ/WZ)                                |
| `khdzial`     | int              |                                                       |
| `termin`      | datetime         | Termin                                                |
| `netto`       | float            |                                                       |
| `typ_dk`      | varchar          | `"PZ"`, `"WZ"`, `"RWU"`, `"MM"`, `"PW"`, `"RW"`       |
| `magazyn`     | int              | ID magazynu                                           |
| `przychod`, `rozchod` | float    |                                                       |
| `wartoscWz`   | float            |                                                       |
| `wartk`       | float            |                                                       |
| `bufor`       | smallint         | **0 = zatwierdzony, 1 = w buforze**                   |
| `rozlmg`      | smallint         | Status rozliczenia magazynowego                       |
| `anulowany`   | smallint         |                                                       |
| `iddokkoryg`  | int              |                                                       |
| `statusFK`, `statusMig` | smallint |                                                       |
| `ProductionOrderID` | int        | Powiązanie z produkcją                                |
| `IsProductionTrash` | bit        |                                                       |
| `guid`        | uniqueidentifier |                                                       |

> **PUŁAPKA ZATWIERDZANIA:** Próba `UPDATE HM.DK SET bufor = 0 WHERE typ_dk = 'PZ'`
> nigdy nie znajdzie rekordu — PZ są w `HM.MG`. To bardzo częsty błąd!

## 10.4 `HM.TW` — towary

| Kolumna       | Typ              | Opis                                              |
|---------------|------------------|---------------------------------------------------|
| `id` / `ID`   | int (PK)         | ID towaru (= `ZamowieniaMiesoTowar.KodTowaru`)    |
| `kod`         | varchar          | **Tekstowy kod używany w `setField("kod", ...)`** |
| `nazwa`       | varchar          | Pełna nazwa                                       |
| `jm`          | varchar          | Jednostka miary                                   |
| `vat`         | float / int      | Stawka VAT                                        |
| `cena`        | float            | Cena cennikowa                                    |
| `stan`        | float            | Stan magazynowy                                   |
| `katalog`     | int              | ID kategorii (np. `67095` = Świeże, `67153` = Mrożone) |

## 10.5 `HM.DP` — pozycje dokumentów handlowych (HM.DK)

| Kolumna   | Typ   | Opis                              |
|-----------|-------|-----------------------------------|
| `id`      | int   | ID pozycji                        |
| `idDk`    | int   | FK → `HM.DK.id`                   |
| `tw`      | int   | FK → `HM.TW.id`                   |
| `ilosc`   | float |                                   |
| `cena`    | float |                                   |
| `wartosc` | float |                                   |

## 10.6 `HM.MGH` — pozycje dokumentów magazynowych (HM.MG)

(Analogicznie do `HM.DP`, ale dla `HM.MG`. Nazwa wskazuje "MG-Header"
ale faktycznie pozycje. Używana w joinach przy raportach.)

## 10.7 `SSCommon.STContractors` — kontrahenci

| Kolumna       | Typ      | Opis                                              |
|---------------|----------|---------------------------------------------------|
| `Id`          | int (PK) | **ID kontrahenta — używane jako `khId` i jako `Dostawcy.IdSymf`** |
| `Shortcut`    | varchar  | Skrót / kod kontrahenta                           |
| `Name`        | varchar  | Pełna nazwa firmy / osoby                         |
| `NIP`         | varchar  | NIP                                               |
| `Code`        | varchar  | Kod (alternatywny)                                |

## 10.8 `SSCommon.ContractorClassification` — klasyfikacja kontrahentów

| Kolumna                  | Opis                                |
|--------------------------|-------------------------------------|
| `ElementId`              | FK → `STContractors.Id`             |
| `CDim_Handlowiec_Val`    | Przypisany handlowiec               |

## 10.9 `dr.DocumentsLinks` — powiązania dokumentów

Tabela w schemacie `dr` (NIE `HM`!) — przechowuje powiązania PZ↔FV i inne.

> Struktura wymaga sprawdzenia w czasie pracy — dokładny schemat tej
> tabeli nie został w pełni zmapowany. Kluczowe przy tworzeniu skryptów,
> które chcą programowo łączyć PZ z fakturą zakupu.
>
> **Tymczasowe rozwiązanie:** użytkownik łączy PZ ↔ FV ręcznie w GUI
> Symfonii (w PZ kliknij "Dokument zakupu") albo Symfonia robi to
> automatycznie przy zatwierdzaniu.

## 10.10 Inne istotne tabele zauważone

`HM._MagazynMap`, `HM._MapMgDoc`, `HM.Image`, `HM.ProductImage`,
`HM.relacje`, `HM.brwrel`, `HM.fk_brwrel`, `MF.ProductionInventoryItemGroup`,
`HM.REP_PurchaseDocuments_RelatedDocuments`, `HM.REP_SaleDocuments_RelatedDocuments`,
`HM.OfferDocumentsRelationsForeignOrderDocuments`, `HM.STElementRelations`,
`HM.STStructureRelations`, `HM.ReleaseInfo`.

## 10.11 Mapa "co gdzie szukać"

| Cel                                       | Tabela                  |
|-------------------------------------------|-------------------------|
| Faktura sprzedaży FVS                     | `HM.DK` (typ_dk = 'FVS')|
| Faktura zakupu FVZ                        | `HM.DK` (typ_dk = 'FVZ')|
| Faktura RR / VAT RR                       | `HM.DK` (typ_dk = 'FVR' lub 'FVRR') |
| PZ — przyjęcie zewnętrzne                 | `HM.MG` (typ_dk = 'PZ') |
| WZ — wydanie zewnętrzne                   | `HM.MG` (typ_dk = 'WZ') |
| RWU — rozchód wewnętrzny / usługa         | `HM.MG` (typ_dk = 'RWU')|
| MM — przesunięcie międzymagazynowe        | `HM.MG` (typ_dk = 'MM') |
| Pozycje faktur                            | `HM.DP`                 |
| Pozycje dokumentów MG                     | `HM.MGH`                |
| Towary                                    | `HM.TW`                 |
| Kontrahenci                               | `SSCommon.STContractors`|
| Powiązania dokumentów                     | `dr.DocumentsLinks`     |

---

# 11. Mapowanie LibraNet ↔ Symfonia

## 11.1 Tabela mapowań

| Encja LibraNet             | Pole łączące                                | Encja Symfonia                | Pole docelowe                  |
|----------------------------|---------------------------------------------|-------------------------------|--------------------------------|
| `Dostawcy`                 | `Dostawcy.IdSymf`                           | `SSCommon.STContractors`      | `Id`                           |
| `Odbiorcy` / `ZamowieniaMieso` | `ZamowieniaMieso.KlientId`              | `SSCommon.STContractors`      | `Id` (mapowanie 1:1)           |
| `ZamowieniaMiesoTowar`     | `KodTowaru` (faktycznie ID liczbowe)        | `HM.TW`                       | `ID` (lookup → `kod`)          |
| `FarmerCalc.SymfoniaIdFV`  | wynik importu                                | `HM.DK.id`                    |                                |
| `FarmerCalc.SymfoniaNrFV`  | wynik importu                                | `HM.DK.kod`                   |                                |
| `ZamowieniaMieso.NumerFaktury` | wynik importu                            | `HM.DK.kod`                   |                                |

## 11.2 Mapowanie kontrahentów — różnica zakup vs sprzedaż

```
ZAKUPY (od hodowców):
  LibraNet.Dostawcy.IdSymf  ──►  Symfonia.STContractors.Id
  (potrzebne ręczne mapowanie — dostawcy są zewnętrznie identyfikowani)

SPRZEDAŻ (do odbiorców):
  LibraNet.ZamowieniaMieso.KlientId  ═══  Symfonia.STContractors.Id
  (mapowanie 1:1 — KlientId JEST ID-em kontrahenta w Symfonii)
```

## 11.3 Mapowanie towarów — pułapka nazewnictwa

W tabeli `dbo.ZamowieniaMiesoTowar` kolumna nazywa się `KodTowaru`,
ale **faktycznie zawiera ID liczbowe** z `HM.TW.ID`, nie tekstowy kod.

```
ZamowieniaMiesoTowar.KodTowaru = "66443"
                                    │
                                    ▼ (lookup)
HM.TW WHERE id = 66443:
  id    = 66443
  kod   = "Kurczak A"      ◄── TO trafia do setField("kod", ...)
  nazwa = "Kurczak A świeży"
```

**Skrypt MUSI robić lookup** przed wstawieniem pozycji.

## 11.4 Konwencje kodów towarów (znane)

| Pole `kod` w HM.TW   | Znaczenie                              |
|----------------------|----------------------------------------|
| `Kurczak żywy -7`    | Kurczak żywy z faktury VAT RR (rolnik) |
| `Kurczak żywy -8`    | Kurczak żywy z faktury VAT (vatowiec)  |
| `Kurczak A`          | Kurczak A — produkt sprzedażowy        |
| ...                  |                                        |

> **WAŻNE:** Sprawdź w czasie pracy `SELECT kod, nazwa FROM HM.TW
> WHERE nazwa LIKE '%Kurczak%'` — kody są wrażliwe na spacje
> i polskie znaki!

---

# 12. Workflow eksportu zakupów (PZ + FVR/FVZ + RWU)

## 12.1 Skrypt: `ExportPZLibraNet_v37+`

### Cel
Konwertuje dzienne dostawy hodowców (`FarmerCalc`) na komplet dokumentów
zakupowych w Symfonii.

### Pełny workflow

```
1. Operator wybiera datę uboju
   └─► form "EKSPORT PZ Z LIBRANET"
       Wybór: NOWE (Symfonia=0) / WSZYSTKIE / Anuluj

2. Pobranie listy dostawców z dnia
   SELECT DISTINCT fc.CustomerGID, d.ShortName, d.IdSymf, d.NIP
   FROM dbo.FarmerCalc fc
   JOIN dbo.Dostawcy d ON LTRIM(RTRIM(fc.CustomerGID)) = d.ID
   WHERE fc.CalcDate = @data
     AND (@bIgnorujFlage = 1 OR ISNULL(fc.Symfonia, 0) = 0)

3. Pętla po dostawcach
   Dla każdego dostawcy:

   a) Pomiń jeśli IdSymf is NULL (brak mapowania)

   b) Zapytaj operatora: FVR (rolnik) czy FVZ (vatowiec)?
      └─► form z dwoma przyciskami
          (UWAGA: wartości 2 i 3, nie 1!)

   c) Utwórz PZ:
      - typDk = "PZ", seria = "sPZ", bufor = "1"
      - dzial = "M. PROD"
      - daneKh: khId = IdSymf
      - Pozycje z FarmerCalc:
        kod   = "Kurczak żywy -8"  (zawsze -8 dla PZ)
        ilosc = waga (PayWgt)
        cena  = Price
      - importMg() → idPZ

   d) Utwórz FVR lub FVZ (zależnie od wyboru):
      FVR: typDk = "FVR", seria = "sFVR", kod towaru = "Kurczak żywy -7"
      FVZ: typDk = "FVZ", seria = "sFVZ", kod towaru = "Kurczak żywy -8"
      - daneKh: khId = IdSymf
      - dataDokumentuObcego = data uboju (kluczowe!)
      - dataZakupu          = data uboju (kluczowe!)
      - Pozycje (te same waga/cena co PZ)
      - ImportZK(dokFv) → idFV

   e) Pobierz numer faktury z Symfonii:
      SELECT kod FROM HM.DK WHERE id = @idFV

   f) Update LibraNet:
      UPDATE FarmerCalc SET
        Symfonia = 1,
        SymfoniaIdFV = @idFV,
        SymfoniaNrFV = @kod
      WHERE CalcDate = @data
        AND LTRIM(RTRIM(CustomerGID)) = @CustomerGID

   g) Akumuluj sumę kg do gSumaKg

4. Po pętli — utworzenie RWU z sumą kg
   - typDk = "RWU", seria = "sRWU", bufor = "1"
   - dzial = "M. PROD"
   - Brak daneKh (RWU nie ma kontrahenta)
   - Pozycja: kod = "Kurczak żywy -8", ilosc = gSumaKg, cena = 0
   - importMg() → idRWU

5. Raport końcowy
   message using "Utworzono: PZ %d, FV %d, RWU 1 (suma kg: %.2f)",
                 nLicznikPZ, nLicznikFV, gSumaKg
```

## 12.2 Kluczowe decyzje projektowe

1. **Każdy dostawca = osobne PZ + osobna FVR/FVZ** (jednoznaczne powiązanie).
2. **Jedno wspólne RWU na koniec** — łączna suma kg z dnia uboju.
3. **Wszystko w buforze** — operator zatwierdza ręcznie po weryfikacji.
4. **Dialog typu faktury per dostawca** — typ podatnika nie jest w bazie
   LibraNet (przyszły kierunek rozwoju: dodać pole `TypPodatnika` do
   `dbo.Dostawcy`).
5. **Kontrahent przez `khId`, nie NIP** — niezawodne nawet przy pustych NIP.

## 12.3 Interfejs użytkownika

```
┌─────────────────────────────────────────────────┐
│  EKSPORT PZ Z LIBRANET            [×]           │
│                                                 │
│  Data uboju: [_____________]                    │
│                                                 │
│  Dla każdego dostawcy: PZ + Faktura VAT RR/FVZ  │
│  Na końcu: RWU z sumą kg (PZ-FV powiązane)      │
│                                                 │
│  [Eksportuj NOWE]  [Eksportuj WSZYSTKIE]  [Anuluj] │
└─────────────────────────────────────────────────┘

Dla każdego dostawcy:

┌─────────────────────────────────────────┐
│  Wybór typu faktury        [×]          │
│                                         │
│  Wodzyński Stanisław                    │
│                                         │
│  [TAK - FVR Rolnik]   [NIE - FVZ Vatowiec] │
└─────────────────────────────────────────┘
```

## 12.4 Krytyczne pola do wstawienia

```basic
// Daty obowiązkowe na fakturze zakupu
dokFv.setField("dataDokumentuObcego", pData)   // "z dnia" w UI Symfonii
dokFv.setField("dataZakupu",          pData)   // "data zakupu" w UI

// Bez tych pól faktura tworzy się, ale ma puste pola w GUI Symfonii
// (znany bug — zauważony w wersji v36, naprawiony w v37+)
```

## 12.5 Co aktualizować w `FarmerCalc` po sukcesie

```sql
UPDATE dbo.FarmerCalc
SET
    Symfonia      = 1,
    SymfoniaIdFV  = @idFV,
    SymfoniaNrFV  = @kodFV
WHERE CalcDate = @data
  AND LTRIM(RTRIM(CustomerGID)) = @customerGid
```

**Wszystkie wiersze danego dostawcy z danego dnia** dostają ten sam IdFV/NrFV
(jedna faktura per dostawca per dzień).

## 12.6 Pomijanie / specjalne przypadki

| Przypadek                          | Zachowanie                                      |
|------------------------------------|-------------------------------------------------|
| `IdSymf` IS NULL                   | Pomiń dostawcę, dodaj do raportu jako "brak mapowania" |
| `Symfonia = 1` (już wyeksportowany)| Pomiń (chyba że tryb "WSZYSTKIE")               |
| Ilość 0 lub ujemna                 | Pomiń pozycję, nie wstawiaj do dokumentu        |
| Cena 0                             | Wstaw, ale ostrzeż w raporcie                   |

---

# 13. Workflow eksportu sprzedaży (WZ + FVS)

## 13.1 Skrypt: `WZ.sc`

### Cel
Konwertuje zrealizowane zamówienia (`ZamowieniaMieso`) na faktury sprzedaży
w Symfonii (z opcją tworzenia WZ).

### Pełny workflow

```
1. Operator wybiera datę uboju
   └─► form "WZ - Eksport faktur" (800x550)
       Duże okno z instrukcją

2. Pobranie zamówień
   SELECT z.Id, z.KlientId, z.Uwagi,
          (SELECT SUM(CAST(Ilosc AS FLOAT))
           FROM ZamowieniaMiesoTowar
           WHERE ZamowienieId = z.Id) AS SumaIlosc
   FROM dbo.ZamowieniaMieso z
   WHERE z.DataUboju = @data
     AND z.Status IN ('Zrealizowane', 'Wydano')
     AND ISNULL(z.CzyZafakturowane, 0) = 0
   ORDER BY z.Id

3. Wyświetl listę z checkboxami (max 15 zamówień)
   └─► form "Wybierz zamówienia" (950x850)
       Każde zamówienie ma checkbox (domyślnie zaznaczony)
       [EKSPORTUJ ZAZNACZONE] [Anuluj]

4. Pętla po zaznaczonych zamówieniach
   Dla każdego (gSel == 1):

   a) Pobierz nazwę klienta (Shortcut)
      SELECT Shortcut FROM SSCommon.STContractors WHERE Id = @klientId

   b) Pobierz pozycje
      SELECT KodTowaru, Ilosc,
             ISNULL(NULLIF(Cena, ''), '0') AS Cena
      FROM dbo.ZamowieniaMiesoTowar
      WHERE ZamowienieId = @zamId

   c) Dla każdej pozycji — lookup kodu towaru:
      SELECT kod FROM HM.TW WHERE id = @kodTowaru

   d) Utwórz FVS:
      typDk = "FVS", seria = "sFVS", bufor = "1"
      daneKh: khId = klientId
      Pozycje (kod z lookup, Ilosc, Cena)
      ImportSP(dokFvs) → idFV

   e) Pobierz numer faktury z HM.DK.kod

   f) Update LibraNet:
      UPDATE ZamowieniaMieso SET
        CzyZafakturowane = 1,
        NumerFaktury = @kod
      WHERE Id = @zamId

5. Raport końcowy
   message using "Utworzono %d faktur FVS w buforze", nLicznik
```

## 13.2 Kluczowe różnice vs eksport zakupów

| Aspekt              | Zakupy (ExportPZLibraNet)         | Sprzedaż (WZ.sc)            |
|---------------------|-----------------------------------|-----------------------------|
| Tabela źródłowa     | `dbo.FarmerCalc`                  | `dbo.ZamowieniaMieso`       |
| Mapowanie kontrah.  | `Dostawcy.IdSymf`                 | `KlientId` = bezpośrednio   |
| Lookup towaru       | Stała ("Kurczak żywy -8")         | Per pozycja przez `HM.TW.ID`|
| Funkcja importu     | `importMg()` + `ImportZK()`       | `ImportSP()`                |
| Liczba dokumentów   | PZ + FV per dostawca + 1 RWU      | 1 FVS per zamówienie        |
| Wybór elementów     | Wszystkie (lub wg flagi Symfonia) | Checkboxy per zamówienie    |
| Flaga eksportu      | `FarmerCalc.Symfonia`             | `ZamowieniaMieso.CzyZafakturowane` |
| Numer wynikowy      | `FarmerCalc.SymfoniaNrFV`         | `ZamowieniaMieso.NumerFaktury` |

## 13.3 Konwersja ceny — String → Float

`Cena` w LibraNet jest `varchar` z polskim formatowaniem (`"4,50"` zamiast `"4.50"`).
Dla Symfonii potrzebujemy formatu z kropką:

```sql
-- W SQL — bezpieczna konwersja
SELECT
    REPLACE(ISNULL(NULLIF(Cena, ''), '0'), ',', '.') AS Cena
FROM dbo.ZamowieniaMiesoTowar
```

Lub w AMBasic:

```basic
String sCena
sCena = rsPoz.fields("Cena").value
// W praktyce SQL Server często sam konwertuje, ale dla bezpieczeństwa:
// sCena = REPLACE(sCena, ",", ".") — ale w AMBasic nie ma replace,
// dlatego robimy to w SQL.
```

## 13.4 Zarządzanie listą zaznaczonych

Wzorzec ze stałymi zmiennymi (max 15):

```basic
String gZam1, gZam2, /* ... */ gZam15
int gSel1, gSel2, /* ... */ gSel15
String gIdTab1, gIdTab2, /* ... */ gIdTab15

// Wypełnianie listy
nr = 0
While !rs.EOF
    if nr < 15 then
        nr = nr + 1
        if nr == 1 then
            gZam1 = using "%2d. ID:%s | %s | %s kg", nr, sId, sKlient, sSuma
            gSel1 = 1
            gIdTab1 = sId
        endif
        // ... powtórka dla gZam2..gZam15
    endif
    rs.moveNext()
Wend

// Po ExecForm — eksport tylko zaznaczonych
if gSel1 == 1 and gIdTab1 != "" then
    EksportujJedno(gIdTab1)
endif
// ... powtórka dla gSel2..gSel15
```

> **Limit 15 jest świadomy** — większy formularz nie mieści się komfortowo
> na jednym ekranie, a na ten przypadek użycia (jednorazowe wywołania
> dziennie) wystarcza. Przy potrzebie większych ilości — paginacja lub
> grupowanie.

---

# 14. Sprawdzone wzorce kodu

## 14.1 Szablon nowego skryptu

```basic
//"NazwaSkryptu.sc","Opis w menu","\Procedury",0,1.0.0,SYSTEM

// === STAŁE I ZMIENNE GLOBALNE ===
String mySrv = "192.168.0.109"
String myDb  = "LibraNet"
String myUsr = "pronova"
String myPwd = "pronova"
String gConnStr

String datap
int wynik_form

// === FORMULARZ STARTOWY ===
form "Tytuł", 450, 170
    ground 80, 200, 200
    Datedit "Data:", datap, 150, 20, 150, 22
    button "&Eksportuj", 50, 110, 130, 35, 2
    button "&Anuluj",   200, 110, 130, 35, -1
wynik_form = ExecForm

if wynik_form < 1 then
    Error ""
endif

// Connection string globalny
gConnStr = "Provider=SQLOLEDB;Data Source=" + mySrv +
           ";Initial Catalog=" + myDb +
           ";User ID=" + myUsr + ";Password=" + myPwd

// === FUNKCJE POMOCNICZE ===

String sub PobierzKodTowaru(String pIdTw)
    Dispatch conS, rsS
    String sql, wynik

    conS = getAdoConnection()
    rsS = "ADODB.Recordset"
    rsS.cursorType = 1
    rsS.lockType = 1

    sql = "SELECT ISNULL(kod, '') AS kod FROM HM.TW WHERE id = " + pIdTw
    rsS.open(sql, conS)

    if rsS.EOF then
        wynik = ""
    else
        wynik = rsS.fields("kod").value
    endif
    rsS.close()

    PobierzKodTowaru = wynik
endsub

// === GŁÓWNA FUNKCJA ===

int sub main()
    Dispatch con, rs
    String sql
    int licznik

    con = createObject("ADODB.Connection")
    con.connectionString = gConnStr
    con.open()

    rs = "ADODB.Recordset"
    rs.cursorType = 1
    rs.lockType = 1

    sql = "SELECT ... FROM ... WHERE Data = '" + datap + "'"
    rs.open(sql, con)

    licznik = 0
    While !rs.EOF
        // przetwarzanie
        licznik = licznik + 1
        rs.moveNext()
    Wend

    rs.close()
    con.close()

    message using "Przetworzono %d rekordów", licznik
    main = licznik
endsub

// === URUCHOMIENIE ===
noOutput()
main()
```

## 14.2 Wzorzec — pobranie pojedynczej wartości z Symfonii

```basic
String sub PobierzNazweKontrahenta(String pId)
    Dispatch con, rs
    String sql, wynik

    con = getAdoConnection()
    rs = "ADODB.Recordset"
    rs.cursorType = 1
    rs.lockType = 1

    sql = "SELECT ISNULL(Shortcut, '?') AS Shortcut FROM SSCommon.STContractors WHERE Id = " + pId
    rs.open(sql, con)

    if rs.EOF then
        wynik = "?"
    else
        wynik = rs.fields("Shortcut").value
    endif
    rs.close()

    PobierzNazweKontrahenta = wynik
endsub
```

## 14.3 Wzorzec — pobranie listy z LibraNet

```basic
int sub PrzetworzListe(String pData)
    Dispatch con, rs
    String sql
    int licznik

    con = createObject("ADODB.Connection")
    con.connectionString = gConnStr
    con.open()

    rs = "ADODB.Recordset"
    rs.cursorType = 1
    rs.lockType = 1

    sql = "SELECT "
    sql = sql + "  CAST(z.Id AS VARCHAR(20)) AS Id, "
    sql = sql + "  CAST(z.KlientId AS VARCHAR(20)) AS KlientId, "
    sql = sql + "  ISNULL(z.Uwagi, '') AS Uwagi "
    sql = sql + "FROM dbo.ZamowieniaMieso z "
    sql = sql + "WHERE z.DataUboju = '" + pData + "' "
    sql = sql + "  AND z.Status IN ('Zrealizowane', 'Wydano') "
    sql = sql + "  AND ISNULL(z.CzyZafakturowane, 0) = 0 "
    sql = sql + "ORDER BY z.Id"
    rs.open(sql, con)

    licznik = 0
    While !rs.EOF
        // ... przetwarzanie wiersza
        licznik = licznik + 1
        rs.moveNext()
    Wend

    rs.close()
    con.close()

    PrzetworzListe = licznik
endsub
```

## 14.4 Wzorzec — UPDATE w LibraNet po sukcesie

```basic
int sub OznaczZafakturowane(String pZamId, String pNrFaktury)
    Dispatch conU
    String sql

    conU = createObject("ADODB.Connection")
    conU.connectionString = gConnStr
    conU.open()

    sql = "UPDATE dbo.ZamowieniaMieso SET "
    sql = sql + "CzyZafakturowane = 1, "
    sql = sql + "NumerFaktury = '" + pNrFaktury + "' "
    sql = sql + "WHERE Id = " + pZamId

    conU.execute(sql)
    conU.close()

    OznaczZafakturowane = 1
endsub
```

## 14.5 Wzorzec — formularz z listą (max N pozycji)

Używaj zmiennych globalnych `gPosX` (1..N) przed `main()` i wypełnij je
w pętli `while`. Ten wzorzec został pokazany w sekcji 4.11 i 13.4.

## 14.6 Wzorzec — bezpieczne wywołanie sub bez wyniku

```basic
int sub MojaProcedura()
    // ... robi coś ...
    MojaProcedura = 1     // zawsze zwracaj coś, nawet "OK = 1"
endsub

// Wywołanie:
int tmp
tmp = MojaProcedura()     // zawsze przypisuj do zmiennej
```

---

# 15. Zatwierdzanie dokumentów i operacje SQL

## 15.1 Zatwierdzanie z poziomu SQL

> **PUŁAPKA:** PZ/RWU są w `HM.MG`, faktury w `HM.DK`!
> Aktualizacja niewłaściwej tabeli to nic nie zrobi.

### Zatwierdzanie dokumentu magazynowego (PZ, WZ, RWU)

```sql
UPDATE HM.MG
SET bufor = 0,
    flag = flag | 1     -- ustaw bit "zatwierdzony"
WHERE id = @idDokumentu
```

### Zatwierdzanie dokumentu handlowego (FVS, FVZ, FVR)

```sql
UPDATE HM.DK
SET bufor = 0,
    flag = flag | 1
WHERE id = @idDokumentu
```

### Z poziomu AMBasic

```basic
sub ZatwierdzPZ(long pId)
    Dispatch con
    String sql

    con = getAdoConnection()
    sql = using "UPDATE HM.MG SET bufor = 0, flag = flag | 1 WHERE id = %d", pId
    con.execute(sql)
    // NIE zamykaj con
endsub

sub ZatwierdzFV(long pId)
    Dispatch con
    String sql

    con = getAdoConnection()
    sql = using "UPDATE HM.DK SET bufor = 0, flag = flag | 1 WHERE id = %d", pId
    con.execute(sql)
endsub
```

> **OSTRZEŻENIE:** Zatwierdzanie przez UPDATE omija wewnętrzne walidacje
> Symfonii (sprawdzenie stanów magazynowych, zaksięgowania, JPK itp.).
> **Stosować TYLKO** gdy dokument jest pewny i kompletnie poprawny.
> Zalecane jest zatwierdzanie ręczne w GUI Symfonii.

## 15.2 Cofnięcie eksportu (rollback)

### W LibraNet — cofnięcie flagi

```sql
-- Cofnięcie eksportu zakupów dla daty
UPDATE dbo.FarmerCalc
SET Symfonia     = 0,
    SymfoniaIdFV = NULL,
    SymfoniaNrFV = NULL
WHERE CalcDate = @data

-- Cofnięcie eksportu sprzedaży dla zamówienia
UPDATE dbo.ZamowieniaMieso
SET CzyZafakturowane = 0,
    NumerFaktury = NULL
WHERE Id = @zamId
```

### W Symfonii — usunięcie z bufora

W Symfonii (GUI): Otwórz dokument → Usuń (działa tylko jeśli `bufor = 1`).

Z SQL — **nie zalecane**, ale możliwe:

```sql
-- Tylko gdy dokument jest w buforze i nie był nigdzie powiązany
DELETE FROM HM.DP WHERE idDk = @id   -- pozycje
DELETE FROM HM.DK WHERE id = @id AND bufor = 1
```

## 15.3 Zapytania diagnostyczne

### Co zostało wyeksportowane danego dnia

```sql
-- Zakupy
SELECT
    fc.CalcDate,
    fc.CustomerGID,
    d.ShortName,
    fc.PayWgt,
    fc.Price,
    fc.Symfonia,
    fc.SymfoniaIdFV,
    fc.SymfoniaNrFV
FROM dbo.FarmerCalc fc
LEFT JOIN dbo.Dostawcy d
    ON LTRIM(RTRIM(fc.CustomerGID)) = d.ID
WHERE fc.CalcDate = '2026-01-20'
ORDER BY d.ShortName, fc.CarLp;

-- Sprzedaż
SELECT
    z.Id,
    z.DataUboju,
    z.KlientId,
    c.Shortcut AS Klient,
    z.Status,
    z.CzyZafakturowane,
    z.NumerFaktury
FROM dbo.ZamowieniaMieso z
LEFT JOIN HANDEL.SSCommon.STContractors c
    ON z.KlientId = c.Id
WHERE z.DataUboju = '2026-01-20'
ORDER BY z.Id;
```

### Brakujące mapowania dostawców

```sql
SELECT DISTINCT
    fc.CustomerGID,
    d.ShortName,
    d.NIP,
    d.IdSymf
FROM dbo.FarmerCalc fc
LEFT JOIN dbo.Dostawcy d
    ON LTRIM(RTRIM(fc.CustomerGID)) = d.ID
WHERE fc.CalcDate = '2026-01-20'
  AND (d.IdSymf IS NULL OR d.IdSymf = 0)
ORDER BY d.ShortName;
```

### Lista nierozliczonych zamówień

```sql
SELECT
    z.Id,
    z.DataUboju,
    z.KlientId,
    c.Shortcut AS Klient,
    z.Status,
    (SELECT SUM(CAST(Ilosc AS FLOAT))
     FROM dbo.ZamowieniaMiesoTowar
     WHERE ZamowienieId = z.Id) AS SumaIlosc
FROM dbo.ZamowieniaMieso z
LEFT JOIN HANDEL.SSCommon.STContractors c ON z.KlientId = c.Id
WHERE z.Status IN ('Zrealizowane', 'Wydano')
  AND ISNULL(z.CzyZafakturowane, 0) = 0
ORDER BY z.DataUboju DESC, z.Id;
```

### Dokumenty w buforze w Symfonii

```sql
-- Dokumenty handlowe w buforze
SELECT
    typ_dk,
    COUNT(*) AS Ilosc,
    MIN(data) AS NajstarszaData
FROM HM.DK
WHERE bufor = 1
  AND aktywny = 1
GROUP BY typ_dk;

-- Dokumenty magazynowe w buforze
SELECT
    typ_dk,
    COUNT(*) AS Ilosc,
    MIN(data) AS NajstarszaData
FROM HM.MG
WHERE bufor = 1
  AND aktywny = 1
GROUP BY typ_dk;
```

### Sprawdzenie ostatnich utworzonych dokumentów

```sql
-- Ostatnie 10 dokumentów handlowych
SELECT TOP 10
    id, kod, typ_dk, data, khid, netto, bufor, opis
FROM HM.DK
ORDER BY id DESC;

-- Ostatnie 10 dokumentów magazynowych
SELECT TOP 10
    id, kod, typ_dk, data, khid, bufor, opis
FROM HM.MG
ORDER BY id DESC;
```

---

# 16. Powiązania dokumentów (PZ ↔ FV)

## 16.1 Stan obecny

Powiązania PZ ↔ FVR/FVZ są przechowywane w schemacie `dr` (NIE `HM`!),
prawdopodobnie w tabeli `dr.DocumentsLinks`. Dokładny schemat tej tabeli
nie został w pełni zmapowany podczas dotychczasowych prac.

## 16.2 Próba przez `PowiazanieHNdoMG`

Funkcja AMBasic `PowiazanieHNdoMG(...)` była eksperymentowana, ale jej
**dokładna sygnatura nie jest znana**:

```basic
// PRÓBA — może działać lub nie, zależnie od wersji Symfonii:
PowiazanieHNdoMG(idDH, idMG, ...)
```

Próby kończyły się komunikatem "nieprawidłowa liczba argumentów".

## 16.3 Praktyczne podejście — ręczne łączenie

Najpewniejsza droga do uzyskania poprawnego powiązania:

1. **W skrypcie**: tworzyć dokumenty w buforze (PZ, FV).
2. **W GUI Symfonii**: operator otwiera PZ → klika "Dokument zakupu" →
   wskazuje powiązaną fakturę → Symfonia tworzy powiązanie wewnętrznie.
3. **Alternatywa**: utworzyć FV PRZEZ PZ — Symfonia ma standardową funkcję
   "Generuj fakturę z PZ" w GUI.

## 16.4 Plan na przyszłość

Aby zautomatyzować powiązanie, potrzeba:
1. Pełnej dokumentacji `dr.DocumentsLinks` (struktura kolumn, klucze).
2. Sprawdzenia, czy `PowiazanieHNdoMG` ma dokumentację w `AMBASHM.HLP`.
3. Ewentualnie — odczytania struktury istniejących powiązań:

```sql
-- Krok 1: znajdź tabele związane z powiązaniami
SELECT TABLE_SCHEMA, TABLE_NAME
FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_NAME LIKE '%Link%'
   OR TABLE_NAME LIKE '%Relac%'
   OR TABLE_NAME LIKE '%Rel%';

-- Krok 2: zbadaj strukturę
SELECT COLUMN_NAME, DATA_TYPE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = 'dr' AND TABLE_NAME = 'DocumentsLinks';

-- Krok 3: zobacz przykładowe rekordy z istniejących powiązanych dokumentów
SELECT TOP 10 * FROM dr.DocumentsLinks;
```

---

# 17. Konwencje i zasady pisania nowych skryptów

## 17.1 Nazewnictwo

- **Prefiks zmiennych globalnych:** `g` (np. `gConnStr`, `gSumaKg`).
- **Prefiks parametrów funkcji:** `p` (np. `pData`, `pIdSymf`).
- **Prefiks zmiennych lokalnych:** `s` (string), `n` (int/long), `f` (float),
  `b` (bool jako int 0/1), `d` (Date).
- **Funkcje:** PascalCase (`PobierzKodTowaru`, `UtworzPZ`).
- **Pliki skryptów:** PascalCase z opisowym sufiksem (`ExportPZLibraNet_v40.bas`,
  `WZ.sc`).

## 17.2 Wersjonowanie

Plik z numerem wersji w nazwie (`_v37`, `_v40`) — pozwala wrócić do
poprzedniej wersji. Po stabilnej wersji utwórz kopię "stable" w innym
katalogu.

## 17.3 Commit checklist (przed wdrożeniem nowej wersji)

- [ ] Wszystkie nowe `IORec` deklarowane wewnątrz funkcji
- [ ] Nie używamy wartości `1` w żadnym `button`
- [ ] Wszystkie `getAdoConnection()` połączenia NIE są zamykane
- [ ] Wszystkie inne połączenia ADO są zamykane (`con.close()`)
- [ ] Wszystkie SQL z `ISNULL(...)` dla pól mogących być NULL
- [ ] Wszystkie pola dat są w formacie `YYYY-MM-DD`
- [ ] Kody towarów dokładnie pasują do `HM.TW.kod` (sprawdź spacje, polskie znaki)
- [ ] Wszystkie dokumenty tworzone w `bufor = "1"`
- [ ] Po sukcesie aktualizujemy flagę w LibraNet
- [ ] Brak hardkodowanych ścieżek systemowych (Windows specific)
- [ ] Komunikat końcowy z liczbą utworzonych dokumentów
- [ ] Test na danych testowych (np. jednym dostawcy / jednym zamówieniu)

## 17.4 Dobre praktyki SQL

### Format zapytań — wieloliniowe składanie

```basic
sql = "SELECT "
sql = sql + "  fc.ID, "
sql = sql + "  fc.CarLp, "
sql = sql + "  fc.CustomerGID, "
sql = sql + "  CAST(COALESCE(fc.PayWgt, fc.NettoFarmWeight, 0) AS VARCHAR(50)) AS Waga "
sql = sql + "FROM dbo.FarmerCalc fc "
sql = sql + "WHERE fc.CalcDate = '" + datap + "' "
sql = sql + "  AND ISNULL(fc.Symfonia, 0) = 0"
```

Czytelne, łatwe do debugowania (można wkleić w SSMS jednym kawałkiem).

### Zawsze CAST stringów dla bezpieczeństwa

```sql
SELECT
    CAST(z.Id AS VARCHAR(20)) AS Id,                  -- int → string
    CAST(z.KlientId AS VARCHAR(20)) AS KlientId,
    ISNULL(z.Uwagi, '') AS Uwagi,                     -- nigdy NULL
    ISNULL(CAST(z.SumaIlosc AS VARCHAR(50)), '0') AS SumaIlosc
FROM dbo.ZamowieniaMieso z
```

### Trim w joinach

```sql
LEFT JOIN dbo.Dostawcy d
    ON LTRIM(RTRIM(fc.CustomerGID)) = LTRIM(RTRIM(d.ID))
```

(W LibraNet w polach VARCHAR często są spacje wiodące/końcowe.)

## 17.5 Dobre praktyki AMBasic

1. **Wszystkie zmienne deklaruj na początku funkcji** — niektóre wersje
   AMBasic wymagają tego (problem z deklaracjami w środku bloku `if`).
2. **Inicjalizuj liczby globalne na 0**, stringi na `""` — nie polegaj
   na automatycznych wartościach.
3. **Każde `if/while/sub` zamykaj odpowiednim `endif/wend/endsub`** —
   brak nawiasów klamrowych, łatwo zgubić poziom zagnieżdżenia.
4. **Komunikaty pokazuj przez `message`** — nie przez `Error`, chyba że
   chcesz przerwać skrypt.
5. **W formularzu pisz tytuły wielkimi literami** — daje wrażenie ważności
   (przykład produkcyjny: `"EKSPORT PZ Z LIBRANET"`).

---

# 18. Diagnostyka i debugging

## 18.1 Strategie debugowania

### 1. Wyświetl wartości w trakcie wykonania

```basic
message using "DEBUG: data=%s, idSymf=%d, waga=%.2f", pData, nIdSymf, fWaga
```

### 2. Etapowy eksport (jeden rekord na raz)

Przed pełnym wdrożeniem zrób wersję "test" — eksportuje tylko pierwszy
znaleziony rekord, pokazuje komunikat z ID utworzonego dokumentu, koniec.

### 3. Sprawdzanie SQL osobno w SSMS

Każde zapytanie SQL ze skryptu wklej najpierw do SSMS i sprawdź czy zwraca
oczekiwane dane.

### 4. Trzy poziomy logowania

```basic
// Poziom 1: wynik główny
message using "Utworzono %d dokumentów", licznik

// Poziom 2: per dostawca (komentowane w produkcji)
// message using "OK: %s, idPZ=%d", sDostawca, idPZ

// Poziom 3: szczegóły pozycji (komentowane w produkcji)
// message using "  Pozycja: kod=%s, ilosc=%s", sKod, sIlosc
```

## 18.2 Częste komunikaty błędów i rozwiązania

| Komunikat                                         | Przyczyna                                | Rozwiązanie                              |
|---------------------------------------------------|------------------------------------------|------------------------------------------|
| `Invalid object name 'HM.DocumentsLinks'`         | Zły schemat                              | Schemat `dr`, nie `HM`                   |
| `Invalid column name 'idd_mg'`                    | Wymyślone nazwy kolumn                   | Sprawdź `INFORMATION_SCHEMA.COLUMNS`     |
| `Login failed for user 'pronova'`                 | Złe hasło / serwer niedostępny           | Sprawdź sieć, hasło, instancję SQL       |
| Skrypt kończy bez efektu                           | Przycisk z wartością `1` lub `Error ""`  | Sprawdź wartości buttonów                |
| Faktura tworzy się pusta                           | Niezmapowany kontrahent                  | Sprawdź `daneKh` / `IdSymf`              |
| Pozycja nie wpada na fakturę                       | Niezgodny `kod` z `HM.TW.kod`            | Sprawdź dokładne brzmienie               |
| `bufor = 0`, ale dokument nadal w buforze GUI      | Update niewłaściwej tabeli (DK vs MG)    | Dla PZ/RWU używaj `HM.MG`                |
| Złe daty na fakturze                               | Brak `dataDokumentuObcego`/`dataZakupu`  | Dodaj te pola w `setField`               |

## 18.3 Sprawdzenie czy dokument powstał

```sql
-- Najpierw sprawdź obie tabele
SELECT 'HM.DK' AS Tabela, id, kod, typ_dk, bufor, data, opis
FROM HM.DK
WHERE id = @sprawdzaneId
UNION ALL
SELECT 'HM.MG' AS Tabela, id, kod, typ_dk, bufor, data, opis
FROM HM.MG
WHERE id = @sprawdzaneId;
```

## 18.4 Test na nowym kontrahencie / towarze

Przed pełnym wdrożeniem dodaj jednego testowego kontrahenta i jeden
testowy towar do Symfonii (np. "TEST_KURCZAK"), sprawdź na nim cały
workflow, dopiero potem mapuj prawdziwych dostawców.

---

# 19. Słowniki i kody

## 19.1 Typy dokumentów (pole `typ_dk`)

| Skrót  | Nazwa                                          | Tabela    | Funkcja importu   |
|--------|------------------------------------------------|-----------|-------------------|
| `PZ`   | Przyjęcie zewnętrzne                           | `HM.MG`   | `importMg()`      |
| `WZ`   | Wydanie zewnętrzne                             | `HM.MG`   | `importMg()`      |
| `PW`   | Przyjęcie wewnętrzne                           | `HM.MG`   | `importMg()`      |
| `RW`   | Rozchód wewnętrzny                             | `HM.MG`   | `importMg()`      |
| `RWU`  | Rozchód wewnętrzny — usługa                    | `HM.MG`   | `importMg()`      |
| `MM`   | Przesunięcie międzymagazynowe                  | `HM.MG`   | `importMg()`      |
| `FVZ`  | Faktura VAT zakupu                             | `HM.DK`   | `ImportZK(dok)`   |
| `FVR`  | Faktura VAT RR (rolnik ryczałtowy)             | `HM.DK`   | `ImportZK(dok)`   |
| `FVRR` | Faktura VAT RR (alternatywne oznaczenie)       | `HM.DK`   | `ImportZK(dok)`   |
| `KFZ`  | Korekta faktury zakupu                         | `HM.DK`   | `ImportZK(dok)`   |
| `FVS`  | Faktura VAT sprzedaży                          | `HM.DK`   | `ImportSP(dok)`   |
| `FW`   | Faktura wewnętrzna                             | `HM.DK`   | `ImportSP(dok)`   |
| `PAR`  | Paragon                                        | `HM.DK`   | `ImportSP(dok)`   |
| `KFS`  | Korekta faktury sprzedaży                      | `HM.DK`   | `ImportSP(dok)`   |
| `FKS`  | Faktura korygująca sprzedażowa (alt.)          | `HM.DK`   | `ImportSP(dok)`   |
| `FKSB` | Faktura korygująca sprzedażowa do bufora       | `HM.DK`   | `ImportSP(dok)`   |

## 19.2 Statusy zamówień (LibraNet)

| Status            | Kolor UI       | Opis                            |
|-------------------|----------------|---------------------------------|
| `Nowe`            | Żółty `#FFF8E1`| Nowe zamówienie                 |
| `W realizacji`    | Niebieski `#E3F2FD` | W trakcie kompletacji      |
| `Zrealizowane`    | Zielony `#E8F5E9` | Gotowe do wydania            |
| `Wydano`          | Niebieski `#E1F5FE` | Wydane z magazynu          |
| `Anulowane`       | (ukryte)       | Anulowane                       |

Eksport do FVS: status `Zrealizowane` LUB `Wydano`.

## 19.3 Serie numeracji (przykładowe)

| Seria       | Typ dokumentu           |
|-------------|-------------------------|
| `sPZ`       | PZ                      |
| `sWZ`       | WZ                      |
| `sFVR`      | FVR                     |
| `sFVZ`      | FVZ                     |
| `sFVS`      | FVS                     |
| `sRWU`      | RWU                     |

> Serie mogą być dostosowane w konkretnej instalacji Symfonii — sprawdź
> w GUI: Ustawienia → Numeracja dokumentów.

## 19.4 Kategorie towarów (znane wartości `HM.TW.katalog`)

| `katalog` | Znaczenie       |
|-----------|-----------------|
| `67095`   | Świeże          |
| `67153`   | Mrożone         |

(Inne — do uzupełnienia w czasie pracy.)

## 19.5 Kody towarów drobiarskich (przykładowe)

| Kod (HM.TW.kod)    | Użycie                                               |
|--------------------|------------------------------------------------------|
| `Kurczak żywy -7`  | Rolnik ryczałtowy (FVR), VAT 7%                       |
| `Kurczak żywy -8`  | Vatowiec (FVZ), VAT 8% (lub PZ + RWU)                |
| `Kurczak A`        | Produkt sprzedażowy (FVS) — kategoria świeże          |

> **Sprawdzaj zawsze przed kodowaniem!** Brzmienie pola `kod` jest wrażliwe
> na spacje, polskie znaki i wielkość liter.

## 19.6 Magazyny / działy (znane wartości pola `dzial`)

| `dzial`     | Znaczenie               |
|-------------|-------------------------|
| `M. PROD`   | Magazyn produkcyjny     |

(Inne — do uzupełnienia.)

## 19.7 Format dat

W `setField` zawsze format ISO 8601: `YYYY-MM-DD` (string).
W SQL: `'YYYY-MM-DD'` lub `'YYYY-MM-DD HH:MM:SS'` (datetime).

## 19.8 Format liczb

W `setField` zawsze string z **kropką dziesiętną**: `"4.50"`, `"1234.56"`.
W LibraNet `dbo.ZamowieniaMiesoTowar.Cena` może być `"4,50"` (przecinek) —
trzeba konwertować w SQL przez `REPLACE(Cena, ',', '.')`.

---

# DODATEK A: Najczęściej używane fragmenty kodu (cheat sheet)

## A.1 Otwarcie połączenia LibraNet

```basic
String gConnStr
gConnStr = "Provider=SQLOLEDB;Data Source=192.168.0.109;Initial Catalog=LibraNet;User ID=pronova;Password=pronova"

Dispatch con
con = createObject("ADODB.Connection")
con.connectionString = gConnStr
con.open()
// ... użycie ...
con.close()
```

## A.2 Otwarcie Recordset

```basic
Dispatch rs
rs = "ADODB.Recordset"
rs.cursorType = 1
rs.lockType = 1
rs.open(sql, con)

While !rs.EOF
    String s = rs.fields("kolumna").value
    rs.moveNext()
Wend
rs.close()
```

## A.3 Wzór formularza wejściowego

```basic
String datap
int wynik
form "Tytuł", 450, 150
    ground 80, 200, 200
    Datedit "Data:", datap, 100, 20, 200, 22
    button "&OK", 50, 90, 130, 35, 2
    button "&Anuluj", 200, 90, 130, 35, -1
wynik = ExecForm
if wynik < 1 then
    Error ""
endif
```

## A.4 Tworzenie PZ — minimalna wersja

```basic
IORec dokPz
long idMg
Date termin
termin.today()
termin.addDays(35)

dokPz.setField("typDk", "PZ")
dokPz.setField("seria", "sPZ")
dokPz.setField("dataWystawienia", datap)
dokPz.setField("dataOperacji", datap)
dokPz.setField("termin", termin.toStr())
dokPz.setField("dzial", "M. PROD")
dokPz.setField("bufor", "1")
dokPz.setField("opis", "Test")

dokPz.beginSection("daneKh")
    dokPz.setField("khId", "5209")
dokPz.endSection()

dokPz.beginSection("Pozycja dokumentu")
    dokPz.setField("kod", "Kurczak żywy -8")
    dokPz.setField("ilosc", "100.00")
    dokPz.setField("cena", "4.50")
dokPz.endSection()

idMg = dokPz.importMg()
message using "ID utworzonego PZ: %d", idMg
```

## A.5 Tworzenie FVS — minimalna wersja

```basic
IORec dokFvs
long idSp

dokFvs.setField("typDk", "FVS")
dokFvs.setField("seria", "sFVS")
dokFvs.setField("dataWystawienia", datap)
dokFvs.setField("dataOperacji", datap)
dokFvs.setField("bufor", "1")

dokFvs.beginSection("daneKh")
    dokFvs.setField("khId", "939")
dokFvs.endSection()

dokFvs.beginSection("Pozycja dokumentu")
    dokFvs.setField("kod", "Kurczak A")
    dokFvs.setField("ilosc", "1000.00")
    dokFvs.setField("cena", "8.50")
dokFvs.endSection()

idSp = ImportSP(dokFvs)
message using "ID utworzonej FVS: %d", idSp
```

## A.6 UPDATE w LibraNet po sukcesie eksportu

```basic
Dispatch conU
String sql
conU = createObject("ADODB.Connection")
conU.connectionString = gConnStr
conU.open()

sql = "UPDATE dbo.FarmerCalc SET Symfonia = 1, "
sql = sql + "SymfoniaIdFV = " + using "%d", idFV + ", "
sql = sql + "SymfoniaNrFV = '" + sNrFV + "' "
sql = sql + "WHERE CalcDate = '" + datap + "' "
sql = sql + "  AND LTRIM(RTRIM(CustomerGID)) = '" + sCustomerGID + "'"

conU.execute(sql)
conU.close()
```

---

# DODATEK B: Lista plików produkcyjnych

| Plik                                | Cel                                       | Status         |
|-------------------------------------|-------------------------------------------|----------------|
| `ExportPZLibraNet_v37.bas`          | Eksport zakupów (PZ + FVR/FVZ + RWU)      | Stabilny       |
| `ExportPZLibraNet_v40.bas`          | Wersja z eksperymentem powiązań           | Eksperymentalny|
| `WZ.sc`                             | Eksport sprzedaży (FVS, opcjonalnie WZ)   | Stabilny       |

---

# DODATEK C: Pomysły rozwojowe (backlog)

1. **Pole `TypPodatnika` w `dbo.Dostawcy`** — automatyczny wybór FVR/FVZ
   bez pytania operatora.
2. **Tryb "wsadowy bez dialogów"** — eksport całego dnia jednym kliknięciem.
3. **Walidacja przed eksportem** — sprawdzenie problemów (brak mapowania,
   pusta cena, ujemna waga) przed startem.
4. **Tabela `dbo.SymfoniaExportLog`** — audyt eksportów (kto, kiedy, ile).
5. **Eksport zakresu dat** — od-do zamiast jednego dnia.
6. **Numer obcy automatyczny** — `LN/2026-01-20/773`.
7. **Cofanie eksportu jednym przyciskiem** — reset flagi + usunięcie z bufora.
8. **Powiadomienia email** — raport po zakończeniu eksportu.
9. **Pełna automatyzacja powiązań PZ ↔ FV** — po zmapowaniu `dr.DocumentsLinks`.
10. **Dashboard w C# (Kalendarz1 ZPSP)** — status eksportu na dany dzień.

---

# DODATEK D: Linki i odnośniki

- Plik pomocy AMBasic w instalacji Symfonii: `AMBASHM.HLP`
  (zwykle w katalogu instalacji Symfonii). Zawiera szczegółową dokumentację
  funkcji, których brakuje w internecie.
- Online: https://pomoc.symfonia.pl/data/ambasic/ (czasami niedostępne).

---

*Dokumentacja przygotowana w oparciu o doświadczenie z projektu integracji
LibraNet ↔ Symfonia Handel. Aktualizowana wraz z nowymi odkryciami.*

*Format: Markdown — przeznaczony do pracy z Claude Code i innymi narzędziami
deweloperskimi obsługującymi MD.*
