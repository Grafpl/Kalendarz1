-- =============================================================================
-- EKSPLORACJA LIBRANET - RUNDA 3 (najgłębsze pytania)
-- =============================================================================
-- Co bada:
--   - WagoCounter szczegoly (czemu przestal pisac 2025-04-01)
--   - HR_*, KG_* moduly HR (szczegolowe struktury)
--   - vw_QC_* + vw_HR_* + vw_KG_* (wszystkie definicje)
--   - intel_Articles, intel_Prices (inteligencja konkurencji?)
--   - DocOut0E, HeaderDocOut0E (dokumenty wyjsc 0E)
--   - Notatki ekosystem (5 tabel)
--   - IRZplusLog (panstwowa rejestracja zwierzat)
--   - CallReminderLog/Config/Contacts/PKDPriority (przypomnienia)
--   - Notyfikacje, Spotkania
--   - intel + FirefliesTranskrypcje
--   - KodyPocztowe (21k!) + GeoCache (20k)
--   - Oferty + Saldo Opakowan
--   - WebfleetVehicleMapping
--   - DostawcyChangeRequest workflow
--   - PdfHistory
--   - Klasy A vs B per partia (pełen ranking)
--   - Anulacje per klient (top problemy)
--
-- INSTRUKCJA jak zwykle:
--   1. SSMS -> 192.168.0.109 (pronova/pronova)
--   2. Ctrl+T -> F5 -> Ctrl+A -> Ctrl+C
--   3. Wklej do WYNIKI_LIBRANET_3.txt
-- =============================================================================

USE LibraNet;
GO

SET NOCOUNT ON;
GO

PRINT '################################################################';
PRINT '## EKSPLORACJA LIBRANET RUNDA 3 - START                       ##';
PRINT '################################################################';
GO

-- =============================================================================
-- BLOK 56 - WagoCounter SZCZEGOLY (czemu przestal pisac)
-- =============================================================================

-- 56.A - Pełny zakres dat z liczeniem per miesiąc
SELECT
    '56_A_wagocounter_per_miesiac' AS __SEKCJA,
    YEAR(CalcDate) AS rok,
    MONTH(CalcDate) AS miesiac,
    COUNT(*) AS liczba_wpisow,
    COUNT(DISTINCT CONVERT(date, CalcDate)) AS liczba_dni,
    MIN(CalcDate) AS pierwsza,
    MAX(CalcDate) AS ostatnia,
    SUM(Quantity) AS suma_sztuk
FROM dbo.WagoCounter
GROUP BY YEAR(CalcDate), MONTH(CalcDate)
ORDER BY rok DESC, miesiac DESC;
GO

-- 56.B - 30 najnowszych wpisow
SELECT TOP 30 '56_B_wagocounter_najnowsze' AS __SEKCJA, *
FROM dbo.WagoCounter
ORDER BY CalcDate DESC, CarLP DESC;
GO

-- 56.C - Czy w okresie kiedy WagoCounter pisal, In0E tez pisal? (cross-check)
SELECT
    '56_C_wago_vs_in0e_2025' AS __SEKCJA,
    LEFT(CalcDate, 7) AS rok_miesiac,
    COUNT(*) AS wago_wpisow,
    SUM(Quantity) AS wago_sztuk
FROM dbo.WagoCounter
WHERE YEAR(CalcDate) = 2025
GROUP BY LEFT(CalcDate, 7)
ORDER BY rok_miesiac DESC;
GO

-- =============================================================================
-- BLOK 57 - HR/KG MODULY (kadry)
-- =============================================================================

-- 57.A - Wszystkie tabele HR_*, KG_*
SELECT
    '57_A_hr_kg_tabele' AS __SEKCJA,
    name AS table_name,
    create_date,
    modify_date,
    (SELECT SUM(p.rows) FROM sys.partitions p
     WHERE p.object_id = t.object_id AND p.index_id IN (0,1)) AS row_count
