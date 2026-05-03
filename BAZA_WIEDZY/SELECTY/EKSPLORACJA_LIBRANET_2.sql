-- =============================================================================
-- EKSPLORACJA LIBRANET - RUNDA 2
-- =============================================================================
-- Plik powstal po analizie runda 1 (WYNIKI_ANALIZA_RUNDA1.md).
-- Idziemy glebiej w odkryte zagadki:
--   - WagoCounter (8168 wierszy! Wago JEST w bazie)
--   - 3 tabele klientow (OdbiorcyCRM, TymczasowiOdbiorcy, kontrahenci)
--   - Reklamacje (7 tabel)
--   - QC widoki (vw_QC_*)
--   - HR/KG moduly
--   - State0E, Aktywnosc, EtykietyZbiorcze (duze tabele bez kontekstu)
--   - Out1A - czy zywa
--   - Notatki ekosystem
--
-- INSTRUKCJA: Identyczna jak RUNDA 1.
--   1. SSMS -> 192.168.0.109 (pronova/pronova)
--   2. Ctrl+T
--   3. F5
--   4. Ctrl+A, Ctrl+C
--   5. Wklej do WYNIKI_RAW_2.txt miedzy znaczniki
--
-- =============================================================================

USE LibraNet;
GO

SET NOCOUNT ON;
GO

PRINT '################################################################';
PRINT '## EKSPLORACJA LIBRANET RUNDA 2 - START                       ##';
PRINT '################################################################';
GO

-- =============================================================================
-- BLOK 21 - WAGOCOUNTER (Wago jest w bazie!)
-- =============================================================================

SELECT
    '21_A_wagocounter_kolumny' AS __SEKCJA,
    ORDINAL_POSITION, COLUMN_NAME, DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'WagoCounter'
ORDER BY ORDINAL_POSITION;
GO

SELECT TOP 30 '21_B_wagocounter_sample' AS __SEKCJA, *
FROM dbo.WagoCounter
ORDER BY 1 DESC;
GO

-- 21.C - Zakres dat wagocounter
DECLARE @sql nvarchar(max);
SELECT @sql = STRING_AGG('MIN(CAST(' + COLUMN_NAME + ' AS varchar(40))) AS [min_' + COLUMN_NAME + ']', ', ')
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'WagoCounter'
  AND DATA_TYPE IN ('datetime', 'datetime2', 'date');

IF @sql IS NOT NULL
BEGIN
    SET @sql = 'SELECT ''21_C_wagocounter_zakres'' AS __SEKCJA, ' + @sql + ', COUNT(*) AS razem FROM dbo.WagoCounter';
    EXEC(@sql);
END
GO

-- =============================================================================
-- BLOK 22 - STATE0E (101k wierszy, co to)
-- =============================================================================

SELECT
    '22_A_state0e_kolumny' AS __SEKCJA,
    ORDINAL_POSITION, COLUMN_NAME, DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'State0E'
ORDER BY ORDINAL_POSITION;
GO

SELECT TOP 20 '22_B_state0e_sample' AS __SEKCJA, *
FROM dbo.State0E
ORDER BY 1 DESC;
GO

-- =============================================================================
-- BLOK 23 - AKTYWNOSC (185k wierszy)
-- =============================================================================

SELECT
    '23_A_aktywnosc_kolumny' AS __SEKCJA,
    ORDINAL_POSITION, COLUMN_NAME, DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'Aktywnosc'
ORDER BY ORDINAL_POSITION;
GO

SELECT TOP 20 '23_B_aktywnosc_sample' AS __SEKCJA, *
FROM dbo.Aktywnosc
ORDER BY 1 DESC;
GO

-- =============================================================================
-- BLOK 24 - ETYKIETYZBIORCZE (36k wierszy)
-- =============================================================================

SELECT
    '24_A_etykiety_kolumny' AS __SEKCJA,
    ORDINAL_POSITION, COLUMN_NAME, DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'EtykietyZbiorcze'
ORDER BY ORDINAL_POSITION;
GO

SELECT TOP 20 '24_B_etykiety_sample' AS __SEKCJA, *
FROM dbo.EtykietyZbiorcze
ORDER BY 1 DESC;
GO

-- =============================================================================
-- BLOK 25 - 3 TABELE KLIENTOW (OdbiorcyCRM, TymczasowiOdbiorcy, kontrahenci)
-- =============================================================================

SELECT
    '25_A_klienci_kolumny' AS __SEKCJA,
    TABLE_NAME, ORDINAL_POSITION, COLUMN_NAME, DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME IN ('OdbiorcyCRM', 'TymczasowiOdbiorcy', 'kontrahenci',
                     'OdbiorcyCRM_Rozszerzeni', 'OdbiorcyKurczaka',
                     'WlascicieleOdbiorcow', 'ImportCRM',
                     'NotatkiCRM', 'HistoriaZmianCRM')
