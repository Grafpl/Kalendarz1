-- =====================================================================
-- DIAGNOSTYKA OPAKOWAN V2 — pelna analiza przed indeksowaniem
-- Uruchom w SSMS na serwerze 192.168.0.112 (baza Handel)
-- =====================================================================

-- =====================================================================
-- A. INDEKSY — co juz istnieje na MG, MZ, TW
-- =====================================================================

-- A1. Wszystkie indeksy na MG z kolumnami i rozmiarami
SELECT
    i.name AS [Indeks],
    i.type_desc AS [Typ],
    i.is_unique AS [Unikalny],
    i.filter_definition AS [Filtr],
    STRING_AGG(CASE WHEN ic.is_included_column = 0 THEN c.name END, ', ') WITHIN GROUP (ORDER BY ic.key_ordinal) AS [Kolumny klucza],
    STRING_AGG(CASE WHEN ic.is_included_column = 1 THEN c.name END, ', ') WITHIN GROUP (ORDER BY ic.key_ordinal) AS [Kolumny INCLUDE],
    ps.row_count AS [Wiersze],
    CAST(ps.used_page_count * 8.0 / 1024 AS DECIMAL(10,2)) AS [Rozmiar MB]
FROM sys.indexes i
INNER JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
INNER JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
LEFT JOIN sys.dm_db_partition_stats ps ON i.object_id = ps.object_id AND i.index_id = ps.index_id
WHERE i.object_id = OBJECT_ID('[HM].[MG]')
GROUP BY i.name, i.type_desc, i.is_unique, i.filter_definition, ps.row_count, ps.used_page_count
ORDER BY i.type_desc, i.name;

-- A2. Wszystkie indeksy na MZ
SELECT
    i.name AS [Indeks],
    i.type_desc AS [Typ],
    i.is_unique AS [Unikalny],
    i.filter_definition AS [Filtr],
    STRING_AGG(CASE WHEN ic.is_included_column = 0 THEN c.name END, ', ') WITHIN GROUP (ORDER BY ic.key_ordinal) AS [Kolumny klucza],
    STRING_AGG(CASE WHEN ic.is_included_column = 1 THEN c.name END, ', ') WITHIN GROUP (ORDER BY ic.key_ordinal) AS [Kolumny INCLUDE],
    ps.row_count AS [Wiersze],
    CAST(ps.used_page_count * 8.0 / 1024 AS DECIMAL(10,2)) AS [Rozmiar MB]
FROM sys.indexes i
INNER JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
INNER JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
LEFT JOIN sys.dm_db_partition_stats ps ON i.object_id = ps.object_id AND i.index_id = ps.index_id
WHERE i.object_id = OBJECT_ID('[HM].[MZ]')
GROUP BY i.name, i.type_desc, i.is_unique, i.filter_definition, ps.row_count, ps.used_page_count
ORDER BY i.type_desc, i.name;

-- A3. Wszystkie indeksy na TW
SELECT
    i.name AS [Indeks],
    i.type_desc AS [Typ],
    STRING_AGG(CASE WHEN ic.is_included_column = 0 THEN c.name END, ', ') WITHIN GROUP (ORDER BY ic.key_ordinal) AS [Kolumny klucza],
    ps.row_count AS [Wiersze],
    CAST(ps.used_page_count * 8.0 / 1024 AS DECIMAL(10,2)) AS [Rozmiar MB]
FROM sys.indexes i
INNER JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
INNER JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
LEFT JOIN sys.dm_db_partition_stats ps ON i.object_id = ps.object_id AND i.index_id = ps.index_id
WHERE i.object_id = OBJECT_ID('[HM].[TW]')
GROUP BY i.name, i.type_desc, ps.row_count, ps.used_page_count
ORDER BY i.name;

-- A4. Indeksy na STContractors
SELECT
    i.name AS [Indeks],
    i.type_desc AS [Typ],
    STRING_AGG(CASE WHEN ic.is_included_column = 0 THEN c.name END, ', ') WITHIN GROUP (ORDER BY ic.key_ordinal) AS [Kolumny klucza],
    ps.row_count AS [Wiersze]
FROM sys.indexes i
INNER JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
INNER JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
LEFT JOIN sys.dm_db_partition_stats ps ON i.object_id = ps.object_id AND i.index_id = ps.index_id
WHERE i.object_id = OBJECT_ID('[SSCommon].[STContractors]')
GROUP BY i.name, i.type_desc, ps.row_count, ps.used_page_count
ORDER BY i.name;

