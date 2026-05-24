-- ════════════════════════════════════════════════════════════════════
-- COLD CHAIN HACCP — monitoring CCP (Critical Control Points)
-- Baza: LibraNet (192.168.0.109)
-- Pomysł #2 z BAZA_WIEDZY/30_POMYSLY
-- Tryb MANUALNY działa od razu (operator wpisuje temp).
-- Tryb AUTO po podłączeniu sond (Modbus → CCPService).
-- ════════════════════════════════════════════════════════════════════

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'CCP_Punkt')
BEGIN
    CREATE TABLE dbo.CCP_Punkt (
        Id            INT IDENTITY(1,1) PRIMARY KEY,
        Kod           NVARCHAR(30)  NOT NULL UNIQUE,  -- 'CCP_01_PARZELNIK'
        Nazwa         NVARCHAR(100) NOT NULL,
        TypPomiaru    NVARCHAR(20)  NOT NULL DEFAULT 'TEMP',  -- TEMP/PH/CHLOR
        LimitDolny    DECIMAL(8,2)  NULL,
        LimitGorny    DECIMAL(8,2)  NULL,
        Jednostka     NVARCHAR(10)  NOT NULL DEFAULT '°C',
        CzestotliwoscMin INT        NULL,             -- min. częstotliwość pomiaru
        OpisZasad     NVARCHAR(500) NULL,
        Aktywny       BIT           NOT NULL DEFAULT 1
    );

    INSERT INTO dbo.CCP_Punkt (Kod, Nazwa, TypPomiaru, LimitDolny, LimitGorny, Jednostka, CzestotliwoscMin, OpisZasad)
    VALUES
    ('CCP_01_PARZELNIK', 'Parzelnik (woda)',     'TEMP', 50.0, 62.5, '°C', 30, 'Niska 50-52, wysoka 60-62'),
    ('CCP_02_SPINCHIL',  'Spin chiller (woda)',  'TEMP', 0.0,  4.0,  '°C', 30, 'Poniżej 4°C'),
    ('CCP_03_CHILLING',  'Chłodnia (powietrze)', 'TEMP', 0.0,  4.0,  '°C', 60, 'Core <4°C w 6h'),
    ('CCP_04_COLDSTORE', 'Magazyn chłodniczy',   'TEMP', 0.0,  4.0,  '°C', 60, 'Stała 0-4°C'),
    ('CCP_05_MROZNIA',   'Mroźnia',              'TEMP', -30.0,-18.0,'°C', 60, 'Poniżej -18°C'),
    ('CCP_06_TRANSPORT', 'Transport ekspedycja', 'TEMP', 0.0,  4.0,  '°C', 60, 'Poniżej 4°C'),
    ('CCP_10_PREPACK',   'Przed pakowaniem (rdzeń)','TEMP', 0.0, 4.0, '°C', 60, 'Core <4°C');

    PRINT 'Utworzono CCP_Punkt + 7 domyślnych punktów';
END
ELSE PRINT 'CCP_Punkt już istnieje';
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'CCP_Sonda')
BEGIN
    CREATE TABLE dbo.CCP_Sonda (
        Id            INT IDENTITY(1,1) PRIMARY KEY,
        PunktId       INT NOT NULL FOREIGN KEY REFERENCES dbo.CCP_Punkt(Id),
        KodSondy      NVARCHAR(30) NOT NULL UNIQUE,
        NumerSeryjny  NVARCHAR(50) NULL,
        ProducentModel NVARCHAR(100) NULL,
        ModbusAdres   INT NULL,            -- adres rejestru (tryb AUTO)
        DataKalibracji DATE NULL,
        DataNastepnejKalibracji DATE NULL,
        Aktywna       BIT NOT NULL DEFAULT 1
    );
    PRINT 'Utworzono CCP_Sonda';
END
ELSE PRINT 'CCP_Sonda już istnieje';
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'CCP_Pomiar')
BEGIN
    CREATE TABLE dbo.CCP_Pomiar (
        Id            BIGINT IDENTITY(1,1) PRIMARY KEY,
        PunktId       INT NOT NULL FOREIGN KEY REFERENCES dbo.CCP_Punkt(Id),
        SondaId       INT NULL FOREIGN KEY REFERENCES dbo.CCP_Sonda(Id),
        PomiarDateTime DATETIME NOT NULL CONSTRAINT DF_CCP_Pom_Dt DEFAULT GETDATE(),
        Wartosc       DECIMAL(8,2) NOT NULL,
        Zrodlo        NVARCHAR(20) NOT NULL DEFAULT 'MANUALNY',  -- MANUALNY / AUTO
        OperatorId    NVARCHAR(50) NULL,
        Uwagi         NVARCHAR(300) NULL
    );
    CREATE INDEX IX_CCP_Pom_Punkt_Dt ON dbo.CCP_Pomiar(PunktId, PomiarDateTime);
    CREATE INDEX IX_CCP_Pom_Dt ON dbo.CCP_Pomiar(PomiarDateTime);
    PRINT 'Utworzono CCP_Pomiar';
END
ELSE PRINT 'CCP_Pomiar już istnieje';
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'CCP_Incydent')
BEGIN
    CREATE TABLE dbo.CCP_Incydent (
        Id            BIGINT IDENTITY(1,1) PRIMARY KEY,
        PunktId       INT NOT NULL FOREIGN KEY REFERENCES dbo.CCP_Punkt(Id),
        StartDateTime DATETIME NOT NULL,
        EndDateTime   DATETIME NULL,
        WartoscMin    DECIMAL(8,2) NULL,
        WartoscMax    DECIMAL(8,2) NULL,
        LimitDolny    DECIMAL(8,2) NULL,
        LimitGorny    DECIMAL(8,2) NULL,
        Priorytet     NVARCHAR(20) NOT NULL DEFAULT 'WYSOKI',
        StatusFinal   NVARCHAR(20) NOT NULL DEFAULT 'OTWARTY',  -- OTWARTY/ZAMKNIETY
        KorektaOpis   NVARCHAR(2000) NULL,
        KorektaPrzezId NVARCHAR(50) NULL,
        KorektaDateTime DATETIME NULL
    );
    CREATE INDEX IX_CCP_Inc_Status ON dbo.CCP_Incydent(StatusFinal);
    CREATE INDEX IX_CCP_Inc_Start ON dbo.CCP_Incydent(StartDateTime);
    PRINT 'Utworzono CCP_Incydent';
END
ELSE PRINT 'CCP_Incydent już istnieje';
GO