FROM sys.tables t
WHERE name LIKE 'HR_%' OR name LIKE 'KG_%'
ORDER BY name;
GO

-- 57.B - Pełne struktury HR_*
SELECT
    '57_B_hr_kolumny' AS __SEKCJA,
    TABLE_NAME, ORDINAL_POSITION, COLUMN_NAME, DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME LIKE 'HR_%'
ORDER BY TABLE_NAME, ORDINAL_POSITION;
GO

-- 57.C - Pełne struktury KG_*
SELECT
    '57_C_kg_kolumny' AS __SEKCJA,
    TABLE_NAME, ORDINAL_POSITION, COLUMN_NAME, DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME LIKE 'KG_%'
ORDER BY TABLE_NAME, ORDINAL_POSITION;
GO

-- 57.D - V_HR_*, V_KG_* widoki
SELECT
    '57_D_hr_kg_widoki_definicje' AS __SEKCJA,
    v.name,
    LEFT(m.definition, 3000) AS definition_preview
FROM sys.views v
JOIN sys.sql_modules m ON m.object_id = v.object_id
WHERE v.name LIKE 'V_HR_%' OR v.name LIKE 'V_KG_%' OR v.name LIKE 'vw_HR_%' OR v.name LIKE 'vw_KG_%'
ORDER BY v.name;
GO

-- =============================================================================
-- BLOK 58 - QC WIDOKI (pełne definicje)
-- =============================================================================

SELECT
    '58_A_qc_widoki_definicje' AS __SEKCJA,
    v.name,
    LEFT(m.definition, 3000) AS definition_preview
FROM sys.views v
JOIN sys.sql_modules m ON m.object_id = v.object_id
WHERE v.name LIKE 'vw_QC%' OR v.name LIKE 'V_QC%' OR v.name = 'RaportQC' OR v.name = 'Wady'
ORDER BY v.name;
GO

-- 58.B - Sample z RaportQC i Wady (jako widoki)
SELECT TOP 5 '58_B_raportqc_sample' AS __SEKCJA, * FROM dbo.RaportQC;
GO

SELECT TOP 10 '58_C_wady_sample' AS __SEKCJA, * FROM dbo.Wady;
GO

-- =============================================================================
-- BLOK 59 - intel_Articles + intel_Prices (inteligencja rynkowa?)
-- =============================================================================

SELECT
    '59_A_intel_kolumny' AS __SEKCJA,
    TABLE_NAME, ORDINAL_POSITION, COLUMN_NAME, DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME LIKE 'intel_%'
ORDER BY TABLE_NAME, ORDINAL_POSITION;
GO

SELECT '59_B_intel_articles_all' AS __SEKCJA, * FROM dbo.intel_Articles;
GO

SELECT '59_C_intel_prices_all' AS __SEKCJA, * FROM dbo.intel_Prices;
GO

-- =============================================================================
-- BLOK 60 - DocOut0E, HeaderDocOut0E (dokumenty wyjsc 0E)
-- =============================================================================

SELECT
    '60_A_docout0e_kolumny' AS __SEKCJA,
    TABLE_NAME, ORDINAL_POSITION, COLUMN_NAME, DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME IN ('DocOut0E', 'HeaderDocOut0E', 'DokMagPozycjeBuf', 'DokMagHeaderBuf')
ORDER BY TABLE_NAME, ORDINAL_POSITION;
GO

SELECT TOP 5 '60_B_docout0e_sample' AS __SEKCJA, * FROM dbo.DocOut0E ORDER BY 1 DESC;
GO

SELECT TOP 5 '60_C_headerout0e_sample' AS __SEKCJA, * FROM dbo.HeaderDocOut0E ORDER BY 1 DESC;
GO

-- =============================================================================
-- BLOK 61 - NOTATKI EKOSYSTEM (sample wszystkich 5)
-- =============================================================================

SELECT TOP 10 '61_A_notatki_sample' AS __SEKCJA, * FROM dbo.Notatki ORDER BY 1 DESC;
GO