-- A5. Indeksy na ContractorClassification
SELECT
    i.name AS [Indeks],
    i.type_desc AS [Typ],
    STRING_AGG(CASE WHEN ic.is_included_column = 0 THEN c.name END, ', ') WITHIN GROUP (ORDER BY ic.key_ordinal) AS [Kolumny klucza],
    ps.row_count AS [Wiersze]
FROM sys.indexes i
INNER JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
INNER JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
LEFT JOIN sys.dm_db_partition_stats ps ON i.object_id = ps.object_id AND i.index_id = ps.index_id
WHERE i.object_id = OBJECT_ID('[SSCommon].[ContractorClassification]')
GROUP BY i.name, i.type_desc, ps.row_count, ps.used_page_count
ORDER BY i.name;

-- A6. Rozmiary tabel (dane + indeksy)
SELECT
    OBJECT_SCHEMA_NAME(t.object_id) + '.' + t.name AS [Tabela],
    SUM(ps.row_count) AS [Wiersze],
    CAST(SUM(ps.reserved_page_count) * 8.0 / 1024 AS DECIMAL(10,2)) AS [Rozmiar calkowity MB],
    CAST(SUM(CASE WHEN i.type <= 1 THEN ps.used_page_count ELSE 0 END) * 8.0 / 1024 AS DECIMAL(10,2)) AS [Dane MB],
    CAST(SUM(CASE WHEN i.type > 1 THEN ps.used_page_count ELSE 0 END) * 8.0 / 1024 AS DECIMAL(10,2)) AS [Indeksy MB]
FROM sys.tables t
INNER JOIN sys.indexes i ON t.object_id = i.object_id
INNER JOIN sys.dm_db_partition_stats ps ON i.object_id = ps.object_id AND i.index_id = ps.index_id
WHERE t.object_id IN (
    OBJECT_ID('[HM].[MG]'), OBJECT_ID('[HM].[MZ]'), OBJECT_ID('[HM].[TW]'),
    OBJECT_ID('[SSCommon].[STContractors]'), OBJECT_ID('[SSCommon].[ContractorClassification]')
)
GROUP BY t.object_id, t.name
ORDER BY [Rozmiar calkowity MB] DESC;

-- =====================================================================
-- B. UZYCIE INDEKSOW — ktore sa uzywane a ktore nie
-- =====================================================================

-- B1. Statystyki uzycia indeksow na MG (od ostatniego restartu SQL Server)
SELECT
    i.name AS [Indeks],
    ius.user_seeks AS [Seeks],
    ius.user_scans AS [Scans],
    ius.user_lookups AS [Lookups],
    ius.user_updates AS [Updates],
    ius.last_user_seek AS [Ostatni Seek],
    ius.last_user_scan AS [Ostatni Scan]
FROM sys.indexes i
LEFT JOIN sys.dm_db_index_usage_stats ius ON i.object_id = ius.object_id AND i.index_id = ius.index_id AND ius.database_id = DB_ID()
WHERE i.object_id = OBJECT_ID('[HM].[MG]')
ORDER BY ISNULL(ius.user_seeks, 0) + ISNULL(ius.user_scans, 0) DESC;

-- B2. Statystyki uzycia indeksow na MZ
SELECT
    i.name AS [Indeks],
    ius.user_seeks AS [Seeks],
    ius.user_scans AS [Scans],
    ius.user_lookups AS [Lookups],
    ius.user_updates AS [Updates]
FROM sys.indexes i
LEFT JOIN sys.dm_db_index_usage_stats ius ON i.object_id = ius.object_id AND i.index_id = ius.index_id AND ius.database_id = DB_ID()
WHERE i.object_id = OBJECT_ID('[HM].[MZ]')
ORDER BY ISNULL(ius.user_seeks, 0) + ISNULL(ius.user_scans, 0) DESC;

-- =====================================================================
-- C. BRAKUJACE INDEKSY — co SQL Server sam sugeruje
-- =====================================================================

SELECT TOP 10
    OBJECT_NAME(mid.object_id) AS [Tabela],
    mid.equality_columns AS [Equality],
    mid.inequality_columns AS [Inequality],
    mid.included_columns AS [Include],
    migs.avg_user_impact AS [Poprawa %],
    migs.user_seeks AS [Ile razy potrzebny],
    migs.avg_total_user_cost AS [Sredni koszt]
