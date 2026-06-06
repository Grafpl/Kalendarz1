/* ============================================================================
   Migracja uprawnień: TransportWPF (pozycja 78) → UstalanieTranportu (pozycja 17)

   POWÓD: Kafelek „Planowanie Transportu (WPF)" został zlany z „Planowanie
   Transportu" (commit 9089a50). Stary WinForms TransportMainFormImproved
   zastąpiony przez WPF PlanowanieTransportuWpfWindow pod kluczem
   UstalanieTranportu. Pozycja 78 w bitstringu zostaje (kompatybilność),
   ale użytkownicy którzy mieli tylko ją (a nie pozycji 17) by stracili dostęp.

   Co robi:
     - DRY RUN (sekcja 1): pokazuje kogo dotknie, bez zmian
     - NAPRAWA (sekcja 2): dopełnia Access do 82 znaków, ustawia poz. 17 na '1'
       dla wszystkich z poz. 78 = '1'. Transakcja z TRY/CATCH ROLLBACK.
     - WALIDACJA (sekcja 3): po naprawie powinno być 0 sierot

   Bezpieczne:
     - Nigdy nie odbiera uprawnień (tylko ustawia '1')
     - Idempotentne (uruchom raz lub wielokrotnie — ten sam wynik)
     - Transakcyjne (wszystko-albo-nic)

   Pozycje w stringu Access (1-indexed w SQL SUBSTRING/STUFF):
     poz. 17 = UstalanieTranportu (bit 16 w 0-indexed _moduleAccessOrder)
     poz. 78 = TransportWPF       (bit 77 w 0-indexed _moduleAccessOrder)

   Baza: LibraNet (192.168.0.109)   |   Tabela: dbo.operators (ID, Name, Access)
   ============================================================================ */

USE LibraNet;
GO

------------------------------------------------------------------------------
-- SEKCJA 1 — DRY RUN: kogo dotknie migracja (URUCHOM NAJPIERW, przejrzyj)
------------------------------------------------------------------------------
PRINT N'═══════════════════════════════════════════════════════════════════════';
PRINT N'SEKCJA 1 — DRY RUN: użytkownicy ze straconym dostępem (bez zmiany)';
PRINT N'═══════════════════════════════════════════════════════════════════════';

;WITH stan AS (
    SELECT
        ID,
        Name,
        ISNULL(Access, '') AS Access,
        LEN(ISNULL(Access, '')) AS Dlugosc,
        CASE WHEN LEN(ISNULL(Access, '')) >= 17 THEN SUBSTRING(Access, 17, 1) ELSE '?' END AS Poz17,
        CASE WHEN LEN(ISNULL(Access, '')) >= 78 THEN SUBSTRING(Access, 78, 1) ELSE '?' END AS Poz78
    FROM dbo.operators
)
SELECT
    ID, Name, Dlugosc AS DlugoscAccess,
    Poz17 AS Poz17_Ustalanie_PRZED,
    Poz78 AS Poz78_WPF_PRZED,
    N'→ ustawi poz. 17 = 1' AS Akcja
FROM stan
WHERE Poz78 = '1' AND Poz17 IN ('0', '?')
ORDER BY ID;

DECLARE @doMigracji INT = (
    SELECT COUNT(*) FROM dbo.operators
    WHERE LEN(ISNULL(Access, '')) >= 78
      AND SUBSTRING(Access, 78, 1) = '1'
      AND (LEN(Access) < 17 OR SUBSTRING(Access, 17, 1) = '0')
);
PRINT N'';
PRINT N'Do migracji: ' + CAST(@doMigracji AS NVARCHAR(10)) + N' użytkowników.';
PRINT N'';
GO

------------------------------------------------------------------------------
-- SEKCJA 2 — NAPRAWA (uruchom po akceptacji DRY-RUN)
------------------------------------------------------------------------------
PRINT N'═══════════════════════════════════════════════════════════════════════';
PRINT N'SEKCJA 2 — NAPRAWA (transakcja z rollbackiem przy błędzie)';
PRINT N'═══════════════════════════════════════════════════════════════════════';

BEGIN TRY
    BEGIN TRAN;

    -- KROK 0: dopełnij Access do 82 znaków (same zera na końcu — niczego nie odbiera)
    -- Bezpieczne, bo długość 82 odpowiada aktualnemu _moduleAccessOrder w Menu.cs.
    UPDATE dbo.operators
        SET Access = LEFT(ISNULL(Access, '') + REPLICATE('0', 82), 82)
    WHERE LEN(ISNULL(Access, '')) < 82;

    DECLARE @dopelnione INT = @@ROWCOUNT;
    PRINT N'Dopełniono do 82 znaków: ' + CAST(@dopelnione AS NVARCHAR(10)) + N' rekordów.';

    -- KROK 1: ustaw poz. 17 = '1' dla wszystkich z poz. 78 = '1' (gdy poz. 17 jeszcze '0')
    UPDATE dbo.operators
        SET Access = STUFF(Access, 17, 1, '1')
    WHERE SUBSTRING(Access, 78, 1) = '1'
      AND SUBSTRING(Access, 17, 1) = '0';

    DECLARE @zmigrowane INT = @@ROWCOUNT;
    PRINT N'Zmigrowano (poz. 17 ← 1): ' + CAST(@zmigrowane AS NVARCHAR(10)) + N' użytkowników.';

    COMMIT TRAN;
    PRINT N'';
    PRINT N'✓ Transakcja zatwierdzona.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    PRINT N'';
    PRINT N'✗ BŁĄD: ' + ERROR_MESSAGE();
    PRINT N'  Wszystkie zmiany cofnięte (ROLLBACK).';
    THROW;
END CATCH
GO

------------------------------------------------------------------------------
-- SEKCJA 3 — WALIDACJA: po migracji nie powinno być sierot
------------------------------------------------------------------------------
PRINT N'';
PRINT N'═══════════════════════════════════════════════════════════════════════';
PRINT N'SEKCJA 3 — WALIDACJA (powinno być 0 wierszy poniżej)';
PRINT N'═══════════════════════════════════════════════════════════════════════';

SELECT
    ID,
    Name,
    SUBSTRING(Access, 17, 1) AS Poz17_Ustalanie,
    SUBSTRING(Access, 78, 1) AS Poz78_WPF,
    N'⚠ Sierota — poz. 78 = 1 ale poz. 17 = 0' AS Stan
FROM dbo.operators
WHERE LEN(Access) >= 78
  AND SUBSTRING(Access, 78, 1) = '1'
  AND SUBSTRING(Access, 17, 1) = '0';

DECLARE @sieroty INT = (
    SELECT COUNT(*) FROM dbo.operators
    WHERE LEN(Access) >= 78
      AND SUBSTRING(Access, 78, 1) = '1'
      AND SUBSTRING(Access, 17, 1) = '0'
);
PRINT N'';
IF @sieroty = 0
    PRINT N'✓ Walidacja OK. Każdy z dostępem do TransportWPF ma teraz UstalanieTranportu.';
ELSE
    PRINT N'⚠ UWAGA: ' + CAST(@sieroty AS NVARCHAR(10)) + N' sierot. Coś poszło nie tak.';
GO
