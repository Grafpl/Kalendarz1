-- ============================================================================
-- MEGA DIAGNOZA: HarmonogramDostaw + FarmerCalc + Matryca dla wybranego dnia
-- Pokazuje WSZYSTKIE problemy w lancuchu danych + sugerowane SQL'e naprawcze
--
-- Sposob uzycia:
--   1. Zmien @Data ponizej na dzien ktory chcesz zdiagnozowac
--   2. Wykonaj caly skrypt w SSMS
--   3. Kazda sekcja zwraca osobny result-set z opisem problemu
--   4. Sekcje [REPAIR] sa zakomentowane - odkomentuj i wykonaj jak chcesz naprawic
-- ============================================================================

DECLARE @Data DATE = '2026-06-03';   -- ZMIEN NA DZIEN DIAGNOZY

PRINT '=========================================================================';
PRINT '=== MEGA DIAGNOZA dnia ' + CONVERT(VARCHAR(10), @Data, 120);
PRINT '=========================================================================';
PRINT '';

-- ============================================================================
-- SEKCJA 1: OGOLNE LICZBY (PODSUMOWANIE WSTEPNE)
-- Pokazuje: ile harmonogramow, ile potwierdzonych, ile aut wjechalo
-- ============================================================================
PRINT '--- 1. PODSUMOWANIE WSTEPNE ---';
SELECT
    (SELECT COUNT(*) FROM dbo.HarmonogramDostaw WHERE CAST(DataOdbioru AS DATE) = @Data) AS Harmonogramow_wszystkich,
    (SELECT COUNT(*) FROM dbo.HarmonogramDostaw WHERE CAST(DataOdbioru AS DATE) = @Data AND PotwWaga = 1) AS Potwierdzonych_przez_Asie,
    (SELECT COUNT(*) FROM dbo.HarmonogramDostaw WHERE CAST(DataOdbioru AS DATE) = @Data AND ISNULL(PotwWaga,0) = 0) AS Niepotwierdzonych,
    (SELECT COUNT(*) FROM dbo.HarmonogramDostaw WHERE CAST(DataOdbioru AS DATE) = @Data AND ISNULL(TRY_CAST(Auta AS INT),0) = 0) AS Bez_Aut_planowanych,
    (SELECT COUNT(*) FROM dbo.FarmerCalc WHERE CalcDate = @Data AND ISNULL(Deleted,0) = 0) AS Aut_wjechalo,
    (SELECT COUNT(DISTINCT LpDostawy) FROM dbo.FarmerCalc WHERE CalcDate = @Data AND ISNULL(Deleted,0) = 0 AND LpDostawy IS NOT NULL) AS Unikalnych_LP_w_FarmerCalc,
    (SELECT RecordCount FROM dbo.MatrycaTransferLog WHERE CalcDate = @Data ORDER BY TransferDate DESC OFFSET 0 ROWS FETCH NEXT 1 ROWS ONLY) AS Ostatni_import_z_AVILOG;

