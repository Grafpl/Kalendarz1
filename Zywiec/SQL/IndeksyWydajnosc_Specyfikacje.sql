-- ════════════════════════════════════════════════════════════════════
-- LibraNet (192.168.0.109) — indeksy przyspieszające widok Specyfikacje.
-- BEZPIECZNE: indeksy nieklastrowane (tylko przyspieszają SELECT, nie zmieniają danych).
-- Idempotentne (IF NOT EXISTS). Sergiusz Piorkowski, 2026-05-25.
--
-- Cel: glowne zapytanie LoadData filtruje "WHERE fc.CalcDate = @date" + JOINy.
--      Bez indeksu = skan calej FarmerCalc. Z indeksem = seek (szybciej).
-- ════════════════════════════════════════════════════════════════════

USE LibraNet;
GO

-- 1) Glowny indeks: filtr po dacie uboju (CalcDate) — najwazniejszy.
--    INCLUDE najczesciej czytanych kolumn = "covering index" (czesto bez zagladania do tabeli).
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_FarmerCalc_CalcDate' AND object_id = OBJECT_ID('dbo.FarmerCalc'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_FarmerCalc_CalcDate
        ON dbo.FarmerCalc (CalcDate)
        INCLUDE (CarLp, CustomerGID, CustomerRealGID, DriverGID, PriceTypeID, PartiaGuid, IdPosrednik,
                 NettoWeight, NettoFarmWeight, PayWgt, Price, Addition, Loss, IncDeadConf,
                 DeclI1, DeclI2, DeclI3, DeclI4, DeclI5, LumQnt, Opasienie, KlasaB,
                 Symfonia, SymfoniaNrFV, NrDokArimr);
    PRINT '+ Utworzono IX_FarmerCalc_CalcDate (covering index po dacie)';
END
ELSE
    PRINT '. IX_FarmerCalc_CalcDate juz istnieje';
GO

-- 2) Indeks pod JOIN PartiaDostawca po guid (jezeli kolumna istnieje i nie ma PK/indeksu na guid)
IF EXISTS (SELECT 1 FROM sys.columns WHERE name = 'guid' AND object_id = OBJECT_ID('dbo.PartiaDostawca'))
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PartiaDostawca_guid' AND object_id = OBJECT_ID('dbo.PartiaDostawca'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_PartiaDostawca_guid
        ON dbo.PartiaDostawca (guid)
        INCLUDE (CustomerID, Partia);
    PRINT '+ Utworzono IX_PartiaDostawca_guid (JOIN po guid)';
END
ELSE
    PRINT '. IX_PartiaDostawca_guid pominiety (brak kolumny lub juz istnieje)';
GO

-- 3) Indeks pod LoadTransportData / inne zapytania filtrujace po CalcDate (jezeli osobna tabela)
--    (Jezeli LoadTransportData czyta z FarmerCalc — indeks #1 juz pomaga.)

PRINT '';
PRINT '═══ Indeksy gotowe ═══';
PRINT 'Sprawdz plan zapytania: glowne SELECT powinno teraz robic Index Seek na IX_FarmerCalc_CalcDate.';
PRINT 'Jezeli FarmerCalc ma duzo kolumn — covering index moze byc duzy; to normalne.';
GO
