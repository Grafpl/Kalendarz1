-- =============================================================================
-- EKSPLORACJA CROSS-DB - LibraNet ↔ HANDEL ↔ TransportPL
-- =============================================================================
-- Sprawdzamy spójność danych między 3 bazami:
--   - LibraNet.OdbiorcyCRM.ID = HANDEL.STContractors.Id ?
--   - LibraNet.kontrahenci = HANDEL.STContractors ?
--   - LibraNet.Driver = TransportPL.Kierowca ?
--   - LibraNet.CarTrailer = TransportPL.Pojazd ?
--   - ZamowieniaMieso.TransportKursID = TransportPL.Kurs.KursID ?
--   - listapartii.HarmonogramLp = HarmonogramDostaw.Lp ?
--
-- UWAGA: Jeśli nie ma Linked Server, niektóre cross-DB nie zadziałają.
--        LibraNet i TransportPL są na tym samym serwerze (192.168.0.109)
--        więc cross-database działa.
--        HANDEL na 192.168.0.112 - wymaga Linked Server lub osobnego query.
--
-- INSTRUKCJA:
--   1. SSMS -> 192.168.0.109 (pronova/pronova)
--   2. Ctrl+T -> F5 -> Ctrl+A -> Ctrl+C
--   3. Wklej do WYNIKI_CROSS_DB.txt
-- =============================================================================

USE LibraNet;
GO

SET NOCOUNT ON;
GO

PRINT '################################################################';
PRINT '## EKSPLORACJA CROSS-DB - START                               ##';
PRINT '################################################################';
GO

-- =============================================================================
-- BLOK X01 - LibraNet ↔ TransportPL (kierowcy + pojazdy)
-- =============================================================================

-- X01.A - LibraNet.Driver vs TransportPL.Kierowca
SELECT
    'X01_A_drivers' AS __SEKCJA,
    'LibraNet.Driver' AS zrodlo,
    COUNT(*) AS razem
FROM dbo.Driver
UNION ALL
SELECT
    'X01_A_drivers',
    'TransportPL.Kierowca',
    COUNT(*)
FROM TransportPL.dbo.Kierowca;
GO

-- X01.B - Czy ID się pokrywa (Driver vs Kierowca)
SELECT
    'X01_B_drivers_overlap' AS __SEKCJA,
    (SELECT COUNT(*) FROM dbo.Driver d WHERE EXISTS (
        SELECT 1 FROM TransportPL.dbo.Kierowca k WHERE k.KierowcaID = d.ID
    )) AS w_obu;
GO

-- X01.C - LibraNet.CarTrailer vs TransportPL.Pojazd
SELECT
    'X01_C_pojazdy' AS __SEKCJA,
    'LibraNet.CarTrailer' AS zrodlo,
    COUNT(*) AS razem
FROM dbo.CarTrailer
UNION ALL
SELECT
    'X01_C_pojazdy',
    'TransportPL.Pojazd',
    COUNT(*)
FROM TransportPL.dbo.Pojazd;
GO

-- =============================================================================
-- BLOK X02 - ZamowieniaMieso → TransportPL.Kurs (broken FK?)
-- =============================================================================

-- X02.A - Sieroty: ZamowieniaMieso.TransportKursID bez odpowiadającego Kursa
SELECT
    'X02_A_zamowienia_bez_kursu' AS __SEKCJA,
    COUNT(*) AS razem,
    MIN(zm.DataZamowienia) AS najstarsze,
    MAX(zm.DataZamowienia) AS najnowsze
FROM dbo.ZamowieniaMieso zm
WHERE zm.TransportKursID IS NOT NULL
  AND NOT EXISTS (
      SELECT 1 FROM TransportPL.dbo.Kurs k WHERE k.KursID = zm.TransportKursID
  );
GO

-- X02.B - Top kursy z największą liczbą zamówień
SELECT TOP 30
    'X02_B_top_kursy_zamowien' AS __SEKCJA,
    zm.TransportKursID,
    k.DataKursu,
    k.Trasa,
    COUNT(*) AS liczba_zamowien,
    SUM(zm.LiczbaPalet) AS suma_palet
FROM dbo.ZamowieniaMieso zm
JOIN TransportPL.dbo.Kurs k ON k.KursID = zm.TransportKursID
WHERE zm.DataZamowienia >= DATEADD(DAY, -30, GETDATE())
GROUP BY zm.TransportKursID, k.DataKursu, k.Trasa
ORDER BY liczba_zamowien DESC;
GO

