-- =============================================================================
-- EKSPLORACJA LIBRANET - RUNDA 5 (najgłębsza analiza)
-- =============================================================================
-- Cele:
--   - Distinct values w kluczowych kolumnach (DIR_ID, Direction, Status, etc.)
--   - Operator2ID/Wagowy2 walidacja (czy 100% NULL)
--   - In0E.CustomerID dystrybucja (zauważone '0')
--   - TymczasowiOdbiorcy vs OdbiorcyCRM (różnica)
--   - TowarZdjecia szczegóły (23 MB)
--   - ReklamacjeUstawienia + identyfikacja UserID 6611
--   - PartiaStatus + PartiaAuditLog sample
--   - Aktywnosc.TypLicznika - co znaczy 1/2/4/7/8
--   - 'Dane hodowców$' próba odczytu
--   - CallReminder szczegóły
--   - PartiaAuditLog co Sergiusz audytuje
--   - Reklamacje per typ produktu
--   - FarmerCalc AvgWeight trend per miesiąc
--   - TransportZmiany wzorzec dzienny
--   - WSZYSTKIE foreign keys
--   - Tabele sieroty (bez referencji)
--
-- INSTRUKCJA:
--   1. SSMS -> 192.168.0.109 (pronova/pronova)
--   2. Ctrl+T -> F5 -> Ctrl+A -> Ctrl+C
--   3. Wklej do WYNIKI_LIBRANET_5.txt
-- =============================================================================

USE LibraNet;
GO

SET NOCOUNT ON;
GO

PRINT '################################################################';
PRINT '## EKSPLORACJA LIBRANET RUNDA 5 - START                       ##';
PRINT '################################################################';
GO

-- =============================================================================
-- BLOK 110 - DISTINCT VALUES w kluczowych kolumnach
-- =============================================================================

-- 110.A - listapartii.DIR_ID (czy są inne niż 1A, 0E, 0K?)
SELECT
    '110_A_listapartii_dirid_all' AS __SEKCJA,
    DIR_ID,
    COUNT(*) AS razem,
    MIN(CreateData) AS pierwsza,
    MAX(CreateData) AS ostatnia
FROM dbo.listapartii
GROUP BY DIR_ID
ORDER BY razem DESC;
GO

-- 110.B - In0E.Direction wszystkie wartości
SELECT
    '110_B_in0e_direction_all' AS __SEKCJA,
    Direction,
    COUNT(*) AS razem,
    MIN(Data) AS pierwsza,
    MAX(Data) AS ostatnia
FROM dbo.In0E
GROUP BY Direction
ORDER BY razem DESC;
GO

-- 110.C - Out1A.Direction wszystkie wartości
SELECT
    '110_C_out1a_direction_all' AS __SEKCJA,
    Direction,
    COUNT(*) AS razem,
    MIN(Data) AS pierwsza,
    MAX(Data) AS ostatnia
FROM dbo.Out1A
GROUP BY Direction
ORDER BY razem DESC;
GO

-- 110.D - In0E.TermType wszystkie (K1, K2, ...?)
SELECT
    '110_D_in0e_termtype_all' AS __SEKCJA,
    TermType,
    COUNT(*) AS razem
FROM dbo.In0E
GROUP BY TermType
ORDER BY razem DESC;
GO

-- 110.E - listapartii.CalcMethod wszystkie wartości
SELECT
    '110_E_listapartii_calcmethod' AS __SEKCJA,
    CalcMethod,
    COUNT(*) AS razem
FROM dbo.listapartii
GROUP BY CalcMethod
ORDER BY razem DESC;
GO

-- 110.F - HarmonogramDostaw.Bufor wszystkie wartości
SELECT
    '110_F_harmonogram_bufor' AS __SEKCJA,
    Bufor,
    COUNT(*) AS razem,
    MIN(DataOdbioru) AS pierwsza,
    MAX(DataOdbioru) AS ostatnia
FROM dbo.HarmonogramDostaw
GROUP BY Bufor
ORDER BY razem DESC;
GO