ORDER BY TABLE_NAME, ORDINAL_POSITION;
GO

-- 25.B - Sample 3 klientow z kazdej tabeli
SELECT TOP 5 '25_B_odbiorcycrm_sample' AS __SEKCJA, * FROM dbo.OdbiorcyCRM ORDER BY 1 DESC;
GO

SELECT TOP 5 '25_C_tymczasowi_sample' AS __SEKCJA, * FROM dbo.TymczasowiOdbiorcy ORDER BY 1 DESC;
GO

SELECT TOP 5 '25_D_kontrahenci_sample' AS __SEKCJA, * FROM dbo.kontrahenci ORDER BY 1 DESC;
GO

SELECT TOP 5 '25_E_importcrm_sample' AS __SEKCJA, * FROM dbo.ImportCRM ORDER BY 1 DESC;
GO

SELECT TOP 5 '25_F_odbiorcykurczaka_sample' AS __SEKCJA, * FROM dbo.OdbiorcyKurczaka;
GO

-- 25.G - Czy klienci sa wspolne miedzy tabelami (po jakiem ID)
SELECT
    '25_G_klienci_overlap' AS __SEKCJA,
    'OdbiorcyCRM' AS tabela, COUNT(DISTINCT 1) AS dummy, COUNT(*) AS razem
FROM dbo.OdbiorcyCRM
UNION ALL
SELECT '25_G_klienci_overlap', 'TymczasowiOdbiorcy', 1, COUNT(*) FROM dbo.TymczasowiOdbiorcy
UNION ALL
SELECT '25_G_klienci_overlap', 'kontrahenci', 1, COUNT(*) FROM dbo.kontrahenci
UNION ALL
SELECT '25_G_klienci_overlap', 'ImportCRM', 1, COUNT(*) FROM dbo.ImportCRM
UNION ALL
SELECT '25_G_klienci_overlap', 'OdbiorcyKurczaka', 1, COUNT(*) FROM dbo.OdbiorcyKurczaka
UNION ALL
SELECT '25_G_klienci_overlap', 'WlascicieleOdbiorcow', 1, COUNT(*) FROM dbo.WlascicieleOdbiorcow;
GO

-- =============================================================================
-- BLOK 26 - REKLAMACJE (7 tabel)
-- =============================================================================

SELECT
    '26_A_reklamacje_kolumny' AS __SEKCJA,
    TABLE_NAME, ORDINAL_POSITION, COLUMN_NAME, DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME IN ('Reklamacje', 'ReklamacjeTowary', 'ReklamacjeKomentarze',
                     'ReklamacjeZalaczniki', 'ReklamacjeZdjecia',
                     'ReklamacjeUstawienia', 'ReklamacjeHistoria')
ORDER BY TABLE_NAME, ORDINAL_POSITION;
GO

SELECT '26_B_reklamacje_liczby' AS __SEKCJA, 'Reklamacje' AS tabela, COUNT(*) AS rekordow FROM dbo.Reklamacje
UNION ALL SELECT '26_B_reklamacje_liczby', 'ReklamacjeTowary', COUNT(*) FROM dbo.ReklamacjeTowary
UNION ALL SELECT '26_B_reklamacje_liczby', 'ReklamacjeKomentarze', COUNT(*) FROM dbo.ReklamacjeKomentarze
UNION ALL SELECT '26_B_reklamacje_liczby', 'ReklamacjeZalaczniki', COUNT(*) FROM dbo.ReklamacjeZalaczniki
UNION ALL SELECT '26_B_reklamacje_liczby', 'ReklamacjeZdjecia', COUNT(*) FROM dbo.ReklamacjeZdjecia
UNION ALL SELECT '26_B_reklamacje_liczby', 'ReklamacjeUstawienia', COUNT(*) FROM dbo.ReklamacjeUstawienia
UNION ALL SELECT '26_B_reklamacje_liczby', 'ReklamacjeHistoria', COUNT(*) FROM dbo.ReklamacjeHistoria;
GO

SELECT TOP 10 '26_C_reklamacje_sample' AS __SEKCJA, * FROM dbo.Reklamacje ORDER BY 1 DESC;
GO

SELECT TOP 10 '26_D_reklamacjetowary_sample' AS __SEKCJA, * FROM dbo.ReklamacjeTowary ORDER BY 1 DESC;
GO

SELECT TOP 10 '26_E_reklamacjehistoria_sample' AS __SEKCJA, * FROM dbo.ReklamacjeHistoria ORDER BY 1 DESC;
GO

