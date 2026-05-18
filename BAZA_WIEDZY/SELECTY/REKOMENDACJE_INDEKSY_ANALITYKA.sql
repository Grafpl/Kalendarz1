/* ═══════════════════════════════════════════════════════════════════════════════
   Rekomendacje indeksów dla modułu "Analityka Pełna"
   Cel: przyspieszyć kluczowe query w WydajnoscService / RealizacjaService.

   ⚠️ Uwaga: skrypt jest niedestrukcyjny — wszystkie CREATE INDEX są w `IF NOT EXISTS`.
   ⚠️ Indeksy zwiększają zajętość dysku (każdy ~10-30 MB dla In0E przy ~1-2M rekordów).
   ⚠️ Indeksy spowalniają INSERT (waga LIVE). Sprawdzić na produkcji w niskim ruchu.

   Wykonać na: LibraNet (192.168.0.109)
   Wersja:     SQL Server 2008 R2 (kompatybilne)
   Autor:      Sergiusz Piórkowski / ZPSP
   Data:       2026-05-08
   ═══════════════════════════════════════════════════════════════════════════════ */

USE [LibraNet];
GO

-- ────────────────────────────────────────────────────────────────────
-- 1) Indeks dla query per klasa wagowa (WydajnoscService.LoadWydajnoscPerKlasaAsync)
--    Pokrycie: WHERE Data BETWEEN ... AND QntInCont BETWEEN 4 AND 12 AND ActWeight > 0
-- ────────────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_In0E_Data_QntInCont' AND object_id = OBJECT_ID('dbo.In0E'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_In0E_Data_QntInCont
    ON dbo.In0E (Data, QntInCont)
    INCLUDE (ActWeight, Weight, P1)
    WITH (FILLFACTOR = 90, ONLINE = OFF);
    PRINT '✓ IX_In0E_Data_QntInCont utworzony';
END
ELSE
    PRINT '○ IX_In0E_Data_QntInCont już istnieje — pominięto';
GO

-- ────────────────────────────────────────────────────────────────────
-- 2) Indeks dla query Realizacja LIVE (RealizacjaService.LoadWazeniaAsync)
--    Pokrycie: WHERE Data BETWEEN ... AND ArticleID = ...
-- ────────────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_In0E_Data_ArticleID' AND object_id = OBJECT_ID('dbo.In0E'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_In0E_Data_ArticleID
    ON dbo.In0E (Data, ArticleID)
    INCLUDE (ActWeight, Weight, OperatorID, Wagowy, P1, QntInCont, Tara)
    WITH (FILLFACTOR = 90, ONLINE = OFF);
    PRINT '✓ IX_In0E_Data_ArticleID utworzony';
END
ELSE
    PRINT '○ IX_In0E_Data_ArticleID już istnieje — pominięto';
GO

-- ────────────────────────────────────────────────────────────────────
-- 3) Indeks dla JOIN In0E ↔ PartiaDostawca po partii
--    Pokrycie: LEFT JOIN dbo.PartiaDostawca pd ON e.P1 = pd.Partia
-- ────────────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_In0E_P1' AND object_id = OBJECT_ID('dbo.In0E'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_In0E_P1
    ON dbo.In0E (P1)
    INCLUDE (Data)
    WITH (FILLFACTOR = 90, ONLINE = OFF);
    PRINT '✓ IX_In0E_P1 utworzony';
END
ELSE
    PRINT '○ IX_In0E_P1 już istnieje — pominięto';
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PartiaDostawca_Partia' AND object_id = OBJECT_ID('dbo.PartiaDostawca'))
BEGIN
    -- Może już istnieć jako PRIMARY KEY na Partia — sprawdź przed CREATE
    DECLARE @ma_pk INT;
    SELECT @ma_pk = COUNT(*) FROM sys.indexes
    WHERE object_id = OBJECT_ID('dbo.PartiaDostawca') AND is_primary_key = 1;

    IF @ma_pk = 0
    BEGIN
        CREATE NONCLUSTERED INDEX IX_PartiaDostawca_Partia
        ON dbo.PartiaDostawca (Partia)
        INCLUDE (CustomerID, CustomerName)
        WITH (FILLFACTOR = 95, ONLINE = OFF);
        PRINT '✓ IX_PartiaDostawca_Partia utworzony';
    END
    ELSE
        PRINT '○ PartiaDostawca ma już PK — indeks niepotrzebny';
END
GO

-- ────────────────────────────────────────────────────────────────────
-- 4) (OPCJONALNIE) Tabela preferencji użytkownika dla Analityki Pełnej
--    Zapisuje ostatnie filtry, układ UI itp. per user.
--    Domyślnie tabela JEST tworzona — można skomentować jeśli niepotrzebna.
-- ────────────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'AnalitykaPreferencjeUzytkownika')
BEGIN
    CREATE TABLE dbo.AnalitykaPreferencjeUzytkownika
    (
        UserID            varchar(50)   NOT NULL,           -- App.UserID
        Klucz             varchar(100)  NOT NULL,           -- np. 'AnalitykaPelna.OstatnieFiltry'
        WartoscJson       nvarchar(max) NOT NULL,           -- serializacja JSON
        ZmodyfikowanoUTC  datetime2(0)  NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_AnalitykaPreferencjeUzytkownika PRIMARY KEY (UserID, Klucz)
    );
    PRINT '✓ Tabela AnalitykaPreferencjeUzytkownika utworzona';
END
ELSE
    PRINT '○ Tabela AnalitykaPreferencjeUzytkownika już istnieje — pominięto';
GO

-- ────────────────────────────────────────────────────────────────────
-- 5) Diagnostyka: rozmiary indeksów na In0E (po wykonaniu skryptu)
-- ────────────────────────────────────────────────────────────────────
SELECT
    i.name                                          AS NazwaIndeksu,
    i.type_desc                                     AS Typ,
    SUM(p.rows)                                     AS LiczbaWierszy,
    SUM(a.total_pages) * 8 / 1024                   AS RozmiarMB,
    i.fill_factor                                   AS FillFactor
FROM sys.indexes i
JOIN sys.partitions p ON p.object_id = i.object_id AND p.index_id = i.index_id
JOIN sys.allocation_units a ON a.container_id = p.partition_id
WHERE i.object_id = OBJECT_ID('dbo.In0E')
GROUP BY i.name, i.type_desc, i.fill_factor
ORDER BY SUM(a.total_pages) DESC;
GO

PRINT '';
PRINT '═══════════════════════════════════════════════════════════════════════════════';
PRINT 'Skrypt zakończony. Sprawdź powyższy zestaw indeksów na In0E.';
PRINT 'Aby usunąć indeks: DROP INDEX IX_NazwaIndeksu ON dbo.NazwaTabeli;';
PRINT '═══════════════════════════════════════════════════════════════════════════════';
