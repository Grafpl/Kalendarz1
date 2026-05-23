-- ═══════════════════════════════════════════════════════════════════════════
-- MASTER DIAGNOSTIC — pelen stan systemu Transport + Webfleet + LibraNet
-- ═══════════════════════════════════════════════════════════════════════════
-- Uruchom w SSMS na 192.168.0.109. Wynik wszystkich sekcji wklej do chatu.
-- Skrypt READ-ONLY (zadne UPDATE/INSERT/DELETE).
-- ═══════════════════════════════════════════════════════════════════════════

SET NOCOUNT ON;
PRINT '╔══════════════════════════════════════════════════════════════════════╗';
PRINT '║   MASTER DIAGNOSTIC — Transport + Webfleet + LibraNet (.109)         ║';
PRINT CONCAT('║   Czas: ', CONVERT(varchar, GETDATE(), 120), '                                     ║');
PRINT '╚══════════════════════════════════════════════════════════════════════╝';

-- ════════════════════════════════════════════════════════════════════
-- [1] OGOLNE LICZNIKI — ile czego jest
-- ════════════════════════════════════════════════════════════════════
PRINT '';
PRINT '─── [1] LICZNIKI TransportPL + LibraNet ───────────────────────────────';
SELECT
    'TransportPL.Pojazd'                    AS Tabela,
    COUNT(*)                                 AS Wszystkie,
    SUM(CASE WHEN Aktywny=1 THEN 1 ELSE 0 END) AS Aktywne
FROM TransportPL.dbo.Pojazd
UNION ALL SELECT 'TransportPL.Kierowca', COUNT(*), SUM(CASE WHEN Aktywny=1 THEN 1 ELSE 0 END)
    FROM TransportPL.dbo.Kierowca
UNION ALL SELECT 'TransportPL.Kurs', COUNT(*), SUM(CASE WHEN DataKursu >= DATEADD(DAY,-30,CAST(GETDATE() AS DATE)) THEN 1 ELSE 0 END)
    FROM TransportPL.dbo.Kurs
UNION ALL SELECT 'TransportPL.Ladunek', COUNT(*), NULL
    FROM TransportPL.dbo.Ladunek
UNION ALL SELECT 'TransportPL.WebfleetVehicleMapping', COUNT(*), SUM(CASE WHEN PojazdID IS NOT NULL THEN 1 ELSE 0 END)
    FROM TransportPL.dbo.WebfleetVehicleMapping
UNION ALL SELECT 'TransportPL.WebfleetDriverMapping', COUNT(*), SUM(CASE WHEN KierowcaID IS NOT NULL THEN 1 ELSE 0 END)
    FROM TransportPL.dbo.WebfleetDriverMapping
UNION ALL SELECT 'LibraNet.ZamowieniaMieso (30d)', COUNT(*), SUM(CASE WHEN DataPrzyjazdu >= DATEADD(DAY,-30,GETDATE()) THEN 1 ELSE 0 END)
    FROM LibraNet.dbo.ZamowieniaMieso WHERE DataPrzyjazdu >= DATEADD(DAY,-90,GETDATE());

-- ════════════════════════════════════════════════════════════════════
-- [2] MAPPING INTEGRITY — czy wszystkie WebfleetVehicleMapping wskazuja na istniejacy Pojazd
-- ════════════════════════════════════════════════════════════════════
PRINT '';
PRINT '─── [2] WebfleetVehicleMapping — integralnosc ─────────────────────────';
SELECT
    m.WebfleetObjectNo, m.WebfleetObjectName, m.PojazdID,
    p.Rejestracja, p.Marka, p.Aktywny,
    CASE WHEN p.PojazdID IS NULL THEN '❌ ORPHAN (Pojazd nie istnieje)'
         WHEN p.Aktywny=0 THEN '⚠ Pojazd niekatywny'
         ELSE '✓ OK' END AS Status,
    (SELECT COUNT(*) FROM TransportPL.dbo.Kurs WHERE PojazdID = m.PojazdID
            AND DataKursu >= DATEADD(DAY,-90,CAST(GETDATE() AS DATE))) AS Kursy_90d
FROM TransportPL.dbo.WebfleetVehicleMapping m
LEFT JOIN TransportPL.dbo.Pojazd p ON p.PojazdID = m.PojazdID
ORDER BY Kursy_90d DESC;

