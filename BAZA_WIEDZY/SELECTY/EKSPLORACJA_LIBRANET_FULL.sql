-- =============================================================================
-- EKSPLORACJA LIBRANET - JEDEN PLIK, WSZYSTKO
-- =============================================================================
-- INSTRUKCJA:
--   1. Otworz w SSMS (Server: 192.168.0.109, login: pronova/pronova)
--   2. Wlacz tryb tekstowy: Ctrl+T
--      (lub menu: Query -> Results To -> Results To Text)
--   3. Ustaw szerokosc wiersza tekstowego: Tools -> Options -> Query Results
--      -> SQL Server -> Results To Text -> Maximum number of characters: 8192
--   4. F5 - uruchom calosc (cel: ~30 sekund)
--   5. Ctrl+A w panelu wynikow, Ctrl+C
--   6. Wklej do pliku: BAZA_WIEDZY/SELECTY/WYNIKI_RAW.txt
--   7. Daj plik agentowi w nastepnej rozmowie - dojdzie do wszystkiego
--      po pierwszej kolumnie __SEKCJA i komentarzach.
--
-- Kazdy SELECT ma pierwsza kolumne '__SEKCJA' = identyfikator bloku.
-- Bloki sa numerowane: BLOK_XX_<litera> gdzie XX = numer pliku, litera = pod-blok.
-- =============================================================================

USE LibraNet;
GO

SET NOCOUNT ON;
GO

PRINT '';
PRINT '################################################################';
PRINT '## EKSPLORACJA LIBRANET - START                                ##';
PRINT '################################################################';
PRINT '';

-- =============================================================================
-- BLOK 00 - METADANE SERWERA I BAZY
-- =============================================================================

SELECT '00_A_wersja_serwera' AS __SEKCJA, @@VERSION AS WersjaServera;
GO

SELECT
    '00_B_baza_info' AS __SEKCJA,
    DB_NAME() AS BazaDanych,
    CAST(SERVERPROPERTY('ProductVersion') AS varchar(50)) AS ProductVersion,
    CAST(SERVERPROPERTY('ProductLevel') AS varchar(50)) AS ProductLevel,
    CAST(SERVERPROPERTY('Edition') AS varchar(100)) AS Edition,
    CAST(SERVERPROPERTY('Collation') AS varchar(100)) AS DefaultCollation,
    CAST(DATABASEPROPERTYEX(DB_NAME(), 'Collation') AS varchar(100)) AS DBCollation;
GO

SELECT
    '00_C_rozmiar_bazy' AS __SEKCJA,
    name AS LogicalName,
    type_desc,
    size * 8 / 1024.0 AS SizeMB
FROM sys.master_files
WHERE database_id = DB_ID();
GO

SELECT
    '00_D_inne_bazy_na_serwerze' AS __SEKCJA,
    name AS DatabaseName,
    state_desc,
    create_date
FROM sys.databases
WHERE name NOT IN ('master','tempdb','msdb','model')
ORDER BY name;
GO

-- =============================================================================
-- BLOK 01 - LISTA TABEL (top 100 po liczbie wierszy)
-- =============================================================================

SELECT TOP 100
    '01_A_top_tabel_po_wierszach' AS __SEKCJA,
    s.name + '.' + t.name AS TableFullName,
    p.rows AS RowCount_,
    SUM(a.total_pages) * 8 / 1024.0 AS TotalMB,
    SUM(a.used_pages) * 8 / 1024.0 AS UsedMB
FROM sys.tables t
JOIN sys.schemas s ON s.schema_id = t.schema_id
JOIN sys.partitions p ON p.object_id = t.object_id AND p.index_id IN (0,1)
JOIN sys.allocation_units a ON a.container_id = p.partition_id
GROUP BY s.name, t.name, p.rows
ORDER BY p.rows DESC;
GO

SELECT
    '01_B_liczba_obiektow' AS __SEKCJA,
    (SELECT COUNT(*) FROM sys.tables) AS LiczbaTabel,
    (SELECT COUNT(*) FROM sys.views) AS LiczbaWidokow,
    (SELECT COUNT(*) FROM sys.procedures) AS LiczbaProcedur,
    (SELECT COUNT(*) FROM sys.objects WHERE type IN ('FN','IF','TF','FS','FT')) AS LiczbaFunkcji,
    (SELECT COUNT(*) FROM sys.triggers WHERE parent_class_desc = 'OBJECT_OR_COLUMN') AS LiczbaTriggerow,
    (SELECT COUNT(*) FROM sys.foreign_keys) AS LiczbaForeignKeys;
GO

SELECT TOP 30
    '01_C_najnowsze_tabele' AS __SEKCJA,
    s.name + '.' + t.name AS TableFullName,
    t.create_date,
    t.modify_date
FROM sys.tables t
JOIN sys.schemas s ON s.schema_id = t.schema_id
ORDER BY t.modify_date DESC;
GO

-- =============================================================================
-- BLOK 02 - WIDOKI, PROCEDURY, FUNKCJE, TRIGGERY
-- =============================================================================

SELECT '02_A_widoki' AS __SEKCJA, name, create_date, modify_date
FROM sys.views
ORDER BY name;
GO

SELECT '02_B_procedury' AS __SEKCJA, name, create_date, modify_date
FROM sys.procedures
ORDER BY name;
GO

SELECT '02_C_funkcje' AS __SEKCJA, name, type_desc, create_date
FROM sys.objects
WHERE type IN ('FN','IF','TF','FS','FT')
ORDER BY name;
GO

SELECT
    '02_D_triggery' AS __SEKCJA,
    OBJECT_NAME(parent_id) AS table_name,
    name AS trigger_name,
    is_disabled,
    is_instead_of_trigger,
    create_date,
    modify_date
FROM sys.triggers
WHERE parent_class_desc = 'OBJECT_OR_COLUMN'
ORDER BY OBJECT_NAME(parent_id), name;
GO

