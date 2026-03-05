-- ============================================================================
-- FLOTA MODULE - Tabele rozszerzajace Driver i CarTrailer
-- Baza: LibraNet na 192.168.0.109
-- Uruchom raz w SSMS na bazie LibraNet
-- ============================================================================

-- 1. DriverDetails (rozszerzenie danych kierowcy 1:1 z Driver)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'DriverDetails')
BEGIN
    CREATE TABLE DriverDetails (
        DriverGID           int            NOT NULL PRIMARY KEY,
        FirstName           nvarchar(50)   NULL,
        LastName            nvarchar(80)   NULL,
        Phone1              nvarchar(20)   NULL,
        Phone2              nvarchar(20)   NULL,
        Email               nvarchar(100)  NULL,
        PESEL               nvarchar(11)   NULL,
        NrPrawaJazdy        nvarchar(30)   NULL,
        KategoriePrawaJazdy nvarchar(20)   NULL,
        DataWaznosciPJ      date           NULL,
        NrBadanLekarskich   nvarchar(30)   NULL,
        DataWazBadanLek     date           NULL,
        NrSzkoleniaBHP      nvarchar(30)   NULL,
        DataWazBHP          date           NULL,
        DataZatrudnienia    date           NULL,
        DataZwolnienia      date           NULL,
        TypZatrudnienia     nvarchar(30)   NULL,
        Uwagi               nvarchar(500)  NULL,
        ZdjecieKierowcy     varbinary(MAX) NULL,
        CreatedAtUTC        datetime2      NOT NULL DEFAULT SYSUTCDATETIME(),
        ModifiedAtUTC       datetime2      NULL,
        ModifiedBy          nvarchar(64)   NULL,
        CONSTRAINT FK_DriverDetails_Driver FOREIGN KEY (DriverGID) REFERENCES Driver(GID)
    );
    PRINT 'Created table DriverDetails';
END
GO

-- 2. VehicleDetails (rozszerzenie danych pojazdu 1:1 z CarTrailer)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'VehicleDetails')
BEGIN
    CREATE TABLE VehicleDetails (
        CarTrailerID        varchar(10)    NOT NULL PRIMARY KEY,
        Registration        nvarchar(20)   NULL,
        VIN                 nvarchar(17)   NULL,
        RokProdukcji        int            NULL,
        DataPrzegladu       date           NULL,
        DataUbezpieczenia   date           NULL,
        NrPolisyOC          nvarchar(30)   NULL,
        NrPolisyAC          nvarchar(30)   NULL,
        Ubezpieczyciel      nvarchar(100)  NULL,
        PrzebiegKm          int            NULL,
        DataOstatniegoTank  date           NULL,
        SrednieSpalanie     decimal(5,2)   NULL,
        PojemnoscBaku       int            NULL,
        MaxLadownoscKg      int            NULL,
        MaxPaletH1          int            NULL,
        MaxPojemnikE2       int            NULL,
        TypNadwozia         nvarchar(30)   NULL,
        TemperaturaMin      decimal(5,1)   NULL,
        TemperaturaMax      decimal(5,1)   NULL,
        GPSModul            nvarchar(50)   NULL,
        Uwagi               nvarchar(500)  NULL,
        ZdjeciePojazdu      varbinary(MAX) NULL,
        CreatedAtUTC        datetime2      NOT NULL DEFAULT SYSUTCDATETIME(),
        ModifiedAtUTC       datetime2      NULL,
        ModifiedBy          nvarchar(64)   NULL,
        CONSTRAINT FK_VehicleDetails_CarTrailer FOREIGN KEY (CarTrailerID) REFERENCES CarTrailer(ID)
    );
    PRINT 'Created table VehicleDetails';
END
GO

-- 3. DriverVehicleAssignment (przypisanie kierowca<->pojazd z historia)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'DriverVehicleAssignment')
BEGIN
    CREATE TABLE DriverVehicleAssignment (
        ID                  int            NOT NULL IDENTITY PRIMARY KEY,
        DriverGID           int            NOT NULL,
        CarTrailerID        varchar(10)    NOT NULL,
        Rola                nvarchar(30)   NOT NULL DEFAULT N'Glowny',
        DataOd              date           NOT NULL,
        DataDo              date           NULL,
        Powod               nvarchar(200)  NULL,
        Uwagi               nvarchar(500)  NULL,
        CreatedAtUTC        datetime2      NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedBy           nvarchar(64)   NULL,
        CONSTRAINT FK_DVA_Driver FOREIGN KEY (DriverGID) REFERENCES Driver(GID),
        CONSTRAINT FK_DVA_CarTrailer FOREIGN KEY (CarTrailerID) REFERENCES CarTrailer(ID)
    );
    CREATE INDEX IX_DVA_Active ON DriverVehicleAssignment(DriverGID, DataDo) WHERE DataDo IS NULL;
    CREATE INDEX IX_DVA_Vehicle ON DriverVehicleAssignment(CarTrailerID, DataDo) WHERE DataDo IS NULL;
    PRINT 'Created table DriverVehicleAssignment';
END
GO

-- 4. VehicleServiceLog (serwis, przeglady, tankowania)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'VehicleServiceLog')
BEGIN
    CREATE TABLE VehicleServiceLog (
        ID                  int            NOT NULL IDENTITY PRIMARY KEY,
        CarTrailerID        varchar(10)    NOT NULL,
        TypZdarzenia        nvarchar(30)   NOT NULL,
        Data                date           NOT NULL,
        DataNastepne        date           NULL,
        Opis                nvarchar(500)  NULL,
        KosztBrutto         decimal(10,2)  NULL,
        PrzebiegKm          int            NULL,
        LitryPaliwa         decimal(8,2)   NULL,
        CenaLitra           decimal(6,3)   NULL,
        Warsztat            nvarchar(100)  NULL,
        NrFaktury           nvarchar(50)   NULL,
        Uwagi               nvarchar(500)  NULL,
        CreatedAtUTC        datetime2      NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedBy           nvarchar(64)   NULL,
        CONSTRAINT FK_VSL_CarTrailer FOREIGN KEY (CarTrailerID) REFERENCES CarTrailer(ID)
    );
    CREATE INDEX IX_VSL_Vehicle_Data ON VehicleServiceLog(CarTrailerID, Data DESC);
    PRINT 'Created table VehicleServiceLog';
END
GO