-- 110.G - HarmonogramDostaw.TypUmowy + TypCeny dystrybucja
SELECT
    '110_G_harmonogram_typumowy' AS __SEKCJA,
    TypUmowy,
    TypCeny,
    COUNT(*) AS razem
FROM dbo.HarmonogramDostaw
WHERE DataOdbioru >= DATEADD(YEAR, -1, GETDATE())
GROUP BY TypUmowy, TypCeny
ORDER BY razem DESC;
GO

-- 110.H - ZamowieniaMieso.Waluta - czy zawsze PLN?
SELECT
    '110_H_zamowienia_waluta' AS __SEKCJA,
    Waluta,
    COUNT(*) AS razem
FROM dbo.ZamowieniaMieso
GROUP BY Waluta;
GO

-- 110.I - Reklamacje.StatusV2 + Status (oba pola)
SELECT
    '110_I_reklamacje_statusy' AS __SEKCJA,
    Status,
    StatusV2,
    COUNT(*) AS razem
FROM dbo.Reklamacje
GROUP BY Status, StatusV2
ORDER BY razem DESC;
GO

-- 110.J - Reklamacje.Priorytet + DecyzjaJakosci
SELECT
    '110_J_reklamacje_priorytet' AS __SEKCJA,
    Priorytet,
    DecyzjaJakosci,
    COUNT(*) AS razem
FROM dbo.Reklamacje
GROUP BY Priorytet, DecyzjaJakosci
ORDER BY razem DESC;
GO

-- =============================================================================
-- BLOK 111 - OPERATOR2ID / WAGOWY2 walidacja
-- =============================================================================

-- 111.A - Czy Operator2ID/Wagowy2 są kiedykolwiek wypełnione (FULL HISTORIA)
SELECT
    '111_A_operator2_full' AS __SEKCJA,
    COUNT(*) AS razem,
    SUM(CASE WHEN Operator2ID IS NOT NULL AND Operator2ID <> '' THEN 1 ELSE 0 END) AS z_operator2,
    SUM(CASE WHEN Wagowy2 IS NOT NULL AND Wagowy2 <> '' THEN 1 ELSE 0 END) AS z_wagowy2
FROM dbo.In0E;
GO

-- 111.B - Operator2ID identycznie dla Out1A
SELECT
    '111_B_out1a_operator2' AS __SEKCJA,
    COUNT(*) AS razem,
    SUM(CASE WHEN Operator2ID IS NOT NULL AND Operator2ID <> '' THEN 1 ELSE 0 END) AS z_operator2,
    SUM(CASE WHEN Wagowy2 IS NOT NULL AND Wagowy2 <> '' THEN 1 ELSE 0 END) AS z_wagowy2
FROM dbo.Out1A;
GO

-- =============================================================================
-- BLOK 112 - In0E.CustomerID dystrybucja (zauważone '0')
-- =============================================================================

-- 112.A - Top wartości CustomerID w In0E
SELECT TOP 20
    '112_A_in0e_customerid_top' AS __SEKCJA,
    CustomerID,
    COUNT(*) AS razem,
    MIN(Data) AS pierwsza,
    MAX(Data) AS ostatnia
FROM dbo.In0E
WHERE Data >= CONVERT(varchar(10), DATEADD(DAY, -90, GETDATE()), 120)
GROUP BY CustomerID
ORDER BY razem DESC;
GO

-- 112.B - Out1A.CustomerID (klient w wydaniach)
SELECT TOP 20
    '112_B_out1a_customerid_top' AS __SEKCJA,
    CustomerID,
    COUNT(*) AS razem,
    SUM(ActWeight) AS suma_kg
FROM dbo.Out1A
WHERE Data >= CONVERT(varchar(10), DATEADD(DAY, -30, GETDATE()), 120)
  AND ActWeight > 0
GROUP BY CustomerID
ORDER BY razem DESC;
GO

-- 112.C - Czy In0E.CustomerID powiązany z Dostawcy?
SELECT
    '112_C_in0e_customerid_linki' AS __SEKCJA,
    COUNT(DISTINCT e.CustomerID) AS unikalne,
    SUM(CASE WHEN e.CustomerID IN (SELECT CAST(d.ID AS varchar) FROM dbo.Dostawcy d) THEN 1 ELSE 0 END) AS w_dostawcy
