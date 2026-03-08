-- ================================================================
-- Transport - tabela migawek zamowien (Snapshot for change detection)
-- Uruchom raz w SSMS na bazie TransportPL (192.168.0.109)
-- ================================================================

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'TransportOrderSnapshot')
CREATE TABLE TransportOrderSnapshot (
    ZamowienieId        INT PRIMARY KEY,
    KlientId            INT           NOT NULL,
    LiczbaPojemnikow    INT           NULL,
    LiczbaPalet         INT           NULL,
    DataZamowienia      DATETIME      NULL,
    DataUboju           DATETIME      NULL,
    Status              VARCHAR(50)   NULL,
    TransportStatus     VARCHAR(50)   NULL,
    TransportKursID     BIGINT        NULL,
    Uwagi               NVARCHAR(500) NULL,
    KlientNazwa         NVARCHAR(200) NULL,
    LastChecked         DATETIME      NOT NULL DEFAULT GETDATE()
);
GO