-- =============================================================================
-- BLOK X03 - Ladunki TransportPL.Ladunek.KodKlienta = "ZAM_xxx" → ZamowieniaMieso.Id
-- =============================================================================

-- X03.A - Czy ZAM_xxx z Ladunku rzeczywiście istnieją w ZamowieniaMieso
SELECT
    'X03_A_ladunki_zam' AS __SEKCJA,
    COUNT(*) AS razem,
    SUM(CASE WHEN EXISTS (
        SELECT 1 FROM dbo.ZamowieniaMieso zm
        WHERE 'ZAM_' + CAST(zm.Id AS varchar) = l.KodKlienta
    ) THEN 1 ELSE 0 END) AS znalezione,
    SUM(CASE WHEN NOT EXISTS (
        SELECT 1 FROM dbo.ZamowieniaMieso zm
        WHERE 'ZAM_' + CAST(zm.Id AS varchar) = l.KodKlienta
    ) THEN 1 ELSE 0 END) AS niezalezione
FROM TransportPL.dbo.Ladunek l
WHERE l.KodKlienta LIKE 'ZAM_%';
GO

-- =============================================================================
-- BLOK X04 - listapartii.HarmonogramLp → HarmonogramDostaw.LP
-- =============================================================================

-- X04.A - Sieroty: partie z HarmonogramLp ale brak HarmonogramDostaw
SELECT
    'X04_A_partie_bez_harm' AS __SEKCJA,
    COUNT(*) AS razem
FROM dbo.listapartii lp
WHERE lp.HarmonogramLp IS NOT NULL
  AND NOT EXISTS (
      SELECT 1 FROM dbo.HarmonogramDostaw h WHERE h.LP = lp.HarmonogramLp
  );
GO

-- X04.B - Czy HarmonogramLp używane (ile partii ma wpis vs nie)
SELECT
    'X04_B_partie_harm_uzycie' AS __SEKCJA,
    SUM(CASE WHEN HarmonogramLp IS NOT NULL THEN 1 ELSE 0 END) AS z_harm,
    SUM(CASE WHEN HarmonogramLp IS NULL THEN 1 ELSE 0 END) AS bez_harm,
    COUNT(*) AS razem
FROM dbo.listapartii;
GO

-- =============================================================================
-- BLOK X05 - PartiaDostawca.CustomerID → Pozyskiwanie_Hodowcy
-- =============================================================================

-- X05.A - Czy hodowcy z PartiaDostawca są w Pozyskiwanie_Hodowcy
SELECT
    'X05_A_hodowcy_pozyskiwanie' AS __SEKCJA,
    COUNT(DISTINCT pd.CustomerID) AS unikalnych_w_partia,
    COUNT(DISTINCT CASE WHEN ph.CustomerID IS NOT NULL THEN pd.CustomerID END) AS w_pozyskiwanie
FROM dbo.PartiaDostawca pd
LEFT JOIN dbo.Pozyskiwanie_Hodowcy ph ON ph.CustomerID = pd.CustomerID;
GO

-- X05.B - Hodowcy w Pozyskiwanie_Hodowcy ale BEZ partii (CRM lead niezamknięty)
SELECT
    'X05_B_hodowcy_bez_partii' AS __SEKCJA,
    COUNT(*) AS razem
FROM dbo.Pozyskiwanie_Hodowcy ph
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.PartiaDostawca pd WHERE pd.CustomerID = ph.CustomerID
);
GO

-- =============================================================================
-- BLOK X06 - DostawcyAdresy → Dostawcy
-- =============================================================================

SELECT
    'X06_A_dostawcyadresy_overlap' AS __SEKCJA,
    (SELECT COUNT(*) FROM dbo.Dostawcy) AS dostawcy,
    (SELECT COUNT(*) FROM dbo.DostawcyAdresy) AS adresy,
    (SELECT COUNT(*) FROM dbo.DostawcyAdresy da WHERE EXISTS (
        SELECT 1 FROM dbo.Dostawcy d WHERE d.ID = da.DostawcaID
    )) AS adresy_z_dostawca;
GO

-- =============================================================================
-- BLOK X07 - Aktywnosc.KtoStworzyl → operators
-- =============================================================================

-- Czy wszyscy z Aktywnosc istnieją w operators
SELECT
    'X07_A_aktywnosc_operators' AS __SEKCJA,
    a.KtoStworzyl,
    op.Name AS operator_imie,
    COUNT(*) AS liczba_aktywnosci