FROM (SELECT DISTINCT CustomerID FROM dbo.In0E WHERE CustomerID IS NOT NULL AND CustomerID <> '0' AND CustomerID <> '') e;
GO

-- =============================================================================
-- BLOK 113 - TymczasowiOdbiorcy vs OdbiorcyCRM (porównanie)
-- =============================================================================

-- 113.A - TymczasowiOdbiorcy struktura
SELECT
    '113_A_tymczasowi_kolumny' AS __SEKCJA,
    ORDINAL_POSITION, COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'TymczasowiOdbiorcy'
ORDER BY ORDINAL_POSITION;
GO

-- 113.B - OdbiorcyCRM struktura
SELECT
    '113_B_odbiorcycrm_kolumny' AS __SEKCJA,
    ORDINAL_POSITION, COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'OdbiorcyCRM'
ORDER BY ORDINAL_POSITION;
GO

-- 113.C - Czy ID się pokrywa?
SELECT
    '113_C_klienci_overlap_full' AS __SEKCJA,
    (SELECT COUNT(*) FROM dbo.OdbiorcyCRM) AS odbiorcy_crm,
    (SELECT COUNT(*) FROM dbo.TymczasowiOdbiorcy) AS tymczasowi,
    (SELECT COUNT(*) FROM dbo.OdbiorcyCRM o WHERE EXISTS (
        SELECT 1 FROM dbo.TymczasowiOdbiorcy t WHERE TRY_CAST(t.IDSymfonii AS int) = o.ID
    )) AS w_obu;
GO

-- =============================================================================
-- BLOK 114 - TowarZdjecia szczegóły (23 MB)
-- =============================================================================

SELECT
    '114_A_towarzdjecia_kolumny' AS __SEKCJA,
    ORDINAL_POSITION, COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'TowarZdjecia'
ORDER BY ORDINAL_POSITION;
GO

-- 114.B - Liczba zdjęć per produkt
SELECT
    '114_B_towarzdjecia_per_produkt' AS __SEKCJA,
    tz.TowarId,
    a.Name AS NazwaTowaru,
    COUNT(*) AS liczba_zdjec,
    SUM(LEN(tz.Zdjecie) / 1024) AS rozmiar_kb_lacznie,
    AVG(tz.Szerokosc) AS sr_szerokosc,
    AVG(tz.Wysokosc) AS sr_wysokosc
FROM dbo.TowarZdjecia tz
LEFT JOIN dbo.Article a ON TRY_CAST(a.ID AS int) = tz.TowarId
WHERE tz.Aktywne = 1
GROUP BY tz.TowarId, a.Name
ORDER BY rozmiar_kb_lacznie DESC;
GO

-- =============================================================================
-- BLOK 115 - ReklamacjeUstawienia + UserID 6611
-- =============================================================================

-- 115.A - ReklamacjeUstawienia full
SELECT '115_A_reklamacje_ustawienia' AS __SEKCJA, * FROM dbo.ReklamacjeUstawienia;
GO

-- 115.B - Kto to user 6611 (z ReklamacjeHistoria)
SELECT '115_B_user_6611' AS __SEKCJA, * FROM dbo.operators WHERE ID = 6611;
GO

-- 115.C - Top users w ReklamacjeHistoria
SELECT TOP 10
    '115_C_top_users_reklamacje' AS __SEKCJA,
    rh.UserID,
    op.Name AS UserName,
    COUNT(*) AS liczba_zmian,
    MIN(rh.DataZmiany) AS pierwsza,
    MAX(rh.DataZmiany) AS ostatnia
FROM dbo.ReklamacjeHistoria rh
LEFT JOIN dbo.operators op ON op.ID = TRY_CAST(rh.UserID AS int)
GROUP BY rh.UserID, op.Name
ORDER BY liczba_zmian DESC;
GO

