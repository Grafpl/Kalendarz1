-- ============================================================
-- PARTIE MODULE V2 - Rozszerzenie o statusy i normy QC
-- Baza: LibraNet (192.168.0.109)
-- ============================================================

-- 1. Tabela historii statusow partii
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'PartiaStatus')
BEGIN
    CREATE TABLE PartiaStatus (
        ID              int           NOT NULL IDENTITY PRIMARY KEY,
        Partia          varchar(15)   NOT NULL,
        Status          varchar(30)   NOT NULL,   -- PLANNED, IN_TRANSIT, AT_RAMP, VET_CHECK, APPROVED, IN_PRODUCTION, PROD_DONE, CLOSED, CLOSED_INCOMPLETE, REJECTED
        StatusPoprzedni varchar(30)   NULL,
        OperatorID      varchar(15)   NULL,
        OperatorNazwa   nvarchar(50)  NULL,
        Komentarz       nvarchar(500) NULL,
        CreatedAtUTC    datetime2     NOT NULL DEFAULT SYSUTCDATETIME()
    );
    CREATE INDEX IX_PartiaStatus_Partia ON PartiaStatus(Partia);
    PRINT 'Created table PartiaStatus';
END
GO

-- 2. Tabela konfigurowalnych norm QC
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'QC_Normy')
BEGIN
    CREATE TABLE QC_Normy (
        ID              int           NOT NULL IDENTITY PRIMARY KEY,
        Nazwa           nvarchar(50)  NOT NULL,       -- np. 'TempRampa', 'TempChillera', 'KlasaB', 'Przekarmienie'
        Opis            nvarchar(200) NULL,
        MinWartosc      decimal(10,2) NULL,
        MaxWartosc      decimal(10,2) NULL,
        JednostkaMiary  nvarchar(20)  NULL,           -- 'C', '%', 'kg'
        Kategoria       varchar(30)   NOT NULL DEFAULT 'TEMPERATURA',  -- TEMPERATURA, WADY, PODSUMOWANIE
        IsAktywna       bit           NOT NULL DEFAULT 1,
        Kolejnosc       int           NOT NULL DEFAULT 0
    );
    PRINT 'Created table QC_Normy';
END
GO

-- 3. Kolumna HarmonogramLp w listapartii (linkage do harmonogramu dostaw)
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('listapartii') AND name = 'HarmonogramLp')
BEGIN
    ALTER TABLE listapartii ADD HarmonogramLp int NULL;
    PRINT 'Added column HarmonogramLp to listapartii';
END
GO

-- 4. Kolumna StatusV2 w listapartii
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('listapartii') AND name = 'StatusV2')
BEGIN
    ALTER TABLE listapartii ADD StatusV2 varchar(30) NULL DEFAULT 'IN_PRODUCTION';
    PRINT 'Added column StatusV2 to listapartii';
END
GO

-- 5. Domyslne normy QC
IF NOT EXISTS (SELECT TOP 1 1 FROM QC_Normy)
BEGIN
    INSERT INTO QC_Normy (Nazwa, Opis, MinWartosc, MaxWartosc, JednostkaMiary, Kategoria, Kolejnosc) VALUES
    ('TempRampa',       'Temperatura na rampie',        NULL,  4.00, 'C',  'TEMPERATURA', 1),
    ('TempChillera',    'Temperatura chillera',         -2.00, 2.00, 'C',  'TEMPERATURA', 2),
    ('TempTunel',       'Temperatura tunelu',           NULL,  -18.00,'C', 'TEMPERATURA', 3),
    ('KlasaB',          'Procent klasy B',              NULL,  20.00, '%', 'PODSUMOWANIE', 10),
    ('Przekarmienie',   'Przekarmienie w kg',           NULL,  50.00, 'kg','PODSUMOWANIE', 11),
    ('Skrzydla',        'Ocena wad skrzydel (1-5)',     1,     5,    'pkt','WADY', 20),
    ('Nogi',            'Ocena wad nog (1-5)',          1,     5,    'pkt','WADY', 21),
    ('Oparzenia',       'Ocena oparzen (1-5)',          1,     5,    'pkt','WADY', 22);
    PRINT 'Inserted default QC norms';
END
GO

-- 6. Indeks na FarmerCalc.LpDostawy (linkage do HarmonogramDostaw)
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_FarmerCalc_LpDostawy' AND object_id = OBJECT_ID('FarmerCalc'))
BEGIN
    CREATE INDEX IX_FarmerCalc_LpDostawy ON FarmerCalc(LpDostawy) WHERE LpDostawy IS NOT NULL;
    PRINT 'Created index IX_FarmerCalc_LpDostawy';
END
GO

-- 7. Indeks na FarmerCalc.PartiaNumber
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_FarmerCalc_PartiaNumber' AND object_id = OBJECT_ID('FarmerCalc'))
BEGIN
    CREATE INDEX IX_FarmerCalc_PartiaNumber ON FarmerCalc(PartiaNumber) WHERE PartiaNumber IS NOT NULL;
    PRINT 'Created index IX_FarmerCalc_PartiaNumber';
END
GO

PRINT 'Partie V2 SQL setup complete.';
GO