FROM sys.dm_db_missing_index_details mid
INNER JOIN sys.dm_db_missing_index_groups mig ON mid.index_handle = mig.index_handle
INNER JOIN sys.dm_db_missing_index_group_stats migs ON mig.index_group_handle = migs.group_handle
WHERE mid.database_id = DB_ID()
  AND mid.object_id IN (OBJECT_ID('[HM].[MG]'), OBJECT_ID('[HM].[MZ]'))
ORDER BY migs.avg_user_impact * migs.user_seeks DESC;

-- =====================================================================
-- D. PLAN WYKONANIA
-- Aby zobaczyc plan: zaznacz zapytanie z sekcji A-C i nacisnij Ctrl+L w SSMS
-- (Display Estimated Execution Plan)
-- =====================================================================

-- =====================================================================
-- E. DANE OPAKOWAN — szczegolowa analiza
-- =====================================================================

-- E1. Ile dokumentow per miesiac (ostatni rok)?
SELECT
    FORMAT(MG.data, 'yyyy-MM') AS Miesiac,
    COUNT(*) AS Dokumenty,
    SUM(CASE WHEN MG.typ_dk = 'MW1' THEN 1 ELSE 0 END) AS Wydania,
    SUM(CASE WHEN MG.typ_dk = 'MP' THEN 1 ELSE 0 END) AS Przyjecia,
    SUM(MZ.Ilosc) AS BilansMiesiaca
FROM [HANDEL].[HM].[MG] MG WITH (NOLOCK)
INNER JOIN [HANDEL].[HM].[MZ] MZ WITH (NOLOCK) ON MZ.super = MG.id
INNER JOIN [HANDEL].[HM].[TW] TW WITH (NOLOCK) ON MZ.idtw = TW.id
WHERE MG.anulowany = 0 AND MG.magazyn = 65559 AND MG.typ_dk IN ('MW1','MP')
  AND TW.nazwa IN ('Pojemnik Drobiowy E2','Paleta H1','Paleta EURO','Paleta plastikowa','Paleta Drewniana')
  AND MG.data >= DATEADD(YEAR, -1, GETDATE())
GROUP BY FORMAT(MG.data, 'yyyy-MM')
ORDER BY Miesiac DESC;

-- E2. Kontrahenci z najwieksza ZMIANA salda w ostatnich 3 tygodniach
;WITH Zmiana AS (
    SELECT
        MG.khid AS KontrahentId,
        SUM(CASE WHEN TW.nazwa = 'Pojemnik Drobiowy E2' THEN MZ.Ilosc ELSE 0 END) AS ZmianaE2,
        SUM(CASE WHEN TW.nazwa = 'Paleta H1' THEN MZ.Ilosc ELSE 0 END) AS ZmianaH1,
        SUM(MZ.Ilosc) AS ZmianaRazem,
        COUNT(DISTINCT MG.id) AS IleDokumentow
    FROM [HANDEL].[HM].[MG] MG WITH (NOLOCK)
    INNER JOIN [HANDEL].[HM].[MZ] MZ WITH (NOLOCK) ON MZ.super = MG.id
    INNER JOIN [HANDEL].[HM].[TW] TW WITH (NOLOCK) ON MZ.idtw = TW.id
    WHERE MG.anulowany = 0 AND MG.magazyn = 65559 AND MG.typ_dk IN ('MW1','MP')
      AND TW.nazwa IN ('Pojemnik Drobiowy E2','Paleta H1','Paleta EURO','Paleta plastikowa','Paleta Drewniana')
      AND MG.data > DATEADD(WEEK, -3, GETDATE())
    GROUP BY MG.khid
)
SELECT TOP 15
    C.Shortcut AS Kontrahent,
    Z.ZmianaE2, Z.ZmianaH1, Z.ZmianaRazem, Z.IleDokumentow,
    ISNULL(WYM.CDim_Handlowiec_Val, '-') AS Handlowiec
FROM Zmiana Z
INNER JOIN [HANDEL].[SSCommon].[STContractors] C WITH (NOLOCK) ON C.id = Z.KontrahentId
LEFT JOIN [HANDEL].[SSCommon].[ContractorClassification] WYM WITH (NOLOCK) ON C.Id = WYM.ElementId
ORDER BY ABS(Z.ZmianaRazem) DESC;