-- ════════════════════════════════════════════════════════════════════
-- [3] POJAZDY BEZ MAPPING — sa zarejestrowane ale nie maja Webfleet
-- ════════════════════════════════════════════════════════════════════
PRINT '';
PRINT '─── [3] Pojazdy TransportPL.Pojazd BEZ WebfleetVehicleMapping ────────';
SELECT
    p.PojazdID, p.Rejestracja, p.Marka, p.Aktywny,
    (SELECT COUNT(*) FROM TransportPL.dbo.Kurs WHERE PojazdID = p.PojazdID
            AND DataKursu >= DATEADD(DAY,-90,CAST(GETDATE() AS DATE))) AS Kursy_90d,
    (SELECT MAX(DataKursu) FROM TransportPL.dbo.Kurs WHERE PojazdID = p.PojazdID) AS Ostatni_Kurs
FROM TransportPL.dbo.Pojazd p
WHERE NOT EXISTS (SELECT 1 FROM TransportPL.dbo.WebfleetVehicleMapping m WHERE m.PojazdID = p.PojazdID)
ORDER BY Kursy_90d DESC;

-- ════════════════════════════════════════════════════════════════════
-- [4] DISTRIBUTION KURSOW — kursy per dzien tygodnia (ostatnie 30 dni)
-- ════════════════════════════════════════════════════════════════════
PRINT '';
PRINT '─── [4] Kursy per dzien tygodnia (ostatnie 30 dni) ────────────────────';
SELECT
    DATENAME(WEEKDAY, DataKursu)        AS Dzien,
    DATEPART(WEEKDAY, DataKursu)        AS DowNum,
    COUNT(*)                            AS Liczba_Kursow,
    COUNT(DISTINCT PojazdID)            AS Unikalnych_Pojazdow,
    COUNT(DISTINCT KierowcaID)          AS Unikalnych_Kierowcow
FROM TransportPL.dbo.Kurs
WHERE DataKursu >= DATEADD(DAY,-30,CAST(GETDATE() AS DATE))
GROUP BY DATENAME(WEEKDAY, DataKursu), DATEPART(WEEKDAY, DataKursu)
ORDER BY DowNum;

-- ════════════════════════════════════════════════════════════════════
-- [5] TOP TRASY (ostatnie 30 dni) — najczesciej powtarzajace sie
-- ════════════════════════════════════════════════════════════════════
PRINT '';
PRINT '─── [5] TOP 20 tras ostatnich 30 dni ──────────────────────────────────';
SELECT TOP 20
    Trasa,
    COUNT(*)                  AS Wystapien,
    COUNT(DISTINCT PojazdID)  AS Pojazdow,
    MIN(DataKursu)            AS Pierwsza,
    MAX(DataKursu)            AS Ostatnia
FROM TransportPL.dbo.Kurs
WHERE DataKursu >= DATEADD(DAY,-30,CAST(GETDATE() AS DATE))
  AND Trasa IS NOT NULL AND LEN(Trasa) > 0
GROUP BY Trasa
ORDER BY Wystapien DESC;

-- ════════════════════════════════════════════════════════════════════
-- [6] STATUSY KURSOW — rozklad
-- ════════════════════════════════════════════════════════════════════
PRINT '';
PRINT '─── [6] Status kursow (30 dni) ────────────────────────────────────────';
SELECT
    ISNULL(Status, '(NULL)') AS Status,
    COUNT(*)                  AS Liczba,
    MIN(DataKursu)            AS Najstarszy,
    MAX(DataKursu)            AS Najnowszy
FROM TransportPL.dbo.Kurs
WHERE DataKursu >= DATEADD(DAY,-30,CAST(GETDATE() AS DATE))
GROUP BY Status
ORDER BY Liczba DESC;

-- ════════════════════════════════════════════════════════════════════
-- [7] LADUNEK INTEGRITY — % ladunkow ktore matchuja LibraNet.ZamowieniaMieso
-- ════════════════════════════════════════════════════════════════════
PRINT '';
PRINT '─── [7] Ladunek: ile ZAM_ matchuje LibraNet.ZamowieniaMieso ───────────';
SELECT
    COUNT(*)                             AS Wszystkie_Ladunki,
    SUM(CASE WHEN l.KodKlienta LIKE 'ZAM[_]%' THEN 1 ELSE 0 END) AS Ladunki_ZAM_format,
    SUM(CASE WHEN zm.Id IS NOT NULL THEN 1 ELSE 0 END)            AS Z_Match_LibraNet,
    SUM(CASE WHEN l.KodKlienta LIKE 'ZAM[_]%' AND zm.Id IS NULL THEN 1 ELSE 0 END) AS Orphan_ZAM
