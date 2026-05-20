-- ═══════════════════════════════════════════════════════════════════════════
-- CLEANUP: usun rekordy zaimportowane z LibraNet do TransportPL
-- ═══════════════════════════════════════════════════════════════════════════
-- Kontekst: wczesniejszy auto-backfill (juz usuniety z kodu) INSERTowal
-- LibraNet.Driver -> TransportPL.Kierowca i LibraNet.CarTrailer -> TransportPL.Pojazd.
-- Powstaly DUPLIKATY oraz pojazdy/kierowcy ktorzy nigdy nie byli w TransportPL.
--
-- Ten skrypt usuwa WSZYSTKIE rekordy zbackfillowane (oznaczone LibraNet*ID NOT NULL).
-- Pozostawia tylko ORYGINALNE rekordy TransportPL (LibraNet*ID IS NULL) — te same
-- ktore widzi edytor kursu.
--
-- Uruchom raz w SSMS na bazie TransportPL (192.168.0.109).
-- Skrypt jest IDEMPOTENTNY — kazde uruchomienie wykonuje tylko delete brakujacych.
-- ═══════════════════════════════════════════════════════════════════════════

USE TransportPL;
GO

-- ────────────────────────────────────────────────────────────
-- KROK 1: Inwentarz — co jest do usuniecia
-- ────────────────────────────────────────────────────────────
PRINT '═══ Inwentarz PRZED czyszczeniem ═══';

SELECT 'Kierowcy' AS Co,
       COUNT(*) AS Wszystkich,
       SUM(CASE WHEN LibraNetDriverGID IS NULL THEN 1 ELSE 0 END) AS Oryginalne_TransportPL,
       SUM(CASE WHEN LibraNetDriverGID IS NOT NULL THEN 1 ELSE 0 END) AS Zbackfillowane_DO_USUNIECIA
FROM dbo.Kierowca
UNION ALL
SELECT 'Pojazdy',
       COUNT(*),
       SUM(CASE WHEN LibraNetCarTrailerID IS NULL THEN 1 ELSE 0 END),
       SUM(CASE WHEN LibraNetCarTrailerID IS NOT NULL THEN 1 ELSE 0 END)
FROM dbo.Pojazd;

PRINT '';
PRINT 'Lista kierowcow do usuniecia:';
SELECT TOP 100 KierowcaID, Imie, Nazwisko, Telefon, Aktywny, LibraNetDriverGID
FROM dbo.Kierowca
WHERE LibraNetDriverGID IS NOT NULL
ORDER BY Nazwisko, Imie;

PRINT '';
PRINT 'Lista pojazdow do usuniecia:';
SELECT TOP 100 PojazdID, Rejestracja, Marka, Model, Aktywny, LibraNetCarTrailerID
FROM dbo.Pojazd
WHERE LibraNetCarTrailerID IS NOT NULL
ORDER BY Rejestracja;
GO

-- ────────────────────────────────────────────────────────────
-- KROK 2: Sprawdz integralnosc — czy te rekordy nie sa uzywane w kursach
-- ────────────────────────────────────────────────────────────
PRINT '';
PRINT '═══ Czy zbackfillowane Kierowcy/Pojazdy sa uzywane w kursach? ═══';

-- Sprawdz Kurs.KierowcaID i Kurs.PojazdID (lub jak sie nazywa pole)
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Kurs') AND name = 'KierowcaID')
BEGIN
    SELECT 'Kursy z zbackfillowanym kierowca' AS Co, COUNT(*) AS Liczba
    FROM dbo.Kurs k
    INNER JOIN dbo.Kierowca dr ON k.KierowcaID = dr.KierowcaID
    WHERE dr.LibraNetDriverGID IS NOT NULL;