SELECT TOP 10 '61_B_notatkicrm_sample' AS __SEKCJA, * FROM dbo.NotatkiCRM ORDER BY 1 DESC;
GO

SELECT TOP 10 '61_C_notatkimentions_sample' AS __SEKCJA, * FROM dbo.NotatkiMentions ORDER BY 1 DESC;
GO

SELECT TOP 10 '61_D_notatkiuczestnicy_sample' AS __SEKCJA, * FROM dbo.NotatkiUczestnicy ORDER BY 1 DESC;
GO

SELECT TOP 10 '61_E_notatkiwidocznosc_sample' AS __SEKCJA, * FROM dbo.NotatkiWidocznosc ORDER BY 1 DESC;
GO

-- =============================================================================
-- BLOK 62 - IRZplus (państwowy rejestr zwierząt rzeźnych)
-- =============================================================================

SELECT
    '62_A_irzplus_kolumny' AS __SEKCJA,
    ORDINAL_POSITION, COLUMN_NAME, DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'IRZplusLog'
ORDER BY ORDINAL_POSITION;
GO

SELECT TOP 20 '62_B_irzplus_sample' AS __SEKCJA, * FROM dbo.IRZplusLog ORDER BY 1 DESC;
GO

-- =============================================================================
-- BLOK 63 - CALL REMINDER (przypomnienia telefonów)
-- =============================================================================

SELECT
    '63_A_callreminder_kolumny' AS __SEKCJA,
    TABLE_NAME, ORDINAL_POSITION, COLUMN_NAME, DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME LIKE 'CallReminder%'
ORDER BY TABLE_NAME, ORDINAL_POSITION;
GO

SELECT TOP 10 '63_B_callreminder_log_sample' AS __SEKCJA, *
FROM dbo.CallReminderLog ORDER BY 1 DESC;
GO

SELECT TOP 10 '63_C_callreminder_contacts_sample' AS __SEKCJA, *
FROM dbo.CallReminderContacts ORDER BY 1 DESC;
GO

SELECT '63_D_callreminder_config' AS __SEKCJA, * FROM dbo.CallReminderConfig;
GO

SELECT '63_E_callreminder_pkd' AS __SEKCJA, * FROM dbo.CallReminderPKDPriority;
GO

-- =============================================================================
-- BLOK 64 - SPOTKANIA + NOTYFIKACJE
-- =============================================================================

SELECT
    '64_A_spotkania_kolumny' AS __SEKCJA,
    TABLE_NAME, ORDINAL_POSITION, COLUMN_NAME, DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME IN ('Spotkania', 'SpotkaniaUczestnicy', 'SpotkaniaNotyfikacje',
                     'FirefliesTranskrypcje')
ORDER BY TABLE_NAME, ORDINAL_POSITION;
GO

SELECT TOP 5 '64_B_spotkania_sample' AS __SEKCJA, * FROM dbo.Spotkania ORDER BY 1 DESC;
GO

SELECT TOP 5 '64_C_uczestnicy_sample' AS __SEKCJA, * FROM dbo.SpotkaniaUczestnicy ORDER BY 1 DESC;
GO

-- 64.D - FirefliesTranskrypcje (struktura tylko, BEZ treści transkrypcji bo cieżkie!)
SELECT
    '64_D_fireflies_kolumny' AS __SEKCJA,
    ORDINAL_POSITION, COLUMN_NAME, DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'FirefliesTranskrypcje'
ORDER BY ORDINAL_POSITION;
GO

-- 64.E - Sample fireflies (TYLKO metadane, bez kolumn z treścią >2000 znaków)
SELECT TOP 5
    '64_E_fireflies_metadane' AS __SEKCJA,
    Id,
    Tytul,
    DataSpotkania,
    CzasTrwaniaMin,
    UrlFireflies,
    DataDodania,
    LiczbaUczestnikow,
    JezykTranskrypcji,
    LEN(Transkrypcja) AS dlugosc_transkrypcji,
    LEN(Notatki) AS dlugosc_notatek