SELECT '26_F_reklamacjeustawienia_all' AS __SEKCJA, * FROM dbo.ReklamacjeUstawienia;
GO

-- =============================================================================
-- BLOK 27 - QC WIDOKI (definicje + sample)
-- =============================================================================

SELECT
    '27_A_qc_widoki_definicje' AS __SEKCJA,
    v.name,
    LEFT(m.definition, 3000) AS definition_preview
FROM sys.views v
JOIN sys.sql_modules m ON m.object_id = v.object_id
WHERE v.name LIKE 'vw_QC_%' OR v.name LIKE 'V_HR_%' OR v.name LIKE 'V_KG_%'
ORDER BY v.name;
GO

SELECT TOP 10 '27_B_qc_podsum_sample' AS __SEKCJA, * FROM dbo.vw_QC_Podsum;
GO

SELECT TOP 10 '27_C_qc_temp_sample' AS __SEKCJA, * FROM dbo.vw_QC_TempSummary;
GO

SELECT TOP 10 '27_D_qc_wadyskale_sample' AS __SEKCJA, * FROM dbo.vw_QC_WadySkale;
GO

-- 27.E - QC tabele zrodlowe
SELECT
    '27_E_qc_tabele' AS __SEKCJA,
    TABLE_NAME, ORDINAL_POSITION, COLUMN_NAME, DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME IN ('QC_Normy', 'QC_Zdjecia', 'QC_Temperatury',
                     'Wady', 'Temperatury')
ORDER BY TABLE_NAME, ORDINAL_POSITION;
GO

SELECT '27_F_qc_normy_all' AS __SEKCJA, * FROM dbo.QC_Normy;
GO

SELECT TOP 10 '27_G_qc_temperatury_sample' AS __SEKCJA, * FROM dbo.QC_Temperatury ORDER BY 1 DESC;
GO

SELECT TOP 10 '27_H_temperatury_sample' AS __SEKCJA, * FROM dbo.Temperatury ORDER BY 1 DESC;
GO

-- =============================================================================
-- BLOK 28 - HR / KG MODULY (kadry)
-- =============================================================================

-- Wszystkie tabele HR_*, KG_*, V_HR_*, V_KG_*
SELECT
    '28_A_hr_kg_tabele' AS __SEKCJA,
    name AS table_name,
    create_date,
    modify_date
FROM sys.tables
WHERE name LIKE 'HR_%' OR name LIKE 'KG_%'
ORDER BY name;
GO

SELECT
    '28_B_hr_kg_widoki' AS __SEKCJA,
    name AS view_name
FROM sys.views
WHERE name LIKE 'V_HR_%' OR name LIKE 'V_KG_%' OR name LIKE 'vw_HR_%' OR name LIKE 'vw_KG_%'
ORDER BY name;
GO

-- 28.C - Struktury kluczowych tabel HR/KG
SELECT
    '28_C_hr_kg_kolumny' AS __SEKCJA,
    TABLE_NAME, ORDINAL_POSITION, COLUMN_NAME, DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME LIKE 'HR_%' OR TABLE_NAME LIKE 'KG_%'
ORDER BY TABLE_NAME, ORDINAL_POSITION;
GO

-- =============================================================================
-- BLOK 29 - NOTATKI EKOSYSTEM
-- =============================================================================

SELECT
    '29_A_notatki_kolumny' AS __SEKCJA,
    TABLE_NAME, ORDINAL_POSITION, COLUMN_NAME, DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME IN ('Notatki', 'NotatkiCRM', 'NotatkiMentions',
                     'NotatkiUczestnicy', 'NotatkiWidocznosc')
ORDER BY TABLE_NAME, ORDINAL_POSITION;
GO

SELECT '29_B_notatki_liczby' AS __SEKCJA, 'Notatki' AS tabela, COUNT(*) AS rekordow FROM dbo.Notatki
UNION ALL SELECT '29_B_notatki_liczby', 'NotatkiCRM', COUNT(*) FROM dbo.NotatkiCRM
UNION ALL SELECT '29_B_notatki_liczby', 'NotatkiMentions', COUNT(*) FROM dbo.NotatkiMentions
UNION ALL SELECT '29_B_notatki_liczby', 'NotatkiUczestnicy', COUNT(*) FROM dbo.NotatkiUczestnicy
UNION ALL SELECT '29_B_notatki_liczby', 'NotatkiWidocznosc', COUNT(*) FROM dbo.NotatkiWidocznosc;
GO

SELECT TOP 5 '29_C_notatki_sample' AS __SEKCJA, * FROM dbo.Notatki ORDER BY 1 DESC;
GO