-- E3. Kontrahenci ktorzy TYLKO wydaja (nigdy nie zwracaja)
SELECT
    C.Shortcut AS Kontrahent,
    ISNULL(WYM.CDim_Handlowiec_Val, '-') AS Handlowiec,
    SUM(CASE WHEN MG.typ_dk = 'MW1' THEN 1 ELSE 0 END) AS IleWydan,
    SUM(CASE WHEN MG.typ_dk = 'MP' THEN 1 ELSE 0 END) AS IlePrzyjec,
    SUM(MZ.Ilosc) AS SaldoRazem,
    MAX(MG.data) AS OstatniDok
FROM [HANDEL].[HM].[MG] MG WITH (NOLOCK)
INNER JOIN [HANDEL].[HM].[MZ] MZ WITH (NOLOCK) ON MZ.super = MG.id
INNER JOIN [HANDEL].[HM].[TW] TW WITH (NOLOCK) ON MZ.idtw = TW.id
INNER JOIN [HANDEL].[SSCommon].[STContractors] C WITH (NOLOCK) ON C.id = MG.khid
LEFT JOIN [HANDEL].[SSCommon].[ContractorClassification] WYM WITH (NOLOCK) ON C.Id = WYM.ElementId
WHERE MG.anulowany = 0 AND MG.magazyn = 65559 AND MG.typ_dk IN ('MW1','MP')
  AND TW.nazwa IN ('Pojemnik Drobiowy E2','Paleta H1','Paleta EURO','Paleta plastikowa','Paleta Drewniana')
  AND MG.data >= '2024-01-01'
GROUP BY C.Shortcut, WYM.CDim_Handlowiec_Val
HAVING SUM(CASE WHEN MG.typ_dk = 'MP' THEN 1 ELSE 0 END) = 0
   AND SUM(MZ.Ilosc) > 50
ORDER BY SaldoRazem DESC;

-- E4. Sredni czas zwrotu per handlowiec (dni miedzy wydaniem a przyjaciem)
-- Oblicza srednia roznice dat miedzy ostatnim MW1 a ostatnim MP per kontrahent
;WITH OstatnieDaty AS (
    SELECT
        MG.khid AS KontrahentId,
        MAX(CASE WHEN MG.typ_dk = 'MW1' THEN MG.data END) AS OstatnieWydanie,
        MAX(CASE WHEN MG.typ_dk = 'MP' THEN MG.data END) AS OstatniePrzyjecie
    FROM [HANDEL].[HM].[MG] MG WITH (NOLOCK)
    INNER JOIN [HANDEL].[HM].[MZ] MZ WITH (NOLOCK) ON MZ.super = MG.id
    INNER JOIN [HANDEL].[HM].[TW] TW WITH (NOLOCK) ON MZ.idtw = TW.id
    WHERE MG.anulowany = 0 AND MG.magazyn = 65559 AND MG.typ_dk IN ('MW1','MP')
      AND TW.nazwa = 'Pojemnik Drobiowy E2'
      AND MG.data >= DATEADD(YEAR, -1, GETDATE())
    GROUP BY MG.khid
)
SELECT
    ISNULL(WYM.CDim_Handlowiec_Val, '(brak)') AS Handlowiec,
    COUNT(*) AS IluKontrahentow,
    AVG(DATEDIFF(DAY, OD.OstatnieWydanie, OD.OstatniePrzyjecie)) AS SredniCzasZwrotuDni,
    MIN(DATEDIFF(DAY, OD.OstatnieWydanie, OD.OstatniePrzyjecie)) AS MinDni,
    MAX(DATEDIFF(DAY, OD.OstatnieWydanie, OD.OstatniePrzyjecie)) AS MaxDni
FROM OstatnieDaty OD
INNER JOIN [HANDEL].[SSCommon].[STContractors] C WITH (NOLOCK) ON C.id = OD.KontrahentId
LEFT JOIN [HANDEL].[SSCommon].[ContractorClassification] WYM WITH (NOLOCK) ON C.Id = WYM.ElementId
WHERE OD.OstatnieWydanie IS NOT NULL AND OD.OstatniePrzyjecie IS NOT NULL
  AND OD.OstatniePrzyjecie >= OD.OstatnieWydanie
GROUP BY WYM.CDim_Handlowiec_Val
ORDER BY SredniCzasZwrotuDni DESC;