FROM dbo.FirefliesTranskrypcje
ORDER BY DataSpotkania DESC;
GO

-- =============================================================================
-- BLOK 65 - KODY POCZTOWE + GEO CACHE
-- =============================================================================

SELECT
    '65_A_kody_kolumny' AS __SEKCJA,
    TABLE_NAME, ORDINAL_POSITION, COLUMN_NAME, DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME IN ('KodyPocztowe', 'GeoCache', 'GeoCacheKodyPocztowe', 'Province')
ORDER BY TABLE_NAME, ORDINAL_POSITION;
GO

SELECT TOP 10 '65_B_kody_sample' AS __SEKCJA, * FROM dbo.KodyPocztowe;
GO

SELECT TOP 10 '65_C_geocache_sample' AS __SEKCJA, * FROM dbo.GeoCache;
GO

SELECT '65_D_province_all' AS __SEKCJA, * FROM dbo.Province;
GO

-- =============================================================================
-- BLOK 66 - OFERTY + OFERTY POZYCJE
-- =============================================================================

SELECT
    '66_A_oferty_kolumny' AS __SEKCJA,
    TABLE_NAME, ORDINAL_POSITION, COLUMN_NAME, DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME LIKE 'Oferty%' OR TABLE_NAME LIKE 'Oferta%'
ORDER BY TABLE_NAME, ORDINAL_POSITION;
GO

SELECT TOP 10 '66_B_oferty_pozycje' AS __SEKCJA, *
FROM dbo.Oferty_Pozycje ORDER BY 1 DESC;
GO

-- 66.C - Definicje vw_Oferty*
SELECT
    '66_C_oferty_widoki' AS __SEKCJA,
    v.name,
    LEFT(m.definition, 2000) AS definition_preview
FROM sys.views v
JOIN sys.sql_modules m ON m.object_id = v.object_id
WHERE v.name LIKE 'vw_Oferty%';
GO

-- =============================================================================
-- BLOK 67 - SALDA OPAKOWAN (vw_SaldaOpakowaniKontrahentow, vw_StatusHistoriiSald)
-- =============================================================================

SELECT
    '67_A_opakowania_widoki' AS __SEKCJA,
    v.name,
    LEFT(m.definition, 2000) AS definition_preview
FROM sys.views v
JOIN sys.sql_modules m ON m.object_id = v.object_id
WHERE v.name LIKE '%Saldo%' OR v.name LIKE '%Opakowan%' OR v.name LIKE '%HistoriiSald%';
GO

-- 67.B - Tabele zwiazane z opakowaniami / saldami
SELECT
    '67_B_opakowania_tabele' AS __SEKCJA,
    name AS table_name,
    (SELECT SUM(p.rows) FROM sys.partitions p
     WHERE p.object_id = t.object_id AND p.index_id IN (0,1)) AS row_count
FROM sys.tables t
WHERE name LIKE '%Opakowa%' OR name LIKE '%Saldo%' OR name LIKE '%Saldz%'
   OR name LIKE '%Potwierdzenie%';
GO

-- =============================================================================
-- BLOK 68 - WEBFLEET VEHICLE MAPPING
-- =============================================================================

SELECT
    '68_A_webfleet_kolumny' AS __SEKCJA,
    ORDINAL_POSITION, COLUMN_NAME, DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'WebfleetVehicleMapping'
ORDER BY ORDINAL_POSITION;
GO

SELECT '68_B_webfleet_all' AS __SEKCJA, * FROM dbo.WebfleetVehicleMapping;
GO

-- =============================================================================
-- BLOK 69 - DOSTAWCY CHANGE REQUEST WORKFLOW
-- =============================================================================

SELECT
    '69_A_dostawcychangerequest_kolumny' AS __SEKCJA,
    TABLE_NAME, ORDINAL_POSITION, COLUMN_NAME, DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME IN ('DostawcyChangeRequest', 'DostawcyCR', 'DostawcyCRItem')