-- =============================================================================
-- BLOK 30 - CHAT (czy ZPSP ma wewnetrzny chat?)
-- =============================================================================

SELECT
    '30_A_chat_kolumny' AS __SEKCJA,
    TABLE_NAME, ORDINAL_POSITION, COLUMN_NAME, DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME IN ('ChatMessages', 'ChatTypingStatus')
ORDER BY TABLE_NAME, ORDINAL_POSITION;
GO

SELECT TOP 10 '30_B_chat_sample' AS __SEKCJA, * FROM dbo.ChatMessages ORDER BY 1 DESC;
GO

SELECT '30_C_chat_typing_sample' AS __SEKCJA, * FROM dbo.ChatTypingStatus;
GO

-- =============================================================================
-- BLOK 31 - FIREFLIESTRANSKRYPCJE (102 wpisy - transkrypcje sa w bazie!)
-- =============================================================================

SELECT
    '31_A_fireflies_kolumny' AS __SEKCJA,
    ORDINAL_POSITION, COLUMN_NAME, DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'FirefliesTranskrypcje'
ORDER BY ORDINAL_POSITION;
GO

-- 31.B - sample bez tresci (pierwsze 200 znakow tylko)
SELECT TOP 5
    '31_B_fireflies_sample_metadane' AS __SEKCJA,
    * -- moze byc ciezkie - pole tekstu transkrypcji moze byc gigantyczne
FROM dbo.FirefliesTranskrypcje
ORDER BY 1 DESC;
GO

-- =============================================================================
-- BLOK 32 - INTEL (intel_Articles, intel_Prices)
-- =============================================================================

SELECT
    '32_A_intel_kolumny' AS __SEKCJA,
    TABLE_NAME, ORDINAL_POSITION, COLUMN_NAME, DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME LIKE 'intel_%'
ORDER BY TABLE_NAME, ORDINAL_POSITION;
GO

SELECT '32_B_intel_articles_all' AS __SEKCJA, * FROM dbo.intel_Articles;
GO

SELECT '32_C_intel_prices_all' AS __SEKCJA, * FROM dbo.intel_Prices;
GO

-- =============================================================================
-- BLOK 33 - STORNO KLAS WAGOWYCH (-1, -6, -10) i Operator2ID
-- =============================================================================

-- 33.A - Pelne sample wierszy z ujemna klasa
SELECT TOP 30
    '33_A_in0e_klasa_ujemna' AS __SEKCJA,
    Data, Godzina, ArticleID, ArticleName,
    QntInCont, ActWeight, Weight, P1,
    OperatorID, Wagowy
FROM dbo.In0E
WHERE QntInCont < 0
ORDER BY Data DESC;
GO

-- 33.B - Operator2ID kiedy uzywany
SELECT
    '33_B_in0e_operator2_uzycie' AS __SEKCJA,
    COUNT(*) AS razem,
    SUM(CASE WHEN Operator2ID IS NOT NULL AND Operator2ID <> '' THEN 1 ELSE 0 END) AS z_operator2,
    SUM(CASE WHEN Operator2ID IS NULL OR Operator2ID = '' THEN 1 ELSE 0 END) AS bez_operator2
FROM dbo.In0E
WHERE Data >= CONVERT(varchar(10), DATEADD(DAY, -90, GETDATE()), 120);
GO

-- 33.C - Sample 20 wierszy z Operator2ID
SELECT TOP 20
    '33_C_in0e_operator2_sample' AS __SEKCJA,
    Data, Godzina, ArticleID, OperatorID, Wagowy, Operator2ID, Wagowy2,
    ActWeight, P1
FROM dbo.In0E
WHERE Operator2ID IS NOT NULL AND Operator2ID <> ''
ORDER BY Data DESC;
GO

-- =============================================================================
-- BLOK 34 - JUSTYNA TERKA STORNO (1980)
-- =============================================================================

-- 34.A - Sample 30 storno Justyny
SELECT TOP 30
    '34_A_storno_justyny_sample' AS __SEKCJA,
    Data, Godzina, ArticleID, ArticleName,
    ActWeight, Weight, P1, Direction
FROM dbo.In0E
WHERE OperatorID = '1980'
  AND ActWeight < 0
ORDER BY Data DESC;
GO

-- 34.B - Co Justyna wazy najczesciej
SELECT TOP 20
    '34_B_justyna_typy_wazenia' AS __SEKCJA,
    ArticleID, ArticleName,
    COUNT(*) AS razem,
    SUM(CASE WHEN ActWeight < 0 THEN 1 ELSE 0 END) AS storno,
    AVG(CASE WHEN ActWeight > 0 THEN ActWeight ELSE NULL END) AS sr_waga
