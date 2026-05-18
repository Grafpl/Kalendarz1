/* ═══════════════════════════════════════════════════════════════════════════════
   EKSPLORACJA MAGAZYNÓW W BAZIE HANDEL (Sage Symfonia) — V2 z self-JOIN
   ───────────────────────────────────────────────────────────────────────────────
   KLUCZOWE ODKRYCIE: HM.MG to polimorficzna tabela. Trzyma dokumenty
   (typ=201), foldery (typ=110, "Dokumenty magazynowe"), kategorie (typ=102),
   ORAZ magazyny (typ=??? — to znajdziemy poniżej).

   Kolumna HM.MZ.magazyn wskazuje na HM.MG.id rekordu opisującego magazyn
   (a nie na osobną tabelę słownikową).

   Server:  192.168.0.112
   Baza:    HANDEL
   ═══════════════════════════════════════════════════════════════════════════════ */

USE [HANDEL];
GO

/* ═════ 1) DETEKCJA: znajdź typ używany dla magazynów w HM.MG ═════════════════ */

PRINT '═══ 1. Statystyka typ-ów w HM.MG (jaki typ to magazyn?) ═══';
SELECT
    MG.typ                                         AS Typ,
    MG.subtyp                                      AS Subtyp,
    COUNT(*)                                       AS Liczba,
    MIN(MG.id)                                     AS MinId,
    MAX(MG.id)                                     AS MaxId,
    -- Przykładowe kody (do identyfikacji)
    STUFF((
        SELECT TOP 5 ', ' + ISNULL(MG2.kod, '<NULL>')
        FROM HM.MG MG2
        WHERE MG2.typ = MG.typ AND MG2.subtyp = MG.subtyp
        ORDER BY MG2.id
        FOR XML PATH(''), TYPE
    ).value('.', 'nvarchar(max)'), 1, 2, '')        AS PrzykladoweKody
FROM HM.MG MG
GROUP BY MG.typ, MG.subtyp
ORDER BY COUNT(*) DESC;
GO

/* ═════ 2) GŁÓWNE — pobierz nazwy magazynów po self-JOIN ═════════════════════ */

PRINT '═══ 2. Magazyny + ich nazwy (self-JOIN HM.MZ.magazyn → HM.MG.id) ═══';
SELECT
    MAG.id                              AS MagazynID,
    MAG.kod                             AS Kod,
    MAG.nazwa                           AS Nazwa,
    MAG.opis                            AS Opis,
    MAG.typ                             AS Typ,
    MAG.subtyp                          AS Subtyp,
    MAG.aktywny                         AS Aktywny,
    MAG.flag                            AS Flag,
    -- Statystyki użycia
    (SELECT COUNT(*)
     FROM HM.MZ MZ
     INNER JOIN HM.MG MGD ON MGD.id = MZ.super
     WHERE MZ.magazyn = MAG.id AND MGD.anulowany = 0)  AS LiczbaPozycji,
    (SELECT CAST(SUM(ABS(MZ.ilosc)) AS decimal(18,1))
     FROM HM.MZ MZ
     INNER JOIN HM.MG MGD ON MGD.id = MZ.super
     WHERE MZ.magazyn = MAG.id AND MGD.anulowany = 0)  AS SumaKgAbs
FROM HM.MG MAG
WHERE MAG.id IN (
    SELECT DISTINCT MZ.magazyn
    FROM HM.MZ MZ
    WHERE MZ.magazyn IS NOT NULL
)
ORDER BY MAG.id;
GO

/* ═════ 3) FALLBACK — szczegóły każdego konkretnego ID magazynu ═════════════ */

PRINT '═══ 3. Szczegółowe wiersze dla każdego znalezionego magazynu ═══';
-- Pełen dump 12 znalezionych magazynów (każda kolumna)
SELECT *
FROM HM.MG
WHERE id IN (65547, 65550, 65551, 65552, 65554, 65555, 65556, 65559,
             65562, 65564, 65566, 65543, 65882, 65883)
ORDER BY id;
GO

/* ═════ 4) SKONSOLIDOWANY FINAŁ — gotowe mapowanie ID → Nazwa ═══════════════ */

PRINT '═══ 4. FINALNE MAPOWANIE: ID → Nazwa magazynu + statystyki ═══';
WITH UzywaneMagazyny AS (
    SELECT MZ.magazyn                              AS MagazynID,
           COUNT(*)                                AS LiczbaPozycji,
           COUNT(DISTINCT MG.id)                   AS LiczbaDokumentow,
           CAST(SUM(ABS(MZ.ilosc)) AS decimal(18,1)) AS SumaKg,
           MIN(MG.data)                            AS DataOd,
           MAX(MG.data)                            AS DataDo
    FROM HM.MZ MZ
    INNER JOIN HM.MG MG ON MG.id = MZ.super
    WHERE MG.anulowany = 0 AND MZ.magazyn IS NOT NULL
    GROUP BY MZ.magazyn
)
SELECT
    um.MagazynID,
    MAG.kod                             AS Kod,
    MAG.nazwa                           AS Nazwa,
    MAG.opis                            AS Opis,
    um.LiczbaPozycji,
    um.LiczbaDokumentow,
    um.SumaKg,
    CAST(um.DataOd AS date)             AS DataOd,
    CAST(um.DataDo AS date)             AS DataDo,
    DATEDIFF(DAY, um.DataDo, GETDATE()) AS DniOdOstatniegoUzycia
FROM UzywaneMagazyny um
LEFT JOIN HM.MG MAG ON MAG.id = um.MagazynID
ORDER BY um.SumaKg DESC;
GO

/* ═════ 5) BACKUP — gdyby kolumny kod/nazwa były puste, sprawdź inne pola ═══ */

PRINT '═══ 5. WSZYSTKIE pola tekstowe dla magazynów (jakby kod/nazwa były puste) ═══';
SELECT
    MAG.id                              AS MagazynID,
    MAG.kod                             AS Kod,
    MAG.nazwa                           AS Nazwa,
    MAG.opis                            AS Opis,
    MAG.info                            AS Info,
    MAG.seria                           AS Seria,
    MAG.seriadzial                      AS SeriaDzial,
    MAG.typ_dk                          AS TypDk,
    MAG.schemat                         AS Schemat,
    MAG.guid                            AS GUID
FROM HM.MG MAG
WHERE MAG.id IN (65547, 65550, 65551, 65552, 65554, 65555, 65556, 65559,
                 65562, 65564, 65566, 65543, 65882, 65883)
ORDER BY MAG.id;
GO

PRINT '';
PRINT '═══════════════════════════════════════════════════════════════════════════';
PRINT '  Gotowe. Sprawdź wyniki:';
PRINT '  • pkt 1 → który typ HM.MG to magazyny (zobaczysz typ z 12-15 rekordami)';
PRINT '  • pkt 2 → najważniejsze: ID + Kod + Nazwa magazynu (z self-JOIN)';
PRINT '  • pkt 3 → pełny dump (jakby kod/nazwa były puste, zobaczysz info gdzie)';
PRINT '  • pkt 4 → finalne mapowanie do wkleenia w MagazynyHelper.cs';
PRINT '  • pkt 5 → wszystkie pola tekstowe (info, opis, etc.)';
PRINT '═══════════════════════════════════════════════════════════════════════════';
