-- =============================================================================
-- EKSPLORACJA HANDEL (Symfonia 192.168.0.112) - RUNDA 2
-- =============================================================================
-- Cele:
--   - HM.OP (1.34M wierszy) - co to operacje?
--   - FK.zapisy (1.16M) - księga główna
--   - HM.PW (1.03M) - przyjęcia wewnętrzne
--   - HM.MZ.ProductionLineID - czy 100% NULL
--   - HM.DK per typ + per kontrahent (top 30)
--   - SSCommon.STContractors - aktywni vs nieaktywni
--   - Sprzedaz: dokumenty FVS sample
--   - Cross-check: ID kontrahentów w LibraNet vs HANDEL
--   - Top klienci per wartosc faktur
--   - Top towary per ilosc sprzedaz
--   - Wymagi (CDim_Handlowiec_Val) - top handlowcy w HANDEL
--   - WF_Piorkowscy (workflow) - co tam jest
--
-- INSTRUKCJA:
--   1. SSMS -> 192.168.0.112 (sa / ?cs_'Y6,n5#Xd'Yd)
--   2. Ctrl+T -> F5 -> Ctrl+A -> Ctrl+C
--   3. Wklej do WYNIKI_HANDEL_2.txt
-- =============================================================================

USE Handel;
GO

SET NOCOUNT ON;
GO

-- =============================================================================
-- BLOK H21 - HM.OP (1.34M wierszy - co to)
-- =============================================================================

SELECT
    'H21_A_op_kolumny' AS __SEKCJA,
    ORDINAL_POSITION, COLUMN_NAME, DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = 'HM' AND TABLE_NAME = 'OP'
ORDER BY ORDINAL_POSITION;
GO

SELECT TOP 5 'H21_B_op_sample' AS __SEKCJA, * FROM HM.OP ORDER BY id DESC;
GO

-- =============================================================================
-- BLOK H22 - FK.zapisy (1.16M - księga główna)
-- =============================================================================

SELECT
    'H22_A_fk_zapisy_kolumny' AS __SEKCJA,
    ORDINAL_POSITION, COLUMN_NAME, DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = 'FK' AND TABLE_NAME = 'zapisy'
ORDER BY ORDINAL_POSITION;
GO

SELECT TOP 5 'H22_B_fk_zapisy_sample' AS __SEKCJA, * FROM FK.zapisy ORDER BY id DESC;
GO

-- =============================================================================
-- BLOK H23 - HM.PW (1.03M - przyjęcia wewnętrzne)
-- =============================================================================

SELECT
    'H23_A_pw_kolumny' AS __SEKCJA,
    ORDINAL_POSITION, COLUMN_NAME, DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = 'HM' AND TABLE_NAME = 'PW'
ORDER BY ORDINAL_POSITION;
GO

SELECT TOP 5 'H23_B_pw_sample' AS __SEKCJA, * FROM HM.PW ORDER BY id DESC;
GO

-- =============================================================================
-- BLOK H24 - HM.MZ pełniej (907k zapasów)
-- =============================================================================

SELECT
    'H24_A_mz_kolumny_full' AS __SEKCJA,
    ORDINAL_POSITION, COLUMN_NAME, DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = 'HM' AND TABLE_NAME = 'MZ'
ORDER BY ORDINAL_POSITION;
GO

-- H24.B - HM.MZ.ProductionLineID dystrybucja (czy 100% NULL?)
SELECT
    'H24_B_mz_productionline' AS __SEKCJA,
    ProductionLineID,
    COUNT(*) AS razem
FROM HM.MZ
GROUP BY ProductionLineID
ORDER BY razem DESC;
GO

-- H24.C - HM.MZ sample 5 ostatnich
SELECT TOP 5 'H24_C_mz_sample' AS __SEKCJA, * FROM HM.MZ ORDER BY id DESC;
GO

-- =============================================================================
-- BLOK H25 - HM.DK serie + typy + per kontrahent
-- =============================================================================

-- H25.A - Wszystkie unikalne typy + serie (cały okres)
SELECT
    'H25_A_dk_typy_full' AS __SEKCJA,
    typ_dk,
    seria,
    COUNT(*) AS razem,
    MIN(data) AS pierwsza,
    MAX(data) AS ostatnia
FROM HM.DK
GROUP BY typ_dk, seria
ORDER BY razem DESC;
GO

-- H25.B - Top 30 kontrahentów po liczbie dokumentów (90 dni)
SELECT TOP 30
    'H25_B_top_kontrahenci' AS __SEKCJA,
    dk.khid,
    c.Shortcut,
    c.Name1,
    COUNT(*) AS liczba_dokumentow,
    SUM(dk.walbrutto) AS suma_brutto