FROM dbo.In0E
WHERE OperatorID = '1980'
  AND Data >= CONVERT(varchar(10), DATEADD(DAY, -90, GETDATE()), 120)
GROUP BY ArticleID, ArticleName
ORDER BY razem DESC;
GO

-- =============================================================================
-- BLOK 35 - TERMID K1 vs K2 (co to za rozdzielenie)
-- =============================================================================

SELECT
    '35_A_termid_typy' AS __SEKCJA,
    TermID, TermType,
    COUNT(*) AS razem,
    COUNT(DISTINCT OperatorID) AS unikalni_operatorzy,
    COUNT(DISTINCT ArticleID) AS unikalne_artykuly,
    SUM(CASE WHEN ArticleID = '40' THEN 1 ELSE 0 END) AS palety_kurczaka_a,
    SUM(CASE WHEN ArticleID <> '40' THEN 1 ELSE 0 END) AS porcje
FROM dbo.In0E
WHERE Data >= CONVERT(varchar(10), DATEADD(DAY, -30, GETDATE()), 120)
  AND ActWeight > 0
GROUP BY TermID, TermType
ORDER BY razem DESC;
GO

-- 35.B - Per terminal ktore artykuly waza najczesciej
SELECT TOP 30
    '35_B_termid_artykuly' AS __SEKCJA,
    TermID, TermType, ArticleID, ArticleName,
    COUNT(*) AS razem
FROM dbo.In0E
WHERE Data >= CONVERT(varchar(10), DATEADD(DAY, -30, GETDATE()), 120)
  AND ActWeight > 0
GROUP BY TermID, TermType, ArticleID, ArticleName
ORDER BY TermID, razem DESC;
GO

-- =============================================================================
-- BLOK 36 - OUT1A - czy zywa, czy martwa
-- =============================================================================

-- 36.A - Zakres dat Out1A
SELECT
    '36_A_out1a_zakres' AS __SEKCJA,
    MIN(Data) AS pierwsza,
    MAX(Data) AS ostatnia,
    COUNT(*) AS razem
FROM dbo.Out1A;
GO

-- 36.B - Czy ostatnie 30 dni cos jest w Out1A
SELECT
    '36_B_out1a_ostatnio' AS __SEKCJA,
    Data,
    COUNT(*) AS razem
FROM dbo.Out1A
WHERE Data >= CONVERT(varchar(10), DATEADD(DAY, -30, GETDATE()), 120)
GROUP BY Data
ORDER BY Data DESC;
GO

-- 36.C - Sample 10 najnowszych
SELECT TOP 10 '36_C_out1a_sample' AS __SEKCJA, *
FROM dbo.Out1A
ORDER BY Data DESC, Godzina DESC;
GO

-- =============================================================================
-- BLOK 37 - DOSTAWCY vs DOSTAWCY (case sensitive!)
-- =============================================================================

SELECT
    '37_A_dostawcy_kolumny' AS __SEKCJA,
    TABLE_NAME, ORDINAL_POSITION, COLUMN_NAME, DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME IN ('Dostawcy', 'DOSTAWCY', 'DostawcyAdresy',
                     'DostawcyChangeRequest', 'Audit_Dostawcy')
ORDER BY TABLE_NAME, ORDINAL_POSITION;
GO

SELECT '37_B_dostawcy_liczby' AS __SEKCJA, 'Dostawcy' AS tabela, COUNT(*) AS rekordow FROM dbo.Dostawcy
UNION ALL SELECT '37_B_dostawcy_liczby', 'DOSTAWCY', COUNT(*) FROM dbo.DOSTAWCY
UNION ALL SELECT '37_B_dostawcy_liczby', 'DostawcyAdresy', COUNT(*) FROM dbo.DostawcyAdresy
UNION ALL SELECT '37_B_dostawcy_liczby', 'DostawcyChangeRequest', COUNT(*) FROM dbo.DostawcyChangeRequest
UNION ALL SELECT '37_B_dostawcy_liczby', 'Audit_Dostawcy', COUNT(*) FROM dbo.Audit_Dostawcy;
GO

SELECT TOP 5 '37_C_dostawcy_sample' AS __SEKCJA, * FROM dbo.Dostawcy;
GO

-- =============================================================================
-- BLOK 38 - 'Dane hodowcw$' - dziwna tabela ze znakami specjalnymi
-- =============================================================================

SELECT
    '38_A_dziwna_tabela_kolumny' AS __SEKCJA,
    ORDINAL_POSITION, COLUMN_NAME, DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'Dane hodowców$'
ORDER BY ORDINAL_POSITION;
GO

