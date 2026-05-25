-- ═══════════════════════════════════════════════════════════════════════════
-- DIAGNOZA: kurs "Romanowska" + ladunki (Trzepalka, Cezar) nie widoczne
-- ═══════════════════════════════════════════════════════════════════════════
-- Problem: kurs z trasa zawierajaca Romanowska ma 2 ladunki (Trzepalka, Cezar)
-- ktore nie pokazuja sie w podgladzie kursu ani ogolnym.
-- Uruchom w SSMS na 192.168.0.109. READ-ONLY. Wynik wklej do chatu.
-- ═══════════════════════════════════════════════════════════════════════════

SET NOCOUNT ON;
DECLARE @Dzis DATE = CAST(GETDATE() AS DATE);

PRINT '╔══════════════════════════════════════════════════════════════════╗';
PRINT CONCAT('║  DIAGNOZA Romanowska — dzien: ', CONVERT(varchar, @Dzis, 120), '              ║');
PRINT '╚══════════════════════════════════════════════════════════════════╝';

-- ════════════════════════════════════════════════════════════════════
-- [1] KURSY NA DZIS z trasa zawierajaca Romanowska / Trzepalka / Cezar
-- ════════════════════════════════════════════════════════════════════
PRINT '';
PRINT '─── [1] Kursy DZIS z Romanowska/Trzepalka/Cezar w trasie ──────────';
SELECT
    k.KursID, k.DataKursu, k.GodzWyjazdu, k.GodzPowrotu, k.Status, k.Trasa,
    k.PojazdID, p.Rejestracja,
    k.KierowcaID, CONCAT(ki.Imie,' ',ki.Nazwisko) AS Kierowca,
    (SELECT COUNT(*) FROM TransportPL.dbo.Ladunek WHERE KursID = k.KursID) AS Liczba_Ladunkow
FROM TransportPL.dbo.Kurs k
LEFT JOIN TransportPL.dbo.Pojazd p ON p.PojazdID = k.PojazdID
LEFT JOIN TransportPL.dbo.Kierowca ki ON ki.KierowcaID = k.KierowcaID
WHERE k.DataKursu = @Dzis
  AND (k.Trasa LIKE '%Romanowska%' OR k.Trasa LIKE '%Trzepa%' OR k.Trasa LIKE '%Cezar%')
ORDER BY k.GodzWyjazdu;

-- ════════════════════════════════════════════════════════════════════
-- [2] WSZYSTKIE kursy na dzis (zeby zobaczyc kontekst)
-- ════════════════════════════════════════════════════════════════════
PRINT '';
PRINT '─── [2] WSZYSTKIE kursy na dzis ───────────────────────────────────';
SELECT
    k.KursID, k.GodzWyjazdu, k.Status, k.Trasa, p.Rejestracja,
    (SELECT COUNT(*) FROM TransportPL.dbo.Ladunek WHERE KursID = k.KursID) AS Ladunki
FROM TransportPL.dbo.Kurs k
LEFT JOIN TransportPL.dbo.Pojazd p ON p.PojazdID = k.PojazdID
WHERE k.DataKursu = @Dzis
ORDER BY k.GodzWyjazdu;

-- ════════════════════════════════════════════════════════════════════
-- [3] LADUNKI tych kursow + rozwiazanie klienta (cross-DB)
-- ════════════════════════════════════════════════════════════════════
PRINT '';
PRINT '─── [3] Ladunki kursow Romanowska/Trzepalka/Cezar + klient ────────';
SELECT
    l.LadunekID, l.KursID, k.Trasa AS Kurs_Trasa,
    l.Kolejnosc, l.KodKlienta, l.PojemnikiE2, l.PaletyH1, l.TrybE2, l.Uwagi,
    TRY_CAST(SUBSTRING(l.KodKlienta, 5, 20) AS INT) AS ZamId,
    zm.Id            AS Zam_Id,
    zm.KlientId      AS LibraNet_KlientId,
    zm.DataPrzyjazdu AS Awizacja,
    zm.DataZamowienia AS DataZam,
    c.Name           AS Klient_HANDEL
FROM TransportPL.dbo.Ladunek l
JOIN TransportPL.dbo.Kurs k ON k.KursID = l.KursID
LEFT JOIN LibraNet.dbo.ZamowieniaMieso zm
    ON zm.Id = TRY_CAST(SUBSTRING(l.KodKlienta, 5, 20) AS INT) AND l.KodKlienta LIKE 'ZAM[_]%'