FROM HM.DK dk
LEFT JOIN [SSCommon].STContractors c ON c.Id = dk.khid
WHERE dk.data >= DATEADD(DAY, -90, GETDATE())
  AND dk.aktywny = 1
  AND dk.typ_dk IN ('FVS', 'FVR', 'FVZ')
GROUP BY dk.khid, c.Shortcut, c.Name1
ORDER BY liczba_dokumentow DESC;
GO

-- =============================================================================
-- BLOK H26 - HM.DP per produkt (linie faktur)
-- =============================================================================

-- Top 30 produktów per wartość sprzedaży 90 dni
SELECT TOP 30
    'H26_A_top_produkty_sprzedaz' AS __SEKCJA,
    dp.idtw,
    tw.kod,
    tw.nazwa,
    SUM(dp.ilosc) AS suma_ilosci,
    SUM(dp.cena * dp.ilosc) AS suma_wartosci,
    AVG(dp.cena) AS sr_cena,
    COUNT(DISTINCT dp.super) AS liczba_dokumentow
FROM HM.DP dp
JOIN HM.DK dk ON dk.id = dp.super
LEFT JOIN HM.TW tw ON tw.ID = dp.idtw
WHERE dk.data >= DATEADD(DAY, -90, GETDATE())
  AND dk.aktywny = 1
  AND dk.typ_dk = 'FVS'
GROUP BY dp.idtw, tw.kod, tw.nazwa
ORDER BY suma_wartosci DESC;
GO

-- =============================================================================
-- BLOK H27 - SSCommon.STContractors aktywni vs nieaktywni
-- =============================================================================

-- H27.A - Statystyki
SELECT
    'H27_A_kontrahenci_stats' AS __SEKCJA,
    COUNT(*) AS razem,
    SUM(CASE WHEN c.Inactive = 0 THEN 1 ELSE 0 END) AS aktywni,
    SUM(CASE WHEN c.Inactive = 1 THEN 1 ELSE 0 END) AS nieaktywni,
    SUM(CASE WHEN c.IsClient = 1 THEN 1 ELSE 0 END) AS klienci,
    SUM(CASE WHEN c.IsSupplier = 1 THEN 1 ELSE 0 END) AS dostawcy
FROM [SSCommon].STContractors c;
GO

-- H27.B - Top 30 handlowców (wymiar Handlowiec)
SELECT TOP 30
    'H27_B_handlowcy_top' AS __SEKCJA,
    wym.CDim_Handlowiec_Val AS Handlowiec,
    COUNT(DISTINCT wym.ElementId) AS liczba_klientow
FROM [SSCommon].ContractorClassification wym
WHERE wym.CDim_Handlowiec_Val IS NOT NULL
  AND wym.CDim_Handlowiec_Val <> ''
GROUP BY wym.CDim_Handlowiec_Val
ORDER BY liczba_klientow DESC;
GO

-- =============================================================================
-- BLOK H28 - Sprzedaż per miesiąc (FVS)
-- =============================================================================

SELECT
    'H28_A_sprzedaz_per_miesiac' AS __SEKCJA,
    YEAR(data) AS rok,
    MONTH(data) AS miesiac,
    typ_dk,
    COUNT(*) AS liczba_dokumentow,
    SUM(walbrutto) AS suma_brutto
FROM HM.DK
WHERE data >= DATEADD(MONTH, -12, GETDATE())
  AND aktywny = 1
  AND typ_dk IN ('FVS', 'FVR', 'FVZ', 'FKS', 'FKSB', 'FWK')
GROUP BY YEAR(data), MONTH(data), typ_dk
ORDER BY rok DESC, miesiac DESC, typ_dk;
GO

-- =============================================================================
-- BLOK H29 - HM.PN (płatności) - kto ile zalega
-- =============================================================================

SELECT
    'H29_A_pn_kolumny' AS __SEKCJA,
    ORDINAL_POSITION, COLUMN_NAME, DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = 'HM' AND TABLE_NAME = 'PN'
ORDER BY ORDINAL_POSITION;
GO

-- H29.B - Top 30 klientów z największym zaległościami
SELECT TOP 30
    'H29_B_top_dluznicy' AS __SEKCJA,
    dk.khid,
    c.Shortcut,
    c.Name1,
    COUNT(*) AS liczba_faktur,
    SUM(dk.walbrutto) AS suma_brutto,
    SUM(ISNULL(pn.kwotarozl, 0)) AS rozliczone,
    SUM(dk.walbrutto - ISNULL(pn.kwotarozl, 0)) AS zaleglosc
