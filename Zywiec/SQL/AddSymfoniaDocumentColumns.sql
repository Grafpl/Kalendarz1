-- ════════════════════════════════════════════════════════════════════
-- LibraNet (192.168.0.109) — migracja dla skryptu Symfonia ExportPZLibraNet v3.
-- Wersja FV-only (PZ i RWU usuniete ze skryptu i bazy).
-- Sergiusz Piorkowski, 2026-05-22.
-- ════════════════════════════════════════════════════════════════════

USE LibraNet;
GO

-- ============ DODAJ ============

-- 1) Dostawcy.IsVatowiec — auto-wybor typu faktury (NULL = pytaj, 0 = rolnik FVR, 1 = vatowiec FVZ)
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE name = 'IsVatowiec' AND object_id = OBJECT_ID('dbo.Dostawcy'))
BEGIN
    ALTER TABLE dbo.Dostawcy ADD IsVatowiec BIT NULL;
    PRINT '+ Dodano Dostawcy.IsVatowiec';
END
ELSE
    PRINT '. Dostawcy.IsVatowiec juz istnieje';
GO

-- ============ POSPRZATAJ PO POPRZEDNIEJ ITERACJI (PZ + RWU) ============
-- Te kolumny/tabela byly dodane w wersji v2 ktora obslugiwala PZ + RWU.
-- Wersja v3 (FV-only) ich nie potrzebuje. Drop jest bezpieczny - byly puste.

IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SymfoniaExportLog' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    -- Sprawdzimy czy nie ma juz wpisow przed drop
    DECLARE @cnt INT;
    SELECT @cnt = COUNT(*) FROM dbo.SymfoniaExportLog;
    IF @cnt = 0
    BEGIN
        DROP TABLE dbo.SymfoniaExportLog;
        PRINT '- DROP dbo.SymfoniaExportLog (byla pusta)';
    END
    ELSE
        PRINT '! dbo.SymfoniaExportLog ma ' + CAST(@cnt AS VARCHAR(10)) + ' wpisow - NIE drop. Zweryfikuj recznie.';
END
ELSE
    PRINT '. dbo.SymfoniaExportLog juz nie istnieje';
GO

-- 2) FarmerCalc.SymfoniaDocNr — dropujemy bo skrypt v3 nie zapisuje PZ
IF EXISTS (SELECT 1 FROM sys.columns WHERE name = 'SymfoniaDocNr' AND object_id = OBJECT_ID('dbo.FarmerCalc'))
BEGIN
    DECLARE @nonEmpty INT;
    SELECT @nonEmpty = COUNT(*) FROM dbo.FarmerCalc WHERE SymfoniaDocNr IS NOT NULL AND LTRIM(RTRIM(SymfoniaDocNr)) <> '';
    IF @nonEmpty = 0
    BEGIN
        ALTER TABLE dbo.FarmerCalc DROP COLUMN SymfoniaDocNr;
        PRINT '- DROP FarmerCalc.SymfoniaDocNr (byla pusta)';
    END
    ELSE
        PRINT '! FarmerCalc.SymfoniaDocNr ma ' + CAST(@nonEmpty AS VARCHAR(10)) + ' niepustych wpisow - NIE drop';
END
ELSE
    PRINT '. FarmerCalc.SymfoniaDocNr juz nie istnieje';
GO

PRINT '';
PRINT '=== Migracja v3 (FV-only) zakonczona ===';
PRINT 'Skrypt AmBasic ExportPZLibraNet_v2.sc tworzy TYLKO FVR/FVZ.';
PRINT 'Zapisuje do FarmerCalc.SymfoniaIdFV + SymfoniaNrFV + SymfoniaExportDate + Symfonia=1';