-- 38.B - sample (uwaga - moze sie nie zaladowac, znaki specjalne)
BEGIN TRY
    SELECT TOP 5 '38_B_dziwna_tabela_sample' AS __SEKCJA, *
    FROM [dbo].[Dane hodowców$];
END TRY
BEGIN CATCH
    SELECT '38_B_dziwna_tabela_sample' AS __SEKCJA, ERROR_MESSAGE() AS blad;
END CATCH;
GO

-- =============================================================================
-- BLOK 39 - POZYSKIWANIE_HODOWCY (1874 leady CRM)
-- =============================================================================

SELECT
    '39_A_pozyskiwanie_kolumny' AS __SEKCJA,
    TABLE_NAME, ORDINAL_POSITION, COLUMN_NAME, DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME IN ('Pozyskiwanie_Hodowcy', 'Pozyskiwanie_Aktywnosci',
                     'Pozyskiwanie_DuplicateIgnore')
ORDER BY TABLE_NAME, ORDINAL_POSITION;
GO

SELECT TOP 10 '39_B_pozyskiwanie_sample' AS __SEKCJA, *
FROM dbo.Pozyskiwanie_Hodowcy
ORDER BY 1 DESC;
GO

SELECT TOP 10 '39_C_aktywnosci_sample' AS __SEKCJA, *
FROM dbo.Pozyskiwanie_Aktywnosci
ORDER BY 1 DESC;
GO

-- =============================================================================
-- BLOK 40 - CENNIKI HISTORIA (CenaTuszki, Min, Rolnicza)
-- =============================================================================

SELECT
    '40_A_cenniki_kolumny' AS __SEKCJA,
    TABLE_NAME, ORDINAL_POSITION, COLUMN_NAME, DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME IN ('CenaTuszki', 'CenaMinisterialna', 'CenaRolnicza', 'PriceType')
ORDER BY TABLE_NAME, ORDINAL_POSITION;
GO

SELECT TOP 30 '40_B_cena_tuszki' AS __SEKCJA, * FROM dbo.CenaTuszki ORDER BY 1 DESC;
GO

SELECT TOP 30 '40_C_cena_ministerialna' AS __SEKCJA, * FROM dbo.CenaMinisterialna ORDER BY 1 DESC;
GO

SELECT TOP 30 '40_D_cena_rolnicza' AS __SEKCJA, * FROM dbo.CenaRolnicza ORDER BY 1 DESC;
GO

SELECT '40_E_pricetype_all' AS __SEKCJA, * FROM dbo.PriceType;
GO

-- =============================================================================
-- BLOK 41 - SPOTKANIA (SpotkaniaUczestnicy, SpotkaniaNotyfikacje)
-- =============================================================================

SELECT
    '41_A_spotkania_kolumny' AS __SEKCJA,
    TABLE_NAME, ORDINAL_POSITION, COLUMN_NAME, DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME IN ('Spotkania', 'SpotkaniaUczestnicy',
                     'SpotkaniaNotyfikacje')
ORDER BY TABLE_NAME, ORDINAL_POSITION;
GO

SELECT TOP 10 '41_B_spotkaniauczestnicy' AS __SEKCJA, *
FROM dbo.SpotkaniaUczestnicy ORDER BY 1 DESC;
GO

SELECT TOP 10 '41_C_spotkanianotyfikacje' AS __SEKCJA, *
FROM dbo.SpotkaniaNotyfikacje ORDER BY 1 DESC;
GO

-- =============================================================================
-- BLOK 42 - OPERATORS / PRACOWNICY (kto loguje sie do ZPSP)
-- =============================================================================

SELECT
    '42_A_operators_kolumny' AS __SEKCJA,
    ORDINAL_POSITION, COLUMN_NAME, DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'operators'
ORDER BY ORDINAL_POSITION;
GO

SELECT '42_B_operators_all' AS __SEKCJA, * FROM dbo.operators;
GO

-- =============================================================================
-- BLOK 43 - WEBFLEET / FLOTA (Driver, CarTrailer, WebfleetVehicleMapping)
-- =============================================================================

SELECT
    '43_A_flota_kolumny' AS __SEKCJA,
    TABLE_NAME, ORDINAL_POSITION, COLUMN_NAME, DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME IN ('Driver', 'CarTrailer', 'WebfleetVehicleMapping',
                     'DriverDetails', 'VehicleDetails',
                     'DriverVehicleAssignment', 'VehicleServiceLog')
ORDER BY TABLE_NAME, ORDINAL_POSITION;
GO

SELECT '43_B_driver_sample' AS __SEKCJA, * FROM dbo.Driver;
GO

SELECT '43_C_cartrailer_sample' AS __SEKCJA, * FROM dbo.CarTrailer;
GO