-- 02.E - Definicje top widokow (max 1500 znakow)
SELECT TOP 10
    '02_E_definicje_widokow' AS __SEKCJA,
    v.name,
    LEFT(m.definition, 1500) AS definition_preview
FROM sys.views v
JOIN sys.sql_modules m ON m.object_id = v.object_id
ORDER BY v.modify_date DESC;
GO

-- 02.F - Definicje procedur (max 1500 znakow)
SELECT
    '02_F_definicje_procedur' AS __SEKCJA,
    p.name,
    LEFT(m.definition, 1500) AS definition_preview
FROM sys.procedures p
JOIN sys.sql_modules m ON m.object_id = p.object_id
ORDER BY p.name;
GO

-- =============================================================================
-- BLOK 03 - LISTAPARTII (master partii ubojowych)
-- =============================================================================

SELECT
    '03_A_listapartii_kolumny' AS __SEKCJA,
    ORDINAL_POSITION,
    COLUMN_NAME,
    DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH,
    NUMERIC_PRECISION,
    NUMERIC_SCALE,
    IS_NULLABLE,
    COLUMN_DEFAULT
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'listapartii'
ORDER BY ORDINAL_POSITION;
GO

SELECT TOP 10 '03_B_listapartii_sample' AS __SEKCJA, *
FROM dbo.listapartii
ORDER BY CreateData DESC;
GO

SELECT
    '03_C_listapartii_status_v2' AS __SEKCJA,
    StatusV2,
    COUNT(*) AS liczba
FROM dbo.listapartii
WHERE StatusV2 IS NOT NULL
GROUP BY StatusV2
ORDER BY liczba DESC;
GO

SELECT
    '03_D_listapartii_dziaty' AS __SEKCJA,
    DirID,
    COUNT(*) AS liczba
FROM dbo.listapartii
WHERE DirID IS NOT NULL
GROUP BY DirID
ORDER BY liczba DESC;
GO

SELECT TOP 30
    '03_E_listapartii_dziennie_30dni' AS __SEKCJA,
    CreateData,
    COUNT(*) AS liczba_partii,
    SUM(CASE WHEN IsClose = 1 THEN 1 ELSE 0 END) AS zamkniete,
    SUM(CASE WHEN ISNULL(IsClose,0) = 0 THEN 1 ELSE 0 END) AS otwarte
FROM dbo.listapartii
WHERE CreateData >= CONVERT(varchar(10), DATEADD(DAY, -30, GETDATE()), 120)
GROUP BY CreateData
ORDER BY CreateData DESC;
GO

SELECT
    '03_F_listapartii_per_rok' AS __SEKCJA,
    LEFT(CONVERT(varchar(10), CreateData, 120), 4) AS rok,
    COUNT(*) AS liczba_partii
FROM dbo.listapartii
WHERE CreateData IS NOT NULL
GROUP BY LEFT(CONVERT(varchar(10), CreateData, 120), 4)
ORDER BY rok DESC;
GO

SELECT
    '03_G_listapartii_zakres_dat' AS __SEKCJA,
    MIN(CreateData) AS najstarsza,
    MAX(CreateData) AS najnowsza,
    COUNT(*) AS razem
FROM dbo.listapartii;
GO

-- =============================================================================
-- BLOK 04 - IN0E (rdzen wazen produkcji)
-- =============================================================================

SELECT
    '04_A_in0e_kolumny' AS __SEKCJA,
    ORDINAL_POSITION,
    COLUMN_NAME,
    DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH,
    NUMERIC_PRECISION,
    NUMERIC_SCALE,
    IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'In0E'
ORDER BY ORDINAL_POSITION;
GO

SELECT TOP 10 '04_B_in0e_sample' AS __SEKCJA, *
FROM dbo.In0E
ORDER BY Data DESC, Godzina DESC;
GO

SELECT
    '04_C_in0e_klasy_30dni' AS __SEKCJA,
    QntInCont AS klasa,
    COUNT(*) AS liczba_wazen,
    SUM(CASE WHEN ActWeight > 0 THEN ActWeight ELSE 0 END) AS suma_kg
FROM dbo.In0E
WHERE Data >= CONVERT(varchar(10), DATEADD(DAY, -30, GETDATE()), 120)
  AND ArticleID = '40'
GROUP BY QntInCont
ORDER BY klasa;
GO

SELECT
    '04_D_in0e_terminale_30dni' AS __SEKCJA,
    TermID,
    TermType,
    COUNT(*) AS liczba_wazen,
    MIN(Data) AS pierwsza,
    MAX(Data) AS ostatnia
FROM dbo.In0E
WHERE Data >= CONVERT(varchar(10), DATEADD(DAY, -30, GETDATE()), 120)
GROUP BY TermID, TermType
ORDER BY liczba_wazen DESC;
GO

SELECT
    '04_E_in0e_direction' AS __SEKCJA,
    Direction,
    COUNT(*) AS liczba
FROM dbo.In0E
WHERE Data >= CONVERT(varchar(10), DATEADD(DAY, -90, GETDATE()), 120)
GROUP BY Direction;
GO

SELECT
    '04_F_in0e_operatorzy_30dni' AS __SEKCJA,
    OperatorID,
    MIN(Wagowy) AS imie,
    COUNT(*) AS liczba_wazen,
    SUM(CASE WHEN ActWeight > 0 THEN 1 ELSE 0 END) AS prawidlowe,
    SUM(CASE WHEN ActWeight < 0 THEN 1 ELSE 0 END) AS storno,
    SUM(CASE WHEN ArticleID = '40' THEN 1 ELSE 0 END) AS palety_kurczaka_A,
    SUM(CASE WHEN ArticleID <> '40' THEN 1 ELSE 0 END) AS porcje_inne
FROM dbo.In0E
WHERE Data >= CONVERT(varchar(10), DATEADD(DAY, -30, GETDATE()), 120)
  AND OperatorID IS NOT NULL
GROUP BY OperatorID
ORDER BY liczba_wazen DESC;
GO