FROM HM.DK dk
LEFT JOIN (SELECT dkid, SUM(kwotarozl) AS kwotarozl FROM HM.PN GROUP BY dkid) pn ON pn.dkid = dk.id
LEFT JOIN [SSCommon].STContractors c ON c.Id = dk.khid
WHERE dk.aktywny = 1
  AND dk.typ_dk IN ('FVS', 'FVR', 'FVZ')
  AND dk.data >= DATEADD(MONTH, -6, GETDATE())
  AND dk.plattermin < GETDATE()
GROUP BY dk.khid, c.Shortcut, c.Name1
HAVING SUM(dk.walbrutto - ISNULL(pn.kwotarozl, 0)) > 1000
ORDER BY zaleglosc DESC;
GO

-- =============================================================================
-- BLOK H30 - HM.TW pełna kartoteka towarów (top 50 per katalog)
-- =============================================================================

SELECT
    'H30_A_tw_kolumny_full' AS __SEKCJA,
    ORDINAL_POSITION, COLUMN_NAME, DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = 'HM' AND TABLE_NAME = 'TW'
ORDER BY ORDINAL_POSITION;
GO

-- H30.B - Towary mięsne (katalog 67095, 67153) z aktywnością
SELECT TOP 50
    'H30_B_tw_miesne' AS __SEKCJA,
    tw.ID, tw.kod, tw.nazwa, tw.katalog, tw.jm,
    (SELECT COUNT(*) FROM HM.DP WHERE DP.idtw = tw.ID) AS liczba_pozycji_doc
FROM HM.TW tw
WHERE tw.katalog IN (67095, 67153)
ORDER BY tw.katalog, tw.kod;
GO

-- =============================================================================
-- BLOK H31 - WF_Piorkowscy (workflow Symfonia, od 2026-02-19)
-- =============================================================================

USE WF_Piorkowscy;
GO

-- H31.A - Lista tabel
SELECT TOP 30
    'H31_A_wf_tabele' AS __SEKCJA,
    s.name + '.' + t.name AS pelna_nazwa,
    p.rows AS row_count
FROM sys.tables t
JOIN sys.schemas s ON s.schema_id = t.schema_id
JOIN sys.partitions p ON p.object_id = t.object_id AND p.index_id IN (0,1)
ORDER BY p.rows DESC;
GO

USE Handel;
GO

-- =============================================================================
-- BLOK H32 - Reklamacje pochodzace z Symfonii (FKS, FKSB, FWK)
-- =============================================================================

SELECT
    'H32_A_korekty_per_handlowiec' AS __SEKCJA,
    YEAR(dk.data) AS rok,
    MONTH(dk.data) AS miesiac,
    dk.typ_dk,
    wym.CDim_Handlowiec_Val AS Handlowiec,
    COUNT(*) AS liczba_korekt,
    SUM(dk.walbrutto) AS suma_brutto
FROM HM.DK dk
LEFT JOIN [SSCommon].ContractorClassification wym ON wym.ElementId = dk.khid
WHERE dk.aktywny = 1
  AND dk.typ_dk IN ('FKS', 'FKSB', 'FWK')
  AND dk.data >= DATEADD(MONTH, -6, GETDATE())
GROUP BY YEAR(dk.data), MONTH(dk.data), dk.typ_dk, wym.CDim_Handlowiec_Val
ORDER BY rok DESC, miesiac DESC, liczba_korekt DESC;
GO

-- =============================================================================
-- BLOK H33 - HM.MZ + HM.MG: aktywność magazynów per dzień (30 dni)
-- =============================================================================

SELECT TOP 30
    'H33_A_magazyny_dziennie' AS __SEKCJA,
    mg.data AS data_,
    mg.seria,
    COUNT(*) AS liczba_dokumentow,
    SUM(ABS(mz.ilosc)) AS suma_ilosci
FROM HM.MZ mz
JOIN HM.MG mg ON mg.id = mz.super
WHERE mg.aktywny = 1
  AND mg.data >= DATEADD(DAY, -30, GETDATE())
  AND mg.seria IN ('sWZ', 'sWZ-W', 'sPWP', 'PWP', 'sPZ', 'sPWU', 'RWP')
GROUP BY mg.data, mg.seria
ORDER BY mg.data DESC, liczba_dokumentow DESC;
GO

-- H33.B - Per magazyn (kolumna magazyn lub mag w MZ)
SELECT
    'H33_B_per_magazyn' AS __SEKCJA,
    mg.kod AS NumerDok,
    mg.seria,
    mg.data,
    COUNT(*) AS pozycji
FROM HM.MZ mz
JOIN HM.MG mg ON mg.id = mz.super
WHERE mg.aktywny = 1
  AND mg.data = CONVERT(varchar(10), GETDATE(), 120)
GROUP BY mg.kod, mg.seria, mg.data
ORDER BY mg.seria, mg.kod;
GO

PRINT 'EKSPLORACJA HANDEL RUNDA 2 - KONIEC';
GO