-- =============================================================================
-- BLOK 116 - PartiaStatus + PartiaAuditLog
-- =============================================================================

-- 116.A - PartiaStatus rozkład per status
SELECT
    '116_A_partiastatus_dystrybucja' AS __SEKCJA,
    Status,
    COUNT(*) AS razem,
    COUNT(DISTINCT Partia) AS unikalnych_partii
FROM dbo.PartiaStatus
GROUP BY Status
ORDER BY razem DESC;
GO

-- 116.B - Sample 20 ostatnich zmian PartiaStatus
SELECT TOP 20 '116_B_partiastatus_recent' AS __SEKCJA, *
FROM dbo.PartiaStatus
ORDER BY ID DESC;
GO

-- 116.C - PartiaAuditLog distinct akcje
SELECT
    '116_C_partiaauditlog_akcje' AS __SEKCJA,
    Akcja,
    COUNT(*) AS razem
FROM dbo.PartiaAuditLog
GROUP BY Akcja
ORDER BY razem DESC;
GO

-- 116.D - PartiaAuditLog 30 ostatnich zmian
SELECT TOP 30 '116_D_partiaauditlog_recent' AS __SEKCJA, *
FROM dbo.PartiaAuditLog
ORDER BY ID DESC;
GO

-- =============================================================================
-- BLOK 117 - Aktywnosc.TypLicznika (co znaczy 1/2/4/7/8)
-- =============================================================================

SELECT
    '117_A_aktywnosc_typliczników' AS __SEKCJA,
    TypLicznika,
    COUNT(*) AS razem,
    COUNT(DISTINCT KtoStworzyl) AS unikalni_uzytkownicy,
    MIN(Data) AS pierwsza,
    MAX(Data) AS ostatnia
FROM dbo.Aktywnosc
GROUP BY TypLicznika
ORDER BY razem DESC;
GO

-- 117.B - Sample dla każdego TypLicznika
WITH ranked AS (
    SELECT *, ROW_NUMBER() OVER (PARTITION BY TypLicznika ORDER BY Data DESC) AS rn
    FROM dbo.Aktywnosc
)
SELECT '117_B_aktywnosc_sample_per_typ' AS __SEKCJA, Lp, Licznik, TypLicznika, KtoStworzyl, Data
FROM ranked
WHERE rn <= 3
ORDER BY TypLicznika, Data DESC;
GO

-- =============================================================================
-- BLOK 118 - 'Dane hodowców$' próba odczytania (z polskimi znakami)
-- =============================================================================

BEGIN TRY
    SELECT TOP 10 '118_A_dane_hodowcow_sample' AS __SEKCJA, *
    FROM [dbo].[Dane hodowców$];
END TRY
BEGIN CATCH
    SELECT '118_A_dane_hodowcow_sample' AS __SEKCJA,
           ERROR_NUMBER() AS num,
           ERROR_MESSAGE() AS blad;
END CATCH;
GO

-- 118.B - Struktura
SELECT
    '118_B_dane_hodowcow_kolumny' AS __SEKCJA,
    ORDINAL_POSITION, COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME LIKE 'Dane%hodowc%'
ORDER BY ORDINAL_POSITION;
GO

-- =============================================================================
-- BLOK 119 - CallReminder szczegóły
-- =============================================================================

SELECT '119_A_callreminderconfig' AS __SEKCJA, * FROM dbo.CallReminderConfig;
GO

SELECT '119_B_callreminder_pkd' AS __SEKCJA, * FROM dbo.CallReminderPKDPriority;
GO

-- 119.C - CallReminderLog stats
SELECT
    '119_C_callreminderlog_stats' AS __SEKCJA,
    COUNT(*) AS razem,
    COUNT(DISTINCT UserID) AS unikalni_user,
    MIN(CreatedAt) AS pierwsza,
    MAX(CreatedAt) AS ostatnia
FROM dbo.CallReminderLog;
GO

-- =============================================================================
-- BLOK 120 - Reklamacje per typ produktu (ReklamacjeTowary)
-- =============================================================================