SELECT
    '04_G_in0e_histogram_godzin' AS __SEKCJA,
    LEFT(Godzina, 2) AS godzina,
    COUNT(*) AS liczba_wazen
FROM dbo.In0E
WHERE Data >= CONVERT(varchar(10), DATEADD(DAY, -30, GETDATE()), 120)
  AND ActWeight > 0
GROUP BY LEFT(Godzina, 2)
ORDER BY godzina;
GO

SELECT TOP 30
    '04_H_in0e_tolerancja_per_towar' AS __SEKCJA,
    ArticleID,
    ArticleName,
    AVG(ABS(ActWeight - Weight)) AS sr_odchylenie_kg,
    AVG(ABS(ActWeight - Weight) / NULLIF(Weight, 0) * 100) AS sr_odchylenie_proc,
    MAX(ABS(ActWeight - Weight)) AS max_odchylenie_kg,
    COUNT(*) AS liczba_wazen
FROM dbo.In0E
WHERE Data >= CONVERT(varchar(10), DATEADD(DAY, -30, GETDATE()), 120)
  AND ActWeight > 0 AND Weight > 0
GROUP BY ArticleID, ArticleName
HAVING COUNT(*) > 50
ORDER BY liczba_wazen DESC;
GO

SELECT
    '04_I_in0e_bez_partii' AS __SEKCJA,
    COUNT(*) AS liczba_bez_partii,
    MIN(Data) AS najstarsze,
    MAX(Data) AS najnowsze
FROM dbo.In0E
WHERE (P1 IS NULL OR P1 = '')
  AND Data >= CONVERT(varchar(10), DATEADD(DAY, -90, GETDATE()), 120);
GO

SELECT
    '04_J_in0e_p2_vs_p1' AS __SEKCJA,
    COUNT(*) AS razem,
    SUM(CASE WHEN P1 = P2 THEN 1 ELSE 0 END) AS p2_rowne_p1,
    SUM(CASE WHEN P2 IS NULL OR P2 = '' THEN 1 ELSE 0 END) AS p2_puste,
    SUM(CASE WHEN P1 <> P2 AND P2 IS NOT NULL AND P2 <> '' THEN 1 ELSE 0 END) AS p2_rozne_od_p1
FROM dbo.In0E
WHERE Data >= CONVERT(varchar(10), DATEADD(DAY, -90, GETDATE()), 120)
  AND P1 IS NOT NULL AND P1 <> '';
GO

SELECT
    '04_K_in0e_zakres_dat' AS __SEKCJA,
    MIN(Data) AS najstarsze_wazenie,
    MAX(Data) AS najnowsze_wazenie,
    COUNT(*) AS razem
FROM dbo.In0E;
GO

-- =============================================================================
-- BLOK 05 - ARTICLE (slownik towarow)
-- =============================================================================

SELECT
    '05_A_article_kolumny' AS __SEKCJA,
    ORDINAL_POSITION,
    COLUMN_NAME,
    DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH,
    IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'Article'
ORDER BY ORDINAL_POSITION;
GO

SELECT TOP 5 '05_B_article_kurczak_a' AS __SEKCJA, *
FROM dbo.Article
WHERE ID = '40';
GO

SELECT TOP 30
    '05_C_article_top_30' AS __SEKCJA,
    a.ID,
    a.Name,
    a.ShortName,
    COUNT(*) AS liczba_wazen,
    SUM(CASE WHEN e.ActWeight > 0 THEN e.ActWeight ELSE 0 END) AS suma_kg
FROM dbo.Article a
JOIN dbo.In0E e ON e.ArticleID = a.ID
WHERE e.Data >= CONVERT(varchar(10), DATEADD(DAY, -30, GETDATE()), 120)
GROUP BY a.ID, a.Name, a.ShortName
ORDER BY liczba_wazen DESC;
GO

SELECT TOP 100 '05_D_article_lista' AS __SEKCJA, ID, Name, ShortName
FROM dbo.Article
WHERE ID IS NOT NULL AND ID <> ''
ORDER BY Name;
GO

SELECT
    '05_E_article_statystyki' AS __SEKCJA,
    COUNT(*) AS razem,
    COUNT(DISTINCT ID) AS unikalne_id,
    SUM(CASE WHEN Name IS NULL OR Name = '' THEN 1 ELSE 0 END) AS bez_nazwy
FROM dbo.Article;
GO

SELECT
    '05_F_kolumny_tolerancji' AS __SEKCJA,
    TABLE_NAME, COLUMN_NAME, DATA_TYPE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE COLUMN_NAME LIKE '%Toler%'
   OR COLUMN_NAME LIKE '%MinW%'
   OR COLUMN_NAME LIKE '%MaxW%'
   OR COLUMN_NAME LIKE '%WeightStandard%'
   OR COLUMN_NAME LIKE '%StdWeight%'
ORDER BY TABLE_NAME, COLUMN_NAME;
GO

-- =============================================================================
-- BLOK 06 - PARTIADOSTAWCA (hodowcy + dekoder partii)
-- =============================================================================

SELECT
    '06_A_partiadostawca_kolumny' AS __SEKCJA,
    ORDINAL_POSITION,
    COLUMN_NAME,
    DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH,
    IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'PartiaDostawca'
ORDER BY ORDINAL_POSITION;
GO

SELECT TOP 10 '06_B_partiadostawca_sample' AS __SEKCJA, *
FROM dbo.PartiaDostawca
ORDER BY CreateData DESC;
GO

SELECT TOP 30
    '06_C_top_hodowcow_90dni' AS __SEKCJA,
    pd.CustomerID,
    pd.CustomerName,
    COUNT(*) AS liczba_partii,
    MIN(pd.CreateData) AS pierwsza_dostawa,
    MAX(pd.CreateData) AS ostatnia_dostawa
FROM dbo.PartiaDostawca pd
WHERE pd.CreateData >= CONVERT(varchar(10), DATEADD(DAY, -90, GETDATE()), 120)
GROUP BY pd.CustomerID, pd.CustomerName
ORDER BY liczba_partii DESC;
GO

