-- ════════════════════════════════════════════════════════════════════
-- COLD CHAIN — warstwa CIĄGŁEGO MONITORINGU SONDAMI (na później)
-- Baza: LibraNet (192.168.0.109)
--
-- URUCHOM DOPIERO PO ZAKUPIE SOND PT1000 + gateway Modbus.
-- To NIEZALEŻNA warstwa od TemperaturyMiejsca (pomiary per partia).
-- Tu: ciągłe pomiary 24/7 z fizycznych punktów (parzelnik, chłodnia, mroźnia).
-- Kod odczytu Modbus: BAZA_WIEDZY/30_POMYSLY/09_Scalding_Monitor.md
-- ════════════════════════════════════════════════════════════════════

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'CCP_Punkt')
BEGIN
    CREATE TABLE dbo.CCP_Punkt (
        Id            INT IDENTITY(1,1) PRIMARY KEY,
        Kod           NVARCHAR(30)  NOT NULL UNIQUE,
        Nazwa         NVARCHAR(100) NOT NULL,
        LimitDolny    DECIMAL(8,2)  NULL,
        LimitGorny    DECIMAL(8,2)  NULL,
        Jednostka     NVARCHAR(10)  NOT NULL DEFAULT '°C',
        CzestotliwoscMin INT        NULL,
        Aktywny       BIT           NOT NULL DEFAULT 1
    );
    INSERT INTO dbo.CCP_Punkt (Kod, Nazwa, LimitDolny, LimitGorny, Jednostka, CzestotliwoscMin) VALUES
    ('CCP_PARZELNIK', 'Parzelnik (woda)',     50.0, 62.5, '°C', 30),
    ('CCP_SPINCHIL',  'Spin chiller (woda)',  0.0,  4.0,  '°C', 30),
    ('CCP_MROZNIA',   'Mroźnia',              -30.0,-18.0,'°C', 60);
    PRINT 'Utworzono CCP_Punkt (sondy)';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'CCP_Sonda')
BEGIN
    CREATE TABLE dbo.CCP_Sonda (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        PunktId INT NOT NULL FOREIGN KEY REFERENCES dbo.CCP_Punkt(Id),
        KodSondy NVARCHAR(30) NOT NULL UNIQUE,
        ModbusAdres INT NULL,
        DataKalibracji DATE NULL,
        DataNastepnejKalibracji DATE NULL,
        Aktywna BIT NOT NULL DEFAULT 1
    );
    PRINT 'Utworzono CCP_Sonda';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'CCP_Pomiar')
BEGIN
    CREATE TABLE dbo.CCP_Pomiar (
        Id BIGINT IDENTITY(1,1) PRIMARY KEY,
        PunktId INT NOT NULL FOREIGN KEY REFERENCES dbo.CCP_Punkt(Id),
        SondaId INT NULL FOREIGN KEY REFERENCES dbo.CCP_Sonda(Id),
        PomiarDateTime DATETIME NOT NULL DEFAULT GETDATE(),
        Wartosc DECIMAL(8,2) NOT NULL,
        Zrodlo NVARCHAR(20) NOT NULL DEFAULT 'AUTO'
    );
    CREATE INDEX IX_CCP_Pom_Punkt_Dt ON dbo.CCP_Pomiar(PunktId, PomiarDateTime);
    PRINT 'Utworzono CCP_Pomiar';
END
GO