SELECT TOP 20
    '120_A_reklamacje_per_produkt' AS __SEKCJA,
    rt.Symbol,
    rt.Nazwa,
    COUNT(*) AS liczba_reklamacji,
    SUM(rt.Waga) AS suma_kg,
    SUM(rt.Wartosc) AS suma_wartosci,
    AVG(rt.Cena) AS sr_cena,
    rt.PrzyczynaReklamacji
FROM dbo.ReklamacjeTowary rt
GROUP BY rt.Symbol, rt.Nazwa, rt.PrzyczynaReklamacji
ORDER BY liczba_reklamacji DESC;
GO

-- =============================================================================
-- BLOK 121 - FarmerCalc AvgWeight trend per miesiąc (jakość ptaków w czasie)
-- =============================================================================

SELECT
    '121_A_farmercalc_trend' AS __SEKCJA,
    YEAR(CalcDate) AS rok,
    MONTH(CalcDate) AS miesiac,
    COUNT(*) AS dostaw,
    SUM(LumQnt) AS suma_sztuk,
    SUM(NettoWeight) AS suma_kg,
    AVG(AvWeight) AS sr_kg_na_sztuke,
    AVG(Loss) AS sr_loss_proc,
    AVG(Price + ISNULL(Addition, 0)) AS sr_cena
FROM dbo.FarmerCalc
WHERE CalcDate >= DATEADD(MONTH, -12, GETDATE())
GROUP BY YEAR(CalcDate), MONTH(CalcDate)
ORDER BY rok DESC, miesiac DESC;
GO

-- =============================================================================
-- BLOK 122 - TransportZmiany wzorzec dzienny (która godzina, kto)
-- =============================================================================

USE TransportPL;
GO

SELECT
    '122_A_transportzmiany_godziny' AS __SEKCJA,
    DATEPART(HOUR, DataZgloszenia) AS godzina,
    TypZmiany,
    COUNT(*) AS razem
FROM dbo.TransportZmiany
WHERE DataZgloszenia >= DATEADD(DAY, -30, GETDATE())
GROUP BY DATEPART(HOUR, DataZgloszenia), TypZmiany
ORDER BY godzina, razem DESC;
GO

-- 122.B - Top zgłaszający TransportZmiany
SELECT TOP 15
    '122_B_top_zglaszajacy' AS __SEKCJA,
    ZgloszonePrzez,
    COUNT(*) AS razem,
    SUM(CASE WHEN StatusZmiany = 'Zaakceptowano' THEN 1 ELSE 0 END) AS zaakceptowane,
    SUM(CASE WHEN StatusZmiany = 'Oczekuje' THEN 1 ELSE 0 END) AS oczekujace
FROM dbo.TransportZmiany
WHERE DataZgloszenia >= DATEADD(DAY, -30, GETDATE())
GROUP BY ZgloszonePrzez
ORDER BY razem DESC;
GO

-- 122.C - Top akceptujący
SELECT TOP 15
    '122_C_top_akceptujacy' AS __SEKCJA,
    ZaakceptowanePrzez,
    COUNT(*) AS razem,
    AVG(DATEDIFF(MINUTE, DataZgloszenia, DataAkceptacji)) AS sr_min_do_akceptacji
FROM dbo.TransportZmiany
WHERE DataZgloszenia >= DATEADD(DAY, -30, GETDATE())
  AND ZaakceptowanePrzez IS NOT NULL
GROUP BY ZaakceptowanePrzez
ORDER BY razem DESC;
GO

USE LibraNet;
GO

-- =============================================================================
-- BLOK 123 - WagoCounter histogram per godzina
-- =============================================================================

SELECT
    '123_A_wago_histogram_godzin' AS __SEKCJA,
    DATEPART(HOUR, DateFrom) AS godzina_start,
    COUNT(*) AS liczba_aut,
    SUM(Quantity) AS suma_sztuk,
    AVG(CAST(DATEDIFF(MINUTE, DateFrom, DateTo) AS float)) AS sr_minut_per_auto,
    AVG(Quantity) AS sr_sztuk_per_auto
