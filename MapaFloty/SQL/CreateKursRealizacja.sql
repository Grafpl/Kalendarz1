-- Tabela realizacji kursów — automatycznie wypełniana z analizy GPS
-- Baza: TransportPL (192.168.0.109)
-- Tworzy się automatycznie przy pierwszym użyciu

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'KursRealizacja')
BEGIN
    CREATE TABLE KursRealizacja (
        ID                  int IDENTITY(1,1) PRIMARY KEY,
        KursID              bigint          NOT NULL,
        LadunekID           bigint          NOT NULL,
        KodKlienta          varchar(100)    NOT NULL,
        NazwaKlienta        nvarchar(200)   NULL,
        KolejnoscPlan       int             NOT NULL,       -- zaplanowana kolejność
        KolejnoscFakt       int             NULL,           -- faktyczna kolejność dotarcia
        Status              varchar(30)     NOT NULL DEFAULT 'Oczekujacy',
                            -- Oczekujacy, WDrodze, Dotarl, Obsluzony, Pominiety
        CzasDotarcia        datetime2       NULL,
        CzasOdjazdu         datetime2       NULL,
        CzasPostojuMin      int             NULL,
        OdlegloscMinM       int             NULL,           -- minimalna odległość pojazdu od punktu (metry)
        LatKlient            float           NULL,
        LonKlient            float           NULL,
        LatDotarcie          float           NULL,           -- pozycja GPS przy dotarciu
        LonDotarcie          float           NULL,
        ModifiedAtUTC       datetime2       NOT NULL DEFAULT SYSUTCDATETIME()
    );
    CREATE INDEX IX_KR_Kurs ON KursRealizacja (KursID);
    CREATE INDEX IX_KR_Status ON KursRealizacja (Status);
END
GO
