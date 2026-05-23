-- ═══════════════════════════════════════════════════════════════════════════
-- FIX DATA QUALITY — czyszczenie orphan mapowan + brakujace indeksy
-- ═══════════════════════════════════════════════════════════════════════════
-- Uruchom w SSMS na 192.168.0.109. Czyta i poprawia stan bazy.
-- IDEMPOTENTNY — bezpieczny do wielokrotnego uruchamiania.
-- ═══════════════════════════════════════════════════════════════════════════

USE TransportPL;
GO

SET NOCOUNT ON;
PRINT '╔══════════════════════════════════════════════════════════════════╗';
PRINT '║   FIX DATA QUALITY — orphan mapowan + brakujace indeksy          ║';
PRINT CONCAT('║   Czas: ', CONVERT(varchar, GETDATE(), 120), '                                ║');
PRINT '╚══════════════════════════════════════════════════════════════════╝';

-- ════════════════════════════════════════════════════════════════════
-- [1] CLEANUP — usun ORPHAN mapowan w WebfleetVehicleMapping
-- ════════════════════════════════════════════════════════════════════
PRINT '';
PRINT '─── [1] Cleanup orphan WebfleetVehicleMapping ─────────────────────';

DECLARE @orphans INT;
SELECT @orphans = COUNT(*) FROM dbo.WebfleetVehicleMapping
WHERE PojazdID IS NULL
   OR PojazdID NOT IN (SELECT PojazdID FROM dbo.Pojazd);

PRINT CONCAT('Orphan mapowan do usuniecia: ', @orphans);

IF @orphans > 0
BEGIN
    -- Lista przed usunieciem
    PRINT 'Lista orphan przed DELETE:';
    SELECT WebfleetObjectNo, WebfleetObjectName, PojazdID, CreatedAtUTC
    FROM dbo.WebfleetVehicleMapping
    WHERE PojazdID IS NULL
       OR PojazdID NOT IN (SELECT PojazdID FROM dbo.Pojazd);

    DELETE FROM dbo.WebfleetVehicleMapping
    WHERE PojazdID IS NULL
       OR PojazdID NOT IN (SELECT PojazdID FROM dbo.Pojazd);

    PRINT CONCAT('Usunieto: ', @@ROWCOUNT, ' rekordow');
END
ELSE PRINT 'Brak orphanow — OK.';
GO

-- ════════════════════════════════════════════════════════════════════
-- [2] CLEANUP — usun ORPHAN mapowan kierowcow (WebfleetDriverMapping)
-- ════════════════════════════════════════════════════════════════════
PRINT '';
PRINT '─── [2] Cleanup orphan WebfleetDriverMapping ──────────────────────';

IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'WebfleetDriverMapping')
BEGIN
    DECLARE @orphansD INT;
    SELECT @orphansD = COUNT(*) FROM dbo.WebfleetDriverMapping
    WHERE KierowcaID IS NOT NULL
      AND KierowcaID NOT IN (SELECT KierowcaID FROM dbo.Kierowca);

    PRINT CONCAT('Orphan kierowcow mapowan: ', @orphansD);

    IF @orphansD > 0
    BEGIN
        DELETE FROM dbo.WebfleetDriverMapping
        WHERE KierowcaID IS NOT NULL
          AND KierowcaID NOT IN (SELECT KierowcaID FROM dbo.Kierowca);
        PRINT CONCAT('Usunieto: ', @@ROWCOUNT, ' rekordow');
    END
    ELSE PRINT 'Brak orphanow kierowcow — OK.';
END
GO

-- ════════════════════════════════════════════════════════════════════
-- [3] INDEKSY — perf na kluczowych tabelach
-- ════════════════════════════════════════════════════════════════════
PRINT '';
PRINT '─── [3] Brakujace indeksy (perf) ──────────────────────────────────';

-- IX_Kurs_PojazdID_DataKursu — kluczowe dla popup pojazdu
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Kurs_PojazdID_DataKursu' AND object_id = OBJECT_ID('dbo.Kurs'))
BEGIN
    CREATE INDEX IX_Kurs_PojazdID_DataKursu ON dbo.Kurs(PojazdID, DataKursu DESC, GodzWyjazdu DESC)
        INCLUDE (KursID, Trasa, Status, GodzPowrotu, KierowcaID)
        WHERE PojazdID IS NOT NULL;
    PRINT '✓ CREATE IX_Kurs_PojazdID_DataKursu — szybkie wyszukiwanie kursow per pojazd';