ORDER BY TABLE_NAME, ORDINAL_POSITION;
GO

SELECT TOP 10 '69_B_dostawcychangerequest_sample' AS __SEKCJA, *
FROM dbo.DostawcyChangeRequest ORDER BY 1 DESC;
GO

-- =============================================================================
-- BLOK 70 - PDF HISTORY (496 wierszy)
-- =============================================================================

SELECT
    '70_A_pdfhistory_kolumny' AS __SEKCJA,
    ORDINAL_POSITION, COLUMN_NAME, DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'PdfHistory'
ORDER BY ORDINAL_POSITION;
GO

SELECT TOP 10 '70_B_pdfhistory_sample' AS __SEKCJA, *
FROM dbo.PdfHistory ORDER BY 1 DESC;
GO

-- =============================================================================
-- BLOK 71 - ANULACJE PER KLIENT (top problemy 90 dni)
-- =============================================================================

SELECT TOP 30
    '71_A_anulacje_per_klient' AS __SEKCJA,
    KlientId,
    COUNT(*) AS razem,
    SUM(CASE WHEN Status = 'Anulowane' THEN 1 ELSE 0 END) AS anulowane,
    SUM(CASE WHEN Status = 'Anulowane' THEN 1 ELSE 0 END) * 100.0 / NULLIF(COUNT(*), 0) AS proc_anulacji,
    MAX(DataZamowienia) AS ostatnie_zamowienie
FROM dbo.ZamowieniaMieso
WHERE DataZamowienia >= CONVERT(varchar(10), DATEADD(DAY, -90, GETDATE()), 120)
GROUP BY KlientId
HAVING COUNT(*) >= 5
ORDER BY proc_anulacji DESC;
GO

-- 71.B - Anulacje per dzień + przyczyna
SELECT
    '71_B_anulacje_przyczyny' AS __SEKCJA,
    PrzyczynaAnulowania,
    COUNT(*) AS liczba
FROM dbo.ZamowieniaMieso
WHERE Status = 'Anulowane'
  AND PrzyczynaAnulowania IS NOT NULL
  AND DataZamowienia >= CONVERT(varchar(10), DATEADD(DAY, -180, GETDATE()), 120)
GROUP BY PrzyczynaAnulowania
ORDER BY liczba DESC;
GO

-- =============================================================================
-- BLOK 72 - RANKING HODOWCOW per partia (klasy + reklamacje)
-- =============================================================================

SELECT TOP 30
    '72_A_ranking_hodowcow' AS __SEKCJA,
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
    SUM(CASE WHEN e.QntInCont < 0 THEN 1 ELSE 0 END) AS storno,
    -- Proc 6+7 (idealna) z palet z klasą wagową
    SUM(CASE WHEN e.QntInCont IN (6,7) THEN 1 ELSE 0 END) * 100.0 /
        NULLIF(SUM(CASE WHEN e.QntInCont > 0 THEN 1 ELSE 0 END), 0) AS proc_idealna
FROM dbo.PartiaDostawca pd
JOIN dbo.In0E e ON e.P1 = pd.Partia
WHERE e.ArticleID = '40'
  AND e.Data >= CONVERT(varchar(10), DATEADD(DAY, -90, GETDATE()), 120)
GROUP BY pd.CustomerID, pd.CustomerName
HAVING COUNT(*) > 50
ORDER BY proc_idealna DESC;
GO

-- =============================================================================
-- BLOK 73 - WSZYSTKIE TRIGGERY z definicjami
-- =============================================================================

SELECT
    '73_A_wszystkie_triggery' AS __SEKCJA,
    OBJECT_NAME(parent_id) AS table_name,
    name AS trigger_name,
    is_disabled,
    is_instead_of_trigger,
    LEFT(OBJECT_DEFINITION(object_id), 2500) AS definicja
FROM sys.triggers
WHERE parent_class_desc = 'OBJECT_OR_COLUMN'
ORDER BY OBJECT_NAME(parent_id);
GO