FROM dbo.WagoCounter
WHERE CalcDate >= DATEADD(DAY, -30, GETDATE())
GROUP BY DATEPART(HOUR, DateFrom)
ORDER BY godzina_start;
GO

-- 123.B - Per dzień tygodnia (kiedy najwięcej ubijamy)
SELECT
    '123_B_wago_per_dzien_tyg' AS __SEKCJA,
    DATENAME(WEEKDAY, CalcDate) AS dzien_tyg,
    DATEPART(WEEKDAY, CalcDate) AS dzien_num,
    COUNT(DISTINCT CONVERT(date, CalcDate)) AS dni,
    SUM(Quantity) AS suma_sztuk,
    SUM(Quantity) / NULLIF(COUNT(DISTINCT CONVERT(date, CalcDate)), 0) AS sr_sztuk_dziennie
FROM dbo.WagoCounter
WHERE CalcDate >= DATEADD(DAY, -90, GETDATE())
GROUP BY DATENAME(WEEKDAY, CalcDate), DATEPART(WEEKDAY, CalcDate)
ORDER BY dzien_num;
GO

-- =============================================================================
-- BLOK 124 - Cross: ZamowieniaMieso per klient per dzień tygodnia
-- =============================================================================

SELECT TOP 30
    '124_A_klient_dzien_tyg' AS __SEKCJA,
    zm.KlientId,
    DATENAME(WEEKDAY, zm.DataPrzyjazdu) AS dzien_tyg,
    DATEPART(WEEKDAY, zm.DataPrzyjazdu) AS dzien_num,
    COUNT(*) AS liczba_zamowien,
    SUM(zm.LiczbaPalet) AS suma_palet
FROM dbo.ZamowieniaMieso zm
WHERE zm.DataZamowienia >= DATEADD(DAY, -90, GETDATE())
  AND zm.DataPrzyjazdu IS NOT NULL
  AND zm.Status <> 'Anulowane'
GROUP BY zm.KlientId, DATENAME(WEEKDAY, zm.DataPrzyjazdu), DATEPART(WEEKDAY, zm.DataPrzyjazdu)
ORDER BY liczba_zamowien DESC;
GO

-- =============================================================================
-- BLOK 125 - KartotekaScoring - pełen ranking klientów
-- =============================================================================

SELECT TOP 30
    '125_A_scoring_full' AS __SEKCJA,
    ks.KlientId,
    ks.TerminowoscPkt, ks.HistoriaPkt, ks.RegularnoscPkt, ks.TrendPkt, ks.LimitPkt,
    ks.ScoreTotal,
    ks.Kategoria,
    ks.RekomendacjaLimitu,
    LEFT(ks.RekomendacjaOpis, 200) AS RekomendacjaOpis_skrot,
    ks.DataObliczenia
FROM dbo.KartotekaScoring ks
ORDER BY ks.ScoreTotal DESC;
GO

-- =============================================================================
-- BLOK 126 - SmsHistory + ContactHistory aktywność
-- =============================================================================

SELECT
    '126_A_sms_per_miesiac' AS __SEKCJA,
    YEAR(DataWyslania) AS rok,
    MONTH(DataWyslania) AS miesiac,
    COUNT(*) AS razem
FROM dbo.SmsHistory
GROUP BY YEAR(DataWyslania), MONTH(DataWyslania)
ORDER BY rok DESC, miesiac DESC;
GO

SELECT TOP 5 '126_B_smshistory_sample' AS __SEKCJA, * FROM dbo.SmsHistory ORDER BY 1 DESC;
GO

SELECT TOP 5 '126_C_contacthistory_sample' AS __SEKCJA, * FROM dbo.ContactHistory ORDER BY 1 DESC;
GO

-- =============================================================================
-- BLOK 127 - Spotkania (czy spotkania 13:00 są realne)
-- =============================================================================

SELECT
    '127_A_spotkania_per_typ' AS __SEKCJA,
    TypSpotkania,
    COUNT(*) AS razem,
    MIN(DataSpotkania) AS pierwsze,
    MAX(DataSpotkania) AS ostatnie
