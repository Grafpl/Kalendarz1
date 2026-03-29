-- Tabela mapowania pojazdów Webfleet → Pojazd (system transportowy)
-- Uruchom raz w SSMS na bazie TransportPL (192.168.0.109)
-- LUB tabela tworzy się automatycznie przy pierwszym otwarciu dialogu mapowania

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'WebfleetVehicleMapping')
BEGIN
    CREATE TABLE WebfleetVehicleMapping (
        WebfleetObjectNo    varchar(20)     NOT NULL,
        WebfleetObjectName  nvarchar(100)   NULL,
        PojazdID            int             NULL,       -- FK do Pojazd.PojazdID (NULL = niezmapowany)
        CreatedAtUTC        datetime2       NOT NULL DEFAULT SYSUTCDATETIME(),
        ModifiedAtUTC       datetime2       NULL,
        ModifiedBy          nvarchar(64)    NULL,
        CONSTRAINT PK_WebfleetVehicleMapping PRIMARY KEY (WebfleetObjectNo)
    );

    CREATE INDEX IX_WVM_Pojazd ON WebfleetVehicleMapping (PojazdID) WHERE PojazdID IS NOT NULL;

    PRINT 'Tabela WebfleetVehicleMapping utworzona.';
END
ELSE
    PRINT 'Tabela WebfleetVehicleMapping już istnieje.';
GO