-- =============================================================================
-- BLOK 74 - HARMONOGRAM DOSTAW AUDIT LOG
-- =============================================================================

SELECT
    '74_A_audit_kolumny' AS __SEKCJA,
    TABLE_NAME, ORDINAL_POSITION, COLUMN_NAME, DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME IN ('HarmonogramDostaw_AuditLog', 'AuditLog_Dostawy', 'Audit_Dostawcy')
ORDER BY TABLE_NAME, ORDINAL_POSITION;
GO

SELECT TOP 10 '74_B_harmonogram_audit' AS __SEKCJA, *
FROM dbo.HarmonogramDostaw_AuditLog ORDER BY 1 DESC;
GO

SELECT TOP 10 '74_C_audit_dostawy' AS __SEKCJA, *
FROM dbo.AuditLog_Dostawy ORDER BY 1 DESC;
GO

-- =============================================================================
-- BLOK 75 - REZERWACJE KLAS WAGOWYCH (96 wpisów)
-- =============================================================================

SELECT
    '75_A_rezerwacje_kolumny' AS __SEKCJA,
    ORDINAL_POSITION, COLUMN_NAME, DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'RezerwacjeKlasWagowych'
ORDER BY ORDINAL_POSITION;
GO

SELECT TOP 20 '75_B_rezerwacje_sample' AS __SEKCJA, *
FROM dbo.RezerwacjeKlasWagowych ORDER BY 1 DESC;
GO

-- =============================================================================
-- BLOK 76 - SCALOWANIE TOWAROW (32 wiersze - relacja artykułów?)
-- =============================================================================

SELECT
    '76_A_scalowanie_kolumny' AS __SEKCJA,
    ORDINAL_POSITION, COLUMN_NAME, DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'ScalowanieTowarow'
ORDER BY ORDINAL_POSITION;
GO

SELECT '76_B_scalowanie_all' AS __SEKCJA, * FROM dbo.ScalowanieTowarow;
GO

-- =============================================================================
-- BLOK 77 - ARTPARTITIOND (32 wiersze, 1.7 MB)
-- =============================================================================

SELECT
    '77_A_artpartition_kolumny' AS __SEKCJA,
    ORDINAL_POSITION, COLUMN_NAME, DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'ArtPartitionD'
ORDER BY ORDINAL_POSITION;
GO

SELECT TOP 5 '77_B_artpartition_sample' AS __SEKCJA, * FROM dbo.ArtPartitionD;
GO

-- =============================================================================
-- BLOK 78 - OBCEKONTRAKTY (58 wpisów - kontrakty zewn?)
-- =============================================================================

SELECT
    '78_A_obcekontrakty_kolumny' AS __SEKCJA,
    ORDINAL_POSITION, COLUMN_NAME, DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'ObceKontrakty'
ORDER BY ORDINAL_POSITION;
GO

SELECT TOP 10 '78_B_obcekontrakty_sample' AS __SEKCJA, *
FROM dbo.ObceKontrakty ORDER BY 1 DESC;
GO

-- =============================================================================
-- BLOK 79 - 'Dane hodowcw$' obejście (varchar literal)
-- =============================================================================

-- 79.A - Spróbuj z prawidłowymi nawiasami
BEGIN TRY
    SELECT TOP 5 '79_A_dziwna_tabela' AS __SEKCJA, *
    FROM dbo.[Dane hodowców$];
END TRY
BEGIN CATCH
    SELECT '79_A_dziwna_tabela' AS __SEKCJA,
           ERROR_NUMBER() AS num,
           ERROR_MESSAGE() AS blad;
END CATCH;
GO

-- =============================================================================
-- BLOK 80 - DASHBOARDWIDOKI
-- =============================================================================

SELECT
    '80_A_dashboard_kolumny' AS __SEKCJA,
    ORDINAL_POSITION, COLUMN_NAME, DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'DashboardWidoki'
ORDER BY ORDINAL_POSITION;
GO

