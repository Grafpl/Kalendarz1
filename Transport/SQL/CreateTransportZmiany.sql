-- ================================================================
-- Transport - tabela zmian w zamowieniach (Approval System)
-- Uruchom raz w SSMS na bazie TransportPL (192.168.0.109)
-- ================================================================

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'TransportZmiany')
CREATE TABLE TransportZmiany (
    Id                  INT IDENTITY(1,1) PRIMARY KEY,
    ZamowienieId        INT           NOT NULL,
    KlientKod           VARCHAR(100)  NULL,
    KlientNazwa         NVARCHAR(200) NULL,
    TypZmiany           VARCHAR(50)   NOT NULL,
    -- Typy: 'NoweZamowienie', 'ZmianaIlosci', 'ZmianaTerminu',
    --       'Anulowanie', 'ZmianaStatusu', 'ZmianaPojemnikow', 'ZmianaUwag'
    Opis                NVARCHAR(500) NULL,
    StareWartosc        NVARCHAR(200) NULL,
    NowaWartosc         NVARCHAR(200) NULL,
    StatusZmiany        VARCHAR(20)   NOT NULL DEFAULT 'Oczekuje',
    -- Statusy: 'Oczekuje', 'Zaakceptowano', 'Odrzucono'
    ZgloszonePrzez      VARCHAR(50)   NOT NULL,
    DataZgloszenia      DATETIME      NOT NULL DEFAULT GETDATE(),
    ZaakceptowanePrzez  VARCHAR(50)   NULL,
    DataAkceptacji      DATETIME      NULL,
    Komentarz           NVARCHAR(500) NULL
);
GO

CREATE INDEX IX_TransportZmiany_Status ON TransportZmiany(StatusZmiany);
CREATE INDEX IX_TransportZmiany_Zamowienie ON TransportZmiany(ZamowienieId);
GO