SELECT '43_D_webfleet_sample' AS __SEKCJA, * FROM dbo.WebfleetVehicleMapping;
GO

-- =============================================================================
-- BLOK 44 - OFERTY (vw_OfertyLista, sp_ZapiszOferte)
-- =============================================================================

SELECT
    '44_A_oferty_kolumny' AS __SEKCJA,
    TABLE_NAME, ORDINAL_POSITION, COLUMN_NAME, DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME LIKE 'Oferty%' OR TABLE_NAME LIKE 'Oferta%'
ORDER BY TABLE_NAME, ORDINAL_POSITION;
GO

SELECT TOP 10 '44_B_oferty_pozycje' AS __SEKCJA, *
FROM dbo.Oferty_Pozycje ORDER BY 1 DESC;
GO

-- 44.C - Definicja vw_Oferty*
SELECT
    '44_C_oferty_widoki' AS __SEKCJA,
    v.name,
    LEFT(m.definition, 2000) AS definition_preview
FROM sys.views v
JOIN sys.sql_modules m ON m.object_id = v.object_id
WHERE v.name LIKE 'vw_Oferty%';
GO

-- =============================================================================
-- BLOK 45 - SALDA OPAKOWAN (vw_SaldaOpakowaniKontrahentow, vw_StatusHistoriiSald)
-- =============================================================================

SELECT
    '45_A_opakowania_widoki' AS __SEKCJA,
    v.name,
    LEFT(m.definition, 2000) AS definition_preview
FROM sys.views v
JOIN sys.sql_modules m ON m.object_id = v.object_id
WHERE v.name LIKE '%Saldo%' OR v.name LIKE '%Opakowan%' OR v.name LIKE '%Historii%';
GO

SELECT
    '45_B_opakowania_tabele' AS __SEKCJA,
    name AS table_name
FROM sys.tables
WHERE name LIKE '%Opakowa%' OR name LIKE '%Saldo%' OR name LIKE '%Saldz%';
GO

-- =============================================================================
-- BLOK 46 - ANULACJE PER KLIENT (kto najczesciej anuluje)
-- =============================================================================

SELECT TOP 30
    '46_A_anulacje_per_klient' AS __SEKCJA,
    KlientId,
    COUNT(*) AS razem,
    SUM(CASE WHEN Status = 'Anulowane' THEN 1 ELSE 0 END) AS anulowane,
    SUM(CASE WHEN Status = 'Anulowane' THEN 1 ELSE 0 END) * 100.0 / NULLIF(COUNT(*), 0) AS proc_anulacji
FROM dbo.ZamowieniaMieso
WHERE DataZamowienia >= CONVERT(varchar(10), DATEADD(DAY, -90, GETDATE()), 120)
GROUP BY KlientId
HAVING COUNT(*) >= 5
ORDER BY proc_anulacji DESC;
GO

-- =============================================================================
-- BLOK 47 - PRZYCZYNY ANULACJI
-- =============================================================================

SELECT
    '47_A_anulacje_przyczyny' AS __SEKCJA,
    PrzyczynaAnulowania,
    COUNT(*) AS liczba
FROM dbo.ZamowieniaMieso
WHERE Status = 'Anulowane'
  AND DataZamowienia >= CONVERT(varchar(10), DATEADD(DAY, -180, GETDATE()), 120)
GROUP BY PrzyczynaAnulowania
ORDER BY liczba DESC;
GO

-- =============================================================================
-- BLOK 48 - RANKING HODOWCOW PER KLASY A vs B
-- =============================================================================

-- 48.A - Polacz In0E (klasy wagowe palet) z PartiaDostawca
SELECT TOP 30
    '48_A_hodowcy_klasy' AS __SEKCJA,
    pd.CustomerID,
    pd.CustomerName,
    COUNT(DISTINCT pd.Partia) AS liczba_partii,
    COUNT(*) AS liczba_palet,
    SUM(CASE WHEN e.QntInCont = 5 THEN 1 ELSE 0 END) AS klasa_5,
    SUM(CASE WHEN e.QntInCont = 6 THEN 1 ELSE 0 END) AS klasa_6,
    SUM(CASE WHEN e.QntInCont = 7 THEN 1 ELSE 0 END) AS klasa_7,
    SUM(CASE WHEN e.QntInCont = 8 THEN 1 ELSE 0 END) AS klasa_8,
    SUM(CASE WHEN e.QntInCont >= 9 THEN 1 ELSE 0 END) AS klasa_9plus,
    SUM(CASE WHEN e.QntInCont = 0 THEN 1 ELSE 0 END) AS klasa_0_brak,
    SUM(CASE WHEN e.QntInCont < 0 THEN 1 ELSE 0 END) AS storno
