-- ═══════════════════════════════════════════════════════════════════════════
-- DIAGNOZA v2 — kurs Romanowska + ladunki (bez HANDEL, ktory jest na .112)
-- ═══════════════════════════════════════════════════════════════════════════
-- CZESC A: uruchom na 192.168.0.109 (TransportPL + LibraNet)
-- CZESC B (na dole): uruchom na 192.168.0.112 (HANDEL) — nazwy klientow
-- ═══════════════════════════════════════════════════════════════════════════

-- ╔══════════════════════════════════════════════════════════════════╗
-- ║  CZESC A — uruchom na 192.168.0.109                              ║
-- ╚══════════════════════════════════════════════════════════════════╝
SET NOCOUNT ON;
DECLARE @Dzis DATE = CAST(GETDATE() AS DATE);

-- [A1] Ladunek(i) kursu 1739 (Romanowska) — co tam faktycznie jest
PRINT '─── [A1] Ladunki kursu Romanowska (1739) ─────────────────────────';
SELECT
    l.LadunekID, l.KursID, l.Kolejnosc, l.KodKlienta,
    l.PojemnikiE2, l.PaletyH1, l.TrybE2, l.Uwagi,
    TRY_CAST(SUBSTRING(l.KodKlienta, 5, 20) AS INT) AS ZamId,
    zm.KlientId      AS LibraNet_KlientId,
    zm.DataPrzyjazdu AS Awizacja
FROM TransportPL.dbo.Ladunek l
LEFT JOIN LibraNet.dbo.ZamowieniaMieso zm
    ON zm.Id = TRY_CAST(SUBSTRING(l.KodKlienta, 5, 20) AS INT) AND l.KodKlienta LIKE 'ZAM[_]%'
WHERE l.KursID = 1739
ORDER BY l.Kolejnosc;

-- [A2] WSZYSTKIE zamowienia z awizacja DZIS (LibraNet) + czy przypisane do kursu
--      Tu znajdziemy Trzepalka/Cezar po KlientId (nazwa w czesci B)
PRINT '';
PRINT '─── [A2] Zamowienia awizowane DZIS + czy w kursie ─────────────────';
SELECT
    zm.Id            AS ZamId,
    zm.KlientId,
    zm.DataPrzyjazdu AS Awizacja,
    zm.DataZamowienia,
    zm.LiczbaPalet,
    zm.LiczbaPojemnikow,
    zm.Status        AS Zam_Status,
    zm.TransportStatus,
    (SELECT TOP 1 l.KursID FROM TransportPL.dbo.Ladunek l
     WHERE l.KodKlienta = 'ZAM_' + CAST(zm.Id AS varchar)) AS W_Kursie_ID,
    (SELECT TOP 1 k.Trasa FROM TransportPL.dbo.Ladunek l
     JOIN TransportPL.dbo.Kurs k ON k.KursID = l.KursID
     WHERE l.KodKlienta = 'ZAM_' + CAST(zm.Id AS varchar)) AS W_Kursie_Trasa
FROM LibraNet.dbo.ZamowieniaMieso zm
WHERE CAST(zm.DataPrzyjazdu AS DATE) = @Dzis
ORDER BY zm.DataPrzyjazdu;

-- [A3] Lista KlientId z dzisiejszych zamowien — skopiuj do CZESCI B
PRINT '';
PRINT '─── [A3] Unikalne KlientId z dzisiejszych zamowien (do czesci B) ──';
SELECT DISTINCT zm.KlientId
FROM LibraNet.dbo.ZamowieniaMieso zm
WHERE CAST(zm.DataPrzyjazdu AS DATE) = @Dzis
ORDER BY zm.KlientId;

-- [A4] Zamowienia ktore NIE sa w zadnym kursie (orphan — moze tu Trzepalka/Cezar)
PRINT '';
PRINT '─── [A4] Zamowienia awizowane dzis NIE przypisane do kursu ────────';
SELECT
    zm.Id AS ZamId, zm.KlientId, zm.DataPrzyjazdu AS Awizacja,
    zm.LiczbaPalet, zm.LiczbaPojemnikow, zm.TransportStatus
FROM LibraNet.dbo.ZamowieniaMieso zm
WHERE CAST(zm.DataPrzyjazdu AS DATE) = @Dzis
  AND NOT EXISTS (
      SELECT 1 FROM TransportPL.dbo.Ladunek l
      WHERE l.KodKlienta = 'ZAM_' + CAST(zm.Id AS varchar))
ORDER BY zm.DataPrzyjazdu;

PRINT '';
PRINT '═══ KONIEC CZESCI A. Teraz CZESC B na 192.168.0.112 ═══';
GO

-- ╔══════════════════════════════════════════════════════════════════╗
-- ║  CZESC B — uruchom OSOBNO na 192.168.0.112 (HANDEL)             ║
-- ║  Znajdz Id klientow Trzepalka / Cezar / Romanowska              ║
-- ╚══════════════════════════════════════════════════════════════════╝
-- SELECT c.Id, c.Name, c.Shortcut
-- FROM SSCommon.STContractors c
-- WHERE c.Name LIKE '%Trzepa%' OR c.Name LIKE '%Cezar%' OR c.Name LIKE '%Romanowsk%'
-- ORDER BY c.Name;
--
-- Potem porownaj Id z KlientId z sekcji [A2]/[A3] — zobaczysz czy zamowienia
-- Trzepalka/Cezar sa awizowane dzis i czy maja W_Kursie_ID (NULL = nieprzypisane).