-- ============================================================================
-- SEKCJA 2: DUCHY - harmonogramy stare, niepotwierdzone, bez aut
-- FIX: grupujemy po UPPER(Dostawca) bo DostawcaID czesto jest NULL.
-- Kryterium: PotwWaga IS NULL/0 AND 0 aut wjechalo AND istnieje INNY harmonogram tego hodowcy ktory ma auta lub PotwWaga
-- Te wpisy MOZNA USUNAC bez konsekwencji - to placeholdery zostawione po zmianie planu
-- ============================================================================
PRINT '';
PRINT '--- 2. DUCHY (do usuniecia - bez wplywu na realizacje) ---';
;WITH FCcounts AS (
    SELECT LpDostawy, COUNT(*) AS Cnt
    FROM dbo.FarmerCalc WHERE CalcDate = @Data AND ISNULL(Deleted,0)=0 AND LpDostawy IS NOT NULL
    GROUP BY LpDostawy
),
AktywnePerHodowca AS (
    SELECT UPPER(LTRIM(RTRIM(hd.Dostawca))) AS NazwaKey,
           COUNT(*) AS LiczbaAktywnych
    FROM dbo.HarmonogramDostaw hd
    LEFT JOIN FCcounts fc ON fc.LpDostawy = hd.Lp
    WHERE CAST(hd.DataOdbioru AS DATE) = @Data
      AND (ISNULL(hd.PotwWaga, 0) = 1 OR ISNULL(fc.Cnt, 0) > 0)
    GROUP BY UPPER(LTRIM(RTRIM(hd.Dostawca)))
)
SELECT hd.Lp, hd.Dostawca,
       ISNULL(TRY_CAST(hd.Auta AS INT), 0) AS Plan_Aut,
       ISNULL(TRY_CAST(hd.SztukiDek AS INT), 0) AS Plan_Sztuk,
       hd.PotwWaga, hd.PotwSztuki,
       CONVERT(VARCHAR(16), hd.DataUtw, 120) AS Utworzone,
       CONVERT(VARCHAR(16), hd.DataMod, 120) AS Modyfikowane,
       ISNULL(fc.Cnt, 0) AS AutaReal,
       'DELETE FROM dbo.HarmonogramDostaw WHERE Lp = ' + CAST(hd.Lp AS VARCHAR) + ';' AS REPAIR_SQL
FROM dbo.HarmonogramDostaw hd
INNER JOIN AktywnePerHodowca ap ON ap.NazwaKey = UPPER(LTRIM(RTRIM(hd.Dostawca)))
LEFT JOIN FCcounts fc ON fc.LpDostawy = hd.Lp
WHERE CAST(hd.DataOdbioru AS DATE) = @Data
  AND ISNULL(hd.PotwWaga, 0) = 0
  AND ISNULL(fc.Cnt, 0) = 0
  AND ap.LiczbaAktywnych >= 1   -- ten hodowca ma INNY (aktywny) wpis tego dnia → ten jest duchem
ORDER BY hd.Dostawca, hd.Lp;

-- ============================================================================
-- SEKCJA 3: HODOWCY Z DUPLIKATAMI (2+ wpisow tego samego hodowcy)
-- ============================================================================
PRINT '';
PRINT '--- 3. HODOWCY Z DUPLIKATAMI W HARMONOGRAMIE ---';
SELECT hd.Dostawca, hd.DostawcaID, COUNT(*) AS Wpisow,
       STRING_AGG(CAST(hd.Lp AS VARCHAR(10)) + '(' +
           CASE WHEN hd.PotwWaga=1 THEN '✓' ELSE '✗' END + ',plan=' +
           CAST(ISNULL(TRY_CAST(hd.Auta AS INT),0) AS VARCHAR) + 'aut)', ' | ') AS Wpisy
FROM dbo.HarmonogramDostaw hd
WHERE CAST(hd.DataOdbioru AS DATE) = @Data
GROUP BY hd.Dostawca, hd.DostawcaID
HAVING COUNT(*) > 1
ORDER BY Wpisow DESC, hd.Dostawca;

-- ============================================================================
-- SEKCJA 4: AUTA-SIEROTY (FarmerCalc bez powiazania do harmonogramu dnia)
-- LpDostawy w FarmerCalc nie istnieje w HarmonogramDostaw dla tego dnia
-- Mozliwosc: dispatcher wpisal zly LP, lub harmonogram zostal usuniety
-- ============================================================================
PRINT '';
PRINT '--- 4. AUTA-SIEROTY (FarmerCalc bez harmonogramu dnia) ---';
SELECT fc.ID, fc.CarLp AS Nr, fc.LpDostawy, RTRIM(fc.CustomerGID) AS GID,
       d.Name AS Hodowca_w_FarmerCalc,
       fc.NettoWeight AS Netto,
       hd.Dostawca AS Hodowca_z_LP,
       CAST(hd.DataOdbioru AS DATE) AS DataHarmonogramu,
       'UPDATE dbo.FarmerCalc SET LpDostawy = NULL WHERE ID = ' + CAST(fc.ID AS VARCHAR) + ';  -- albo zmien na poprawny LP' AS REPAIR_SQL