FROM TransportPL.dbo.Ladunek l
JOIN TransportPL.dbo.Kurs k ON k.KursID = l.KursID
LEFT JOIN LibraNet.dbo.ZamowieniaMieso zm
    ON zm.Id = TRY_CAST(SUBSTRING(l.KodKlienta, 5, 20) AS INT) AND l.KodKlienta LIKE 'ZAM[_]%'
WHERE k.DataKursu >= DATEADD(DAY,-30,CAST(GETDATE() AS DATE));

-- ════════════════════════════════════════════════════════════════════
-- [8] PROBLEMS — kursy bez pojazdu, kursy bez ladunkow, ladunki orphan
-- ════════════════════════════════════════════════════════════════════
PRINT '';
PRINT '─── [8] Anomalie / problemy w bazie ───────────────────────────────────';
SELECT
    'Kursy bez PojazdID'    AS Problem,
    COUNT(*)                 AS Liczba
FROM TransportPL.dbo.Kurs WHERE PojazdID IS NULL
UNION ALL SELECT 'Kursy bez KierowcaID', COUNT(*) FROM TransportPL.dbo.Kurs WHERE KierowcaID IS NULL
UNION ALL SELECT 'Kursy bez GodzWyjazdu', COUNT(*) FROM TransportPL.dbo.Kurs WHERE GodzWyjazdu IS NULL
UNION ALL SELECT 'Kursy bez Trasa', COUNT(*) FROM TransportPL.dbo.Kurs WHERE Trasa IS NULL OR LEN(Trasa) = 0
UNION ALL SELECT 'Kursy bez ladunkow (30d)',
    (SELECT COUNT(*) FROM TransportPL.dbo.Kurs k
     WHERE k.DataKursu >= DATEADD(DAY,-30,CAST(GETDATE() AS DATE))
       AND NOT EXISTS (SELECT 1 FROM TransportPL.dbo.Ladunek l WHERE l.KursID = k.KursID))
UNION ALL SELECT 'Ladunki bez KodKlienta',
    (SELECT COUNT(*) FROM TransportPL.dbo.Ladunek WHERE KodKlienta IS NULL OR LEN(KodKlienta) = 0)
UNION ALL SELECT 'Mapowania Webfleet bez PojazdID',
    (SELECT COUNT(*) FROM TransportPL.dbo.WebfleetVehicleMapping WHERE PojazdID IS NULL);

-- ════════════════════════════════════════════════════════════════════
-- [9] AKTYWNOSC PER POJAZD — top 15 ostatnich 30 dni
-- ════════════════════════════════════════════════════════════════════
PRINT '';
PRINT '─── [9] TOP 15 pojazdow po liczbie kursow (30 dni) ────────────────────';
SELECT TOP 15
    p.PojazdID, p.Rejestracja, p.Marka,
    m.WebfleetObjectNo                       AS Mapping,
    COUNT(k.KursID)                          AS Kursy_30d,
    COUNT(DISTINCT k.KierowcaID)             AS Roznych_Kierowcow,
    MAX(k.DataKursu)                         AS Ostatni_Kurs,
    SUM(CASE WHEN k.DataKursu = CAST(GETDATE() AS DATE) THEN 1 ELSE 0 END) AS Dzis
FROM TransportPL.dbo.Pojazd p
LEFT JOIN TransportPL.dbo.WebfleetVehicleMapping m ON m.PojazdID = p.PojazdID
LEFT JOIN TransportPL.dbo.Kurs k ON k.PojazdID = p.PojazdID
    AND k.DataKursu >= DATEADD(DAY,-30,CAST(GETDATE() AS DATE))
GROUP BY p.PojazdID, p.Rejestracja, p.Marka, m.WebfleetObjectNo
ORDER BY Kursy_30d DESC;

