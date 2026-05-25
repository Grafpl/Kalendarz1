-- ═══════════════════════════════════════════════════════════════════════════
-- FIX — zamowienia "duchy": niespojnosc TransportStatus / TransportKursId vs Ladunek
-- ═══════════════════════════════════════════════════════════════════════════
-- Stan przypisania zyje w 2 bazach na .109 i MUSI byc spojny:
--   LibraNet.ZamowieniaMieso: TransportStatus ('Oczekuje'/'Przypisany') + TransportKursId
--   TransportPL.Ladunek:      KodKlienta = 'ZAM_{id}' (faktyczny ladunek w kursie)
--
-- DWIE KLASY sierot (obie = brak Ladunek):
--   [A] CEZAR:     TransportStatus='Przypisany', TransportKursId ustawiony, brak ladunku
--                  → widac jako "przypisane", ale nie ma w zadnym kursie.
--   [B] TRZEPALKA: TransportStatus='Oczekuje',  TransportKursId USTAWIONY, brak ladunku
--                  → DUCH: edytor wyklucza z puli wolnych (warunek !hasKursId),
--                    a w zadnym kursie tez go nie ma. Niewidoczny wszedzie.
--
-- UNIWERSALNY warunek sieroty:
--   (TransportStatus='Przypisany' LUB TransportKursId IS NOT NULL) AND brak Ladunek
--
-- Przyczyna naprawiona w transport-editor.cs (commity 6a326bf + e59126c auto-healing),
-- ale PRE-EXISTING duchy trzeba zresetowac tym skryptem.
--
-- Uruchom w SSMS na 192.168.0.109. [1]+[2] = podglad, [3] = naprawa.
-- ═══════════════════════════════════════════════════════════════════════════

SET NOCOUNT ON;

-- ════════════════════════════════════════════════════════════════════
-- [1] PODGLAD — WSZYSTKIE duchy (obie klasy A i B), ostatnie 7 dni
-- ════════════════════════════════════════════════════════════════════
PRINT '─── [1] Duchy: (Przypisany LUB TransportKursId ustawiony) BEZ ladunku ───';
SELECT
    zm.Id            AS ZamId,
    zm.KlientId,
    zm.DataPrzyjazdu AS Awizacja,
    zm.TransportStatus,
    zm.TransportKursId,
    CASE
        WHEN zm.TransportStatus = 'Przypisany' THEN 'A: Przypisany bez ladunku'
        ELSE 'B: DUCH (Oczekuje + KursId, niewidoczny w puli)'
    END AS Klasa,
    CASE WHEN k.KursID IS NULL THEN 'KURS NIE ISTNIEJE' ELSE 'kurs: ' + k.Trasa END AS Kurs_Stan
FROM LibraNet.dbo.ZamowieniaMieso zm
LEFT JOIN TransportPL.dbo.Kurs k ON k.KursID = zm.TransportKursId
WHERE (zm.TransportStatus = 'Przypisany' OR zm.TransportKursId IS NOT NULL)
  AND NOT EXISTS (
      SELECT 1 FROM TransportPL.dbo.Ladunek l
      WHERE l.KodKlienta = 'ZAM_' + CAST(zm.Id AS varchar))
  AND CAST(zm.DataPrzyjazdu AS DATE) >= DATEADD(DAY, -7, CAST(GETDATE() AS DATE))
ORDER BY zm.DataPrzyjazdu DESC;

PRINT '';
PRINT 'Klasa A = widac jako przypisane. Klasa B = niewidoczne wszedzie (duch).';
PRINT 'Jesli pusto — brak sierot, system spojny.';

-- ════════════════════════════════════════════════════════════════════
-- [2] FOKUS na Trzepalka — pelny rzut zamowienia (KlientId 931) + dlaczego ukryte
--     (zmien @ZamId jesli inny; nazwa klienta jest w HANDEL .112 — tu po KlientId)
-- ════════════════════════════════════════════════════════════════════
PRINT '';
PRINT '─── [2] Trzepalka (KlientId 931) — pelny stan + warunki puli ──────';
SELECT
    zm.Id AS ZamId, zm.KlientId, zm.DataPrzyjazdu AS Awizacja,
    zm.DataUboju, zm.DataZamowienia,
    zm.Status AS Zam_Status, zm.TransportStatus, zm.TransportKursId,
    -- czy ma ladunek gdziekolwiek?
    CASE WHEN EXISTS (SELECT 1 FROM TransportPL.dbo.Ladunek l
                      WHERE l.KodKlienta = 'ZAM_' + CAST(zm.Id AS varchar))
         THEN 'TAK' ELSE 'NIE' END AS Ma_Ladunek,
    -- dlaczego (nie) widoczny w puli wolnych:
    CASE
        WHEN zm.TransportStatus = 'Przypisany' THEN 'UKRYTY: status=Przypisany'
        WHEN zm.TransportKursId IS NOT NULL     THEN 'UKRYTY: TransportKursId ustawiony (!hasKursId)'
        WHEN ISNULL(zm.Status,'Nowe') = 'Anulowane' THEN 'UKRYTY: zamowienie Anulowane'
        ELSE 'WIDOCZNY w puli (jesli data w oknie kursu)'
    END AS Widocznosc_w_puli
FROM LibraNet.dbo.ZamowieniaMieso zm
WHERE zm.KlientId = 931
  AND CAST(zm.DataPrzyjazdu AS DATE) >= DATEADD(DAY, -7, CAST(GETDATE() AS DATE))
ORDER BY zm.DataPrzyjazdu DESC;

-- ════════════════════════════════════════════════════════════════════
-- [3] NAPRAWA — reset WSZYSTKICH duchow (A i B) na 'Oczekuje' + KursId=NULL
--     (odkomentuj BEGIN/COMMIT zeby wykonac)
-- ════════════════════════════════════════════════════════════════════
PRINT '';
PRINT '─── [3] Naprawa (zakomentowana — odkomentuj BEGIN/COMMIT zeby wykonac) ──';

/*  ← USUN te 2 znaki na poczatku i na koncu bloku zeby wykonac naprawe

BEGIN TRANSACTION;

UPDATE zm
SET zm.TransportStatus = 'Oczekuje',
    zm.TransportKursId = NULL
FROM LibraNet.dbo.ZamowieniaMieso zm
WHERE (zm.TransportStatus = 'Przypisany' OR zm.TransportKursId IS NOT NULL)
  AND NOT EXISTS (
      SELECT 1 FROM TransportPL.dbo.Ladunek l
      WHERE l.KodKlienta = 'ZAM_' + CAST(zm.Id AS varchar))
  AND CAST(zm.DataPrzyjazdu AS DATE) >= DATEADD(DAY, -7, CAST(GETDATE() AS DATE));

PRINT CONCAT('Zresetowano duchow (A+B): ', @@ROWCOUNT);

COMMIT;

*/  ← USUN te 2 znaki

PRINT '';
PRINT 'Po naprawie: duchy (Cezar typ A + Trzepalka typ B) wroca do puli wolnych';
PRINT 'zamowien (Oczekuje, KursId=NULL) i bedzie je mozna dodac do kursu w edytorze.';
PRINT '';
PRINT '════════════════════════════════════════════════════════════════════';
