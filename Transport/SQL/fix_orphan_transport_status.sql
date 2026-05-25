-- ═══════════════════════════════════════════════════════════════════════════
-- FIX — zamowienia z TransportStatus='Przypisany' ale BEZ ladunku w kursie
-- ═══════════════════════════════════════════════════════════════════════════
-- Problem (zdiagnozowany na przykl. Cezar ZAM_6203): zamowienie ma
-- TransportStatus='Przypisany' w LibraNet, ale NIE ma odpowiadajacego rekordu
-- w TransportPL.Ladunek → "sierota". W widoku zamowien wyglada na przypisane,
-- ale nie ma go w zadnym kursie. Przyczyna: bug w edytorze kursu (juz poprawiony)
-- ustawial status dla _zamowieniaDoDodania nawet gdy ladunek znikal z _ladunki.
--
-- Ten skrypt RESETUJE takie sieroty na 'Oczekuje' → wracaja do puli wolnych
-- zamowien i mozna je ponownie przypisac do kursu.
--
-- Uruchom w SSMS na 192.168.0.109. Krok [1] = podglad, [2] = naprawa.
-- ═══════════════════════════════════════════════════════════════════════════

SET NOCOUNT ON;

-- ════════════════════════════════════════════════════════════════════
-- [1] PODGLAD — sieroty (Przypisany w LibraNet, brak Ladunek w TransportPL)
-- ════════════════════════════════════════════════════════════════════
PRINT '─── [1] Sieroty: TransportStatus=Przypisany BEZ ladunku ───────────';
SELECT
    zm.Id            AS ZamId,
    zm.KlientId,
    zm.DataPrzyjazdu AS Awizacja,
    zm.TransportStatus,
    zm.TransportKursId,
    -- czy kurs na ktory wskazuje TransportKursId jeszcze istnieje?
    CASE WHEN k.KursID IS NULL THEN 'KURS NIE ISTNIEJE' ELSE 'kurs istnieje: ' + k.Trasa END AS Kurs_Stan
FROM LibraNet.dbo.ZamowieniaMieso zm
LEFT JOIN TransportPL.dbo.Kurs k ON k.KursID = zm.TransportKursId
WHERE zm.TransportStatus = 'Przypisany'
  AND NOT EXISTS (
      SELECT 1 FROM TransportPL.dbo.Ladunek l
      WHERE l.KodKlienta = 'ZAM_' + CAST(zm.Id AS varchar))
  AND CAST(zm.DataPrzyjazdu AS DATE) >= DATEADD(DAY, -7, CAST(GETDATE() AS DATE))
ORDER BY zm.DataPrzyjazdu DESC;

PRINT '';
PRINT 'Powyzej: zamowienia ktore widac jako Przypisane ale NIE sa w zadnym kursie.';
PRINT 'Jesli pusto — brak sierot, system spojny.';

-- ════════════════════════════════════════════════════════════════════
-- [2] NAPRAWA — reset sierot na 'Oczekuje' (odkomentuj zeby wykonac)
-- ════════════════════════════════════════════════════════════════════
PRINT '';
PRINT '─── [2] Naprawa (zakomentowana — odkomentuj BEGIN/COMMIT zeby wykonac) ──';

/*  ← USUN te 2 znaki na poczatku i na koncu bloku zeby wykonac naprawe

BEGIN TRANSACTION;

UPDATE zm
SET zm.TransportStatus = 'Oczekuje',
    zm.TransportKursId = NULL
FROM LibraNet.dbo.ZamowieniaMieso zm
WHERE zm.TransportStatus = 'Przypisany'
  AND NOT EXISTS (
      SELECT 1 FROM TransportPL.dbo.Ladunek l
      WHERE l.KodKlienta = 'ZAM_' + CAST(zm.Id AS varchar))
  AND CAST(zm.DataPrzyjazdu AS DATE) >= DATEADD(DAY, -7, CAST(GETDATE() AS DATE));

PRINT CONCAT('Zresetowano sierot: ', @@ROWCOUNT);

COMMIT;

*/  ← USUN te 2 znaki

PRINT '';
PRINT 'Po naprawie: sieroty wroca do puli wolnych zamowien (status Oczekuje)';
PRINT 'i bedzie je mozna ponownie dodac do kursu w edytorze.';
PRINT '';
PRINT '════════════════════════════════════════════════════════════════════';