FROM dbo.FarmerCalc fc
LEFT JOIN dbo.Dostawcy d ON LTRIM(RTRIM(d.ID)) = LTRIM(RTRIM(fc.CustomerGID))
LEFT JOIN dbo.HarmonogramDostaw hd ON hd.Lp = fc.LpDostawy
WHERE fc.CalcDate = @Data AND ISNULL(fc.Deleted,0) = 0
  AND fc.LpDostawy IS NOT NULL
  AND (hd.Lp IS NULL OR CAST(hd.DataOdbioru AS DATE) <> @Data)
ORDER BY fc.CarLp;

-- ============================================================================
-- SEKCJA 5: AUTA BEZ PRZYPISANIA DO HARMONOGRAMU (LpDostawy = NULL)
-- ============================================================================
PRINT '';
PRINT '--- 5. AUTA BEZ LpDostawy (w ogole nieprzypisane) ---';
SELECT fc.ID, fc.CarLp, RTRIM(fc.CustomerGID) AS GID, d.Name AS Hodowca,
       fc.FullWeight AS Brutto, fc.NettoWeight AS Netto,
       CONVERT(VARCHAR(16), fc.Przyjazd, 120) AS Przyjazd
FROM dbo.FarmerCalc fc
LEFT JOIN dbo.Dostawcy d ON LTRIM(RTRIM(d.ID)) = LTRIM(RTRIM(fc.CustomerGID))
WHERE fc.CalcDate = @Data AND ISNULL(fc.Deleted,0) = 0
  AND fc.LpDostawy IS NULL;

-- ============================================================================
-- SEKCJA 6: NIEZGODNOSCI HODOWCA W FarmerCalc vs HarmonogramDostaw
-- Auto z LpDostawy=X gdzie hodowca w Dostawcy != hodowca w HarmonogramDostaw
-- Klasyczna sytuacja Lapiak: auta jako Piotr (GID), harmonogram dla Moniki
-- ============================================================================
PRINT '';
PRINT '--- 6. NIEZGODNOSCI NAZWY HODOWCY (Dostawcy.Name != HarmonogramDostaw.Dostawca) ---';
SELECT fc.ID, fc.CarLp, fc.LpDostawy,
       RTRIM(fc.CustomerGID) AS GID,
       d.Name AS Hodowca_FarmerCalc,
       hd.Dostawca AS Hodowca_Harmonogram,
       fc.NettoWeight AS Netto,
       CASE WHEN UPPER(LTRIM(RTRIM(d.Name))) <> UPPER(LTRIM(RTRIM(hd.Dostawca)))
            THEN '⚠️ ROZJAZD' ELSE 'OK' END AS Status
FROM dbo.FarmerCalc fc
INNER JOIN dbo.HarmonogramDostaw hd ON hd.Lp = fc.LpDostawy
LEFT JOIN dbo.Dostawcy d ON LTRIM(RTRIM(d.ID)) = LTRIM(RTRIM(fc.CustomerGID))
WHERE fc.CalcDate = @Data AND ISNULL(fc.Deleted,0) = 0
  AND UPPER(LTRIM(RTRIM(ISNULL(d.Name, '')))) <> UPPER(LTRIM(RTRIM(ISNULL(hd.Dostawca, ''))))
ORDER BY fc.CarLp;

