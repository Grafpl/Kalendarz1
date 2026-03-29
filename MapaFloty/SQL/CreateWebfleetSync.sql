-- Tabele synchronizacji kursów z Webfleet
-- Baza: TransportPL (192.168.0.109)
-- Tabele tworzą się automatycznie przy pierwszym użyciu

-- 1. Cache adresów klientów z geokodowaniem
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'KlientAdres')
CREATE TABLE KlientAdres (
    KodKlienta      varchar(100)    NOT NULL PRIMARY KEY,
    NazwaKlienta    nvarchar(200)   NULL,
    Ulica           nvarchar(200)   NULL,
    Miasto          nvarchar(100)   NULL,
    KodPocztowy     varchar(10)     NULL,
    Kraj            varchar(2)      NULL DEFAULT 'PL',
    Latitude        float           NULL,
    Longitude       float           NULL,
    GeokodowanyUTC  datetime2       NULL,
    ModifiedAtUTC   datetime2       NOT NULL DEFAULT SYSUTCDATETIME(),
    ModifiedBy      nvarchar(64)    NULL
);
GO

-- 2. Status synchronizacji kursów z Webfleet
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'WebfleetOrderSync')
CREATE TABLE WebfleetOrderSync (
    SyncID              int IDENTITY(1,1) PRIMARY KEY,
    KursID              bigint          NOT NULL,
    WebfleetOrderId     varchar(30)     NOT NULL,
    WebfleetObjectNo    varchar(20)     NULL,
    Status              varchar(30)     NOT NULL DEFAULT 'Oczekujacy',
    WyslaноUTC          datetime2       NULL,
    OdpowiedzKod        varchar(10)     NULL,
    OdpowiedzMsg        nvarchar(500)   NULL,
    IloscPrzystankow    int             NULL,
    CreatedAtUTC        datetime2       NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy           nvarchar(64)    NULL,
    CONSTRAINT UQ_WOS_Kurs UNIQUE (KursID)
);
CREATE INDEX IX_WOS_Status ON WebfleetOrderSync (Status);
GO