SELECT TOP 10 '80_B_dashboard_sample' AS __SEKCJA, *
FROM dbo.DashboardWidoki ORDER BY 1 DESC;
GO

-- =============================================================================
-- BLOK 81 - SENDBACK (41 wpisów - co to za tabela?)
-- =============================================================================

SELECT
    '81_A_sendback_kolumny' AS __SEKCJA,
    ORDINAL_POSITION, COLUMN_NAME, DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'sendback'
ORDER BY ORDINAL_POSITION;
GO

SELECT '81_B_sendback_all' AS __SEKCJA, * FROM dbo.sendback ORDER BY 1 DESC;
GO

-- =============================================================================
-- BLOK 82 - FARMERWGTLOG (51 wpisów - log wag farmera?)
-- =============================================================================

SELECT
    '82_A_farmerwgtlog_kolumny' AS __SEKCJA,
    ORDINAL_POSITION, COLUMN_NAME, DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'FarmerWgtLog'
ORDER BY ORDINAL_POSITION;
GO

SELECT TOP 10 '82_B_farmerwgtlog_sample' AS __SEKCJA, *
FROM dbo.FarmerWgtLog ORDER BY 1 DESC;
GO

-- =============================================================================
-- BLOK 83 - HISTORIA ZMIAN ZAMÓWIEŃ (16k wpisów - sample 30)
-- =============================================================================

SELECT TOP 30 '83_A_historia_zmian_sample' AS __SEKCJA, *
FROM dbo.HistoriaZmianZamowien
WHERE DataZmiany >= DATEADD(DAY, -7, GETDATE())
ORDER BY DataZmiany DESC;
GO

-- 83.B - Top typy zmian
SELECT
    '83_B_historia_typy' AS __SEKCJA,
    TypZmiany,
    COUNT(*) AS liczba
FROM dbo.HistoriaZmianZamowien
WHERE DataZmiany >= DATEADD(DAY, -90, GETDATE())
GROUP BY TypZmiany
ORDER BY liczba DESC;
GO

-- =============================================================================
-- BLOK 84 - DOSTAWAFEEDBACK (4226 wpisów)
-- =============================================================================

SELECT
    '84_A_feedback_kolumny' AS __SEKCJA,
    ORDINAL_POSITION, COLUMN_NAME, DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'DostawaFeedback'
ORDER BY ORDINAL_POSITION;
GO

SELECT TOP 10 '84_B_feedback_sample' AS __SEKCJA, *
FROM dbo.DostawaFeedback ORDER BY 1 DESC;
GO

-- =============================================================================
-- BLOK 85 - SP_GETDASHBOARDKPIS pełna definicja
-- =============================================================================

SELECT
    '85_A_dashboardkpis_def' AS __SEKCJA,
    p.name,
    LEFT(m.definition, 5000) AS definition
FROM sys.procedures p
JOIN sys.sql_modules m ON m.object_id = p.object_id
WHERE p.name = 'sp_GetDashboardKPIs';
GO

-- =============================================================================
-- BLOK 86 - WSZYSTKIE PROCEDURY z pełnymi definicjami
-- =============================================================================

SELECT
    '86_A_procedury_full' AS __SEKCJA,
    p.name,
    LEFT(m.definition, 3500) AS definition
FROM sys.procedures p
JOIN sys.sql_modules m ON m.object_id = p.object_id
ORDER BY p.name;
GO

-- =============================================================================
-- BLOK 87 - WSZYSTKIE WIDOKI z pełnymi definicjami
-- =============================================================================

SELECT
    '87_A_widoki_full' AS __SEKCJA,
    v.name,
    LEFT(m.definition, 3500) AS definition
FROM sys.views v
JOIN sys.sql_modules m ON m.object_id = v.object_id
ORDER BY v.name;
GO

PRINT '################################################################';
PRINT '## EKSPLORACJA LIBRANET RUNDA 3 - KONIEC                       ##';
PRINT '################################################################';
GO