-- E5. Kontrahenci z rozbieznoscia — duzo wydan, malo przyjec (podejrzane)
;WITH BilansDok AS (
    SELECT
        MG.khid,
        SUM(CASE WHEN MG.typ_dk = 'MW1' THEN ABS(MZ.Ilosc) ELSE 0 END) AS SumaWydan,
        SUM(CASE WHEN MG.typ_dk = 'MP' THEN ABS(MZ.Ilosc) ELSE 0 END) AS SumaPrzyjec
    FROM [HANDEL].[HM].[MG] MG WITH (NOLOCK)
    INNER JOIN [HANDEL].[HM].[MZ] MZ WITH (NOLOCK) ON MZ.super = MG.id
    INNER JOIN [HANDEL].[HM].[TW] TW WITH (NOLOCK) ON MZ.idtw = TW.id
    WHERE MG.anulowany = 0 AND MG.magazyn = 65559 AND MG.typ_dk IN ('MW1','MP')
      AND TW.nazwa = 'Pojemnik Drobiowy E2'
      AND MG.data >= '2024-01-01'
    GROUP BY MG.khid
    HAVING SUM(CASE WHEN MG.typ_dk = 'MW1' THEN ABS(MZ.Ilosc) ELSE 0 END) > 1000
)
SELECT TOP 15
    C.Shortcut,
    BD.SumaWydan, BD.SumaPrzyjec,
    BD.SumaWydan - BD.SumaPrzyjec AS Roznica,
    CAST(BD.SumaPrzyjec * 100.0 / NULLIF(BD.SumaWydan, 0) AS DECIMAL(5,1)) AS [Zwrot %],
    ISNULL(WYM.CDim_Handlowiec_Val, '-') AS Handlowiec
FROM BilansDok BD
INNER JOIN [HANDEL].[SSCommon].[STContractors] C WITH (NOLOCK) ON C.id = BD.khid
LEFT JOIN [HANDEL].[SSCommon].[ContractorClassification] WYM WITH (NOLOCK) ON C.Id = WYM.ElementId
ORDER BY (BD.SumaWydan - BD.SumaPrzyjec) DESC;

-- E6. MP1 (korekty przyjec) — co pomijamy?
SELECT
    C.Shortcut AS Kontrahent,
    MG.kod AS NrDokumentu,
    MG.data AS Data,
    TW.nazwa AS Towar,
    MZ.Ilosc
FROM [HANDEL].[HM].[MG] MG WITH (NOLOCK)
INNER JOIN [HANDEL].[HM].[MZ] MZ WITH (NOLOCK) ON MZ.super = MG.id
INNER JOIN [HANDEL].[HM].[TW] TW WITH (NOLOCK) ON MZ.idtw = TW.id
INNER JOIN [HANDEL].[SSCommon].[STContractors] C WITH (NOLOCK) ON C.id = MG.khid
WHERE MG.magazyn = 65559 AND MG.anulowany = 0 AND MG.typ_dk = 'MP1'
  AND TW.nazwa IN ('Pojemnik Drobiowy E2','Paleta H1','Paleta EURO','Paleta plastikowa','Paleta Drewniana')
ORDER BY MG.data DESC;

-- E7. Tygodniowy trend E2 (ostatnie 12 tygodni)
SELECT
    DATEADD(WEEK, DATEDIFF(WEEK, 0, MG.data), 0) AS TydzienOd,
    SUM(CASE WHEN MG.typ_dk = 'MW1' THEN MZ.Ilosc ELSE 0 END) AS Wydane,
    SUM(CASE WHEN MG.typ_dk = 'MP' THEN ABS(MZ.Ilosc) ELSE 0 END) AS Przyjete,
    SUM(MZ.Ilosc) AS Bilans,
    COUNT(DISTINCT MG.khid) AS IluKontrahentow
FROM [HANDEL].[HM].[MG] MG WITH (NOLOCK)
INNER JOIN [HANDEL].[HM].[MZ] MZ WITH (NOLOCK) ON MZ.super = MG.id
INNER JOIN [HANDEL].[HM].[TW] TW WITH (NOLOCK) ON MZ.idtw = TW.id
WHERE MG.anulowany = 0 AND MG.magazyn = 65559 AND MG.typ_dk IN ('MW1','MP')
  AND TW.nazwa = 'Pojemnik Drobiowy E2'
  AND MG.data >= DATEADD(WEEK, -12, GETDATE())