FROM dbo.Spotkania
GROUP BY TypSpotkania
ORDER BY razem DESC;
GO

-- 127.B - Czy spotkania 13:00 odbywają się
SELECT
    '127_B_spotkania_godziny' AS __SEKCJA,
    DATEPART(HOUR, DataSpotkania) AS godzina,
    COUNT(*) AS razem
FROM dbo.Spotkania
WHERE DataSpotkania >= DATEADD(DAY, -90, GETDATE())
GROUP BY DATEPART(HOUR, DataSpotkania)
ORDER BY godzina;
GO

-- =============================================================================
-- BLOK 128 - Reklamacje sprawdzone vs niesprawdzone (kto kiedy zaczął)
-- =============================================================================

SELECT
    '128_A_reklamacje_sprawdzanie' AS __SEKCJA,
    CONVERT(varchar(7), DataAnalizy, 120) AS rok_miesiac,
    UserAnalizy,
    COUNT(*) AS sprawdzonych
FROM dbo.Reklamacje
WHERE DataAnalizy IS NOT NULL
GROUP BY CONVERT(varchar(7), DataAnalizy, 120), UserAnalizy
ORDER BY rok_miesiac DESC, sprawdzonych DESC;
GO

-- =============================================================================
-- BLOK 129 - PEŁNE FOREIGN KEYS w LibraNet (jakie relacje są zachowane)
-- =============================================================================

SELECT
    '129_A_full_fk' AS __SEKCJA,
    fk.name AS FK_Name,
    OBJECT_NAME(fk.parent_object_id) AS Tabela_dziecko,
    cp.name AS Kolumna_dziecko,
    OBJECT_NAME(fk.referenced_object_id) AS Tabela_rodzic,
    cr.name AS Kolumna_rodzic,
    fk.delete_referential_action_desc AS OnDelete,
    fk.is_disabled AS Wylaczony
FROM sys.foreign_keys fk
JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
JOIN sys.columns cp ON cp.object_id = fkc.parent_object_id AND cp.column_id = fkc.parent_column_id
JOIN sys.columns cr ON cr.object_id = fkc.referenced_object_id AND cr.column_id = fkc.referenced_column_id
ORDER BY OBJECT_NAME(fk.parent_object_id), fk.name;
GO

-- =============================================================================
-- BLOK 130 - HEAVY: Tabele bez żadnych FK (sieroty struktury)
-- =============================================================================

SELECT
    '130_A_tabele_bez_fk' AS __SEKCJA,
    t.name AS table_name,
    p.rows AS row_count
FROM sys.tables t
JOIN sys.partitions p ON p.object_id = t.object_id AND p.index_id IN (0,1)
WHERE NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys fk
    WHERE fk.parent_object_id = t.object_id OR fk.referenced_object_id = t.object_id
)
  AND p.rows > 100
ORDER BY p.rows DESC;
GO

-- =============================================================================
-- BLOK 131 - Top stored procedures wywoływane (na podstawie sys.dm_exec_procedure_stats)
-- =============================================================================

SELECT TOP 30
    '131_A_top_sp_wywoły' AS __SEKCJA,
    OBJECT_NAME(ps.object_id) AS sp_name,
    ps.execution_count,
    ps.last_execution_time,
    ps.total_elapsed_time / 1000 AS total_ms,
    (ps.total_elapsed_time / NULLIF(ps.execution_count, 0)) / 1000 AS avg_ms
FROM sys.dm_exec_procedure_stats ps
WHERE ps.database_id = DB_ID('LibraNet')
ORDER BY ps.execution_count DESC;
GO

-- =============================================================================
-- BLOK 132 - Klasy A/B per Hodowca (FULL ranking)
-- =============================================================================

