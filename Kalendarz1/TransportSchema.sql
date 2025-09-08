/* =============================================
   SCHEMAT PANELU TRANSPORTU (LibraNet, serwer .109)
   -------------------------------------------------
   Uruchom jednokrotnie w bazie LibraNet.
   Zawiera tabele do planowania i realizacji kursów
   ciê¿arówek powi¹zanych z zamówieniami (ZamowieniaMieso).
   ============================================= */

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'TransportTrip')
BEGIN
    CREATE TABLE dbo.TransportTrip
    (
        TripID           BIGINT IDENTITY(1,1) PRIMARY KEY,
        TripDate         DATE            NOT NULL,                -- Dzieñ realizacji
        PlannedDeparture DATETIME2(0)    NULL,                    -- Planowany wyjazd z zak³adu
        PickupWindowFrom DATETIME2(0)    NULL,                    -- Okno odbioru OD (je¿eli dotyczy)
        PickupWindowTo   DATETIME2(0)    NULL,                    -- Okno odbioru DO
        DriverGID        INT             NULL,                    -- FK -> Driver.GID
        CarID            VARCHAR(32)     NULL,                    -- FK -> CarTrailer.ID (kind=1)
        TrailerID        VARCHAR(32)     NULL,                    -- FK -> CarTrailer.ID (kind=2)
        Status           VARCHAR(20)     NOT NULL DEFAULT('Planned') -- Planned/InProgress/Completed/Canceled
            CHECK (Status IN ('Planned','InProgress','Completed','Canceled')),
        Notes            NVARCHAR(2000)  NULL,
        CombineGroup     NVARCHAR(100)   NULL,                    -- Nazwa grupy ³¹czenia (np. "£¹czenie z Trip 105")
        CreatedAtUTC     DATETIME2(3)    NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedBy        NVARCHAR(64)    NOT NULL,
        ModifiedAtUTC    DATETIME2(3)    NULL,
        ModifiedBy       NVARCHAR(64)    NULL,
        RowVer           ROWVERSION      NOT NULL
    );
    CREATE INDEX IX_TransportTrip_Date ON dbo.TransportTrip(TripDate);
    CREATE INDEX IX_TransportTrip_Status ON dbo.TransportTrip(Status);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'TransportTripOrder')
BEGIN
    CREATE TABLE dbo.TransportTripOrder
    (
        TripOrderID   BIGINT IDENTITY(1,1) PRIMARY KEY,
        TripID        BIGINT        NOT NULL FOREIGN KEY REFERENCES dbo.TransportTrip(TripID) ON DELETE CASCADE,
        OrderID       INT           NOT NULL,            -- FK -> ZamowieniaMieso.Id (brak cascady – biznesowo zachowujemy spójnoœæ aplikacyjnie)
        SequenceNo    INT           NULL,                -- Kolejnoœæ zabierania / roz³adunku
        PlannedPickup DATETIME2(0)  NULL,                -- Dok³adniejszy plan odbioru per zamówienie (opcjonalnie)
        MergeNote     NVARCHAR(500) NULL,                -- Notatka ³¹czenia (np. z kim dzielony ch³ód)
        CreatedAtUTC  DATETIME2(3)  NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedBy     NVARCHAR(64)  NOT NULL,
        ModifiedAtUTC DATETIME2(3)  NULL,
        ModifiedBy    NVARCHAR(64)  NULL,
        RowVer        ROWVERSION    NOT NULL
    );
    CREATE UNIQUE INDEX UX_TransportTripOrder_Trip_Order ON dbo.TransportTripOrder(TripID, OrderID);
    CREATE INDEX IX_TransportTripOrder_OrderID ON dbo.TransportTripOrder(OrderID);
END
GO

/* =============================================
   WIDOK £¥CZ¥CY KURS + ZAMÓWIENIA (podgl¹dowy)
   ============================================= */
IF NOT EXISTS (SELECT 1 FROM sys.views WHERE name = 'vw_TransportTripWithOrders')
BEGIN
    EXEC('CREATE VIEW dbo.vw_TransportTripWithOrders AS \n'+
         'SELECT t.TripID, t.TripDate, t.Status, t.DriverGID, t.CarID, t.TrailerID, t.PlannedDeparture, '+
         '       o.TripOrderID, o.OrderID, o.SequenceNo, o.PlannedPickup, o.MergeNote '+
         'FROM dbo.TransportTrip t LEFT JOIN dbo.TransportTripOrder o ON t.TripID = o.TripID');
END
GO

/* =============================================
   PRZYK£ADOWE ROLE / UPRAWNIENIA (opcjonalnie) – dostosuj wg polityki
   GRANT SELECT, INSERT, UPDATE, DELETE ON dbo.TransportTrip TO someRole;
   GRANT SELECT, INSERT, UPDATE, DELETE ON dbo.TransportTripOrder TO someRole;
   ============================================= */