-- ============================================================================
-- SEKCJA 7: HARMONOGRAMY POTWIERDZONE ALE BEZ AUT (Asia potwierdzila ale 0 wjechalo)
-- Mozliwosc: Asia potwierdzila stary harmonogram, auta sa w innym LP
-- ============================================================================
PRINT '';
PRINT '--- 7. POTWIERDZONE PRZEZ ASIE ALE BEZ AUT ---';
SELECT hd.Lp, hd.Dostawca, hd.DostawcaID,
       ISNULL(TRY_CAST(hd.Auta AS INT), 0) AS Plan_Aut,
       hd.PotwWaga, hd.PotwSztuki, hd.KtoWaga,
       CONVERT(VARCHAR(16), hd.DataPotwWaga, 120) AS KiedyPotwierdzono,
       (SELECT COUNT(*) FROM dbo.FarmerCalc fc WHERE fc.LpDostawy = hd.Lp AND fc.CalcDate = @Data AND ISNULL(fc.Deleted,0)=0) AS AutaReal
FROM dbo.HarmonogramDostaw hd
WHERE CAST(hd.DataOdbioru AS DATE) = @Data
  AND hd.PotwWaga = 1
  AND NOT EXISTS (SELECT 1 FROM dbo.FarmerCalc fc WHERE fc.LpDostawy = hd.Lp AND fc.CalcDate = @Data AND ISNULL(fc.Deleted,0)=0)
ORDER BY hd.Dostawca;

-- ============================================================================
-- SEKCJA 8: BRAK SZTUK/WAGI W HARMONOGRAMIE (placeholder bez danych)
-- SztukiDek = 0 lub WagaDek = 0 - dispatcher zaczal i nie skonczyl
-- ============================================================================
PRINT '';
PRINT '--- 8. HARMONOGRAMY Z BRAKAMI (SztukiDek = 0 lub WagaDek = 0) ---';
SELECT hd.Lp, hd.Dostawca,
       hd.Auta, hd.SztukiDek, hd.WagaDek, hd.SztSzuflada,
       hd.PotwWaga, hd.PotwSztuki,
       CONVERT(VARCHAR(16), hd.DataUtw, 120) AS Utworzone
FROM dbo.HarmonogramDostaw hd
WHERE CAST(hd.DataOdbioru AS DATE) = @Data
  AND (ISNULL(TRY_CAST(hd.SztukiDek AS INT), 0) = 0 OR ISNULL(TRY_CAST(hd.WagaDek AS DECIMAL(10,3)), 0) = 0)
ORDER BY hd.Dostawca;

-- ============================================================================
-- SEKCJA 9: ROZJAZD PLAN vs REAL na poziomie aut (overflow/underflow)
-- ============================================================================
PRINT '';
PRINT '--- 9. PLAN vs REAL (overflow/underflow aut) ---';
SELECT hd.Lp, hd.Dostawca,
       ISNULL(TRY_CAST(hd.Auta AS INT), 0) AS Plan_Aut,
       (SELECT COUNT(*) FROM dbo.FarmerCalc fc WHERE fc.LpDostawy = hd.Lp AND fc.CalcDate = @Data AND ISNULL(fc.Deleted,0)=0) AS Real_Aut,
       (SELECT COUNT(*) FROM dbo.FarmerCalc fc WHERE fc.LpDostawy = hd.Lp AND fc.CalcDate = @Data AND ISNULL(fc.Deleted,0)=0) - ISNULL(TRY_CAST(hd.Auta AS INT), 0) AS Roznica,
       hd.PotwWaga,
       CASE
         WHEN (SELECT COUNT(*) FROM dbo.FarmerCalc fc WHERE fc.LpDostawy = hd.Lp AND fc.CalcDate = @Data AND ISNULL(fc.Deleted,0)=0) > ISNULL(TRY_CAST(hd.Auta AS INT), 0) THEN '⚠️ OVERFLOW'
         WHEN (SELECT COUNT(*) FROM dbo.FarmerCalc fc WHERE fc.LpDostawy = hd.Lp AND fc.CalcDate = @Data AND ISNULL(fc.Deleted,0)=0) < ISNULL(TRY_CAST(hd.Auta AS INT), 0) THEN '🚫 BRAK AUT'
         ELSE 'OK'
       END AS Status
