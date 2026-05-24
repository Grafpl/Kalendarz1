# Migracja istniejących umów z Excela do tabeli Kontrakty

> Faza M (Część 4 audytu). Asia inwentaryzuje papierowe umowy do Excela, Ser importuje do bazy. **~2 dni Asi + 4h Magdy + 4h Sera.**

---

## Krok 1 — Asia tworzy Excel inwentaryzacji

Plik: `\\192.168.0.170\Install\UmowyZakupu\_MIGRACJA\Inwentaryzacja_umow.xlsx`

**Wymagane kolumny (dokładnie te nagłówki w wierszu 1):**

| Kolumna | Typ | Przykład | Uwagi |
|---|---|---|---|
| `DostawcaId` | liczba | 12345 | ID z DOSTAWCY (Asia wyszukuje w ZPSP) |
| `NazwaHodowcy` | tekst | Jan Kowalski | |
| `Nip` | tekst | 1234567890 | może być puste |
| `NrGospodarstwa` | tekst | PL12345678 | może być puste |
| `Adres` | tekst | ul. Wiejska 12 | może być puste |
| `TypKontraktu` | tekst | ARIMR_3LAT | ARIMR_3LAT / ROCZNY / WIECZNY / SPOT |
| `DataOd` | data | 2024-01-01 | format YYYY-MM-DD |
| `DataDo` | data | 2027-01-01 | puste = wieczny |
| `ProcentUbytku` | liczba | 3.00 | |
| `TypCeny` | tekst | wolnorynkowa | wolnorynkowa / rolnicza / ministerialna / laczona |
| `Cena` | liczba | 7.50 | puste = cennik dnia |
| `TerminPlatnosciDni` | liczba | 21 | |
| `LiczySieDoArimr` | 1/0 | 1 | 1 = pod dotację |
| `Status` | tekst | ACTIVE | zwykle ACTIVE dla istniejących |
| `SciezkaPdfSkan` | tekst | \\...\skan.pdf | jeśli skan już jest |

**Tip dla Asi:** zacznij od segregatorów, jeden wiersz = jedna umowa. Nie wszystko na raz — 20-30 dziennie.

---

## Krok 2 — Ser konwertuje Excel → CSV

W Excelu: **Plik → Zapisz jako → CSV UTF-8 (rozdzielany przecinkami)**.
Zapisz jako: `Inwentaryzacja_umow.csv`.

---

## Krok 3 — Ser uruchamia import

**Opcja A — przez kod C#** (`MigracjaKontraktowImport.cs` — patrz obok).

**Opcja B — przez czysty SQL** (gdy Excel mały, < 50 wierszy):
```sql
-- Dla każdego wiersza Excela, Asia/Ser wkleja:
DECLARE @num VARCHAR(20), @lp INT;
EXEC dbo.sp_KontraktyNastepnyNumer @Rok = 2024, @NumerOut = @num OUTPUT, @LpOut = @lp OUTPUT;

INSERT INTO dbo.Kontrakty
(NumerKontraktu, Rok, LpRoku, DostawcaId, TypKontraktu, Status,
 DataObowiazujeOd, DataObowiazujeDo, ProcentUbytku, TypCeny, Cena,
 TerminPlatnosciDni, NazwaHodowcySnapshot, NipSnapshot, NrGospodarstwaSnapshot,
 AdresSnapshot, LiczySieDoArimr, PartiaPiorkowscy, UtworzylUserId, SciezkaPdfSkan)
VALUES
(@num, 2024, @lp, 12345, 'ARIMR_3LAT', 'ACTIVE',
 '2024-01-01', '2027-01-01', 3.00, 'wolnorynkowa', 7.50,
 21, 'Jan Kowalski', '1234567890', 'PL12345678',
 'ul. Wiejska 12', 1, 'PIORKOWSCY', 'migracja', '\\...\skan.pdf');
```

---

## Krok 4 — Asia weryfikuje

1. Otwórz ZPSP → **Kontrakty Hodowców**
2. Sprawdź czy wszystkie wiersze się pojawiły
3. Zmień status na `EXPIRED` dla tych które już wygasły
4. Dla brakujących skanów → "📎 Dodaj skan" gdy zeskanujesz papier

---

## ⚠️ Uwagi

- **DostawcaId musi istnieć w DOSTAWCY** — FK constraint. Jeśli hodowcy nie ma w bazie, najpierw go załóż.
- **Rok numeru = rok DataOd** — np. umowa z 2024 dostanie numer `1/24`, `2/24`...
- **Numeracja migracyjna może być myląca** — rozważ osobny prefiks dla migrowanych (np. `M1/24`). Patrz kod C# obok.
- **Backup przed importem**: `BACKUP DATABASE LibraNet TO DISK = '...'` lub przynajmniej `SELECT * INTO Kontrakty_PRZED_MIGRACJA FROM Kontrakty;`