END
ELSE PRINT '  IX_Kurs_PojazdID_DataKursu juz istnieje';

-- IX_Kurs_DataKursu — full table scan przy LoadTodayKursy
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Kurs_DataKursu' AND object_id = OBJECT_ID('dbo.Kurs'))
BEGIN
    CREATE INDEX IX_Kurs_DataKursu ON dbo.Kurs(DataKursu, GodzWyjazdu)
        INCLUDE (KursID, PojazdID, KierowcaID, Trasa, Status, GodzPowrotu);
    PRINT '✓ CREATE IX_Kurs_DataKursu — szybkie wyszukiwanie dzisiejszych kursow';
END
ELSE PRINT '  IX_Kurs_DataKursu juz istnieje';

-- IX_Kurs_KierowcaID — raporty per kierowca
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Kurs_KierowcaID' AND object_id = OBJECT_ID('dbo.Kurs'))
BEGIN
    CREATE INDEX IX_Kurs_KierowcaID ON dbo.Kurs(KierowcaID, DataKursu DESC)
        WHERE KierowcaID IS NOT NULL;
    PRINT '✓ CREATE IX_Kurs_KierowcaID';
END
ELSE PRINT '  IX_Kurs_KierowcaID juz istnieje';

-- IX_WebfleetVehicleMapping_PojazdID — lookup mapowania
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_WebfleetVehicleMapping_PojazdID' AND object_id = OBJECT_ID('dbo.WebfleetVehicleMapping'))
BEGIN
    CREATE INDEX IX_WebfleetVehicleMapping_PojazdID ON dbo.WebfleetVehicleMapping(PojazdID)
        WHERE PojazdID IS NOT NULL;
    PRINT '✓ CREATE IX_WebfleetVehicleMapping_PojazdID';
END
ELSE PRINT '  IX_WebfleetVehicleMapping_PojazdID juz istnieje';

-- IX_WebfleetDriverMapping_KierowcaID
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_WebfleetDriverMapping_KierowcaID' AND object_id = OBJECT_ID('dbo.WebfleetDriverMapping'))
BEGIN
    IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'WebfleetDriverMapping')
    BEGIN
        CREATE INDEX IX_WebfleetDriverMapping_KierowcaID ON dbo.WebfleetDriverMapping(KierowcaID)
            WHERE KierowcaID IS NOT NULL;
        PRINT '✓ CREATE IX_WebfleetDriverMapping_KierowcaID';
    END
END
ELSE PRINT '  IX_WebfleetDriverMapping_KierowcaID juz istnieje';
GO

-- ════════════════════════════════════════════════════════════════════
-- [4] WERYFIKACJA — stan po fix
-- ════════════════════════════════════════════════════════════════════
PRINT '';
PRINT '─── [4] STAN PO NAPRAWIE ──────────────────────────────────────────';

-- Weryfikacja przez LEFT JOIN (SQL Server nie pozwala subquery w SUM CASE)
SELECT 'WebfleetVehicleMapping' AS Tabela,
       COUNT(*)                  AS Wszystkie,
       SUM(CASE WHEN m.PojazdID IS NOT NULL THEN 1 ELSE 0 END) AS Z_PojazdID,
       SUM(CASE WHEN m.PojazdID IS NOT NULL AND p.PojazdID IS NULL THEN 1 ELSE 0 END) AS Orphan
FROM dbo.WebfleetVehicleMapping m
LEFT JOIN dbo.Pojazd p ON p.PojazdID = m.PojazdID
UNION ALL
SELECT 'WebfleetDriverMapping', COUNT(*),
       SUM(CASE WHEN m.KierowcaID IS NOT NULL THEN 1 ELSE 0 END),
       SUM(CASE WHEN m.KierowcaID IS NOT NULL AND k.KierowcaID IS NULL THEN 1 ELSE 0 END)
FROM dbo.WebfleetDriverMapping m
LEFT JOIN dbo.Kierowca k ON k.KierowcaID = m.KierowcaID;

PRINT '';
PRINT 'Indeksy na tabeli Kurs:';
SELECT name AS Indeks, type_desc AS Typ
FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.Kurs') AND type > 0
ORDER BY name;

PRINT '';
PRINT '════════════════════════════════════════════════════════════════════';
PRINT '  GOTOWE.  Wynik wklej do chatu (sekcja [4] potwierdza naprawe).';
PRINT '════════════════════════════════════════════════════════════════════';