SELECT
    '06_D_duplikaty_hodowcow' AS __SEKCJA,
    CustomerName,
    COUNT(DISTINCT CustomerID) AS liczba_id
FROM dbo.PartiaDostawca
GROUP BY CustomerName
HAVING COUNT(DISTINCT CustomerID) > 1
ORDER BY liczba_id DESC;
GO

SELECT
    '06_E_unikalne_hodowcy' AS __SEKCJA,
    COUNT(DISTINCT CustomerID) AS unikalne_id,
    COUNT(DISTINCT CustomerName) AS unikalne_nazwy,
    COUNT(*) AS partii_lacznie
FROM dbo.PartiaDostawca;
GO

SELECT TOP 5
    '06_F_dekoder_partii' AS __SEKCJA,
    pd.CustomerID,
    pd.Partia,
    pd.CustomerName,
    pd.CreateData,
    LEFT(pd.Partia, 2) AS rok_z_partii,
    SUBSTRING(pd.Partia, 3, 3) AS dzien_z_partii,
    SUBSTRING(pd.Partia, 6, 3) AS auto_z_partii
FROM dbo.PartiaDostawca pd
WHERE pd.Partia IS NOT NULL
  AND LEN(pd.Partia) = 8
ORDER BY pd.CreateData DESC;
GO

-- =============================================================================
-- BLOK 07 - HARMONOGRAMDOSTAW + FARMERCALC + WSTAWIENIAKURCZAKOW
-- =============================================================================

SELECT
    '07_A_harmonogram_kolumny' AS __SEKCJA,
    ORDINAL_POSITION, COLUMN_NAME, DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'HarmonogramDostaw'
ORDER BY ORDINAL_POSITION;
GO

SELECT
    '07_B_farmercalc_kolumny' AS __SEKCJA,
    ORDINAL_POSITION, COLUMN_NAME, DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'FarmerCalc'
ORDER BY ORDINAL_POSITION;
GO

SELECT
    '07_C_wstawienia_kolumny' AS __SEKCJA,
    ORDINAL_POSITION, COLUMN_NAME, DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'WstawieniaKurczakow'
ORDER BY ORDINAL_POSITION;
GO

SELECT TOP 10 '07_D_harmonogram_sample' AS __SEKCJA, *
FROM dbo.HarmonogramDostaw
ORDER BY DataOdbioru DESC;
GO

SELECT TOP 10 '07_E_farmercalc_sample' AS __SEKCJA, *
FROM dbo.FarmerCalc;
GO

SELECT TOP 10 '07_F_wstawienia_sample' AS __SEKCJA, *
FROM dbo.WstawieniaKurczakow;
GO

SELECT
    '07_G_harmonogram_30dni' AS __SEKCJA,
    DataOdbioru,
    COUNT(*) AS liczba_dostaw,
    SUM(SztukiDek) AS suma_sztuk,
    SUM(WagaDek) AS suma_kg,
    COUNT(DISTINCT Dostawca) AS liczba_hodowcow
FROM dbo.HarmonogramDostaw
WHERE DataOdbioru >= CONVERT(varchar(10), DATEADD(DAY, -30, GETDATE()), 120)
GROUP BY DataOdbioru
ORDER BY DataOdbioru DESC;
GO

SELECT '07_H_liczby' AS __SEKCJA, 'HarmonogramDostaw' AS tabela, COUNT(*) AS rekordow FROM dbo.HarmonogramDostaw
UNION ALL SELECT '07_H_liczby', 'FarmerCalc', COUNT(*) FROM dbo.FarmerCalc
UNION ALL SELECT '07_H_liczby', 'WstawieniaKurczakow', COUNT(*) FROM dbo.WstawieniaKurczakow;
GO

-- =============================================================================
-- BLOK 08 - ZAMOWIENIAMIESO (zamowienia od klientow)
-- =============================================================================

SELECT
    '08_A_zamowieniamieso_kolumny' AS __SEKCJA,
    ORDINAL_POSITION, COLUMN_NAME, DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'ZamowieniaMieso'
ORDER BY ORDINAL_POSITION;
GO

SELECT
    '08_B_zamowieniamieso_towar_kolumny' AS __SEKCJA,
    ORDINAL_POSITION, COLUMN_NAME, DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'ZamowieniaMiesoTowar'
ORDER BY ORDINAL_POSITION;
GO

SELECT
    '08_C_inne_zamowien_kolumny' AS __SEKCJA,
    TABLE_NAME, COLUMN_NAME, DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME IN ('ZamowieniaMiesoProdukcjaNotatki',
                     'ZamowieniaMiesoSnapshot',
                     'SzablonyZamowien',
                     'SzablonyZamowienTowar',
                     'HistoriaZmianZamowien',
                     'ZamowienieWydanieRoznice')
ORDER BY TABLE_NAME, ORDINAL_POSITION;
GO

SELECT TOP 10 '08_D_zamowienia_sample' AS __SEKCJA, *
FROM dbo.ZamowieniaMieso
ORDER BY Id DESC;
GO

SELECT
    '08_E_zamowienia_status' AS __SEKCJA,
    Status,
    COUNT(*) AS liczba
FROM dbo.ZamowieniaMieso
WHERE DataZamowienia >= CONVERT(varchar(10), DATEADD(DAY, -90, GETDATE()), 120)
GROUP BY Status
ORDER BY liczba DESC;
GO

SELECT
    '08_F_zamowienia_transport_status' AS __SEKCJA,
    TransportStatus,
    COUNT(*) AS liczba
FROM dbo.ZamowieniaMieso
WHERE DataZamowienia >= CONVERT(varchar(10), DATEADD(DAY, -90, GETDATE()), 120)
  AND TransportStatus IS NOT NULL
GROUP BY TransportStatus
ORDER BY liczba DESC;
GO