END

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Kurs') AND name = 'PojazdID')
BEGIN
    SELECT 'Kursy z zbackfillowanym pojazdem' AS Co, COUNT(*) AS Liczba
    FROM dbo.Kurs k
    INNER JOIN dbo.Pojazd p ON k.PojazdID = p.PojazdID
    WHERE p.LibraNetCarTrailerID IS NOT NULL;
END
GO

-- ────────────────────────────────────────────────────────────
-- KROK 3: USUNIECIE — zakomentuj jesli chcesz tylko podglad
-- ────────────────────────────────────────────────────────────
-- UWAGA: usuwa rekordy z LibraNet*ID NOT NULL.
-- Jesli kursy uzywaja tych rekordow — odkomentuj NULL'owanie ponizej najpierw.
-- ────────────────────────────────────────────────────────────

BEGIN TRANSACTION;

-- Opcja A (bezpieczna): zamiast DELETE, oznacz Aktywny=0 (ukryje w UI ktore filtruje WHERE Aktywny=1)
-- UPDATE dbo.Kierowca SET Aktywny = 0 WHERE LibraNetDriverGID IS NOT NULL;
-- UPDATE dbo.Pojazd   SET Aktywny = 0 WHERE LibraNetCarTrailerID IS NOT NULL;

-- Opcja B (radykalna): usun fizycznie. Wymaga ze zadne FK nie referencuja.
-- Najpierw NULL'uj FK w Kurs (jesli istnieja):
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Kurs') AND name = 'KierowcaID')
BEGIN
    UPDATE dbo.Kurs SET KierowcaID = NULL
    WHERE KierowcaID IN (SELECT KierowcaID FROM dbo.Kierowca WHERE LibraNetDriverGID IS NOT NULL);
    PRINT 'NULL-owano Kurs.KierowcaID dla zbackfillowanych kierowcow.';
END

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Kurs') AND name = 'PojazdID')
BEGIN
    UPDATE dbo.Kurs SET PojazdID = NULL
    WHERE PojazdID IN (SELECT PojazdID FROM dbo.Pojazd WHERE LibraNetCarTrailerID IS NOT NULL);
    PRINT 'NULL-owano Kurs.PojazdID dla zbackfillowanych pojazdow.';
END

-- DELETE rekordow
DELETE FROM dbo.Kierowca WHERE LibraNetDriverGID IS NOT NULL;
PRINT CONCAT('Usunieto kierowcow zbackfillowanych: ', @@ROWCOUNT);

DELETE FROM dbo.Pojazd WHERE LibraNetCarTrailerID IS NOT NULL;
PRINT CONCAT('Usunieto pojazdow zbackfillowanych: ', @@ROWCOUNT);

COMMIT;
GO

-- ────────────────────────────────────────────────────────────
-- KROK 4: Inwentarz po
-- ────────────────────────────────────────────────────────────
PRINT '';
PRINT '═══ Inwentarz PO czyszczeniu ═══';

SELECT 'Kierowcy' AS Co,
       COUNT(*) AS Wszystkich,
       SUM(CASE WHEN LibraNetDriverGID IS NULL THEN 1 ELSE 0 END) AS Oryginalne_TransportPL,
       SUM(CASE WHEN LibraNetDriverGID IS NOT NULL THEN 1 ELSE 0 END) AS Zbackfillowane
FROM dbo.Kierowca
UNION ALL
SELECT 'Pojazdy',
       COUNT(*),
       SUM(CASE WHEN LibraNetCarTrailerID IS NULL THEN 1 ELSE 0 END),
       SUM(CASE WHEN LibraNetCarTrailerID IS NOT NULL THEN 1 ELSE 0 END)
FROM dbo.Pojazd;
GO

-- ────────────────────────────────────────────────────────────
-- ROLLBACK (jesli cos poszlo zle)
-- ────────────────────────────────────────────────────────────
-- W razie potrzeby restore z backup'u. SQL Server NIE ma 'undo delete'.
-- PRZED uruchomieniem skryptu zrob backup: BACKUP DATABASE TransportPL TO DISK = '...'