FROM dbo.HarmonogramDostaw hd
WHERE CAST(hd.DataOdbioru AS DATE) = @Data
  AND ISNULL(TRY_CAST(hd.Auta AS INT), 0) > 0
ORDER BY hd.Dostawca;

-- ============================================================================
-- SEKCJA 10: AUTA Z BRAKAMI WAG (Brutto bez Tara lub odwrotnie)
-- ============================================================================
PRINT '';
PRINT '--- 10. AUTA Z NIEPELNYM WAZENIEM (Brutto + Tara musza byc obie) ---';
SELECT fc.ID, fc.CarLp, fc.LpDostawy, d.Name AS Hodowca,
       fc.FullWeight AS Brutto, fc.EmptyWeight AS Tara, fc.NettoWeight AS Netto,
       CASE
         WHEN ISNULL(fc.FullWeight,0) > 0 AND ISNULL(fc.EmptyWeight,0) = 0 THEN '⏳ Czeka na tare'
         WHEN ISNULL(fc.FullWeight,0) = 0 AND ISNULL(fc.EmptyWeight,0) > 0 THEN '⚠️ Tara bez brutto (dziwne)'
         ELSE 'OK'
       END AS Status,
       CONVERT(VARCHAR(16), fc.Przyjazd, 120) AS Przyjazd
FROM dbo.FarmerCalc fc
LEFT JOIN dbo.Dostawcy d ON LTRIM(RTRIM(d.ID)) = LTRIM(RTRIM(fc.CustomerGID))
WHERE fc.CalcDate = @Data AND ISNULL(fc.Deleted,0) = 0
  AND (
        (ISNULL(fc.FullWeight,0) > 0 AND ISNULL(fc.EmptyWeight,0) = 0)
        OR (ISNULL(fc.FullWeight,0) = 0 AND ISNULL(fc.EmptyWeight,0) > 0)
      )
ORDER BY fc.CarLp;

-- ============================================================================
-- SEKCJA 11: HISTORIA TRANSFERoW Z AVILOG (kto, kiedy, ile)
-- ============================================================================
PRINT '';
PRINT '--- 11. TRANSFERY Z AVILOG (ostatnie 5 dla tego dnia) ---';
SELECT TOP 5 ID,
       CONVERT(VARCHAR(16), TransferDate, 120) AS Kiedy,
       TransferByUser AS Kto,
       RecordCount AS Wpisy
FROM dbo.MatrycaTransferLog
WHERE CalcDate = @Data
ORDER BY TransferDate DESC;

-- ============================================================================
-- SEKCJA 12: STATYSTYKA OSTATNICH 14 DNI (trendy duchow)
-- ============================================================================
PRINT '';
PRINT '--- 12. STATYSTYKA 14 DNI (ile duchow per dzien?) ---';
;WITH HodowcyZAktywnymi AS (
    SELECT hd.DostawcaID, CAST(hd.DataOdbioru AS DATE) AS Data
    FROM dbo.HarmonogramDostaw hd
    LEFT JOIN (SELECT LpDostawy, CalcDate FROM dbo.FarmerCalc WHERE ISNULL(Deleted,0)=0 GROUP BY LpDostawy, CalcDate) fc
        ON fc.LpDostawy = hd.Lp AND fc.CalcDate = CAST(hd.DataOdbioru AS DATE)
    WHERE CAST(hd.DataOdbioru AS DATE) BETWEEN DATEADD(DAY, -14, GETDATE()) AND DATEADD(DAY, 7, GETDATE())
      AND (hd.PotwWaga = 1 OR fc.LpDostawy IS NOT NULL)
    GROUP BY hd.DostawcaID, CAST(hd.DataOdbioru AS DATE)
),
FCcounts AS (
    SELECT LpDostawy, CalcDate, COUNT(*) AS Cnt
    FROM dbo.FarmerCalc WHERE ISNULL(Deleted,0)=0
    GROUP BY LpDostawy, CalcDate
)
SELECT
    CAST(hd.DataOdbioru AS DATE) AS Data,
    COUNT(*) AS HarmonogramowRazem,
    SUM(CASE WHEN hd.PotwWaga = 1 THEN 1 ELSE 0 END) AS Potwierdzone,
    SUM(CASE WHEN ISNULL(hd.PotwWaga, 0) = 0 AND ISNULL(fc.Cnt, 0) = 0 AND z.DostawcaID IS NOT NULL
         THEN 1 ELSE 0 END) AS Duchy_doUsuniecia,
    SUM(CASE WHEN ISNULL(TRY_CAST(hd.SztukiDek AS INT),0) = 0 OR ISNULL(TRY_CAST(hd.WagaDek AS DECIMAL(10,3)),0) = 0 THEN 1 ELSE 0 END) AS BrakDanych