SELECT TOP 30
    '08_G_top_klientow_90dni' AS __SEKCJA,
    KlientId,
    COUNT(*) AS liczba_zamowien,
    SUM(LiczbaPojemnikow) AS suma_pojemnikow,
    SUM(LiczbaPalet) AS suma_palet
FROM dbo.ZamowieniaMieso
WHERE DataZamowienia >= CONVERT(varchar(10), DATEADD(DAY, -90, GETDATE()), 120)
GROUP BY KlientId
ORDER BY liczba_zamowien DESC;
GO

SELECT
    '08_H_anulacje_30dni' AS __SEKCJA,
    CONVERT(varchar(10), DataZamowienia, 120) AS data_,
    COUNT(*) AS razem,
    SUM(CASE WHEN Status = 'Anulowane' THEN 1 ELSE 0 END) AS anulowane,
    SUM(CASE WHEN Status = 'Zrealizowane' THEN 1 ELSE 0 END) AS zrealizowane
FROM dbo.ZamowieniaMieso
WHERE DataZamowienia >= CONVERT(varchar(10), DATEADD(DAY, -30, GETDATE()), 120)
GROUP BY CONVERT(varchar(10), DataZamowienia, 120)
ORDER BY data_ DESC;
GO

SELECT '08_I_liczby' AS __SEKCJA, 'ZamowieniaMieso' AS tabela, COUNT(*) AS rekordow FROM dbo.ZamowieniaMieso
UNION ALL SELECT '08_I_liczby', 'ZamowieniaMiesoTowar', COUNT(*) FROM dbo.ZamowieniaMiesoTowar
UNION ALL SELECT '08_I_liczby', 'ZamowieniaMiesoProdukcjaNotatki', COUNT(*) FROM dbo.ZamowieniaMiesoProdukcjaNotatki
UNION ALL SELECT '08_I_liczby', 'ZamowieniaMiesoSnapshot', COUNT(*) FROM dbo.ZamowieniaMiesoSnapshot
UNION ALL SELECT '08_I_liczby', 'SzablonyZamowien', COUNT(*) FROM dbo.SzablonyZamowien
UNION ALL SELECT '08_I_liczby', 'SzablonyZamowienTowar', COUNT(*) FROM dbo.SzablonyZamowienTowar
UNION ALL SELECT '08_I_liczby', 'HistoriaZmianZamowien', COUNT(*) FROM dbo.HistoriaZmianZamowien;
GO

-- =============================================================================
-- BLOK 09 - KARTOTEKA ODBIORCY (CRM klientow)
-- =============================================================================

SELECT
    '09_A_kartoteka_kolumny' AS __SEKCJA,
    TABLE_NAME, ORDINAL_POSITION, COLUMN_NAME, DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME IN ('KartotekaOdbiorcyDane',
                     'KartotekaOdbiorcyKontakty',
                     'KartotekaOdbiorcyNotatki',
                     'KartotekaPrzypomnienia',
                     'KartotekaScoring',
                     'KartotekaHistoriaZmian')
ORDER BY TABLE_NAME, ORDINAL_POSITION;
GO

SELECT '09_B_kartoteka_liczby' AS __SEKCJA, 'KartotekaOdbiorcyDane' AS tabela, COUNT(*) AS rekordow FROM dbo.KartotekaOdbiorcyDane
UNION ALL SELECT '09_B_kartoteka_liczby', 'KartotekaOdbiorcyKontakty', COUNT(*) FROM dbo.KartotekaOdbiorcyKontakty
UNION ALL SELECT '09_B_kartoteka_liczby', 'KartotekaOdbiorcyNotatki', COUNT(*) FROM dbo.KartotekaOdbiorcyNotatki
UNION ALL SELECT '09_B_kartoteka_liczby', 'KartotekaPrzypomnienia', COUNT(*) FROM dbo.KartotekaPrzypomnienia
UNION ALL SELECT '09_B_kartoteka_liczby', 'KartotekaScoring', COUNT(*) FROM dbo.KartotekaScoring
UNION ALL SELECT '09_B_kartoteka_liczby', 'KartotekaHistoriaZmian', COUNT(*) FROM dbo.KartotekaHistoriaZmian;
GO

SELECT TOP 5 '09_C_dane_sample' AS __SEKCJA, * FROM dbo.KartotekaOdbiorcyDane;
GO

SELECT TOP 5 '09_D_kontakty_sample' AS __SEKCJA, * FROM dbo.KartotekaOdbiorcyKontakty;
GO

SELECT TOP 5 '09_E_notatki_sample' AS __SEKCJA, * FROM dbo.KartotekaOdbiorcyNotatki;
GO

SELECT TOP 5 '09_F_przypomnienia_sample' AS __SEKCJA, * FROM dbo.KartotekaPrzypomnienia;
GO

SELECT TOP 5 '09_G_scoring_sample' AS __SEKCJA, * FROM dbo.KartotekaScoring;
GO

-- =============================================================================
-- BLOK 10 - FOREIGN KEYS, INDEKSY, CONSTRAINTS
-- =============================================================================

SELECT
    '10_A_foreign_keys' AS __SEKCJA,
    fk.name AS FK_Name,
    OBJECT_NAME(fk.parent_object_id) AS Tabela_dziecko,
    cp.name AS Kolumna_dziecko,
    OBJECT_NAME(fk.referenced_object_id) AS Tabela_rodzic,
    cr.name AS Kolumna_rodzic,
    fk.delete_referential_action_desc AS OnDelete,
    fk.update_referential_action_desc AS OnUpdate
FROM sys.foreign_keys fk
JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
JOIN sys.columns cp ON cp.object_id = fkc.parent_object_id AND cp.column_id = fkc.parent_column_id
JOIN sys.columns cr ON cr.object_id = fkc.referenced_object_id AND cr.column_id = fkc.referenced_column_id
ORDER BY OBJECT_NAME(fk.parent_object_id), fk.name;
GO