GROUP BY DATEADD(WEEK, DATEDIFF(WEEK, 0, MG.data), 0)
ORDER BY TydzienOd;

-- E8. Kontrahenci z saldem ujemnym (my im jestesmy winni)
SELECT
    C.Shortcut AS Kontrahent,
    SUM(CASE WHEN TW.nazwa = 'Pojemnik Drobiowy E2' THEN MZ.Ilosc ELSE 0 END) AS E2,
    SUM(CASE WHEN TW.nazwa = 'Paleta H1' THEN MZ.Ilosc ELSE 0 END) AS H1,
    SUM(MZ.Ilosc) AS Razem,
    ISNULL(WYM.CDim_Handlowiec_Val, '-') AS Handlowiec
FROM [HANDEL].[HM].[MG] MG WITH (NOLOCK)
INNER JOIN [HANDEL].[HM].[MZ] MZ WITH (NOLOCK) ON MZ.super = MG.id
INNER JOIN [HANDEL].[HM].[TW] TW WITH (NOLOCK) ON MZ.idtw = TW.id
INNER JOIN [HANDEL].[SSCommon].[STContractors] C WITH (NOLOCK) ON C.id = MG.khid
LEFT JOIN [HANDEL].[SSCommon].[ContractorClassification] WYM WITH (NOLOCK) ON C.Id = WYM.ElementId
WHERE MG.anulowany = 0 AND MG.magazyn = 65559 AND MG.typ_dk IN ('MW1','MP')
  AND TW.nazwa IN ('Pojemnik Drobiowy E2','Paleta H1','Paleta EURO','Paleta plastikowa','Paleta Drewniana')
GROUP BY C.Shortcut, WYM.CDim_Handlowiec_Val
HAVING SUM(MZ.Ilosc) < -50
ORDER BY Razem ASC;

-- E9. Dzienna aktywnosc (ostatnie 30 dni) — kiedy jest najwiecej dokumentow?
SELECT
    DATENAME(WEEKDAY, MG.data) AS DzienTygodnia,
    DATEPART(WEEKDAY, MG.data) AS NrDnia,
    COUNT(*) AS IleDokumentow,
    SUM(ABS(MZ.Ilosc)) AS ObrotAbsolutny
FROM [HANDEL].[HM].[MG] MG WITH (NOLOCK)
INNER JOIN [HANDEL].[HM].[MZ] MZ WITH (NOLOCK) ON MZ.super = MG.id
INNER JOIN [HANDEL].[HM].[TW] TW WITH (NOLOCK) ON MZ.idtw = TW.id
WHERE MG.anulowany = 0 AND MG.magazyn = 65559 AND MG.typ_dk IN ('MW1','MP')
  AND TW.nazwa IN ('Pojemnik Drobiowy E2','Paleta H1','Paleta EURO','Paleta plastikowa','Paleta Drewniana')
  AND MG.data >= DATEADD(DAY, -30, GETDATE())
GROUP BY DATENAME(WEEKDAY, MG.data), DATEPART(WEEKDAY, MG.data)
ORDER BY NrDnia;

-- E10. Seria dokumentow — jakie serie uzywamy?
SELECT TOP 10
    MG.seria,
    MG.typ_dk,
    COUNT(*) AS Ile,
    MIN(MG.data) AS OdDaty,
    MAX(MG.data) AS DoDaty
FROM [HANDEL].[HM].[MG] MG WITH (NOLOCK)
WHERE MG.magazyn = 65559 AND MG.anulowany = 0 AND MG.typ_dk IN ('MW1','MP')
GROUP BY MG.seria, MG.typ_dk
ORDER BY Ile DESC;

-- E11. Collation check — czy nazwy towarow sa case-sensitive?
SELECT
    DATABASEPROPERTYEX(DB_NAME(), 'Collation') AS [Database Collation],
    COLUMNPROPERTY(OBJECT_ID('[HM].[TW]'), 'nazwa', 'Collation') AS [TW.nazwa Collation];

-- E12. Fragmentacja indeksow na MG
SELECT
    i.name AS [Indeks],
    ps.avg_fragmentation_in_percent AS [Fragmentacja %],
    ps.page_count AS [Stron],
    ps.avg_page_space_used_in_percent AS [Wypelnienie stron %]