FROM dbo.HarmonogramDostaw hd
LEFT JOIN FCcounts fc ON fc.LpDostawy = hd.Lp AND fc.CalcDate = CAST(hd.DataOdbioru AS DATE)
LEFT JOIN HodowcyZAktywnymi z ON z.DostawcaID = hd.DostawcaID AND z.Data = CAST(hd.DataOdbioru AS DATE)
WHERE CAST(hd.DataOdbioru AS DATE) BETWEEN DATEADD(DAY, -14, GETDATE()) AND DATEADD(DAY, 7, GETDATE())
GROUP BY CAST(hd.DataOdbioru AS DATE)
ORDER BY Data DESC;

-- ============================================================================
-- SEKCJA 13: UTRATA DANYCH AVILOG → HarmonogramDostaw
-- AVILOG zaimportowal X wpisow (z MatrycaTransferLog.RecordCount),
-- ale w HarmonogramDostaw na ten dzien jest Y wpisow. Roznica = utracone wpisy.
-- Sprawdza tez Matryca (tymczasowy bufor) - moze tam zostaly nieprzepisane.
-- ============================================================================
PRINT '';
PRINT '--- 13. UTRATA DANYCH (AVILOG vs HarmonogramDostaw vs Matryca) ---';
SELECT
    (SELECT TOP 1 RecordCount FROM dbo.MatrycaTransferLog WHERE CalcDate = @Data ORDER BY TransferDate DESC) AS AVILOG_ImportowanoWpisow,
    (SELECT COUNT(*) FROM dbo.HarmonogramDostaw WHERE CAST(DataOdbioru AS DATE) = @Data) AS HarmonogramDostawWpisow,
    (SELECT COUNT(*) FROM dbo.Matryca WHERE DataUboju = @Data) AS Matryca_BuforNieprzepisanych,
    -- Roznica
    ISNULL((SELECT TOP 1 RecordCount FROM dbo.MatrycaTransferLog WHERE CalcDate = @Data ORDER BY TransferDate DESC), 0)
        - (SELECT COUNT(*) FROM dbo.HarmonogramDostaw WHERE CAST(DataOdbioru AS DATE) = @Data) AS UtraconeWpisow,
    -- Jesli > 0 to znaczy ze cos sie nie przeniosło z AVILOG do HarmonogramDostaw
    CASE
        WHEN (SELECT COUNT(*) FROM dbo.Matryca WHERE DataUboju = @Data) > 0
            THEN N'⚠️ W Matryca są nieprzepisane wpisy - sprawdz Menu>Matryca Transportu'
        WHEN ISNULL((SELECT TOP 1 RecordCount FROM dbo.MatrycaTransferLog WHERE CalcDate = @Data ORDER BY TransferDate DESC), 0)
             > (SELECT COUNT(*) FROM dbo.HarmonogramDostaw WHERE CAST(DataOdbioru AS DATE) = @Data)
            THEN N'⚠️ AVILOG zaimportowal wiecej niz jest w HarmonogramDostaw - mozliwe usuniete recznie albo nigdy nie przepisane'
        ELSE N'OK - zgadza sie'
    END AS Diagnoza;