SELECT
    '10_B_indeksy_kluczowych' AS __SEKCJA,
    OBJECT_NAME(i.object_id) AS table_name,
    i.name AS index_name,
    i.type_desc,
    c.name AS column_name,
    ic.key_ordinal,
    i.is_unique,
    i.is_primary_key
FROM sys.indexes i
JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
WHERE OBJECT_NAME(i.object_id) IN (
    'In0E','Out1A','Article','PartiaDostawca','listapartii',
    'ZamowieniaMieso','ZamowieniaMiesoTowar','HarmonogramDostaw',
    'FarmerCalc','PartiaStatus','QC_Normy','KartotekaOdbiorcyDane',
    'Pozyskiwanie_Hodowcy','Kierowca','Pojazd','Kurs','Ladunek'
)
ORDER BY table_name, index_name, key_ordinal;
GO

SELECT
    '10_C_indeksy_per_tabela' AS __SEKCJA,
    OBJECT_NAME(i.object_id) AS table_name,
    COUNT(DISTINCT i.index_id) AS liczba_indeksow,
    SUM(CASE WHEN i.is_primary_key = 1 THEN 1 ELSE 0 END) AS pk_count,
    SUM(CASE WHEN i.is_unique = 1 AND i.is_primary_key = 0 THEN 1 ELSE 0 END) AS unique_count
FROM sys.indexes i
WHERE i.index_id > 0
  AND OBJECT_NAME(i.object_id) NOT LIKE 'sys%'
GROUP BY i.object_id
HAVING COUNT(DISTINCT i.index_id) > 0
ORDER BY liczba_indeksow DESC;
GO

SELECT TOP 50
    '10_D_default_constraints' AS __SEKCJA,
    OBJECT_NAME(parent_object_id) AS table_name,
    name AS constraint_name,
    OBJECT_DEFINITION(object_id) AS definicja
FROM sys.default_constraints
ORDER BY table_name;
GO

SELECT TOP 50
    '10_E_check_constraints' AS __SEKCJA,
    OBJECT_NAME(parent_object_id) AS table_name,
    name AS constraint_name,
    definition
FROM sys.check_constraints
ORDER BY table_name;
GO

-- =============================================================================
-- BLOK 11 - QUIRKI: TYPY DATA/GODZINA/STATUS
-- =============================================================================

SELECT
    '11_A_typy_data_godzina' AS __SEKCJA,
    TABLE_NAME,
    COLUMN_NAME,
    DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH,
    IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE COLUMN_NAME IN ('Data','Godzina','Czas','CreateData','CreateGodzina',
                      'DataOdbioru','DataZamowienia','DataUboju','DataProdukcji',
                      'ModificationData','ModificationGodzina','CalcDate',
                      'Wyjazd','Zaladunek','Przyjazd','DataPrzyjazdu','DataPowrotu')
ORDER BY TABLE_NAME, COLUMN_NAME;
GO

SELECT
    '11_B_kolumny_status' AS __SEKCJA,
    TABLE_NAME,
    COLUMN_NAME,
    DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH
FROM INFORMATION_SCHEMA.COLUMNS
WHERE COLUMN_NAME IN ('IsClose', 'Status', 'StatusV2', 'TransportStatus',
                      'IsActive', 'Aktywny', 'IsCancelled', 'Anulowane')
ORDER BY TABLE_NAME, COLUMN_NAME;
GO

SELECT
    '11_C_kolumny_guid' AS __SEKCJA,
    TABLE_NAME, COLUMN_NAME, DATA_TYPE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE DATA_TYPE = 'uniqueidentifier'
ORDER BY TABLE_NAME, COLUMN_NAME;
GO

SELECT
    '11_D_kolumny_klienta' AS __SEKCJA,
    TABLE_NAME, COLUMN_NAME, DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH
FROM INFORMATION_SCHEMA.COLUMNS
WHERE COLUMN_NAME IN ('CustomerID', 'CustomerName', 'KlientId', 'KontrahentId',
                      'DostawcaId', 'OperatorID', 'KierowcaID', 'PojazdID')
ORDER BY TABLE_NAME, COLUMN_NAME;
GO

-- =============================================================================
-- BLOK 12 - ROZSZERZENIA ZPSP (PartiaStatus, QC_*, Flota, Pozyskiwanie)
-- =============================================================================

SELECT
    '12_A_extensions_kolumny' AS __SEKCJA,
    TABLE_NAME,
    COLUMN_NAME,
    DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH,
    IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME IN (
    'PartiaStatus', 'PartiaAuditLog', 'QC_Normy', 'QC_Zdjecia',
    'TransportZmiany', 'DriverDetails', 'VehicleDetails',
    'DriverVehicleAssignment', 'VehicleServiceLog',
    'ArticleAuditLog', 'ArticleFavorites',
    'Pozyskiwanie_Hodowcy', 'Pozyskiwanie_Aktywnosci',
    'CallReminderLog', 'CallReminderContacts',
    'AuditLog_Dostawy', 'OdpadyRejestr',
    'StanyMagazynowe', 'DokumentyWZ', 'TowarZdjecia',
    'DostawaFeedback', 'DashboardWidoki',
    'KonfiguracjaProdukty', 'KonfiguracjaWydajnosc',
    'AvilogHodowcyMapping', 'WstawieniaKurczakow',
    'Dostawcy', 'DOSTAWCY', 'DostawcyCR', 'DostawcyCRItem',
    'RozliczeniaZatwierdzenia',
    'SmsHistory', 'SmsChangeLog', 'ContactHistory',
    'AppSettings', 'KolejnoscTowarow', 'PriceType',
    'CenaTuszki', 'CenaMinisterialna', 'CenaRolnicza',
    'Haccp', 'Out1A',
    'Kierowca', 'Pojazd', 'Kurs', 'Ladunek', 'MatrycaTransferLog'
)
ORDER BY TABLE_NAME, ORDINAL_POSITION;
GO