FROM dbo.Aktywnosc a
LEFT JOIN dbo.operators op ON op.ID = a.KtoStworzyl
WHERE a.Data >= DATEADD(DAY, -30, GETDATE())
GROUP BY a.KtoStworzyl, op.Name
ORDER BY liczba_aktywnosci DESC;
GO

-- =============================================================================
-- BLOK X08 - HistoriaZmianZamowien.Uzytkownik → operators
-- =============================================================================

SELECT TOP 30
    'X08_A_zmiany_uzytkownicy' AS __SEKCJA,
    h.Uzytkownik,
    h.UzytkownikNazwa,
    op.Name AS operator_z_table,
    COUNT(*) AS liczba_zmian
FROM dbo.HistoriaZmianZamowien h
LEFT JOIN dbo.operators op ON CAST(op.ID AS varchar) = h.Uzytkownik
WHERE h.DataZmiany >= DATEADD(DAY, -30, GETDATE())
GROUP BY h.Uzytkownik, h.UzytkownikNazwa, op.Name
ORDER BY liczba_zmian DESC;
GO

-- =============================================================================
-- BLOK X09 - Reklamacje.IdKontrahenta → OdbiorcyCRM lub kontrahenci
-- =============================================================================

-- X09.A - Reklamacje per kontrahent (top 20)
SELECT TOP 20
    'X09_A_reklamacje_per_klient' AS __SEKCJA,
    r.IdKontrahenta,
    r.NazwaKontrahenta,
    COUNT(*) AS liczba_reklamacji,
    SUM(CASE WHEN r.ZrodloZgloszenia <> 'Symfonia' OR r.ZrodloZgloszenia IS NULL THEN 1 ELSE 0 END) AS reczne,
    SUM(r.SumaKg) AS suma_kg,
    SUM(r.SumaWartosc) AS suma_wartosci
FROM dbo.Reklamacje r
WHERE r.DataZgloszenia >= DATEADD(MONTH, -6, GETDATE())
GROUP BY r.IdKontrahenta, r.NazwaKontrahenta
ORDER BY reczne DESC, liczba_reklamacji DESC;
GO

-- X09.B - Sprawdzenie czy IdKontrahenta jest w OdbiorcyCRM/kontrahenci
SELECT
    'X09_B_reklamacje_klient_link' AS __SEKCJA,
    COUNT(DISTINCT r.IdKontrahenta) AS unikalnych_klientow,
    SUM(CASE WHEN EXISTS (SELECT 1 FROM dbo.OdbiorcyCRM o WHERE o.ID = r.IdKontrahenta) THEN 1 ELSE 0 END) AS w_odbiorcyCRM,
    SUM(CASE WHEN EXISTS (SELECT 1 FROM dbo.kontrahenci k WHERE TRY_CAST(k.ID AS int) = r.IdKontrahenta) THEN 1 ELSE 0 END) AS w_kontrahenci
FROM (SELECT DISTINCT IdKontrahenta FROM dbo.Reklamacje) r;
GO

-- =============================================================================
-- BLOK X10 - In0E.OperatorID → operators (sieroty)
-- =============================================================================

SELECT
    'X10_A_in0e_operator_sieroty' AS __SEKCJA,
    e.OperatorID,
    e.Wagowy,
    COUNT(*) AS liczba_wazen
FROM dbo.In0E e
LEFT JOIN dbo.operators op ON CAST(op.ID AS varchar) = e.OperatorID
WHERE e.Data >= CONVERT(varchar(10), DATEADD(DAY, -30, GETDATE()), 120)
  AND e.OperatorID IS NOT NULL
  AND e.OperatorID <> ''
  AND op.ID IS NULL
GROUP BY e.OperatorID, e.Wagowy
ORDER BY liczba_wazen DESC;
GO

-- =============================================================================
-- BLOK X11 - Notatki.AutorID → operators
-- =============================================================================

-- X11.A - Sprawdzenie czy autorzy notatek są w operators
SELECT TOP 20
    'X11_A_notatki_autorzy' AS __SEKCJA,
    n.UtworzylID,
    op.Name AS operator_imie,
    COUNT(*) AS liczba_notatek,
    MIN(n.DataUtworzenia) AS pierwsza,
    MAX(n.DataUtworzenia) AS ostatnia
FROM dbo.Notatki n
LEFT JOIN dbo.operators op ON op.ID = n.UtworzylID
GROUP BY n.UtworzylID, op.Name
ORDER BY liczba_notatek DESC;
GO