-- Pokaz CO konkretnie zostało w Matryca (jesli cokolwiek)
PRINT '';
PRINT '--- 13b. POZOSTALE WPISY W BUFORZE Matryca dla tego dnia ---';
SELECT NrAuta, Dostawca, WagaDek, SztSzuflada, Kierowca, Pojazd
FROM dbo.Matryca
WHERE DataUboju = @Data
ORDER BY NrAuta;

-- ============================================================================
-- SEKCJA 14: KTORZY HODOWCY MAJA AUTA W FarmerCalc ALE BRAK W HarmonogramDostaw
-- To pokazuje "kto przyjechal a nie zostal zaplanowany" - brak harmonogramu
-- ============================================================================
PRINT '';
PRINT '--- 14. HODOWCY KTORZY PRZYJECHALI BEZ HARMONOGRAMU ---';
;WITH HodowcyZHarm AS (
    SELECT UPPER(LTRIM(RTRIM(Dostawca))) AS NazwaKey
    FROM dbo.HarmonogramDostaw
    WHERE CAST(DataOdbioru AS DATE) = @Data
),
HodowcyZAut AS (
    SELECT RTRIM(fc.CustomerGID) AS GID,
           UPPER(LTRIM(RTRIM(d.Name))) AS NazwaKey,
           d.Name AS Hodowca,
           COUNT(*) AS LiczbaAut,
           SUM(fc.NettoWeight) AS SumaNetto
    FROM dbo.FarmerCalc fc
    LEFT JOIN dbo.Dostawcy d ON LTRIM(RTRIM(d.ID)) = LTRIM(RTRIM(fc.CustomerGID))
    WHERE fc.CalcDate = @Data AND ISNULL(fc.Deleted,0)=0
    GROUP BY RTRIM(fc.CustomerGID), UPPER(LTRIM(RTRIM(d.Name))), d.Name
)
SELECT ha.GID, ha.Hodowca, ha.LiczbaAut, ha.SumaNetto,
       N'-- Brak harmonogramu - dodaj wpis do HarmonogramDostaw albo przepnij auta na inny LP' AS Sugestia
FROM HodowcyZAut ha
LEFT JOIN HodowcyZHarm hh ON hh.NazwaKey = ha.NazwaKey
WHERE hh.NazwaKey IS NULL
ORDER BY ha.LiczbaAut DESC;

-- ============================================================================
-- SEKCJA 15: KTORZY HODOWCY MAJA HARMONOGRAM ALE NIE PRZYJECHALI
-- To pokazuje "zaplanowani ale brak dostawy" - moze anulacja, moze pojechalo gdzie indziej
-- ============================================================================
PRINT '';
PRINT '--- 15. HODOWCY ZAPLANOWANI ALE NIC NIE PRZYJECHALO ---';
SELECT hd.Lp, hd.Dostawca,
       ISNULL(TRY_CAST(hd.Auta AS INT),0) AS Plan_Aut,
       ISNULL(TRY_CAST(hd.SztukiDek AS INT),0) AS Plan_Sztuk,
       hd.PotwWaga,
       N'-- Jesli anulacja: DELETE. Jesli przesunieta - sprawdz harmonogram nastepnego dnia.' AS Sugestia
FROM dbo.HarmonogramDostaw hd
WHERE CAST(hd.DataOdbioru AS DATE) = @Data
  AND ISNULL(TRY_CAST(hd.Auta AS INT),0) > 0
  AND NOT EXISTS (SELECT 1 FROM dbo.FarmerCalc fc WHERE fc.LpDostawy = hd.Lp AND fc.CalcDate = @Data AND ISNULL(fc.Deleted,0)=0)
ORDER BY hd.Dostawca;

-- ============================================================================
-- KONIEC DIAGNOZY
-- ============================================================================
PRINT '';
PRINT '=== KONIEC DIAGNOZY ===';
PRINT 'Aby naprawic problemy: skopiuj REPAIR_SQL z odpowiedniej sekcji i wykonaj w SSMS';