SELECT '12_B_extensions_liczby' AS __SEKCJA, 'PartiaStatus' AS tabela, COUNT(*) AS rekordow FROM dbo.PartiaStatus
UNION ALL SELECT '12_B_extensions_liczby', 'PartiaAuditLog', COUNT(*) FROM dbo.PartiaAuditLog
UNION ALL SELECT '12_B_extensions_liczby', 'QC_Normy', COUNT(*) FROM dbo.QC_Normy
UNION ALL SELECT '12_B_extensions_liczby', 'QC_Zdjecia', COUNT(*) FROM dbo.QC_Zdjecia
UNION ALL SELECT '12_B_extensions_liczby', 'TransportZmiany', COUNT(*) FROM dbo.TransportZmiany
UNION ALL SELECT '12_B_extensions_liczby', 'DriverDetails', COUNT(*) FROM dbo.DriverDetails
UNION ALL SELECT '12_B_extensions_liczby', 'VehicleDetails', COUNT(*) FROM dbo.VehicleDetails
UNION ALL SELECT '12_B_extensions_liczby', 'DriverVehicleAssignment', COUNT(*) FROM dbo.DriverVehicleAssignment
UNION ALL SELECT '12_B_extensions_liczby', 'VehicleServiceLog', COUNT(*) FROM dbo.VehicleServiceLog
UNION ALL SELECT '12_B_extensions_liczby', 'ArticleAuditLog', COUNT(*) FROM dbo.ArticleAuditLog
UNION ALL SELECT '12_B_extensions_liczby', 'ArticleFavorites', COUNT(*) FROM dbo.ArticleFavorites
UNION ALL SELECT '12_B_extensions_liczby', 'Pozyskiwanie_Hodowcy', COUNT(*) FROM dbo.Pozyskiwanie_Hodowcy
UNION ALL SELECT '12_B_extensions_liczby', 'Pozyskiwanie_Aktywnosci', COUNT(*) FROM dbo.Pozyskiwanie_Aktywnosci
UNION ALL SELECT '12_B_extensions_liczby', 'CallReminderLog', COUNT(*) FROM dbo.CallReminderLog
UNION ALL SELECT '12_B_extensions_liczby', 'CallReminderContacts', COUNT(*) FROM dbo.CallReminderContacts
UNION ALL SELECT '12_B_extensions_liczby', 'AuditLog_Dostawy', COUNT(*) FROM dbo.AuditLog_Dostawy
UNION ALL SELECT '12_B_extensions_liczby', 'OdpadyRejestr', COUNT(*) FROM dbo.OdpadyRejestr
UNION ALL SELECT '12_B_extensions_liczby', 'StanyMagazynowe', COUNT(*) FROM dbo.StanyMagazynowe
UNION ALL SELECT '12_B_extensions_liczby', 'DokumentyWZ', COUNT(*) FROM dbo.DokumentyWZ
UNION ALL SELECT '12_B_extensions_liczby', 'TowarZdjecia', COUNT(*) FROM dbo.TowarZdjecia
UNION ALL SELECT '12_B_extensions_liczby', 'DashboardWidoki', COUNT(*) FROM dbo.DashboardWidoki
UNION ALL SELECT '12_B_extensions_liczby', 'AvilogHodowcyMapping', COUNT(*) FROM dbo.AvilogHodowcyMapping
UNION ALL SELECT '12_B_extensions_liczby', 'WstawieniaKurczakow', COUNT(*) FROM dbo.WstawieniaKurczakow
UNION ALL SELECT '12_B_extensions_liczby', 'DostawcyCR', COUNT(*) FROM dbo.DostawcyCR
UNION ALL SELECT '12_B_extensions_liczby', 'DostawcyCRItem', COUNT(*) FROM dbo.DostawcyCRItem
UNION ALL SELECT '12_B_extensions_liczby', 'RozliczeniaZatwierdzenia', COUNT(*) FROM dbo.RozliczeniaZatwierdzenia
UNION ALL SELECT '12_B_extensions_liczby', 'SmsHistory', COUNT(*) FROM dbo.SmsHistory
UNION ALL SELECT '12_B_extensions_liczby', 'ContactHistory', COUNT(*) FROM dbo.ContactHistory
UNION ALL SELECT '12_B_extensions_liczby', 'AppSettings', COUNT(*) FROM dbo.AppSettings
UNION ALL SELECT '12_B_extensions_liczby', 'KonfiguracjaWydajnosc', COUNT(*) FROM dbo.KonfiguracjaWydajnosc
UNION ALL SELECT '12_B_extensions_liczby', 'CenaTuszki', COUNT(*) FROM dbo.CenaTuszki
UNION ALL SELECT '12_B_extensions_liczby', 'Haccp', COUNT(*) FROM dbo.Haccp
UNION ALL SELECT '12_B_extensions_liczby', 'Out1A', COUNT(*) FROM dbo.Out1A
UNION ALL SELECT '12_B_extensions_liczby', 'Kierowca', COUNT(*) FROM dbo.Kierowca
UNION ALL SELECT '12_B_extensions_liczby', 'Pojazd', COUNT(*) FROM dbo.Pojazd
UNION ALL SELECT '12_B_extensions_liczby', 'Kurs', COUNT(*) FROM dbo.Kurs
UNION ALL SELECT '12_B_extensions_liczby', 'Ladunek', COUNT(*) FROM dbo.Ladunek
ORDER BY tabela;
GO

-- =============================================================================
-- BLOK 13 - SAMPLE Z TABEL ROZSZERZEN (top 5-10 z kazdej kluczowej)
-- =============================================================================

SELECT TOP 10 '13_A_partiastatus_sample' AS __SEKCJA, * FROM dbo.PartiaStatus;
GO

SELECT '13_B_qc_normy_all' AS __SEKCJA, * FROM dbo.QC_Normy;
GO

SELECT TOP 10 '13_C_qc_zdjecia_sample' AS __SEKCJA, * FROM dbo.QC_Zdjecia;
GO

SELECT TOP 10 '13_D_pozyskiwanie_hodowcy_sample' AS __SEKCJA, * FROM dbo.Pozyskiwanie_Hodowcy;
GO