-- =============================================================================
-- BLOK X12 - Top dokumenty WZ z LibraNet vs HANDEL (jeśli linked)
-- =============================================================================

-- X12.A - DokumentyWZ struktura
SELECT
    'X12_A_dokumentywz_kolumny' AS __SEKCJA,
    ORDINAL_POSITION, COLUMN_NAME, DATA_TYPE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'DokumentyWZ'
ORDER BY ORDINAL_POSITION;
GO

-- X12.B - Top 30 ostatnich WZ
SELECT TOP 30
    'X12_B_dokumentywz_recent' AS __SEKCJA, *
FROM dbo.DokumentyWZ
ORDER BY 1 DESC;
GO

-- =============================================================================
-- BLOK X13 - WstawieniaKurczakow → HarmonogramDostaw (cykl 35-42 dni)
-- =============================================================================

-- X13.A - Sprawdzenie czy wstawienia są powiązane z harmonogramem przez LpUK/LpU/LpP
SELECT
    'X13_A_wstawienia_links' AS __SEKCJA,
    COUNT(*) AS razem,
    SUM(CASE WHEN LpUK IS NOT NULL THEN 1 ELSE 0 END) AS z_LpUK,
    SUM(CASE WHEN LpU IS NOT NULL THEN 1 ELSE 0 END) AS z_LpU,
    SUM(CASE WHEN LpP IS NOT NULL THEN 1 ELSE 0 END) AS z_LpP
FROM dbo.WstawieniaKurczakow;
GO

-- X13.B - Hodowcy z wstawień vs hodowcy z partii (ostatnie 3 miesiące)
SELECT
    'X13_B_hodowcy_wstaw_vs_partie' AS __SEKCJA,
    COUNT(DISTINCT wk.Dostawca) AS hodowcy_wstawien,
    COUNT(DISTINCT pd.CustomerName) AS hodowcy_partii
FROM dbo.WstawieniaKurczakow wk
FULL OUTER JOIN (
    SELECT DISTINCT CustomerName
    FROM dbo.PartiaDostawca
    WHERE CreateData >= CONVERT(varchar(10), DATEADD(DAY, -90, GETDATE()), 120)
) pd ON wk.Dostawca = pd.CustomerName
WHERE wk.DataWstawienia >= DATEADD(DAY, -180, GETDATE())
   OR wk.DataWstawienia IS NULL;
GO

-- =============================================================================
-- BLOK X14 - WagoCounter → FarmerCalc per CarLp per dzień (rozszerzona walidacja)
-- =============================================================================

SELECT TOP 30
    'X14_A_wago_farmer_match' AS __SEKCJA,
    wc.CalcDate,
    wc.CarLP AS Wago_CarLp,
    fc.CarLp AS Farmer_CarLp,
    wc.Quantity AS wago_szt,
    fc.LumQnt AS farmer_dek,
    fc.Pieces AS farmer_polic,
    fc.NettoWeight AS farmer_kg,
    wc.Quantity - ISNULL(fc.Pieces, 0) AS roznica
FROM dbo.WagoCounter wc
FULL OUTER JOIN dbo.FarmerCalc fc
    ON CAST(fc.CalcDate AS date) = CAST(wc.CalcDate AS date)
   AND fc.CarLp = wc.CarLP
WHERE COALESCE(wc.CalcDate, fc.CalcDate) >= DATEADD(DAY, -7, GETDATE())
ORDER BY COALESCE(wc.CalcDate, fc.CalcDate) DESC, COALESCE(wc.CarLP, fc.CarLp) DESC;
GO

-- =============================================================================
-- BLOK X15 - DocOut0E + HeaderDocOut0E (DOC dokumenty wyjść mroźni)
-- =============================================================================

SELECT
    'X15_A_docout0e_struktura' AS __SEKCJA,
    TABLE_NAME, ORDINAL_POSITION, COLUMN_NAME, DATA_TYPE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME IN ('DocOut0E', 'HeaderDocOut0E')
ORDER BY TABLE_NAME, ORDINAL_POSITION;
GO

SELECT TOP 5 'X15_B_docout0e_sample' AS __SEKCJA, * FROM dbo.DocOut0E ORDER BY 1 DESC;
GO

SELECT TOP 5 'X15_C_headerout0e_sample' AS __SEKCJA, * FROM dbo.HeaderDocOut0E ORDER BY 1 DESC;
GO

PRINT '################################################################';
PRINT '## EKSPLORACJA CROSS-DB - KONIEC                              ##';
PRINT '################################################################';
GO