FROM sys.dm_db_index_physical_stats(DB_ID(), OBJECT_ID('[HM].[MG]'), NULL, NULL, 'LIMITED') ps
INNER JOIN sys.indexes i ON ps.object_id = i.object_id AND ps.index_id = i.index_id
WHERE ps.page_count > 100
ORDER BY ps.avg_fragmentation_in_percent DESC;

-- E13. Kto DZISIAJ mial dokumenty opakowan?
SELECT
    C.Shortcut AS Kontrahent,
    MG.typ_dk AS Typ,
    MG.kod AS NrDokumentu,
    TW.nazwa AS Towar,
    MZ.Ilosc
FROM [HANDEL].[HM].[MG] MG WITH (NOLOCK)
INNER JOIN [HANDEL].[HM].[MZ] MZ WITH (NOLOCK) ON MZ.super = MG.id
INNER JOIN [HANDEL].[HM].[TW] TW WITH (NOLOCK) ON MZ.idtw = TW.id
INNER JOIN [HANDEL].[SSCommon].[STContractors] C WITH (NOLOCK) ON C.id = MG.khid
WHERE MG.anulowany = 0 AND MG.magazyn = 65559 AND MG.typ_dk IN ('MW1','MP')
  AND TW.nazwa IN ('Pojemnik Drobiowy E2','Paleta H1','Paleta EURO','Paleta plastikowa','Paleta Drewniana')
  AND CAST(MG.data AS DATE) = CAST(GETDATE() AS DATE)
ORDER BY C.Shortcut, MG.typ_dk;

-- E14. Czy sa dokumenty z przyszla data? (bledne daty)
SELECT COUNT(*) AS DokumentyZPrzyszlosci
FROM [HANDEL].[HM].[MG] MG WITH (NOLOCK)
WHERE MG.magazyn = 65559 AND MG.anulowany = 0 AND MG.typ_dk IN ('MW1','MP')
  AND MG.data > GETDATE();

-- E15. Ranking handlowcow — kto ma najwiecej zaleglosci
;WITH SaldaH AS (
    SELECT
        MG.khid,
        SUM(CASE WHEN TW.nazwa = 'Pojemnik Drobiowy E2' THEN MZ.Ilosc ELSE 0 END) AS E2,
        SUM(CASE WHEN TW.nazwa = 'Paleta H1' THEN MZ.Ilosc ELSE 0 END) AS H1,
        SUM(MZ.Ilosc) AS Razem,
        MAX(MG.data) AS OstatniDok
    FROM [HANDEL].[HM].[MG] MG WITH (NOLOCK)
    INNER JOIN [HANDEL].[HM].[MZ] MZ WITH (NOLOCK) ON MZ.super = MG.id
    INNER JOIN [HANDEL].[HM].[TW] TW WITH (NOLOCK) ON MZ.idtw = TW.id
    WHERE MG.anulowany = 0 AND MG.magazyn = 65559 AND MG.typ_dk IN ('MW1','MP')
      AND TW.nazwa IN ('Pojemnik Drobiowy E2','Paleta H1','Paleta EURO','Paleta plastikowa','Paleta Drewniana')
    GROUP BY MG.khid
    HAVING SUM(MZ.Ilosc) > 0
)
SELECT
    ISNULL(WYM.CDim_Handlowiec_Val, '(brak)') AS Handlowiec,
    COUNT(*) AS IluDluznikow,
    SUM(SH.E2) AS LaczneSaldoE2,
    SUM(SH.H1) AS LaczneSaldoH1,
    SUM(SH.Razem) AS LaczneRazem,
    SUM(CASE WHEN DATEDIFF(DAY, SH.OstatniDok, GETDATE()) > 30 THEN 1 ELSE 0 END) AS Zaleglosci30d,
    SUM(CASE WHEN DATEDIFF(DAY, SH.OstatniDok, GETDATE()) > 90 THEN 1 ELSE 0 END) AS Zaleglosci90d
FROM SaldaH SH
INNER JOIN [HANDEL].[SSCommon].[STContractors] C WITH (NOLOCK) ON C.id = SH.khid
LEFT JOIN [HANDEL].[SSCommon].[ContractorClassification] WYM WITH (NOLOCK) ON C.Id = WYM.ElementId
GROUP BY WYM.CDim_Handlowiec_Val
ORDER BY LaczneRazem DESC;
