-- ════════════════════════════════════════════════════════════════════════════
-- Migracja uprawnień: TransportWPF (bit 77) → UstalanieTranportu (bit 16)
--
-- POWÓD: Kafelek „Planowanie Transportu (WPF)" został zlany z „Planowanie Transportu"
-- (commit przed tym skryptem). Stary WinForms TransportMainFormImproved został
-- zastąpiony przez WPF PlanowanieTransportuWpfWindow pod kluczem UstalanieTranportu.
--
-- Co robi: użytkownicy którzy mieli uprawnienie TransportWPF (bit 77) a NIE mieli
-- UstalanieTranportu (bit 16) dostają bit 16 = 1, żeby zachować dostęp do nowego
-- widoku (który jest pod tym samym kluczem co stary).
--
-- Bezpieczne — idempotentne (uruchom raz lub wielokrotnie, da ten sam wynik).
--
-- Baza: LibraNet, tabela operators
-- ════════════════════════════════════════════════════════════════════════════

USE LibraNet;
GO

-- Pozycje w bitstring (1-indexed w SQL przez SUBSTRING/STUFF):
--   bit 16 = znak nr 17  (UstalanieTranportu)
--   bit 77 = znak nr 78  (TransportWPF — alias do tego samego widoku)

DECLARE @migrated INT = 0;

-- Pokaż przed migracją (audyt)
PRINT N'=== PRZED MIGRACJĄ ===';
SELECT
    ID,
    Name,
    CASE WHEN LEN(Access) >= 17 THEN SUBSTRING(Access, 17, 1) ELSE '-' END AS Bit16_Ustalanie,
    CASE WHEN LEN(Access) >= 78 THEN SUBSTRING(Access, 78, 1) ELSE '-' END AS Bit77_WPF,
    LEN(Access) AS DlugoscAccess
FROM dbo.operators
WHERE LEN(Access) >= 78
  AND SUBSTRING(Access, 78, 1) = '1'
  AND (LEN(Access) < 17 OR SUBSTRING(Access, 17, 1) = '0')
ORDER BY ID;

-- Migracja: ustaw bit 16 = 1 dla wszystkich z bitem 77 = 1, którzy nie mają jeszcze bitu 16
UPDATE dbo.operators
SET Access = STUFF(Access, 17, 1, '1'),
    @migrated = @migrated + 1
WHERE LEN(Access) >= 78
  AND SUBSTRING(Access, 78, 1) = '1'
  AND SUBSTRING(Access, 17, 1) = '0';

PRINT N'';
PRINT N'=== WYNIK ===';
PRINT N'Zmigrowano użytkowników: ' + CAST(@migrated AS NVARCHAR(10));

-- Walidacja: wszyscy z bitem 77 powinni teraz mieć też bit 16
PRINT N'';
PRINT N'=== WALIDACJA (powinno być 0 rekordów) ===';
SELECT
    ID,
    Name,
    SUBSTRING(Access, 17, 1) AS Bit16,
    SUBSTRING(Access, 78, 1) AS Bit77
FROM dbo.operators
WHERE LEN(Access) >= 78
  AND SUBSTRING(Access, 78, 1) = '1'
  AND SUBSTRING(Access, 17, 1) = '0';

PRINT N'';
PRINT N'✓ Migracja zakończona. Stary kafelek „Planowanie Transportu (WPF)" znika z menu i admina,';
PRINT N'  ale każdy kto miał do niego dostęp ma teraz dostęp do „Planowanie Transportu" (nowy WPF).';