SELECT TOP 10 '13_E_pozyskiwanie_aktywnosci_sample' AS __SEKCJA, * FROM dbo.Pozyskiwanie_Aktywnosci;
GO

SELECT TOP 10 '13_F_dashboard_widoki_sample' AS __SEKCJA, * FROM dbo.DashboardWidoki;
GO

SELECT TOP 20 '13_G_appsettings_sample' AS __SEKCJA, * FROM dbo.AppSettings;
GO

SELECT TOP 20 '13_H_konfig_wydajnosc' AS __SEKCJA, * FROM dbo.KonfiguracjaWydajnosc;
GO

SELECT TOP 20 '13_I_kolejnosc_towarow' AS __SEKCJA, * FROM dbo.KolejnoscTowarow;
GO

SELECT TOP 10 '13_J_pricetype' AS __SEKCJA, * FROM dbo.PriceType;
GO

SELECT TOP 10 '13_K_cena_tuszki' AS __SEKCJA, * FROM dbo.CenaTuszki ORDER BY 1 DESC;
GO

SELECT TOP 10 '13_L_haccp_sample' AS __SEKCJA, * FROM dbo.Haccp ORDER BY 1 DESC;
GO

SELECT TOP 10 '13_M_out1a_sample' AS __SEKCJA, * FROM dbo.Out1A ORDER BY 1 DESC;
GO

SELECT
    '13_N_out1a_zakres' AS __SEKCJA,
    MIN(Data) AS najstarszy,
    MAX(Data) AS najnowszy,
    COUNT(*) AS razem
FROM dbo.Out1A;
GO

SELECT TOP 10 '13_O_dostawcycr_proposed' AS __SEKCJA, *
FROM dbo.DostawcyCR
WHERE Status = 'Proposed';
GO

SELECT TOP 10 '13_P_avilog_mapping' AS __SEKCJA, *
FROM dbo.AvilogHodowcyMapping;
GO

SELECT TOP 5 '13_Q_kierowcy_sample' AS __SEKCJA, * FROM dbo.Kierowca;
GO

SELECT TOP 5 '13_R_pojazdy_sample' AS __SEKCJA, * FROM dbo.Pojazd;
GO

SELECT TOP 5 '13_S_kursy_sample' AS __SEKCJA, * FROM dbo.Kurs;
GO

SELECT TOP 5 '13_T_ladunki_sample' AS __SEKCJA, * FROM dbo.Ladunek;
GO

SELECT TOP 10 '13_U_smshistory_sample' AS __SEKCJA, * FROM dbo.SmsHistory ORDER BY 1 DESC;
GO

SELECT TOP 10 '13_V_contacthistory_sample' AS __SEKCJA, * FROM dbo.ContactHistory ORDER BY 1 DESC;
GO

SELECT TOP 10 '13_W_callreminderlog_sample' AS __SEKCJA, * FROM dbo.CallReminderLog ORDER BY 1 DESC;
GO

SELECT TOP 10 '13_X_stanymagazynowe_sample' AS __SEKCJA, * FROM dbo.StanyMagazynowe ORDER BY 1 DESC;
GO

SELECT TOP 10 '13_Y_dokumentywz_sample' AS __SEKCJA, * FROM dbo.DokumentyWZ ORDER BY 1 DESC;
GO

SELECT TOP 10 '13_Z_odpadyrejestr_sample' AS __SEKCJA, * FROM dbo.OdpadyRejestr ORDER BY 1 DESC;
GO

-- =============================================================================
-- BLOK 14 - WIDOKI Z DEFINICJAMI
-- =============================================================================

SELECT
    '14_A_widoki_pelne' AS __SEKCJA,
    v.name,
    LEFT(m.definition, 2500) AS definition_preview
FROM sys.views v
JOIN sys.sql_modules m ON m.object_id = v.object_id
ORDER BY v.name;
GO

-- 14.B - Sample z kluczowych widokow
SELECT TOP 5 '14_B_v_wstawienia_sample' AS __SEKCJA, * FROM dbo.v_WstawieniaDoKontaktu;
GO

SELECT TOP 5 '14_C_vw_qc_podsum_sample' AS __SEKCJA, * FROM dbo.vw_QC_Podsum;
GO

SELECT TOP 5 '14_D_vw_qc_wadyskale_sample' AS __SEKCJA, * FROM dbo.vw_QC_WadySkale;
GO

-- =============================================================================
-- BLOK 15 - ALL TABLE COLUMNS (catalog calej bazy - powolny ale komplet)
-- =============================================================================

SELECT
    '15_A_full_catalog' AS __SEKCJA,
    TABLE_SCHEMA,
    TABLE_NAME,
    COLUMN_NAME,
    ORDINAL_POSITION,
    DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH,
    NUMERIC_PRECISION,
    NUMERIC_SCALE,
    IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = 'dbo'
ORDER BY TABLE_NAME, ORDINAL_POSITION;
GO

PRINT '';
PRINT '################################################################';
PRINT '## EKSPLORACJA LIBRANET - KONIEC                              ##';
PRINT '################################################################';
PRINT '';
GO

-- =============================================================================
-- TIPY PO ZAKONCZENIU:
--
-- 1) Aby zapisac wyniki - prawy klik na panel wynikow -> Save Results As -> .txt
--    Albo Ctrl+A + Ctrl+C i wklej do pliku WYNIKI_RAW.txt
--
-- 2) Jesli ktoras tabela nie istnieje (BLOK 12.B / 13.X) - zignoruj blad,
--    reszta uruchomi sie dalej.
--
-- 3) Jesli SQL jest starszy (2008 R2) i niektore funkcje nie dzialaja - daj znac
--    agentowi, ktore bloki zwrocily blad.
--
-- 4) Wynik (~10-50 MB tekstu) wrzuc agentowi z prosba o analize -
--    sam dojdzie do wszystkiego po pierwszej kolumnie __SEKCJA.
-- =============================================================================