-- ════════════════════════════════════════════════════════════════════
-- [10] AKTYWNOSC PER KIEROWCA — top 15 ostatnich 30 dni
-- ════════════════════════════════════════════════════════════════════
PRINT '';
PRINT '─── [10] TOP 15 kierowcow po liczbie kursow (30 dni) ──────────────────';
SELECT TOP 15
    ki.KierowcaID,
    CONCAT(ki.Imie, ' ', ki.Nazwisko)        AS Kierowca,
    ki.Aktywny,
    wm.WebfleetDriverId                      AS Webfleet_GPS,
    COUNT(k.KursID)                          AS Kursy_30d,
    COUNT(DISTINCT k.PojazdID)               AS Roznych_Pojazdow,
    MAX(k.DataKursu)                         AS Ostatni_Kurs
FROM TransportPL.dbo.Kierowca ki
LEFT JOIN TransportPL.dbo.WebfleetDriverMapping wm ON wm.KierowcaID = ki.KierowcaID
LEFT JOIN TransportPL.dbo.Kurs k ON k.KierowcaID = ki.KierowcaID
    AND k.DataKursu >= DATEADD(DAY,-30,CAST(GETDATE() AS DATE))
GROUP BY ki.KierowcaID, ki.Imie, ki.Nazwisko, ki.Aktywny, wm.WebfleetDriverId
ORDER BY Kursy_30d DESC;

-- ════════════════════════════════════════════════════════════════════
-- [11] DZISIAJ — co planowane na dzis
-- ════════════════════════════════════════════════════════════════════
PRINT '';
PRINT '─── [11] Kursy zaplanowane NA DZIS ────────────────────────────────────';
SELECT
    k.KursID, k.GodzWyjazdu, k.GodzPowrotu, k.Status, k.Trasa,
    p.Rejestracja, CONCAT(ki.Imie,' ',ki.Nazwisko) AS Kierowca,
    (SELECT COUNT(*) FROM TransportPL.dbo.Ladunek WHERE KursID = k.KursID) AS Ladunki
FROM TransportPL.dbo.Kurs k
LEFT JOIN TransportPL.dbo.Pojazd p ON p.PojazdID = k.PojazdID
LEFT JOIN TransportPL.dbo.Kierowca ki ON ki.KierowcaID = k.KierowcaID
WHERE k.DataKursu = CAST(GETDATE() AS DATE)
ORDER BY k.GodzWyjazdu;

-- ════════════════════════════════════════════════════════════════════
-- [12] DUPLIKATY MAPOWAN — czy ktos jest zmapowany 2x
-- ════════════════════════════════════════════════════════════════════
PRINT '';
PRINT '─── [12] Duplikaty: jeden PojazdID dla wielu WebfleetObjectNo ─────────';
SELECT m.PojazdID, p.Rejestracja, COUNT(*) AS Liczba_Mapowan,
       STUFF((SELECT ', ' + m2.WebfleetObjectNo FROM TransportPL.dbo.WebfleetVehicleMapping m2
              WHERE m2.PojazdID = m.PojazdID FOR XML PATH('')), 1, 2, '') AS WebfleetObjectNo_List
FROM TransportPL.dbo.WebfleetVehicleMapping m
LEFT JOIN TransportPL.dbo.Pojazd p ON p.PojazdID = m.PojazdID
WHERE m.PojazdID IS NOT NULL
GROUP BY m.PojazdID, p.Rejestracja
HAVING COUNT(*) > 1;

-- ════════════════════════════════════════════════════════════════════
-- [13] INDEKSY — czy najwazniejsze sa zalozone (perf check)
-- ════════════════════════════════════════════════════════════════════
PRINT '';
PRINT '─── [13] Indeksy na kluczowych tabelach ───────────────────────────────';
SELECT
    t.name      AS Tabela,
    i.name      AS Indeks,
    i.type_desc AS Typ,
    (SELECT STUFF((SELECT ', ' + c.name FROM sys.index_columns ic
                   JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
                   WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id
                   ORDER BY ic.key_ordinal FOR XML PATH('')), 1, 2, '')) AS Kolumny
FROM sys.indexes i
JOIN sys.tables t ON t.object_id = i.object_id
WHERE t.name IN ('Kurs','Ladunek','Pojazd','Kierowca','WebfleetVehicleMapping','WebfleetDriverMapping')
  AND i.type > 0
ORDER BY t.name, i.name;

PRINT '';
PRINT '════════════════════════════════════════════════════════════════════════';
PRINT '  KONIEC.  Skopiuj wszystkie wyniki + Messages tab i wklej do chatu.';
PRINT '════════════════════════════════════════════════════════════════════════';