SELECT TOP 30
    '132_A_klasy_per_hodowca_full' AS __SEKCJA,
    pd.CustomerID,
    pd.CustomerName,
    COUNT(DISTINCT pd.Partia) AS liczba_partii,
    COUNT(*) AS liczba_palet,
    SUM(CASE WHEN e.QntInCont = 5 THEN 1 ELSE 0 END) AS klasa_5,
    SUM(CASE WHEN e.QntInCont = 6 THEN 1 ELSE 0 END) AS klasa_6,
    SUM(CASE WHEN e.QntInCont = 7 THEN 1 ELSE 0 END) AS klasa_7,
    SUM(CASE WHEN e.QntInCont = 8 THEN 1 ELSE 0 END) AS klasa_8,
    SUM(CASE WHEN e.QntInCont = 9 THEN 1 ELSE 0 END) AS klasa_9,
    SUM(CASE WHEN e.QntInCont = 10 THEN 1 ELSE 0 END) AS klasa_10,
    SUM(CASE WHEN e.QntInCont = 11 THEN 1 ELSE 0 END) AS klasa_11,
    SUM(CASE WHEN e.QntInCont = 12 THEN 1 ELSE 0 END) AS klasa_12,
    SUM(CASE WHEN e.QntInCont = 0 THEN 1 ELSE 0 END) AS klasa_0,
    -- KPI: Procent klas idealnych (6+7) / klas wagowych
    SUM(CASE WHEN e.QntInCont IN (6, 7) THEN 1 ELSE 0 END) * 100.0
        / NULLIF(SUM(CASE WHEN e.QntInCont > 0 THEN 1 ELSE 0 END), 0) AS proc_idealna,
    -- Średnia kg na sztukę
    SUM(e.ActWeight) / NULLIF(COUNT(*), 0) * 1.0 AS sr_kg_paleta
FROM dbo.PartiaDostawca pd
JOIN dbo.In0E e ON e.P1 = pd.Partia
WHERE e.ArticleID = '40'
  AND e.Data >= CONVERT(varchar(10), DATEADD(DAY, -90, GETDATE()), 120)
  AND e.ActWeight > 0
GROUP BY pd.CustomerID, pd.CustomerName
HAVING COUNT(*) >= 30
ORDER BY proc_idealna DESC;
GO

-- =============================================================================
-- BLOK 133 - vw_ReklamacjePelneInfo - widok pełnych danych reklamacji
-- =============================================================================

BEGIN TRY
    SELECT TOP 10 '133_A_vw_reklamacje_full' AS __SEKCJA, *
    FROM dbo.vw_ReklamacjePelneInfo
    ORDER BY DataZgloszenia DESC;
END TRY
BEGIN CATCH
    SELECT '133_A_vw_reklamacje_full' AS __SEKCJA, ERROR_MESSAGE() AS blad;
END CATCH;
GO

-- =============================================================================
-- BLOK 134 - HM.MZ.ProductionLineID - potwierdzenie czy 100% NULL
-- =============================================================================

-- Sprawdzanie czy linked server do HANDEL działa (cross-DB)
BEGIN TRY
    SELECT
        '134_A_hm_mz_productionline' AS __SEKCJA,
        COUNT(*) AS razem,
        SUM(CASE WHEN ProductionLineID IS NOT NULL THEN 1 ELSE 0 END) AS z_productionline
    FROM [192.168.0.112].Handel.HM.MZ;
END TRY
BEGIN CATCH
    SELECT '134_A_hm_mz_productionline' AS __SEKCJA,
           'Linked Server brak - sprawdz reka' AS info;
END CATCH;
GO

-- =============================================================================
-- BLOK 135 - In0E ostatnie 24h - pełen sample (porównaj z dziś)
-- =============================================================================

SELECT TOP 50
    '135_A_in0e_last24h' AS __SEKCJA,
    e.Data, e.Godzina, e.ArticleID, e.ArticleName,
    e.ActWeight, e.QntInCont,
    e.OperatorID, e.Wagowy,
    e.TermID, e.TermType,
    e.P1 AS Partia,
    e.CustomerID
FROM dbo.In0E e
WHERE e.Data >= CONVERT(varchar(10), DATEADD(DAY, -1, GETDATE()), 120)
ORDER BY e.Data DESC, e.Godzina DESC;
GO

PRINT '################################################################';
PRINT '## EKSPLORACJA LIBRANET RUNDA 5 - KONIEC                       ##';
PRINT '################################################################';
GO