LEFT JOIN Handel.SSCommon.STContractors c ON c.Id = zm.KlientId
WHERE k.DataKursu = @Dzis
  AND (k.Trasa LIKE '%Romanowska%' OR k.Trasa LIKE '%Trzepa%' OR k.Trasa LIKE '%Cezar%')
ORDER BY l.KursID, l.Kolejnosc;

-- ════════════════════════════════════════════════════════════════════
-- [4] ZAMOWIENIA na dzis z klientem Trzepalka / Cezar / Romanowska (LibraNet)
--     — moze zamowienia sa, ale nie zostaly dodane jako Ladunek do kursu
-- ════════════════════════════════════════════════════════════════════
PRINT '';
PRINT '─── [4] Zamowienia (LibraNet) Trzepalka/Cezar/Romanowska — awizacja dzis ──';
SELECT
    zm.Id            AS ZamId,
    zm.KlientId,
    c.Name           AS Klient,
    zm.DataPrzyjazdu AS Awizacja,
    zm.DataZamowienia,
    zm.LiczbaPalet,
    zm.LiczbaPojemnikow,
    zm.Status        AS Zam_Status,
    zm.TransportStatus,
    -- czy to zamowienie jest gdzies przypisane jako Ladunek?
    (SELECT TOP 1 l.KursID FROM TransportPL.dbo.Ladunek l
     WHERE l.KodKlienta = 'ZAM_' + CAST(zm.Id AS varchar)) AS Przypisane_do_KursID
FROM LibraNet.dbo.ZamowieniaMieso zm
LEFT JOIN Handel.SSCommon.STContractors c ON c.Id = zm.KlientId
WHERE CAST(zm.DataPrzyjazdu AS DATE) = @Dzis
  AND c.Name IS NOT NULL
  AND (c.Name LIKE '%Trzepa%' OR c.Name LIKE '%Cezar%' OR c.Name LIKE '%Romanowsk%')
ORDER BY zm.DataPrzyjazdu;

-- ════════════════════════════════════════════════════════════════════
-- [5] WSZYSTKIE ladunki na dzis (pelen obraz co jest w kursach)
-- ════════════════════════════════════════════════════════════════════
PRINT '';
PRINT '─── [5] WSZYSTKIE ladunki kursow na dzis + klient ─────────────────';
SELECT
    l.KursID, k.Trasa, l.Kolejnosc, l.KodKlienta,
    c.Name AS Klient_HANDEL,
    zm.DataPrzyjazdu AS Awizacja,
    l.PojemnikiE2, l.PaletyH1
FROM TransportPL.dbo.Ladunek l
JOIN TransportPL.dbo.Kurs k ON k.KursID = l.KursID
LEFT JOIN LibraNet.dbo.ZamowieniaMieso zm
    ON zm.Id = TRY_CAST(SUBSTRING(l.KodKlienta, 5, 20) AS INT) AND l.KodKlienta LIKE 'ZAM[_]%'
LEFT JOIN Handel.SSCommon.STContractors c ON c.Id = zm.KlientId
WHERE k.DataKursu = @Dzis
ORDER BY l.KursID, l.Kolejnosc;

-- ════════════════════════════════════════════════════════════════════
-- [6] CZY KodKlienta ma inny format niz ZAM_ (moze Trzepalka/Cezar maja inny)
-- ════════════════════════════════════════════════════════════════════
PRINT '';
PRINT '─── [6] Formaty KodKlienta w ladunkach dzis (rozklad) ─────────────';
SELECT
    CASE
        WHEN l.KodKlienta LIKE 'ZAM[_]%' THEN 'ZAM_<id>'
        WHEN l.KodKlienta IS NULL THEN '(NULL)'
        WHEN l.KodKlienta = '' THEN '(pusty)'
        ELSE 'INNY: ' + LEFT(l.KodKlienta, 12)
    END AS Format_KodKlienta,
    COUNT(*) AS Liczba
FROM TransportPL.dbo.Ladunek l
JOIN TransportPL.dbo.Kurs k ON k.KursID = l.KursID
WHERE k.DataKursu = @Dzis
GROUP BY CASE
        WHEN l.KodKlienta LIKE 'ZAM[_]%' THEN 'ZAM_<id>'
        WHEN l.KodKlienta IS NULL THEN '(NULL)'
        WHEN l.KodKlienta = '' THEN '(pusty)'
        ELSE 'INNY: ' + LEFT(l.KodKlienta, 12)
    END
ORDER BY Liczba DESC;

PRINT '';
PRINT '════════════════════════════════════════════════════════════════════';
PRINT '  KONIEC. Wklej wyniki wszystkich sekcji do chatu.';
PRINT '════════════════════════════════════════════════════════════════════';
