-- ═══════════════════════════════════════════════════════════════════════════
-- Faza 0.7 — CHECK constraint na TransportPL.Kurs.Status
-- ═══════════════════════════════════════════════════════════════════════════
-- Domena (zgodna z Shared/Domain/KursStatus.cs):
--   Planowany, Akceptowany, WTrasie, Zakonczony, Anulowany
--
-- Uruchom w SSMS na bazie TransportPL (192.168.0.109).
-- Wykonuj BLOK PO BLOKU — sprawdź rezultat po każdym.
-- ═══════════════════════════════════════════════════════════════════════════

USE TransportPL;
GO

-- ────────────────────────────────────────────────────────────
-- KROK 1: Rozpoznanie — co siedzi w tabeli?
-- ────────────────────────────────────────────────────────────
-- Spodziewane wartości: Planowany. Możliwe historyczne rozjazdy:
--   "W trasie" (ze spacją), "Zakończony" (z ogonkiem), pusty string, NULL.
SELECT
    ISNULL(Status, '(NULL)') AS Status,
    COUNT(*)                 AS Liczba
FROM Kurs
GROUP BY Status
ORDER BY COUNT(*) DESC;
GO

-- ────────────────────────────────────────────────────────────
-- KROK 2: Normalizacja — uruchom TYLKO jeśli krok 1 pokazał odstępstwa
-- ────────────────────────────────────────────────────────────
-- Mapowanie znanych wariantów na kanoniczne wartości.
-- Każdy UPDATE niezależny — można uruchamiać selektywnie.

-- UPDATE Kurs SET Status = 'WTrasie'     WHERE Status IN ('W trasie', 'W Trasie', 'w trasie');
-- UPDATE Kurs SET Status = 'Zakonczony'  WHERE Status IN ('Zakończony', 'zakonczony', 'Zakończono');
-- UPDATE Kurs SET Status = 'Planowany'   WHERE Status IS NULL OR LTRIM(RTRIM(Status)) = '';
-- UPDATE Kurs SET Status = 'Anulowany'   WHERE Status IN ('anulowany', 'Anulowane');

-- Po UPDATE zweryfikuj ponownie:
-- SELECT DISTINCT Status FROM Kurs;
GO

-- ────────────────────────────────────────────────────────────
-- KROK 3: Dodanie CHECK constraint
-- ────────────────────────────────────────────────────────────
-- Uruchom dopiero gdy SELECT DISTINCT Status FROM Kurs zwraca TYLKO 5 dozwolonych wartości.

IF NOT EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE name = 'CK_Kurs_Status'
      AND parent_object_id = OBJECT_ID('dbo.Kurs')
)
BEGIN
    ALTER TABLE Kurs WITH CHECK
    ADD CONSTRAINT CK_Kurs_Status CHECK (
        Status IN ('Planowany', 'Akceptowany', 'WTrasie', 'Zakonczony', 'Anulowany')
    );
    PRINT 'CK_Kurs_Status dodany pomyślnie.';
END
ELSE
BEGIN
    PRINT 'CK_Kurs_Status już istnieje — pomijam.';
END
GO

-- ────────────────────────────────────────────────────────────
-- ROLLBACK (gdyby trzeba było cofnąć):
-- ────────────────────────────────────────────────────────────
-- ALTER TABLE Kurs DROP CONSTRAINT CK_Kurs_Status;