FROM dbo.PartiaDostawca pd
JOIN dbo.In0E e ON e.P1 = pd.Partia
WHERE e.ArticleID = '40'
  AND e.Data >= CONVERT(varchar(10), DATEADD(DAY, -90, GETDATE()), 120)
GROUP BY pd.CustomerID, pd.CustomerName
HAVING COUNT(*) > 50
ORDER BY liczba_palet DESC;
GO

-- =============================================================================
-- BLOK 49 - KARTOTEKA KategoriaHandlowca distribution
-- =============================================================================

SELECT
    '49_A_kategorie_klientow' AS __SEKCJA,
    KategoriaHandlowca,
    COUNT(*) AS liczba
FROM dbo.KartotekaOdbiorcyDane
GROUP BY KategoriaHandlowca
ORDER BY liczba DESC;
GO

-- =============================================================================
-- BLOK 50 - KLIENCI Z GPS (Latitude/Longitude)
-- =============================================================================

SELECT
    '50_A_klienci_gps' AS __SEKCJA,
    SUM(CASE WHEN Latitude IS NOT NULL AND Longitude IS NOT NULL THEN 1 ELSE 0 END) AS z_gps,
    SUM(CASE WHEN Latitude IS NULL OR Longitude IS NULL THEN 1 ELSE 0 END) AS bez_gps,
    COUNT(*) AS razem
FROM dbo.KartotekaOdbiorcyDane;
GO

-- =============================================================================
-- BLOK 51 - ODPADYREJESTR + vw_OdpadyDzienne
-- =============================================================================

SELECT
    '51_A_odpady_kolumny' AS __SEKCJA,
    ORDINAL_POSITION, COLUMN_NAME, DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'OdpadyRejestr'
ORDER BY ORDINAL_POSITION;
GO

SELECT TOP 20 '51_B_odpady_sample' AS __SEKCJA, * FROM dbo.OdpadyRejestr ORDER BY 1 DESC;
GO

-- 51.C - definicja vw_OdpadyDzienne
SELECT
    '51_C_odpady_widok' AS __SEKCJA,
    v.name,
    LEFT(m.definition, 3000) AS definition
FROM sys.views v
JOIN sys.sql_modules m ON m.object_id = v.object_id
WHERE v.name = 'vw_OdpadyDzienne';
GO

-- =============================================================================
-- BLOK 52 - SP_GETDASHBOARDKPIS - co dokladnie liczy?
-- =============================================================================

SELECT
    '52_A_sp_getdashboardkpis' AS __SEKCJA,
    p.name,
    LEFT(m.definition, 5000) AS definition
FROM sys.procedures p
JOIN sys.sql_modules m ON m.object_id = p.object_id
WHERE p.name = 'sp_GetDashboardKPIs';
GO

-- =============================================================================
-- BLOK 53 - SP_POBIERZRANKINGHANDLOWCOW
-- =============================================================================

SELECT
    '53_A_sp_rankinghandlowcow' AS __SEKCJA,
    p.name,
    LEFT(m.definition, 5000) AS definition
FROM sys.procedures p
JOIN sys.sql_modules m ON m.object_id = p.object_id
WHERE p.name = 'sp_PobierzRankingHandlowcow';
GO

-- =============================================================================
-- BLOK 54 - PRZESTRZE/POMIESZCZENIA (jesli sa)
-- =============================================================================

SELECT
    '54_A_pomieszczenia_tabele' AS __SEKCJA,
    name AS table_name
FROM sys.tables
WHERE name LIKE '%Pomiesz%' OR name LIKE '%Hala%' OR name LIKE '%Mroznia%'
   OR name LIKE '%Komora%' OR name LIKE '%Strefa%'
ORDER BY name;
GO

-- =============================================================================
-- BLOK 55 - WSZYSTKIE TABELE LIBRANET (bardzo dlugi - na koncu)
-- =============================================================================

SELECT
    '55_A_wszystkie_tabele_full' AS __SEKCJA,
    s.name + '.' + t.name AS TableFullName,
    p.rows AS RowCount_,
    SUM(a.total_pages) * 8 / 1024.0 AS TotalMB
FROM sys.tables t
JOIN sys.schemas s ON s.schema_id = t.schema_id
JOIN sys.partitions p ON p.object_id = t.object_id AND p.index_id IN (0,1)
JOIN sys.allocation_units a ON a.container_id = p.partition_id
GROUP BY s.name, t.name, p.rows
ORDER BY t.name;
GO

PRINT '################################################################';
PRINT '## EKSPLORACJA LIBRANET RUNDA 2 - KONIEC                       ##';
PRINT '################################################################';
GO
